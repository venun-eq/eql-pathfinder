// Independent pass over the raw logs: for every stored visit (its zone-in, zone-out and trips outside, from the visits
// table), count the slain lines inside it by the documented rule and value the loot lines inside it. Compare with History.
// node fullcheck.mjs <db> <history.json> <fragment value> <out.json> <log1> [<log2> ...]   (logs in time order)
import { DatabaseSync } from "node:sqlite";
import fs from "node:fs";
import readline from "node:readline";
const [dbPath, histPath, fvArg, outPath, ...logs] = process.argv.slice(2);
if (!logs.length) throw new Error("usage: node fullcheck.mjs <db> <history.json> <fragment value> <out.json> <log1> [<log2> ...]");
const FV = +fvArg;
const db = new DatabaseSync(dbPath, { readOnly: true });
const ME = db.prepare("select character from kills limit 1").get()?.character.split("_")[0]; // the log's own character: kills keep <Name>_<server>
const hist = Object.fromEntries(JSON.parse(fs.readFileSync(histPath, "utf8")).map(r => [r.id, r]));
const iso = (s) => Date.parse(s + "Z") / 1000;
const visits = db.prepare("select id, json from visits").all().map(v => JSON.parse(v.json)).map(v => ({
  id: v.Id, zone: v.Zone, start: iso(v.Start), exit: iso(v.ExitedAt), pauses: (v.Pauses ?? []).map(p => [iso(p.From), iso(p.To)]),
  kills: 0, you: 0, other: 0, t5: 0, chest: 0, high: 0, open: false })).sort((a, b) => a.start - b.start);
