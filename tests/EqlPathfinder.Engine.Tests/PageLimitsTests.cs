using System.Diagnostics;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Dev work is pinned and below normal so the game keeps the machine (docs/developing-while-playing.md); the harness mustn't undo that.</summary>
public class PageLimitsTests
{
    [Theory]
    [InlineData(24, false, 12)]
    [InlineData(12, false, 6)]
    [InlineData(12, true, 4)]
    [InlineData(2, true, 2)]
    [InlineData(1, false, 2)]
    [InlineData(4, true, 2)]
    public void SlotsFollowTheCoresTheRunMayUseAndTheGame(int allowed, bool game, int expected)
        => Assert.Equal(expected, PageLimits.SlotCount(allowed, game));

    [Fact]
    public void AllowedProcessorsIsTheAffinityOfThisRun()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) return;
        using var me = Process.GetCurrentProcess();
        Assert.Equal(System.Numerics.BitOperations.PopCount((ulong)(long)me.ProcessorAffinity), PageLimits.AllowedProcessors());
    }

    [Fact]
    public void RaisingTouchesOnlyMemoryAndDiskPriority()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var me = Process.GetCurrentProcess();
        var cpu = me.PriorityClass;
        var affinity = me.ProcessorAffinity;
        me.PriorityClass = ProcessPriorityClass.BelowNormal;
        try
        {
            PageLimits.SetMemoryPriority(2);
            PageLimits.SetDiskPriority(0);
            PageLimits.RaiseToNormal();
            me.Refresh();
            Assert.True(PageLimits.DiskPriority() >= 2, "disk priority is raised to normal");
            Assert.Equal(ProcessPriorityClass.BelowNormal, me.PriorityClass);
            Assert.Equal(affinity, me.ProcessorAffinity);
            Assert.True(PageLimits.MemoryPriority() >= 5, "memory priority is raised to normal");
        }
        finally { me.PriorityClass = cpu; }
    }
}
