using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Parsing;
using EqlPathfinder.Engine.Tracking;

namespace EqlPathfinder.Engine.Tests;

public class TrioInferenceTests
{
    private static readonly DateTime T0 = new(2026, 8, 2, 20, 0, 0);
    private static int _id;

    /// <summary>The spell table as spells_us.txt has it for these names (classes that can cast each).</summary>
    private static readonly Dictionary<string, int> Spells = new()
    {
        ["Color Flux"] = TrioInference.Mask("ENC"),
        ["Mesmerization"] = TrioInference.Mask("ENC"),
        ["Light Healing"] = TrioInference.Mask("CLR/PAL/DRU/SHM"),
        ["Togor's Insects"] = TrioInference.Mask("SHM"),
        ["Spirit of the Puma"] = TrioInference.Mask("SHM"),
        ["Lifedraw"] = TrioInference.Mask("SHD/NEC"),
        ["Pacify"] = TrioInference.Mask("CLR/PAL/ENC"),
        ["Rampage"] = TrioInference.Mask("WAR/CLR/PAL/RNG/SHD/DRU/MNK/ROG/SHM/NEC/WIZ/MAG/ENC/BST/BER"),
    };

    /// <summary>A closed visit an hour after the previous one, at continuing levels; <paramref name="who"/> = a /who showed the trio.</summary>
    private static Visit V(int hour, int level, string? trio, bool who, params (string Spell, int Casts)[] casts) => new()
    {
        Id = $"t-{++_id}",
        Zone = "Befallen 4",
        BaseZone = "Befallen",
        Difficulty = 4,
        Start = T0.AddHours(hour),
        ActiveSeconds = 1800,
        Closed = true,
        Completed = true,
        Trio = trio,
        TrioConfirmed = who,
        Race = trio != null ? "Ogre" : null,
        LevelStart = level,
        LevelEnd = level + 2,
        Casts = casts.ToDictionary(c => c.Spell, c => c.Casts),
    };

    [Fact]
    public void BeginCastingLinesAreTalliedPerVisitWithoutTheRank()
    {
        var p = new LineParser("Venun", new HashSet<string>());
        Assert.True(LogLine.TryParse("[Sat Nov 14 10:00:00 2026] You begin casting Color Flux II.", out var line));
        Assert.Equal("Color Flux", Assert.IsType<SpellCast>(p.Parse(line)).Spell);

        var s = new TrackerTests.Sink();
        var e = new PathfinderEngine("Venun", new TrackerOptions(), s);
        foreach (var l in new[]
        {
            "[Sat Nov 14 10:00:00 2026] You have entered Befallen 4 (Refined).",
            "[Sat Nov 14 10:01:00 2026] You begin casting Color Flux.",
            "[Sat Nov 14 10:02:00 2026] You begin casting Color Flux II.",
            "[Sat Nov 14 10:03:00 2026] You begin casting Mesmerization.",
        }) e.ProcessLine(l);
        e.Flush();
        var casts = e.State.Current!.Casts;
        Assert.Equal(2, casts["Color Flux"]);
        Assert.Equal(1, casts["Mesmerization"]);
    }

    [Fact]
    public void ACarriedTrioTheSpellsFitIsChecked()
    {
        var a = V(0, 10, "PAL/MNK/ENC", true);
        var b = V(1, 12, "PAL/MNK/ENC", false, ("Color Flux", 30), ("Light Healing", 12));
        var calls = TrioInference.Attribute([a, b], Spells);
        Assert.Equal(("PAL/MNK/ENC", "who"), (calls[a.Id].Trio, calls[a.Id].Source));
        Assert.Equal(("PAL/MNK/ENC", "spells"), (calls[b.Id].Trio, calls[b.Id].Source));
    }

