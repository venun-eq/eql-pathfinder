using System.Globalization;
using System.Text.RegularExpressions;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Parsing;

/// <summary>
/// Turns log lines into <see cref="LogEvent"/>s. Cheap prefix checks run first, so most lines
/// (other players' combat, chat, system spam) are rejected without touching a regex.
/// Line formats are versioned by observation date in docs/log-formats.md.
/// </summary>
public sealed partial class LineParser
{
    private const StringComparison O = StringComparison.Ordinal;
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    private readonly string _me;
    private readonly ISet<string> _pets;

    /// <param name="characterName">Your character, from the log file name (eqlog_Name_server.txt).</param>
    /// <param name="pets">Pet names learned so far; shared with the tracker state so checkpoints keep them.</param>
    /// <param name="charm">What is known about your charmed pet; saved with the checkpoint so a restart picks the charm back up.</param>
    public LineParser(string characterName, ISet<string> pets, CharmState? charm = null)
    {
        _me = characterName;
        _pets = pets;
        _c = charm ?? new CharmState();
        // Older versions kept charmed mobs' names here for good, which made every later mob of that name your pet.
        foreach (var stale in pets.Where(p => IsMobName(p, characterName)).ToList()) pets.Remove(stale);
    }

    // ------------------------------------------------------------------ charmed pets
    //
    // A summoned pet has a name of its own ("Labtik", "Venun`s warder"), so remembering it is safe. A charmed pet keeps its
    // mob name ("A fetid fiend"), and the zone is full of others with that name, so it is your pet only from the moment it
    // says "Attacking … Master." until the charm ends: the charm spell wears off of it, it turns on you, it dies, you die
    // or zone, another pet speaks, the game says you have no pet, or it goes two minutes without acting. After that the
    // name is a mob again until it says "Attacking … Master." once more (or the charm lands on it again within two minutes).

    /// <summary>How long a charmed pet can go without a line before it stops being counted as yours.</summary>
    public const double CharmQuietSeconds = 120;

    /// <summary>A target counts as your pet's for this long after it announced it or you last fought it.</summary>
    public const double TargetSeconds = 180;

    private readonly CharmState _c;

    /// <summary>Charmed-pet damage counted, and damage by that name on targets that weren't your pet's (see <see cref="IsPetTarget"/>).</summary>
    public long CharmDamageKept { get; private set; }
    public long CharmDamageDropped { get; private set; }

    // Charm spells by name, without rank. The spell you cast just before a mob first calls you Master is learned as you play.
    private static readonly HashSet<string> CharmSpells = new(StringComparer.OrdinalIgnoreCase)
    {
        "Charm", "Beguile", "Cajoling Whispers", "Allure", "Boltran's Agacerie", "Dictate", "Solon's Bewitching Bravura",
        "Solon's Song of the Sirens", "Befriend Animal", "Charm Animals", "Beguile Animals", "Allure of the Wild", "Call of Karana",
        "Tunare's Request", "Beguile Plants", "Dominate Undead", "Beguile Undead", "Cajole Undead", "Thrall of Bones", "Enslave Death",
    };

    /// <summary>The mob that is your charmed pet right now, as it names itself ("A fetid fiend"), or null.</summary>
    public string? CharmedPet => _c.Pet;

    private bool IsCharmSpell(string spell) => CharmSpells.Contains(spell) || _c.Learned.Contains(spell);

    /// <summary>
    /// A pet that just spoke is a charmed mob, not a summoned pet: it has a mob's name, or the charm visibly landed on that
    /// name just before, or one of that name was hostile to you in this zone (a summoned pet never is).
    /// </summary>
    private bool IsCharmed(string pet, DateTime t) =>
        IsMobName(pet, _me) || _c.Hostile.Contains(pet) || (_c.Landed != null && SameName(_c.Landed, pet) && (t - _c.LandedAt).TotalSeconds <= 30);

    /// <summary>You or your pet are on this mob: it announced it, you hit it, or it hit you.</summary>
    private void Engaged(string mob, DateTime t)
    {
        if (mob.Length == 0) return;
        if (_c.Targets.Count > 300)
            foreach (var old in _c.Targets.Where(kv => (t - kv.Value).TotalSeconds > TargetSeconds).Select(kv => kv.Key).ToList()) _c.Targets.Remove(old);
        _c.Targets[mob] = t;
    }

