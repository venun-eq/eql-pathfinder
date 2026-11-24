using EqlPathfinder.Engine.Analysis;

namespace EqlPathfinder.Engine.Combat;

/// <summary>
/// Your typical damage per second (you and your pet) in one zone and difficulty with one trio, and the same per spell.
/// Recent fights weigh more (half as much per 30 days, like the Ask page). Runs before the 11-10 patch that were mostly
/// Denon's Desperate Dirge are left out.
/// </summary>
public sealed record Usual(int Fights, double Dps, double FightDps, IReadOnlyDictionary<string, double> ByAbility)
{
    /// <summary>The same figures on the kill-time clock (pauses counted); the record's own are active time.</summary>
    public Usual? KillClock { get; init; }

    /// <summary>The figures for a clock: "kill", or active for anything else.</summary>
    public Usual For(string? clock) => clock == "kill" && KillClock != null ? KillClock : this;

    public const string PetKey = "\u0001pet";
    public const int MinFights = 5;
    public const double HalfLifeDays = 30;

    public static Usual? From(IEnumerable<Fight> fights, DateTime now)
    {
        var all = fights as IReadOnlyCollection<Fight> ?? fights.ToList();
        return From(all, now, "active") is { } active ? active with { KillClock = From(all, now, "kill") } : null;
    }

    private static Usual? From(IEnumerable<Fight> fights, DateTime now, string clock)
    {
        double sw = 0, dmg = 0, fightSeconds = 0;
        int n = 0;
        var by = new Dictionary<string, double>(StringComparer.Ordinal);
        // One stay in the zone at a time, so mobs fought together count their time once, as the meter does.
        foreach (var stay in fights.Where(f => f.MyDamage > 0 && !PreNerfDdd(f, f.MyDamage)).GroupBy(f => f.Stint))
        {
            var list = stay.ToList();
            double w = Math.Pow(0.5, Math.Max(0, (now - list.Min(f => f.Start)).TotalDays) / HalfLifeDays);
            n += list.Count;
            sw += w * MeterViews.CombatSeconds(list, clock);
            fightSeconds += w * list.Sum(f => f.SecondsBy(clock));
            foreach (var f in list)
            {
                dmg += w * f.MyDamage;
                foreach (var (key, amount) in MyAbilities(f)) by[key] = by.GetValueOrDefault(key) + w * amount;
            }
        }
        if (n < MinFights || sw <= 0) return null;
        return new Usual(n, dmg / sw, dmg / fightSeconds, by.ToDictionary(kv => kv.Key, kv => kv.Value / sw, StringComparer.Ordinal));
    }

    /// <summary>Your damage by spell, with your pet as one line (<see cref="PetKey"/>).</summary>
    public static IEnumerable<(string Key, long Damage)> MyAbilities(Fight f)
    {
        foreach (var who in f.Fighters.Values)
        {
            if (who.Kind == "pet") yield return (PetKey, who.Total.Damage);
            else if (who.Kind == "me") foreach (var (name, a) in who.Abilities) yield return (name, a.Damage);
        }
    }

    private static bool PreNerfDdd(Fight f, long mine)
    {
        if (f.Start >= RouteAnalysis.FarmPatch) return false;
        long ddd = f.Fighters.Values.Where(x => x.Kind == "me").SelectMany(x => x.Abilities).Where(a => RouteAnalysis.IsDdd(a.Key)).Sum(a => a.Value.Damage);
        return ddd * 2 >= mine;
    }
}

/// <summary>Everything the meter window draws from, copied so the window can read it while the tracker keeps going.</summary>
/// <param name="Fights">This session's finished fights, oldest first.</param>
/// <param name="Open">Fights going on now.</param>
public sealed record MeterFrame(string Me, string? Zone, int? Difficulty, bool InInstance, string? Trio, DateTime Session, DateTime Stint,
    IReadOnlyList<Fight> Fights, IReadOnlyList<Fight> Open, Usual? Usual, double? ImportPercent = null, bool NotKnown = false)
{
    public string Place => Zone == null ? "" : (Zone.StartsWith("The ", StringComparison.Ordinal) ? Zone[4..] : Zone) + (Difficulty is { } d ? $" D{d}" : "");
}