    [Fact]
    public void ShamanSpellsUnderACarriedEnchanterSwapTheOneClassThatChanged()
    {
        // The loadout changed with no /who: the log only shows the spells. Any trio with SHM explains them, so SHM is
        // known; PAL and MNK stay from what was carried. The next visit, with nothing cast, carries the new trio on.
        var a = V(0, 10, "PAL/MNK/ENC", true, ("Color Flux", 20));
        var b = V(1, 22, "PAL/MNK/ENC", false, ("Togor's Insects", 17), ("Spirit of the Puma", 11), ("Light Healing", 8));
        var c = V(2, 24, "PAL/MNK/ENC", false);
        var d = V(3, 26, "PAL/MNK/SHM", true);
        var calls = TrioInference.Attribute([a, b, c, d], Spells);
        Assert.Equal("PAL/MNK/SHM", calls[b.Id].Trio);
        Assert.Equal("Ogre", calls[b.Id].Race);   // two classes stayed: same loadout's race
        Assert.Contains("SHM", calls[b.Id].Known);
        Assert.Equal(("PAL/MNK/SHM", "carried"), (calls[c.Id].Trio, calls[c.Id].Source));
        Assert.Equal("who", calls[d.Id].Source);
    }

    /// <summary>A PAL/MNK/SHM raid visit's casts (11-17): Pacify and shaman spells, nothing from the monk.</summary>
    private static readonly (string, int)[] PalShmCasts = [("Pacify", 7), ("Togor's Insects", 4), ("Spirit of the Puma", 4), ("Light Healing", 5)];

    [Fact]
    public void ATieGoesToTheTrioAWhoShowsSoonAfter()
    {
        // 11-17: Venun swapped from SHD/ROG/BER to PAL/MNK/SHM with no /who. The spells fit PAL/MNK/SHM and PAL/SHD/SHM alike;
        // keeping SHD from the carried trio named a loadout Venun never had, though a /who 9 minutes later said PAL/MNK/SHM.
        var a = V(0, 46, "SHD/ROG/BER", true, ("Lifedraw", 20));
        var b = V(1, 50, "SHD/ROG/BER", false, PalShmCasts);
        var c = V(2, 50, "PAL/MNK/SHM", true);
        c.Race = "Froglok";
        var call = TrioInference.Attribute([a, b, c], Spells)[b.Id];
        Assert.Equal("PAL/MNK/SHM", call.Trio);
        Assert.Equal("Froglok", call.Race);   // that loadout's own race, not the carried one's
    }

    [Fact]
    public void WithNoWhoSoonAfterATieGoesToALoadoutYouHave()
    {
        // No /who for days after: PAL/MNK/SHM, carried into an earlier visit from a /who outside it, beats PAL/SHD/SHM,
        // which keeps a class from the carried trio but was never anyone's loadout.
        var main = V(-200, 50, "PAL/MNK/SHM", false);
        var a = V(0, 46, "SHD/ROG/BER", true, ("Lifedraw", 20));
        var b = V(1, 50, "SHD/ROG/BER", false, PalShmCasts);
        var late = V(30, 50, "SHD/ROG/BER", true);   // more than a day later: says nothing about b
        var calls = TrioInference.Attribute([main, a, b, late], Spells);
        Assert.Equal("PAL/MNK/SHM", calls[b.Id].Trio);
    }

    [Fact]
    public void SpellsThatFitTheWhoTrioAgainGoBackToIt()
    {
        // 10-05: a guess swapped the /who's RNG/BRD/ROG out; the next visits' spells fit RNG/BRD/ROG again, and that beat
        // another loadout that kept more of the guess. Here: PAL/MNK/ENC, guessed SHD/SHM/NEC, then enchanter spells.
        var x = V(-100, 50, "SHD/SHM/NEC", true);
        var y = V(-90, 50, "SHD/NEC/ENC", true);
        var a = V(0, 10, "PAL/MNK/ENC", true);
        var b = V(1, 12, "PAL/MNK/ENC", false, ("Togor's Insects", 10), ("Lifedraw", 10));
        var c = V(2, 30, "PAL/MNK/ENC", false, ("Color Flux", 20));
        var calls = TrioInference.Attribute([x, y, a, b, c], Spells);
        Assert.Equal("SHD/SHM/NEC", calls[b.Id].Trio);
        Assert.Equal("PAL/MNK/ENC", calls[c.Id].Trio);   // not SHD/NEC/ENC, two classes closer to the guess
    }

    [Fact]
    public void AFewStrayCastsDoNotOverturnATrio()
    {
        // 3 shaman casts among 30 enchanter ones (a clicky, a scroll): the carried trio still explains 91%.
        var a = V(0, 10, "PAL/MNK/ENC", true);
        var b = V(1, 12, "PAL/MNK/ENC", false, ("Color Flux", 30), ("Togor's Insects", 3));
        Assert.Equal("PAL/MNK/ENC", TrioInference.Attribute([a, b], Spells)[b.Id].Trio);
    }

