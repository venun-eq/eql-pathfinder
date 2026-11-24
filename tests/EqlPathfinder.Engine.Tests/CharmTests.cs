using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.Tests;

/// <summary>A charmed mob is your pet only while the charm holds; its name belongs to every other mob like it.</summary>
public class CharmTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 20, 0, 0);

    /// <summary>Each line is "+seconds body"; returns your pet's damage events and the parser.</summary>
    private static (List<DamageDealt> Pet, LineParser Parser, List<LogEvent> All) Run(ISet<string>? pets, params string[] lines)
    {
        var parser = new LineParser("Venun", pets ?? new HashSet<string>());
        var all = new List<LogEvent>();
        foreach (var l in lines)
        {
            int sp = l.IndexOf(' ');
            var e = parser.Parse(new LogLine(T0.AddSeconds(double.Parse(l[..sp], System.Globalization.CultureInfo.InvariantCulture)), l[(sp + 1)..]));
            if (e != null) all.Add(e);
        }
        return (all.OfType<DamageDealt>().Where(d => d.Source == DamageSource.Pet).ToList(), parser, all);
    }

    private const string Charm = "0 A fetid fiend told you, 'Attacking a turmoil toad Master.'";

    [Fact]
    public void ItsHitsCountFromMasterUntilTheCharmSpellWearsOff()
    {
        var (pet, parser, _) = Run(null,
            "-10 A fetid fiend slashes a turmoil toad for 50 points of damage.",     // before it's yours: a mob hitting a mob
            "-3 You begin casting Cajole Undead VI.",
            Charm,
            "1 A fetid fiend slashes a turmoil toad for 112 points of damage.",
            "2 a fetid fiend hit a turmoil toad for 20 points of disease damage by Strong Disease.",
            "3 A fetid fiend slashes Drakoh for 300 points of damage.",              // a wild one on a group member
            "30 Your Odium X spell has worn off of a fetid fiend.",                  // not the charm
            "31 A fetid fiend slashes a turmoil toad for 40 points of damage.",
            "40 Your Cajole Undead VI spell has worn off of a fetid fiend.",
            "41 A fetid fiend slashes a turmoil toad for 999 points of damage.");
        Assert.Equal([112, 20, 40], pet.Select(d => d.Amount));
        Assert.Null(parser.CharmedPet);
    }

    [Theory]
    [InlineData("5 A fetid fiend hits YOU for 80 points of damage.")]
    [InlineData("5 A fetid fiend tries to hit YOU, but misses!")]
    [InlineData("5 A fetid fiend has been slain by Hellsfury!")]
    [InlineData("5 You have entered The Plane of Hate.")]
    [InlineData("5 You have been slain by a turmoil toad!")]
    [InlineData("5 You do not have a pet.")]
    [InlineData("5 Labtik told you, 'Attacking a turmoil toad Master.'")]
    [InlineData("130 Someone says, 'two quiet minutes'")]
    public void TheCharmEndsWhen(string line)
    {
        var (pet, parser, _) = Run(null, Charm, "1 A fetid fiend slashes a turmoil toad for 10 points of damage.", line,
            "140 A fetid fiend slashes a turmoil toad for 999 points of damage.");
        Assert.Equal(10, Assert.Single(pet).Amount);
        Assert.Null(parser.CharmedPet);
    }

    [Fact]
    public void ItIsYoursAgainWhenItSpeaksOrTheSongLandsAgain()
    {
        var (pet, _, _) = Run(null,
            "-2 You begin singing Solon's Bewitching Bravura X.",
            Charm,
            "5 A fetid fiend hits YOU for 80 points of damage.",                     // broke
            "6 A fetid fiend slashes a turmoil toad for 1 points of damage.",
            "8 a fetid fiend's eyes glaze over.",                                     // the song took it back
            "9 A fetid fiend slashes a turmoil toad for 20 points of damage.",
            "20 A fetid fiend has been slain by a turmoil toad!",
            "21 A fetid fiend slashes a turmoil toad for 2 points of damage.",
            "30 A fetid fiend told you, 'Attacking a turmoil toad Master.'",          // a new one charmed
            "31 A fetid fiend slashes a turmoil toad for 30 points of damage.");
        Assert.Equal([20, 30], pet.Select(d => d.Amount));
    }

    [Fact]
    public void TheSpellYouCastBeforeItCalledYouMasterIsLearnedAsACharm()
    {
        var (pet, parser, _) = Run(null,
            "-4 You begin casting Bind Wyrm III.",
            Charm,
            "10 Your Bind Wyrm III spell has worn off of a fetid fiend.",
            "11 A fetid fiend slashes a turmoil toad for 999 points of damage.");
        Assert.Empty(pet);
        Assert.Null(parser.CharmedPet);
    }

    [Fact]
    public void OldCharmedNamesAreDroppedAndSummonedPetsStay()
    {
        var pets = new HashSet<string> { "Labtik", "Venun`s warder", "A vis ghoul knight", "an abhorrent" };
        var (pet, _, all) = Run(pets,
            "0 A vis ghoul knight slashes Drakoh for 500 points of damage.",
            "1 Labtik punches a zombie for 70 points of damage.",
            "2 Venun`s warder bites a zombie for 30 points of damage.",
            "3 You hit yourself for 55 points of magic damage by Burnout.");
        Assert.Equal(["Labtik", "Venun`s warder"], pets.Order(StringComparer.Ordinal));
        Assert.Equal([70, 30], pet.Select(d => d.Amount));
        Assert.DoesNotContain(all, e => e is DamageDealt { Target: "yourself" });
    }

    [Theory]
    [InlineData("Labtik", false)]
    [InlineData("Venun`s warder", false)]
    [InlineData("A fetid fiend", true)]
    [InlineData("Cleric of Innoruuk", true)]
    [InlineData("Amygdalan knight", true)]
    public void MobNamesAndPetNames(string name, bool mob) => Assert.Equal(mob, LineParser.IsMobName(name, "Venun"));

    private static LogEvent? At(LineParser p, double s, string body) => p.Parse(new LogLine(T0.AddSeconds(s), body));

    [Fact]
    public void ARestartMidCharmKeepsThePet()
    {
        // The charm state is saved with the checkpoint: what comes back from JSON carries on where the app stopped.
        var state = new CharmState();
        var before = new LineParser("Venun", new HashSet<string>(), state);
        At(before, 0, "A fetid fiend told you, 'Attacking a turmoil toad Master.'");
        var saved = System.Text.Json.JsonSerializer.Deserialize<CharmState>(System.Text.Json.JsonSerializer.Serialize(state))!;

        var after = new LineParser("Venun", new HashSet<string>(), saved);
        Assert.Equal("A fetid fiend", after.CharmedPet);
        var hit = Assert.IsType<DamageDealt>(At(after, 5, "A fetid fiend slashes a turmoil toad for 112 points of damage."));
        Assert.Equal((DamageSource.Pet, 112), (hit.Source, hit.Amount));
    }

    [Fact]
    public void ItsDamageCountsOnlyOnItsOwnTargets()
    {
        var p = new LineParser("Venun", new HashSet<string>());
        At(p, 0, "A fetid fiend told you, 'Attacking a turmoil toad Master.'");
        Assert.NotNull(At(p, 1, "A fetid fiend slashes a turmoil toad for 10 points of damage."));     // announced
        Assert.Null(At(p, 2, "A fetid fiend slashes a spite golem for 20 points of damage."));         // another one's fight
        At(p, 3, "You crush a spite golem for 50 points of damage.");                                   // now you're on it
        Assert.NotNull(At(p, 4, "A fetid fiend slashes a spite golem for 30 points of damage."));
        At(p, 5, "A scorn banshee hits YOU for 40 points of damage.");                                  // and what's on you
        Assert.NotNull(At(p, 6, "A fetid fiend slashes a scorn banshee for 40 points of damage."));
        Assert.Equal((80L, 20L), (p.CharmDamageKept, p.CharmDamageDropped));
    }

    [Fact]
    public void AOneWordNameIsACharmWhenTheCharmLandedOnItOrItFoughtYou()
    {
        // The charm visibly landed on it: charmed, never remembered as a summoned pet.
        var pets = new HashSet<string>();
        var p = new LineParser("Venun", pets);
        At(p, 0, "Dread has been charmed.");
        var id = Assert.IsType<PetIdentified>(At(p, 2, "Dread told you, 'Attacking a turmoil toad Master.'"));
        Assert.True(id.Charmed);
        Assert.Equal("Dread", p.CharmedPet);
        Assert.Empty(pets);

        // It hit you earlier in this zone: a mob, so a charm.
        var q = new LineParser("Venun", new HashSet<string>());
        At(q, 0, "Dread hits YOU for 300 points of damage.");
        Assert.True(Assert.IsType<PetIdentified>(At(q, 60, "Dread told you, 'Attacking a turmoil toad Master.'")).Charmed);

        // No sign of a charm: a summoned pet. If it ever hits you it was a mob after all, and it's forgotten.
        var pets2 = new HashSet<string> { "Dread" };
        var r = new LineParser("Venun", pets2);
        Assert.False(Assert.IsType<PetIdentified>(At(r, 0, "Labtik told you, 'Attacking a turmoil toad Master.'")).Charmed);
        At(r, 5, "Dread hits YOU for 300 points of damage.");
        Assert.Empty(pets2);
        Assert.Null(At(r, 6, "Dread slashes a turmoil toad for 50 points of damage."));
    }
}
