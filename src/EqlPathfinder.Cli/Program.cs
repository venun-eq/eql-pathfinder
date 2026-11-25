using System.Diagnostics;
using System.Globalization;
using EqlPathfinder.Engine;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Tracking;

// pathfinder <command> [options]
//   crawls  [--log <file>] [--from "2026-11-14 00:40"] [--to "..."] [--all] [--frag 1.6]
//   ledger  [--log <file>] [--from ...] [--all]          high-tier drops + mote bank
//   bench   [--all]                                      parse speed
//   unknown [--log <file>] [--from ...]                  lines that looked like ours but matched nothing

var CI = CultureInfo.InvariantCulture;
var argv = args.ToList();
string cmd = argv.Count > 0 && !argv[0].StartsWith("--") ? argv[0] : "crawls";
string? Opt(string name) { int i = argv.IndexOf(name); return i >= 0 && i + 1 < argv.Count ? argv[i + 1] : null; }
bool Flag(string name) => argv.Contains(name);

var opts = new TrackerOptions();
if (Opt("--frag") is { } fv) opts.FragmentValue = double.Parse(fv, CI);
DateTime? from = cmd != "upgrade" && Opt("--from") is { } f ? DateTime.Parse(f, CI) : null;
DateTime? to = Opt("--to") is { } tt ? DateTime.Parse(tt, CI) : null;

