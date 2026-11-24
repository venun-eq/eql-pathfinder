using System.Text.Json;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Live;

public sealed partial class TrackerService
{
    private (string Key, List<CampaignPreset> Presets)? _presetCache;
    private (string Key, List<CampaignChange> Fixes)? _fixCache;
    private (string Key, CampaignFirstLane Lane)? _laneCache;
    private (string Key, (IReadOnlyList<CampaignComboRow>? Rows, int? Floor, bool? Cost, string? Why, string? CostWhat) Combos)? _comboCache;

    /// <summary>The race ranks now (zero for a race with none), for ordering a plan's race goals when it comes across.</summary>
    public IReadOnlyDictionary<string, int> RaceRanks() => Ranks().Races;

    private (Dictionary<string, int> Classes, Dictionary<string, int> Races) Ranks()
    {
        var r = _engine.State.Roster;
        return (ClassNames.All.ToDictionary(c => c.Abbrev, c => r.Classes.GetValueOrDefault(c.Abbrev)?.Rank ?? 0),
            ClassNames.Races.ToDictionary(x => x, x => r.Races.GetValueOrDefault(x)?.Rank ?? 0));
    }

    /// <summary>
    /// What breaks ties between plans that cost the same, and what a cache of it is keyed on: the player's carries and the
    /// survival check, where they level.
    /// </summary>
    private (Func<IReadOnlyList<string>, double> Quality, string Key, IReadOnlyCollection<string> Carries) TieBreak(
        IReadOnlyList<Journey> journeys, CampaignRoles roles)
    {
        var check = SurvivalCheck.Of(journeys, _options.FarmDifficulties);
        return (Campaign.RolesQuality(roles, check.Checks), Campaign.TieKey(roles, check), roles.Carries);
    }

    /// <summary>What a ready-made goal's count keeps from the plan: its combos and, with runs, its pinned runs and, once the log has reincarnations, every run (which of them you played).</summary>
    private static string PinKey(CampaignPlan plan, IReadOnlyList<ReincarnationEvent> events)
    {
        // The click keeps the plan's combos and first-lane picks, runs or not.
        var held = "|combos:" + string.Join(";", (plan.Combos ?? []).Select(c => $"{string.Join(",", c.Classes)}{(c.Must ? "m" : "")}{(c.Apart ? "a" : "")}{(c.EveryRun ? "e" : "")}{c.Times}{(c.UnlockNow ? "u" : "")}")) + "|first:" + string.Join(",", plan.First);
        if (plan.Loadouts.Count == 0 || !(events.Count > 0 || plan.Loadouts.Any(l => l.Pinned))) return held;
        var ls = events.Count > 0 ? plan.Loadouts : plan.Loadouts.Where(l => l.Pinned);
        return held + "|runs:" + events.Count + ":" + plan.Since?.Ticks + ":" + string.Join(";", ls.Select(l => $"{l.Race}/{string.Join(",", l.Classes)}x{l.Times}{(l.Pinned ? "p" : "")}"));
    }

