using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.Combat;

/// <summary>What the fight tracker carries between lines; saved so a restart continues the same session and zone.</summary>
public sealed class FightContext
{
    public DateTime? LastLine { get; set; }
    public DateTime Session { get; set; }
    public string? Zone { get; set; }
    /// <summary>The zone as the log names it ("Befallen 4 (Refined)").</summary>
    public string? FullZone { get; set; }
    public int? Difficulty { get; set; }
    public bool InInstance { get; set; }
    public DateTime Stint { get; set; }
    /// <summary>This stint's crawl is done ("You have completed the Dungeon Crawl").</summary>
    public bool Completed { get; set; }
    /// <summary>An instance was created or offered since you last entered one: going in next is a new crawl.</summary>
    public bool NewInstanceSinceEntry { get; set; }
    /// <summary>
    /// When you left the last instance and which, so coming back within 10 minutes continues the same stint (a death, an
    /// errand). Not when that crawl was done or a new instance was made since: that's the next run, as the crawl tracker has it.
    /// </summary>
    public string? LeftZone { get; set; }
    public DateTime? LeftAt { get; set; }
    public DateTime LeftStint { get; set; }
    public bool LeftCompleted { get; set; }
    /// <summary>The last instance's difficulty: a D0 one is re-entered under the plain zone name.</summary>
    public int? LeftDifficulty { get; set; }
    /// <summary>The instance just created or offered (D0 enters under the plain zone name).</summary>
    public InstanceWatch Instance { get; set; } = new();
    /// <summary>The trio your last /who showed, and when.</summary>
    public string? Trio { get; set; }
    public DateTime? WhoAt { get; set; }
    public string? Pet { get; set; }
    public HashSet<string> Pets { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Your charmed pet, if any, as the parser left it (so a restart mid-charm keeps it).</summary>
    public EqlPathfinder.Engine.Parsing.CharmState Charm { get; set; } = new();

    /// <summary>The app's trio over time (its /who and spell-checked trios), handed over by the app; null until then.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public Analysis.TrioTimeline? Timeline { get; set; }

    /// <summary>
    /// The trio playing at <paramref name="time"/>: the app's, the same one every other page uses, unless a /who seen
    /// since that stretch began says otherwise (the timeline is only as new as the last visit).
    /// </summary>
    /// <summary>
    /// True when the active loadout at <paramref name="time"/> is unmet (swapped without a /who, or classes changed);
    /// the meter withholds verdicts until a /who identifies it.
    /// </summary>
    public bool NotKnownAt(DateTime time) => Timeline?.At(time) is { Trio: null } span && !(WhoAt is { } who && who > span.From && who <= time);

    public string? TrioAt(DateTime time) =>
        Timeline?.At(time) is { } span && !(WhoAt is { } who && who > span.From && who <= time) ? span.Trio : Trio;
}

/// <summary>
/// Turns combat lines into fights. A fight starts when you or your pet swing at a mob or it swings at you, hit or miss
/// (a resisted spell counts); other fighters' hits on that mob join it (the few seconds before you engaged are picked
/// up too). It ends when the mob dies and nothing more hits that name, when you zone, or after
/// <see cref="LingerSeconds"/> with nothing. Deterministic: the same lines give the same fights.
/// </summary>
public sealed class FightTracker(string me, FightContext context, Action<Fight> closed)
{
    /// <summary>A heal counts toward a fight within this long of its last action.</summary>
    public const double GapSeconds = 10;
    /// <summary>
    /// Silence timeout before an unkilled mob's fight closes (configurable 3–30 s, default 8 s).
    /// 8 s avoids splitting fights across 6 s DoT ticks without chaining separate identically named mobs together.
    /// 3 s 13%, 2 s 27%, 1 s 63%, and at 1 s or less some kills are lost outright (the death line comes after the fight closed).
    /// </summary>
    public const double DefaultLingerSeconds = 8, MinLingerSeconds = 3, MaxLingerSeconds = 30;
    public double LingerSeconds { get; set; } = DefaultLingerSeconds;
    /// <summary>After a death, a hit on the same name within this long means more of them are in the fight (a DoT tick is 6 seconds).</summary>
    public const double SameNameSeconds = 6;
    public const double SessionGapMinutes = 30;
    public const double ResumeMinutes = 10;
    private const double BufferSeconds = 12;

