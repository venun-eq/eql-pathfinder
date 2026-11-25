using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Parsing;

namespace EqlPathfinder.Engine.Tracking;

/// <summary>Receives what the tracker finds. All methods are optional.</summary>
public interface ITrackerSink
{
    void OnVisitClosed(Visit visit) { }
    /// <summary>A closed visit changed later: its crawl reward got sized at a ding.</summary>
    void OnVisitUpdated(Visit visit) { }
    void OnKill(KillRecord kill) { }
    void OnLoot(LootRecord loot) { }
    void OnLevel(LevelRecord level) { }
    void OnHighTier(HighTierDrop drop) { }
}

/// <summary>
/// The state machine: visits (instance runs), levels and crawl-reward sizing, kills, loot, damage.
/// Deterministic: the same lines always give the same records, and its state round-trips through JSON.
/// </summary>
public sealed class Tracker
{
    private readonly ITrackerSink _sink;
    private int _rewardSeq;

    public Tracker(EngineState state, TrackerOptions options, ITrackerSink sink)
    {
        State = state;
        Options = options;
        _sink = sink;
    }

    public EngineState State { get; }
    public TrackerOptions Options { get; }

    /// <summary>The visit you are inside right now (including a finished crawl you haven't left yet).</summary>
    public Visit? Inside =>
        State.Current is { } c && c.Zone == State.Zone && (c.SegmentStart != null || c.Completed) ? c : null;

    /// <summary>Call for every timestamped line, before <see cref="Apply"/>.</summary>
    public void OnLine(DateTime t)
    {
        FlushPending(t);
        if (State.LastTime is { } last && (t - last).TotalMinutes > Options.SilentLogMinutes)
            EndForSilence(last);
        State.FirstTime ??= t;
        State.LastTime = t;
        CheckTimeouts(t);
    }

    /// <summary>End of input (backfill finished): emit anything still waiting.</summary>
    public void Flush() => FlushPending(null);

    /// <summary>Live mode: call about once a second with the wall clock.</summary>
    public void Tick(DateTime wallNow)
    {
        if (State.LastTime is not { } last) return;
        if ((wallNow - last).TotalMinutes > Options.SilentLogMinutes) { EndForSilence(last); return; }
        CheckTimeouts(wallNow < last.AddMinutes(5) ? wallNow : last.AddMinutes(5));
    }

    public void Apply(LogEvent e)
    {
        switch (e)
        {
            case ExperienceGained x: OnExperience(x); break;
            case DamageDealt d: OnDamage(d); break;
            case DamageTaken d: if (Inside is { } inside) inside.Damage.Taken += d.Amount; break;
            case MobSlain s: OnSlain(s); break;
            case SpellCast c: if (Inside is { } casting) casting.Casts[c.Spell] = casting.Casts.GetValueOrDefault(c.Spell) + 1; break;
            case MoteLooted l: OnLoot(l); break;
            case ZoneEntered z: OnZone(z); break;
            case RewardExperience r: OnReward(r); break;
            case LevelGained l: OnLevel(l); break;
            case CrawlCompleted c: OnCompleted(c); break;
            case InstanceRefunded: if (State.Current is { } cur) cur.Refunded = true; break;
            case InstanceCreated ic: State.NewInstanceSinceEntry = true; State.Instance.Created(ic.Time, ic.Zone); break;
            case InstanceInvite ii: State.NewInstanceSinceEntry = true; State.Instance.Invited(ii.Time, ii.Zone); break;
            case PetIdentified p: if (!p.Charmed) State.Pets.Add(p.Pet); break;   // a charmed mob is a pet only while the parser says so
            case WhoSelf w: OnWho(w); break;
            case Reincarnated r: OnReincarnated(r); break;
            case LoadoutSwapped sw: OnSwap(sw); break;
            case LoadoutClassesChanged cc: OnClassesChanged(cc); break;
            case YouDied d: OnDied(d); break;
            case MoteOffered o:
                State.Offered.Add(o.Kind, o.Tier, o.Count, false);
                _sink.OnLoot(new LootRecord(o.Time, null, State.Zone, o.Kind, o.Tier, o.Count, "Constructed Potential", false, true));
                break;
            case VoidTouchedReceived: State.VoidTouchedReceived++; break;
        }
    }

    // ------------------------------------------------------------------ zones and visits

