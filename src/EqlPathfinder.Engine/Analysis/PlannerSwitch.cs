using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// The part of settings.json that holds the plans (AppSettings implements it). Nothing else in
/// the app reads or writes a plan: every read and save goes through <see cref="PlannerSwitch"/>.
/// </summary>
public interface IPlannerSettings
{
    /// <summary>The old planner's plans: never read or shown now. They are counted (the card) and removed (its Got it).</summary>
    Dictionary<string, CampaignPlan> Campaigns { get; set; }
    /// <summary>
    /// Every character's plan. Kept under this name so older apps and settings.json keep working; a plan made in the beta is
    /// the player's plan. A null <see cref="NewCampaignPlan.Plan"/> is "no plan yet", never the old one.
    /// </summary>
    Dictionary<string, NewCampaignPlan> NewCampaigns { get; set; }
}

/// <summary>
/// A character's plan, with the carries kept beside it. A later field that is absent means "saved before that field existed".
/// </summary>
public sealed class NewCampaignPlan
{
    /// <summary>Null: no plan yet (Class roles saved the carries first): the empty plan, never an old one.</summary>
    public CampaignPlan? Plan { get; set; } = new();
    /// <summary>The player's carries, best first. Absent: Pathfinder's list; empty: none.</summary>
    public List<string>? Carries { get; set; }
    /// <summary>"One strong slow per trio". Absent: on.</summary>
    public bool? OneStrongSlow { get; set; }
    /// <summary>
    /// Race-goal list schema version. Absent on legacy plans whose race list ranked every goal;
    /// 1 = race goals are separate from priority ranking. Intentionally has no default initializer.
    /// </summary>
    public int? Lists { get; set; }
}

/// <summary>A character's carries, best first, and "One strong slow per trio", kept with its plan.</summary>
public sealed record CampaignRoles(IReadOnlyList<string> Carries, bool OneStrongSlow);

/// <summary>
/// Resolves which Campaign plan to read and write, plus legacy-plan migration/dismissal helpers.
/// </summary>
public static class PlannerSwitch
{
    private static bool Same(string a, string? b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);

    private static NewCampaignPlan? Entry(IPlannerSettings s, string? character) =>
        s.NewCampaigns.FirstOrDefault(kv => Same(kv.Key, character)).Value;

    /// <summary>One-time notice shown when a legacy plan's all-races list is cleared on load.</summary>
    public const string RaceListEmptiedLine = "Your race list was every race at 3/3: that's your goal now, and Pathfinder picks the order. Rank a race to put it first.";

    private static string[] FirstOf(CampaignPlan plan) =>
        [.. (plan.ClassOrder ?? []).Select(w => "class:" + w.Key).Concat((plan.RaceOrder ?? []).Select(w => "race:" + w.Key)).Take(CampaignPlan.MaxFirst)];

    /// <summary>
    /// Returns the normalized plan for an entry. For legacy plans without <see cref="NewCampaignPlan.Lists"/> whose
    /// race list contains all 16 races at 3/3 (none Unlock now), clears the ranking list while keeping the goals;
    /// the next save stamps <c>Lists</c>.
    /// </summary>
    private static (CampaignPlan? Plan, bool Emptied) Read(NewCampaignPlan? e)
    {
        var plan = e?.Plan?.Normalized();
        if (plan == null || e!.Lists != null) return (plan, false);
        var list = plan.RaceOrder;
        if (list is not { Count: 16 } || list.Any(w => w.Target != ClassNames.MaxPassiveRank || w.UnlockNow) || !ClassNames.Races.All(r => list.Any(w => w.Key == r)))
            return (plan, false);
        plan.RaceOrder = [];
        plan.First = [.. FirstOf(plan)];
        return (plan, true);
    }

    /// <summary>The plan Campaign shows: never written by a read.</summary>
    public static CampaignPlan PlanFor(IPlannerSettings s, string? character)
    {
        var plan = Read(Entry(s, character)).Plan ?? new CampaignPlan().Normalized();
        if (!plan.Ranked) SetLists(plan, null, null);   // saved before the lists
        return plan;
    }

