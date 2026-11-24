namespace EqlPathfinder.Engine.Sharing;

/// <summary>
/// Which data folder a sharing key was made in (settings' ShareHome). A developer's isolated copy keeps only a key it made
/// itself (the app's AppSettings.ForgetCopiedSharing).
/// </summary>
public static class ShareKeyHome
{
    /// <summary>
    /// True when the sharing key belongs to <paramref name="dataDir"/>. A legacy key with no recorded home
    /// belongs to the primary install, never to an isolated copy.
    /// </summary>
    public static bool MadeIn(string? home, string dataDir) =>
        !string.IsNullOrWhiteSpace(home) && !string.IsNullOrWhiteSpace(dataDir)
        && string.Equals(Normalize(home), Normalize(dataDir), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd('\\', '/');
}