    /// <summary>
    /// A zone entered without a difficulty in its name is still an instance when it's the one just created or offered
    /// (<see cref="InstanceWatch"/>), or a return to an unfinished D0 crawl of the same name, from the open world of that name, within
    /// <see cref="TrackerOptions.ResumeMinutes"/> (after a death, say). Otherwise it's the open world, as before.
    /// </summary>
    private ZoneEntered AsInstance(ZoneEntered z)
    {
        var resolved = State.Instance.Resolve(z);
        if (resolved.Difficulty != null) return resolved;
        if (State.Current is { Difficulty: 0, Completed: false } c && c.PausedAt is { } pa && string.Equals(z.Zone, c.Zone, StringComparison.Ordinal)
            && string.Equals(State.Zone, c.Zone, StringComparison.Ordinal) && (z.Time - pa).TotalMinutes < Options.ResumeMinutes && !State.NewInstanceSinceEntry)
            return InstanceWatch.AsD0(z, c.BaseZone, c.DifficultyLabel);
        return resolved;
    }

    private void OnZone(ZoneEntered z)
    {
        z = AsInstance(z);
        var t = z.Time;
        var c = State.Current;
        State.Zone = z.Zone;
        if (c != null)
        {
            if (c.SegmentStart is { } ss)
            {
                if (!c.Completed) c.ActiveSeconds += Math.Max(0, (t - ss).TotalSeconds);
                c.SegmentStart = null;
                c.PausedAt = t;
                c.ExitedAt = t;
            }
            else if (c.Completed && c.ExitedAt == null)
            {
                c.ExitedAt = t;
            }

            bool hasActivity = c.Kills > 0 || c.Loot.MoteCount + c.Loot.FragmentCount > 0;
            bool resume = z.IsInstance && z.Zone == c.Zone && !c.Completed && !State.NewInstanceSinceEntry &&
                          c.PausedAt is { } pa && (t - pa).TotalMinutes < Options.ResumeMinutes && hasActivity;
            if (resume)
            {
                if (c.PausedAt is { } away) c.Pauses.Add(new PauseSpan(away, t));
                c.SegmentStart = t;
                c.PausedAt = null;
                c.ExitedAt = null;
                return;
            }
            if (c.Completed || z.IsInstance || !hasActivity) Close(c);
            // Otherwise: an unfinished visit, out of its instance (death, errand). Paused; may resume.
        }
        if (z.IsInstance) StartVisit(z);
    }

    private void StartVisit(ZoneEntered z)
    {
        var v = new Visit
        {
            Id = "v-" + z.Time.ToString("yyyyMMddHHmmss"),
            Zone = z.Zone,
            BaseZone = z.BaseZone,
            Difficulty = z.Difficulty,
            DifficultyLabel = z.DifficultyLabel,
            Start = z.Time,
            SegmentStart = z.Time,
            LastActivity = z.Time,
            Trio = State.Trio,
            Race = State.Race,
            LevelStart = State.Level,
            Slot = State.Loadout?.Slot,
            // A slot's trio is its own source, never "a /who during the crawl"; a slot no /who has named has no trio at all.
            TrioFrom = State.Loadout switch { null => null, { State: "known" } when State.Trio != null => "slot", { State: "known" } => null, _ => "unmet" },
        };
        if (State.LastVisitExit is { } le)
        {
            double gap = (z.Time - le).TotalMinutes;
            if (gap >= 0 && gap <= Options.ResetCapMinutes) v.ResetMinutes = gap;
        }
        State.Current = v;
        State.NewInstanceSinceEntry = false;
    }

    private static bool WorthKeeping(Visit v) =>
        v.Kills > 0 || v.Loot.MoteCount + v.Loot.FragmentCount > 0 || v.Completed || v.ActiveSeconds >= 120;

    private void Close(Visit c)
    {
        c.Closed = true;
        c.SegmentStart = null;
        c.ExitedAt ??= c.PausedAt ?? c.CompletedAt ?? State.LastTime;
        c.LevelEnd ??= State.Level;   // a frozen visit (FreezeLevels) keeps the level its own loadout reached
        if (ReferenceEquals(State.Current, c)) State.Current = null;
        if (!WorthKeeping(c)) return;
        State.LastVisitExit = c.ExitedAt;
        if (c.RewardSizing) State.AwaitingReward[c.Id] = c;
        _sink.OnVisitClosed(c);
    }

