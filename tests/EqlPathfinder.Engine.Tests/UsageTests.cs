using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using EqlPathfinder.Engine.Usage;

namespace EqlPathfinder.Engine.Tests;

public class UsageTests
{
    private static readonly UsageContext Who = new("0123456789abcdef0123456789abcdef", "stable", "10.0.26300.0", "2026-11-17");

    /// <summary>Each test listens to a meter of its own, so the app's own counters (and other tests) never land in it.</summary>
    private static (UsageMetrics m, UsageRecorder r, Func<DateTime> clock, Action<DateTime> set) Make(string? path = null, [CallerMemberName] string name = "")
    {
        var now = new DateTime(2026, 11, 17, 10, 0, 0);
        var m = new UsageMetrics(new Meter("test-" + name + "-" + Guid.NewGuid().ToString("N")));
        Func<DateTime> clock = () => now;
        var r = new UsageRecorder(path, m, clock);
        r.StartVersion("0.9.28");
        return (m, r, clock, t => now = t);
    }

    [Fact]
    public void Counters_add_up_by_key()
    {
        var (m, r, _, _) = Make();
        m.Page("ask"); m.Page("ask"); m.Page("Home");
        m.Ask("level", true); m.Ask("level", false); m.Ask("level", false); m.Ask("places", null);
        m.Feature("upgrade");
        m.Minute(true); m.Minute(false); m.Minute(false);
        m.Life("start");
        var d = Assert.Single(r.Days);
        Assert.Equal(2, d.Counts["page.ask"]);
        Assert.Equal(1, d.Counts["page.home"]);
        Assert.Equal(1, d.Counts["ask.level.pool"]);
        Assert.Equal(2, d.Counts["ask.level.nopool"]);
        Assert.Equal(1, d.Counts["ask.places.na"]);
        Assert.Equal(1, d.Counts["feature.upgrade"]);
        Assert.Equal(1, d.Counts["minutes.game"]);
        Assert.Equal(2, d.Counts["minutes.nogame"]);
        Assert.Equal(1, d.Counts["life.start"]);
        Assert.Equal("0.9.28", d.App);
    }

    [Fact]
    public void Imports_keep_count_sum_and_max()
    {
        var (m, r, _, _) = Make();
        m.Import(12.5, 850);
        m.Import(3.25, 120);
        var d = Assert.Single(r.Days);
        Assert.Equal(2, d.Stats["import.seconds"].N);
        Assert.Equal(15.75, d.Stats["import.seconds"].Sum, 3);
        Assert.Equal(12.5, d.Stats["import.seconds"].Max, 3);
        Assert.Equal(970, d.Stats["import.mb"].Sum, 3);
    }

    [Fact]
    public void Words_are_scrubbed_to_short_safe_names()
    {
        Assert.Equal("ask", UsageKeys.Word("Ask"));
        Assert.Equal("cusersbobappdata", UsageKeys.Word(@"C:\Users\Bob\AppData"));
        Assert.Equal("none", UsageKeys.Word(""));
        Assert.Equal("none", UsageKeys.Word("..."));
        Assert.Equal(32, UsageKeys.Word(new string('a', 100)).Length);
    }

    [Theory]
    [InlineData("update feed https://pub-95ff.r2.dev", "update feed")]
    [InlineData(@"update feed C:\Users\Bob\feed", "update feed")]
    [InlineData("sharing: upload", "sharing")]
    [InlineData("share key unreadable (another Windows user, or settings copied from another PC)", "share key unreadable")]
    [InlineData("main window message", "main window message")]
    [InlineData("", "other")]
    [InlineData("12345", "other")]
    public void Error_labels_stop_before_anything_that_could_be_a_path(string what, string kind) =>
        Assert.Equal(kind, ErrorScrub.Kind(what));

    [Fact]
    public void Errors_carry_type_and_where_never_the_message_or_a_path()
    {
        var (m, r, _, _) = Make();
        var ex = Throw(() => ThrowFromHere(@"Could not find C:\Users\Bob\AppData\Local\EQL Pathfinder\usage.json for Venun"));
        m.Error("opening database", ex);
        m.Error("opening database", ex);
        var e = Assert.Single(Assert.Single(r.Days).Errors);
        Assert.Equal("opening database", e.Kind);
        Assert.Equal("System.IO.FileNotFoundException", e.Type);
        Assert.Equal(2, e.N);
        Assert.EndsWith("UsageTests.ThrowFromHere", e.At);
        var json = UsageRecorder.ToJson(r.Preview(Who));
        Assert.DoesNotContain("Bob", json);
        Assert.DoesNotContain("Venun", json);
        Assert.DoesNotContain(":\\\\", json);
        Assert.DoesNotContain(".cs", json);
    }

