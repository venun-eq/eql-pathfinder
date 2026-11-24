using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Parsing;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// The whole app knows the loadout you're on (docs/specs/current-loadout.md): the game's per-slot files say which slot
/// is on, dbg.txt says when a class changed inside it, and a /who on a slot names it from then on. The swaps and class
/// changes go into the engine as events between the log's lines, at the times the game wrote them.
/// </summary>
public class CurrentLoadoutTests
{
    private static readonly DateTime Day = new(2026, 11, 18);

    private static DateTime T(string hms) => Day + TimeSpan.Parse(hms, System.Globalization.CultureInfo.InvariantCulture);
    private static string L(string hms, string body) => $"[Wed Nov 18 {hms} 2026] {body}";
    private static LoadoutSwapped Swap(string hms, int slot) => new(T(hms), slot);
    private static LoadoutClassesChanged ClassesChanged(string hms) => new(T(hms));
    private static string Reinc(string kind, string name) => $"You have gained the ability \"{kind} Reincarnation: {name}\" at a cost of 0 ability points.";

    // Made-up loadouts, as docs/log-formats.md's watch of 11-19 had them.
    private const string Relevel = "[10 DRU/BRD/NEC] Venun (Barbarian) ZONE: Befallen";   // slot 6
    private const string Main = "[50 PAL/MNK/SHM] Venun (Froglok) ZONE: Befallen";        // slot 1
    private const string Third = "[1 SHD/SHM/WIZ] Venun (Iksar) ZONE: Befallen";          // slot 2