    private void OnCompleted(CrawlCompleted e)
    {
        var c = State.Current;
        if (c == null || c.Completed || State.Zone != c.Zone) return;
        if (c.SegmentStart is { } ss) c.ActiveSeconds += Math.Max(0, (e.Time - ss).TotalSeconds);
        c.SegmentStart = null;
        c.Completed = true;
        c.CompletedAt = e.Time;
        c.LastActivity = e.Time;
    }

    private void EndForSilence(DateTime last)
    {
        var c = State.Current;
        if (c == null) return;
        if (c.SegmentStart is { } ss && !c.Completed)
            c.ActiveSeconds += Math.Max(0, (last - ss).TotalSeconds);
        c.SegmentStart = null;
        c.PausedAt ??= last;
        c.ExitedAt ??= last;
        Close(c);
    }

    private void CheckTimeouts(DateTime now)
    {
        var c = State.Current;
        if (c == null || c.Completed || c.SegmentStart != null) return;
        if (c.PausedAt is { } pa && (now - pa).TotalMinutes > Options.ResumeMinutes) Close(c);
    }

    // ------------------------------------------------------------------ XP, levels, rewards

    /// <summary>
    /// An XP line fills the bar, but it is not a kill (kills-at-cap): a kill is its slain line. It pairs with the slain line in its
    /// second, whichever is written first; one with none is a quest or task reward, its percent kept in the visit as a reward.
    /// </summary>
    private void OnExperience(ExperienceGained x)
    {
        var t = x.Time;
        if (x.Bonus) State.NoteBonusXp(t);
        State.KillPercentInLevel += x.Percent;
        State.LastReward = null;
        State.LastRewardTime = null;

        var c = Inside;
        var xp = new PendingKill
        {
            Time = t,
            Percent = x.Percent,
            Unknown = x.Unknown,
            Party = x.Party,
            VisitId = c?.Id,
            Zone = State.Zone,
            Difficulty = c?.Difficulty,
            Level = State.Level,
        };
        int i = State.PendingSlains.FindIndex(s => Math.Abs((t - s.Time).TotalSeconds) <= 1 && (!s.NeedsParty || x.Party));
        if (i < 0) { State.PendingXp.Add(xp); return; }
        var slain = State.PendingSlains[i];
        State.PendingSlains.RemoveAt(i);
        EmitKill(slain, xp);
    }

    /// <summary>The kill is counted in its visit when its slain line is read; its XP, when the XP line pairs with it.</summary>
    private void EmitKill(PendingSlain s, PendingKill? xp)
    {
        // A kill's level is the level its XP line was read at (the XP line comes before the ding, so the kill whose XP filled the
        // bar counts as below the cap even when its slain line is read after the ding); with no XP line, its slain line's.
        var level = xp?.Level ?? s.Level;
        if (s.VisitId != null && FindVisit(s.VisitId) is { } v)
        {
            if (s.Seconds is { } k) v.KillSeconds.Add(k);
            if (xp is { Unknown: true }) v.UnknownXpKills++;
            else
            {
                if (xp != null) v.PercentKills++;
                // At the cap the bar fills but no level follows: a kill there is never an XP kill, and its percent is kept as the log
                // says it, not as levels (xp-at-cap).
                if (level >= Options.CapOn(s.Time, level)) { v.CapKills++; v.CapPercent += xp?.Percent ?? 0; }
                else if (xp != null) v.KillPercent += xp.Percent;
            }
        }
        _sink.OnKill(new KillRecord(s.Time, s.VisitId, s.Zone, s.Difficulty, level, s.Mob, xp?.Percent ?? 0, xp?.Party ?? false, s.Seconds, xp?.Unknown ?? false));
    }

    /// <summary>
    /// Settles what has waited more than a second (all of it with <c>null</c>): a slain line with no XP line is a kill that gave
    /// none (another player's outside an instance, with no party XP, is someone else's); an XP line with no slain line is a reward.
    /// </summary>
    private void FlushPending(DateTime? now)
    {
        bool Old(DateTime t) => now is not { } n || (n - t).TotalSeconds > 1;
        foreach (var s in State.PendingSlains.Where(s => Old(s.Time)).ToList())
        {
            State.PendingSlains.Remove(s);
            if (!s.NeedsParty) EmitKill(s, null);
        }
        foreach (var xp in State.PendingXp.Where(x => Old(x.Time)).ToList())
        {
            State.PendingXp.Remove(xp);
            if (xp.Percent > 0 && xp.VisitId != null && FindVisit(xp.VisitId) is { } v)
                v.Rewards.Add(new VisitReward { Id = $"x-{xp.Time:yyyyMMddHHmmss}-{++_rewardSeq}", Time = xp.Time, Share = xp.Percent, Sizing = false });
        }
    }

