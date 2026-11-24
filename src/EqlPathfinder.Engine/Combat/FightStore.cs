using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace EqlPathfinder.Engine.Combat;

/// <summary>Where the fight meter read each log up to, and what it was carrying there.</summary>
public sealed class FightCheckpoint
{
    public int Version { get; set; }
    public Dictionary<string, long> Offsets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public FightContext Context { get; set; } = new();
}

/// <summary>
/// The fight meter's own SQLite file (fights.db), apart from the history database so neither waits on the other.
/// A fight's key is its character, start and mob, so reading the same lines twice never doubles it.
/// </summary>
public sealed class FightStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault };
    private readonly SqliteConnection _db;
    private readonly object _lock = new();

    private readonly Storage.WriterLock _writer;

    /// <exception cref="Storage.StoreInUseException">Another process has this database open.</exception>
    public FightStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _writer = Storage.WriterLock.Take(path);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        _db.Open();
        Exec("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;");
        Exec("""
            CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS fights (
                character TEXT NOT NULL, start TEXT NOT NULL, mob TEXT NOT NULL, zone TEXT, difficulty INTEGER, trio TEXT,
                session TEXT NOT NULL, stint TEXT NOT NULL, json BLOB NOT NULL, PRIMARY KEY (character, start, mob)) WITHOUT ROWID;
            CREATE INDEX IF NOT EXISTS fights_place ON fights (character, zone, start);
            """);
    }

    public FightCheckpoint? LoadCheckpoint(string character)
    {
        lock (_lock)
        {
            using var cmd = Cmd("SELECT value FROM meta WHERE key = $k");
            cmd.Parameters.AddWithValue("$k", "checkpoint:" + character);
            return cmd.ExecuteScalar() is string s ? JsonSerializer.Deserialize<FightCheckpoint>(s, Json) : null;
        }
    }

    /// <summary>Fights and, when given, the checkpoint, in one transaction.</summary>
    public void Save(string character, IReadOnlyList<Fight> fights, FightCheckpoint? checkpoint)
    {
        if (fights.Count == 0 && checkpoint == null) return;
        lock (_lock)
        {
            using var tx = _db.BeginTransaction();
            if (fights.Count > 0)
            {
                using var cmd = Cmd("INSERT OR REPLACE INTO fights VALUES ($c,$s,$m,$z,$d,$t,$se,$st,$j)", tx);
                var names = new[] { "$c", "$s", "$m", "$z", "$d", "$t", "$se", "$st", "$j" };
                var p = names.Select(n => { var x = cmd.CreateParameter(); x.ParameterName = n; cmd.Parameters.Add(x); return x; }).ToArray();
                foreach (var f in fights)
                {
                    p[0].Value = character; p[1].Value = Iso(f.Start); p[2].Value = f.Mob; p[3].Value = (object?)f.Zone ?? DBNull.Value;
                    p[4].Value = (object?)f.Difficulty ?? DBNull.Value; p[5].Value = (object?)f.Trio ?? DBNull.Value;
                    p[6].Value = Iso(f.Session); p[7].Value = Iso(f.Stint); p[8].Value = Pack(f);
                    cmd.ExecuteNonQuery();
                }
            }
            if (checkpoint != null)
            {
                using var cmd = Cmd("INSERT OR REPLACE INTO meta VALUES ($k, $v)", tx);
                cmd.Parameters.AddWithValue("$k", "checkpoint:" + character);
                cmd.Parameters.AddWithValue("$v", JsonSerializer.Serialize(checkpoint, Json));
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    /// <summary>Forgets a character's fights and checkpoint (before reading the logs again).</summary>
    public void Reset(string character)
    {
        lock (_lock)
        {
            using var tx = _db.BeginTransaction();
            using (var cmd = Cmd("DELETE FROM fights WHERE character = $c", tx)) { cmd.Parameters.AddWithValue("$c", character); cmd.ExecuteNonQuery(); }
            using (var cmd = Cmd("DELETE FROM meta WHERE key = $k", tx)) { cmd.Parameters.AddWithValue("$k", "checkpoint:" + character); cmd.ExecuteNonQuery(); }
            tx.Commit();
        }
    }

    /// <summary>One play session's fights, oldest first.</summary>
    public List<Fight> Session(string character, DateTime session) =>
        Read("SELECT " + Cols + " FROM fights WHERE character = $c AND start >= $a AND session = $a ORDER BY start", character, c => c.Parameters.AddWithValue("$a", Iso(session)));

    /// <summary>The newest fights in one zone and difficulty with one trio, for "your usual".</summary>
    public List<Fight> InPlace(string character, string? zone, int? difficulty, string? trio, int take = 600) =>
        Read($"""
            SELECT {Cols} FROM fights WHERE character = $c AND zone IS $z AND difficulty IS $d AND trio IS $t
            ORDER BY start DESC LIMIT {take}
            """, character, c =>
        {
            c.Parameters.AddWithValue("$z", (object?)zone ?? DBNull.Value);
            c.Parameters.AddWithValue("$d", (object?)difficulty ?? DBNull.Value);
            c.Parameters.AddWithValue("$t", (object?)trio ?? DBNull.Value);
        });

    /// <summary>Fights that started in a stretch of time, oldest first (the reconciliation check).</summary>
    public List<Fight> Between(string character, DateTime from, DateTime to) =>
        Read("SELECT " + Cols + " FROM fights WHERE character = $c AND start >= $a AND start <= $b ORDER BY start", character, c =>
        {
            c.Parameters.AddWithValue("$a", Iso(from));
            c.Parameters.AddWithValue("$b", Iso(to));
        });

    public int Count(string character)
    {
        lock (_lock)
        {
            using var cmd = Cmd("SELECT COUNT(*) FROM fights WHERE character = $c");
            cmd.Parameters.AddWithValue("$c", character);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    private List<Fight> Read(string sql, string character, Action<SqliteCommand> bind)
    {
        lock (_lock)
        {
            using var cmd = Cmd(sql);
            cmd.Parameters.AddWithValue("$c", character);
            bind(cmd);
            var list = new List<Fight>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (Unpack((byte[])r[0]) is not { } f) continue;
                f.Start = DateTime.Parse(r.GetString(1), System.Globalization.CultureInfo.InvariantCulture);
                f.Mob = r.GetString(2);
                f.Zone = r.IsDBNull(3) ? null : r.GetString(3);
                f.Difficulty = r.IsDBNull(4) ? null : r.GetInt32(4);
                f.Trio = r.IsDBNull(5) ? null : r.GetString(5);
                f.Session = DateTime.Parse(r.GetString(6), System.Globalization.CultureInfo.InvariantCulture);
                f.Stint = DateTime.Parse(r.GetString(7), System.Globalization.CultureInfo.InvariantCulture);
                list.Add(f);
            }
            return list;
        }
    }

    // The JSON holds only what the columns don't.
    private const string Cols = "json, start, mob, zone, difficulty, trio, session, stint";

    /// <summary>The session of the last fight at or before a moment (for looking back at an earlier day).</summary>
    public DateTime? SessionBefore(string character, DateTime at)
    {
        lock (_lock)
        {
            using var cmd = Cmd("SELECT session FROM fights WHERE character = $c AND start <= $a ORDER BY start DESC LIMIT 1");
            cmd.Parameters.AddWithValue("$c", character);
            cmd.Parameters.AddWithValue("$a", Iso(at));
            return cmd.ExecuteScalar() is string s ? DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture) : null;
        }
    }

    /// <summary>
    /// Gives every stored fight the trio the app says was playing then (<see cref="Analysis.TrioTimeline"/>), so "your
    /// usual" compares like with like as every other page does. Fights before the first visit keep theirs.
    /// </summary>
    public int Relabel(string character, Analysis.TrioTimeline timeline)
    {
        lock (_db)
        {
            using var tx = _db.BeginTransaction();
            using var cmd = Cmd("UPDATE fights SET trio = $t WHERE character = $c AND start >= $a AND start < $b AND trio IS NOT $t", tx);
            var t = cmd.Parameters.Add("$t", SqliteType.Text);
            cmd.Parameters.AddWithValue("$c", character);
            var a = cmd.Parameters.Add("$a", SqliteType.Text);
            var b = cmd.Parameters.Add("$b", SqliteType.Text);
            int changed = 0;
            foreach (var (from, until, trio) in timeline.Spans())
            {
                (t.Value, a.Value, b.Value) = ((object?)trio ?? DBNull.Value, Iso(from), until == DateTime.MaxValue ? "9999" : Iso(until));
                changed += cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return changed;
        }
    }

    private static string Iso(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm:ss");

    private static byte[] Pack(Fight f)
    {
        using var ms = new MemoryStream();
        using (var z = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
            JsonSerializer.Serialize(z, f, Json);
        return ms.ToArray();
    }

    private static Fight? Unpack(byte[] blob)
    {
        using var z = new System.IO.Compression.DeflateStream(new MemoryStream(blob), System.IO.Compression.CompressionMode.Decompress);
        return JsonSerializer.Deserialize<Fight>(z, Json);
    }

    private SqliteCommand Cmd(string sql, SqliteTransaction? tx = null)
    {
        var c = _db.CreateCommand();
        c.CommandText = sql;
        c.Transaction = tx;
        return c;
    }

    private void Exec(string sql) { using var c = Cmd(sql); c.ExecuteNonQuery(); }

    public void Dispose()
    {
        _db.Dispose();
        _writer.Dispose();
    }
}
