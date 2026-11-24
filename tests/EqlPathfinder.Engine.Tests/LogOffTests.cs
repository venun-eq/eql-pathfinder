using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Live;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// "Your log is off" only when it's certain: the tracked character, on its server, is in the world and its log is quiet.
/// Lines and times are a real start on 2026-11-17: the client started 17:16:40, the server was picked 17:20:15 (3½
/// minutes of login), Venun entered 17:20:45, the eqlog's first line 17:20:54, the zone finished loading 17:21:02.
/// </summary>
public class LogOffTests
{
    private static readonly DateTime Start = new(2026, 11, 17, 17, 16, 40);
    private static readonly DateTime Entered = new(2026, 11, 17, 17, 20, 45);
    private static readonly DateTime Loaded = new(2026, 11, 17, 17, 21, 2);
    private static readonly DateTime FirstLine = new(2026, 11, 17, 17, 20, 54);
    private static readonly DateTime Yesterday = new(2026, 11, 16, 23, 2, 11);   // the eqlog's last line before this run

    private const string Starting = "[Tue Nov 17 17:16:40 2026]00000:Starting EverQuest (Built Nov 17 2026 09:39:03)";
    private const string Window = "[Tue Nov 17 17:16:44 2026]00001:EQ Window Width: 2560 Height: 1440 in windowed mode.";
    private const string Server = "[Tue Nov 17 17:20:16 2026]00039:WorldRPServer message: server name oggok";
    private const string CharSelect = "[Tue Nov 17 17:20:33 2026]00304:Starting char select.";
    private const string Sending = "[Tue Nov 17 17:20:44 2026]00318:Zone Connect -- 2 -- Sending MSG_EQ_ADDPLAYER";
    private const string Entry = "[Tue Nov 17 17:20:45 2026]00321:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = New Sebilis Expedition";
    private const string MainLoop = "[Tue Nov 17 17:20:58 2026]00403:Entering main loop.";
    private const string Complete = "[Tue Nov 17 17:21:02 2026]00420:Initialization complete.";
    private const string Zoned = "[Tue Nov 17 17:41:02 2026]00630:Received MSG_EQ_ADDPLAYER, Player = Venun, zone = The Estate of Unrest";
    private const string ZonedLoaded = "[Tue Nov 17 17:41:20 2026]00702:Initialization complete.";

    private static ClientSession InWorld(string player = "Venun", string? server = "oggok", DateTime? loaded = null) =>
        new(Start, [new WorldEntry(Entered, player, server, "New Sebilis Expedition", loaded ?? Loaded)]);

    private static bool Off(DateTime now, ClientSession? client, DateTime? lastLine, DateTime? started = null) =>
        LogOffCheck.IsOff(now, started ?? Start, client, "Venun", "oggok", lastLine);

    [Fact]
    public void ReadsTheWorldEntry()
    {
        var e = ClientDebugLog.ParseEntry(Entry);
        Assert.NotNull(e);
        Assert.Equal(Entered, e.At);
        Assert.Equal("Venun", e.Player);
        Assert.Equal("New Sebilis Expedition", e.Zone);
        Assert.Null(ClientDebugLog.ParseEntry(Sending));
        Assert.Null(ClientDebugLog.ParseEntry(CharSelect));
    }

    [Fact]
    public void TheTrackedCharacterInTheWorldWithAQuietLogIsOff()
    {
        Assert.True(Off(Loaded.AddMinutes(1.5), InWorld(), Yesterday));
        Assert.True(Off(Loaded.AddMinutes(1.5), InWorld(), null));   // a log with no lines at all
        Assert.True(Off(Loaded.AddMinutes(1.5), InWorld("venun", "Oggok"), Yesterday));
    }

    [Fact]
    public void ALogThatMovedIsOn() =>
        Assert.False(Off(Loaded.AddHours(1), InWorld(), FirstLine));

    [Fact]
    public void LoginAndCharacterSelectNeverAre()
    {
        // 3½ minutes in, still at the login screen (what the first version called "off"), and half an hour later.
        var atLogin = new ClientSession(Start, []);
        Assert.False(Off(Start.AddSeconds(215), atLogin, Yesterday));
        Assert.False(Off(Start.AddHours(1), atLogin, Yesterday));
    }