    /// <summary>
    /// Puts the plan's ranked lists in order against the stored plan (Classes and Races you want first on top).
    /// A plan that arrives with its lists keeps them as sent and `First` is rewritten from them; only one with no lists takes this path (its `First` orders the stored lists).
    /// A rebuild runs this first, so it plans from the new lists, and Store runs it again (it changes nothing the second time).
    /// </summary>
    public static void WithLists(IPlannerSettings s, string character, CampaignPlan plan)
    {
        if (plan.Ranked)
        {
            // The lists as sent are the truth (whoever sent them owns the order); First is their projection for older apps.
            SetLists(plan, null, null, asIs: true);
            plan.First = [.. FirstOf(plan)];
            return;
        }
        var stored = Read(Entry(s, character)).Plan;
        if (stored is not { Ranked: true } || !plan.Normalized().First.SequenceEqual(stored.First))
            SetLists(plan, plan.ClassOrder ?? stored?.ClassOrder, plan.RaceOrder ?? stored?.RaceOrder);   // the first lane changed: it orders the lists
        else
        {
            plan.ClassOrder ??= stored.ClassOrder;
            plan.RaceOrder ??= stored.RaceOrder;
            SetLists(plan, null, null, asIs: true);
        }
    }

    /// <summary>Saves an edit to the plan. Every other field of the entry stays.</summary>
    public static void Store(IPlannerSettings s, string character, CampaignPlan plan)
    {
        WithLists(s, character, plan);
        var keep = Entry(s, character) ?? new NewCampaignPlan();
        foreach (var k in s.NewCampaigns.Keys.Where(k => Same(k, character)).ToList()) s.NewCampaigns.Remove(k);
        keep.Plan = plan;
        keep.Lists = 1;
        s.NewCampaigns[character] = keep;
    }

    /// <summary>
    /// Puts the plan's ranked lists in order from the first lane: its classes, then its races on top of <paramref name="raceRest"/> (the rest in
    /// their order, new goals at the bottom), Unlock now carried by key. With <paramref name="asIs"/> the lists stay as they are and are only cleaned.
    /// </summary>
    private static void SetLists(CampaignPlan plan, IEnumerable<CampaignWant>? classSource, IEnumerable<CampaignWant>? raceRest, bool asIs = false)
    {
        if (!asIs)
        {
            var first = plan.Normalized().First;
            bool Now(IEnumerable<CampaignWant>? src, string key) => (src ?? []).Any(w => w.UnlockNow && Same(w.Key, key));
            var rest = raceRest?.ToList() ?? [];
            plan.ClassOrder = first.Where(k => k.StartsWith("class:")).Select(k => k[6..])
                .Select(c => new CampaignWant { Key = c, UnlockNow = Now(classSource, c) }).ToList();
            plan.RaceOrder = first.Where(k => k.StartsWith("race:")).Select(k => k[5..])
                .Select(r => new CampaignWant { Key = r, UnlockNow = Now(rest, r) }).Concat(rest).ToList();
        }
        var n = plan.Normalized();
        plan.ClassOrder = n.ClassOrder;
        plan.RaceOrder = n.RaceOrder;
    }

    private static List<string> Clean(IEnumerable<string?> carries) =>
        carries.Select(c => string.IsNullOrWhiteSpace(c) ? null : ClassNames.Info(c)?.Abbrev).OfType<string>().Distinct().ToList();

    /// <summary>The carries (class or name, any case, real classes once) and the toggle kept with the plan; Pathfinder's list when none is stored.</summary>
    public static CampaignRoles RolesFor(IPlannerSettings s, string? character)
    {
        var e = Entry(s, character);
        return new(e?.Carries == null ? ClassRoles.DefaultCarries : Clean(e.Carries), e?.OneStrongSlow ?? true);
    }

    /// <summary>Saves the Class roles tab's carries and toggle with the plan. Pathfinder's list is stored as "none stored", so it follows a later change to it.</summary>
    public static bool StoreRoles(IPlannerSettings s, string character, CampaignRoles roles)
    {
        var e = Entry(s, character);
        if (e == null) s.NewCampaigns[character] = e = new NewCampaignPlan { Plan = null };
        var list = Clean(roles.Carries);
        e.Carries = list.SequenceEqual(ClassRoles.DefaultCarries) ? null : list;
        e.OneStrongSlow = roles.OneStrongSlow;
        return true;
    }

