using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.Tests;

/// <summary>Real lines from the EverQuest Legends client (formats as of 2026-11).</summary>
public class ParserTests
{
    private static readonly HashSet<string> Pets = ["Jarann", "Venun`s warder"];
    private static readonly LineParser P = new("Venun", Pets);

    private static LogEvent? Parse(string raw)
    {
        Assert.True(LogLine.TryParse(raw, out var line), "timestamp should parse");
        return P.Parse(line);
    }

    [Fact]
    public void Timestamp()
    {
        Assert.True(LogLine.TryParse("[Sat Nov 14 10:54:15 2026] You have slain a tentacle terror!", out var l));
        Assert.Equal(new DateTime(2026, 11, 14, 10, 54, 15), l.Time);
        Assert.Equal("You have slain a tentacle terror!", l.Body);
        Assert.False(LogLine.TryParse("garbage", out _));
    }

    [Theory]
    [InlineData("[Sat Nov 14 10:36:46 2026] You have entered The Estate of Unrest 4 (Refined).", "The Estate of Unrest", 4, true)]
    [InlineData("[Tue Nov 10 16:00:29 2026] You have entered The Ruins of Old Paineel - Solo 4 (Refined).", "The Ruins of Old Paineel - Solo", 4, true)]
    [InlineData("[Sat Nov 14 10:32:56 2026] You have entered The Estate of Unrest.", "The Estate of Unrest", null, false)]
    public void Zones(string raw, string baseZone, int? d, bool instance)
    {
        var z = Assert.IsType<ZoneEntered>(Parse(raw));
        Assert.Equal(baseZone, z.BaseZone);
        Assert.Equal(d, z.Difficulty);
        Assert.Equal(instance, z.IsInstance);
    }

    [Fact]
    public void LevitationAreaIsNotAZone() =>
        Assert.Null(Parse("[Sat Nov 14 10:36:46 2026] You have entered an area where levitation effects do not function."));

    [Theory]
    [InlineData("[Sat Nov 14 10:53:59 2026] You looted a Mote of Major Potential from a tentacle terror's corpse and stored it in your currency", LootKind.Mote, MoteTier.Major, 1, false, "a tentacle terror")]
    [InlineData("[Sat Nov 14 10:54:15 2026] You looted 4 Fragment of Major Potential from Reward Chest and stored it in your currency", LootKind.Fragment, MoteTier.Major, 4, true, "Reward Chest")]
    [InlineData("[Sat Nov 14 02:41:34 2026] You looted a Mote of Potential from a lurking mummy's corpse and stored it in your currency", LootKind.Mote, MoteTier.Potential, 1, false, "a lurking mummy")]
    [InlineData("[Thu Nov 12 00:13:32 2026] --You have looted a Fragment of Infinite Potential from Magi P`tasa's corpse.--", LootKind.Fragment, MoteTier.Infinite, 1, false, "Magi P`tasa")]
    public void Loot(string raw, LootKind kind, MoteTier tier, int n, bool chest, string source)
    {
        var l = Assert.IsType<MoteLooted>(Parse(raw));
        Assert.Equal(kind, l.Kind);
        Assert.Equal(tier, l.Tier);
        Assert.Equal(n, l.Count);
        Assert.Equal(chest, l.FromChest);
        Assert.Equal(source, l.Source);
    }

    [Fact]
    public void OfferedAtConstructPotential()
    {
        var o = Assert.IsType<MoteOffered>(Parse("[Mon Oct 27 20:00:00 2026] You offered 12 Mote of Major Potential to Constructed Potential."));
        Assert.Equal(12, o.Count);
        Assert.Equal(MoteTier.Major, o.Tier);
    }

