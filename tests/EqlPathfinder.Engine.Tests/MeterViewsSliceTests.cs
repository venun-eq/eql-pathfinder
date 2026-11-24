using EqlPathfinder.Engine.Combat;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Direct tests for <see cref="MeterViews"/> scope resolution, footer precedence, and healing/taken slice builders.
/// </summary>
public class MeterViewsSliceTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 20, 0, 0);

    private static Fight MakeFight(int startSec, int endSec, string mob, long myDamage = 500, int kills = 1,
        long healing = 0, long taken = 0, DateTime? stint = null)
    {
        var f = new Fight
        {
            Start = T0.AddSeconds(startSec),
            End = T0.AddSeconds(endSec),
            Mob = mob,
            Kills = kills,
            Taken = taken,
            Stint = stint ?? T0,
        };
        var me = new FighterStat { Kind = "me" };
        me.Total.Damage = myDamage;
        me.Abilities["Crush"] = new AbilityStat { Damage = myDamage, Hits = 5, Crits = 1, Max = (int)myDamage };
        if (healing > 0)
        {
            me.Healing = healing;
            me.Heals["Light Healing"] = healing;
        }
        f.Fighters["Venun"] = me;
        if (taken > 0) f.TakenBy["bash"] = taken;
        return f;
    }

    [Fact]
    public void ResolveScopeCoercesByMobOrFightToFighterOnSingleFightAndPinsMobToFightSlice()
    {
        var f1 = MakeFight(0, 9, "a zombie");
        var f2 = MakeFight(30, 39, "a skeleton", stint: T0.AddMinutes(10));
        var frame = new MeterFrame("Venun", "The Befallen", 4, InInstance: true, "WAR/CLR/ENC", T0, T0.AddMinutes(10), [f1, f2], [], null);

        var single = MeterViews.ResolveScope(frame, new MeterQuery(Scope: "fight", By: "mob"));
        Assert.Equal("fighter", single.By);
        Assert.True(single.OneFight);
        Assert.False(single.CanBack);
        Assert.Equal("a skeleton", single.Title);

        var placeMob = MeterViews.ResolveScope(frame, new MeterQuery(Scope: "place", By: "fighter", Mob: "a skeleton"));
        Assert.Equal("fight", placeMob.By);
        Assert.True(placeMob.CanBack);
        Assert.Single(placeMob.Fights);
    }

    [Fact]
    public void FormatDamageFooterKeepsLoadoutNotKnownFirstAndIncludesKillsInFights()
    {
        var f1 = MakeFight(0, 9, "a zombie", myDamage: 1000, kills: 2);
        var f2 = MakeFight(30, 39, "a ghoul", myDamage: 1000, kills: 1);

        string notKnown = MeterViews.FormatDamageFooter([f1, f2], oneFight: false, notKnown: true, usual: null, "kill", 20, 2000);
        Assert.StartsWith("Loadout not known · 3 kills in 2 fights · Kill time 0:20 · 100 DPS", notKnown);
        Assert.DoesNotContain("no usual here yet", notKnown);

        string noUsual = MeterViews.FormatDamageFooter([f1], oneFight: true, notKnown: false, usual: null, "active", 10, 1000);
        Assert.Equal("2 mobs · Active 0:10 · 100 DPS · no usual here yet", noUsual);
    }

    [Fact]
    public void HealingAndTakenModesFormatFighterDrilldownAndSingleVsMultiFightSlices()
    {
        var f1 = MakeFight(0, 9, "a zombie", healing: 400, taken: 150);
        var f2 = MakeFight(30, 39, "a ghoul", healing: 200, taken: 50);
        var frame = new MeterFrame("Venun", "Befallen", 4, true, null, T0, T0, [f1, f2], [], null);

        var heal = MeterViews.Build(frame, new MeterQuery(Scope: "session", Mode: "healing", Fighter: "Venun"));
        Assert.Equal("HPS", heal.Unit);
        Assert.True(heal.CanBack);
        Assert.Equal("600 healed", heal.Footer);
        Assert.Equal("Light Healing", Assert.Single(heal.Rows).Label);

        var singleTaken = MeterViews.Build(frame, new MeterQuery(Scope: "fight", Mode: "taken"));
        Assert.Equal("DTPS", singleTaken.Unit);
        Assert.Equal("ability", Assert.Single(singleTaken.Rows).Layout);
        Assert.Equal("bash", Assert.Single(singleTaken.Rows).Label);

        var sessionTaken = MeterViews.Build(frame, new MeterQuery(Scope: "session", Mode: "taken"));
        Assert.Equal(2, sessionTaken.Rows.Count);
        Assert.All(sessionTaken.Rows, r => Assert.Equal("bar", r.Layout));
    }
}
