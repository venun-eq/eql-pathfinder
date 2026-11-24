using System.Text.Json;
using EqlPathfinder.Engine.Tracking;
using Microsoft.Data.Sqlite;

namespace EqlPathfinder.Engine.Storage;

/// <summary>Where each log file was read up to, plus the tracker state at that point.</summary>
public sealed class Checkpoint
{
    public Dictionary<string, long> Offsets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime? LastTime { get; set; }
    public EngineState State { get; set; } = new();
    public DateTime SavedAt { get; set; }
}

/// <summary>
/// Local SQLite store. Records and the checkpoint are written in one transaction, so a crash or
/// restart never loses or double-counts anything.
/// </summary>
public sealed class PathfinderStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private readonly SqliteConnection _db;

    private readonly WriterLock _writer;

    /// <exception cref="StoreInUseException">Another process has this database open.</exception>
    public PathfinderStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _writer = WriterLock.Take(path);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        _db.Open();
        Exec("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;");
        Exec("""
            CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS visits (
                character TEXT NOT NULL, id TEXT NOT NULL, start TEXT NOT NULL, zone TEXT NOT NULL,
                difficulty INTEGER, trio TEXT, json TEXT NOT NULL, PRIMARY KEY (character, id));
            CREATE TABLE IF NOT EXISTS kills (
                character TEXT NOT NULL, time TEXT NOT NULL, visit_id TEXT, zone TEXT, difficulty INTEGER,
                level INTEGER, mob TEXT, pct REAL NOT NULL, party INTEGER NOT NULL, secs REAL);
            CREATE INDEX IF NOT EXISTS kills_by_mob ON kills (character, zone, mob, level);
            CREATE TABLE IF NOT EXISTS loot (
                character TEXT NOT NULL, time TEXT NOT NULL, visit_id TEXT, zone TEXT, kind INTEGER NOT NULL,
                tier INTEGER NOT NULL, count INTEGER NOT NULL, source TEXT, chest INTEGER NOT NULL, spent INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS loot_by_time ON loot (character, time);
            CREATE TABLE IF NOT EXISTS levels (
                character TEXT NOT NULL, time TEXT NOT NULL, kind TEXT NOT NULL, level INTEGER, detail TEXT);
            -- current-loadout: each loadout swap and class change the game's files showed. Reset leaves it: a file keeps only its last write and dbg.txt is rewritten at each start, so this is the only copy.
            CREATE TABLE IF NOT EXISTS loadout_events (
                id INTEGER PRIMARY KEY, character TEXT NOT NULL, time TEXT NOT NULL, kind TEXT NOT NULL, slot INTEGER NOT NULL,
                UNIQUE (character, time, kind, slot));
            """);
        // kills-at-cap: a kill whose XP line had no (x%) has unknown XP, not 0. CREATE TABLE IF NOT EXISTS never widens a table
        // an older build made, so the column is added here (docs/code-map.md recipe 7). An older build's rows are 0 = known.
        bool hasXpUnknown = false;
        using (var info = Cmd("SELECT name FROM pragma_table_info('kills')"))
        using (var rd = info.ExecuteReader())
            while (rd.Read()) if (rd.GetString(0) == "xp_unknown") hasXpUnknown = true;
        // A revert of this release must keep this ALTER or at least the named column list in Commit's INSERT: players' kills tables have 11 columns after it.
        if (!hasXpUnknown) Exec("ALTER TABLE kills ADD COLUMN xp_unknown INTEGER NOT NULL DEFAULT 0;");
    }

    // ------------------------------------------------------------------ writes

    public sealed class Batch
    {
        internal readonly List<Visit> Visits = new();
        internal readonly List<KillRecord> Kills = new();
        internal readonly List<LootRecord> Loot = new();
        internal readonly List<LevelRecord> Levels = new();
        public int Count => Visits.Count + Kills.Count + Loot.Count + Levels.Count;
    }

    /// <summary>Writes a batch of records and (optionally) the checkpoint atomically.</summary>
    public void Commit(string character, Batch batch, Checkpoint? checkpoint)
    {
        using var tx = _db.BeginTransaction();
        if (batch.Visits.Count > 0)
        {
            using var cmd = Cmd("INSERT OR REPLACE INTO visits VALUES ($c,$id,$s,$z,$d,$t,$j)", tx);
            var p = Params(cmd, "$c", "$id", "$s", "$z", "$d", "$t", "$j");
            foreach (var v in batch.Visits)
            {
                Set(p, character, v.Id, Iso(v.Start), v.BaseZone, v.Difficulty, v.Trio, JsonSerializer.Serialize(v, Json));
                cmd.ExecuteNonQuery();
            }
        }
        if (batch.Kills.Count > 0)
        {
            // Keep the named column list: a build after a revert still writes to the 11-column table (xp_unknown has a default).
            using var cmd = Cmd("INSERT INTO kills (character, time, visit_id, zone, difficulty, level, mob, pct, party, secs, xp_unknown) VALUES ($c,$t,$v,$z,$d,$l,$m,$p,$party,$s,$u)", tx);
            var p = Params(cmd, "$c", "$t", "$v", "$z", "$d", "$l", "$m", "$p", "$party", "$s", "$u");
            foreach (var k in batch.Kills)
            {
                Set(p, character, Iso(k.Time), k.VisitId, k.Zone, k.Difficulty, k.Level, k.Mob, k.Percent, k.Party ? 1 : 0, k.KillSeconds, k.XpUnknown ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }
        if (batch.Loot.Count > 0)
        {
            using var cmd = Cmd("INSERT INTO loot VALUES ($c,$t,$v,$z,$k,$tier,$n,$src,$chest,$spent)", tx);
            var p = Params(cmd, "$c", "$t", "$v", "$z", "$k", "$tier", "$n", "$src", "$chest", "$spent");
            foreach (var l in batch.Loot)
            {
                Set(p, character, Iso(l.Time), l.VisitId, l.Zone, (int)l.Kind, (int)l.Tier, l.Count, l.Source, l.FromChest ? 1 : 0, l.Spent ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }
        if (batch.Levels.Count > 0)
        {
            using var cmd = Cmd("INSERT INTO levels VALUES ($c,$t,$k,$l,$d)", tx);
            var p = Params(cmd, "$c", "$t", "$k", "$l", "$d");
            foreach (var l in batch.Levels)
            {
                Set(p, character, Iso(l.Time), l.Kind, l.Level, l.Detail);
                cmd.ExecuteNonQuery();
            }
        }
        if (checkpoint != null)
        {
            checkpoint.SavedAt = DateTime.Now;
            SetMeta("checkpoint:" + character, JsonSerializer.Serialize(checkpoint, Json), tx);
        }
        tx.Commit();
        batch.Visits.Clear(); batch.Kills.Clear(); batch.Loot.Clear(); batch.Levels.Clear();
    }

    /// <summary>Forget everything for a character (before a full re-import).</summary>
    public void Reset(string character)
    {
        using var tx = _db.BeginTransaction();
        foreach (var table in new[] { "visits", "kills", "loot", "levels" })
        {
            using var cmd = Cmd($"DELETE FROM {table} WHERE character = $c", tx);
            cmd.Parameters.AddWithValue("$c", character);
            cmd.ExecuteNonQuery();
        }
        using (var cmd = Cmd("DELETE FROM meta WHERE key = $k", tx))
        {
            cmd.Parameters.AddWithValue("$k", "checkpoint:" + character);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>One loadout swap ("swap", to <paramref name="slot"/>) or class change ("classes", slot 0) the game's files showed, kept the moment it is seen. A repeat is ignored.</summary>
    public void AddLoadoutEvent(string character, DateTime time, string kind, int slot)
    {
        using var cmd = Cmd("INSERT OR IGNORE INTO loadout_events (character, time, kind, slot) VALUES ($c,$t,$k,$s)");
        cmd.Parameters.AddWithValue("$c", character);
        cmd.Parameters.AddWithValue("$t", Iso(time));
        cmd.Parameters.AddWithValue("$k", kind);
        cmd.Parameters.AddWithValue("$s", slot);
        cmd.ExecuteNonQuery();
    }

    /// <summary>The loadout events stored after row <paramref name="id"/>, oldest row first.</summary>
    public List<(long Id, DateTime Time, string Kind, int Slot)> LoadoutEventsAfter(string character, long id)
    {
        using var cmd = Cmd("SELECT id, time, kind, slot FROM loadout_events WHERE character = $c AND id > $i ORDER BY id");
        cmd.Parameters.AddWithValue("$c", character);
        cmd.Parameters.AddWithValue("$i", id);
        var list = new List<(long, DateTime, string, int)>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add((r.GetInt64(0), DateTime.Parse(r.GetString(1)), r.GetString(2), r.GetInt32(3)));
        return list;
    }

    // ------------------------------------------------------------------ reads

    public Checkpoint? LoadCheckpoint(string character)
    {
        var json = GetMeta("checkpoint:" + character);
        if (json == null) return null;
        try { return JsonSerializer.Deserialize<Checkpoint>(json, Json); }
        catch (JsonException) { return null; }
    }

    public List<Visit> RecentVisits(string character, int limit)
    {
        using var cmd = Cmd("SELECT json FROM visits WHERE character = $c ORDER BY start DESC LIMIT $n");
        cmd.Parameters.AddWithValue("$c", character);
        cmd.Parameters.AddWithValue("$n", limit);
        var list = new List<Visit>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            if (JsonSerializer.Deserialize<Visit>(r.GetString(0), Json) is { } v) list.Add(v);
        list.Reverse();
        return list;
    }

    public sealed record LootTotal(Model.MoteTier Tier, Model.LootKind Kind, long Count, long FromChests);

    /// <summary>Everything looted since the logs began, by tier and kind. Offers at Construct Potential aren't subtracted.</summary>
    public (List<LootTotal> Totals, DateTime? Since) LootTotals(string character)
    {
        using var cmd = Cmd("""
            SELECT tier, kind, SUM(count), SUM(CASE WHEN chest = 1 THEN count ELSE 0 END), MIN(time) FROM loot
            WHERE character = $c AND spent = 0 GROUP BY tier, kind ORDER BY tier, kind
            """);
        cmd.Parameters.AddWithValue("$c", character);
        var list = new List<LootTotal>();
        DateTime? since = null;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new LootTotal((Model.MoteTier)r.GetInt32(0), (Model.LootKind)r.GetInt32(1), r.GetInt64(2), r.GetInt64(3)));
            var first = DateTime.Parse(r.GetString(4));
            if (since == null || first < since) since = first;
        }
        return (list, since);
    }

    /// <summary>One mote or fragment pickup. Place is "Zone D4" for an instance visit, else the zone the log named.</summary>
    public sealed record LootRow(DateTime Time, Model.LootKind Kind, Model.MoteTier Tier, int Count, bool Chest, string? Source,
        string? Place, bool InInstance);

    /// <summary>Every mote and fragment looted, newest first (offers at Construct Potential excluded).</summary>
    public List<LootRow> AllLoot(string character)
    {
        using var cmd = Cmd("""
            SELECT l.time, l.kind, l.tier, l.count, l.chest, l.source, l.zone, v.zone, v.difficulty FROM loot l
            LEFT JOIN visits v ON v.character = l.character AND v.id = l.visit_id
            WHERE l.character = $c AND l.spent = 0 ORDER BY l.time DESC
            """);
        cmd.Parameters.AddWithValue("$c", character);
        static string Short(string z) => z.StartsWith("The ") ? z[4..] : z;
        var list = new List<LootRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            bool inInstance = !r.IsDBNull(7);
            string? place = inInstance ? Short(r.GetString(7)) + (r.IsDBNull(8) ? "" : $" D{r.GetInt32(8)}")
                          : r.IsDBNull(6) ? null : Short(r.GetString(6));
            list.Add(new LootRow(DateTime.Parse(r.GetString(0)), (Model.LootKind)r.GetInt32(1), (Model.MoteTier)r.GetInt32(2),
                r.GetInt32(3), r.GetInt32(4) == 1, r.IsDBNull(5) ? null : r.GetString(5), place, inInstance));
        }
        return list;
    }

    /// <summary>Every pickup and every offer at Constructed Potential after a moment, oldest first (for the mote bank).</summary>
    public List<Upgrades.BankEvent> BankEventsAfter(string character, DateTime after)
    {
        using var cmd = Cmd("SELECT time, kind, tier, count, spent FROM loot WHERE character = $c AND time > $t ORDER BY time");
        cmd.Parameters.AddWithValue("$c", character);
        cmd.Parameters.AddWithValue("$t", Iso(after));
        var list = new List<Upgrades.BankEvent>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Upgrades.BankEvent(DateTime.Parse(r.GetString(0)), (Model.LootKind)r.GetInt32(1), (Model.MoteTier)r.GetInt32(2),
                r.GetInt32(3), r.GetInt32(4) == 1));
        return list;
    }

    /// <summary>Kills and party kills per instance visit: a crawl of mostly party kills was played in a group.</summary>
    public Dictionary<string, (int Kills, int Party)> PartyKillsByVisit(string character)
    {
        using var cmd = Cmd("SELECT visit_id, COUNT(*), SUM(party) FROM kills WHERE character = $c AND visit_id IS NOT NULL GROUP BY visit_id");
        cmd.Parameters.AddWithValue("$c", character);
        var result = new Dictionary<string, (int, int)>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) result[r.GetString(0)] = (r.GetInt32(1), r.IsDBNull(2) ? 0 : r.GetInt32(2));
        return result;
    }

    /// <summary>Every kill since <paramref name="since"/>, in and out of instances, oldest first.</summary>
    public List<KillRecord> KillsSince(string character, DateTime since)
    {
        using var cmd = Cmd("SELECT time, visit_id, zone, difficulty, level, mob, pct, party, secs, xp_unknown FROM kills WHERE character = $c AND time >= $t ORDER BY time");
        cmd.Parameters.AddWithValue("$c", character);
        cmd.Parameters.AddWithValue("$t", Iso(since));
        var list = new List<KillRecord>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new KillRecord(DateTime.Parse(r.GetString(0)), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetInt32(3), r.IsDBNull(4) ? null : r.GetInt32(4), r.IsDBNull(5) ? null : r.GetString(5),
                r.GetDouble(6), r.GetInt32(7) == 1, r.IsDBNull(8) ? null : r.GetDouble(8), r.GetInt32(9) == 1));
        return list;
    }

    /// <summary>Dings, deaths and reincarnations since <paramref name="since"/>, oldest first.</summary>
    public List<LevelRecord> LevelEventsSince(string character, DateTime since)
    {
        using var cmd = Cmd("SELECT time, kind, level, detail FROM levels WHERE character = $c AND time >= $t ORDER BY time");
        cmd.Parameters.AddWithValue("$c", character);
        cmd.Parameters.AddWithValue("$t", Iso(since));
        var list = new List<LevelRecord>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new LevelRecord(DateTime.Parse(r.GetString(0)), r.GetString(1), r.IsDBNull(2) ? null : r.GetInt32(2), r.IsDBNull(3) ? null : r.GetString(3)));
        return list;
    }

    /// <summary>Kills outside any instance, oldest first: open-world hunting.</summary>
    public List<KillRecord> OpenWorldKills(string character)
    {
        using var cmd = Cmd("SELECT time, zone, difficulty, level, mob, pct, party, secs, xp_unknown FROM kills WHERE character = $c AND visit_id IS NULL ORDER BY time");
        cmd.Parameters.AddWithValue("$c", character);
        var list = new List<KillRecord>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new KillRecord(DateTime.Parse(r.GetString(0)), null, r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetInt32(2),
                r.IsDBNull(3) ? null : r.GetInt32(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetDouble(5), r.GetInt32(6) == 1, r.IsDBNull(7) ? null : r.GetDouble(7), r.GetInt32(8) == 1));
        return list;
    }

    /// <summary>Drops above T5 (Greater and up).</summary>
    public List<HighTierDrop> HighTierDrops(string character, int limit) => Drops(character, limit, "tier >= 6");

    /// <summary>Drops worth listing one by one: everything above T5, and every fragment.</summary>
    public List<HighTierDrop> NotableDrops(string character, int limit) => Drops(character, limit, "(tier >= 6 OR kind = 1)");

    private List<HighTierDrop> Drops(string character, int limit, string where)
    {
        using var cmd = Cmd($"""
            SELECT time, kind, tier, count, source, chest, visit_id, zone FROM loot
            WHERE character = $c AND spent = 0 AND {where} ORDER BY time DESC LIMIT $n
            """);
        cmd.Parameters.AddWithValue("$c", character);
        cmd.Parameters.AddWithValue("$n", limit);
        var list = new List<HighTierDrop>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new HighTierDrop
            {
                Time = DateTime.Parse(r.GetString(0)),
                Kind = (Model.LootKind)r.GetInt32(1),
                Tier = (Model.MoteTier)r.GetInt32(2),
                Count = r.GetInt32(3),
                Source = r.IsDBNull(4) ? "" : r.GetString(4),
                FromChest = r.GetInt32(5) == 1,
                VisitId = r.IsDBNull(6) ? null : r.GetString(6),
                Zone = r.IsDBNull(7) ? null : r.GetString(7),
            });
        return list;
    }

    public string? GetMeta(string key)
    {
        using var cmd = Cmd("SELECT value FROM meta WHERE key = $k");
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetMeta(string key, string value, SqliteTransaction? tx = null)
    {
        using var cmd = Cmd("INSERT OR REPLACE INTO meta VALUES ($k, $v)", tx);
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _db.Dispose();
        _writer.Dispose();
    }

    // ------------------------------------------------------------------ helpers

    private void Exec(string sql) { using var c = Cmd(sql); c.ExecuteNonQuery(); }

    private SqliteCommand Cmd(string sql, SqliteTransaction? tx = null)
    {
        var c = _db.CreateCommand();
        c.CommandText = sql;
        c.Transaction = tx;
        return c;
    }

    private static SqliteParameter[] Params(SqliteCommand cmd, params string[] names) =>
        names.Select(n => cmd.Parameters.Add(n, SqliteType.Text)).ToArray();

    private static void Set(SqliteParameter[] ps, params object?[] values)
    {
        for (int i = 0; i < ps.Length; i++)
        {
            var v = values[i];
            ps[i].SqliteType = v switch { int or long or bool => SqliteType.Integer, double => SqliteType.Real, _ => SqliteType.Text };
            ps[i].Value = v ?? DBNull.Value;
        }
    }

    private static string Iso(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm:ss");
}