    [Fact]
    public void Experience()
    {
        var x = Assert.IsType<ExperienceGained>(Parse("[Sat Nov 14 10:54:15 2026] You gain experience! (6.036%)"));
        Assert.Equal(6.036, x.Percent, 3);
        Assert.False(x.Party);
        var p = Assert.IsType<ExperienceGained>(Parse("[Sat Nov 14 10:54:15 2026] You gain party experience! (0.810%)"));
        Assert.True(p.Party);
        Assert.False(p.Bonus);
        // A bonus-XP event writes "(with a bonus)": missed until 11-17, so every kill of one went uncounted.
        var b = Assert.IsType<ExperienceGained>(Parse("[Sat Sep 05 11:48:16 2026] You gain party experience (with a bonus)! (0.337%)"));
        Assert.Equal((0.337, true, true), (b.Percent, b.Party, b.Bonus));
        var sb = Assert.IsType<ExperienceGained>(Parse("[Sat Sep 05 11:48:16 2026] You gain experience (with a bonus)! (1.250%)"));
        Assert.Equal((1.25, false, true), (sb.Percent, sb.Party, sb.Bonus));
        // Without a share of the level (the regular bar didn't move) it is still an XP line, its XP unsized (kills-at-cap).
        var u = Assert.IsType<ExperienceGained>(Parse("[Tue Sep 08 02:45:33 2026] You gain party experience (with a bonus)!"));
        Assert.Equal((0.0, true, true, true), (u.Percent, u.Party, u.Bonus, u.Unknown));
        Assert.IsType<RewardExperience>(Parse("[Sat Nov 14 10:54:15 2026] You gained reward experience from the Dungeon Crawl!"));
        Assert.Equal(24, Assert.IsType<LevelGained>(Parse("[Sat Nov 14 10:48:45 2026] You have gained a level! Welcome to level 24!")).Level);
    }

    [Fact]
    public void Reincarnation()
    {
        var r = Assert.IsType<Reincarnated>(Parse("[Wed Nov 11 12:00:57 2026] You have gained the ability \"Class Reincarnation: Cleric\" at a cost of 0 ability points."));
        Assert.Equal(ReincarnationKind.Class, r.Kind);
        Assert.Equal("Cleric", r.Name);
        Assert.Equal("CLR", ClassNames.Abbrev(r.Name));
    }

    [Fact]
    public void WhoSelfOnlyForYou()
    {
        var w = Assert.IsType<WhoSelf>(Parse("[Sat Nov 14 10:56:54 2026] [24 NEC/MAG/BST] Venun (Erudite) <Wayfarers> ZONE: The Estate of Unrest (unrest)  "));
        Assert.Equal(24, w.Level);
        Assert.Equal(["NEC", "MAG", "BST"], w.Classes);
        Assert.Equal("Erudite", w.Race);
        Assert.Null(Parse("[Sat Nov 14 10:56:54 2026] [50 WAR] Somebody (Ogre) ZONE: Oggok"));
    }

    [Theory]
    [InlineData("[Sat Nov 14 10:54:14 2026] You crush Garanel Rucksif for 113 points of damage.", DamageSource.Melee, 113, null, false)]
    [InlineData("[Sat Nov 14 10:54:09 2026] You punch Garanel Rucksif for 98 points of damage. (Critical)", DamageSource.Melee, 98, null, true)]
    [InlineData("[Sat Nov 14 10:54:03 2026] You hit Garanel Rucksif for 1297 points of fire damage by Bolt of Flame VI. (Critical)", DamageSource.Spell, 1297, "Bolt of Flame", true)]
    [InlineData("[Sat Nov 14 02:51:52 2026] A greater dark bone has taken 22 damage from your Negation of Life.", DamageSource.DoT, 22, "Negation of Life", false)]
    [InlineData("[Sat Nov 14 10:54:09 2026] Garanel Rucksif is burned by YOUR flames for 9 points of non-melee damage.", DamageSource.DamageShield, 9, "damage shield", false)]
    [InlineData("[Sat Nov 14 10:54:10 2026] Jarann punches Garanel Rucksif for 75 points of damage.", DamageSource.Pet, 75, null, false)]
    public void Damage(string raw, DamageSource src, int amount, string? ability, bool crit)
    {
        var d = Assert.IsType<DamageDealt>(Parse(raw));
        Assert.Equal(src, d.Source);
        Assert.Equal(amount, d.Amount);
        Assert.Equal(ability, d.Ability);
        Assert.Equal(crit, d.Critical);
    }