    /// <summary>Log lines (strings) and the game files' events (a swap, a class change), in the order given.</summary>
    private static (PathfinderEngine e, TrackerTests.Sink s) Run(params object[] steps)
    {
        var s = new TrackerTests.Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), s);
        foreach (var step in steps)
        {
            if (step is string line) e.ProcessLine(line);
            else e.Tracker.Apply((LogEvent)step);
        }
        e.Flush();
        return (e, s);
    }

    private static (string? Trio, string? Race, int? Level, LoadoutOn? Loadout) On(PathfinderEngine e) =>
        (e.State.Trio, e.State.Race, e.State.Level, e.State.Loadout);

    /// <summary>Slot 6 and slot 1 each met through a /who, the player back on slot 6.</summary>
    private static object[] BothMet() =>
    [
        Swap("12:00:00", 6), L("12:00:30", Relevel),
        Swap("12:01:00", 1), L("12:01:30", Main),
    ];

    // ------------------------------------------------------------------ the engine

    [Fact]
    public void ASwapToASlotMetBeforeTakesItsTrioRaceAndLevelWithoutAWho()
    {
        var (e, _) = Run([.. BothMet(), Swap("12:02:00", 6)]);
        Assert.Equal(("DRU/BRD/NEC", "Barbarian", (int?)10, new LoadoutOn(6, "known")), On(e));
    }

    [Fact]
    public void ACrawlEnteredAfterASwapHasTheSwappedToLoadoutAndLevel()
    {
        // The spec's picture: on relevel 8's slot, the player swaps to the main and enters a crawl with no /who.
        var (_, s) = Run([.. BothMet(),
            Swap("12:02:00", 6), L("12:02:30", Relevel),
            Swap("12:03:00", 1),
            L("12:04:00", "You have entered Kedge Keep 4 (Refined)."),
            L("12:06:00", "You have slain a seahorse!"),
            L("12:08:00", "You have slain a seahorse!"),
            L("12:10:00", "You have completed the Dungeon Crawl and earned reward loot!"),
            L("12:11:00", "You have entered Dagnor's Cauldron.")]);
        var kedge = Assert.Single(s.Closed, v => v.BaseZone == "Kedge Keep");
        Assert.Equal(("PAL/MNK/SHM", "Froglok", (int?)50), (kedge.Trio, kedge.Race, kedge.LevelStart));
    }

    [Fact]
    public void ASwapToASlotNotMetShowsNoTrioRaceOrLevelFromTheLoadoutLeft()
    {
        var (e, _) = Run(Swap("12:00:00", 6), L("12:00:30", Relevel), Swap("12:01:00", 2));
        Assert.Equal(((string?)null, (string?)null, (int?)null, new LoadoutOn(2, "new")), On(e));
    }

    [Fact]
    public void AWhoOnASlotIsRememberedAfterSwappingAway()
    {
        var (e, _) = Run(
            Swap("12:00:00", 6), L("12:00:30", Relevel),
            Swap("12:01:00", 2), L("12:01:30", Third),
            Swap("12:02:00", 6), Swap("12:03:00", 2));
        Assert.Equal(("SHD/SHM/WIZ", "Iksar", (int?)1, new LoadoutOn(2, "known")), On(e));
    }

    [Fact]
    public void ADingOnASlotNotMetGivesItsLevelButNoTrio()
    {
        // 10 to 11 reads as the next level today, so the relevel's trio carried on to the slot swapped to.
        var (e, _) = Run(Swap("12:00:00", 6), L("12:00:30", Relevel), Swap("12:01:00", 2),
            L("12:05:00", "You have gained a level! Welcome to level 11!"));
        Assert.Equal(((string?)null, (string?)null, (int?)11, new LoadoutOn(2, "new")), On(e));
    }

    [Fact]
    public void AClassChangeForgetsTheTrioAndKeepsTheSlotsLevelAndRace()
    {
        var (e, _) = Run([.. BothMet(), Swap("12:02:00", 6), ClassesChanged("12:03:00")]);
        Assert.Equal(((string?)null, "Barbarian", (int?)10, new LoadoutOn(6, "classes")), On(e));

        // The /who names the new trio, and the slot keeps it through a swap away and back.
        var (after, _) = Run([.. BothMet(), Swap("12:02:00", 6), ClassesChanged("12:03:00"),
            L("12:04:00", "[10 BRD/NEC/ENC] Venun (Barbarian) ZONE: Befallen"), Swap("12:05:00", 1), Swap("12:06:00", 6)]);
        Assert.Equal(("BRD/NEC/ENC", "Barbarian", (int?)10, new LoadoutOn(6, "known")), On(after));
    }

    [Fact]
    public void AWhoInTheLogAfterTheSlotsSwapMeetsItEvenWhenTheFileIsReadLater()
    {
        // App start with the game running: the log (a /who at 12:05) is read before the files (slot 6, written at 12:00).
        var (e, _) = Run(L("12:05:00", Relevel), Swap("12:00:00", 6));
        Assert.Equal(("DRU/BRD/NEC", "Barbarian", (int?)10, new LoadoutOn(6, "known")), On(e));
        var (back, _) = Run(L("12:05:00", Relevel), Swap("12:00:00", 6), Swap("12:10:00", 1), Swap("12:11:00", 6));
        Assert.Equal(("DRU/BRD/NEC", "Barbarian", (int?)10, new LoadoutOn(6, "known")), On(back));
    }

    [Fact]
    public void ASwapReadAfterAWhoAndADingKeepsTheLevelTheEngineHas()
    {
        // The /who at 12:05 said 10; a ding at 12:06 made it 11; the file's swap (12:00) is only now read.
        var (e, _) = Run(L("12:05:00", Relevel), L("12:06:00", "You have gained a level! Welcome to level 11!"), Swap("12:00:00", 6));
        Assert.Equal(("DRU/BRD/NEC", "Barbarian", (int?)11, new LoadoutOn(6, "known")), On(e));
    }

    [Fact]
    public void AWhoInsideTheSettleWindowAfterASwapIsNotKeptForTheSlotLeft()
    {
        // On slot 3 (never met) until 12:02, then slot 6; the /who typed at 12:02:01 is read before the swap row is.
        var (e, _) = Run(Swap("12:00:00", 3), L("12:02:01", Relevel), Swap("12:02:00", 6));
        Assert.Equal(("DRU/BRD/NEC", "Barbarian", (int?)10, new LoadoutOn(6, "known")), On(e));
        var (back, _) = Run(Swap("12:00:00", 3), L("12:02:01", Relevel), Swap("12:02:00", 6), Swap("12:10:00", 3));
        Assert.Equal(((string?)null, (string?)null, (int?)null, new LoadoutOn(3, "new")), On(back));
    }

    [Fact]
    public void AWhoAtExactlyTheSwapsTimeCountsForTheSlotSwappedToOnly()
    {
        var (e, _) = Run(Swap("12:00:00", 3), L("12:02:00", Relevel), Swap("12:02:00", 6));
        Assert.Equal(("DRU/BRD/NEC", "Barbarian", (int?)10, new LoadoutOn(6, "known")), On(e));
        var (back, _) = Run(Swap("12:00:00", 3), L("12:02:00", Relevel), Swap("12:02:00", 6), Swap("12:10:00", 3));
        Assert.Equal(((string?)null, (string?)null, (int?)null, new LoadoutOn(3, "new")), On(back));
    }

    [Fact]
    public void AWhoBeforeTheSwapToTheSlotDoesNotMeetIt()
    {
        var (e, _) = Run(L("11:55:00", Relevel), Swap("12:00:00", 6));
        Assert.Equal(((string?)null, (string?)null, (int?)null, new LoadoutOn(6, "new")), On(e));
    }

    [Fact]
    public void AReincarnationKeepsTheSlotAndItsTrioAndDropsTheLevelTo1()
    {
        var (e, _) = Run(Swap("12:00:00", 3), L("12:00:30", "[50 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
            L("12:05:00", Reinc("Class", "Warrior")));
        Assert.Equal(("WAR/CLR/RNG", "Ogre", (int?)1, new LoadoutOn(3, "known")), On(e));

        var (race, _) = Run(Swap("12:00:00", 3), L("12:00:30", "[50 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
            L("12:05:00", Reinc("Race", "Half Dark Elf")));
        Assert.Equal(("WAR/CLR/RNG", "Half Dark Elf", (int?)1, new LoadoutOn(3, "known")), On(race));
    }

    [Fact]
    public void AClassChangeBeforeAReincarnationStillAsksForAWho()
    {
        var (e, _) = Run(Swap("12:00:00", 3), L("12:00:30", "[50 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"),
            ClassesChanged("12:03:00"), L("12:05:00", Reinc("Class", "Paladin")));
        Assert.Equal(((string?)null, "Ogre", (int?)1, new LoadoutOn(3, "classes")), On(e));
    }

    // ------------------------------------------------------------------ Home's state

    /// <summary>A relevel open and never played, as the log gives it (docs/quality.md: a state the app never builds).</summary>
    private static Roster OneOpen()
    {
        var (e, _) = Run(L("11:00:00", Reinc("Race", "Kerran")), L("11:00:00", Reinc("Class", "Warrior")), L("11:00:00", Reinc("Class", "Cleric")),
            L("11:00:00", Reinc("Class", "Ranger")), L("11:01:00", "[32 WAR/CLR/RNG] Venun (Ogre) ZONE: Befallen"));
        return e.State.Roster;
    }

    [Fact]
    public void HomeAsksForAWhoOnALoadoutNotMetWhateverRelevelsAreOpen()
    {
        var parked = HomeState.Of(null, null, null, 50, OneOpen(), [], new LoadoutOn(6, "new"));
        Assert.Equal(("newloadout", new LoadoutOn(6, "new"), 1), (parked.Mode, parked.Loadout, parked.Parked.Count));
        var firstRun = HomeState.Of(null, null, null, 50, new Roster(), [], new LoadoutOn(6, "new"));
        Assert.Equal("newloadout", firstRun.Mode);
        var dinged = HomeState.Of(11, null, null, 50, new Roster(), [], new LoadoutOn(6, "new"));
        Assert.Equal(("newloadout", (int?)11), (dinged.Mode, dinged.Level));
    }

    [Fact]
    public void HomeSaysClassesChangedBelowTheCapAndKeepsFarmingAtIt()
    {
        var below = HomeState.Of(10, null, "Barbarian", 50, OneOpen(), [], new LoadoutOn(6, "classes"));
        Assert.Equal(("classes", (int?)10, new LoadoutOn(6, "classes")), (below.Mode, below.Level, below.Loadout));
        var atCap = HomeState.Of(50, null, "Froglok", 50, OneOpen(), [], new LoadoutOn(1, "classes"));
        Assert.Equal(("farming", new LoadoutOn(1, "classes")), (atCap.Mode, atCap.Loadout));
    }

    // ------------------------------------------------------------------ the game's files

    private static DateTime At(string hms) => new DateTime(2026, 11, 19) + TimeSpan.Parse(hms, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>docs/log-formats.md's watch: on slot 6, then a swap to slot 1 (the slot left is saved a second first).</summary>
    private static (string, DateTime)[] SwappedTo1() =>
    [
        ("UI_Venun_oggok_LO2.ini", At("15:10:00")),
        ("Venun_oggok_LO6.ini", At("15:20:00")),
        ("UI_Venun_oggok_LO6.ini", At("15:36:00")),
        ("UI_Venun_oggok_LO1.ini", At("15:36:01")),
        ("Venun_oggok_LO1.ini", At("15:36:07")),
    ];

    [Fact]
    public void TheSlotOnIsTheLoadoutFileWrittenLast()
    {
        Assert.Equal((1, At("15:36:07")), LoadoutFiles.SlotOn(SwappedTo1(), "Venun", "oggok"));
    }

    [Fact]
    public void OnlyTheTrackedCharactersFilesSinceTheClientStartedCount()
    {
        (string, DateTime)[] files = [.. SwappedTo1(),
            ("Kibarer_oggok_LO3.ini", At("15:50:00")),      // another character
            ("UI_Venun_halas_LO4.ini", At("15:51:00")),     // the same name on another server
            ("UI_Venun_oggok.ini", At("15:52:00")),         // the character's own settings, no slot
            ("eqclient.ini", At("15:53:00"))];
        Assert.Equal((1, At("15:36:07")), LoadoutFiles.SlotOn(files, "Venun", "oggok"));
        Assert.Equal((1, At("15:36:07")), LoadoutFiles.SlotOn(files, "Venun", "oggok", since: At("15:30:00")));
        Assert.Null(LoadoutFiles.SlotOn(files, "Venun", "oggok", since: At("15:40:00")));   // at character select: none this run
    }

    [Fact]
    public void AClassChangeIsAnArrivalInTheSameZoneWithNoResetOrZoning()
    {
        string[] lines =
        [
            "[Thu Nov 19 15:30:00 2026]00000:Starting EverQuest (Built Nov 17 2026 09:39:03)",
            "[Thu Nov 19 15:30:20 2026]00039:WorldRPServer message: server name oggok",
            "[Thu Nov 19 15:30:33 2026]00304:Starting char select.",
            "[Thu Nov 19 15:30:44 2026]00318:Zone Connect -- 2 -- Sending MSG_EQ_ADDPLAYER",
            "[Thu Nov 19 15:30:45 2026]00321:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Estate of Unrest",
            "[Thu Nov 19 15:31:02 2026]00420:Initialization complete.",
            // A loadout swap: the reset, then the arrival in the same zone.
            "[Thu Nov 19 15:36:00 2026]00500:Resetting UI.",
            "[Thu Nov 19 15:36:06 2026]00510:Received our Player from zone.",
            "[Thu Nov 19 15:36:06 2026]00511:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Estate of Unrest",
            // A class changed: the arrival alone.
            "[Thu Nov 19 15:40:00 2026]00600:Received our Player from zone.",
            "[Thu Nov 19 15:40:00 2026]00601:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Estate of Unrest",
            // Zoning into the same zone (a /zone): the zoning lines say it isn't one.
            "[Thu Nov 19 15:45:00 2026]00700:Waiting to connect to zone... Character Venun",
            "[Thu Nov 19 15:45:01 2026]00701:Zone Connect -- 2 -- Sending MSG_EQ_ADDPLAYER",
            "[Thu Nov 19 15:45:03 2026]00702:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Estate of Unrest",
            "[Thu Nov 19 15:45:03 2026]00703:Initializing zone.",
            "[Thu Nov 19 15:45:20 2026]00704:Initialization complete.",
            // A class changed again.
            "[Thu Nov 19 15:50:00 2026]00800:Received our Player from zone.",
            "[Thu Nov 19 15:50:00 2026]00801:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Estate of Unrest",
            // Camp, another character on another server, and a class change of theirs.
            "[Thu Nov 19 16:02:00 2026]00900:WorldRPServer message: server name halas",
            "[Thu Nov 19 16:02:29 2026]00910:Zone Connect -- 2 -- Sending MSG_EQ_ADDPLAYER",
            "[Thu Nov 19 16:02:30 2026]00911:Received MSG_EQ_ADDPLAYER, Player = Kibarer, zone = Halas",
            "[Thu Nov 19 16:02:41 2026]00960:Initialization complete.",
            "[Thu Nov 19 16:10:00 2026]01000:Received our Player from zone.",
            "[Thu Nov 19 16:10:00 2026]01001:Received MSG_EQ_ADDPLAYER, Player = Kibarer, zone = Halas",
        ];
        var path = Path.Combine(Path.GetTempPath(), $"dbg-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllLines(path, lines);
            var s = new ClientDebugLog(path).Read()!;
            Assert.Equal([At("15:40:00"), At("15:50:00")], s.ClassChangesOf("Venun", "oggok"));
            Assert.Equal([At("16:10:00")], s.ClassChangesOf("Kibarer", "halas"));
            Assert.Empty(s.ClassChangesOf("Kibarer", "oggok"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AResetAtWorldEntryIsNotASwapAtTheNextZoning()
    {
        // The user's real dbg.txt, 11-19: the UI resets twice on entering the world, then they zone twice, never swapping.
        string[] lines =
        [
            "[Thu Nov 19 17:44:03 2026]00000:Starting EverQuest (Built Nov 17 2026 09:39:03)",
            "[Thu Nov 19 17:44:30 2026]00039:WorldRPServer message: server name oggok",
            "[Thu Nov 19 17:45:27 2026]00321:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = New Sebilis Expedition",
            "[Thu Nov 19 17:45:30 2026]00322:Resetting UI.",
            "[Thu Nov 19 17:45:30 2026]00323:Resetting UI.",
            "[Thu Nov 19 17:53:34 2026]00426:Beginning EnterZone block.",
            "[Thu Nov 19 17:53:35 2026]00443:Waiting to connect to zone... Character Venun",
            "[Thu Nov 19 17:53:38 2026]00444:Zone Connect -- 0 -- Received MSG_ZONE_ADDRESS",
            "[Thu Nov 19 17:53:39 2026]00450:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = West Commonlands",
            "[Thu Nov 19 17:59:24 2026]00500:Waiting to connect to zone... Character Venun",
            "[Thu Nov 19 17:59:24 2026]00501:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = Befallen",
        ];
        var path = Path.Combine(Path.GetTempPath(), $"dbg-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllLines(path, lines);
            var s = new ClientDebugLog(path).Read()!;
            Assert.Empty(s.SwapsOf("Venun", "oggok"));
            Assert.Empty(s.ClassChangesOf("Venun", "oggok"));
            var watch = new EqlPathfinder.Engine.Live.LoadoutWatch(Path.GetTempPath(), Path.GetTempPath(), "Venun", "oggok");
            Assert.Equal(At("17:45:27"), watch.SwapTime(s, At("17:59:16")));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ADeathThatRespawnsInTheSameZoneIsNotAClassChange()
    {
        // The user's real dbg.txt, 11-20: two deaths in Mistmoore, each respawning in the zone with no zoning lines. The
        // overlay asked for a /who on LO6 though no class had changed.
        string[] lines =
        [
            "[Thu Nov 19 20:59:04 2026]00000:Starting EverQuest (Built Nov 17 2026 09:39:03)",
            "[Thu Nov 19 20:59:30 2026]00039:WorldRPServer message: server name oggok",
            "[Thu Nov 19 23:58:43 2026]02599:Waiting to connect to zone... Character Venun",
            "[Thu Nov 19 23:58:45 2026]02607:Zone Connect -- 2 -- Sending MSG_EQ_ADDPLAYER",
            "[Thu Nov 19 23:58:46 2026]02609:Received our Player from zone.",
            "[Thu Nov 19 23:58:46 2026]02610:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Castle of Mistmoore 227",
            "[Thu Nov 19 23:58:50 2026]02703:Initialization complete.",
            "[Fri Nov 20 00:22:55 2026]02721:knockout 1",
            "[Fri Nov 20 00:22:56 2026]02722:Out of combat timer changed by server for character Venun:9.",
            "",
            "[Fri Nov 20 00:22:56 2026]02723:I died.",
            "",
            "[Fri Nov 20 00:22:56 2026]02724:knockout 3",
            "[Fri Nov 20 00:22:57 2026]02725:Received our Player from zone.",
            "[Fri Nov 20 00:22:57 2026]02726:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Castle of Mistmoore 227",
            "[Fri Nov 20 00:27:07 2026]02727:Out of combat timer changed by server for character Venun:9.",
            "",
            "[Fri Nov 20 00:27:07 2026]02728:I died.",
            "",
            "[Fri Nov 20 00:27:07 2026]02729:knockout 3",
            "[Fri Nov 20 00:27:08 2026]02730:Received our Player from zone.",
            "[Fri Nov 20 00:27:08 2026]02731:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Castle of Mistmoore 227",
            // A class changed after the deaths: the arrival alone is still one.
            "[Fri Nov 20 00:40:00 2026]02800:Received our Player from zone.",
            "[Fri Nov 20 00:40:00 2026]02801:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Castle of Mistmoore 227",
        ];
        var path = Path.Combine(Path.GetTempPath(), $"dbg-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllLines(path, lines);
            var s = new ClientDebugLog(path).Read()!;
            Assert.Empty(s.SwapsOf("Venun", "oggok"));
            Assert.Equal([new DateTime(2026, 11, 20, 0, 40, 0)], s.ClassChangesOf("Venun", "oggok"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AClassChangeRightAfterWorldEntryIsOneClassChangeAndNoSwap()
    {
        string[] lines =
        [
            "[Thu Nov 19 17:44:03 2026]00000:Starting EverQuest (Built Nov 17 2026 09:39:03)",
            "[Thu Nov 19 17:44:30 2026]00039:WorldRPServer message: server name oggok",
            "[Thu Nov 19 17:45:27 2026]00321:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Estate of Unrest",
            "[Thu Nov 19 17:45:30 2026]00322:Resetting UI.",
            "[Thu Nov 19 17:45:30 2026]00323:Resetting UI.",
            "[Thu Nov 19 17:46:00 2026]00400:Received our Player from zone.",
            "[Thu Nov 19 17:46:00 2026]00401:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Estate of Unrest",
        ];
        var path = Path.Combine(Path.GetTempPath(), $"dbg-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllLines(path, lines);
            var s = new ClientDebugLog(path).Read()!;
            Assert.Empty(s.SwapsOf("Venun", "oggok"));
            Assert.Equal([At("17:46:00")], s.ClassChangesOf("Venun", "oggok"));
        }
        finally { File.Delete(path); }
    }
}
