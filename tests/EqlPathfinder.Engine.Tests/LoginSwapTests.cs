using EqlPathfinder.Engine.Parsing;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// A swap the game's files showed at login is a guess: the newest slot file can be the previous run's (docs/specs/login-swap.md).
/// It counts once a /who or a later swap confirms it (a zone that shows another slot is a later swap); a ding its slot's level rules out drops it.
/// </summary>
public class LoginSwapTests
{
    private static readonly DateTime Day = new(2026, 11, 24);
    private static DateTime T(string hms) => Day + TimeSpan.Parse(hms, System.Globalization.CultureInfo.InvariantCulture);
    private static string L(string hms, string body) => $"[Tue Nov 24 {hms} 2026] {body}";
    private static string Welcome(string hms) => L(hms, "Welcome to EverQuest Legends!");
    private static string Ding(string hms, int level) => L(hms, $"You have gained a level! Welcome to level {level}!");
    private static string Entered(string hms, string zone) => L(hms, $"You have entered {zone}.");

    private const string Pal = "[47 PAL/WIZ/BER] Venun (Erudite) ZONE: Befallen";
    private const string Dru = "[50 DRU/BRD/NEC] Venun (Iksar) ZONE: Befallen";

    /// <summary>Strings are log lines; events are the game files' rows, queued as the app does.</summary>
    private static PathfinderEngine Run(params object[] steps) => RunBoth(steps).e;

