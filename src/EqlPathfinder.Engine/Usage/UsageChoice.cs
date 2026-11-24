namespace EqlPathfinder.Engine.Usage;

/// <summary>Where a copy stands on usage counts when it starts (docs/specs/usage-choice.md).</summary>
public enum UsageStart { Ask, On, Off }

/// <summary>What the tour's Usage step shows: which answer is chosen (null = neither) and whether No's line is the deleting one.</summary>
public readonly record struct UsageStep(bool? Selected, bool NoDeletes);

/// <summary>Reads the player's answer about usage counts from what settings.json keeps.</summary>
public static class UsageChoice
{
    /// <param name="chosen">Whether the player has answered: null when settings.json was written before the question
    /// existed (a missing value counts as answered), false on a new copy until they answer.</param>
    /// <param name="enabled">settings.json's UsageEnabled.</param>
    public static UsageStart Read(bool? chosen, bool enabled) =>
        chosen == false ? UsageStart.Ask : enabled ? UsageStart.On : UsageStart.Off;

    /// <summary>The Usage step's state: the chosen card is the saved setting (none on Ask), and No deletes from the server
    /// only when counts are on and this copy has an id (the same fact as the DELETE).</summary>
    public static UsageStep Step(UsageStart start, bool hasId) => new(
        start switch { UsageStart.On => true, UsageStart.Off => false, _ => null },
        start == UsageStart.On && hasId);
}