    /// <summary>Plan → Campaign: your plan played out from your ranks now, with what the ready-made goals would take.</summary>
    public CampaignView CampaignData(CampaignPlan plan, List<Journey> journeys, CampaignRoles roles)
    {
        plan = plan.Normalized();
        var (cr, rr) = Ranks();
        var (quality, tie, carries) = TieBreak(journeys, roles);
        var events = Campaign.Events(_engine.State.Roster);
        var relevels = Relevels.Of(_engine.State.Roster, journeys, _options.LevelCap);
        // The presets only change with your ranks and what you prefer between equal loadouts: build them again only then.
        string key = string.Join(",", cr.Values) + "|" + string.Join(",", rr.Values) + tie + "|" + string.Join(",", plan.Locked) + "|" + string.Join(",", (plan.ClassOrder ?? []).Select(w => w.Key + (w.UnlockNow ? "!" : ""))) + "|" + string.Join(",", (plan.RaceOrder ?? []).Select(w => w.Key)) + PinKey(plan, events);
        if (_presetCache?.Key != key) _presetCache = (key, Campaign.Presets(cr, rr, quality, plan, events, relevels, plan));
        var v = Campaign.Evaluate(plan, cr, rr, events, relevels, _presetCache.Value.Presets);
        // Your combos: each row's price and state, and the runs a rebuild with your Musts gives. Like the fixes, they only change with the plan, your ranks and the log.
        string fixKey = JsonSerializer.Serialize(plan) + "|" + key + "|" + events.Count;
        if (plan.Combos is { Count: > 0 })
        {
            if (_comboCache?.Key != fixKey) _comboCache = (fixKey, Campaign.ComboRows(plan, v, cr, rr, events, quality, relevels));
            v = v with
            {
                ComboRows = _comboCache.Value.Combos.Rows,
                ComboFloor = _comboCache.Value.Combos.Floor,
                CombosCost = _comboCache.Value.Combos.Cost,
                ComboWhy = _comboCache.Value.Combos.Why,
                ComboCostWhat = _comboCache.Value.Combos.CostWhat,
            };
        }
        // What you want first: each ranked row's line, with the same fuse as the combos' (a rebuild per row, only when the plan, your ranks or the log change).
        if (plan.Ranked)
        {
            if (_laneCache?.Key != fixKey) _laneCache = (fixKey, Campaign.FirstLane(plan, v, cr, rr, events, quality, relevels));
            v = v with { FirstLane = _laneCache.Value.Lane };
        }
        if (v.Goals.All(g => g.AtEnd >= g.Target) && v.Left <= (v.ComboFloor ?? v.Floor.Runs)) return v;
        // Missing a goal, or runs to spare: the cheapest fixes. They only change with the plan, your ranks and the log's reincarnations.
        if (_fixCache?.Key != fixKey) _fixCache = (fixKey, Campaign.Fixes(plan, cr, rr, events, quality, carries));
        return v with { Fixes = _fixCache.Value.Fixes };
    }

    /// <summary>Your combos → the form's "What it does": a combo's price and clash against the plan as the page has it. <paramref name="index"/>: the combo being changed, left out first.</summary>
    public CampaignComboPrice ComboPrice(CampaignPlan plan, CampaignCombo combo, int? index, CampaignRoles roles)
    {
        plan = plan.Normalized();
        var (cr, rr) = Ranks();
        var journeys = Journeys();
        var (quality, _, _) = TieBreak(journeys, roles);
        var relevels = Relevels.Of(_engine.State.Roster, journeys, _options.LevelCap);
        return Campaign.ComboPrice(plan, combo, cr, rr, Campaign.Events(_engine.State.Roster), quality, index, relevels);
    }

    /// <summary>Campaign → Swap… and the picker: the cheapest changes for one loadout, and every race and class for it.</summary>
    public CampaignOptions? CampaignOptions(CampaignPlan plan, int index, CampaignRoles roles)
    {
        plan = plan.Normalized();
        var (cr, rr) = Ranks();
        var (quality, _, carries) = TieBreak(Journeys(), roles);
        return Campaign.Options(plan, cr, rr, Campaign.Events(_engine.State.Roster), quality, index, carries);
    }

    /// <summary>
    /// Builds the plan's loadouts again from your ranks now, keeping pinned loadouts and the runs already done. A new plan
    /// starts counting the log's reincarnations from now.
    /// </summary>
    public CampaignPlan RebuildCampaign(CampaignPlan plan, CampaignRoles roles)
    {
        plan = plan.Normalized();
        var (cr, rr) = Ranks();
        var (done, off) = Campaign.DoneCounts(plan, Campaign.Events(_engine.State.Roster));
        var (quality, _, _) = TieBreak(Journeys(), roles);
        plan.Loadouts = Campaign.Generate(plan, cr, rr, done, quality, off);
        if (plan.Loadouts.Count > 0) plan.Since ??= _engine.State.LastTime ?? Now;
        return plan;
    }
}
