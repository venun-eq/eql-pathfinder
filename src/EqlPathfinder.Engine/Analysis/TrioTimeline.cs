namespace EqlPathfinder.Engine.Analysis;

/// <summary>
/// Which trio was playing when, as the app knows it (<see cref="PlayFacts"/>: /who, or carried and checked against the
/// spells cast): from each visit's start, its trio, until the next change. Immutable, so another thread (the fight
/// meter) can read it while the tracker builds the next one.
/// </summary>
public sealed class TrioTimeline
{
    private readonly DateTime[] _from;
    private readonly string?[] _trio;

    /// <param name="points">
    /// When a visit started or the loadout changed, and the trio then, oldest first; repeats of the same trio are merged. A null trio
    /// is "not known" (a loadout no /who has named): from there the fight meter judges nothing (current-loadout).
    /// </param>
    public TrioTimeline(IEnumerable<(DateTime From, string? Trio)> points)
    {
        var from = new List<DateTime>();
        var trio = new List<string?>();
        foreach (var (f, t) in points.OrderBy(p => p.From))
        {
            if (trio.Count > 0 && trio[^1] == t) continue;
            from.Add(f);
            trio.Add(t);
        }
        _from = from.ToArray();
        _trio = trio.ToArray();
    }

    public int Count => _from.Length;

    /// <summary>The trio playing at <paramref name="time"/> and since when; null before the first visit.</summary>
    public (DateTime From, string? Trio)? At(DateTime time)
    {
        int i = Array.BinarySearch(_from, time);
        if (i < 0) i = ~i - 1;
        return i >= 0 ? (_from[i], _trio[i]) : null;
    }

    /// <summary>Each stretch with one trio: from, until (the next change; <see cref="DateTime.MaxValue"/> for the last), trio.</summary>
    public IEnumerable<(DateTime From, DateTime Until, string? Trio)> Spans() =>
        _from.Select((f, i) => (f, i + 1 < _from.Length ? _from[i + 1] : DateTime.MaxValue, _trio[i]));
}
