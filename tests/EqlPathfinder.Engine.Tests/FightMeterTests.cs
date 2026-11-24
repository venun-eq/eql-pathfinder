using EqlPathfinder.Engine.Combat;
using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.Tests;

public class FightMeterTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 20, 0, 0);

    /// <summary>Runs log bodies through the meter's two parsers and the tracker; each line is "+seconds body".</summary>
    private static (List<Fight> Closed, FightTracker Tracker) Run(params string[] lines) => Run(FightTracker.DefaultLingerSeconds, lines);

    private static (List<Fight> Closed, FightTracker Tracker) Run(double lingerSeconds, params string[] lines)
    {
        var closed = new List<Fight>();
        var ctx = new FightContext();
        var tracker = new FightTracker("Venun", ctx, closed.Add) { LingerSeconds = lingerSeconds };
        var parser = new LineParser("Venun", ctx.Pets);
        var last = T0;
        foreach (var l in lines)
        {
            int sp = l.IndexOf(' ');
            var line = new LogLine(T0.AddSeconds(double.Parse(l[..sp], System.Globalization.CultureInfo.InvariantCulture)), l[(sp + 1)..]);
            tracker.OnLine(line.Time);
            var e = parser.Parse(line) ?? CombatParser.Parse(line);
            if (e != null) tracker.Apply(e);
            last = line.Time;
        }
        // A kill closes its fight once nothing more has hit that name for a few seconds.
        tracker.OnLine(last.AddSeconds(FightTracker.SameNameSeconds + 1));
        return (closed, tracker);
    }

    [Fact]
    public void FeignDeathPullsAreOneFightWithActiveAndKillTime()
    {
        // Debuff (resisted), feign, debuff again, feign, then stand and fight: one kill, 80 seconds end to end. Feigns this long
        // need "A fight ends" at 1 minute or more; at the 15-second default each stretch is its own fight (below).
        var (closed, _) = Run(60,
            "0 A gnoll resisted your Ignite!",
            "2 A gnoll tries to hit YOU, but misses!",
            "30 A gnoll resisted your Ignite!",
            "33 A gnoll tries to bash YOU, but YOU dodge!",
            "60 You crush a gnoll for 500 points of damage.",
            "70 You crush a gnoll for 500 points of damage.",
            "79 You have slain a gnoll!");
        var f = Assert.Single(closed);
        Assert.Equal(T0, f.Start);
        Assert.Equal(80, f.Seconds);                                   // kill time: first debuff to the death
        Assert.Equal(3 + 4 + 20, f.ActiveSeconds);                     // 0-2, 30-33 and 60-79: the feigns between don't count
        Assert.Equal(1, f.Kills);

        var frame = new MeterFrame("Venun", null, null, false, null, T0, T0, closed, [], null);
        var active = MeterViews.Build(frame, new MeterQuery("fight", "fighter"));
        Assert.Equal((27.0, 1000 / 27.0, 80.0, 12.5), (active.Seconds, active.PerSecond, active.OtherSeconds, active.OtherPerSecond));
        Assert.StartsWith("Kill time 1:20 · ", active.Footer);
        var kill = MeterViews.Build(frame, new MeterQuery("fight", "fighter", Clock: "kill"));
        Assert.Equal((80.0, 12.5, 27.0), (kill.Seconds, kill.PerSecond, kill.OtherSeconds));
        Assert.StartsWith("Active 0:27 · 37 DPS", kill.Footer);
    }

    [Fact]
    public void AMobThatHasntDiedEndsItsFightAfterEightSecondsByDefault()
    {
        // A ghoul hit and left alive (a mez, a split, it ran), then another "a ghoul" pulled 40 seconds later: two fights at
        // the default. At the old fixed 2 minutes the second joined the first, and the encounter ran from the first swing.
        string[] lines =
        [
            "0 You crush a ghoul for 100 points of damage.",
            "3 A ghoul hits YOU for 20 points of damage.",
            "43 You crush a ghoul for 100 points of damage.",
            "50 You have slain a ghoul!",
        ];
        var (closed, _) = Run(lines);
        Assert.Equal(2, closed.Count);
        Assert.Equal(8, closed.Single(f => f.Kills == 1).Seconds);   // 43 to 50, both ends counted
        var (old, _) = Run(120, lines);
        Assert.Equal(51, Assert.Single(old).Seconds);
    }

    [Fact]
    public void SameNamedMobsFoughtTogetherAreOneFightWithItsKills()
    {
        var (closed, _) = Run(
            "0 You crush a zombie for 100 points of damage.",
            "3 You have slain a zombie!",
            "4 You crush a zombie for 100 points of damage.",      // another one, already on you
            "8 You have slain a zombie!",
            "30 You crush a zombie for 100 points of damage.",     // a later pull: its own fight
            "33 You have slain a zombie!");
        Assert.Equal(2, closed.Count);
        Assert.Equal((2, 9.0, 200L), (closed[0].Kills, closed[0].Seconds, closed[0].MyDamage));
        Assert.Equal((1, 4.0, 100L), (closed[1].Kills, closed[1].Seconds, closed[1].MyDamage));
        var frame = new MeterFrame("Venun", null, null, false, null, T0, T0, closed, [], null);
        var byMob = Assert.Single(MeterViews.Build(frame, new MeterQuery("session", "mob")).Rows);
        Assert.Equal(3, byMob.Count);                                   // three kills, not two fights
    }

    [Fact]
    public void AOneWordNamedMobIsNotTakenForAGroupMember()
    {
        // "Fright" looks like a player's name. It hits Hellsfury (a real group member) before you hit it; your damage on it must count.
        var (closed, _) = Run(
            "0 You crush a boogeyman for 100 points of damage.",
            "1 Hellsfury slashes a boogeyman for 50 points of damage.",
            "2 Fright hits Hellsfury for 300 points of damage.",
            "3 You crush Fright for 400 points of damage.",
            "4 Hellsfury slashes Fright for 60 points of damage.",
            "5 Labtik told you, 'Attacking Fright Master.'",
            "6 Labtik punches Fright for 70 points of damage.",
            "7 Labtik punches Hellsfury for 999 points of damage.",     // a pet turning on a friend is not a fight
            "8 You hit yourself for 55 points of magic damage by Burnout.",
            "9 Fright has been slain by Venun!",
            "10 You have slain a boogeyman!");
        Assert.Equal(["Fright", "a boogeyman"], closed.Select(f => f.Mob).Order(StringComparer.Ordinal));
        var fright = closed.Single(f => f.Mob == "Fright");
        Assert.Equal(470, fright.MyDamage);
        Assert.Equal(60, fright.Fighters["Hellsfury"].Total.Damage);
        Assert.DoesNotContain("Fright", closed.Single(f => f.Mob == "a boogeyman").Fighters.Keys);
    }

    [Fact]
    public void YourBarCarriesALineAtYourUsual()
    {
        var fights = new List<Fight> { Make(0, 10, "a zombie", 500, 2000) };
        var frame = new MeterFrame("Venun", "Befallen", 4, true, null, T0, T0, fights, [], new Usual(10, 100, 100, new Dictionary<string, double>()));
        var rows = MeterViews.Build(frame, new MeterQuery("fight", "fighter")).Rows;
        var me = rows.Single(r => r.Label == "Venun");
        Assert.Equal(0.5, me.UsualPct);                    // 50 a second against a usual 100
        Assert.Equal(0.5, me.Tick);                        // the usual is 1,000 over these 10 s, on a scale that tops out at Hellsfury's 2,000
        Assert.Equal(0.25, me.Bar);
        Assert.Null(rows.Single(r => r.Label == "Hellsfury").Tick);
    }

    [Fact]
    public void SwingsAndResistsParse()
    {
        LogEvent? P(string body) => CombatParser.Parse(new LogLine(T0, body));
        Assert.Equal(new Swing(T0, "You", "a zombie"), P("You try to crush a zombie, but miss!"));
        Assert.Equal(new Swing(T0, "You", "a zombie"), P("You try to pierce a zombie, but a zombie parries!"));
        Assert.Equal(new Swing(T0, "a zombie", "YOU"), P("A zombie tries to hit YOU, but YOU dodge!"));
        Assert.Equal(new Swing(T0, "a zombie", "YOU"), P("A zombie tries to hit YOU, but misses! (Riposte)"));
        Assert.Equal(new Swing(T0, "Hellsfury", "a zombie"), P("Hellsfury tries to slash a zombie, but misses!"));
        Assert.Equal(new Swing(T0, "You", "a one-eyed gnoll"), P("A one-eyed gnoll resisted your Ignite!"));
        Assert.Equal(new Swing(T0, "Labtik", "a gnoll"), P("A gnoll resisted Labtik's Spirit of Vermin Strike III!"));
        Assert.Equal(new Swing(T0, "a Rosch Mas Gnoll", "YOU"), P("You resist a Rosch Mas Gnoll's Instill!"));
        Assert.Null(P("Kutsuu tries to cast a spell on you, but you are protected."));
    }

    [Fact]
    public void ParsesOtherFightersHealsAndSpellsOnYou()
    {
        LogEvent? P(string body) => CombatParser.Parse(new LogLine(T0, body));
        Assert.Equal(new OtherDamage(T0, "Goneker", "a gloomstalker mermaid", 137, "Cleave", false), P("Goneker cleaves a gloomstalker mermaid for 137 points of damage."));   // a skill on its own timer (9 s apart), its own row since 11-17
        Assert.Equal(new OtherDamage(T0, "an abhorrent", "a spite golem", 133, null, true), P("An abhorrent claws a spite golem for 133 points of damage. (Critical)"));
        Assert.Equal(new OtherDamage(T0, "Hellsfury", "a Tesch Mas Gnoll", 82, "Enveloping Roots", false),
            P("Hellsfury hit a Tesch Mas Gnoll for 82 points of magic damage by Enveloping Roots."));
        Assert.Equal(new SpellHitYou(T0, "a Tesch Mal Gnoll", 252, "Shock of Spikes"), P("a Tesch Mal Gnoll hit you for 252 points of magic damage by Shock of Spikes."));
        Assert.Equal(new HealLanded(T0, "You", "Venun", 871, "Drain Soul"), P("You healed Venun for 871 hit points by Drain Soul."));
        Assert.Equal(new HealLanded(T0, "Hellsfury", "you", 12, "Talisman of Altuna"), P("Hellsfury healed you for 12 (255) hit points by Talisman of Altuna."));
        Assert.Equal(new HealLanded(T0, "You", "Venun", 40, "Celestial Health"), P("You healed Venun over time for 40 hit points by Celestial Health."));
        Assert.Null(P("Sosk tells the guild, 'he hits me for 300 points of damage.'"));
    }

    [Fact]
    public void AFightRunsFromFirstHitToTheKill()
    {
        var (closed, tracker) = Run(
            "0 Labtik told you, 'Attacking an ire ghast Master.'",
            "1 You hit an ire ghast for 867 points of magic damage by Banish Undead X.",
            "2 Labtik punches an ire ghast for 139 points of damage.",
            "3 You crush an ire ghast for 133 points of damage. (Critical)",
            "4 An ire ghast hits YOU for 73 points of damage.",
            "5 You healed Venun for 871 hit points by Drain Soul.",
            "9 An ire ghast has been slain by Labtik!");
        var f = Assert.Single(closed);
        Assert.Empty(tracker.Open);
        Assert.Equal("an ire ghast", f.Mob);
        Assert.True(f.Slain);
        Assert.Equal(9, f.Seconds);                       // seconds 1 to 9, inclusive
        Assert.Equal(867 + 133, f.Fighters["Venun"].Total.Damage);
        Assert.Equal("me", f.Fighters["Venun"].Kind);
        Assert.Equal(867, f.Fighters["Venun"].Abilities["Banish Undead"].Damage);
        Assert.Equal(1, f.Fighters["Venun"].Abilities["Melee"].Crits);
        Assert.Equal("pet", f.Fighters["Labtik"].Kind);
        Assert.Equal(139, f.Fighters["Labtik"].Total.Damage);
        Assert.Equal(1139, f.MyDamage);
        Assert.Equal(73, f.Taken);
        Assert.Equal(871, f.Fighters["Venun"].Healing);
    }

    [Fact]
    public void GroupMembersJoinIncludingTheSecondsBeforeYouEngaged()
    {
        var (closed, _) = Run(
            "0 Hellsfury slashes a necromancer initiate for 400 points of damage.",
            "1 A necromancer initiate hits Hellsfury for 30 points of damage.",
            "2 Sosk hit a necromancer initiate for 85 points of fire damage by Burn.",
            "3 You crush a necromancer initiate for 100 points of damage.",
            "4 Hellsfury slashes a necromancer initiate for 20 points of damage.",
            "5 Hellsfury slashes a bystander for 999 points of damage.",
            "6 You have slain a necromancer initiate!");
        var f = Assert.Single(closed);
        Assert.Equal(T0, f.Start);
        Assert.Equal(420, f.Fighters["Hellsfury"].Total.Damage);
        Assert.Equal("other", f.Fighters["Hellsfury"].Kind);
        Assert.Equal(85, f.Fighters["Sosk"].Abilities["Burn"].Damage);
        Assert.Equal(100, f.MyDamage);
        Assert.Equal(605, f.Damage);
        Assert.DoesNotContain("a necromancer initiate", f.Fighters.Keys);
    }

    [Fact]
    public void TwoQuietMinutesEndAFightAndAHitAndRunIsNotOne()
    {
        var (closed, tracker) = Run(
            "0 You crush a zombie for 50 points of damage.",
            "4 You crush a zombie for 50 points of damage.",
            "20 A crypt mummy punches YOU for 2 points of damage.",
            "100 Someone says, 'hello'",                             // 96 seconds quiet: the zombie fight is still open
            "200 Someone says, 'hello again'");
        var f = Assert.Single(closed);          // the mummy never took damage: not a fight
        Assert.Equal("a zombie", f.Mob);
        Assert.False(f.Slain);
        Assert.Equal(5, f.Seconds);
        Assert.Empty(tracker.Open);
    }

    [Fact]
    public void AD0InstanceIsACrawlForTheMeterToo()
    {
        var (closed, tracker) = Run(
            "0 You have entered The Estate of Unrest.",
            "10 Player Venun creating instance The Estate of Unrest 274.",
            "30 You have entered The Estate of Unrest.",                 // D0: the plain name
            "35 You crush a ghoul for 50 points of damage.",
            "36 You have slain a ghoul!",
            "300 You have entered Neriak - Commons.",                      // died
            "500 You have entered The Estate of Unrest.",                  // the open world: the way back is through the entrance
            "520 You have entered The Estate of Unrest.",                  // back in within 10 minutes: the same crawl
            "525 You crush a ghoul for 50 points of damage.",
            "526 You have slain a ghoul!",
            "900 You have entered The Estate of Unrest.",                  // walked out: the public zone, same name
            "905 You crush a rat for 50 points of damage.",
            "906 You have slain a rat!");
        Assert.Equal(3, closed.Count);
        Assert.All(closed.Take(2), f => Assert.Equal(0, f.Difficulty));
        Assert.Equal(closed[0].Stint, closed[1].Stint);
        Assert.Null(closed[2].Difficulty);
        Assert.False(tracker.Context.InInstance);
    }

    [Fact]
    public void AD0EntryFromANeighbourIsTheOpenWorldForTheMeterToo()
    {
        // visit-boundaries: out by a zone line to a neighbour, then the plain name twice: the open world, then the instance.
        var (closed, tracker) = Run(
            "0 You have entered Nagafen's Lair.",
            "40 Player Venun creating instance Nagafen's Lair 101.",
            "70 You have entered Nagafen's Lair.",
            "75 You crush a greater kobold for 50 points of damage.",
            "76 You have slain a greater kobold!",
            "300 You have entered Solusek's Eye.",
            "315 You have entered Nagafen's Lair.",                        // the open world
            "332 You have entered Nagafen's Lair.",                        // back in the instance: the same crawl
            "340 You crush a greater kobold for 50 points of damage.",
            "341 You have slain a greater kobold!");
        Assert.Equal(2, closed.Count);
        Assert.All(closed, f => Assert.Equal(0, f.Difficulty));
        Assert.Equal(closed[0].Stint, closed[1].Stint);
        Assert.True(tracker.Context.InInstance);
    }

    [Fact]
    public void AnotherFightersOnePointHitIsOnTheMeter()
    {
        var (closed, _) = Run(
            "0 You have entered Befallen 4 (Refined).",
            "1 Groupmate bashes a ghoul for 1 point of damage.",
            "2 You crush a ghoul for 50 points of damage.",
            "3 You have slain a ghoul!");
        var f = Assert.Single(closed);
        Assert.True(f.Fighters.TryGetValue("Groupmate", out var other), "the 1-point hit put no other fighter on the meter");
        Assert.Equal(1, other.Total.Damage);
        Assert.Equal(50, f.Fighters["Venun"].Total.Damage);
    }

    [Fact]
    public void ASessionStartsAfterThirtyMinutesAwayAndAStintWithTheZone()
    {
        var (closed, tracker) = Run(
            "0 You have entered Befallen 4 (Refined).",
            "5 You crush a zombie for 50 points of damage.",
            "6 You have slain a zombie!",
            "600 You have entered North Ro.",
            "700 You have entered Befallen 4 (Refined).",       // back within 10 minutes: the same crawl
            "705 You crush a zombie for 50 points of damage.",
            "706 You have slain a zombie!",
            "3000 Welcome to EverQuest Legends!",                 // 38 minutes later: a new session
            "3005 You crush a rat for 50 points of damage.",
            "3006 You have slain a rat!");
        Assert.Equal(3, closed.Count);
        Assert.Equal(closed[0].Session, closed[1].Session);
        Assert.Equal(closed[0].Stint, closed[1].Stint);
        Assert.Equal(T0, closed[0].Stint);
        Assert.Equal("Befallen", closed[0].Zone);
        Assert.Equal(4, closed[0].Difficulty);
        Assert.Equal(T0.AddSeconds(3000), closed[2].Session);
        Assert.Equal(T0.AddSeconds(3000), tracker.Context.Session);
    }

    [Fact]
    public void BackToBackCrawlsOfOneZoneAreTwoCrawls()
    {
        // Feedback #10: two Kedge Keep D4 runs in a row read as one 30-minute crawl on the meter. This is the log's own
        // order between two runs (the Befallen fixture): done, out to the public zone, a new instance, in again in minutes.
        var (closed, tracker) = Run(
            "0 You have entered Befallen 4 (Refined).",
            "5 You crush a zombie for 50 points of damage.",
            "6 You have slain a zombie!",
            "300 You have completed the Dungeon Crawl and earned reward loot!",
            "320 You have entered Befallen.",
            "400 Player Venun creating instance Befallen 776.",
            "420 You have entered Befallen 4 (Refined).",
            "425 You crush a zombie for 50 points of damage.",
            "426 You have slain a zombie!");
        Assert.Equal(2, closed.Count);
        Assert.Equal(T0, closed[0].Stint);
        Assert.Equal(T0.AddSeconds(420), closed[1].Stint);
        Assert.Equal(T0.AddSeconds(420), tracker.Context.Stint);
    }

    [Fact]
    public void ACrawlDoneOrANewInstanceEndsTheCrawlEvenWithoutTheOther()
    {
        // Done, out and back into the same instance: the crawl tracker starts a new visit, so the meter does too.
        var (done, _) = Run(
            "0 You have entered Befallen 4 (Refined).",
            "5 You crush a zombie for 50 points of damage.",
            "6 You have slain a zombie!",
            "300 You have completed the Dungeon Crawl and earned reward loot!",
            "320 You have entered Befallen.",
            "400 You have entered Befallen 4 (Refined).",
            "405 You crush a zombie for 50 points of damage.",
            "406 You have slain a zombie!");
        Assert.NotEqual(done[0].Stint, done[1].Stint);

        // Gave up on a run and made a new one, never finishing the first.
        var (fresh, _) = Run(
            "0 You have entered Befallen 4 (Refined).",
            "5 You crush a zombie for 50 points of damage.",
            "6 You have slain a zombie!",
            "320 You have entered Befallen.",
            "400 Player Venun creating instance Befallen 776.",
            "420 You have entered Befallen 4 (Refined).",
            "425 You crush a zombie for 50 points of damage.",
            "426 You have slain a zombie!");
        Assert.NotEqual(fresh[0].Stint, fresh[1].Stint);
    }

    [Fact]
    public void AD0CrawlDoneIsntResumedByThePlainName()
    {
        // D0 enters under the plain name. Once that crawl is done, the plain name a few minutes later is the public zone.
        var (done, doneTracker) = Run(
            "0 You have entered The Estate of Unrest.",
            "10 Player Venun creating instance The Estate of Unrest 274.",
            "30 You have entered The Estate of Unrest.",
            "35 You crush a ghoul for 50 points of damage.",
            "36 You have slain a ghoul!",
            "200 You have completed the Dungeon Crawl and earned reward loot!",
            "300 You have entered Neriak - Commons.",
            "400 You have entered The Estate of Unrest.",
            "405 You crush a rat for 50 points of damage.",
            "406 You have slain a rat!");
        Assert.Equal(0, done[0].Difficulty);
        Assert.Null(done[1].Difficulty);
        Assert.False(doneTracker.Context.InInstance);

        // A new D0 instance made before going back in is the next run, not the last one resumed.
        var (fresh, freshTracker) = Run(
            "0 You have entered The Estate of Unrest.",
            "10 Player Venun creating instance The Estate of Unrest 274.",
            "30 You have entered The Estate of Unrest.",
            "35 You crush a ghoul for 50 points of damage.",
            "36 You have slain a ghoul!",
            "300 You have entered Neriak - Commons.",
            "380 Player Venun creating instance The Estate of Unrest 281.",
            "400 You have entered The Estate of Unrest.",
            "405 You crush a ghoul for 50 points of damage.",
            "406 You have slain a ghoul!");
        Assert.Equal(0, fresh[1].Difficulty);
        Assert.NotEqual(fresh[0].Stint, fresh[1].Stint);
        Assert.True(freshTracker.Context.InInstance);
    }

    private static Fight Make(double start, double seconds, string mob, long mine, long other = 0, DateTime? stint = null, string ability = "Melee")
    {
        var f = new Fight { Start = T0.AddSeconds(start), End = T0.AddSeconds(start + seconds - 1), Mob = mob, Zone = "Befallen", Difficulty = 4, Session = T0, Stint = stint ?? T0 };
        f.Fighter("Venun", "me").Total.Damage = mine;
        f.Fighters["Venun"].Abilities[ability] = new AbilityStat { Damage = mine, Hits = 10, Max = 99 };
        if (other > 0) f.Fighter("Hellsfury", "other").Total.Damage = other;
        return f;
    }

    [Fact]
    public void MobsFoughtTogetherAreOneFight()
    {
        // A pull of three: all of them swing at you at once, and you kill them one after another (10 s each, 1,000 each).
        // Mob by mob the last looked slow (1,000 over 30 s) and the first fast; as one fight it's 3,000 over 30 s.
        var pull = new List<Fight> { Make(0, 10, "a ghoul", 1000), Make(0, 20, "a skeleton", 1000), Make(0, 30, "a ghoul", 1000) };
        var later = Make(120, 10, "a rat", 500);
        var frame = new MeterFrame("Venun", "Befallen", 4, true, null, T0, T0, [.. pull, later], [],
            new Usual(10, 100, 80, new Dictionary<string, double> { ["Melee"] = 100 }));

        Assert.Equal(2, MeterViews.Encounters(frame.Fights).Count);
        var mid = frame with { Fights = pull };
        var fight = MeterViews.Build(mid, new MeterQuery("fight", "fighter"));
        Assert.Equal("a ghoul +2", fight.Title);
        Assert.Equal(30, fight.Seconds);
        Assert.Equal(100, fight.PerSecond);
        Assert.Equal(1.0, fight.UsualPct);                     // 100 against a usual 100
        Assert.StartsWith("3 mobs · ", fight.Footer);

        var list = MeterViews.Build(frame, new MeterQuery("place", "fight"));
        Assert.Equal(["a rat", "a ghoul +2"], list.Rows.Select(r => r.Label));
        var row = list.Rows[1];
        Assert.Equal((3000L, 30.0, 100.0, 0), (row.Amount, row.Seconds, row.PerSecond, row.Count));   // the label says how many
        Assert.StartsWith("4 kills in 2 fights · ", list.Footer);

        // Opening the row opens the whole pull.
        var opened = MeterViews.Build(frame, new MeterQuery("place", "fight", FightStart: row.FightStart, FightMob: row.Key));
        Assert.True(opened.CanBack);
        Assert.Equal("a ghoul +2", opened.Title);
        Assert.Equal(3000, Assert.Single(opened.Rows).Amount);

        // Same-named mobs only: "×".
        Assert.Equal("a ghoul ×2", MeterViews.EncounterLabel([Make(0, 10, "a ghoul", 1000), Make(0, 20, "a ghoul", 1000)]));
        // A mob that starts more than 2 s after the last one ends is a new fight.
        Assert.Equal(2, MeterViews.Encounters([Make(0, 10, "a ghoul", 1000), Make(12, 10, "a ghoul", 1000)]).Count);   // 3 s after
        Assert.Single(MeterViews.Encounters([Make(0, 10, "a ghoul", 1000), Make(11, 10, "a ghoul", 1000)]));             // 2 s after
    }

    [Fact]
    public void SlicesAddUpAndOverlappingFightsCountTheirTimeOnce()
    {
        // Two mobs at once for 10 s, then one alone for 10 s: 20 s in combat, 3,000 damage of mine.
        var fights = new List<Fight> { Make(0, 10, "a zombie", 1000, 500), Make(0, 10, "a rat", 1000), Make(60, 10, "a zombie", 1000) };
        Assert.Equal(20, MeterViews.CombatSeconds(fights));
        var frame = new MeterFrame("Venun", "Befallen", 4, true, null, T0, T0, fights, [], new Usual(10, 100, 80, new Dictionary<string, double> { ["Melee"] = 100 }));

        var byFighter = MeterViews.Build(frame, new MeterQuery("place", "fighter"));
        Assert.Equal("Befallen D4", byFighter.Title);
        Assert.Equal("Crawl", byFighter.PlaceLabel);
        Assert.Equal(175, byFighter.PerSecond);                // 3,500 over 20 s
        Assert.Equal(1.5, byFighter.UsualPct);                 // mine: 150 against a usual 100
        Assert.Equal(["Venun", "Hellsfury"], byFighter.Rows.Select(r => r.Label));
        Assert.Equal(3000, byFighter.Rows[0].Amount);

        var byMob = MeterViews.Build(frame, new MeterQuery("session", "mob"));
        Assert.Equal(["a zombie", "a rat"], byMob.Rows.Select(r => r.Label));
        Assert.Equal(2, byMob.Rows[0].Count);
        Assert.Equal(100, byMob.Rows[0].PerSecond);            // 2,000 over its own 20 s
        Assert.Equal(1.25, byMob.Rows[0].UsualPct);            // against the per-fight usual, 80

        var bySpell = MeterViews.Build(frame, new MeterQuery("place", "spell"));
        Assert.Equal(1.5, Assert.Single(bySpell.Rows).UsualPct);

        var lastFight = MeterViews.Build(frame, new MeterQuery("fight", "mob"));   // a fight has no "by mob"
        Assert.Equal("fighter", lastFight.By);
        Assert.Equal("a zombie", lastFight.Title);
        Assert.Equal(100, lastFight.PerSecond);

        var opened = MeterViews.Build(frame, new MeterQuery("session", "mob", Mob: "a zombie"));
        Assert.True(opened.CanBack);
        Assert.Equal(2, opened.Rows.Count);
        Assert.All(opened.Rows, r => Assert.Equal("fight", r.Open));

        var spells = MeterViews.Build(frame, new MeterQuery("place", "fighter", Fighter: "Venun"));
        Assert.Equal("ability", Assert.Single(spells.Rows).Layout);
        Assert.Equal(30, spells.Rows[0].Hits);
    }

    [Fact]
    public void UsualNeedsFiveFightsAndSkipsPreNerfDdd()
    {
        var now = T0.AddDays(1);
        Assert.Null(Usual.From(Enumerable.Range(0, 4).Select(i => Make(i * 60, 10, "a rat", 1000)), now));
        var usual = Usual.From(Enumerable.Range(0, 6).Select(i => Make(i * 60, 10, "a rat", 1000)), now)!;
        Assert.Equal(100, usual.Dps, 6);
        Assert.Equal(100, usual.ByAbility["Melee"], 6);

        // Before the 11-10 patch and mostly Denon's Desperate Dirge: left out.
        var old = Enumerable.Range(0, 6).Select(i =>
        {
            var f = Make(i * 60, 10, "a rat", 5000, ability: "Denon's Desperate Dirge");
            f.Start = new DateTime(2026, 11, 1).AddSeconds(i * 60);
            f.End = f.Start.AddSeconds(9);
            return f;
        });
        Assert.Null(Usual.From(old, now));
    }

    [Fact]
    public void FightsSurviveTheStore()
    {
        var path = Path.Combine(Path.GetTempPath(), "pf-fights-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var store = new FightStore(path))
            {
                var f = Make(0, 10, "a zombie", 1000, 500);
                f.Trio = "SHD/ROG/BER";
                f.Taken = 40;
                store.Save("Venun_oggok", [f, f], new FightCheckpoint { Version = 1, Offsets = { ["log"] = 123 } });   // the same fight twice is one row
                var back = Assert.Single(store.Session("Venun_oggok", T0));
                Assert.Equal((f.Start, f.End, "a zombie", "Befallen", 4, "SHD/ROG/BER"), (back.Start, back.End, back.Mob, back.Zone, back.Difficulty, back.Trio));
                Assert.Equal(1000, back.MyDamage);
                Assert.Equal(1500, back.Damage);
                Assert.Equal(40, back.Taken);
                Assert.Single(store.InPlace("Venun_oggok", "Befallen", 4, "SHD/ROG/BER"));
                Assert.Empty(store.InPlace("Venun_oggok", "Befallen", 4, null));
                Assert.Equal(123, store.LoadCheckpoint("Venun_oggok")!.Offsets["log"]);
                Assert.Equal(T0, store.SessionBefore("Venun_oggok", T0.AddHours(1)));
            }
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); foreach (var p in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*")) File.Delete(p); }
    }

    [Fact]
    public void FrenzyHitsTheMobNotAMobNamedOn()
    {
        // 11-17: "You frenzy on Lord of Ire" opened a fight for "on Lord of Ire" beside the real one: "Lord of Ire +1".
        var (closed, _) = Run(
            "0 You slash Lord of Ire for 60 points of damage.",
            "1 You frenzy on Lord of Ire for 223 points of damage.",
            "2 You frenzy on Lord of Ire for 92 points of damage. (Critical)",
            "3 You have slain Lord of Ire!");
        var f = Assert.Single(closed);
        Assert.Equal("Lord of Ire", f.Mob);
        Assert.Equal(375, f.Damage);
        Assert.Equal("Lord of Ire", MeterViews.EncounterLabel(MeterViews.Encounters(closed).Single()));
    }

    [Fact]
    public void MeleeSkillsGetTheirOwnRowsAndAutoAttackStaysMelee()
    {
        // 11-17: Backstab and the other skills were all "Melee". Skills land on a timer (5–10 s), auto-attack every 1–2 s.
        var (closed, _) = Run(
            "0 You slash a gnoll for 60 points of damage.",
            "0 You pierce a gnoll for 40 points of damage.",
            "1 You backstab a gnoll for 300 points of damage. (Critical)",
            "2 You kick a gnoll for 20 points of damage.",
            "3 You frenzy on a gnoll for 90 points of damage.",
            "4 Tarn backstabs a gnoll for 200 points of damage.",
            "5 Tarn slashes a gnoll for 30 points of damage.",
            "6 You have slain a gnoll!");
        var me = Assert.Single(closed).Fighters["Venun"].Abilities;
        Assert.Equal(100, me["Melee"].Damage);
        Assert.Equal(300, me["Backstab"].Damage);
        Assert.Equal(1, me["Backstab"].Crits);
        Assert.Equal(20, me["Kick"].Damage);
        Assert.Equal(90, me["Frenzy"].Damage);
        var tarn = closed[0].Fighters["Tarn"].Abilities;
        Assert.Equal(200, tarn["Backstab"].Damage);
        Assert.Equal(30, tarn["Melee"].Damage);
    }
}