if (cmd == "fights")
{
    // pathfinder fights --db <file> [--log <file>] [--scope fight|place|session] [--by fighter|spell|mob|fight] [--mode damage|healing|taken]
    //   the fight meter as text: reads the logs into its own database (first run only), then prints one view of the last session.
    var dir = LogDiscovery.FindLogsDir() ?? throw new InvalidOperationException("Logs folder not found");
    var logs = Opt("--log") is { } one
        ? new CharacterLogs(LogDiscovery.FindCharacters(dir)[0].Name, LogDiscovery.FindCharacters(dir)[0].Server, one, [one])
        : LogDiscovery.FindCharacters(dir)[0];
    using var meter = new EqlPathfinder.Engine.Combat.FightMeterService(logs, Opt("--db") ?? throw new InvalidOperationException("pass --db <file>"));
    if (meter.NeedsImport)
    {
        var watch = Stopwatch.StartNew();
        meter.Import();
        Console.WriteLine($"read {logs.Files.Count} file(s) in {watch.Elapsed.TotalSeconds:N1} s: {meter.Store.Count(logs.Key):N0} fights");
    }
    if (Flag("--check"))
    {
        // The accuracy check: the crawl tracker totals your damage per visit by its own path (only inside the instance).
        // The meter's fights in the same visit must add up to the same number.
        var checkSink = new CollectSink();
        var crawl = new PathfinderEngine(logs.Name, new TrackerOptions(), checkSink);
        // --visit "2026-09-12 00:23": that visit's damage by target on both sides, to see where they part.
        DateTime? probe = Opt("--visit") is { } pv ? DateTime.Parse(pv, CI) : null;
        var byTarget = new Dictionary<string, long>();
        // What the tracker counts that isn't damage to a mob: hits on yourself, and a "pet" hitting a player. The second is
        // a mob you once charmed: its name stays a pet name, so every mob of that name hitting your group reads as your pet.
        var notMobs = new Dictionary<DateTime, long>();
        // The tracker keeps counting after a crawl completes, until you leave: compare up to its last counted hit.
        var lastHit = new Dictionary<DateTime, DateTime>();
        crawl.EventLine = (_, ev) =>
        {
            if (ev is not EqlPathfinder.Engine.Parsing.DamageDealt dd || crawl.Tracker.Inside is not { } inside) return;
            lastHit[inside.Start] = dd.Time;
            if (dd.Target == "yourself" || (dd.Source == EqlPathfinder.Engine.Parsing.DamageSource.Pet && EqlPathfinder.Engine.Combat.FightTracker.IsPlayerName(dd.Target)))
                notMobs[inside.Start] = notMobs.GetValueOrDefault(inside.Start) + dd.Amount;
            if (probe != null && Math.Abs((inside.Start - probe.Value).TotalMinutes) < 2)
                byTarget[dd.Target] = byTarget.GetValueOrDefault(dd.Target) + dd.Amount;
        };
        DateTime? endOfLast = null;
        foreach (var file in logs.Files.Count > 0 ? logs.Files : [logs.LivePath])
        {
            crawl.SkipThrough = endOfLast;
            crawl.ReadFile(file, 0);
            endOfLast = crawl.State.LastTime;
        }
        crawl.Flush();
        Console.WriteLine($"Charmed pets: {crawl.Parser.CharmDamageKept:N0} damage counted, {crawl.Parser.CharmDamageDropped:N0} by the same name on targets that were not the pet's.");
        if (probe != null && checkSink.Visits.FirstOrDefault(v => Math.Abs((v.Start - probe.Value).TotalMinutes) < 2) is { } pvisit)
        {
            Console.WriteLine($"  visit {pvisit.Start:MM-dd HH:mm:ss}–{pvisit.EndTime:HH:mm:ss} {pvisit.Zone}");
            if (Opt("--mob") is { } pm)
                foreach (var pf in meter.Store.Between(logs.Key, pvisit.Start.AddHours(-1), pvisit.EndTime.AddHours(1)).Where(x => x.Mob == pm))
                    Console.WriteLine($"  {pf.Start:HH:mm:ss}-{pf.End:HH:mm:ss} kills {pf.Kills} mine {pf.MyDamage,8:N0} all {pf.Damage,8:N0} zone {pf.Zone} D{pf.Difficulty}");
            var mine = meter.Store.Between(logs.Key, pvisit.Start, pvisit.EndTime).GroupBy(f => f.Mob).ToDictionary(g => g.Key, g => g.Sum(f => f.MyDamage));
            foreach (var (target, amount) in byTarget.OrderByDescending(kv => Math.Abs(kv.Value - mine.GetValueOrDefault(kv.Key))).Take(12))
                Console.WriteLine($"  {target,-32} tracker {amount,10:N0}  meter {mine.GetValueOrDefault(target),10:N0}");
            return;
        }
        int same = 0, checkedVisits = 0;
        long trackerTotal = 0, meterTotal = 0, notMobTotal = 0;
        var off = new List<(Visit V, long Tracker, long Meter)>();
        foreach (var v in checkSink.Visits.Where(v => v.Damage.Total > 0))
        {
            long m = meter.Store.Between(logs.Key, v.Start, lastHit.GetValueOrDefault(v.Start, v.EndTime)).Where(f => f.Zone == v.BaseZone && f.Difficulty == v.Difficulty).Sum(f => f.MyDamage);
            long t = v.Damage.Total - notMobs.GetValueOrDefault(v.Start);
            checkedVisits++;
            notMobTotal += notMobs.GetValueOrDefault(v.Start);
            trackerTotal += t;
            meterTotal += m;
            if (m == t) same++; else off.Add((v, t, m));
        }
        Console.WriteLine($"{checkedVisits:N0} visits with damage: {same:N0} match exactly, {off.Count:N0} differ. Tracker {trackerTotal:N0}, meter {meterTotal:N0} ({(double)meterTotal / Math.Max(1, trackerTotal):P3}).");
        Console.WriteLine($"Left out of the tracker's side: {notMobTotal:N0} that wasn't damage to a mob (hits on yourself; once-charmed mob names hitting players).");
        foreach (var (v, t, m) in off.OrderByDescending(x => Math.Abs(x.Meter - x.Tracker)).Take(int.Parse(Opt("--top") ?? "12", CI)))
            Console.WriteLine($"  {v.Start:MM-dd HH:mm}–{v.EndTime:HH:mm}  {v.BaseZone} D{v.Difficulty}  tracker {t,10:N0}  meter {m,10:N0}  {m - t,+9:N0} ({(double)(m - t) / Math.Max(1, t):P1})");
        return;
    }
    var ctx = meter.Store.LoadCheckpoint(logs.Key)!.Context;
    var session = meter.Store.Session(logs.Key, ctx.Session);
    if (Opt("--at") is { } at && meter.Store.SessionBefore(logs.Key, DateTime.Parse(at, CI)) is { } earlier)
    {
        // Look back: that session, in the place and with the trio of its last fight.
        session = meter.Store.Session(logs.Key, earlier).Where(x => x.Start <= DateTime.Parse(at, CI)).ToList();
        var lastFight = session[^1];
        ctx = new EqlPathfinder.Engine.Combat.FightContext
        {
            Session = earlier,
            Zone = lastFight.Zone,
            Difficulty = lastFight.Difficulty,
            Trio = lastFight.Trio,
            Stint = lastFight.Stint,
            InInstance = lastFight.Difficulty != null,
            LastLine = lastFight.End,
        };
    }
    var usual = EqlPathfinder.Engine.Combat.Usual.From(meter.Store.InPlace(logs.Key, ctx.Zone, ctx.Difficulty, ctx.Trio), ctx.LastLine ?? DateTime.Now);
    var frame = new EqlPathfinder.Engine.Combat.MeterFrame(logs.Name, ctx.Zone, ctx.Difficulty, ctx.InInstance, ctx.Trio, ctx.Session, ctx.Stint, session, [], usual);
    var view = EqlPathfinder.Engine.Combat.MeterViews.Build(frame,
        new EqlPathfinder.Engine.Combat.MeterQuery(Opt("--scope") ?? "session", Opt("--by") ?? "fighter", Opt("--mode") ?? "damage", Opt("--fighter"), Opt("--mob")));
    Console.WriteLine($"{view.Title} · {TimeSpan.FromSeconds(view.Seconds):m\\:ss} · {view.PerSecond:N0} {view.Unit}" +
        (view.UsualPct is { } up ? $" · {up:P0} of usual" : "") + $"   [{frame.Place}, {frame.Trio}, session {ctx.Session:MM-dd HH:mm}, by {view.By}]");
    foreach (var r in view.Rows.Take(int.Parse(Opt("--top") ?? "15", CI)))
        Console.WriteLine($"  {r.Label,-28} {(r.Sub ?? ""),-8} {r.Amount,9:N0} {r.PerSecond,7:N0}/s {r.Share,5:P0}" +
            (r.UsualPct is { } p ? $" {p,5:P0}" : "      ") + (r.Count > 0 ? $"  x{r.Count}" : "") + (r.Seconds > 0 ? $"  {TimeSpan.FromSeconds(r.Seconds):m\\:ss}" : "") +
            (r.Hits > 0 ? $"  hits {r.Hits} crit {r.Crits} max {r.Max}" : ""));
    Console.WriteLine(view.Footer);
    return;
}