    private void OnReward(RewardExperience e)
    {
        var c = Inside ?? State.Current;
        var r = new VisitReward { Id = $"r-{e.Time:yyyyMMddHHmmss}-{++_rewardSeq}", Time = e.Time };
        var rf = new RewardRef(c?.Id ?? "", r.Id);
        c?.Rewards.Add(r);
        State.OpenLevelRewards.Add(rf);
        State.LastReward = rf;
        State.LastRewardTime = e.Time;
    }

    private void OnLevel(LevelGained l)
    {
        // A ding the held slot's known level rules out: that slot isn't the one played, the swap was the last run's file.
        if (State.PendingSwap is { } held && Slot(held.Slot).Level is { } heldLevel && (l.Level <= heldLevel || l.Level > heldLevel + 1)) State.PendingSwap = null;
        // A ding the slot on can't make but the held slot can: the held slot is the one played, so it counts and the slot left keeps its memory.
        else if (State.PendingSwap != null && State.Level is { } on && l.Level != on + 1 && l.Level != on) ConfirmSwap();
        var t = l.Time;
        var open = State.OpenLevelRewards;
        if (open.Count > 0)
        {
            double remainder = Math.Max(0, 100 - State.KillPercentInLevel);
            foreach (var rf in open)
            {
                var r = FindReward(rf);
                if (r == null) continue;
                if (State.LevelPositionKnown)
                {
                    r.Share += remainder / open.Count;
                    if (open.Count > 1) r.Estimated = true;
                }
                else r.Unknown = true;
            }
        }

        // A reward that itself caused this ding spills into the new level and keeps sizing there.
        bool spill = State.LastReward != null && State.LastRewardTime is { } rt && (t - rt).TotalSeconds <= 1.5;
        var keep = new List<RewardRef>();
        foreach (var rf in open)
        {
            if (spill && rf == State.LastReward) { keep.Add(rf); continue; }
            FinishReward(rf);
        }
        State.OpenLevelRewards = keep;
        if (State.Level is { } prev && l.Level != prev + 1 && l.Level != prev)
        {
            // A slot's memory (its last /who) that this level contradicts is stale (reincarnated, or set up again unseen): its
            // trio isn't this play's, so the levels wait for the next /who to name them (login-swap).
            if (State.Loadout is { State: "known" } known)
            {
                if (State.Current is { TrioConfirmed: false, TrioFrom: "slot" } mem) mem.TrioFrom = null;   // not "unmet": the next /who may still name it back (TrioInference)
                var m = Slot(known.Slot);
                (m.Classes, m.Race, m.Level, m.Known, m.ClassesChanged, m.WhoAt) = (null, null, null, false, false, null);
                State.Loadout = new LoadoutOn(known.Slot, "new");
            }
            ForgetLoadout();
            // The level the crawl started at belonged to the old loadout; one already confirmed is over for levels.
            if (State.Current is { } swapped)
            {
                if (swapped.TrioKnown) FreezeLevels(swapped);
                else swapped.LevelStart = l.Level - 1;
            }
        }
        State.Level = l.Level;
        SyncSlotLevel();
        TouchRoster(l.Level, t);
        State.KillPercentInLevel = 0;
        State.LevelPositionKnown = true;
        if (State.Current is { LevelEnd: null } cur) cur.LevelStart ??= l.Level - 1;
        if (Inside is { } c) c.LastActivity = t;
        _sink.OnLevel(new LevelRecord(t, "ding", l.Level, State.Trio));
    }

    private VisitReward? FindReward(RewardRef rf)
    {
        var v = FindVisit(rf.VisitId);
        return v?.Rewards.Find(x => x.Id == rf.RewardId);
    }

    private Visit? FindVisit(string id)
    {
        if (id.Length == 0) return null;
        if (State.Current is { } c && c.Id == id) return c;
        return State.AwaitingReward.GetValueOrDefault(id);
    }

    private void FinishReward(RewardRef rf)
    {
        var v = FindVisit(rf.VisitId);
        var r = v?.Rewards.Find(x => x.Id == rf.RewardId);
        if (v == null || r == null) return;
        r.Sizing = false;
        if (v.Closed)
        {
            if (!v.RewardSizing) State.AwaitingReward.Remove(v.Id);
            _sink.OnVisitUpdated(v);
        }
    }