/// <summary>What to show.</summary>
/// <param name="Scope">"fight", "place" (this crawl, or this zone in the open world) or "session".</param>
/// <param name="By">"fighter", "spell", "mob" or "fight".</param>
/// <param name="Mode">"damage", "healing" or "taken".</param>
/// <param name="Fighter">Opened row: this fighter's spells.</param>
/// <param name="Mob">Opened row: this mob's fights.</param>
/// <param name="FightStart">Opened row: this fight (with <paramref name="FightMob"/>).</param>
/// <param name="Clock">
/// What "per second" divides by: "active" (the seconds spent fighting, pauses left out) or "kill" (first swing to the
/// death, pauses included). The other one rides along in <see cref="MeterView.OtherPerSecond"/>.
/// </param>
public sealed record MeterQuery(string Scope = "fight", string By = "fighter", string Mode = "damage", string? Fighter = null, string? Mob = null,
    DateTime? FightStart = null, string? FightMob = null, string Clock = "active");

/// <param name="Layout">"bar" (amount as a filled row), "usual" (bar against a tick for your usual), "table" (mob or fight line) or "ability".</param>
/// <param name="Open">What clicking the row opens: "fighter", "mob", "fight" or null.</param>
/// <param name="Tick">Where your usual falls along this row's bar (0..1), so the bar reads as ahead of it or short of it.</param>
public sealed record MeterRow(string Layout, string Label, string? Sub, long Amount, double PerSecond, double Share, double Bar, double? UsualPct,
    string Kind = "other", string? Open = null, string? Key = null, DateTime? FightStart = null, double Seconds = 0, int Count = 0,
    int Hits = 0, int Crits = 0, int Max = 0, bool Live = false, double? Tick = null);

/// <param name="Unit">"DPS" or "HPS".</param>
/// <param name="UsualPct">Your damage per second (you and your pet) against your usual here; null when there's no usual yet.</param>
/// <param name="By">The slice actually shown (a fight has no "by mob").</param>
/// <param name="CanBack">A row is open: the view can go back.</param>
/// <param name="Clock">The clock the numbers use ("active" or "kill"); <c>OtherSeconds</c> and <c>OtherPerSecond</c> are the same view on the other one.</param>
public sealed record MeterView(string Title, double Seconds, double PerSecond, string Unit, double? UsualPct, string By, bool CanBack,
    IReadOnlyList<MeterRow> Rows, string Footer, string PlaceLabel, string Clock = "active", double OtherSeconds = 0, double OtherPerSecond = 0);

/// <summary>The meter's slices: this fight, this crawl or zone, this session; by fighter, spell, mob or fight.</summary>
public static class MeterViews
{
    internal readonly record struct ResolvedScope(List<Fight> Fights, string Title, string By, bool CanBack, bool OneFight, string PlaceLabel);

    private readonly record struct ClockTiming(string Clock, string OtherClock, double Seconds, double OtherSeconds,
        long MyDamage, Usual? Usual, double? UsualPct);

    public static MeterView Build(MeterFrame f, MeterQuery q)
    {
        var scope = ResolveScope(f, q);
        var timing = MeasureTiming(f, scope.Fights, q.Clock);

        if (q.Mode == "healing") return BuildHealing(scope, timing, q.Fighter, q.Scope);
        if (q.Mode == "taken") return BuildTaken(scope, timing);

        long total = scope.Fights.Sum(x => x.Damage);
        string footer = FormatDamageFooter(scope.Fights, scope.OneFight, f.NotKnown, timing.Usual, timing.OtherClock, timing.OtherSeconds, total);
        if (q.Fighter != null) return BuildFighterDetail(f, scope, timing, q.Fighter, footer);

        var open = new HashSet<Fight>(f.Open);
        string by = scope.By is "spell" or "mob" or "fight" ? scope.By : "fighter";
        var rows = by switch
        {
            "spell" => RowsBySpell(scope.Fights, timing.Seconds, timing.MyDamage, timing.Usual),
            "mob" => RowsByMob(scope.Fights, timing.Clock, timing.Usual, open),
            "fight" when q.Mob != null => RowsByMobFights(scope.Fights, timing.Clock, timing.Usual, open),
            "fight" => RowsByEncounter(scope.Fights, timing.Clock, timing.Usual, open),
            _ => RowsByFighter(scope.Fights, f.Me, timing.Seconds, total, timing.Usual, timing.UsualPct),
        };
        return new MeterView(scope.Title, timing.Seconds, total / timing.Seconds, "DPS", timing.UsualPct, by, scope.CanBack,
            rows, footer, scope.PlaceLabel, timing.Clock, timing.OtherSeconds, total / timing.OtherSeconds);
    }

