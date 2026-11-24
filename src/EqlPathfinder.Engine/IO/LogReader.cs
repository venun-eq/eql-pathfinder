using System.Text;
using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.IO;

/// <summary>
/// Reads complete lines from a log the game is still writing. Never locks the file, never reads a
/// half-written line: the returned offset always sits just after the last newline consumed.
/// </summary>
public static class LogReader
{
    private const int BlockSize = 4 << 20;

    public static FileStream OpenShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan);

    /// <summary>Reads lines from <paramref name="offset"/> to the end. Returns the offset after the last complete line.</summary>
    public static long ReadFrom(string path, long offset, Action<string> onLine, IProgress<long>? progress = null,
        CancellationToken ct = default)
    {
        using var fs = OpenShared(path);
        if (offset > fs.Length) offset = 0;   // truncated or replaced: start over
        fs.Position = offset;
        var buf = new byte[BlockSize];
        int carry = 0;
        long pos = offset;   // file position of buf[0]
        var enc = Encoding.UTF8;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            int read = fs.Read(buf, carry, buf.Length - carry);
            if (read <= 0) break;
            int len = carry + read;
            int start = 0;
            while (true)
            {
                int nl = Array.IndexOf(buf, (byte)'\n', start, len - start);
                if (nl < 0) break;
                int end = nl;
                if (end > start && buf[end - 1] == (byte)'\r') end--;
                if (end > start) onLine(enc.GetString(buf, start, end - start));
                start = nl + 1;
            }
            pos += start;
            carry = len - start;
            if (carry > 0)
            {
                if (carry == buf.Length) { Array.Resize(ref buf, buf.Length * 2); }   // a giant line
                Buffer.BlockCopy(buf, start, buf, 0, carry);
            }
            progress?.Report(pos);
        }
        return pos;
    }

    /// <summary>Timestamp of the first line in the file, or null.</summary>
    public static DateTime? FirstTime(string path) => TimeAt(path, 0);

    /// <summary>Timestamp of the first complete line at or after <paramref name="position"/>.</summary>
    public static DateTime? TimeAt(string path, long position)
    {
        using var fs = OpenShared(path);
        return TimeAt(fs, position);
    }

    private static DateTime? TimeAt(FileStream fs, long position)
    {
        if (position >= fs.Length) return null;
        fs.Position = position;
        var buf = new byte[16384];
        int n = fs.Read(buf, 0, buf.Length);
        if (n <= 0) return null;
        var s = Encoding.UTF8.GetString(buf, 0, n);
        int i = 0;
        while (i < s.Length)
        {
            if (position > 0 || i > 0)
            {
                int k = s.IndexOf("\n[", i, StringComparison.Ordinal);
                if (k < 0) return null;
                i = k + 1;
            }
            if (s.Length - i >= 26 && LogLine.TryParseTime(s.AsSpan(i, 26), out var t)) return t;
            i++;
        }
        return null;
    }

    /// <summary>Binary search on timestamps: the offset of a line just before <paramref name="target"/>.</summary>
    public static long FindOffset(string path, DateTime target)
    {
        using var fs = OpenShared(path);
        long lo = 0, hi = fs.Length;
        var t0 = TimeAt(fs, 0);
        if (t0 == null || t0 >= target) return 0;
        while (hi - lo > 256 * 1024)
        {
            long mid = lo + (hi - lo) / 2;
            var t = TimeAt(fs, mid);
            if (t != null && t < target) lo = mid; else hi = mid;
        }
        // Back up to a line start.
        if (lo == 0) return 0;
        fs.Position = lo;
        var buf = new byte[65536];
        int n = fs.Read(buf, 0, buf.Length);
        int nl = Array.IndexOf(buf, (byte)'\n', 0, n);
        return nl < 0 ? lo : lo + nl + 1;
    }
}
