using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Parsing;

/// <summary>A typed event parsed from one log line. Only the lines the engine needs become events.</summary>
public abstract record LogEvent(DateTime Time);

/// <summary>"You have entered X." Instances carry " N (Label)" (difficulty) and raid zones " - Solo|Group".</summary>
public sealed record ZoneEntered(DateTime Time, string Zone, string BaseZone, int? Difficulty, string? DifficultyLabel, bool IsInstance)
    : LogEvent(Time);

public sealed record CrawlCompleted(DateTime Time) : LogEvent(Time);
public sealed record InstanceRefunded(DateTime Time) : LogEvent(Time);
public sealed record InstanceCreated(DateTime Time, string Zone) : LogEvent(Time);
public sealed record InstanceInvite(DateTime Time, string Inviter, string Zone) : LogEvent(Time);

/// <summary>"You gain experience! (4.735%)" or "You gain party experience! (x%)". One per XP kill.</summary>
/// <summary>"You gain (party) experience! (x%)"; <see cref="Bonus"/> when the line says "(with a bonus)" (a server event).</summary>
/// <summary><paramref name="Unknown"/>: the line had no (x%), so the XP isn't sized (the regular bar didn't move); Percent is 0.</summary>
public sealed record ExperienceGained(DateTime Time, double Percent, bool Party, bool Bonus = false, bool Unknown = false) : LogEvent(Time);

/// <summary>"You gained reward experience from the Dungeon Crawl!" The log gives no amount.</summary>
public sealed record RewardExperience(DateTime Time) : LogEvent(Time);

public sealed record LevelGained(DateTime Time, int Level) : LogEvent(Time);

public enum ReincarnationKind { Race, Class }
/// <param name="Rank">The rank the line states: a second or later reincarnation is "You have improved Class Reincarnation: Shadow Knight 2"; a first
/// is "You have gained the ability \"Class Reincarnation: Shadow Knight\"" (no number, null here).</param>
public sealed record Reincarnated(DateTime Time, ReincarnationKind Kind, string Name, int? Rank = null) : LogEvent(Time);

/// <summary>Your own /who line: "[24 NEC/MAG/BST] Venun (Erudite) &lt;Guild&gt; ZONE: ...".</summary>
public sealed record WhoSelf(DateTime Time, int Level, IReadOnlyList<string> Classes, string Race) : LogEvent(Time);

/// <summary>
/// Stub (current-loadout, qa): the game wrote the tracked character's loadout file for <paramref name="Slot"/> (LO1 to
/// LO9) at <paramref name="Time"/>: the slot on from then. Not an eqlog line: docs/log-formats.md, "Which loadout is on".
/// </summary>
public sealed record LoadoutSwapped(DateTime Time, int Slot) : LogEvent(Time);

/// <summary>Stub (current-loadout, qa): dbg.txt showed a class changed inside the slot on, at <paramref name="Time"/>.</summary>
public sealed record LoadoutClassesChanged(DateTime Time) : LogEvent(Time);

/// <summary>"You have slain X!" or "X has been slain by Y!".</summary>
public sealed record MobSlain(DateTime Time, string Mob, string? Killer, bool ByYou) : LogEvent(Time);

/// <summary>"You begin casting X." (rank numerals stripped).</summary>
public sealed record SpellCast(DateTime Time, string Spell) : LogEvent(Time);

public sealed record YouDied(DateTime Time, string? Killer) : LogEvent(Time);

public sealed record MoteLooted(DateTime Time, LootKind Kind, MoteTier Tier, int Count, string Source, bool FromChest)
    : LogEvent(Time);

/// <summary>"You offered N Mote of T Potential to Constructed Potential." Motes spent.</summary>
public sealed record MoteOffered(DateTime Time, LootKind Kind, MoteTier Tier, int Count) : LogEvent(Time);

public sealed record VoidTouchedReceived(DateTime Time) : LogEvent(Time);

/// <summary>"&lt;pet&gt; told you, 'Attacking X Master.'" identifies your pet.</summary>
/// <param name="Charmed">A charmed mob (yours only while the charm holds), not a summoned pet.</param>
public sealed record PetIdentified(DateTime Time, string Pet, bool Charmed = false) : LogEvent(Time);

public enum DamageSource { Melee, Spell, DoT, Pet, DamageShield }

public sealed record DamageDealt(DateTime Time, DamageSource Source, string Target, int Amount, string? Ability, bool Critical)
    : LogEvent(Time);

public sealed record DamageTaken(DateTime Time, string From, int Amount, string? Ability) : LogEvent(Time);