    internal static ResolvedScope ResolveScope(MeterFrame f, MeterQuery q)
    {
        string placeLabel = f.InInstance ? "Crawl" : "Zone";
        var all = f.Fights.Concat(f.Open).ToList();
        string by = q.By;
        // A fight on the meter is an encounter: every mob fought at the same time (see Encounters), so a pull of ten is one
        // fight from the first swing to the last kill, not ten fights that each started when that mob first swung at you.
        var encounters = Encounters(all);
        var pinned = q.FightStart is { } fs ? all.FirstOrDefault(x => x.Start == fs && x.Mob == q.FightMob) : null;
        if (pinned != null)
        {
            var fights = encounters.First(e => e.Contains(pinned));
            if (by is "mob" or "fight") by = "fighter";
            return new ResolvedScope(fights, EncounterLabel(fights), by, CanBack: true, OneFight: true, placeLabel);
        }
        if (q.Scope == "fight")
        {
            var cur = f.Open.Count > 0 ? f.Open.MaxBy(x => x.End) : f.Fights.Count > 0 ? f.Fights[^1] : null;
            var fights = cur != null ? encounters.First(e => e.Contains(cur)) : [];
            string title = cur != null ? EncounterLabel(fights) : "No fight yet";
            if (by is "mob" or "fight") by = "fighter";
            return new ResolvedScope(fights, title, by, CanBack: false, OneFight: true, placeLabel);
        }
        var slice = q.Scope == "place" ? all.Where(x => x.Stint == f.Stint).ToList() : all;
        string sliceTitle = q.Scope == "place" ? (f.Place.Length > 0 ? f.Place : placeLabel) : $"Since {f.Session:HH:mm}";
        if (q.Mob != null)
            return new ResolvedScope(slice.Where(x => x.Mob == q.Mob).ToList(), q.Mob, "fight", CanBack: true, OneFight: false, placeLabel);
        return new ResolvedScope(slice, sliceTitle, by, CanBack: false, OneFight: false, placeLabel);
    }

    private static ClockTiming MeasureTiming(MeterFrame f, List<Fight> fights, string? queryClock)
    {
        string clock = queryClock == "kill" ? "kill" : "active";
        string otherClock = clock == "kill" ? "active" : "kill";
        double seconds = CombatSeconds(fights, clock);
        double otherSeconds = CombatSeconds(fights, otherClock);
        long mine = fights.Sum(x => x.MyDamage);
        var usual = f.NotKnown ? null : f.Usual?.For(clock);
        double? usualPct = usual is { Dps: > 0 } u && mine > 0 ? mine / seconds / u.Dps : null;
        return new ClockTiming(clock, otherClock, seconds, otherSeconds, mine, usual, usualPct);
    }

    private static MeterView BuildHealing(ResolvedScope s, ClockTiming t, string? fighter, string queryScope)
    {
        long healed = s.Fights.Sum(x => x.Healing);
        var rows = fighter != null
            ? Amounts(s.Fights.SelectMany(x => x.Fighters).Where(kv => kv.Key == fighter).SelectMany(kv => kv.Value.Heals), t.Seconds, "ability")
            : Amounts(s.Fights.SelectMany(x => x.Fighters).Where(kv => kv.Value.Healing > 0)
                .Select(kv => new KeyValuePair<string, long>(kv.Key, kv.Value.Healing)), t.Seconds, "bar", "fighter", name => KindOf(s.Fights, name));
        string footer = rows.Count == 0 ? "No healing in this " + (queryScope == "fight" ? "fight" : "stretch") : $"{healed:N0} healed";
        return new MeterView(fighter ?? s.Title, t.Seconds, healed / t.Seconds, "HPS", null, "fighter", s.CanBack || fighter != null,
            rows, footer, s.PlaceLabel, t.Clock, t.OtherSeconds, healed / t.OtherSeconds);
    }