if (cmd == "community")
{
    // pathfinder community [--out file.json] [--upload]
    //   the community job (every 30 minutes on the NAS): shared crawls from D1 -> community/v1.json (written with --out, uploaded to R2 with --upload).
    //   Needs CF_ACCOUNT_ID, CF_API_TOKEN (D1 Read + Workers R2 Storage Write) and EQLP_D1 (the database's ID). Exit code 1 on any failure.
    if (EqlPathfinder.Engine.Community.CommunityTarget.FromEnvironment() is not { } target)
    {
        Console.Error.WriteLine("community: set CF_ACCOUNT_ID, CF_API_TOKEN and EQLP_D1 (the D1 database's ID)");
        Environment.ExitCode = 1;
        return;
    }
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var job = new EqlPathfinder.Engine.Community.CommunityJob(http, target);
        var watch = Stopwatch.StartNew();
        var file = await job.BuildAsync(DateTime.Now);
        int cells = file.Places.Sum(p => p.Minutes.Count(m => m != null));
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(file, EqlPathfinder.Engine.Community.CommunityBuilder.Json);
        if (Opt("--out") is { } outPath) File.WriteAllBytes(outPath, json);
        if (Flag("--upload")) await job.UploadAsync(file);
        var r = file.Rules;
        Console.WriteLine($"community: {file.Players} players, {file.Crawls} crawls used, {file.LeftOut} left out; {file.Places.Count} places " +
            $"({cells} level cells), {file.Bands.Count} bands, {file.Difficulties.Count} difficulty comparisons; rules {r.MinPlayers} players, " +
            $"{r.MinCrawls} crawls, {r.MaxShare:P0} max share; {json.Length / 1024.0:N1} KB{(Flag("--upload") ? $", uploaded to {target.Key}" : "")} " +
            $"in {watch.Elapsed.TotalSeconds:N1} s; {job.RowsRead:N0} database rows read; {job.Stats}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"community: failed: {ex.Message}");
        Environment.ExitCode = 1;
    }
    return;
}

