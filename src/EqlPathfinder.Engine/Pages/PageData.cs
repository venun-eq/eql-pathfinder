using System.Text.Json;
using System.Text.Json.Serialization;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Live;
using EqlPathfinder.Engine.Model;

namespace EqlPathfinder.Engine.Pages;

/// <summary>
/// Shared JSON serializer options for the window/page contract so production and tests serialize identically.
/// </summary>
public static class PageJson
{
    /// <summary>
    /// What every message to a page is written with, and what a page posts is read with: camelCase names (read in any
    /// case), an enum by its name, NaN and Infinity as words (a rate over no time doesn't stop the whole message).
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>
    /// What a page's question is read with: numbers even when a saved question kept them as text. Declared after
    /// <see cref="Options"/>, which it copies: static fields are filled top to bottom.
    /// </summary>
    public static readonly JsonSerializerOptions Query = new(Options) { NumberHandling = JsonNumberHandling.AllowReadingFromString };
}

/// <summary>
/// Settings payload sent to pages (<c>data.settings</c>). App-supplied properties are <c>required</c> so missing fields
/// fail at compile time; read-only properties are engine-owned. Keep in sync with PageDataTests.
/// </summary>
public sealed record PageSettings
{
    private static readonly IReadOnlyList<PageFragmentChoice> Choices =
        [.. Potential.FragmentValueChoices.Select(c => new PageFragmentChoice(c.Goal, c.Value))];

    public required double FragmentValue { get; init; }
    public required bool FragmentRecommended { get; init; }
    public double FragmentRecommendedValue => Potential.RecommendedFragmentValue;
    public required bool Sound { get; init; }
    public IReadOnlyList<PageFragmentChoice> FragmentChoices => Choices;
    public required IReadOnlyList<PageOverlay> Overlays { get; init; }
    public required bool OverlaysOnlyWithGame { get; init; }
    public required bool OverlaysLocked { get; init; }
    public required bool Arranging { get; init; }
    public required bool ShareIntroSeen { get; init; }
    public required double FarmRate { get; init; }
    public required string FarmRateMode { get; init; }
    public required string Goal { get; init; }
    /// <summary>The difficulties you farm for motes (Settings → How you play); every other visit is judged as leveling.</summary>
    public required IReadOnlyList<int> FarmDifficulties { get; init; }
    public required string StartWindow { get; init; }
    public required string? Version { get; init; }
    public required string? PendingUpdate { get; init; }
    public required bool UpdatesEnabled { get; init; }
    public required string? LogsDir { get; init; }
    public required string? Character { get; init; }
    public required bool GameRunning { get; init; }
    public required bool Beta { get; init; }
    public required bool OnBetaBuild { get; init; }
    public required bool ShareEnabled { get; init; }
    public required bool Usage { get; init; }
    public required IReadOnlyList<string> NoticesSeen { get; init; }
    /// <summary>
    /// The last Campaign edit the window has finished with (saved, dropped or failed), by the number the page sent it
    /// with: the page stops showing its unsaved copy at this number, whether or not the plan changed. 0 before any.
    /// </summary>
    public required int CampaignAnswered { get; init; }
}

/// <summary>One of Settings' fragment values: how high mote-fed gear goes, and the value that makes.</summary>
public sealed record PageFragmentChoice(int Goal, double Value);

/// <summary>An overlay's row on the Settings page: what it is, and the player's choices for it.</summary>
public sealed record PageOverlay
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string About { get; init; }
    public required bool Visible { get; init; }
    public required bool Locked { get; init; }
    public required double Opacity { get; init; }
    public required IReadOnlyDictionary<string, string> Options { get; init; }
}

/// <summary>The page data: the one message without a <c>kind</c>, which every page draws from.</summary>
public static class PageData
{
    /// <summary>
    /// The message as an object, for <see cref="PageJson.Options"/> to write. Call it on the tracker's turn (inside
    /// <see cref="TrackerService.Query{T}"/>): it reads the engine's state. Write the JSON off it, as the window does.
    /// PageDataTests lists every field: a new one is added there too.
    /// </summary>
    /// <param name="snapshot">The overlay's last snapshot, or null before the first.</param>
    /// <param name="campaign">The player's Plan → Campaign.</param>
    /// <param name="settings">Read by the app on its own thread, before the query.</param>
    /// <param name="planner">The new planner's switch, carries and card, in the beta only (<see cref="PlannerSwitch.PageInput"/>).</param>
    public static object Build(TrackerService s, LiveSnapshot? snapshot, CampaignPlan? campaign, PageSettings settings, PlannerInput? planner = null)
    {
        var history = s.History(5000);
        return new
        {
            character = s.Logs.Name,
            server = s.Logs.Server,
            fragmentValue = s.FragmentValue,
            generated = DateTime.Now,
            snapshot,
            history,
            drops = s.NotableDrops(5000),
            ledger = s.LootLedger(),
            reinc = s.ReincarnationData(campaign, history, planner),
            settings,
        };
    }
}