    [Fact]
    public void WhoWinsAndOneCastOrASpellEveryoneHasSaysNothing()
    {
        var a = V(0, 10, "PAL/MNK/ENC", true, ("Togor's Insects", 40));
        var b = V(1, 12, "PAL/MNK/ENC", false, ("Togor's Insects", 1), ("Rampage", 50));
        var calls = TrioInference.Attribute([a, b], Spells);
        Assert.Equal(("PAL/MNK/ENC", "who"), (calls[a.Id].Trio, calls[a.Id].Source));
        Assert.Equal(("PAL/MNK/ENC", "carried"), (calls[b.Id].Trio, calls[b.Id].Source));
    }

    [Fact]
    public void ProcsAreNotCasts()
    {
        // Right after SHD and WIZ reincarnated, Lifedraw procs did damage in a PAL/MNK/ENC relevel. Damage is not evidence.
        var a = V(0, 1, "PAL/MNK/ENC", true);
        var b = V(1, 3, "PAL/MNK/ENC", false, ("Color Flux", 8));
        b.Damage.ByAbility["Lifedraw"] = 5000;
        b.Damage.Total = 6000;
        Assert.Equal("PAL/MNK/ENC", TrioInference.Attribute([a, b], Spells)[b.Id].Trio);
    }

    [Fact]
    public void AVisitWithNoTrioGetsOneFromItsSpells()
    {
        var a = V(0, 10, null, false, ("Color Flux", 20), ("Mesmerization", 9), ("Togor's Insects", 6));
        var call = TrioInference.Attribute([a], Spells)[a.Id];
        Assert.Equal("partial", call.Source);
        Assert.Equal(["SHM", "ENC"], call.Known);
        Assert.Contains("SHM", call.Trio!.Split('/'));
        Assert.Contains("ENC", call.Trio!.Split('/'));
    }

    [Fact]
    public void WithoutTheSpellFileItIsThePlainAttribution()
    {
        var a = V(0, 10, null, false, ("Togor's Insects", 20));
        var b = V(1, 12, "PAL/MNK/ENC", true);
        var calls = TrioInference.Attribute([a, b], null);
        Assert.Equal(("PAL/MNK/ENC", "carried"), (calls[a.Id].Trio, calls[a.Id].Source));   // filled back from the next /who
    }

    [Fact]
    public void ASlotVisitIsSlotAndKeepsItsTrioWhenItsCastsFitAnother()
    {
        // The slot's last /who named PAL/MNK/ENC; the casts fit a shaman. The slot is the game's word: spells never overwrite it.
        var a = V(0, 10, "PAL/MNK/ENC", true);
        var b = V(1, 12, "PAL/MNK/ENC", false, ("Togor's Insects", 20), ("Spirit of the Puma", 10));
        b.TrioFrom = "slot";
        b.Slot = 6;
        var call = TrioInference.Attribute([a, b], Spells)[b.Id];
        Assert.Equal(("PAL/MNK/ENC", "slot"), (call.Trio, call.Source));
        Assert.Equal("slot", TrioInference.Attribute([a, b], null)[b.Id].Source);
    }

    [Fact]
    public void AnUnmetSlotVisitHasNoTrioWhateverItsCastsOrTheNextWhoSay()
    {
        var a = V(0, 10, "PAL/MNK/ENC", true);
        var b = V(1, 1, null, false, ("Color Flux", 20), ("Mesmerization", 9));
        b.TrioFrom = "unmet";
        b.Slot = 2;
        var c = V(2, 3, "SHD/SHM/WIZ", true);
        var withSpells = TrioInference.Attribute([a, b, c], Spells)[b.Id];
        var without = TrioInference.Attribute([a, b, c], null)[b.Id];
        Assert.Equal((null, "unmet"), (withSpells.Trio, withSpells.Source));
        Assert.Equal((null, "unmet"), (without.Trio, without.Source));
    }

    [Fact]
    public void VisitStatsCarriesTheSlotForTheTooltip()
    {
        var v = V(0, 10, "PAL/MNK/ENC", false);
        v.Slot = 6;
        Assert.Equal(6, VisitStats.From(v, v.Start.AddHours(1), new TrackerOptions()).Slot);
    }
}