    /// <summary>
    /// Other mobs share your charmed pet's name, and so may another player's charmed pet. Its damage is yours only on a
    /// target it announced ("Attacking … Master.") or that you are fighting yourself.
    /// </summary>
    private bool IsPetTarget(string mob, DateTime t) => _c.Targets.TryGetValue(mob, out var at) && (t - at).TotalSeconds <= TargetSeconds;

    /// <summary>A pet name that is a mob's name (a charmed mob), not a summoned pet's own name.</summary>
    public static bool IsMobName(string pet, string me)
    {
        if (pet.StartsWith(me + "`s ", O)) return false;      // "Venun`s warder"
        if (pet.Length == 0 || !char.IsUpper(pet[0])) return true;
        for (int i = 1; i < pet.Length; i++)
            if (!char.IsLetter(pet[i])) return true;
        return false;
    }

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private bool StartsWithCharm(string b) =>
        _c.Pet != null && b.Length > _c.Pet.Length + 1 && b[_c.Pet.Length] is ' ' or '\'' && string.Compare(b, 0, _c.Pet, 0, _c.Pet.Length, StringComparison.OrdinalIgnoreCase) == 0;

    private void StartCharm(string name, DateTime t)
    {
        if (_c.Pet == null || !SameName(_c.Pet, name))
        {
            // The spell that did it: what you cast or sang just before.
            if (_c.Cast != null && (t - _c.CastAt).TotalSeconds <= 20 && !SameName(name, _c.Last ?? "")) _c.Learned.Add(_c.Cast);
            _c.Pet = name;
        }
        _c.Seen = t;
    }

    private void EndCharm(DateTime t)
    {
        if (_c.Pet == null) return;
        _c.Last = _c.Pet;
        _c.LastEnded = t;
        _c.Pet = null;
    }

    /// <summary>Lines that look like ours but match no known format, by rough shape. Detects patch drift.</summary>
    public Dictionary<string, int> UnknownShapes { get; } = new();

    public LogEvent? Parse(in LogLine line)
    {
        var e = ParseLine(line);
        // What you fight yourself is a fair target for your charmed pet.
        if (e is DamageDealt { Source: not DamageSource.Pet } d) Engaged(d.Target, d.Time);
        else if (e is DamageTaken { From.Length: > 0 } taken) Engaged(taken.From, taken.Time);
        return e;
    }

