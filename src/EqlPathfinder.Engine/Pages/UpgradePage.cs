using System.Text.Json;
using EqlPathfinder.Engine.IO;
using EqlPathfinder.Engine.Model;
using EqlPathfinder.Engine.Upgrades;

namespace EqlPathfinder.Engine.Pages;

/// <summary>
/// Builds the Upgrades page answer (inventory gear, mote bank, and computed plan) in the engine so window,
/// --snapshot previews, and tests share the exact same JSON contract (<see cref="PageData"/>, <see cref="AskPage"/>).
/// this returns, written with <see cref="PageJson.Options"/>.
/// </summary>
public static class UpgradePage
{
    /// <summary>
    /// <paramref name="events"/> is what the log saw after the bank was entered; <paramref name="request"/> is what
    /// upgrade.js posted (<c>{target, tier, progress, goal, vtps, copies, merge}</c>), or nothing for no plan yet.
    /// </summary>
    public static object Build(CharacterLogs logs, MoteBank bank, List<BankEvent> events, JsonElement? request)
    {
        var dir = Path.GetDirectoryName(logs.LivePath);
        var path = dir != null ? InventoryExport.PathFor(dir, logs.Name, logs.Server) : null;
        bool exists = path != null && File.Exists(path);
        List<GearItem> items = [];
        string? error = null;
        if (exists)
            try { items = InventoryExport.ReadGear(path!); }
            catch (IOException ex) { error = ex.Message; }

        var (motes, frags, since) = bank.Current(events);
        UpgradePlan? plan = null;
        if (request is { ValueKind: JsonValueKind.Object } r)
        {
            int I(string k, int d) => r.TryGetProperty(k, out var x) && x.TryGetInt32(out var n) ? n : d;
            bool spell = r.TryGetProperty("target", out var tg) && tg.ValueKind == JsonValueKind.String && tg.GetString() == "spell";
            // Your other copies of the item, {tier, progress} each, which the page finds in the inventory export. They're merged
            // in unless the request says merge: false.
            static ItemCopy? Copy(JsonElement x) =>
                x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out var t) ? new ItemCopy(t)
                : x.ValueKind == JsonValueKind.Object && x.TryGetProperty("tier", out var xt) && xt.TryGetInt32(out var t2)
                    ? new ItemCopy(t2, x.TryGetProperty("progress", out var xp) && xp.TryGetInt64(out var p) ? p : 0) : null;
            ItemCopy[] copies = !spell && r.TryGetProperty("copies", out var cp) && cp.ValueKind == JsonValueKind.Array
                ? cp.EnumerateArray().Select(Copy).OfType<ItemCopy>().Take(12).ToArray() : [];
            bool merge = !(r.TryGetProperty("merge", out var mg) && mg.ValueKind == JsonValueKind.False);
            plan = UpgradePlanner.Plan(new UpgradeRequest(spell ? UpgradeTarget.Spell : UpgradeTarget.Gear,
                I("tier", 0), I("progress", 0), I("goal", 10), motes, frags, I("vtps", 0), copies, merge));
        }
        return new
        {
            kind = "upgrade",
            inventory = new { file = path != null ? Path.GetFileName(path) : null, exists, time = exists ? File.GetLastWriteTime(path!) : (DateTime?)null, error },
            items,
            bank = new { entered = new { motes = bank.Motes, fragments = bank.Fragments, vtps = bank.Vtps }, enteredAt = bank.EnteredAt, motes, fragments = frags, vtps = bank.Vtps, since },
            plan,
        };
    }
}