    // "for 1 point of damage": the singular, in each shape the player's log has (visit-boundaries, from damage-lines).
    [Theory]
    [InlineData("[Sat Nov 14 10:54:14 2026] You slash Garanel Rucksif for 1 point of damage.", DamageSource.Melee, null)]
    [InlineData("[Sat Nov 14 10:54:03 2026] You hit Garanel Rucksif for 1 point of fire damage by Bolt of Flame VI.", DamageSource.Spell, "Bolt of Flame")]
    [InlineData("[Sat Nov 14 10:54:09 2026] Garanel Rucksif is tormented by YOUR frost for 1 point of non-melee damage.", DamageSource.DamageShield, "damage shield")]
    [InlineData("[Sat Nov 14 10:54:10 2026] Jarann bashes Garanel Rucksif for 1 point of damage.", DamageSource.Pet, null)]
    public void AOnePointHitIsRead(string raw, DamageSource src, string? ability)
    {
        var d = Assert.IsType<DamageDealt>(Parse(raw));
        Assert.Equal(src, d.Source);
        Assert.Equal(1, d.Amount);
        Assert.Equal(ability, d.Ability);
    }

    [Fact]
    public void AOnePointHitOnYouIsRead()
    {
        var d = Assert.IsType<DamageTaken>(Parse("[Sat Nov 14 10:54:11 2026] A hardened skeleton bashes YOU for 1 point of damage."));
        Assert.Equal(1, d.Amount);
    }

    [Fact]
    public void DotTargetIsNormalized() =>
        Assert.Equal("a greater dark bone",
            Assert.IsType<DamageDealt>(Parse("[Sat Nov 14 02:51:52 2026] A greater dark bone has taken 22 damage from your Negation of Life.")).Target);

    [Fact]
    public void SlainByPetCountsAsYours()
    {
        var s = Assert.IsType<MobSlain>(Parse("[Sat Nov 14 10:48:07 2026] A barbed bone skeleton has been slain by Jarann!"));
        Assert.True(s.ByYou);
        Assert.Equal("a barbed bone skeleton", s.Mob);
        Assert.True(Assert.IsType<MobSlain>(Parse("[Sat Nov 14 00:55:57 2026] A large plague rat has been slain by Venun`s warder!")).ByYou);
        Assert.False(Assert.IsType<MobSlain>(Parse("[Sat Nov 14 00:55:57 2026] A large plague rat has been slain by Hellsfury!")).ByYou);
    }

    [Theory]
    [InlineData("[Sat Nov 14 10:55:06 2026] Rude tells General:1, 'I'm on my first reincarnation of rng/rog/shm, will run this 3 times'")]
    [InlineData("[Sat Nov 14 10:55:06 2026] Rude tells General:1, 'a gnoll has been slain by Rude!'")]
    [InlineData("[Thu Nov 12 00:13:32 2026] You tell General:1, 'Fragment of Infinite Potential'")]
    [InlineData("[Sat Nov 14 10:59:39 2026] Rolo healed himself for 0 (293) hit points by Resolution.")]
    public void ChatAndOthersAreIgnored(string raw) => Assert.Null(Parse(raw));

    [Fact]
    public void PetIsLearnedFromItsAttackLine() =>
        Assert.Equal("Jarann", Assert.IsType<PetIdentified>(Parse("[Sat Nov 14 10:54:01 2026] Jarann told you, 'Attacking Garanel Rucksif Master.'")).Pet);

    [Fact]
    public void TierValues()
    {
        Assert.Equal(1, MoteTiers.T5Value(MoteTier.Major));
        Assert.Equal(32, MoteTiers.T5Value(MoteTier.Infinite));
        Assert.Equal(0.0625, MoteTiers.T5Value(MoteTier.Infinitesimal));
        Assert.Equal(0.5, MoteTiers.T5Value(MoteTier.Potential));
        Assert.True(MoteTiers.IsHighTier(MoteTier.Greater));
        Assert.False(MoteTiers.IsHighTier(MoteTier.Major));
    }
}
