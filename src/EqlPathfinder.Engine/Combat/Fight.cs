using System.Text.Json.Serialization;

namespace EqlPathfinder.Engine.Combat;

public sealed class AbilityStat
{
    [JsonPropertyName("d")] public long Damage { get; set; }
    [JsonPropertyName("h")] public int Hits { get; set; }
    [JsonPropertyName("c")] public int Crits { get; set; }
    [JsonPropertyName("m")] public int Max { get; set; }

    public void Add(int amount, bool crit)
    {
        Damage += amount;
        Hits++;
        if (crit) Crits++;
        if (amount > Max) Max = amount;
    }

    public void Add(AbilityStat o)
    {
        Damage += o.Damage;
        Hits += o.Hits;
        Crits += o.Crits;
        if (o.Max > Max) Max = o.Max;
    }
}

/// <summary>One fighter in a fight: you, your pet, a group member, or one of their pets.</summary>
public sealed class FighterStat
{
    /// <summary>"me" = you, "pet" = your pet, "other" = anyone else.</summary>
    [JsonPropertyName("k")] public string Kind { get; set; } = "other";
    [JsonPropertyName("t")] public AbilityStat Total { get; set; } = new();
    /// <summary>By spell or ability; plain weapon and fist hits are "Melee".</summary>
    [JsonPropertyName("a")] public Dictionary<string, AbilityStat> Abilities { get; set; } = new(StringComparer.Ordinal);
    [JsonPropertyName("hl")] public long Healing { get; set; }
    [JsonPropertyName("hs")] public Dictionary<string, long> Heals { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore] public bool Mine => Kind is "me" or "pet";
}

/// <summary>
/// One fight with one mob (same-named mobs fought together count as one, the log can't tell them apart): from the first
/// swing or hit either way until it dies, you zone, or <see cref="FightTracker.LingerSeconds"/> pass with nothing (8 s by default). Times are log times.
/// </summary>
public sealed class Fight
{
    [JsonIgnore] public DateTime Start { get; set; }
    [JsonPropertyName("e")] public DateTime End { get; set; }
    [JsonIgnore] public string Mob { get; set; } = "";
    [JsonIgnore] public string? Zone { get; set; }
    [JsonIgnore] public int? Difficulty { get; set; }
    [JsonIgnore] public string? Trio { get; set; }
    /// <summary>When this play session began (a login after 30+ minutes away).</summary>
    [JsonIgnore] public DateTime Session { get; set; }
    /// <summary>When this stay in the zone began (the crawl, or the open-world zone).</summary>
    [JsonIgnore] public DateTime Stint { get; set; }
    [JsonPropertyName("s")] public bool Slain { get; set; }
    [JsonPropertyName("f")] public Dictionary<string, FighterStat> Fighters { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Damage you took, by what hit you ("Melee" or the spell).</summary>
    [JsonPropertyName("tk")] public long Taken { get; set; }
    [JsonPropertyName("tb")] public Dictionary<string, long> TakenBy { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Mobs of this name that died in the fight (several when same-named mobs were fought together).</summary>
    [JsonPropertyName("k")] public int Kills { get; set; }
    /// <summary>
    /// The stretches of actual fighting, as [first second, last second] from <see cref="Start"/>: a swing, a hit, a resist
    /// or damage taken within <see cref="ActiveGapSeconds"/> of the last one continues a stretch. A pause (feign death,
    /// a mez, a root rot wait) falls between stretches: it counts toward the kill time but not the active time.
    /// </summary>
    [JsonPropertyName("b")] public List<int[]> Bursts { get; set; } = [];
    /// <summary>When the mob last died, until another of its name is hit (not saved).</summary>
    [JsonIgnore] public DateTime? SlainAt { get; set; }

    public const int ActiveGapSeconds = 10;

    /// <summary>Kill time: from the first hostile action to the last, pauses included.</summary>
    [JsonIgnore] public double Seconds => Math.Max(1, (End - Start).TotalSeconds + 1);
    /// <summary>Active time: the seconds spent actually fighting, pauses left out.</summary>
    [JsonIgnore] public double ActiveSeconds => Bursts.Count == 0 ? Seconds : Bursts.Sum(b => b[1] - b[0] + 1);

    /// <summary>"active" or "kill" (anything else is active).</summary>
    public double SecondsBy(string? clock) => clock == "kill" ? Seconds : ActiveSeconds;

    /// <summary>Something happened in this fight at <paramref name="t"/>: extends the fight and its current stretch, or starts a new one.</summary>
    public void Touch(DateTime t)
    {
        if (t > End) End = t;
        int at = Math.Max(0, (int)(t - Start).TotalSeconds);
        if (Bursts.Count > 0 && at - Bursts[^1][1] <= ActiveGapSeconds) { if (at > Bursts[^1][1]) Bursts[^1][1] = at; }
        else Bursts.Add([at, at]);
    }
    [JsonIgnore] public long Damage => Fighters.Values.Sum(f => f.Total.Damage);
    [JsonIgnore] public long MyDamage => Fighters.Values.Where(f => f.Mine).Sum(f => f.Total.Damage);
    [JsonIgnore] public long Healing => Fighters.Values.Sum(f => f.Healing);

    public FighterStat Fighter(string name, string kind)
    {
        if (!Fighters.TryGetValue(name, out var f)) Fighters[name] = f = new FighterStat { Kind = kind };
        return f;
    }

    /// <summary>A copy that's safe to read while the tracker keeps writing the original.</summary>
    public Fight Clone() => new()
    {
        Start = Start,
        End = End,
        Mob = Mob,
        Zone = Zone,
        Difficulty = Difficulty,
        Trio = Trio,
        Session = Session,
        Stint = Stint,
        Slain = Slain,
        Taken = Taken,
        TakenBy = new(TakenBy, StringComparer.Ordinal),
        Kills = Kills,
        SlainAt = SlainAt,
        Bursts = Bursts.Select(b => new[] { b[0], b[1] }).ToList(),
        Fighters = Fighters.ToDictionary(kv => kv.Key, kv => new FighterStat
        {
            Kind = kv.Value.Kind,
            Healing = kv.Value.Healing,
            Heals = new(kv.Value.Heals, StringComparer.Ordinal),
            Total = Copy(kv.Value.Total),
            Abilities = kv.Value.Abilities.ToDictionary(a => a.Key, a => Copy(a.Value), StringComparer.Ordinal),
        }, StringComparer.Ordinal),
    };

    private static AbilityStat Copy(AbilityStat a) => new() { Damage = a.Damage, Hits = a.Hits, Crits = a.Crits, Max = a.Max };
}