    /// <summary>The level's starting point is no longer known: open rewards can't be sized.</summary>
    private void ForgetLevelPosition()
    {
        foreach (var rf in State.OpenLevelRewards)
        {
            if (FindReward(rf) is { } r) r.Unknown = true;
            FinishReward(rf);
        }
        State.OpenLevelRewards = new();
        State.LastReward = null;
        State.KillPercentInLevel = 0;
    }

    /// <summary>
    /// Freezes an open crawl's level range on loadout swap or reincarnation so another loadout's level does not
    /// overwrite it, while keeping the visit open so visit IDs and sequence numbers remain stable.
    /// </summary>
    private void FreezeLevels(Visit c)
    {
        if (c.Closed || c.LevelEnd != null || State.Level is not { } lv) return;
        c.LevelEnd = lv;
    }

    private void OnWho(WhoSelf w)
    {
        // A /who of the slot on proves nothing of the held one: only a trio its memory names, or a slot with none, confirms it.
        if (State.PendingSwap is { } held)
        {
            var mem = Slot(held.Slot).Classes;
            if (mem is not { Count: > 0 } || ClassNames.TrioKey(mem) == ClassNames.TrioKey(w.Classes)) ConfirmSwap();
            else if (State.Classes is { Count: > 0 } on && ClassNames.TrioKey(on) == ClassNames.TrioKey(w.Classes)) State.PendingSwap = null;
        }
        ApplyWho(w);
    }

    private void ApplyWho(WhoSelf w)
    {
        if (State.Current is { TrioKnown: true, LevelEnd: null } cv &&
            (State.Level is { } sl && sl != w.Level || ClassNames.TrioKey(w.Classes) != ClassNames.TrioKey(cv.Trio ?? "")))
            FreezeLevels(cv);
        int? carried = State.Level;
        State.Classes = w.Classes.ToList();
        if (ClassNames.IsPlayableRace(w.Race)) State.Race = w.Race;
        if (State.Level != w.Level)
        {
            ForgetLevelPosition();
            State.Level = w.Level;
            State.LevelPositionKnown = false;
        }
        TouchRoster(w.Level, w.Time);
        NoteWho(w);
        if (State.Current is { LevelEnd: null } c)
        {
            if (!c.TrioConfirmed)
            {
                c.Trio = State.Trio;
                c.Race = State.Race ?? c.Race;
                c.TrioConfirmed = true;
                c.TrioFrom = null;
                // The level carried in was another loadout's when this /who shows a different one.
                if (carried is { } cl && cl != w.Level) c.LevelStart = w.Level;
            }
            c.LevelStart ??= w.Level;
        }
    }

    /// <summary>
    /// Every active class and the race gain XP together, and the shown level is the lowest of them,
    /// so each active member is at least at this level.
    /// </summary>
    private void TouchRoster(int level, DateTime t)
    {
        if (State.Classes is not { Count: > 0 } classes) return;
        foreach (var c in classes) Touch(State.Roster.Class(ClassNames.Abbrev(c)), level, t);
        if (State.Race is { } race) Touch(State.Roster.Race(race), level, t);
    }

    private void Touch(Incarnation i, int level, DateTime t)
    {
        if (i.Level is not { } l || level > l) i.Level = level;
        if (i.Peak is not { } pk || level > pk) i.Peak = level;
        i.LastActive = t;
        if (i.Level is { } il && il >= Options.CapOn(t, il)) i.ReachedCap ??= t;
    }

    /// <summary>A level change that isn't the next level means a loadout swap: the trio we knew is gone.</summary>
    private void ForgetLoadout()
    {
        State.Classes = null;
        State.Race = null;
        if (State.Current is { TrioKnown: false } c) { c.Trio = null; c.Race = null; }
    }

