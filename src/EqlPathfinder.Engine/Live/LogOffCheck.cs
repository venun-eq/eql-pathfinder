using EqlPathfinder.Engine.IO;

namespace EqlPathfinder.Engine.Live;

/// <summary>
/// Is the player's log off? Only said when it's certain: dbg.txt shows the tracked character (same name, same server)
/// came into the world this run, the zone finished loading a minute ago, and their log hasn't had a line since they
/// arrived. Patching, login, server and character select, another character or another server: never.
/// </summary>
public static class LogOffCheck
{
    /// <summary>From the zone finishing loading. With the log on, "You have entered …" is in it before that.</summary>
    public static readonly TimeSpan AfterLoading = TimeSpan.FromMinutes(1);

    /// <summary>dbg.txt's first line is the client starting; the process start time Windows gives can be a little later.</summary>
    private static readonly TimeSpan SameRun = TimeSpan.FromMinutes(1);

    /// <param name="now">Now, in the game's local time.</param>
    /// <param name="gameStarted">When the running eqgame.exe started (or Pathfinder first saw it).</param>
    /// <param name="client">What dbg.txt says, or null without one.</param>
    /// <param name="player">The tracked character's name.</param>
    /// <param name="server">The tracked character's server.</param>
    /// <param name="lastLine">The tracked log's newest line.</param>
    public static bool IsOff(DateTime now, DateTime gameStarted, ClientSession? client, string player, string server, DateTime? lastLine) =>
        client is { } c && c.Started >= gameStarted - SameRun &&         // dbg.txt is this run's, not the last one's
        c.EntryOf(player, server) is { Loaded: { } loaded } e &&          // the tracked character is in the world
        !(lastLine >= e.At) &&                                            // and their log has said nothing since
        now - loaded > AfterLoading;
}