const M = { Jan: 0, Feb: 1, Mar: 2, Apr: 3, May: 4, Jun: 5, Jul: 6, Aug: 7, Sep: 8, Oct: 9, Nov: 10, Dec: 11 };
const TIER = { Infinitesimal: 1, Minor: 2, Lesser: 3, Major: 5, Greater: 6, Superior: 7, Grand: 8, Ascendant: 9, Infinite: 10 };
const oneWord = (s) => /^[A-Z][a-z`']+$/.test(s);
const pets = new Set();
let charm = null, broke = null; // the charmed mob while the charm holds, and the last one that broke
const endCharm = (t) => { if (charm) broke = { name: charm.name, at: t }; charm = null; };
let vi = 0, cur = null, paused = false, pi = 0, lastT = 0;
const closeIfPast = (t) => {
  if (cur && t > cur.exit) { cur = null; paused = false; }
  if (cur && !paused && cur.pauses[pi] && t > cur.pauses[pi][0] && t < cur.pauses[pi][1]) paused = true; // a trip outside with no zone line at its edge
  if (cur && paused && cur.pauses[pi] && t >= cur.pauses[pi][1]) { paused = false; pi++; }
};
const onEntered = (t, z) => {
  // leaving: the open visit ends or pauses at this zone line
  if (cur && !paused) {
    if (cur.pauses[pi] && cur.pauses[pi][0] === t) paused = true;
    else if (cur.exit === t) { cur = null; }
  } else if (cur && paused && cur.pauses[pi] && cur.pauses[pi][1] === t && z === cur.zone) { paused = false; pi++; return; }
  // arriving: a stored visit starts at this zone line
  while (vi < visits.length && visits[vi].start < t) vi++;
  if (vi < visits.length && visits[vi].start === t && visits[vi].zone === z) { cur = visits[vi]; paused = false; pi = 0; vi++; }
};
const lootRe = [/^You looted (a|an|\d+) (Mote|Fragment) of (?:(\w+) )?Potential from (.+?) and stored/, /^--You have looted (a|an|\d+) (Mote|Fragment) of (?:(\w+) )?Potential from (.+?)\.--$/];
for (const file of logs) {
  const rl = readline.createInterface({ input: fs.createReadStream(file, { encoding: "latin1" }), crlfDelay: Infinity });
  for await (const line of rl) {
    const m = /^\[\w{3} (\w{3}) (\d\d) (\d\d):(\d\d):(\d\d) (\d{4})\] (.*)$/.exec(line);
    if (!m) continue;
    const t = Date.UTC(+m[6], M[m[1]], +m[2], +m[3], +m[4], +m[5]) / 1000, b = m[7];
    lastT = t;
    let x;
    // Pets (docs/fight-meter.md, "Charmed pets"): a summoned pet's one-word name is remembered. A charmed mob keeps its
    // mob name, so it's yours only while the charm holds: from its "Attacking ... Master." until the charm wears off of
    // it, one of its name swings at you or dies, you die or zone, a summoned pet speaks, "You do not have a pet.", or 2
    // minutes without a line from it; back again when the charm lands on that name within 2 minutes. Not followed here:
    // "only its own targets count", so a wild one of the same name can add a kill.
    if ((x = /^(.+?) told you, 'Attacking .* Master\.'/.exec(b))) {
      if (oneWord(x[1])) { pets.add(x[1]); endCharm(t); } else charm = { name: x[1].toLowerCase(), seen: t };
    }
    const lower = b.toLowerCase();
    if (charm) {
      if (t - charm.seen > 120 || /^You have been slain|^You died\.|^You do not have a pet\./.test(b)
        || (x = /^Your .+ spell has worn off of (.+)\.$/.exec(b)) && x[1].toLowerCase() === charm.name
        || lower.startsWith(charm.name) && / YOU\b/.test(b)) endCharm(t);
      else if (lower.startsWith(charm.name)) charm.seen = t;
    }
    if ((x = /^(.+?)(?: has been charmed|'s eyes glaze over)\.$/.exec(b)) && broke && broke.name === x[1].toLowerCase() && t - broke.at <= 120)
      charm = { name: broke.name, seen: t };
    closeIfPast(t);
    if ((x = /^You have entered (.*)\.$/.exec(b))) { endCharm(t); onEntered(t, x[1]); continue; }
    if ((x = /^(.+) has been slain by (.+)!$/.exec(b)) && charm && x[1].toLowerCase() === charm.name) { endCharm(t); continue; } // your pet died: not a kill
    if (!cur || paused) continue;
    if ((x = /^You have slain (.+)!$/.exec(b))) { cur.kills++; cur.you++; continue; }
    if ((x = /^(.+) has been slain by (.+)!$/.exec(b))) {
      const victim = x[1], killer = x[2];
      if (charm && killer.toLowerCase() === charm.name) charm.seen = t;
      if (killer === ME || pets.has(killer) || killer.startsWith(ME + "`s") || charm && killer.toLowerCase() === charm.name) { cur.kills++; cur.you++; }
      else if (oneWord(killer) && !oneWord(victim)) { cur.kills++; cur.other++; }
      else if (!oneWord(victim) && !oneWord(killer)) { cur.mobOnMob = (cur.mobOnMob ?? 0) + 1; (cur.killers ??= {})[killer] = 1; }
      continue;
    }
    for (const re of lootRe) if ((x = re.exec(b))) {
      const n = /\d+/.test(x[1]) ? +x[1] : 1, tier = x[3] ? TIER[x[3]] : 4, v = n * 2 ** (tier - 5) * (x[2] === "Fragment" ? FV : 1);
      cur.t5 += v; if (/Reward Chest/.test(x[4])) cur.chest += v; if (tier > 5) cur.high += v; break;
    }
  }
}
const res = visits.map(v => ({ id: v.id, zone: v.zone, kills: v.kills, mobOnMob: v.mobOnMob ?? 0, killers: Object.keys(v.killers ?? {}).slice(0,4), you: v.you, other: v.other, t5: v.t5, chest: v.chest, high: v.high,
  appKills: hist[v.id]?.kills, appT5: hist[v.id]?.t5, appChest: hist[v.id]?.chestT5, appHigh: hist[v.id]?.highTierT5 }));
fs.writeFileSync(outPath, JSON.stringify(res));
const near = (a, b) => Math.abs(a - b) < 1e-6;
for (const [f, a] of [["kills", "appKills"], ["t5", "appT5"], ["chest", "appChest"], ["high", "appHigh"]]) {
  const bad = res.filter(r => !near(r[f], r[a]));
  console.log(`${f}: ${res.length - bad.length} match, ${bad.length} mismatch; sum mine ${res.reduce((s, r) => s + r[f], 0).toFixed(2)} app ${res.reduce((s, r) => s + r[a], 0).toFixed(2)}`);
  bad.sort((p, q) => Math.abs(q[f] - q[a]) - Math.abs(p[f] - p[a])).slice(0, 10).forEach(r => console.log(`   ${r.id} ${r.zone} mine=${+r[f].toFixed(3)} app=${+r[a].toFixed(3)} (you ${r.you}, other ${r.other})`));
}