    private void OnReincarnated(Reincarnated r)
    {
        bool race = r.Kind == ReincarnationKind.Race;
        var name = race ? r.Name : ClassNames.Abbrev(r.Name);
        var inc = race ? State.Roster.Race(name) : State.Roster.Class(name);
        inc.Seen.Add(r.Time);
        while (inc.SeenLevels.Count < inc.Seen.Count - 1) inc.SeenLevels.Add(Options.BaseCap);
        inc.SeenLevels.Add(Math.Max(inc.Peak ?? Options.CapOn(r.Time), Options.BaseCap));
        inc.Peak = null;
        if (r.Rank is { } stated) { inc.LoggedRank = stated; inc.LoggedAt = r.Time; }
        Roster.SetRank(inc, Options.EnteredCounts.GetValueOrDefault(Roster.Key(race, name)));
        inc.Reincarnated = r.Time;
        inc.Level = 1;
        inc.ReachedCap = null;
        if (r.Kind == ReincarnationKind.Race) State.Reincarnations++;
        // With the game's files naming the slot, you reincarnate inside it: the slot's level drops, its trio stays.
        bool slotted = State.Loadout != null;
        bool affectsActive = slotted || r.Kind == ReincarnationKind.Race ||
                             State.Classes == null ||
                             State.Classes.Contains(ClassNames.Abbrev(r.Name));
        if (affectsActive)
        {
            if (State.Current is { } open) FreezeLevels(open);
            ForgetLevelPosition();
            State.Level = 1;
            State.LevelPositionKnown = true;
        }
        // Reincarnating often coincides with a loadout swap, so treat the active trio as unknown until the next /who.
        if (State.Loadout is { } lo)
        {
            if (race) { if (ClassNames.IsPlayableRace(name)) State.Race = name; }
            else if (State.Classes is { } cl && !cl.Contains(name)) MarkClassesChanged(lo.Slot, r.Time);   // its classes changed unseen
            var m = Slot(lo.Slot);
            (m.Race, m.Level) = (State.Race, State.Level);
        }
        else ForgetLoadout();
        _sink.OnLevel(new LevelRecord(r.Time, r.Kind == ReincarnationKind.Race ? "reincarnate-race" : "reincarnate-class", 1, r.Name));
    }

    // ------------------------------------------------------------------ the loadout on (current-loadout)

    private SlotMemory Slot(int slot)
    {
        if (!State.Slots.TryGetValue(slot, out var m)) State.Slots[slot] = m = new SlotMemory();
        return m;
    }

    private static string StateOf(SlotMemory m) => m.ClassesChanged ? "classes" : m.Known ? "known" : "new";

    /// <summary>The slot on has the level the engine has: a ding or a level the log gave moves the slot's, not another's.</summary>
    private void SyncSlotLevel()
    {
        if (State.Loadout is { } lo) Slot(lo.Slot).Level = State.Level;
    }

    private void AddStint(DateTime t, int slot, string? trio) => State.Stints.Add(new LoadoutStint(t, slot, trio));

    /// <summary>
    /// The first swap the files show from 2 min before to 30 s after a login is held, not taken: the game writes a slot file
    /// only on swap, zone and camp, so the newest at launch can be the slot of the run before (login-swap). A /who
    /// or a later swap row confirms it (a zone writing another slot's file is a later swap row); a ding its slot's level rules out drops it.
    /// </summary>
    private void OnSwap(LoadoutSwapped sw)
    {
        if (State.PendingSwap is { } held)
        {
            if (held.Slot == sw.Slot) State.PendingSwap = null;   // the file written again: the row counts from its own time, not the held one's
            else ConfirmSwap();
        }
        bool atLogin = State.LoginAt is { } la && !State.SwappedSinceLogin && sw.Time >= la.AddMinutes(-2) && sw.Time <= la.AddSeconds(30);
        if (atLogin && State.Loadout?.Slot != sw.Slot) State.PendingSwap = sw;
        else ApplySwap(sw);
    }

    /// <summary>The held swap counts, from its own time.</summary>
    private void ConfirmSwap()
    {
        if (State.PendingSwap is not { } held) return;
        State.PendingSwap = null;
        ApplySwap(held);
    }

    /// <summary>The engine reads a login line: what was held for the login before is dropped.</summary>
    public void NoteLogin(DateTime t)
    {
        State.LoginAt = t;
        State.PendingSwap = null;
        State.SwappedSinceLogin = false;
    }

