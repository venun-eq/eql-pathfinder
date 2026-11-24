using System.Diagnostics;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The one retry a page test run allows (docs/specs/page-tests-steady.md): a browser that hung with no renderer process
/// left (Edge's known stall under a low priority) is run once more with a fresh slot and profile. Each retry is logged
/// to stdout and to a kept file, and the run goes red when more than <see cref="Max"/> browsers needed one. A page that
/// really hangs keeps its renderer and is never retried.
/// </summary>
internal sealed class PageRetries(string logFile, int max = 2)
{
    public const string Marker = "PAGE-RETRY";
    private static readonly object Writing = new();
    private int used;

    public int Max { get; } = max;
    public int Used => Volatile.Read(ref used);

    /// <summary>The run's own: artifacts\page-retries.log at the repository root, appended to and never emptied.</summary>
    public static PageRetries Shared { get; } = new(DefaultLog());

    /// <summary>Takes one of the run's retries. False when they're all used: that browser stays red.</summary>
    public bool TryTake() => Interlocked.Increment(ref used) <= Max;

    /// <summary>One line with the marker, who and the whole dump, to stdout and the kept file.</summary>
    public void Log(string who, string dump)
    {
        var line = $"{Marker} {DateTime.Now:yyyy-MM-dd HH:mm:ss} {who} retry {Math.Min(Used, Max)} of {Max}: " + dump.Replace("\r", "").Replace("\n", " | ");
        Console.Out.WriteLine(line);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
            lock (Writing) File.AppendAllText(logFile, line + Environment.NewLine);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>The test that asked: the first frame in this assembly whose class is a page test class or its fixture ("Pages"). After an await the frame is the compiler's nested "d__n" class: its DeclaringType is the real class, and the method is named inside the angle brackets.</summary>
    public static string Who(string page)
    {
        foreach (var f in new StackTrace().GetFrames())
        {
            var m = f.GetMethod();
            var t = m?.DeclaringType;
            var cls = t?.DeclaringType ?? t;
            if (cls != null && cls.Assembly == typeof(PageRetries).Assembly
                && (cls.Name.EndsWith("Tests", StringComparison.Ordinal) || cls.Name.EndsWith("Pages", StringComparison.Ordinal)))
            {
                var name = m!.Name;
                if (t!.DeclaringType != null && t.Name.StartsWith('<') && t.Name.IndexOf('>') > 1) name = t.Name[1..t.Name.IndexOf('>')];
                return cls.Name + "." + name + " (" + page + ")";
            }
        }
        return "(test not found) (" + page + ")";
    }

    private static string DefaultLog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "EQLPathfinder.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? Path.GetTempPath(), "artifacts", "page-retries.log");
    }
}