    private static MeterView BuildTaken(ResolvedScope s, ClockTiming t)
    {
        long taken = s.Fights.Sum(x => x.Taken);
        var rows = s.Fights.Count == 1
            ? Amounts(s.Fights[0].TakenBy, t.Seconds, "ability")
            : Amounts(s.Fights.GroupBy(x => x.Mob).Select(g => new KeyValuePair<string, long>(g.Key, g.Sum(x => x.Taken))).Where(kv => kv.Value > 0), t.Seconds, "bar");
        return new MeterView(s.Title, t.Seconds, taken / t.Seconds, "DTPS", null, "fighter", s.CanBack, rows,
            rows.Count == 0 ? "Nothing hit you" : $"{taken:N0} taken", s.PlaceLabel, t.Clock, t.OtherSeconds, taken / t.OtherSeconds);
    }

    internal static string FormatDamageFooter(List<Fight> fights, bool oneFight, bool notKnown, Usual? usual,
        string otherClock, double otherSeconds, long total)
    {
        if (fights.Count == 0) return "";
        // The clock you didn't pick rides along here; your usual is the tick on the bars, not a number to do sums with.
        string footer = $"{(otherClock == "kill" ? "Kill time" : "Active")} {Clock(otherSeconds)} · {total / otherSeconds:N0} DPS";
        int kills = fights.Sum(x => Math.Max(1, x.Kills));
        if (oneFight)
        {
            if (kills > 1) footer = $"{kills:N0} mobs · " + footer;
        }
        else
        {
            int n = Encounters(fights).Count;
            if (n > 1 || kills > 1) footer = (kills > n ? $"{kills:N0} kills in {n:N0} fights · " : $"{n:N0} fights · ") + footer;
        }
        if (notKnown) footer = "Loadout not known · " + footer;   // first, so a narrow meter keeps the reason
        else if (usual == null) footer += " · no usual here yet";
        return footer;
    }

    private static MeterView BuildFighterDetail(MeterFrame f, ResolvedScope s, ClockTiming t, string fighter, string footer)
    {
        var abilities = new Dictionary<string, AbilityStat>(StringComparer.Ordinal);
        foreach (var who in s.Fights.SelectMany(x => x.Fighters).Where(kv => kv.Key == fighter))
            foreach (var (name, a) in who.Value.Abilities)
            {
                if (!abilities.TryGetValue(name, out var sum)) abilities[name] = sum = new AbilityStat();
                sum.Add(a);
            }
        bool me = fighter == f.Me || KindOf(s.Fights, fighter) == "me";
        // Opening yourself shows your pets too, one line each (open one for its own spells): your row counts them.
        var pets = me ? PetTotals(s.Fights) : [];
        long fighterTotal = Math.Max(1, abilities.Values.Sum(a => a.Damage) + pets.Sum(p => p.Stat.Damage));
        double? petUsual = pets.Count == 1 && t.Usual?.ByAbility.GetValueOrDefault(Usual.PetKey) is > 0 and var pu ? pu : null;
        var rows = abilities.Select(kv => new MeterRow("ability", kv.Key, null, kv.Value.Damage,
                kv.Value.Damage / t.Seconds, (double)kv.Value.Damage / fighterTotal, (double)kv.Value.Damage / fighterTotal,
                me && t.Usual?.ByAbility.GetValueOrDefault(kv.Key) is > 0 and var ua ? kv.Value.Damage / t.Seconds / ua : null,
                Hits: kv.Value.Hits, Crits: kv.Value.Crits, Max: kv.Value.Max))
            .Concat(pets.Select(p => new MeterRow("ability", $"{p.Name} (pet)", "pet", p.Stat.Damage, p.Stat.Damage / t.Seconds,
                (double)p.Stat.Damage / fighterTotal, (double)p.Stat.Damage / fighterTotal,
                petUsual is { } u1 ? p.Stat.Damage / t.Seconds / u1 : null, "pet", "fighter", p.Name,
                Hits: p.Stat.Hits, Crits: p.Stat.Crits, Max: p.Stat.Max)))
            .OrderByDescending(r => r.Amount).ToList();
        return new MeterView(fighter, t.Seconds, fighterTotal / t.Seconds, "DPS", me ? t.UsualPct : null, "spell", true,
            rows, footer, s.PlaceLabel, t.Clock, t.OtherSeconds, fighterTotal / t.OtherSeconds);
    }