    /// <summary>
    /// The game wrote the loadout file of <paramref name="sw"/>.Slot: that slot is on, with what a /who once named it, or with
    /// nothing from the loadout left when none has. A /who read before this swap but typed after it (an app start reads the
    /// log first, and the files after) names the slot.
    /// </summary>
    private void ApplySwap(LoadoutSwapped sw)
    {
        State.SwappedSinceLogin = true;
        if (State.Loadout?.Slot == sw.Slot) return;
        if (State.Current is { Closed: false, LevelEnd: null } cur)
        {
            if (cur.TrioKnown) FreezeLevels(cur);
            else { cur.Trio = null; cur.Race = null; }
        }
        // A /who typed after this swap but read before it (inside the settle window) was written to the slot left: undo that.
        if (State.Loadout is { } left && Slot(left.Slot) is { WhoAt: { } at } lm && at >= sw.Time)
            (lm.Classes, lm.Race, lm.Level, lm.Known, lm.ClassesChanged, lm.WhoAt) = (null, null, null, false, false, null);
        var m = Slot(sw.Slot);
        // That /who, read before this swap, names the slot swapped to. Its level may be stale (a ding since), so the state's own
        // classes, race and level stay as they are.
        bool late = State.LastWho != null && State.LastWhoTime is { } wt && sw.Time <= wt;
        if (late)
            (m.Classes, m.Race, m.Level, m.Known, m.ClassesChanged, m.WhoAt) = (State.Classes?.ToList(), State.Race, State.Level, true, false, State.LastWhoTime);
        State.Classes = m.ClassesChanged ? null : m.Classes?.ToList();
        State.Race = m.Race;
        if (!late && State.Level != m.Level)
        {
            ForgetLevelPosition();
            State.Level = m.Level;
            State.LevelPositionKnown = false;
        }
        State.Loadout = new LoadoutOn(sw.Slot, StateOf(m));
        AddStint(sw.Time, sw.Slot, State.Trio);
    }

    private void OnClassesChanged(LoadoutClassesChanged cc)
    {
        if (State.Loadout is not { } lo || lo.State == "classes") return;
        if (State.Current is { Closed: false, LevelEnd: null } cur && cur.TrioKnown) FreezeLevels(cur);
        MarkClassesChanged(lo.Slot, cc.Time);
    }

    private void MarkClassesChanged(int slot, DateTime t)
    {
        var m = Slot(slot);
        (m.Classes, m.ClassesChanged) = (null, true);
        State.Classes = null;
        State.Loadout = new LoadoutOn(slot, "classes");
        AddStint(t, slot, null);
    }

    /// <summary>A /who names the slot on: it is met, and its stint has a trio.</summary>
    private void NoteWho(WhoSelf w)
    {
        State.LastWho = new SlotMemory { Classes = w.Classes.ToList(), Race = State.Race, Level = w.Level, Known = true };
        State.LastWhoTime = w.Time;
        if (State.Loadout is not { } lo) return;
        var m = Slot(lo.Slot);
        (m.Classes, m.Race, m.Level, m.Known, m.ClassesChanged, m.WhoAt) = (w.Classes.ToList(), State.Race, w.Level, true, false, w.Time);
        State.Loadout = new LoadoutOn(lo.Slot, "known");
        if (State.Stints.Count > 0 && State.Stints[^1] is { } last && last.Slot == lo.Slot && last.Trio == null)
            State.Stints[^1] = last with { Trio = State.Trio };
        else if (State.Stints.Count == 0 || State.Stints[^1].Trio != State.Trio)
            AddStint(w.Time, lo.Slot, State.Trio);
    }

    private void OnDied(YouDied d)
    {
        if (Inside is { } c) c.Deaths++;
        _sink.OnLevel(new LevelRecord(d.Time, "death", State.Level, d.Killer));
    }

    // ------------------------------------------------------------------ combat and loot

    private void OnSlain(MobSlain s)
    {
        double? secs = State.Engaged.Remove(s.Mob, out var first)
            ? Math.Max(1, (s.Time - first).TotalSeconds)
            : null;
        if (Inside is { } c0) c0.LastActivity = s.Time;
        if (Inside is { } raid && raid.BossKilled == null && IsRaidBoss(raid.BaseZone, s.Mob)) raid.BossKilled = s.Mob;

        // A kill is its slain line (kills-at-cap): yours or your pet's; another player's inside an instance (everyone in it is in
        // your group), and outside one only with party XP in its second. A player's death isn't a kill.
        bool inside = Inside != null;
        if (!s.ByYou && (!IsPlayerName(s.Killer) || IsPlayerName(s.Mob, false))) return;
        var c = Inside;
        if (c != null)
        {
            if (c.Completed && c.CompletedAt is { } done)
            {
                // Finishing off stragglers after the crawl completed: real fighting, so count the time too.
                var from = c.ActiveUntil ?? done;
                c.ActiveSeconds += Math.Max(0, (s.Time - from).TotalSeconds);
                c.ActiveUntil = s.Time;
            }
            c.Kills++;
            c.KillTimes.Add(s.Time);
            c.LastActivity = s.Time;
        }
        var slain = new PendingSlain
        {
            Time = s.Time,
            Mob = s.Mob,
            Seconds = secs,
            NeedsParty = !s.ByYou && !inside,
            VisitId = c?.Id,
            Zone = State.Zone,
            Difficulty = c?.Difficulty,
            Level = State.Level,
        };
        int i = State.PendingXp.FindIndex(x => Math.Abs((s.Time - x.Time).TotalSeconds) <= 1 && (!slain.NeedsParty || x.Party));
        if (i < 0) { State.PendingSlains.Add(slain); return; }
        var xp = State.PendingXp[i];
        State.PendingXp.RemoveAt(i);
        EmitKill(slain, xp);
    }

