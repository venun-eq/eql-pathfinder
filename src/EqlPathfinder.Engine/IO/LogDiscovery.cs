using System.Text.RegularExpressions;

namespace EqlPathfinder.Engine.IO;

/// <summary>One character's logs: the live file plus any archived copies (eqlog_Name_server.txt.bak, .2026-10-14.txt).</summary>
public sealed record CharacterLogs(string Name, string Server, string LivePath, IReadOnlyList<string> Files)
{
    public string Key => $"{Name}_{Server}";
}

public static partial class LogDiscovery
{
    /// <summary>Default EverQuest Legends install folders, checked in order.</summary>
    public static IEnumerable<string> CandidateInstallDirs()
    {
        var pub = Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public";
        yield return Path.Combine(pub, "Daybreak Game Company", "Installed Games", "EverQuest Legends");
        foreach (var drive in new[] { "C", "D", "E" })
        {
            yield return $@"{drive}:\Users\Public\Daybreak Game Company\Installed Games\EverQuest Legends";
            yield return $@"{drive}:\Daybreak Game Company\Installed Games\EverQuest Legends";
            yield return $@"{drive}:\Program Files (x86)\Steam\steamapps\common\EverQuest Legends";
            yield return $@"{drive}:\SteamLibrary\steamapps\common\EverQuest Legends";
        }
    }

    public static string? FindLogsDir(IEnumerable<string>? extraInstallDirs = null)
    {
        var dirs = (extraInstallDirs ?? []).Concat(CandidateInstallDirs());
        foreach (var d in dirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var logs = Path.Combine(d, "Logs");
            if (Directory.Exists(logs) && Directory.EnumerateFiles(logs, "eqlog_*").Any()) return logs;
        }
        return null;
    }

    /// <summary>Characters with a log in <paramref name="logsDir"/>, most recently active first.</summary>
    public static IReadOnlyList<CharacterLogs> FindCharacters(string logsDir)
    {
        var groups = new Dictionary<string, (string name, string server, List<string> files)>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.EnumerateFiles(logsDir, "eqlog_*"))
        {
            var m = FileRx().Match(Path.GetFileName(f));
            if (!m.Success) continue;
            var key = m.Groups["n"].Value + "_" + m.Groups["s"].Value;
            if (!groups.TryGetValue(key, out var g)) groups[key] = g = (m.Groups["n"].Value, m.Groups["s"].Value, new());
            g.files.Add(f);
        }
        var result = new List<(CharacterLogs c, DateTime w)>();
        foreach (var (_, g) in groups)
        {
            var live = Path.Combine(logsDir, $"eqlog_{g.name}_{g.server}.txt");
            var ordered = g.files
                .Select(f => (f, t: SafeFirstTime(f)))
                .Where(x => x.t != null)
                .OrderBy(x => x.t)
                .Select(x => x.f)
                .ToList();
            if (!File.Exists(live) && ordered.Count == 0) continue;
            result.Add((new CharacterLogs(g.name, g.server, live, ordered), File.Exists(live) ? File.GetLastWriteTime(live) : DateTime.MinValue));
        }
        return result.OrderByDescending(x => x.w).Select(x => x.c).ToList();
    }

    private static DateTime? SafeFirstTime(string f)
    {
        try { return LogReader.FirstTime(f); } catch (IOException) { return null; }
    }

    // eqlog_Venun_oggok.txt, eqlog_Venun_oggok.txt.bak, eqlog_Venun_oggok.2026-10-14.txt
    [GeneratedRegex(@"^eqlog_(?<n>[A-Za-z]+)_(?<s>[A-Za-z0-9]+)\..+$")]
    private static partial Regex FileRx();
}
