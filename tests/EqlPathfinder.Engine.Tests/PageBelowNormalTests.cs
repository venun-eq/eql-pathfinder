using System.Diagnostics;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The hourly ship's tests run at below-normal priority (its scheduled task has priority 7) and every Edge inherits it:
/// the first dozen starting together then left some with no renderer, hung until the 20 s fuse, reported for every
/// test of the class (docs/specs/page-tests-steady.md). The harness raises only memory and disk priority (the hang's cause); CPU stays below normal so the game keeps the machine.
/// </summary>
public class PageBelowNormalTests
{
    [Fact]
    public async Task EverySlotsBrowserStartedTogetherFromALowPriorityTestRunAllComeBack()
    {
        var me = Process.GetCurrentProcess();
        var was = me.PriorityClass;
        me.PriorityClass = ProcessPriorityClass.BelowNormal;
        try
        {
            var browsers = PageLimits.Slots(); // every slot at once: the first wave that stalled
            var message = (await PageMessage.FarmingAtTheCap()).ToJsonString();
            var opened = await PageRuns.AtOnce(Enumerable.Range(0, browsers).Select<int, Func<PageHarness, PageRun>>(_ => h => h.Open("home", message)).ToArray());
            Assert.Equal(browsers, opened.Runs.Count);
            me.Refresh();
            Assert.Equal(ProcessPriorityClass.BelowNormal, me.PriorityClass); // dev work stays below normal so the game keeps the CPU
        }
        finally { me.PriorityClass = was; }
    }
}