    public FightContext Context { get; } = context;
    private readonly Dictionary<string, Fight> _open = new(StringComparer.Ordinal);
    private readonly List<OtherDamage> _buffer = [];
    // Players and pets seen fighting beside you: a charmed pet turning on one of them is not a fight with a mob.
    private readonly HashSet<string> _friends = new(StringComparer.Ordinal);
    // Names known to be mobs here: you hit them or they hit you (or your pet). A one-word name ("Fright", "Terror") looks
    // like a player otherwise, and a mob taken for a friend would have your damage on it thrown away.
    private readonly HashSet<string> _mobs = new(StringComparer.Ordinal);

    /// <summary>A target your pet or a group member may be fighting: an ordinary mob name, or a one-word name known to be a mob.</summary>
    private bool IsMob(string name) => _mobs.Contains(name) || (!IsPlayerName(name) && !_friends.Contains(name));

    public IReadOnlyCollection<Fight> Open => _open.Values;

    /// <summary>Call for every timestamped line, before <see cref="Apply"/>.</summary>
    public void OnLine(DateTime t)
    {
        var c = Context;
        if (c.LastLine is not { } last || (t - last).TotalMinutes >= SessionGapMinutes)
        {
            CloseAll();
            c.Session = t;
            c.Stint = t;
        }
        c.LastLine = t;
        if (_open.Count > 0) CloseIdle(t);
    }

    /// <summary>Live: closes fights that went quiet even when the log has no new lines.</summary>
    public void Tick(DateTime now)
    {
        if (_open.Count > 0) CloseIdle(now);
    }

    public void Apply(LogEvent e)
    {
        switch (e)
        {
            case DamageDealt d when d.Target.Length > 0 && d.Target != "yourself" && !_friends.Contains(d.Target):
                {
                    bool pet = d.Source == DamageSource.Pet;
                    // What you hit yourself is a mob. A pet can turn on a friend when a charm breaks, so it doesn't decide.
                    if (!pet) _mobs.Add(d.Target);
                    else if (!IsMob(d.Target)) break;
                    var f = Engage(d.Target, d.Time);
                    Hit(f.Fighter(pet ? Context.Pet ?? "Pet" : me, pet ? "pet" : "me"), d.Ability, d.Amount, d.Critical);
                    break;
                }
            case DamageTaken t when t.From.Length > 0 && t.From != "yourself" && !_friends.Contains(t.From):
                Took(t.From, t.Time, t.Amount, t.Ability);
                break;
            case SpellHitYou s when !_friends.Contains(s.From):
                Took(s.From, s.Time, s.Amount, s.Ability);
                break;
            case OtherDamage o:
                OnOther(o);
                break;
            case HealLanded h:
                OnHeal(h);
                break;
            case MobSlain s:
                // Not closed yet: same-named mobs share a fight, so it ends when nothing more hits that name.
                if (_open.TryGetValue(s.Mob, out var dead)) { dead.Touch(s.Time); dead.Slain = true; dead.Kills++; dead.SlainAt = s.Time; }
                break;
            case Swing w:
                OnSwing(w);
                break;
            case PetIdentified p:
                Context.Pet = p.Pet;
                if (!p.Charmed) Context.Pets.Add(p.Pet);   // a charmed mob is a pet only while the parser says so
                // Its hits before it said so (a new pet looks like any player until then) were yours too.
                foreach (var open in _open.Values)
                    if (open.Fighters.TryGetValue(p.Pet, out var early) && early.Kind == "other") early.Kind = "pet";
                break;
            case WhoSelf w:
                Context.Trio = string.Join("/", w.Classes);
                Context.WhoAt = w.Time;
                break;
            case InstanceCreated ic:
                Context.NewInstanceSinceEntry = true;
                Context.Instance.Created(ic.Time, ic.Zone);
                break;
            case InstanceInvite ii:
                Context.NewInstanceSinceEntry = true;
                Context.Instance.Invited(ii.Time, ii.Zone);
                break;
            case CrawlCompleted when Context.InInstance:
                Context.Completed = true;
                break;
            case ZoneEntered z:
                OnZone(z);
                break;
        }
    }

