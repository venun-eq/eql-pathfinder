namespace EqlPathfinder.Engine.Storage;

/// <summary>Another process already has the database open: the app, or a developer tool pointed at the real one.</summary>
public sealed class StoreInUseException(string path, Exception inner)
    : IOException($"Another copy of EQL Pathfinder, or a developer tool, is already using {path}. Close it first, or point the tool at a copy.", inner);

/// <summary>
/// Single-writer lock file beside the SQLite database (exclusive share mode) so two processes never tail the same log
/// into one database and duplicate records. Windows automatically releases and deletes the lock on process exit.
/// </summary>
internal sealed class WriterLock : IDisposable
{
    private readonly FileStream _file;

    private WriterLock(FileStream file) => _file = file;

    public static WriterLock Take(string dbPath)
    {
        var path = Path.GetFullPath(dbPath) + ".writer";
        try
        {
            return new WriterLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose));
        }
        catch (IOException ex) { throw new StoreInUseException(Path.GetFullPath(dbPath), ex); }
    }

    public void Dispose() => _file.Dispose();
}
