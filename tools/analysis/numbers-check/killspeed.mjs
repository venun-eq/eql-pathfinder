// Leveling rows: kill speed = K/min / median K/min of earlier runs at the place, within 2 levels, solo/group alike, with a rate (pace.md, one-usual.md)
import fs from "node:fs";
const h = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const median = (a) => { a = [...a].sort((x, y) => x - y); const n = a.length; return n % 2 ? a[(n - 1) / 2] : (a[n / 2 - 1] + a[n / 2]) / 2; };
const has = (r) => r.completed || r.kills >= 8;
const variants = {
  anyKind: (h2, r) => h2.kind !== "raid" && h2.kind != null,
  levelingOnly: (h2, r) => h2.kind === "leveling",
  notGrouped: (h2, r) => h2.kind != null && h2.kind !== "raid" && h2.kind !== "grouped",
};
const rows = h.filter(r => r.kind === "leveling");
for (const [name, f] of Object.entries(variants)) {
  let ok = 0, bad = []; 
  for (const r of rows) {
    const peers = h.filter(p => f(p, r) && p.baseZone === r.baseZone && p.difficulty === r.difficulty && p.levelStart != null && r.levelStart != null && Math.abs(p.levelStart - r.levelStart) <= 2 && has(p) && p.activeMinutes > 0 && p.start < r.start && p.id !== r.id);
    let ks = null, word = "none";
    if (peers.length >= 3) { const u = median(peers.map(p => p.kills / p.activeMinutes)); if (!has(r)) word = "early"; else { ks = (r.kills / r.activeMinutes) / u; const band = peers.length >= 5 ? 0.15 : 0.18; word = ks < 1 - band ? "slow" : ks > 1 + band ? "fast" : "pace"; } }
    const appKs = r.killSpeed;
    const same = word === r.pace && ((ks == null && appKs == null) || (ks != null && appKs != null && Math.abs(ks - appKs) < 1e-9));
    if (same) ok++; else bad.push(`${r.id} ${r.baseZone} D${r.difficulty} L${r.levelStart} mine=${word} ${ks?.toFixed(4)} (${peers.length} runs) app=${r.pace} ${appKs?.toFixed?.(4)}`);
  }
  console.log(`${name}: ${ok}/${rows.length} match`); bad.slice(0, 6).forEach(b => console.log("   " + b));
}