    [Fact]
    public void Async_frames_are_named_after_their_method()
    {
        var ex = Throw(() => ThrowAsync().GetAwaiter().GetResult());
        Assert.EndsWith("UsageTests.ThrowAsync", ErrorScrub.Where(ex));
    }

    [Fact]
    public void Errors_past_the_cap_only_count()
    {
        var (m, r, _, _) = Make();
        for (int i = 0; i < UsageRecorder.MaxErrors + 5; i++) m.Error("kind" + new string('x', i), new InvalidOperationException());
        var d = Assert.Single(r.Days);
        Assert.Equal(UsageRecorder.MaxErrors, d.Errors.Count);
        Assert.Equal(5, d.Counts["error.more"]);
    }

    [Fact]
    public void Days_roll_over_and_a_sent_day_is_dropped_once_finished()
    {
        var (m, r, _, set) = Make();
        m.Page("home");
        set(new DateTime(2026, 11, 18, 9, 0, 0));
        m.Page("ask");
        var pending = r.Pending(Who);
        Assert.Equal(["2026-11-17", "2026-11-18"], pending.Select(p => p.Payload.Day));
        foreach (var (p, h) in pending) r.MarkSent(p.Day, h);
        Assert.Equal("2026-11-18", Assert.Single(r.Days).Day);   // yesterday is gone; today stays to keep counting
        Assert.Empty(r.Pending(Who));                             // nothing new since it was sent
        m.Page("ask");
        var again = Assert.Single(r.Pending(Who));
        Assert.Equal(2, again.Payload.Counts["page.ask"]);
    }

    [Fact]
    public void Unsent_days_older_than_two_weeks_are_dropped()
    {
        var (m, r, _, set) = Make();
        m.Page("home");
        set(new DateTime(2026, 12, 5, 9, 0, 0));
        m.Page("ask");
        Assert.Equal(["2026-12-05"], r.Pending(Who).Select(p => p.Payload.Day));
    }

    [Fact]
    public void Updates_and_state_are_noted_on_the_day()
    {
        var (m, r, _, _) = Make();
        Assert.Equal("0.9.28", r.StartVersion("0.9.29"));
        r.SetState(new Dictionary<string, string> { ["sharing"] = "on", ["Overlays"] = "crawl,meter" });
        var p = r.Preview(Who);
        Assert.Equal("0.9.29", p.App);
        Assert.Equal("0.9.28", p.UpdatedFrom);
        Assert.Equal("on", p.State["sharing"]);
        Assert.Equal("crawl,meter", p.State["overlays"]);
    }

    [Fact]
    public void Kept_in_a_file_until_sent_and_gone_when_cleared()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eqlp-usage-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "usage.json");
        try
        {
            var (m, r, _, _) = Make(path);
            m.Page("ask");
            m.Import(4, 100);
            r.Dispose();
            Assert.True(File.Exists(path));
            var (_, r2, _, _) = Make(path);
            var d = Assert.Single(r2.Days);
            Assert.Equal(1, d.Counts["page.ask"]);
            Assert.Equal(1, d.Stats["import.seconds"].N);
            Assert.Equal("0.9.28", r2.LastVersion);
            r2.Clear();
            Assert.False(File.Exists(path));
            Assert.Empty(r2.Days);
            r2.Dispose();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void The_summary_is_small_camel_case_json_with_only_known_fields()
    {
        var (m, r, _, _) = Make();
        m.Page("ask");
        m.Import(4, 100);
        m.Error("ask", new InvalidOperationException("secret"));
        var json = UsageRecorder.ToJson(r.Preview(Who));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(["v", "id", "day", "app", "channel", "os", "firstSeen", "updatedFrom", "counts", "stats", "state", "errors"],
            doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, doc.RootElement.GetProperty("v").GetInt32());
        Assert.Equal(4, doc.RootElement.GetProperty("stats").GetProperty("import.seconds").GetProperty("sum").GetDouble());
        Assert.DoesNotContain("secret", json);
        Assert.True(json.Length < 2000, json);
    }

    private static Exception Throw(Action a)
    {
        try { a(); }
        catch (Exception ex) { return ex; }
        throw new InvalidOperationException("didn't throw");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowFromHere(string message) => throw new FileNotFoundException(message);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task ThrowAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("async");
    }
}