if (cmd == "upgrade")
{
    // pathfinder upgrade --tier 5 --goal 10 [--progress 0] [--spell] [--vtps 1]
    //   [--motes "Major=400,Greater=10"] [--frags "Grand=2,Ascendant=1"]      the Upgrades page's planner, as text
    int[] Parse(string? s)
    {
        var a = new int[11];
        foreach (var part in (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=');
            a[(int)Enum.Parse<MoteTier>(kv[0].Trim(), true)] = int.Parse(kv[1], CI);
        }
        return a;
    }
    var req = new EqlPathfinder.Engine.Upgrades.UpgradeRequest(Flag("--spell") ? UpgradeTarget.Spell : UpgradeTarget.Gear,
        int.Parse(Opt("--tier") ?? "0", CI), long.Parse(Opt("--progress") ?? "0", CI), int.Parse(Opt("--goal") ?? "10", CI),
        Parse(Opt("--motes")), Parse(Opt("--frags")), int.Parse(Opt("--vtps") ?? "0", CI));
    var plan = EqlPathfinder.Engine.Upgrades.UpgradePlanner.Plan(req);
    Console.WriteLine($"+{plan.FromTier} ({plan.Progress} in) to +{plan.Goal}: {plan.PointsNeeded} points, {plan.VtpsUsed} VTP");
    foreach (var c in plan.Condense) Console.WriteLine($"  condense {c.Offered} {c.Kind} of {c.From} -> {c.Offered / 2} {c.From + 1}");
    foreach (var s in plan.Steps)
        Console.WriteLine($"  +{s.StartTier} {s.StartProgress}/{1L << s.StartTier}: {s.Count} x {s.Kind} of {s.Tier} ({s.PointsEach} each) -> +{s.EndTier} {s.EndProgress}/{1L << s.EndTier}");
    Console.WriteLine($"cost {plan.CostT5:N1} T5-eq, farm {plan.FarmT5:N1}, left over {plan.Overshoot} points; by VTPs: {string.Join(" / ", plan.CostByVtps.Select(c => c.ToString("N0", CI)))}");
    return;
}

if (cmd == "share-smoke")
{
    // pathfinder share-smoke [--api https://...]   end-to-end check of the community API with a throwaway key:
    // config, register, upload two units (and the same two again), what the server holds, link code, then delete it all.
    using var key = EqlPathfinder.Engine.Sharing.ShareProtocol.NewKey();
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var api = new Uri(Opt("--api") ?? EqlPathfinder.Engine.Sharing.ShareProtocol.DefaultApi);
    var client = new EqlPathfinder.Engine.Sharing.ShareClient(http, api, key);
    var config = await client.ConfigAsync();
    Console.WriteLine($"config: enrollment {(config.RequireEnrollment ? "required" : "open")}, min app {config.MinApp}, community {config.Community}");
    // --enroll <code>: while sharing is invite-only, a code from someone who shares (Settings > Sharing > Code for a friend).
    var reg = await client.RegisterAsync("0.8.0-smoke", enroll: Opt("--enroll"));
    Console.WriteLine($"registered install {reg.Install} as player {reg.Player}");
    var again = await client.RegisterAsync("0.8.0-smoke");
    Console.WriteLine($"registering again: existing={again.Existing}");
    var payload = new EqlPathfinder.Engine.Sharing.SharePayload(2, "0.8.0-smoke", EngineState.CurrentVersion, "smoketest", "0123456789ab", null,
        EqlPathfinder.Engine.Sharing.ShareBuilder.Columns, [], [],
        [new("0123456789abcdef0001", "2026-11-16", 1, "class", "BER"), new("0123456789abcdef0002", "2026-11-16", 2, "race", "Ogre")], []);
    var units = EqlPathfinder.Engine.Sharing.ShareUnits.From(payload);
    foreach (var b in EqlPathfinder.Engine.Sharing.ShareUnits.Batches(payload, units))
    {
        var up = await client.UploadAsync(b);
        var up2 = await client.UploadAsync(b);
        Console.WriteLine($"upload: accepted {up.Accepted}, ignored {up.Ignored}; same again: accepted {up2.Accepted}");
    }
    var me = await client.MeAsync();
    Console.WriteLine($"me: {string.Join(", ", me.Counts.Select(kv => $"{kv.Key} {kv.Value}"))}; PCs {me.Pcs}; last upload {me.LastUpload}");
    var link = await client.LinkAsync(Convert.ToBase64String(new byte[16]));
    Console.WriteLine($"link code {link.Code}, expires {DateTimeOffset.FromUnixTimeSeconds(link.Expires).ToLocalTime():HH:mm:ss}");
    using (var key2 = EqlPathfinder.Engine.Sharing.ShareProtocol.NewKey())
    {
        var second = new EqlPathfinder.Engine.Sharing.ShareClient(http, api, key2);
        var joined = await second.RegisterAsync("0.8.0-smoke", link: link.Code);
        Console.WriteLine($"second PC joined player {joined.Player} (same: {joined.Player == reg.Player}), salt back: {joined.Salt != null}");
        using var key3 = EqlPathfinder.Engine.Sharing.ShareProtocol.NewKey();
        try
        {
            await new EqlPathfinder.Engine.Sharing.ShareClient(http, api, key3).RegisterAsync("0.8.0-smoke", link: link.Code);
            Console.WriteLine("PROBLEM: a used link code worked twice");
        }
        catch (EqlPathfinder.Engine.Sharing.ShareApiException ex) { Console.WriteLine($"code reused by a third key: {ex.Status} {ex.Code}"); }
    }
    try
    {
        using var stranger = EqlPathfinder.Engine.Sharing.ShareProtocol.NewKey();
        await new EqlPathfinder.Engine.Sharing.ShareClient(http, api, stranger).MeAsync();
    }
    catch (EqlPathfinder.Engine.Sharing.ShareApiException ex) { Console.WriteLine($"unregistered key: {ex.Status} {ex.Code}"); }
    if (Flag("--rules"))
    {
        // The fake-player brakes (docs/community-job.md). With config max_new_players_per_ip = 1, a second new player from this
        // connection today is refused; and this player uploading a second character on the same game server is refused.
        var invite = await client.EnrollAsync();
        using var keyB = EqlPathfinder.Engine.Sharing.ShareProtocol.NewKey();
        var b = new EqlPathfinder.Engine.Sharing.ShareClient(http, api, keyB);
        try
        {
            var rb = await b.RegisterAsync("0.8.0-smoke", enroll: invite.Code);
            Console.WriteLine($"second new player from this connection: registered {rb.Player} (the per-connection limit is off or above 1)");
            await b.DeleteEverythingAsync();
        }
        catch (EqlPathfinder.Engine.Sharing.ShareApiException ex) { Console.WriteLine($"second new player from this connection: {ex.Status} {ex.Code}"); }
        var other = payload with { Character = "fedcba987654" };
        other = other with
        {
            Reincarnations = [new("0123456789abcdef0003", "2026-11-16", 1, "class", "BER"), new("0123456789abcdef0004", "2026-11-16", 2, "race", "Ogre")],
        };
        try
        {
            foreach (var bt in EqlPathfinder.Engine.Sharing.ShareUnits.Batches(other, EqlPathfinder.Engine.Sharing.ShareUnits.From(other)))
                await client.UploadAsync(bt);
            Console.WriteLine("PROBLEM: a second character on the same game server was accepted");
        }
        catch (EqlPathfinder.Engine.Sharing.ShareApiException ex) { Console.WriteLine($"second character on the same server: {ex.Status} {ex.Code} (retry after {ex.RetryAfter} s)"); }
        // The first character still goes through.
        foreach (var bt in EqlPathfinder.Engine.Sharing.ShareUnits.Batches(payload, units))
            Console.WriteLine($"first character again: accepted {(await client.UploadAsync(bt)).Accepted}");
    }
    var gone = await client.DeleteEverythingAsync();
    Console.WriteLine($"deleted {gone.Deleted} rows and {gone.Pcs} PC(s)");
    try { await client.MeAsync(); }
    catch (EqlPathfinder.Engine.Sharing.ShareApiException ex) { Console.WriteLine($"after delete: {ex.Status} {ex.Code}"); }
    return;
}

if (cmd == "scrub")
{
    // pathfinder scrub --log <file> --from <t> --to <t> --out <file>   (the character becomes "Standin"; the clock starts at 12:00:00)
    var src = Opt("--log") ?? throw new ArgumentException("--log required");
    var outPath = Opt("--out") ?? throw new ArgumentException("--out required");
    var who = Path.GetFileName(src).Split('_')[1];
    long off = from is { } ff ? LogReader.FindOffset(src, ff.AddMinutes(-5)) : 0;
    var lines = new List<string>();
    LogReader.ReadFrom(src, off, l =>
    {
        if (!EqlPathfinder.Engine.Parsing.LogLine.TryParseTime(l, out var lt)) return;
        if ((from == null || lt >= from) && (to == null || lt <= to)) lines.Add(l);
    });
    using var w = new StreamWriter(outPath);
    int kept = LogScrubber.Scrub(who, lines, w);
    Console.WriteLine($"kept {kept:N0} of {lines.Count:N0} lines -> {outPath}");
    return;
}

if (cmd is "import" or "status")
{
    // pathfinder import --db <file>   full history import through the live service, then a snapshot
    // pathfinder status --db <file>   resume from the checkpoint, catch up, print a snapshot
    //   either takes --dump-history <file>: the History rows as JSON, for the numbers check
    var dbPath = Opt("--db") ?? Path.Combine(Path.GetTempPath(), "pathfinder-test.db");
    var dir = LogDiscovery.FindLogsDir() ?? throw new InvalidOperationException("Logs folder not found");
    var who = LogDiscovery.FindCharacters(dir)[0];
    using var store = new EqlPathfinder.Engine.Storage.PathfinderStore(dbPath);
    using var svc = new EqlPathfinder.Engine.Live.TrackerService(store, who, opts);
    var clock = Stopwatch.StartNew();
    if (cmd == "import" || !svc.HasHistory)
    {
        int lastPct = -10;
        await svc.ImportHistoryAsync(new Progress<double>(p => { if (p - lastPct >= 10) { lastPct = (int)p; Console.WriteLine($"  import {p:0}%"); } }));
        Console.WriteLine($"imported {who.Files.Count} file(s) for {who.Name} in {clock.Elapsed.TotalSeconds:N1} s");
    }
    clock.Restart();
    var done = new TaskCompletionSource<EqlPathfinder.Engine.Live.LiveSnapshot>();
    svc.SnapshotReady += snap => done.TrySetResult(snap);
    svc.Start();
    var s = await done.Task;
    Console.WriteLine($"caught up in {clock.Elapsed.TotalSeconds:N1} s. {s.Character} level {s.Level} {s.Trio} ({s.Race}) in {s.Zone}; log {s.LogTime}");
    if (s.Current is { } c)
        Console.WriteLine($"current: {c.BaseZone} D{c.Difficulty} {c.State}: {c.Minutes:0.0} min, {c.LevelsGained:0.00} levels ({c.LevelsPerHour:0.0}/h), {c.T5:0.0} T5 ({c.T5PerHour:0}/h), {c.Kills} kills, {c.Dps:0} DPS");
    Console.WriteLine($"session: {s.Session.Visits} visit(s) {s.Session.Zone} D{s.Session.Difficulty}: {s.Session.T5PerHourCrawl:0} T5/h crawl, {s.Session.T5PerHourWall:0} with resets; {s.Session.LevelsPerHourCrawl:0.0} Lv/h{(s.Session.Early ? $" (early: {s.Session.CrawlMinutes:0.0} min in crawls, rates from {s.Session.RateFromKills} kills)" : "")}");
    Console.WriteLine($"looted since logs began: {s.LootedT5:N0} T5-eq ({s.LootedHighTierT5:N0} of it above T5); last above-T5 drop: {(s.HighTier.Count > 0 ? $"{s.HighTier[0].Kind} of {s.HighTier[0].Tier} from {s.HighTier[0].Source} at {s.HighTier[0].Time}" : "none")}");
    Console.WriteLine($"history rows: {svc.History(10000).Count} visits");
    if (Opt("--dump-history") is { } dumpTo)
    {
        // pathfinder import|status --db <copy> --dump-history <file> [--frag <value>]: every History row as the page gets
        // it, for the real-data numbers check (tools/analysis/numbers-check, the numbers-check skill). Use import after a
        // change to parsing: status resumes with the old parse.
        await File.WriteAllTextAsync(dumpTo, System.Text.Json.JsonSerializer.Serialize(
            await svc.Query(x => x.History(10000)), EqlPathfinder.Engine.Pages.PageJson.Options));
        Console.WriteLine($"history dumped to {dumpTo} at fragment value {svc.FragmentValue.ToString(CI)}: give the check's scripts the same value");
    }
    if (Flag("--hunt-check"))
    {
        // pathfinder status --db <copy> --hunt-check: compare shared open-world hunts retained with
        // borrowed same-day trio vs explicit app-supplied trio.
        var payload = await svc.Query(x => x.SharePayload("check", EqlPathfinder.Engine.Sharing.ShareBuilder.NewSalt()));
        var crawls = payload.Crawls;
        var old = payload.Stretches.Select(st => (Kept: EqlPathfinder.Engine.Community.CommunityBuilder.Hunt(st with { Trio = null, Rank = null }, crawls, out var w) != null, Why: w)).ToList();
        var now = payload.Stretches.Select(st => (Kept: EqlPathfinder.Engine.Community.CommunityBuilder.Hunt(st, crawls, out var w) != null, Why: w)).ToList();
        Console.WriteLine($"hunts: {payload.Stretches.Count}; kept the old way {old.Count(x => x.Kept)}, with the app's trio {now.Count(x => x.Kept)}");
        foreach (var (label, list) in new[] { ("old", old), ("new", now) })
            Console.WriteLine($"  left out ({label}): " + string.Join(", ", list.Where(x => !x.Kept).GroupBy(x => x.Why).Select(g => $"{g.Key} {g.Count()}")));
        var since = payload.Stretches.Where(st => string.CompareOrdinal(st.Day, "2026-11-10") > 0).ToList();
        Console.WriteLine($"  since the patch: {since.Count} hunts; old way {since.Count(st => EqlPathfinder.Engine.Community.CommunityBuilder.Hunt(st with { Trio = null, Rank = null }, crawls) != null)}, " +
            $"app's trio {since.Count(st => EqlPathfinder.Engine.Community.CommunityBuilder.Hunt(st, crawls) != null)}");
    }
    if (Flag("--share-test"))
    {
        // pathfinder status --db <copy> --share-test [--api url]: sends this history under a throwaway key, times each
        // batch against the Worker's CPU budget, checks what the server holds, then deletes all of it.
        var salt = EqlPathfinder.Engine.Sharing.ShareBuilder.NewSalt();
        clock.Restart();
        var payload = await svc.Query(x => x.SharePayload("0.8.0", salt));
        var units = EqlPathfinder.Engine.Sharing.ShareUnits.From(payload);
        Console.WriteLine($"payload: {payload.Crawls.Count} crawls/instances, {payload.Stretches.Count} stretches, {payload.Reincarnations.Count} reincarnations, " +
            $"{payload.Relevels.Count} relevels = {units.Count} units, {units.Sum(u => u.Facts.Length) / 1024:N0} KB of facts (base64), built in {clock.Elapsed.TotalSeconds:N1} s");
        using var key = EqlPathfinder.Engine.Sharing.ShareProtocol.NewKey();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var client = new EqlPathfinder.Engine.Sharing.ShareClient(http, new Uri(Opt("--api") ?? EqlPathfinder.Engine.Sharing.ShareProtocol.DefaultApi), key);
        await client.RegisterAsync("0.8.0");
        int accepted = 0, n = 0;
        var times = new List<double>();
        try
        {
            foreach (var b in EqlPathfinder.Engine.Sharing.ShareUnits.Batches(payload, units))
            {
                var size = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(b, EqlPathfinder.Engine.Sharing.ShareClient.Json).Length;
                var t = Stopwatch.StartNew();
                var r = await client.UploadAsync(b);
                times.Add(t.Elapsed.TotalMilliseconds);
                accepted += r.Accepted;
                if (++n <= 3 || n % 5 == 0) Console.WriteLine($"  batch {n}: {b.Rows.Count} units, {size / 1024:N0} KB, {t.Elapsed.TotalMilliseconds:N0} ms, accepted {r.Accepted}");
            }
            var me = await client.MeAsync();
            Console.WriteLine($"uploaded {accepted:N0} of {units.Count:N0} in {n} batches (median {times.Order().ElementAt(times.Count / 2):N0} ms, max {times.Max():N0} ms); " +
                $"server holds {string.Join(", ", me.Counts.Select(kv => $"{kv.Key} {kv.Value}"))}");
        }
        finally
        {
            var gone = await client.DeleteEverythingAsync();
            Console.WriteLine($"deleted {gone.Deleted:N0} rows from the server");
        }
    }
    return;
}

// Which files?
List<string> files;
string character;
if (Opt("--log") is { } logPath)
{
    files = [logPath];
    var name = Path.GetFileName(logPath);
    character = name.StartsWith("eqlog_") ? name.Split('_')[1] : "You";
}
else
{
    var dir = LogDiscovery.FindLogsDir() ?? throw new InvalidOperationException("EverQuest Legends Logs folder not found; pass --log");
    var chars = LogDiscovery.FindCharacters(dir);
    if (chars.Count == 0) throw new InvalidOperationException("No eqlog files in " + dir);
    var c = Opt("--char") is { } cn ? chars.First(x => x.Name.Equals(cn, StringComparison.OrdinalIgnoreCase)) : chars[0];
    character = c.Name;
    files = Flag("--all") ? c.Files.ToList() : [c.LivePath];
}

var sink = new CollectSink();
var engine = new PathfinderEngine(character, opts, sink);
var sw = Stopwatch.StartNew();
long bytes = 0;
DateTime? lastFileEnd = null;
foreach (var file in files)
{
    long start = 0;
    if (from is { } fr && files.Count == 1) start = LogReader.FindOffset(file, fr.AddHours(-1));
    engine.SkipThrough = lastFileEnd;
    long end = engine.ReadFile(file, start);
    bytes += end - start;
    lastFileEnd = engine.State.LastTime;
}
engine.Flush();
sw.Stop();

switch (cmd)
{
    case "bench":
        Console.WriteLine($"{files.Count} file(s), {bytes / 1e6:N0} MB, {engine.Lines:N0} lines, {engine.Events:N0} events in {sw.Elapsed.TotalSeconds:N1} s " +
                          $"({bytes / 1e6 / Math.Max(0.001, sw.Elapsed.TotalSeconds):N0} MB/s)");
        Console.WriteLine($"visits {sink.Visits.Count}, kills {sink.Kills.Count}, loot lines {sink.Loot.Count}, high-tier {sink.HighTier.Count}");
        break;
    case "unknown":
        foreach (var (shape, n) in engine.Parser.UnknownShapes.OrderByDescending(x => x.Value)) Console.WriteLine($"{n,6}  {shape}");
        break;
    case "ledger":
        PrintLedger();
        break;
    case "reinc":
        PrintReincarnations();
        break;
    case "mobs":
        var ks = sink.Kills.Where(k => InRange(k.Time)).ToList();
        Console.WriteLine($"{ks.Count} XP kills, {ks.Count(k => k.Mob != null)} with a mob name, {ks.Count(k => k.KillSeconds != null)} with a kill time");
        foreach (var g in ks.GroupBy(k => k.Mob ?? "(none)").OrderByDescending(g => g.Count()).Take(15))
            Console.WriteLine($"  {g.Count(),4}  {g.Average(k => k.Percent),6:0.00}%  ttk {g.Where(k => k.KillSeconds != null).Select(k => k.KillSeconds!.Value).DefaultIfEmpty(0).Average(),5:0}s  {g.Key}");
        break;
    default:
        PrintCrawls();
        break;
}

bool InRange(DateTime t) => (from == null || t >= from) && (to == null || t <= to);

void PrintCrawls()
{
    var visits = sink.Visits.Where(v => InRange(v.Start)).ToList();
    if (engine.State.Current is { } cur && InRange(cur.Start)) visits.Add(cur);
    Console.WriteLine($"{"Start",-11} {"Trio",-11} {"Zone",-26} {"D",1} {"Lvl",-5} {"Min",5} {"Idle",4} {"Rst",4} {"Levels",-8} {"Lv/h",4} {"T5",5} {"Chest",5} {"T5/h",5} {"Hi",3} {"Kills",5} {"K/m",4} {"DPS",5} {"fDPS",5} {"TTK",4} {"Done",4} {"Dth",3}");
    var now = engine.State.LastTime ?? DateTime.Now;
    foreach (var v in visits)
    {
        double min = v.CrawlSeconds(now) / 60;
        double idle = v.IdleSeconds(opts.IdleThresholdSeconds, now) / 60;
        double activeH = Math.Max(1e-6, (min - idle) / 60);
        double t5 = v.Loot.T5(opts.FragmentValue);
        string flag = v.RewardSizing ? "+R?" : v.Rewards.Any(r => r.Estimated) ? "~" : v.Rewards.Any(r => r.Unknown) ? "?" : "";
        string lvl = $"{v.LevelStart}-{v.LevelEnd ?? engine.State.Level}";
        double secs = Math.Max(1, v.CrawlSeconds(now));
        Console.WriteLine(
            $"{v.Start:MM-dd HH:mm} {Trunc(v.Trio ?? "?", 11),-11} {Trunc(v.BaseZone.Replace("The ", ""), 26),-26} {v.Difficulty?.ToString() ?? "-",1} {lvl,-5} " +
            $"{min,5:0.0} {idle,4:0.0} {(v.ResetMinutes is { } r ? r.ToString("0.0", CI) : ""),4} " +
            $"{(v.KillPercent / 100).ToString("0.00", CI) + "+" + (v.RewardPercent / 100).ToString("0.00", CI) + flag,-8} " +
            $"{v.LevelsGained / activeH,4:0.0} {t5,5:0.0} {v.Loot.ChestT5(opts.FragmentValue),5:0.0} {t5 / activeH,5:0} " +
            $"{v.HighTier.Sum(h => h.Count),3} {v.Kills,5} {v.Kills / Math.Max(0.1, min - idle),4:0.0} " +
            $"{v.Damage.Total / secs,5:0} {(v.Damage.FightSeconds > 0 ? v.Damage.Total / v.Damage.FightSeconds : 0),5:0} " +
            $"{(v.MedianKillSeconds is { } k ? k.ToString("0", CI) : ""),4} {(v.Completed ? "Y" : v.Closed ? "" : "open"),4} {v.Deaths,3}");
    }
    Console.WriteLine();
    Console.WriteLine($"Parsed {engine.Lines:N0} lines in {sw.Elapsed.TotalSeconds:N1} s. Min = time inside the instance until the crawl completed; Lv/h and T5/h use Min - Idle.");
    Console.WriteLine($"Levels = kill XP + crawl reward XP (+R? = reward sized at the next level, ~ = estimate). T5 values fragments at {opts.FragmentValue}x.");
}

void PrintLedger()
{
    var drops = sink.HighTier.Where(d => InRange(d.Time)).ToList();
    Console.WriteLine($"Drops above T5 ({drops.Count}): what you looted, before any condensing.");
    foreach (var d in drops)
        Console.WriteLine($"  {d.Time:MM-dd HH:mm}  {(d.Kind == LootKind.Fragment ? "FRAGMENT" : "mote    ")} {d.Count}x {d.Tier,-10} = {d.BaseT5 * (d.Kind == LootKind.Fragment ? opts.FragmentValue : 1),6:0.#} T5  from {d.Source}  ({d.Zone})");
    var s = engine.State;
    Console.WriteLine();
    Console.WriteLine($"Looted since tracking began: {s.Looted.T5(opts.FragmentValue):N1} T5-eq ({s.Looted.MoteCount} motes, {s.Looted.FragmentCount} fragments)");
    Console.WriteLine($"Offered at Construct Potential: {s.Offered.T5(opts.FragmentValue):N1} T5-eq.  VTP received: {s.VoidTouchedReceived}");
}

void PrintCampaign(EngineState st, IReadOnlyList<EqlPathfinder.Engine.Analysis.Journey> journeys)
{
    // The Plan → Campaign page's two ready-made goals, from the ranks your log shows.
    var cr = ClassNames.All.ToDictionary(c => c.Abbrev, c => st.Roster.Classes.GetValueOrDefault(c.Abbrev)?.Rank ?? 0);
    var rr = ClassNames.Races.ToDictionary(r => r, r => st.Roster.Races.GetValueOrDefault(r)?.Rank ?? 0);
    foreach (var (key, name) in new[] { ("all", "EVERYTHING 3/3"), ("classes", "EVERY CLASS 3/3") })
    {
        var plan = EqlPathfinder.Engine.Analysis.Campaign.Preset(key);
        plan.Loadouts = EqlPathfinder.Engine.Analysis.Campaign.Generate(plan, cr, rr, null, EqlPathfinder.Engine.Analysis.Campaign.Quality(plan.Prefer));
        var v = EqlPathfinder.Engine.Analysis.Campaign.Evaluate(plan, cr, rr, []);
        int carry = plan.Loadouts.Count(l => l.Classes.Any(ClassNames.Carries.ContainsKey)), heals = plan.Loadouts.Count(l => l.Classes.Any(ClassNames.IsHealer));
        Console.WriteLine($"\nCAMPAIGN: {name} from your ranks now: {v.Left} runs, {v.Runs.Sum(r => r.Xp):0.0} fresh relevels of XP, the last needs +{(v.LastXp - 1) * 100:0}% XP to level; " +
                          $"a carry in {carry} of {plan.Loadouts.Count} loadouts, heals in {heals}");
        for (int i = 0; i < plan.Loadouts.Count; i++)
        {
            var l = plan.Loadouts[i];
            var runs = v.Runs.Where(r => r.Loadout == i).ToList();
            var threes = runs.SelectMany(r => r.Gains).Where(g => g.Rank == 3).Select(g => g.Kind == "completionist" ? "Completionist" : g.Key);
            Console.WriteLine($"  {i + 1,2}. {l.Race,-14} {string.Join("/", l.Classes),-12} x{l.Times}  runs {runs.First().N}-{runs.Last().N}, " +
                              $"+{(runs.Min(r => r.Xp) - 1) * 100:0}% to +{(runs.Max(r => r.Xp) - 1) * 100:0}% XP to level  3/3: {string.Join(" ", threes)}");
        }
    }
}

void PrintReincarnations()
{
    var st = engine.State;
    var all = sink.Visits.ToList();
    if (st.Current != null) all.Add(st.Current);
    Console.WriteLine("ROSTER (rank, level)");
    Console.WriteLine("  " + string.Join("  ", ClassNames.All.Select(c => { var i = st.Roster.Classes.GetValueOrDefault(c.Abbrev); return $"{c.Abbrev} r{i?.Rank ?? 0} L{i?.Level?.ToString() ?? "?"}"; })));
    Console.WriteLine("  " + string.Join("  ", st.Roster.Races.Values.Where(r => r.Rank > 0).Select(r => $"{r.Name} r{r.Rank} L{r.Level?.ToString() ?? "?"}")));
    var journeys = EqlPathfinder.Engine.Analysis.Journeys.Build(all, st, opts);
    Console.WriteLine("\nJOURNEYS");
    foreach (var j in journeys)
        Console.WriteLine($"  {j.Trio,-12} {j.Race,-14} {j.Start:MM-dd HH:mm} L{j.LevelFrom}->{j.LevelNow} {(j.Done ? "done" : "leveling")} active {j.ActiveHours:0.0} h, wall {j.WallHours:0.0} h, {j.Crawls} crawls, {j.T5:0} T5, {j.Deaths} deaths" +
                          (j.EtaHours is { } e ? $"  ETA {e:0.0} h ({j.EtaNote})" : "") +
                          $"\n      route: {string.Join(" > ", j.Route.Select(l => $"{l.Label} {l.LevelFrom}-{l.LevelTo} ({l.LevelsPerHour:0.0}/h)"))}");
    Console.WriteLine("\nADVICE");
    foreach (var a in EqlPathfinder.Engine.Analysis.Planner.Advise(st, journeys, opts)) Console.WriteLine($"  [{a.Kind}] {a.Title}\n      {a.Detail}");
    Console.WriteLine("\nNEXT TRIO IDEAS");
    foreach (var i in EqlPathfinder.Engine.Analysis.Planner.Ideas(st, journeys))
        Console.WriteLine($"  {string.Join("/", i.Classes),-12} score {i.Score,4:0.0}  +{(i.XpMultiplier - 1) * 100:0}% XP to level  + {string.Join("; ", i.Pros)}  - {string.Join("; ", i.Cons)}" + (i.SimilarFrom != null ? $"  (like {i.SimilarFrom}: {i.SimilarHours:0.0} h)" : ""));
    PrintCampaign(st, journeys);
    Console.WriteLine("\nROUTE OPTIONS BY BAND (since reincarnation began, by cost per level)");
    var era = EqlPathfinder.Engine.Analysis.RouteAnalysis.EraStart(st);
    var farmRate = EqlPathfinder.Engine.Analysis.RouteAnalysis.EffectiveFarmRate(EqlPathfinder.Engine.Analysis.RouteAnalysis.FarmRates(all, opts), opts);
    foreach (var b in EqlPathfinder.Engine.Analysis.RouteAnalysis.Bands(opts.CapOn(DateTime.Now)))
    {
        var os = EqlPathfinder.Engine.Analysis.RouteAnalysis.LevelingOptions(all, b.Low, b.High, opts, era, farmRate).Take(4).ToList();
        Console.WriteLine($"  {b.Label,-6} " + string.Join(" | ", os.Select(x => $"{x.Label} n{x.Crawls} {x.LevelsPerHour:0.0}Lv/h {x.T5PerHour:0}T5/h cost {x.Cost:0.0}")));
    }
}

static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];

sealed class CollectSink : ITrackerSink
{
    public List<Visit> Visits { get; } = new();
    public List<KillRecord> Kills { get; } = new();
    public List<LootRecord> Loot { get; } = new();
    public List<HighTierDrop> HighTier { get; } = new();
    public void OnVisitClosed(Visit visit) => Visits.Add(visit);
    public void OnKill(KillRecord kill) => Kills.Add(kill);
    public void OnLoot(LootRecord loot) => Loot.Add(loot);
    public void OnHighTier(HighTierDrop drop) => HighTier.Add(drop);
}