    private static (PathfinderEngine e, TrackerTests.Sink s) RunBoth(params object[] steps)
    {
        var sink = new TrackerTests.Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), sink);
        long row = 0;
        foreach (var step in steps)
        {
            if (step is string line) e.ProcessLine(line);
            else e.Queue(++row, (LogEvent)step);
        }
        e.Flush();
        return (e, sink);
    }

    private static LoadoutSwapped Swap(string hms, int slot) => new(T(hms), slot);

    /// <summary>Two days before: slot 4 met at 50 (DRU/BRD/NEC), then slot 3 at 47 (PAL/WIZ/BER), the player ending on slot 3.</summary>
    private static object[] Earlier()
    {
        var d = Day.AddDays(-2);
        string Old(string hms, string body) => $"[Sun Nov 22 {hms} 2026] {body}";
        return
        [
            new LoadoutSwapped(d.AddHours(18), 4), Old("18:00:30", Dru),
            new LoadoutSwapped(d.AddHours(19), 3), Old("19:00:30", Pal),
        ];
    }

    private static object[] Then(object[] before, params object[] after) => [.. before, .. after];

    private static (string?, int?, int?) On(PathfinderEngine e) => (e.State.Trio, e.State.Level, e.State.Loadout?.Slot);

    [Fact]
    public void ASwapSeenAtLoginIsHeldUntilTheGameConfirmsIt()
    {
        var e = Run(Then(Earlier(), Swap("17:45:32", 4), Welcome("17:45:48"), Entered("17:45:48", "The Plane of Hate")));
        Assert.Equal(("PAL/WIZ/BER", 47, 3), On(e));
    }

    [Fact]
    public void ADingTheHeldSlotsLevelRulesOutDropsTheSwap()
    {
        var e = Run(Then(Earlier(), Swap("17:45:32", 4), Welcome("17:45:48"), Ding("18:02:35", 48), Ding("18:48:34", 49),
            Entered("19:09:00", "The Plane of Fear"), Ding("19:19:05", 50)));
        Assert.Equal(("PAL/WIZ/BER", 50, 3), On(e));
        Assert.Null(e.State.PendingSwap);
    }

    [Fact]
    public void ADingThatFitsOnlyTheHeldSlotConfirmsItAndSlotThreeKeepsItsMemory()
    {
        var e = Run(Then(Earlier(), Swap("17:45:32", 4), Welcome("17:45:48"), Ding("18:02:35", 51)));
        Assert.Null(e.State.PendingSwap);
        Assert.Equal(("DRU/BRD/NEC", 51, 4), On(e));
        Assert.Equal(47, e.State.Slots[3].Level);
        Assert.Equal("PAL/WIZ/BER", string.Join("/", e.State.Slots[3].Classes!));
    }

    [Fact]
    public void ALaterRowToTheHeldSlotCountsFromItsOwnTime()
    {
        var e = Run(Then(Earlier(), Swap("17:45:32", 4), Welcome("17:45:48"), Swap("19:26:21", 4)));
        Assert.Equal(4, e.State.Loadout?.Slot);
        Assert.DoesNotContain(e.State.Stints, x => x.Slot == 4 && x.From >= T("17:00:00") && x.From < T("19:00:00"));
        Assert.Contains(e.State.Stints, x => x.Slot == 4 && x.From == T("19:26:21"));
    }

    [Fact]
    public void TheLiveStepDoesNotTakeTheSwapBeforeTheWelcomeLine()
    {
        var e = new PathfinderEngine("Venun", new TrackerOptions(), new TrackerTests.Sink());
        long row = 0;
        foreach (var step in Earlier()) { if (step is string s) e.ProcessLine(s); else e.Queue(++row, (LogEvent)step); }
        e.Queue(++row, Swap("17:45:32", 4));
        e.ApplyQueued(T("17:45:40"));   // what Step passes: the clock, not MaxValue
        e.ProcessLine(Welcome("17:45:48"));
        e.Flush();
        Assert.Equal(3, e.State.Loadout?.Slot);
        Assert.NotNull(e.State.PendingSwap);
    }

    [Fact]
    public void The1124SequenceReadsTheLevelsAsTheLoadoutPlayed()
    {
        var e = Run(Then(Earlier(), Swap("17:45:32", 4), Welcome("17:45:48"), Entered("17:45:48", "The Plane of Hate"),
            Ding("18:02:35", 48), Ding("18:48:34", 49), Entered("19:09:00", "The Plane of Fear"), Ding("19:19:05", 50),
            new LoadoutClassesChanged(T("19:25:45")), Swap("19:26:21", 4), L("19:52:00", "[4 BST/RNG/ENC] Venun (Iksar) ZONE: Befallen")));
        Assert.Equal(("BST/RNG/ENC", 4, 4), On(e));
        Assert.Equal(50, e.State.Slots[3].Level);   // slot 3 played 47 to 50 and keeps it
        Assert.True(e.State.Slots[3].ClassesChanged);   // the 19:25 class change was its own
    }

    [Fact]
    public void AWhoOrALaterSwapConfirmsASwapSeenAtLogin()
    {
        var who = Run(Then(Earlier(), Swap("17:45:32", 4), Welcome("17:45:48"), L("17:50:00", Dru)));
        Assert.Equal(("DRU/BRD/NEC", 50, 4), On(who));


        var later = Run(Then(Earlier(), Swap("17:45:32", 4), Welcome("17:45:48"), Swap("17:46:10", 3)));
        Assert.Equal(3, later.State.Loadout?.Slot);
        Assert.Contains(later.State.Stints, x => x.Slot == 4);
    }

    [Fact]
    public void AZoneAfterTheLoginDoesNotConfirmIt()
    {
        var e = Run(Then(Earlier(), Swap("17:45:32", 4), Welcome("17:45:48"), Entered("17:46:48", "The Plane of Hate 1 (Awakened)")));
        Assert.Equal(3, e.State.Loadout?.Slot);
        Assert.NotNull(e.State.PendingSwap);
    }

    [Fact]
    public void StartupLinesBeforeTheWelcomeLineDoNotLetTheSwapThrough()
    {
        // The real log of 11-24: the swap row was 10 s before the first startup line and 16 s before the Welcome line.
        var e = Run(Then(Earlier(), Swap("17:45:32", 4), L("17:45:42", "You are not currently assigned to an adventure."),
            L("17:45:45", "Channels: 1=General"), Welcome("17:45:48"), Entered("17:45:48", "The Plane of Hate"), Ding("18:02:35", 48)));
        Assert.Equal(("PAL/WIZ/BER", 48, 3), On(e));
    }

    [Fact]
    public void ASwapAfterChatterIsNotWaitedFor()
    {
        var e = Run(Then(Earlier(), L("17:45:00", "Channels: 1=General"), Swap("17:45:20", 4), L("17:45:21", Dru)));
        Assert.Equal(("DRU/BRD/NEC", 50, 4), On(e));
    }

    [Fact]
    public void ASwapAwayFromALoginIsTakenAtOnce()
    {
        var far = Run(Then(Earlier(), Welcome("17:45:48"), Swap("19:26:21", 4)));
        Assert.Equal(4, far.State.Loadout?.Slot);
        var same = Run(Then(Earlier(), Welcome("17:45:48"), Swap("17:45:20", 3)));   // the slot already on
        Assert.Equal(3, same.State.Loadout?.Slot);
        Assert.Null(same.State.PendingSwap);
    }

    [Fact]
    public void ALevelTheSlotsMemoryRulesOutLeavesTheLevelsToTheWhoThatNamesThem()
    {
        // 11-24: slot 4 was met as DRU/BRD/NEC at 50 on 11-22, then set up again unseen (Iksar BST/RNG/ENC, level 1). The
        // swap to it at 19:26 gives the old memory; dings 2 to 4 can't be that slot's, so the /who at level 4 names them all.
        var (e, s) = RunBoth(Then(Earlier(), Swap("19:26:21", 4), Entered("19:43:34", "New Sebilis Expedition 4 (Refined)"),
            Ding("19:45:00", 2), Ding("19:48:00", 3), Ding("19:50:00", 4), L("19:52:00", "[4 RNG/ENC/BST] Venun (Iksar) ZONE: New Sebilis"),
            Entered("19:55:00", "The Plane of Hate")));
        var v = Assert.Single(s.Closed, x => x.BaseZone == "New Sebilis Expedition");
        Assert.Equal(("RNG/ENC/BST", "Iksar", (int?)1, (int?)4), (v.Trio, v.Race, v.LevelStart, v.LevelEnd));
        Assert.True(v.TrioConfirmed);
    }

    [Fact]
    public void AWhoOfTheSlotOnDoesNotConfirmTheHeldSwap()
    {
        var e = Run(Then(Earlier(), Swap("17:45:32", 4), Welcome("17:45:48"), L("17:50:00", Pal)));
        Assert.Equal(("PAL/WIZ/BER", 47, 3), On(e));
        Assert.Null(e.State.PendingSwap);
        Assert.Equal(50, e.State.Slots[4].Level);
        Assert.Equal("DRU/BRD/NEC", string.Join("/", e.State.Slots[4].Classes!));
    }

    [Fact]
    public void ADingFarAboveTheHeldSlotsLevelDropsTheSwap()
    {
        // Held slot 4 known at 30, slot on at 47: a ding to 48 is the slot on's.
        var d = Day.AddDays(-2);
        var e = Run(new LoadoutSwapped(d.AddHours(18), 4), "[Sun Nov 22 18:00:30 2026] [30 DRU/BRD/NEC] Venun (Iksar) ZONE: Befallen",
            new LoadoutSwapped(d.AddHours(19), 3), $"[Sun Nov 22 19:00:30 2026] {Pal}",
            Swap("17:45:32", 4), Welcome("17:45:48"), Ding("18:02:35", 48));
        Assert.Null(e.State.PendingSwap);
        Assert.Equal(("PAL/WIZ/BER", 48, 3), On(e));
    }
}
