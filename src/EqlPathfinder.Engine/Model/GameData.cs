using System.Text.RegularExpressions;

namespace EqlPathfinder.Engine.Model;

public sealed record Passive(string Name, string Ability, string Description);

/// <summary>Text read from the game's own files, so nothing about passives is hardcoded.</summary>
public static class GameData
{
    /// <summary>
    /// Reincarnation passives from dbstr_us.txt (ids 26000-26299): key = class abbreviation, race name,
    /// or "Completionist". The amounts (#1, #2 …) aren't in any of the game's files the app can read (the server sends
    /// them), so <see cref="PassiveText"/> says what each passive does without them.
    /// </summary>
    public static Dictionary<string, Passive> LoadReincarnationPassives(string installDir)
    {
        var result = new Dictionary<string, Passive>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(installDir, "dbstr_us.txt");
        if (!File.Exists(path)) return result;
        var names = new Dictionary<int, string>();
        var abilities = new Dictionary<int, string>();
        var descs = new Dictionary<int, string>();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var r = new StreamReader(fs);
        while (r.ReadLine() is { } line)
        {
            if (line.Length < 8 || line[0] != '2' || line[1] != '6' || line[5] != '^') continue;
            var p = line.Split('^');
            if (p.Length < 3 || !int.TryParse(p[0], out int id) || id < 26000 || id >= 26300) continue;
            switch (p[1])
            {
                case "1": names[id] = p[2]; break;
                case "2": abilities[id] = p[2]; break;
                case "4": descs[id] = p[2]; break;
            }
        }
        foreach (var (id, name) in names)
        {
            string key;
            if (name.StartsWith("Class Reincarnation: ")) key = ClassNames.Abbrev(name[21..]);
            else if (name.StartsWith("Race Reincarnation: ")) key = name[20..];
            else if (name == "Reincarnation: Completionist") key = "Completionist";
            else continue;
            result[key] = new Passive(name, abilities.GetValueOrDefault(id, ""), PassiveText(descs.GetValueOrDefault(id, "")));
        }
        return result;
    }

    /// <summary>
    /// Strips server-filled placeholder values from passive descriptions: "This passive ability reduces incoming
    /// melee damage by #1%." becomes "Reduces incoming melee damage."
    /// </summary>
    public static string PassiveText(string raw)
    {
        var s = raw.Replace("<br>", " ");
        s = Regex.Replace(s, "<c [^>]*>|</c>", "");               // colour tags hold "#66CCFF"
        s = Regex.Replace(s, @"\s+by #\d+(%| units| points)?", "");  // "by #1%", "by #1 units"
        s = Regex.Replace(s, @"\ban additional #\d+ points\b", "additional points");
        s = Regex.Replace(s, @"\s*#\d+(%| units| points)?", "");     // anything else: "an additional #1% chance"
        s = Regex.Replace(s, @"\s{2,}", " ").Trim();
        // "This passive ability will increase …" → "Increases …"; "This passive ability reduces …" → "Reduces …".
        var m = Regex.Match(s, @"^This passive ability (will )?(\w)(\w*)");
        if (m.Success)
            s = char.ToUpperInvariant(m.Groups[2].Value[0]) + m.Groups[3].Value + (m.Groups[1].Success ? "s" : "") + s[m.Length..];
        return s;
    }

    /// <summary>
    /// Which classes can cast each spell, from spells_us.txt: name (rank numerals stripped, as damage lines
    /// name it) → bit mask over <see cref="ClassNames.All"/> (bit 0 = WAR). Class levels are columns 36–51;
    /// 0 or 255 means the class can't. Empty when the file is missing.
    /// </summary>
    public static Dictionary<string, int> LoadSpellClasses(string installDir)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(installDir, "spells_us.txt");
        if (!File.Exists(path)) return result;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var r = new StreamReader(fs);
        while (r.ReadLine() is { } line)
        {
            var p = line.Split('^');
            if (p.Length < 52) continue;
            int mask = 0;
            for (int i = 0; i < 16; i++)
                if (int.TryParse(p[36 + i], out var lv) && lv is > 0 and < 255) mask |= 1 << i;
            if (mask == 0) continue;
            var name = Parsing.LineParser.StripRank(p[1]);
            result[name] = result.GetValueOrDefault(name) | mask;
        }
        return result;
    }

    /// <summary>The install folder is the parent of the Logs folder.</summary>
    public static string? InstallDirFromLogs(string logsDir) => Directory.GetParent(logsDir)?.FullName;
}
