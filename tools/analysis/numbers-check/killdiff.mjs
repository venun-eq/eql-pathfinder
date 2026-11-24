// For one visit: the slain lines in its window against the kills table's rows (time and mob), each side's extras.
// node killdiff.mjs <db> <visit id> <window.txt>
import { DatabaseSync } from "node:sqlite";
import fs from "node:fs";
const [dbPath, id, file] = process.argv.slice(2);
const db = new DatabaseSync(dbPath, { readOnly: true });
const ME = db.prepare("select character from kills limit 1").get()?.character.split("_")[0]; // the log's own character: kills keep <Name>_<server>
const v = JSON.parse(db.prepare("select json from visits where id=?").get(id).json);
const iso = (s) => Date.parse(s + "Z") / 1000;
const M = { Jan: 0, Feb: 1, Mar: 2, Apr: 3, May: 4, Jun: 5, Jul: 6, Aug: 7, Sep: 8, Oct: 9, Nov: 10, Dec: 11 };
const start = iso(v.Start), exit = iso(v.ExitedAt), pauses = (v.Pauses ?? []).map(p => [iso(p.From), iso(p.To)]);
const fmt = (s) => new Date(s * 1000).toISOString().slice(5, 19);
const rows = db.prepare("select time, mob from kills where visit_id=?").all(id).map(k => ({ t: iso(k.time), mob: k.mob, used: false }));
const oneWord = (s) => /^[A-Z][a-z`']+$/.test(s);
const slain = [];
for (const line of fs.readFileSync(file, "latin1").split("\n")) {
  const m = /^\[\w{3} (\w{3}) (\d\d) (\d\d):(\d\d):(\d\d) (\d{4})\] (.*)$/.exec(line); if (!m) continue;
  const t = Date.UTC(+m[6], M[m[1]], +m[2], +m[3], +m[4], +m[5]) / 1000; if (t < start - 5 || t > exit + 5) continue;
  let x;
  if ((x = /^You have slain (.+)!$/.exec(m[7]))) slain.push({ t, mob: x[1], killer: "you" });
  else if ((x = /^(.+) has been slain by (.+)!$/.exec(m[7]))) slain.push({ t, mob: x[1], killer: oneWord(x[2]) ? "ONEWORD" : x[2].startsWith(ME) ? x[2] : "MOB:" + x[2] });
}
for (const s of slain) { const r = rows.find(r => !r.used && r.mob.toLowerCase() === s.mob.toLowerCase() && Math.abs(r.t - s.t) <= 1); if (r) { r.used = true; s.matched = true; } }
const inPause = (t) => pauses.some(([a, b]) => t >= a && t <= b);
console.log(`slain lines ${slain.length}, table rows ${rows.length}`);
console.log("slain lines with no table row:"); slain.filter(s => !s.matched).forEach(s => console.log(`   ${fmt(s.t)} ${s.mob} by ${s.killer}${s.t < start || s.t > exit ? " (outside window)" : ""}${inPause(s.t) ? " (in a pause)" : ""}`));
console.log("table rows with no slain line:"); rows.filter(r => !r.used).forEach(r => console.log(`   ${fmt(r.t)} ${r.mob}`));
