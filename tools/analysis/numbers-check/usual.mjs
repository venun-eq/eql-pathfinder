// Recompute each farming/grouped row's usual and verdict from the other History rows (docs/pace.md, specs one-usual,
// usual-from-earlier, too-low-crawls; the band rule is docs/pace.md, "The farm band": 0.25 by default, held to 0.15-0.6, from the log spread once there are 5 runs; FarmPace.For says the same, checked 11-19).
import fs from "node:fs";
const hist = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const rec = Object.fromEntries(JSON.parse(fs.readFileSync(process.argv[3], "utf8")).map(c => [c.id, c]));
const W = 2, MINRUNS = 3, DEF = 0.25, MINB = 0.15, MAXB = 0.6, CAP = 50;
const median = (a) => { a = [...a].sort((x, y) => x - y); const n = a.length; return n % 2 ? a[(n - 1) / 2] : (a[n / 2 - 1] + a[n / 2]) / 2; };
// my own rate per row (from recomputed.json: t5 / active hours), regardless of gate, 0 when no active time
const rate = (r) => { const c = rec[r.id]; return c.activeMinutes > 0 ? c.t5 / (c.activeMinutes / 60) : 0; };
const has = (r) => r.completed || rec[r.id].kills >= 8;
const byMotes = (k) => k === "farming" || k === "grouped";
let n = 0; const mism = { verdict: [], usual: [], runs: [], band: [], pct: [] };
const capWide = []; // cap visits whose peers include levels 48-49
for (const r of hist) {
  if (!byMotes(r.kind)) { if (r.farmVerdict != null) mism.verdict.push([r.id, "has farm verdict but kind " + r.kind]); continue; }
  n++;
  const peers = hist.filter(h => byMotes(h.kind) && h.baseZone === r.baseZone && h.difficulty === r.difficulty
    && (h.kind === "grouped") === (r.kind === "grouped") && h.completed === r.completed && (!h.completed || h.refunded)
    && r.levelStart != null && h.levelStart != null && Math.abs(h.levelStart - r.levelStart) <= W
    && has(h) && rec[h.id].activeMinutes > 0 && h.start < r.start && h.id !== r.id);
  let verdict, usual = null, band = DEF, pct = null;
  if (r.levelStart == null || peers.length < MINRUNS) verdict = "none";
  else {
    const rates = peers.map(rate); usual = median(rates);
    const m = rates.filter(x => x > 0).map(Math.log);
    if (m.length >= 5) { const mu = m.reduce((a, b) => a + b) / m.length; const sd = Math.sqrt(m.reduce((a, x) => a + (x - mu) ** 2, 0) / (m.length - 1)); band = Math.min(MAXB, Math.max(MINB, Math.exp(sd) - 1)); }
    if (!has(r)) verdict = "early";
    else { const mine = rate(r); pct = usual > 0 ? mine / usual - 1 : 0; verdict = pct > band ? "more" : pct < -band ? "less" : "usual"; }
    if (r.levelStart >= CAP && peers.some(p => p.levelStart < CAP)) capWide.push(r.id);
  }
  const eq = (a, b) => (a == null && b == null) || (a != null && b != null && Math.abs(a - b) <= 1e-9 * Math.max(1, Math.abs(b)));
  if (verdict !== r.farmVerdict) mism.verdict.push([r.id, `${r.baseZone} D${r.difficulty} L${r.levelStart} ${r.kind} mine=${verdict} app=${r.farmVerdict}`]);
  if (!eq(usual, r.farmUsual)) mism.usual.push([r.id, `mine=${usual} app=${r.farmUsual} (peers mine ${peers.length}, app ${r.farmRuns})`]);
  if (peers.length !== r.farmRuns && !(verdict === "none" && r.farmRuns === peers.length)) mism.runs.push([r.id, `mine=${peers.length} app=${r.farmRuns}`]);
  if (!eq(band, r.farmBand)) mism.band.push([r.id, `mine=${band} app=${r.farmBand}`]);
  if (!eq(pct, r.farmPct)) mism.pct.push([r.id, `mine=${pct} app=${r.farmPct}`]);
}
console.log(`rows judged by T5-eq/h: ${n}`);
for (const [k, v] of Object.entries(mism)) { console.log(`${k}: ${n - v.length} match, ${v.length} mismatch`); v.slice(0, 10).forEach(x => console.log("   ", x.join(" "))); }
console.log(`cap visits judged whose usual includes visits that started at 48-49: ${capWide.length}`, capWide.slice(0, 5));
const vc = {}; hist.forEach(r => vc[r.farmVerdict] = (vc[r.farmVerdict] || 0) + 1); console.log(vc);