    [Fact]
    public void NotUntilTheZoneHasLoadedAndAMinuteHasGone()
    {
        Assert.False(Off(Entered.AddMinutes(10), InWorld() with { Entries = [InWorld().Entries[0] with { Loaded = null }] }, Yesterday));
        Assert.False(Off(Loaded.AddSeconds(30), InWorld(), Yesterday));
    }

    [Fact]
    public void AnotherCharacterOrServerNeverIs()
    {
        Assert.False(Off(Loaded.AddHours(1), InWorld(player: "Kibarer"), Yesterday));
        Assert.False(Off(Loaded.AddHours(1), InWorld(server: "halas"), Yesterday));
        Assert.False(Off(Loaded.AddHours(1), InWorld(server: null), Yesterday));   // dbg.txt didn't say which server
    }

    [Fact]
    public void WithoutThisRunsDebugLogItNeverIs()
    {
        Assert.False(Off(Start.AddHours(1), null, Yesterday));
        // Read in the moment before the new client rewrote dbg.txt: yesterday's arrival isn't this run's.
        var last = new ClientSession(Yesterday.AddHours(-3),
            [new WorldEntry(Yesterday.AddHours(-3).AddMinutes(1), "Venun", "oggok", "Oggok", Yesterday.AddHours(-3).AddMinutes(2))]);
        Assert.False(Off(Start.AddSeconds(5), last, Yesterday));
        // Windows' start time can be a moment after the whole second dbg.txt's first line shows.
        Assert.True(Off(Loaded.AddMinutes(1.5), InWorld(), Yesterday, started: Start.AddSeconds(0.8)));
    }

    [Fact]
    public void FollowsTheFileAsTheClientWritesIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dbg-{Guid.NewGuid():N}.txt");
        try
        {
            var log = new ClientDebugLog(path);
            Assert.Null(log.Read());   // no file yet

            File.WriteAllLines(path, [Starting, Window]);
            var s = log.Read();
            Assert.Equal(Start, s?.Started);
            Assert.Empty(s!.Entries);

            File.AppendAllLines(path, [Server, CharSelect, Sending, Entry, MainLoop]);
            var e = Assert.Single(log.Read()!.Entries);
            Assert.Equal((Entered, "Venun", "oggok", (DateTime?)null), (e.At, e.Player, e.Server, e.Loaded));

            File.AppendAllLines(path, [Complete, "Entering main loop."]);
            Assert.Equal(Loaded, log.Read()!.EntryOf("Venun", "oggok")?.Loaded);

            // Zoning writes another arrival; the first one stays.
            File.AppendAllLines(path, [Zoned, ZonedLoaded]);
            e = Assert.Single(log.Read()!.Entries);
            Assert.Equal((Entered, Loaded), (e.At, e.Loaded));

            // Camp, then another character, on another server.
            File.AppendAllLines(path, [
                "[Tue Nov 17 18:02:00 2026]00800:WorldRPServer message: server name halas",
                "[Tue Nov 17 18:02:30 2026]00870:Received MSG_EQ_ADDPLAYER, Player = Kibarer, zone = Halas",
                "[Tue Nov 17 18:02:41 2026]00960:Initialization complete."]);
            s = log.Read()!;
            Assert.Equal(2, s.Entries.Count);
            Assert.Null(s.EntryOf("Kibarer", "oggok"));
            Assert.Equal(new DateTime(2026, 11, 17, 18, 2, 41), s.EntryOf("Kibarer", "halas")?.Loaded);

            // The next start rewrites the file.
            const string next = "[Wed Nov 18 09:02:13 2026]00000:Starting EverQuest (Built Nov 17 2026 09:39:03)";
            File.WriteAllLines(path, [next]);
            s = log.Read()!;
            Assert.Equal(new DateTime(2026, 11, 18, 9, 2, 13), s.Started);
            Assert.Empty(s.Entries);

            // Even when the new file is already longer than what was read of the old one.
            File.WriteAllLines(path, [Starting, Server, Entry, Complete]);
            log.Read();
            File.WriteAllLines(path, [next, Window, CharSelect, Sending, Window, Window, Window, Window]);
            s = log.Read()!;
            Assert.Equal(new DateTime(2026, 11, 18, 9, 2, 13), s.Started);
            Assert.Empty(s.Entries);

            File.Delete(path);
            Assert.Null(log.Read());
        }
        finally { File.Delete(path); }
    }
}