    /// <summary>End of input, or shutdown: every open fight ends where its last line was.</summary>
    public void CloseAll()
    {
        foreach (var f in _open.Values.OrderBy(f => f.Start).ToList()) Finish(f);
        _open.Clear();
        _buffer.Clear();
    }

    private void OnZone(ZoneEntered z)
    {
        CloseAll();
        _friends.Clear();
        _mobs.Clear();
        var c = Context;
        // A D0 instance enters under the plain zone name: the one just created or offered, or a way back into the last one
        // from the open world of that name (a death, an errand), never straight from another zone. Leaving it lands in the public zone of the same
        // name, which stays the open world.
        var r = c.Instance.Resolve(z);
        if (r.Difficulty == null && !c.InInstance && c.LeftDifficulty == 0 && c.LeftZone == z.Zone && c.FullZone == z.Zone && c.LeftAt is { } l0
            && (z.Time - l0).TotalMinutes <= ResumeMinutes && !c.LeftCompleted && !c.NewInstanceSinceEntry)
            r = InstanceWatch.AsD0(z, z.Zone, null);
        z = r;
        if (c.InInstance && c.FullZone != null)
        {
            c.LeftZone = c.FullZone; c.LeftAt = z.Time; c.LeftStint = c.Stint; c.LeftDifficulty = c.Difficulty; c.LeftCompleted = c.Completed;
        }
        bool resume = z.IsInstance && c.LeftZone == z.Zone && c.LeftAt is { } left && (z.Time - left).TotalMinutes <= ResumeMinutes
            && !c.LeftCompleted && !c.NewInstanceSinceEntry;
        c.Stint = resume ? c.LeftStint : z.Time;
        c.Completed = false;   // a resumed stint wasn't done, or it wouldn't resume
        if (z.IsInstance) c.NewInstanceSinceEntry = false;
        c.Zone = z.BaseZone;
        c.Difficulty = z.Difficulty;
        c.InInstance = z.IsInstance;
        c.FullZone = z.Zone;
    }

    private Fight Engage(string mob, DateTime t)
    {
        if (_open.TryGetValue(mob, out var f))
        {
            f.SlainAt = null;   // another of the same name
            f.Touch(t);
            return f;
        }
        var c = Context;
        // Group members who were already on it set the start.
        var early = IsMob(mob)
            ? _buffer.Where(o => o.Target == mob && (t - o.Time).TotalSeconds <= BufferSeconds && !_mobs.Contains(o.Actor)).OrderBy(o => o.Time).ToList()
            : [];
        var start = early.Count > 0 && early[0].Time < t ? early[0].Time : t;
        f = new Fight { Start = start, End = start, Mob = mob, Zone = c.Zone, Difficulty = c.Difficulty, Trio = c.TrioAt(t), Session = c.Session, Stint = c.Stint };
        _open[mob] = f;
        foreach (var o in early)
        {
            f.Touch(o.Time);
            _friends.Add(o.Actor);
            Hit(f.Fighter(o.Actor, "other"), o.Ability, o.Amount, o.Critical);
        }
        _buffer.RemoveAll(o => o.Target == mob);
        f.Touch(t);
        return f;
    }

    private void Took(string from, DateTime t, int amount, string? ability)
    {
        _mobs.Add(from);
        var f = Engage(from, t);
        f.Taken += amount;
        var key = ability ?? "Melee";
        f.TakenBy[key] = f.TakenBy.GetValueOrDefault(key) + amount;
    }

