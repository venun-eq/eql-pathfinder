using EqlPathfinder.Engine.Combat;
using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.Tests;

public class FightMeterPetTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 20, 0, 0);

    /// <summary>A 10-second fight: you, your pet (Gobekar), and a group member with a pet nobody can tie to them.</summary>
    private static MeterFrame Frame()
    {
        var f = new Fight { Start = T0, End = T0.AddSeconds(9), Mob = "a rat", Zone = "Befallen", Difficulty = 4, Session = T0, Stint = T0 };
        f.Fighter("Venun", "me").Total.Damage = 600;
        f.Fighters["Venun"].Abilities["Fireball"] = new AbilityStat { Damage = 600, Hits = 3, Max = 250 };
        var pet = f.Fighter("Gobekar", "pet");
        pet.Total = new AbilityStat { Damage = 400, Hits = 20, Crits = 2, Max = 40 };
        pet.Abilities["melee"] = new AbilityStat { Damage = 400, Hits = 20, Crits = 2, Max = 40 };
        f.Fighter("Hellsfury", "other").Total.Damage = 300;
        f.Fighter("Xanobek", "other").Total.Damage = 100;
        return new MeterFrame("Venun", "Befallen", 4, true, null, T0, T0, [f], [],
            new Usual(10, 100, 100, new Dictionary<string, double> { ["Fireball"] = 50, [Usual.PetKey] = 50 }));
    }

    [Fact]
    public void YourPetCountsAsYouAndOpensUnderYou()
    {
        var byFighter = MeterViews.Build(Frame(), new MeterQuery("fight", "fighter"));
        // You and Gobekar are one row (1,000), with the pet's share named; a group member's pet stays its own row.
        Assert.Equal(["Venun", "Hellsfury", "Xanobek"], byFighter.Rows.Select(r => r.Label));
        var me = byFighter.Rows[0];
        Assert.Equal((1000L, "me", "incl. pet 40%", "fighter", "Venun"), (me.Amount, me.Kind, me.Sub, me.Open, me.Key));
        Assert.Equal(1.0, byFighter.UsualPct);   // you and your pet, 100/s, against a usual of 100

        var opened = MeterViews.Build(Frame(), new MeterQuery("fight", "fighter", Fighter: "Venun"));
        Assert.Equal(["Fireball", "Gobekar (pet)"], opened.Rows.Select(r => r.Label));
        Assert.Equal(100, opened.PerSecond);
        var pet = opened.Rows[1];
        Assert.Equal((400L, 20, 2, 40, "fighter", "Gobekar"), (pet.Amount, pet.Hits, pet.Crits, pet.Max, pet.Open, pet.Key));
        Assert.Equal(0.8, pet.UsualPct);          // 40/s against its usual 50/s
        Assert.Equal(["melee"], MeterViews.Build(Frame(), new MeterQuery("fight", "fighter", Fighter: "Gobekar")).Rows.Select(r => r.Label));
    }

    [Fact]
    public void APetsHitsBeforeItNamedItselfAreYoursToo()
    {
        // The first fight: Gobekar fought before saying "Attacking … Master." and was taken for another player.
        var early = new Fight { Start = T0.AddSeconds(-60), End = T0.AddSeconds(-51), Mob = "a rat", Zone = "Befallen", Difficulty = 4, Session = T0, Stint = T0 };
        early.Fighter("Venun", "me").Total.Damage = 100;
        early.Fighter("Gobekar", "other").Total = new AbilityStat { Damage = 250, Hits = 10, Max = 30 };
        var later = Frame();
        var frame = later with { Fights = [early, .. later.Fights] };
        var byFighter = MeterViews.Build(frame, new MeterQuery("session", "fighter"));
        Assert.Equal(["Venun", "Hellsfury", "Xanobek"], byFighter.Rows.Select(r => r.Label));
        Assert.Equal(1350, byFighter.Rows[0].Amount);   // 700 of yours, 650 of Gobekar's
        var opened = MeterViews.Build(frame, new MeterQuery("session", "fighter", Fighter: "Venun"));
        Assert.Equal(650, opened.Rows.Single(r => r.Label == "Gobekar (pet)").Amount);

        // Live, the fight going on when the pet names itself moves its hits to you.
        var tracker = new FightTracker("Venun", new FightContext(), _ => { });
        var t = T0;
        tracker.Apply(new DamageDealt(t, DamageSource.Melee, "a bat", 50, null, false));
        tracker.Apply(new OtherDamage(t.AddSeconds(1), "Gobekar", "a bat", 30, null, false));
        tracker.Apply(new PetIdentified(t.AddSeconds(2), "Gobekar"));
        var open = Assert.Single(tracker.Open);
        Assert.Equal("pet", open.Fighters["Gobekar"].Kind);
        Assert.Equal(80, open.MyDamage);
    }
}
