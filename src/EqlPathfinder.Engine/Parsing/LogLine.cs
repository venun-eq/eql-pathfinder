namespace EqlPathfinder.Engine.Parsing;

/// <summary>
/// An EverQuest log line: "[Sat Nov 14 10:54:15 2026] body". Timestamps are local game-client time.
/// </summary>
public readonly record struct LogLine(DateTime Time, string Body)
{
    public const int BodyStart = 27;

    private static readonly string[] Months =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>Parses the bracketed timestamp by position; much faster than DateTime.ParseExact.</summary>
    public static bool TryParse(string line, out LogLine result)
    {
        result = default;
        if (!TryParseTime(line, out var t)) return false;
        result = new LogLine(t, line.Length > BodyStart ? line.Substring(BodyStart) : string.Empty);
        return true;
    }

    public static bool TryParseTime(ReadOnlySpan<char> line, out DateTime time)
    {
        time = default;
        // [Ddd Mmm dd HH:mm:ss yyyy]
        // 0    5   9  12       21  25
        if (line.Length < 26 || line[0] != '[' || line[25] != ']') return false;
        int month = MonthIndex(line.Slice(5, 3));
        if (month == 0) return false;
        if (!TwoDigits(line, 9, out int day) || !TwoDigits(line, 12, out int hh) ||
            !TwoDigits(line, 15, out int mm) || !TwoDigits(line, 18, out int ss)) return false;
        if (!TwoDigits(line, 21, out int y1) || !TwoDigits(line, 23, out int y2)) return false;
        int year = y1 * 100 + y2;
        if (day < 1 || day > 31 || hh > 23 || mm > 59 || ss > 59) return false;
        try { time = new DateTime(year, month, day, hh, mm, ss, DateTimeKind.Unspecified); }
        catch (ArgumentOutOfRangeException) { return false; }
        return true;
    }

    private static int MonthIndex(ReadOnlySpan<char> m)
    {
        for (int i = 0; i < Months.Length; i++)
            if (m.SequenceEqual(Months[i])) return i + 1;
        return 0;
    }

    private static bool TwoDigits(ReadOnlySpan<char> s, int at, out int value)
    {
        char a = s[at], b = s[at + 1];
        // Day of month may be space-padded in some clients.
        if (a == ' ') a = '0';
        if (a < '0' || a > '9' || b < '0' || b > '9') { value = 0; return false; }
        value = (a - '0') * 10 + (b - '0');
        return true;
    }
}
