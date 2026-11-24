using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.RegularExpressions;
using EqlPathfinder.Engine.Pages;

namespace EqlPathfinder.Engine.Usage;

/// <summary>
/// The app's usage counters (docs/usage.md), on .NET's own metrics API (<see cref="System.Diagnostics.Metrics"/>), which
/// OpenTelemetry reads as it is: adding an OpenTelemetry exporter later is <c>AddMeter(UsageMetrics.MeterName)</c>, no
/// change here. Today <see cref="UsageRecorder"/> listens, adds them up per day and the app sends one summary.
/// Everything recorded is scrubbed here, before any listener sees it: names are short words, never paths or player text.
/// </summary>
public sealed class UsageMetrics
{
    public const string MeterName = "EqlPathfinder";

    /// <summary>The app's instruments. Tests make their own on a meter of their own, so nothing else lands in theirs.</summary>
    public static UsageMetrics Default { get; } = new(new Meter(MeterName, "1"));

    public Meter Meter { get; }
    private readonly Counter<long> _pages, _asks, _features, _lifecycle, _minutes, _errors;
    private readonly Histogram<double> _importSeconds, _importMegabytes;

    public UsageMetrics(Meter meter)
    {
        Meter = meter;
        _pages = meter.CreateCounter<long>("pathfinder.pages", description: "Pages opened in the main window");
        _asks = meter.CreateCounter<long>("pathfinder.ask", description: "Ask questions, and whether the shared numbers could answer");
        _features = meter.CreateCounter<long>("pathfinder.features", description: "Features used");
        _lifecycle = meter.CreateCounter<long>("pathfinder.lifecycle", description: "Starts, first runs and updates");
        _minutes = meter.CreateCounter<long>("pathfinder.minutes", "min", "Minutes running, with and without the game");
        _errors = meter.CreateCounter<long>("pathfinder.errors", description: "Errors: what Pathfinder was doing, the exception type, where in Pathfinder");
        _importSeconds = meter.CreateHistogram<double>("pathfinder.import.duration", "s", "Reading a character's history");
        _importMegabytes = meter.CreateHistogram<double>("pathfinder.import.size", "MB", "Log size read");
    }

    public void Page(string page) => _pages.Add(1, Tag("page", page));

    /// <param name="pool">true: the shared numbers answered; false: not enough players there (or none loaded); null: the question doesn't use them.</param>
    public void Ask(string question, bool? pool) =>
        _asks.Add(1, Tag("q", question), Tag("pool", pool switch { true => "pool", false => "nopool", null => "na" }));

    public void Feature(string name) => _features.Add(1, Tag("feature", name));

    /// <summary>start, firstrun, updated, seen (an existing copy reporting for the first time).</summary>
    public void Life(string evt) => _lifecycle.Add(1, Tag("event", evt));

    public void Minute(bool gameRunning) => _minutes.Add(1, Tag("game", gameRunning ? "game" : "nogame"));

    public void Error(string what, Exception ex) =>
        _errors.Add(1, new("kind", ErrorScrub.Kind(what)), new("type", ErrorScrub.Type(ex)), new("at", ErrorScrub.Where(ex)));

    public void Import(double seconds, double megabytes)
    {
        _importSeconds.Record(seconds);
        _importMegabytes.Record(megabytes);
    }

    private static KeyValuePair<string, object?> Tag(string key, string value) => new(key, UsageKeys.Word(value));

    /// <summary>
    /// Whether the shared numbers answered an Ask question: the route the players who share would take, their farms, their
    /// difficulty pick, their typical damage. False also when no shared numbers are loaded. Null for Every place.
    /// </summary>
    public static bool? PoolAnswered(AskAnswer a) => a.Q switch
    {
        "level" => a.Route.Community != null,
        "farm" => a.Farm.Community is { Count: > 0 } || a.Farm.CommunityAllTime is { Count: > 0 },
        "diff" => a.Difficulty.Community != null,
        "dmg" => a.Damage.Rows.Any(r => r.Typical != null),
        _ => null,
    };
}