    private void OnOther(OtherDamage o)
    {
        if (o.Actor == o.Target) return;
        // Whatever hits your pet is a mob.
        if (Context.Pets.Contains(o.Target) && !_friends.Contains(o.Actor)) _mobs.Add(o.Actor);
        if (_open.TryGetValue(o.Target, out var f))
        {
            // Mobs hitting each other, or a fight opened on a name that isn't known to be a mob: no one joins it.
            if (!IsPlayerName(o.Actor) || _mobs.Contains(o.Actor) || !IsMob(o.Target)) return;
            f.SlainAt = null;
            f.Touch(o.Time);
            _friends.Add(o.Actor);
            Hit(f.Fighter(o.Actor, "other"), o.Ability, o.Amount, o.Critical);
            return;
        }
        // A mob we're fighting hitting someone else keeps the fight going (you may be healing).
        if (_open.TryGetValue(o.Actor, out var by)) { by.SlainAt = null; by.Touch(o.Time); return; }
        if (!IsPlayerName(o.Actor) || _mobs.Contains(o.Actor)) return;
        _buffer.Add(o);
        if (_buffer.Count > 400) _buffer.RemoveAll(x => (o.Time - x.Time).TotalSeconds > BufferSeconds);
    }

    /// <summary>A miss, an avoided swing or a resisted spell: it starts or continues a fight like a hit does, with no damage.</summary>
    private void OnSwing(Swing w)
    {
        if (w.Actor == "You") { if (w.Target != "YOU" && !_friends.Contains(w.Target)) { _mobs.Add(w.Target); Engage(w.Target, w.Time); } }
        else if (Context.Pets.Contains(w.Actor)) { if (w.Target != "YOU" && IsMob(w.Target)) Engage(w.Target, w.Time); }
        else if (w.Target == "YOU") { if (!_friends.Contains(w.Actor)) { _mobs.Add(w.Actor); Engage(w.Actor, w.Time); } }
        else if (_open.TryGetValue(w.Target, out var f)) { if (IsPlayerName(w.Actor) && !_mobs.Contains(w.Actor)) { f.SlainAt = null; f.Touch(w.Time); } }
        else if (_open.TryGetValue(w.Actor, out var by)) { by.SlainAt = null; by.Touch(w.Time); }
    }

    private void OnHeal(HealLanded h)
    {
        if (_open.Count == 0) return;
        bool mine = h.Actor == "You";
        bool pet = Context.Pet == h.Actor || Context.Pets.Contains(h.Actor);
        if (!mine && !pet && (!IsPlayerName(h.Actor) || _open.ContainsKey(h.Actor))) return;
        var f = _open.Values.MaxBy(x => x.End)!;
        if ((h.Time - f.End).TotalSeconds > GapSeconds) return;
        var who = f.Fighter(mine ? me : h.Actor, mine ? "me" : pet ? "pet" : "other");
        who.Healing += h.Amount;
        who.Heals[h.Ability] = who.Heals.GetValueOrDefault(h.Ability) + h.Amount;
    }

    private static void Hit(FighterStat f, string? ability, int amount, bool crit)
    {
        var key = ability switch
        {
            null => "Melee",
            "damage shield" => "Damage shield",
            _ => ability,
        };
        f.Total.Add(amount, crit);
        if (!f.Abilities.TryGetValue(key, out var a)) f.Abilities[key] = a = new AbilityStat();
        a.Add(amount, crit);
    }

    /// <summary>Players and their pets have one-word capitalized names; mobs are "a …", "an …" or several words.</summary>
    public static bool IsPlayerName(string name)
    {
        if (name.Length < 3 || !char.IsUpper(name[0])) return false;
        for (int i = 1; i < name.Length; i++)
            if (!char.IsLetter(name[i])) return false;
        return true;
    }

    private void CloseIdle(DateTime t)
    {
        List<string>? done = null;
        foreach (var (mob, f) in _open)
            if (f.SlainAt is { } died ? (t - died).TotalSeconds > SameNameSeconds : (t - f.End).TotalSeconds > LingerSeconds) (done ??= []).Add(mob);
        if (done == null) return;
        foreach (var mob in done)
        {
            _open.Remove(mob, out var f);
            Finish(f!);
        }
    }

    private void Finish(Fight f)
    {
        // A mob that only hit you in passing isn't a fight.
        if (f.Damage > 0) closed(f);
    }
}