    /// <summary>
    /// Matches a player name (single capitalized word) or player pet ("Groupmate`s warder").
    /// Set <paramref name="orPet"/> false to match plain player names only.
    /// </summary>
    private static bool IsPlayerName(string? name, bool orPet = true)
    {
        if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0])) return false;
        int end = name.IndexOf(' ');
        if (end < 0) return name.All(char.IsLetter);
        var word = name[..end];
        return orPet && word.Length > 2 && word.EndsWith("`s", StringComparison.Ordinal) && word[..^2].All(char.IsLetter);
    }

    /// <summary>The bosses of the raid instances seen so far (docs/log-formats.md, "What kind of instance"); a raid's kill is the one the Crawls verdict names.</summary>
    private static readonly string[] RaidBosses = ["Lord Nagafen", "Innoruuk, the Prince of Hate", "Lady Vox", "Master Yael"];

    private static bool IsRaidBoss(string zone, string mob) =>
        (zone.EndsWith(" - Solo", StringComparison.Ordinal) || zone.EndsWith(" - Group", StringComparison.Ordinal))
        && RaidBosses.Any(b => string.Equals(b, mob, StringComparison.OrdinalIgnoreCase));

    private void OnDamage(DamageDealt d)
    {
        if (!State.Engaged.ContainsKey(d.Target))
        {
            if (State.Engaged.Count > 300)
            {
                var stale = d.Time.AddMinutes(-10);
                foreach (var k in State.Engaged.Where(kv => kv.Value < stale).Select(kv => kv.Key).ToList())
                    State.Engaged.Remove(k);
            }
            State.Engaged[d.Target] = d.Time;
        }

        var c = Inside;
        if (c == null) return;
        var dt = c.Damage;
        dt.Total += d.Amount;
        dt.Hits++;
        if (d.Critical) dt.Crits++;
        var src = d.Source.ToString();
        dt.BySource[src] = dt.BySource.GetValueOrDefault(src) + d.Amount;
        var ab = d.Ability ?? (d.Source == DamageSource.Pet ? "pet melee" : "melee");
        dt.ByAbility[ab] = dt.ByAbility.GetValueOrDefault(ab) + d.Amount;
        if (dt.LastAt is { } la && (d.Time - la).TotalSeconds <= Options.FightGapSeconds)
            dt.FightSeconds += Math.Max(0, (d.Time - la).TotalSeconds);
        else
            dt.FightSeconds += 1;
        dt.LastAt = d.Time;
        c.LastActivity = d.Time;
    }

    private void OnLoot(MoteLooted l)
    {
        State.Looted.Add(l.Kind, l.Tier, l.Count, l.FromChest);
        var c = Inside;
        c?.Loot.Add(l.Kind, l.Tier, l.Count, l.FromChest);
        if (c != null) c.LastActivity = l.Time;
        _sink.OnLoot(new LootRecord(l.Time, c?.Id, State.Zone, l.Kind, l.Tier, l.Count, l.Source, l.FromChest, false));
        if (MoteTiers.IsHighTier(l.Tier))
        {
            var drop = new HighTierDrop
            {
                Time = l.Time,
                Kind = l.Kind,
                Tier = l.Tier,
                Count = l.Count,
                Source = l.Source,
                FromChest = l.FromChest,
                VisitId = c?.Id,
                Zone = State.Zone,
            };
            c?.HighTier.Add(drop);
            _sink.OnHighTier(drop);
        }
    }
}