    /// <summary>
    /// Builds the plan again around the saved carries and saves it, but only if the carries are still the ones it was built for. When the
    /// stored plan changed meanwhile (a Campaign edit landed, built around the carries before this edit), it builds again from that plan, up to
    /// <paramref name="tries"/> times, so the carries and the plan end up agreeing. False: nothing saved (no plan, or a later carries edit,
    /// whose own rebuild lands instead).
    /// </summary>
    public static async Task<bool> RebuildAround(IPlannerSettings s, string character, Func<CampaignPlan, CampaignRoles, Task<CampaignPlan>> rebuild, int tries = 3)
    {
        for (int i = 0; i < tries; i++)
        {
            var roles = RolesFor(s, character);
            var before = PlanFor(s, character);
            if (before.Loadouts.Count == 0) return false;
            string beforeJson = System.Text.Json.JsonSerializer.Serialize(before);
            var plan = await rebuild(before, roles);
            var now = RolesFor(s, character);
            if (!now.Carries.SequenceEqual(roles.Carries) || now.OneStrongSlow != roles.OneStrongSlow) return false;
            if (System.Text.Json.JsonSerializer.Serialize(PlanFor(s, character)) != beforeJson) continue;
            Store(s, character, plan);
            return true;
        }
        return false;
    }

    /// <summary>Whether "Campaign has a new planner" shows: any character has an old plan stored (an empty one was still saved by the player).</summary>
    public static bool HasOldPlans(IPlannerSettings s) => s.Campaigns is { Count: > 0 };

    /// <summary>The card's Got it: every character's old plan goes, and nothing else in settings.json changes. It can't be undone.</summary>
    public static void RemoveOldPlans(IPlannerSettings s) => s.Campaigns = new();

    /// <summary>The player left Campaign after the one-time race list line: the emptied plan is stored as read, stamped, and the line never shows again. Changes nothing else.</summary>
    public static void RaceListEmptiedSeen(IPlannerSettings s, string character)
    {
        var e = Entry(s, character);
        if (e == null) return;
        var (plan, emptied) = Read(e);
        if (!emptied) return;
        e.Plan = plan;
        e.Lists = 1;
    }

    /// <summary>What the page needs, for everyone: the carries, whether the card shows and the one-time race list line.</summary>
    public static PlannerInput PageInput(IPlannerSettings s, string? character) =>
        new(RolesFor(s, character), HasOldPlans(s)) { RaceListEmptied = Read(Entry(s, character)).Emptied ? RaceListEmptiedLine : null };
}

/// <summary>What Campaign's page needs, read on the UI thread with the plan.</summary>
/// <param name="Roles">The carries and toggle.</param>
/// <param name="OldPlans">An old plan is stored somewhere: the page draws "Campaign has a new planner".</param>
public sealed record PlannerInput(CampaignRoles Roles, bool OldPlans)
{
    /// <summary>One-time notice shown when a legacy all-races list was cleared on load, or null.</summary>
    public string? RaceListEmptied { get; init; }
}

/// <summary>
/// Campaign's planner as the page draws it (<c>campaign.newPlanner</c>). The page only draws: the engine
/// says what each class kills with, whether the carries are Pathfinder's list, and whether the card shows.
/// </summary>
/// <param name="OldPlans">The card "Campaign has a new planner" shows (any old plan stored).</param>
/// <param name="Kills">What each class kills with, for the carries list, by class abbreviation.</param>
public sealed record NewPlannerView(bool OldPlans, IReadOnlyList<string> Carries,
    bool CarriesAreDefault, bool OneStrongSlow, SurvivalCheck Check, IReadOnlyList<int> Farmed, IReadOnlyDictionary<string, string> Kills,
    IReadOnlyList<ClassRole> Table)
{
    /// <summary>One-time notice shown when a legacy all-races list was cleared on load, or null.</summary>
    public string? RaceListEmptied { get; init; }

    public static NewPlannerView Of(PlannerInput p, SurvivalCheck check, IReadOnlyList<int> farmed)
    {
        var roles = p.Roles;
        return new(p.OldPlans, roles.Carries, roles.Carries.SequenceEqual(ClassRoles.DefaultCarries), roles.OneStrongSlow, check, farmed,
            ClassNames.All.ToDictionary(c => c.Abbrev, c => ClassRoles.Kills(c.Abbrev)), ClassRoles.Table)
        { RaceListEmptied = p.RaceListEmptied };
    }
}