/// <summary>How a measurement becomes a counter's name in the day's summary ("page.ask", "ask.level.nopool").</summary>
public static class UsageKeys
{
    private static readonly Dictionary<string, (string Prefix, string[] Tags)> Map = new()
    {
        ["pathfinder.pages"] = ("page", ["page"]),
        ["pathfinder.ask"] = ("ask", ["q", "pool"]),
        ["pathfinder.features"] = ("feature", ["feature"]),
        ["pathfinder.lifecycle"] = ("life", ["event"]),
        ["pathfinder.minutes"] = ("minutes", ["game"]),
        ["pathfinder.import.duration"] = ("import.seconds", []),
        ["pathfinder.import.size"] = ("import.mb", []),
    };

    /// <summary>The summary key for an instrument and its tags, or null for one the summary doesn't keep.</summary>
    public static string? For(string instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (!Map.TryGetValue(instrument, out var m)) return null;
        var parts = new List<string>(m.Tags.Length + 1) { m.Prefix };
        foreach (var name in m.Tags)
        {
            string? value = null;
            foreach (var t in tags) if (t.Key == name) { value = t.Value as string; break; }
            parts.Add(Word(value));
        }
        return string.Join('.', parts);
    }

    /// <summary>A short lowercase word: letters, digits, '-' and '_' only, at most 32 characters ("none" when nothing is left).</summary>
    public static string Word(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "none";
        var w = new string(s.ToLowerInvariant().Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_').Take(32).ToArray());
        return w.Length > 0 ? w : "none";
    }
}

/// <summary>
/// What an error report carries, scrubbed: what Pathfinder was doing (its own label, cut before anything that could be a
/// path, address or name), the exception's type, and the first frame inside Pathfinder as Type.Method. Never the message
/// (it can hold paths, a character or a Windows user name) and never file names or line numbers.
/// </summary>
public static partial class ErrorScrub
{
    public static string Kind(string? what)
    {
        if (string.IsNullOrWhiteSpace(what)) return "other";
        var s = what.ToLowerInvariant();
        int cut = 0;
        while (cut < s.Length && (s[cut] is (>= 'a' and <= 'z') or ' ' or '-' or '\'')) cut++;
        s = Spaces().Replace(s[..cut], " ").Trim();
        // "update feed https://…" or "… C:\Users\…" stops at the colon: drop the scheme or drive letter left behind.
        s = Leftover().Replace(s, "").Trim();
        if (s.Length > 40) s = s[..40].TrimEnd();
        return s.Length > 0 ? s : "other";
    }

    public static string Type(Exception ex)
    {
        var t = ex.GetBaseException().GetType();
        var name = t.FullName ?? t.Name;
        return name.Length > 100 ? name[..100] : name;
    }

    public static string Where(Exception ex)
    {
        var frames = new StackTrace(ex.GetBaseException(), fNeedFileInfo: false).GetFrames();
        if (frames.Length == 0) frames = new StackTrace(ex, fNeedFileInfo: false).GetFrames();
        var names = frames.Select(f => f.GetMethod()).Where(m => m != null).Select(m => Name(m!)).ToList();
        var ours = names.FirstOrDefault(n => n.StartsWith("App.", StringComparison.Ordinal) || n.StartsWith("Engine.", StringComparison.Ordinal));
        var at = ours ?? names.FirstOrDefault() ?? "unknown";
        return at.Length > 160 ? at[..160] : at;
    }

    /// <summary>"Engine.Live.TrackerService.ImportCoreAsync" for a method, with async state machines and lambdas named after the method they're in.</summary>
    private static string Name(MethodBase m)
    {
        var type = m.DeclaringType;
        string method = m.Name;
        // Compiler-made types: "<SendAsk>d__12" (async), "<>c" / "<>c__DisplayClass5_0" (lambdas) sit inside the real type.
        while (type != null && type.Name.StartsWith('<') && type.DeclaringType != null)
        {
            if (Inner().Match(type.Name) is { Success: true } tm && tm.Groups[1].Value.Length > 0) method = tm.Groups[1].Value;
            type = type.DeclaringType;
        }
        if (Inner().Match(method) is { Success: true } mm && mm.Groups[1].Value.Length > 0) method = mm.Groups[1].Value + "+lambda";
        var full = type?.FullName ?? "?";
        if (full.StartsWith("EqlPathfinder.", StringComparison.Ordinal)) full = full["EqlPathfinder.".Length..];
        full = full.Replace('+', '.');
        return full + "." + method;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"(?:^|\s+)(?:[a-z]|https?)$")]
    private static partial Regex Leftover();

    [GeneratedRegex(@"^<([^>]*)>")]
    private static partial Regex Inner();
}
