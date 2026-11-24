using System.Diagnostics;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Dev work on this PC runs pinned to the E-cores and below normal so the game keeps the machine (docs/developing-while-playing.md).
/// The harness follows what it is given: the browser slots count the cores this run may use, and are fewer while the game runs.
/// </summary>
internal static class PageLimits
{
    /// <summary>Half the cores the run may use, at least 2; about 4 while the game runs.</summary>
    public static int SlotCount(int allowed, bool game)
    {
        var n = Math.Max(2, allowed / 2);
        return game ? Math.Min(n, 4) : n;
    }

    public static int AllowedProcessors()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) return Environment.ProcessorCount;
        try
        {
            using var me = Process.GetCurrentProcess();
            return Math.Max(1, System.Numerics.BitOperations.PopCount((ulong)(long)me.ProcessorAffinity));
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException) { return Environment.ProcessorCount; }
    }

    public static bool GameRunning()
    {
        try
        {
            var found = Process.GetProcessesByName("eqgame");
            foreach (var p in found) p.Dispose();
            return found.Length > 0;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    public static int Slots() => SlotCount(AllowedProcessors(), GameRunning());

    /// <summary>
    /// Edge inherits memory and disk priority from whoever starts it, and the hourly task's "priority 7" makes both low,
    /// under which the first dozen Edges stalled (docs/specs/page-tests-steady.md). Only those two are raised to normal;
    /// the CPU class and affinity stay as inherited, because those are what keeps the game's cores free. A run that may not is left as it is.
    /// </summary>
    public static void RaiseToNormal()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var me = Process.GetCurrentProcess();
            // NtSetInformationProcess: 33 is the disk priority (2 normal), 39 the memory priority (5 normal).
            foreach (var (kind, normal) in new[] { (33, 2), (39, 5) })
            {
                if (NtQueryInformationProcess(me.Handle, kind, out var now, 4, out _) == 0 && now < normal)
                {
                    var value = normal;
                    NtSetInformationProcess(me.Handle, kind, ref value, 4);
                }
            }
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException) { }
    }

    public static void SetMemoryPriority(int v)
    {
        using var me = Process.GetCurrentProcess();
        NtSetInformationProcess(me.Handle, 39, ref v, 4);
    }

    public static int MemoryPriority()
    {
        using var me = Process.GetCurrentProcess();
        return NtQueryInformationProcess(me.Handle, 39, out var now, 4, out _) == 0 ? now : -1;
    }

    public static void SetDiskPriority(int v)
    {
        using var me = Process.GetCurrentProcess();
        NtSetInformationProcess(me.Handle, 33, ref v, 4);
    }

    public static int DiskPriority()
    {
        using var me = Process.GetCurrentProcess();
        return NtQueryInformationProcess(me.Handle, 33, out var now, 4, out _) == 0 ? now : -1;
    }

    [System.Runtime.InteropServices.DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr process, int kind, out int value, int size, out int returned);

    [System.Runtime.InteropServices.DllImport("ntdll.dll")]
    private static extern int NtSetInformationProcess(IntPtr process, int kind, ref int value, int size);
}