    private LogEvent? ParseLine(in LogLine line)
    {
        var b = line.Body;
        var t = line.Time;
        if (b.Length < 6) return null;
        if (_c.Pet != null && (t - _c.Seen).TotalSeconds > CharmQuietSeconds) EndCharm(t);

        if (b.StartsWith("You ", O)) return ParseYou(t, b);
        if (b.StartsWith("--You have looted ", O)) return ParseLoot(t, b);
        if (b[0] == '[') return ParseWho(t, b);
        if (b.StartsWith("YOU are ", O)) return ParseThorns(t, b);
        if (b.StartsWith("Player ", O)) return ParseInstanceCreated(t, b);

        // Chat: "Name tells General:1, '...'", "Name says, '...'". Never parse chat as game events,
        // except your pet's "told you, 'Attacking X Master.'".
        if (b.Contains(", '", O))
        {
            if (b.EndsWith(" Master.'", O))
            {
                int k = b.IndexOf(" told you, 'Attacking ", O);
                if (k > 0)
                {
                    var pet = b.Substring(0, k);
                    Engaged(NormalizeMob(b[(k + 22)..^9]), t);
                    // One pet at a time: a mob calling you Master is your charm; a summoned pet speaking means the charm is over.
                    bool charmed = IsCharmed(pet, t);
                    if (charmed) { StartCharm(pet, t); _pets.Remove(pet); } else EndCharm(t);
                    return new PetIdentified(t, pet, charmed);
                }
            }
            return null;
        }

        if (b.StartsWith("Your ", O))
        {
            // "Your Cajole Undead spell has worn off of a revultant rat.": the charm is over.
            if (_c.Pet != null && b.EndsWith('.') && b.IndexOf(" spell has worn off of ", O) is > 5 and var w &&
                SameName(b[(w + 23)..^1], _c.Pet) && IsCharmSpell(StripRank(b[5..w])))
                EndCharm(t);
            return null;
        }
        if (b.Contains(" has been slain by ", O)) return ParseSlainBy(t, b);
        if (b.Contains(" damage from your ", O)) return ParseDot(t, b);
        if (b.Contains(" by YOUR ", O)) return ParseDamageShield(t, b);
        if (b.Contains(" YOU for ", O)) return ParseHitsYou(t, b);
        if (_c.Pet != null && StartsWithCharm(b))
        {
            // A charmed pet never swings at its master: one of that name coming for you is the charm broken (or another mob; either
            // way the name can't be trusted until it calls you Master again).
            if (b.Contains(" YOU", O)) { EndCharm(t); return null; }
            _c.Seen = t;
            if (ParsePetDamage(t, b, _c.Pet) is not DamageDealt hit) return null;
            // Only on its own targets: "A fetid fiend hits Drakoh", or one of that name fighting something you aren't, is a wild one.
            if (!IsPetTarget(hit.Target, t)) { CharmDamageDropped += hit.Amount; return null; }
            CharmDamageKept += hit.Amount;
            return hit;
        }
        if (b.EndsWith(" has been charmed.", O) || b.EndsWith("'s eyes glaze over.", O))
        {
            var landedOn = b[..b.LastIndexOf(b.EndsWith("charmed.", O) ? " has been charmed." : "'s eyes glaze over.", StringComparison.Ordinal)];
            (_c.Landed, _c.LandedAt) = (landedOn, t);
            // The charm landed again on the one you just had (a bard's song re-applies): yours without waiting for it to speak.
            if (_c.Pet == null && _c.Last != null && (t - _c.LastEnded).TotalSeconds <= CharmQuietSeconds && SameName(landedOn, _c.Last))
                StartCharm(_c.Last, t);
            return null;
        }
        if (b.Contains(" has asked you to join the instance: ", O)) return ParseInvite(t, b);

        if (_pets.Count > 0)
        {
            foreach (var pet in _pets)
            {
                if (b.Length > pet.Length + 1 && b.StartsWith(pet, O) && b[pet.Length] == ' ')
                    return ParsePetDamage(t, b, pet);
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ "You ..." lines

    private LogEvent? ParseYou(DateTime t, string b)
    {
        // Ordered roughly by frequency.
        if (b.StartsWith("You gain ", O))
        {
            var m = XpRx().Match(b);
            if (!m.Success) return null;
            // No (x%): the regular bar didn't move (a loadout at the cap), so the XP isn't sized (kills-at-cap).
            if (!m.Groups["p"].Success) return new ExperienceGained(t, 0, m.Groups["party"].Success, m.Groups["bonus"].Success, true);
            return double.TryParse(m.Groups["p"].ValueSpan, NumberStyles.Float, CI, out var p)
                ? new ExperienceGained(t, p, m.Groups["party"].Success, m.Groups["bonus"].Success) : null;
        }
        if (b.StartsWith("You hit ", O))
        {
            var m = SpellHitRx().Match(b);
            if (m.Success)
            {
                // "You hit yourself for 55 points of …" is a self-damaging spell, not damage dealt.
                if (m.Groups["t"].Value == "yourself") return null;
                return new DamageDealt(t, DamageSource.Spell, NormalizeMob(m.Groups["t"].Value), Int(m, "n"),
                    StripRank(m.Groups["s"].Value), IsCrit(m));
            }
        }
        if (b.StartsWith("You looted ", O)) return ParseLoot(t, b);
        // A spell you chose to cast (procs and item effects never write this line): the surest sign of the classes playing.
        if (b.StartsWith("You begin casting ", O) && b.EndsWith('.'))
        {
            (_c.Cast, _c.CastAt) = (StripRank(b[18..^1]), t);
            return new SpellCast(t, _c.Cast);
        }
        if (b.StartsWith("You begin singing ", O) && b.EndsWith('.')) { (_c.Cast, _c.CastAt) = (StripRank(b[18..^1]), t); return null; }
        if (b.StartsWith("You do not have a pet", O)) { EndCharm(t); return null; }
        if (b.StartsWith("You have ", O)) return ParseYouHave(t, b);
        if (b.StartsWith("You gained reward experience", O)) return new RewardExperience(t);
        if (b.StartsWith("You offered ", O)) return ParseOffered(t, b);
        if (b.StartsWith("You earned a refund of your instance charge", O)) return new InstanceRefunded(t);
        if (b.StartsWith("You died", O)) { EndCharm(t); return new YouDied(t, null); }
        if (b.StartsWith("You tell", O) || b.StartsWith("You say", O) || b.StartsWith("You shout", O)) return null;

        // Melee: "You crush X for 113 points of damage." (any verb; "You hit X for N points of damage." too)
        if (b.EndsWith(" of damage.", O) || b.Contains(" of damage. (", O))
        {
            var m = MeleeRx().Match(b);
            if (m.Success)
                return new DamageDealt(t, DamageSource.Melee, NormalizeMob(m.Groups["t"].Value), Int(m, "n"), MeleeSkill(m.Groups["verb"].Value), IsCrit(m));
        }
        return null;
    }

    private LogEvent? ParseYouHave(DateTime t, string b)
    {
        if (b.StartsWith("You have entered ", O))
        {
            var z = b.Substring(17).TrimEnd();
            if (z.EndsWith('.')) z = z[..^1];
            if (z.Length == 0 || !char.IsUpper(z[0])) return null;   // "an area where levitation effects..."
            EndCharm(t);
            _c.Hostile.Clear();
            _c.Targets.Clear();
            return ParseZone(t, z);
        }
        if (b.StartsWith("You have slain ", O))
        {
            var mob = b.Substring(15).TrimEnd('!', '.', ' ');
            return new MobSlain(t, NormalizeMob(mob), _me, true);
        }
        if (b.StartsWith("You have completed the Dungeon Crawl", O)) return new CrawlCompleted(t);
        if (b.StartsWith("You have gained a level", O))
        {
            var m = LevelRx().Match(b);
            return m.Success ? new LevelGained(t, Int(m, "l")) : null;
        }
        if (b.StartsWith("You have gained the ability \"", O))
        {
            var m = ReincRx().Match(b);
            if (!m.Success) return null;
            var kind = m.Groups["k"].Value == "Race" ? ReincarnationKind.Race : ReincarnationKind.Class;
            return new Reincarnated(t, kind, m.Groups["n"].Value);
        }
        // Second and subsequent reincarnations state their rank: "You have improved Class Reincarnation: Shadow Knight 2 at a cost of 0 ability points."
        if (b.StartsWith("You have improved ", O))
        {
            var m = ReincImprovedRx().Match(b);
            if (!m.Success) return null;
            var kind = m.Groups["k"].Value == "Race" ? ReincarnationKind.Race : ReincarnationKind.Class;
            return new Reincarnated(t, kind, m.Groups["n"].Value, Int(m, "r"));
        }
        if (b.StartsWith("You have been slain by ", O))
        {
            EndCharm(t);
            return new YouDied(t, b.Substring(23).TrimEnd('!', '.', ' '));
        }
        if (b.StartsWith("You have taken ", O))
        {
            var m = TakenSpellRx().Match(b);
            if (m.Success)
                return new DamageTaken(t, m.Groups["f"].Success ? NormalizeMob(m.Groups["f"].Value) : "", Int(m, "n"),
                    StripRank(m.Groups["s"].Value));
            return null;
        }
        if (b.StartsWith("You have been given: Void-Touched Potential", O)) return new VoidTouchedReceived(t);
        return null;
    }

    private static LogEvent ParseZone(DateTime t, string z)
    {
        var m = InstanceRx().Match(z);
        if (m.Success)
            return new ZoneEntered(t, z, m.Groups["base"].Value, Int(m, "d"), m.Groups["label"].Value, true);
        bool raid = z.EndsWith(" - Solo", O) || z.EndsWith(" - Group", O);
        return new ZoneEntered(t, z, z, null, null, raid);
    }

    private LogEvent? ParseLoot(DateTime t, string b)
    {
        if (!b.Contains(" of ", O) || !b.Contains("Potential", O)) return null;
        var m = LootRx().Match(b);
        if (!m.Success)
        {
            if (b.Contains("Mote", O) || b.Contains("Fragment", O)) Unknown("loot: " + Shape(b));
            return null;
        }
        if (!MoteTiers.TryParse(m.Groups["tier"].Success ? m.Groups["tier"].Value : null, out var tier))
        {
            Unknown("loot tier: " + m.Groups["tier"].Value);
            return null;
        }
        int n = m.Groups["n"].Success ? Int(m, "n") : 1;
        var kind = m.Groups["kind"].Value == "Fragment" ? LootKind.Fragment : LootKind.Mote;
        var src = m.Groups["src"].Value;
        bool chest = src.StartsWith("Reward Chest", O);
        if (src.EndsWith("'s corpse", O)) src = NormalizeMob(src[..^9]);
        return new MoteLooted(t, kind, tier, n, src, chest);
    }

    private LogEvent? ParseOffered(DateTime t, string b)
    {
        var m = OfferedRx().Match(b);
        if (!m.Success) return null;
        if (!MoteTiers.TryParse(m.Groups["tier"].Success ? m.Groups["tier"].Value : null, out var tier)) return null;
        int n = m.Groups["n"].Success ? Int(m, "n") : 1;
        var kind = m.Groups["kind"].Value == "Fragment" ? LootKind.Fragment : LootKind.Mote;
        return new MoteOffered(t, kind, tier, n);
    }

    // ------------------------------------------------------------------ other line shapes

    private LogEvent? ParseWho(DateTime t, string b)
    {
        var m = WhoRx().Match(b);
        if (!m.Success || m.Groups["name"].Value != _me) return null;
        var classes = m.Groups["cls"].Value.Split('/');
        return new WhoSelf(t, Int(m, "lvl"), classes, m.Groups["race"].Value);
    }

    private LogEvent? ParseInstanceCreated(DateTime t, string b)
    {
        var prefix = "Player " + _me + " creating instance ";
        if (!b.StartsWith(prefix, O)) return null;
        var z = b.Substring(prefix.Length).TrimEnd('.', ' ');
        return new InstanceCreated(t, z);
    }

    private static LogEvent? ParseInvite(DateTime t, string b)
    {
        var m = InviteRx().Match(b);
        return m.Success ? new InstanceInvite(t, m.Groups["who"].Value, m.Groups["z"].Value) : null;
    }

    private LogEvent? ParseSlainBy(DateTime t, string b)
    {
        var m = SlainByRx().Match(b);
        if (!m.Success) return null;
        var killer = m.Groups["k"].Value;
        bool byYou = killer == _me || _pets.Contains(killer) || killer.StartsWith(_me + "`s ", O) || (_c.Pet != null && SameName(killer, _c.Pet));
        // One of its name died: it may have been your pet, so the name is a mob again until it calls you Master.
        if (_c.Pet != null && SameName(m.Groups["m"].Value, _c.Pet)) EndCharm(t);
        return new MobSlain(t, NormalizeMob(m.Groups["m"].Value), killer, byYou);
    }

    private static LogEvent? ParseDot(DateTime t, string b)
    {
        var m = DotRx().Match(b);
        return m.Success
            ? new DamageDealt(t, DamageSource.DoT, NormalizeMob(m.Groups["t"].Value), Int(m, "n"), StripRank(m.Groups["s"].Value), IsCrit(m))
            : null;
    }

    private static LogEvent? ParseDamageShield(DateTime t, string b)
    {
        var m = DamageShieldRx().Match(b);
        return m.Success
            ? new DamageDealt(t, DamageSource.DamageShield, NormalizeMob(m.Groups["t"].Value), Int(m, "n"), "damage shield", false)
            : null;
    }

    private LogEvent? ParseHitsYou(DateTime t, string b)
    {
        if (StartsWithCharm(b)) EndCharm(t);
        var m = HitsYouRx().Match(b);
        if (m.Success)
        {
            var from = m.Groups["f"].Value;
            // A pet never hits its master: a remembered pet name doing it is a mob (a one-word name that was charmed once).
            if (_pets.Remove(from)) _c.Hostile.Add(from);
            else if (!IsMobName(from, _me)) _c.Hostile.Add(from);
        }
        return m.Success ? new DamageTaken(t, NormalizeMob(m.Groups["f"].Value), Int(m, "n"), null) : null;
    }

    private static LogEvent? ParseThorns(DateTime t, string b)
    {
        var m = ThornsRx().Match(b);
        return m.Success ? new DamageTaken(t, NormalizeMob(m.Groups["f"].Value), Int(m, "n"), "damage shield") : null;
    }

    private static LogEvent? ParsePetDamage(DateTime t, string b, string pet)
    {
        var rest = b.Substring(pet.Length + 1);
        var m = PetSpellRx().Match(rest);
        if (m.Success)
            return new DamageDealt(t, DamageSource.Pet, NormalizeMob(m.Groups["t"].Value), Int(m, "n"), StripRank(m.Groups["s"].Value), IsCrit(m));
        m = PetMeleeRx().Match(rest);
        if (m.Success)
            return new DamageDealt(t, DamageSource.Pet, NormalizeMob(m.Groups["t"].Value), Int(m, "n"), null, IsCrit(m));
        return null;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Maps a melee verb to its named class skill ("Backstab", "Kick", …) or null for auto-attack
    /// (slash, pierce, crush, punch, claw, hit). Archery ("shoot") gets its own row. Accepts first or third person.
    /// </summary>
    public static string? MeleeSkill(string verb) => verb switch
    {
        "backstab" or "backstabs" => "Backstab",
        "kick" or "kicks" => "Kick",
        "bash" or "bashes" => "Bash",
        "frenzy" or "frenzies" => "Frenzy",
        "strike" or "strikes" => "Strike",
        "cleave" or "cleaves" => "Cleave",
        "smite" or "smites" => "Smite",
        "reave" or "reaves" => "Reave",
        "shoot" or "shoots" => "Archery",
        _ => null,
    };

    /// <summary>Every name <see cref="MeleeSkill"/> gives: still melee wherever melee is counted as one thing.</summary>
    public static readonly IReadOnlySet<string> MeleeSkills = new HashSet<string>(StringComparer.Ordinal)
        { "Backstab", "Kick", "Bash", "Frenzy", "Strike", "Cleave", "Smite", "Reave", "Archery" };

    /// <summary>"A barbed bone skeleton" at the start of a line is "a barbed bone skeleton" mid-line.</summary>
    public static string NormalizeMob(string name)
    {
        if (name.StartsWith("A ", O) || name.StartsWith("An ", O))
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        return name;
    }

    /// <summary>"Bolt of Flame VI" -> "Bolt of Flame". Rank is tracked separately later.</summary>
    public static string StripRank(string spell)
    {
        var m = RankRx().Match(spell);
        return m.Success ? spell[..m.Index] : spell;
    }

    private static int Int(Match m, string group) => int.Parse(m.Groups[group].ValueSpan, NumberStyles.Integer, CI);

    private static bool IsCrit(Match m)
    {
        var g = m.Groups["tag"];
        if (!g.Success) return false;
        foreach (Capture c in g.Captures)
            if (c.Value.Contains("Critical", O)) return true;
        return false;
    }

    private void Unknown(string shape)
    {
        if (UnknownShapes.Count > 500) return;
        UnknownShapes[shape] = UnknownShapes.GetValueOrDefault(shape) + 1;
    }

    private static string Shape(string b) => DigitsRx().Replace(b.Length > 80 ? b[..80] : b, "N");

    // ------------------------------------------------------------------ patterns (source-generated)

    // Matches both normal and bonus-XP lines, e.g. "You gain party experience (with a bonus)! (0.337%)".
    [GeneratedRegex(@"^You gain (?<party>party )?experience(?<bonus> \(with a bonus\))?!(?: \((?<p>[\d.]+)%\))?", RegexOptions.CultureInvariant)]
    private static partial Regex XpRx();

    [GeneratedRegex(@"Welcome to level (?<l>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex LevelRx();

    [GeneratedRegex(@"^You have gained the ability ""(?<k>Race|Class) Reincarnation: (?<n>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ReincRx();

    [GeneratedRegex(@"^You have improved (?<k>Race|Class) Reincarnation: (?<n>.+?) (?<r>\d+) at a cost", RegexOptions.CultureInvariant)]
    private static partial Regex ReincImprovedRx();

    [GeneratedRegex(@"^(?<base>.+?) (?<d>\d+) \((?<label>[A-Za-z]+)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex InstanceRx();

    // Both formats: "You looted 3 Mote of Major Potential from Reward Chest and stored it in your currency"
    //               "--You have looted a Fragment of Infinite Potential from Magi P`tasa's corpse.--"
    [GeneratedRegex(@"^(?:--)?You (?:have )?looted (?:(?<n>\d+) |an? )?(?<kind>Mote|Fragment)s? of (?:(?<tier>[A-Za-z]+) )?Potential from (?<src>.+?)(?: and stored.*|\.--|\.)?$", RegexOptions.CultureInvariant)]
    private static partial Regex LootRx();

    [GeneratedRegex(@"^You offered (?:(?<n>\d+) |an? )?(?<kind>Mote|Fragment)s? of (?:(?<tier>[A-Za-z]+) )?Potential to ", RegexOptions.CultureInvariant)]
    private static partial Regex OfferedRx();

    [GeneratedRegex(@"^You hit (?<t>.+?) for (?<n>\d+) points? of (?<type>[a-z]+) damage by (?<s>.+?)\.(?: \((?<tag>[^)]+)\))*$", RegexOptions.CultureInvariant)]
    private static partial Regex SpellHitRx();

    // Berserker Frenzy reads "You frenzy on a ghoul for 92 points of damage.": strip the "on" so it does not become part of the mob name.
    [GeneratedRegex(@"^You (?<verb>[a-z]+)(?:(?<=frenzy) on)? (?<t>.+?) for (?<n>\d+) points? of damage\.(?: \((?<tag>[^)]+)\))*$", RegexOptions.CultureInvariant)]
    private static partial Regex MeleeRx();

    [GeneratedRegex(@"^(?<t>.+?) has taken (?<n>\d+) damage from your (?<s>.+?)\.(?: \((?<tag>[^)]+)\))*$", RegexOptions.CultureInvariant)]
    private static partial Regex DotRx();

    [GeneratedRegex(@"^(?<t>.+?) is [a-z]+ by YOUR [a-z]+ for (?<n>\d+) points? of non-melee damage", RegexOptions.CultureInvariant)]
    private static partial Regex DamageShieldRx();

    [GeneratedRegex(@"^(?<f>.+?) [a-z]+ YOU for (?<n>\d+) points? of damage", RegexOptions.CultureInvariant)]
    private static partial Regex HitsYouRx();

    [GeneratedRegex(@"^YOU are [a-z]+ by (?<f>.+?)'s [a-z]+ for (?<n>\d+) points? of non-melee damage", RegexOptions.CultureInvariant)]
    private static partial Regex ThornsRx();

    [GeneratedRegex(@"^You have taken (?<n>\d+) damage from (?<s>.+?)(?: by (?<f>.+?))?\.$", RegexOptions.CultureInvariant)]
    private static partial Regex TakenSpellRx();

    [GeneratedRegex(@"^(?<m>.+?) has been slain by (?<k>.+?)!$", RegexOptions.CultureInvariant)]
    private static partial Regex SlainByRx();

    [GeneratedRegex(@"^\[(?<lvl>\d+) (?<cls>[A-Z]{3}(?:/[A-Z]{3})*)\] (?<name>\S+) \((?<race>[^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex WhoRx();

    [GeneratedRegex(@"^(?<who>.+?) has asked you to join the instance: (?<z>.+?)\.(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex InviteRx();

    [GeneratedRegex(@"^(?<verb>[a-z]+) (?<t>.+?) for (?<n>\d+) points? of damage\.(?: \((?<tag>[^)]+)\))*$", RegexOptions.CultureInvariant)]
    private static partial Regex PetMeleeRx();

    [GeneratedRegex(@"^hits? (?<t>.+?) for (?<n>\d+) points? of (?<type>[a-z]+) damage by (?<s>.+?)\.(?: \((?<tag>[^)]+)\))*$", RegexOptions.CultureInvariant)]
    private static partial Regex PetSpellRx();

    [GeneratedRegex(@" (?:I|II|III|IV|V|VI|VII|VIII|IX|X|XI|XII|XIII|XIV|XV)$", RegexOptions.CultureInvariant)]
    private static partial Regex RankRx();

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex DigitsRx();
}

/// <summary>
/// What the parser knows about your charmed pet, kept with the checkpoint so closing and reopening the app mid-charm
/// doesn't lose it. Times are log times.
/// </summary>
public sealed class CharmState
{
    /// <summary>The mob that is your charmed pet now, as it names itself ("A fetid fiend").</summary>
    public string? Pet { get; set; }
    public DateTime Seen { get; set; }
    /// <summary>The last charmed pet and when its charm ended (it can come back within two minutes).</summary>
    public string? Last { get; set; }
    public DateTime LastEnded { get; set; }
    /// <summary>The last spell you cast or sang, to learn which one charms.</summary>
    public string? Cast { get; set; }
    public DateTime CastAt { get; set; }
    public HashSet<string> Learned { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>The last name a charm visibly landed on ("… has been charmed.").</summary>
    public string? Landed { get; set; }
    public DateTime LandedAt { get; set; }
    /// <summary>One-word names that hit you in this zone: mobs, whatever they look like.</summary>
    public HashSet<string> Hostile { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Mobs your pet announced or you fought, and when last.</summary>
    public Dictionary<string, DateTime> Targets { get; set; } = new(StringComparer.Ordinal);
}
