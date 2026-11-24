using EqlPathfinder.Engine.Combat;
using EqlPathfinder.Engine.Storage;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// One writer per database: on 11-17 a second process following the same log wrote every kill twice into the real
/// database for a few minutes (23 rows). A second open now fails instead.
/// </summary>
public class StoreLockTests
{
    [Fact]
    public void ASecondWriterIsTurnedAway()
    {
        var dir = Directory.CreateTempSubdirectory("eqlp-lock-").FullName;
        try
        {
            var path = Path.Combine(dir, "pathfinder.db");
            using (var first = new PathfinderStore(path))
            {
                var ex = Assert.Throws<StoreInUseException>(() => new PathfinderStore(path));
                Assert.Contains("already using", ex.Message);
            }
            using (new PathfinderStore(path)) { }   // free again once the first one closes

            var fights = Path.Combine(dir, "fights.db");
            using (new FightStore(fights)) Assert.Throws<StoreInUseException>(() => new FightStore(fights));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
