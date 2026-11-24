// Recompute every History row from pathfinder.db's kills and loot tables (and the visit's stored zone-in/out facts),
// using only the documented definitions. Usage: node recompute.mjs <db> <history.json> <fragment value> <out.json>
import { DatabaseSync } from "node:sqlite";
import fs from "node:fs";
const [dbPath, histPath, fvArg, outFile] = process.argv.slice(2);
if (!outFile) throw new Error("usage: node recompute.mjs <db> <history.json> <fragment value> <out.json>");
const FV = +fvArg; // the value the History dump was made at: --dump-history prints it
const IDLE = 180, MINK = 8;
const db = new DatabaseSync(dbPath, { readOnly: true });
const hist = JSON.parse(fs.readFileSync(histPath, "utf8"));
const t = (s) => new Date(s + "Z").getTime() / 1000;
const kills = {}, loot = {};
for (const k of db.prepare("select * from kills where visit_id is not null order by time").all()) (kills[k.visit_id] ??= []).push(k);
for (const l of db.prepare("select * from loot where visit_id is not null and spent=0").all()) (loot[l.visit_id] ??= []).push(l);
const visits = Object.fromEntries(db.prepare("select id, json from visits").all().map(v => [v.id, JSON.parse(v.json)]));
const moteT5 = (tier) => Math.pow(2, tier - 5);
const out = [];
for (const r of hist) {
  const v = visits[r.id], ks = kills[r.id] ?? [], ls = loot[r.id] ?? [];
  const c = { id: r.id };
  c.kills = ks.length;
  // docs/pace.md, docs/log-formats.md, docs/specs/xp-at-cap.md: an XP line read at the cap (level 50) is the bar filling,
  // not levels, so a kill at 50 is not an XP kill. A kill's level is the one its XP line was read at (docs/pace.md), which is what the kills table stores.
  const isXp = (k) => !k.xp_unknown && !(k.level >= 50);
  c.xpKills = ks.filter(isXp).length;
  let t5 = 0, chest = 0, high = 0, highM = 0, mo = 0, fr = 0;
  for (const l of ls) {
    const val = l.count * moteT5(l.tier) * (l.kind === 1 ? FV : 1);
    t5 += val; if (l.chest) chest += val; if (l.tier > 5) { high += val; if (l.kind === 0) highM += val; }
    if (l.kind === 0) mo += l.count; else fr += l.count;
  }
  Object.assign(c, { t5, chestT5: chest, highTierT5: high, highTierMotesOnly: highM, motes: mo, fragments: fr });
  // time inside: zone-in to completed (extended by later kills) or to leaving, minus trips outside
  const start = t(v.Start), lastKill = ks.length ? t(ks[ks.length - 1].time) : null;
  let end = v.Completed ? Math.max(t(v.CompletedAt), lastKill ?? 0) : t(v.ExitedAt);
  const pauses = (v.Pauses ?? []).map(p => [t(p.From), t(p.To)]);
  const pauseSec = pauses.reduce((a, [f, to]) => a + Math.max(0, Math.min(to, end) - Math.max(f, start)), 0);
  const inside = end - start - pauseSec;
  c.minutes = inside / 60;
  // idle: each gap (zone-in -> first kill, kill -> kill, last kill -> end) past 180 s; pause time inside a gap is not a gap
  let idle = 0;
  if (ks.length) {
    const pts = [start, ...ks.map(k => t(k.time)), end];
    for (let i = 1; i < pts.length; i++) {
      let g = pts[i] - pts[i - 1];
      for (const [f, to] of pauses) g -= Math.max(0, Math.min(to, pts[i]) - Math.max(f, pts[i - 1]));
      if (g > IDLE) idle += g - IDLE;
    }
  }
  idle = Math.min(idle, inside);
  c.idleMinutes = idle / 60; c.activeMinutes = (inside - idle) / 60;
  c.wholeMinutes = (t(v.ExitedAt) - start) / 60;
  const has = v.Completed || ks.length >= MINK;
  const ah = c.activeMinutes / 60;
  c.has = has;
  c.t5PerHour = has && ah > 0 ? t5 / ah : null;
  c.killsPerMinute = has && c.activeMinutes > 0 ? ks.length / c.activeMinutes : null;
  c.dps = has && ah > 0 ? (v.Damage?.Total ?? 0) / (inside - idle) : null;
  c.levelsPerHour = has && ah > 0 ? r.levelsGained / ah : null;
  c.killLevels = ks.reduce((a, k) => a + (isXp(k) ? k.pct : 0), 0) / 100;
  c.levelsGained = c.killLevels + (r.rewardLevels ?? 0); // kill levels as the docs say, plus the crawl's completion XP
  const secs = ks.map(k => k.secs).filter(s => s != null).sort((a, b) => a - b);
  c.medianKillSeconds = secs.length ? (secs.length % 2 ? secs[(secs.length - 1) / 2] : (secs[secs.length / 2 - 1] + secs[secs.length / 2]) / 2) : null;
  out.push(c);
}
fs.writeFileSync(outFile, JSON.stringify(out));
const byId = Object.fromEntries(hist.map(r => [r.id, r]));
const fields = ["kills", "xpKills", "t5", "chestT5", "highTierT5", "motes", "fragments", "minutes", "idleMinutes", "activeMinutes", "wholeMinutes", "t5PerHour", "killsPerMinute", "dps", "levelsPerHour", "killLevels", "levelsGained", "medianKillSeconds"];
const close = (a, b, f) => {
  if (a == null || b == null) return a == null && b == null;
  const tol = ["minutes", "idleMinutes", "activeMinutes", "wholeMinutes"].includes(f) ? 0.051 : 1e-6;
  return Math.abs(a - b) <= tol + 1e-6 * Math.abs(b) || Math.abs(a - b) / Math.max(Math.abs(b), 1e-9) < 0.0015;
};
for (const f of fields) {
  const mm = out.filter(c => !close(c[f], byId[c.id][f], f));
  console.log(`${f}: ${out.length - mm.length} match, ${mm.length} mismatch`);
  for (const c of mm.slice(0, 10)) console.log(`   ${c.id} ${byId[c.id].baseZone} D${byId[c.id].difficulty} ${byId[c.id].state} mine=${c[f]} app=${byId[c.id][f]}`);
}