    private static List<MeterRow> RowsBySpell(List<Fight> fights, double seconds, long mine, Usual? usual)
    {
        var mineBy = new Dictionary<string, long>(StringComparer.Ordinal);
        string petName = "Pet";
        foreach (var x in fights)
        {
            foreach (var (key, amount) in Usual.MyAbilities(x)) mineBy[key] = mineBy.GetValueOrDefault(key) + amount;
            if (x.Fighters.FirstOrDefault(kv => kv.Value.Kind == "pet").Key is { } pn) petName = pn;
        }
        long max = Math.Max(1, mineBy.Count > 0 ? mineBy.Values.Max() : 1);
        return mineBy.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv =>
        {
            bool pet = kv.Key == Usual.PetKey;
            double per = kv.Value / seconds;
            double? pct = usual?.ByAbility.GetValueOrDefault(kv.Key) is > 0 and var ua ? per / ua : null;
            return new MeterRow("usual", pet ? petName : kv.Key, pet ? "pet" : null, kv.Value, per, mine > 0 ? (double)kv.Value / mine : 0,
                pct is { } p ? Math.Min(1, p * 0.8) : (double)kv.Value / max * 0.8, pct, pet ? "pet" : "me",
                pet ? "fighter" : null, pet ? petName : null, Tick: pct != null ? 0.8 : null);
        }).ToList();
    }

    private static List<MeterRow> RowsByMob(List<Fight> fights, string clock, Usual? usual, HashSet<Fight> open) =>
        fights.GroupBy(x => x.Mob).Select(g =>
        {
            double secs = g.Sum(x => x.SecondsBy(clock));
            long my = g.Sum(x => x.MyDamage);
            int n = g.Sum(x => Math.Max(1, x.Kills));
            return new MeterRow("table", g.Key, null, my, my / secs, 0, 0, usual is { FightDps: > 0 } us ? my / secs / us.FightDps : null,
                Open: "mob", Key: g.Key, Seconds: secs / n, Count: n, Live: g.Any(open.Contains));
        }).OrderByDescending(r => r.Count).ThenBy(r => r.Label, StringComparer.Ordinal).ToList();

    /// <summary>One mob's own fights (opened from "by mob"): each from its first swing to its death.</summary>
    private static List<MeterRow> RowsByMobFights(List<Fight> fights, string clock, Usual? usual, HashSet<Fight> open) =>
        fights.OrderByDescending(x => x.Start).Select(x => new MeterRow("table", x.Mob, null, x.MyDamage, x.MyDamage / x.SecondsBy(clock), 0, 0,
            usual is { FightDps: > 0 } us ? x.MyDamage / x.SecondsBy(clock) / us.FightDps : null, Open: "fight", Key: x.Mob, FightStart: x.Start,
            Seconds: x.SecondsBy(clock), Count: x.Kills > 1 ? x.Kills : 0, Live: open.Contains(x))).ToList();

    /// <summary>One row per encounter, its time counted once however many mobs were in it, against your usual DPS.</summary>
    private static List<MeterRow> RowsByEncounter(List<Fight> fights, string clock, Usual? usual, HashSet<Fight> open) =>
        Encounters(fights).OrderByDescending(e => e[0].Start).Select(e =>
        {
            double secs = CombatSeconds(e, clock);
            long my = e.Sum(x => x.MyDamage);
            return new MeterRow("table", EncounterLabel(e), null, my, my / secs, 0, 0, usual is { Dps: > 0 } us ? my / secs / us.Dps : null,
                Open: "fight", Key: e[0].Mob, FightStart: e[0].Start, Seconds: secs, Live: e.Any(open.Contains));
        }).ToList();

    private static List<MeterRow> RowsByFighter(List<Fight> fights, string me, double seconds, long total, Usual? usual, double? usualPct)
    {
        // Your pets count as you: one row for you and them (as your usual already does), opened for the split.
        // Another player's pet can't be tied to them (only its owner sees "Attacking … Master."), so it stays a row.
        var sums = new Dictionary<string, (long Damage, string Kind, long Pet)>(StringComparer.Ordinal);
        var myPets = PetNames(fights);
        foreach (var (name, who) in fights.SelectMany(x => x.Fighters))
        {
            bool pet = who.Kind == "pet" || (who.Kind == "other" && myPets.Contains(name));
            string key = pet ? me : name;
            var cur = sums.GetValueOrDefault(key);
            sums[key] = (cur.Damage + who.Total.Damage, pet || who.Kind == "me" || cur.Kind == "me" ? "me" : who.Kind, cur.Pet + (pet ? who.Total.Damage : 0));
        }
        long max = Math.Max(1, sums.Count > 0 ? sums.Values.Max(v => v.Damage) : 1);
        // Room for the usual line when you are behind it: the bars share a scale that reaches it.
        if (usual is { Dps: > 0 } us0 && sums.Values.Any(v => v.Kind == "me")) max = Math.Max(max, (long)Math.Ceiling(us0.Dps * seconds));
        return sums.Where(kv => kv.Value.Damage > 0).OrderByDescending(kv => kv.Value.Damage).Select(kv => new MeterRow("bar", kv.Key,
            kv.Value.Pet > 0 ? $"incl. pet {(double)kv.Value.Pet / kv.Value.Damage:P0}" : null, kv.Value.Damage, kv.Value.Damage / seconds,
            total > 0 ? (double)kv.Value.Damage / total : 0, (double)kv.Value.Damage / max, kv.Value.Kind == "me" ? usualPct : null, kv.Value.Kind, "fighter", kv.Key,
            // Your usual damage over this much time, on the same scale as the bars: your bar ends past it or short of it.
            Tick: kv.Value.Kind == "me" && usual is { Dps: > 0 } ut ? Math.Min(1, ut.Dps * seconds / max) : null)).ToList();
    }

    /// <summary>A mob that starts within this many seconds of the last one ending is still the same encounter.</summary>
    public const double EncounterGapSeconds = 2;

    /// <summary>
    /// Fights grouped into encounters, oldest first: each encounter is a run of fights whose times overlap (or follow within
    /// <see cref="EncounterGapSeconds"/>). A pull of several mobs is one encounter, timed from its first swing to its last,
    /// with overlapping time counted once, so the mobs you got to last don't look slow for having swung at you early.
    /// </summary>
    public static List<List<Fight>> Encounters(IEnumerable<Fight> fights)
    {
        var result = new List<List<Fight>>();
        DateTime end = DateTime.MinValue;
        foreach (var x in fights.OrderBy(x => x.Start).ThenBy(x => x.End))
        {
            if (result.Count > 0 && (x.Start - end).TotalSeconds <= EncounterGapSeconds)
            {
                result[^1].Add(x);
                if (x.End > end) end = x.End;
            }
            else
            {
                result.Add([x]);
                end = x.End;
            }
        }
        return result;
    }

    /// <summary>"a ghoul", "a ghoul ×3" (same name), or the mob you hit hardest and how many more ("a ghoul +5").</summary>
    public static string EncounterLabel(IReadOnlyList<Fight> e)
    {
        if (e.Count == 0) return "";
        int kills = e.Sum(x => Math.Max(1, x.Kills));
        var top = e.GroupBy(x => x.Mob).OrderByDescending(g => g.Sum(x => x.Damage)).ThenBy(g => g.Min(x => x.Start)).First().Key;
        if (kills <= 1) return top;
        return e.All(x => x.Mob == top) ? $"{top} ×{kills}" : $"{top} +{kills - 1}";
    }

    /// <summary>Your pets in these fights, each with its damage, hits, crits and biggest hit summed over its spells and melee.</summary>
    private static List<(string Name, AbilityStat Stat)> PetTotals(List<Fight> fights)
    {
        var by = new Dictionary<string, AbilityStat>(StringComparer.Ordinal);
        var myPets = PetNames(fights);
        foreach (var (name, who) in fights.SelectMany(x => x.Fighters).Where(kv => myPets.Contains(kv.Key)))
        {
            if (!by.TryGetValue(name, out var sum)) by[name] = sum = new AbilityStat();
            sum.Add(who.Total);
        }
        return by.Where(kv => kv.Value.Damage > 0).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    /// <summary>
    /// The names your pets had in these fights. A pet counts as one of other fighters until it first says "Attacking …
    /// Master.", so the same name earlier in the slice is that pet too.
    /// </summary>
    private static HashSet<string> PetNames(List<Fight> fights) =>
        fights.SelectMany(x => x.Fighters).Where(kv => kv.Value.Kind == "pet").Select(kv => kv.Key).ToHashSet(StringComparer.Ordinal);

    private static string KindOf(List<Fight> fights, string name) =>
        fights.SelectMany(x => x.Fighters).FirstOrDefault(kv => kv.Key == name).Value?.Kind ?? "other";

    private static List<MeterRow> Amounts(IEnumerable<KeyValuePair<string, long>> items, double seconds, string layout, string? opens = null,
        Func<string, string>? kind = null)
    {
        var sums = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (k, v) in items) sums[k] = sums.GetValueOrDefault(k) + v;
        long total = Math.Max(1, sums.Values.Sum()), max = Math.Max(1, sums.Count > 0 ? sums.Values.Max() : 1);
        return sums.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => new MeterRow(layout, kv.Key, null, kv.Value,
            kv.Value / seconds, (double)kv.Value / total, (double)kv.Value / max, null, kind?.Invoke(kv.Key) ?? "other", opens, kv.Key)).ToList();
    }

    /// <summary>
    /// The meter as it stood at the end of the session that was running at <paramref name="at"/> (the CLI and the
    /// developer snapshot use it; the live meter builds its frames itself).
    /// </summary>
    public static MeterFrame? LookBack(FightStore store, string character, string me, DateTime at)
    {
        if (store.SessionBefore(character, at) is not { } session) return null;
        var fights = store.Session(character, session).Where(f => f.Start <= at).ToList();
        if (fights.Count == 0) return null;
        var last = fights[^1];
        var usual = Usual.From(store.InPlace(character, last.Zone, last.Difficulty, last.Trio), last.End);
        return new MeterFrame(me, last.Zone, last.Difficulty, last.Difficulty != null, last.Trio, session, last.Stint, fights, [], usual);
    }

    private static string Clock(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Round(seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    /// <summary>
    /// Time in combat on a clock, overlapping fights counted once: "kill" is each fight from its first swing to its last
    /// action; anything else is active time, the stretches of actual fighting with the pauses left out.
    /// </summary>
    public static double CombatSeconds(IReadOnlyList<Fight> fights, string? clock = "kill")
    {
        if (fights.Count == 0) return 1;
        if (fights.Count == 1) return fights[0].SecondsBy(clock);
        if (clock != "kill")
        {
            var spans = new List<(DateTime From, DateTime To)>();
            foreach (var x in fights)
            {
                if (x.Bursts.Count == 0) spans.Add((x.Start, x.End.AddSeconds(1)));
                else foreach (var b in x.Bursts) spans.Add((x.Start.AddSeconds(b[0]), x.Start.AddSeconds(b[1] + 1)));
            }
            spans.Sort((a, b) => a.From.CompareTo(b.From));
            double sum = 0;
            var (from, to) = spans[0];
            foreach (var sp in spans.Skip(1))
            {
                if (sp.From <= to) { if (sp.To > to) to = sp.To; continue; }
                sum += (to - from).TotalSeconds;
                (from, to) = sp;
            }
            return Math.Max(1, sum + (to - from).TotalSeconds);
        }
        double total = 0;
        DateTime? s = null, e = null;
        foreach (var f in fights.OrderBy(x => x.Start))
        {
            var end = f.End.AddSeconds(1);
            if (s == null) { s = f.Start; e = end; continue; }
            if (f.Start <= e) { if (end > e) e = end; continue; }
            total += (e!.Value - s.Value).TotalSeconds;
            s = f.Start;
            e = end;
        }
        total += (e!.Value - s!.Value).TotalSeconds;
        return Math.Max(1, total);
    }
}
