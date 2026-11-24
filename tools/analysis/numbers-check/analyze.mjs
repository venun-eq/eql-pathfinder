// Work out one visit from its raw log lines alone, by the documented rules (docs/log-formats.md, specs/kills-at-cap.md,
// specs/calc-docs.md "As of 0.10.12", one-rate.md, visits-tab.md, motes-and-fragments.md).
// node analyze.mjs <window.txt> "<exact zone name as entered>" <zone-in ISO> <character> <fragment value> [--list]
import fs from "node:fs";
const [file, zone, inS, meArg, fvArg, ...flags] = process.argv.slice(2);
if (!fvArg) throw new Error("usage: node analyze.mjs <window.txt> <zone> <zone-in ISO> <character> <fragment value> [--list]");
const ME = meArg, FV = +fvArg, LIST = flags.includes("--list");
const M = { Jan: 0, Feb: 1, Mar: 2, Apr: 3, May: 4, Jun: 5, Jul: 6, Aug: 7, Sep: 8, Oct: 9, Nov: 10, Dec: 11 };
const lines = fs.readFileSync(file, "latin1").split("\n").filter(Boolean).map(l => {
  const m = /^\[\w{3} (\w{3}) (\d\d) (\d\d):(\d\d):(\d\d) (\d{4})\] (.*)$/.exec(l);
  return m ? { t: Date.UTC(+m[6], M[m[1]], +m[2], +m[3], +m[4], +m[5]) / 1000, b: m[7] } : null; }).filter(Boolean);
const fmt = (s) => new Date(s * 1000).toISOString().slice(11, 19);
const zin = Date.parse(inS + "Z") / 1000;
const TIER = { Infinitesimal: 1, Minor: 2, Lesser: 3, Major: 5, Greater: 6, Superior: 7, Grand: 8, Ascendant: 9, Infinite: 10 };
// pets: any "<Name> told you, 'Attacking ... Master.'" in the window
const pets = new Set(lines.map(l => /^(\w+) told you, 'Attacking .* Master\.'/.exec(l.b)?.[1]).filter(Boolean));
// time inside: from zone-in; "You have entered <zone>." again continues, any other zone ends a segment
let inside = [], cur = null, completed = null, refund = false, deaths = 0;
const entered = lines.filter(l => /^You have entered (.*)\.$/.test(l.b) && l.t >= zin);
for (const l of entered) {
  const z = /^You have entered (.*)\.$/.exec(l.b)[1];
  // a D0 instance has the open world's plain name: entering that name again from inside means you went out to the world
  const plain = !/ \d+ \(\w+\)$/.test(zone);
  if (z === zone) { if (cur == null) cur = l.t; else if (plain) { inside.push([cur, l.t]); cur = null; } }
  else if (cur != null) { inside.push([cur, l.t]); cur = null; }
}
if (cur != null) inside.push([cur, lines[lines.length - 1].t]);
// a re-entry continues this visit only within 10 minutes, with no new instance made or joined and no "completed" before it
const segs = [inside[0]];
for (let i = 1; i < inside.length; i++) {
  const last = segs[segs.length - 1], gap = inside[i][0] - last[1];
  const between = lines.filter(l => l.t > last[1] && l.t < inside[i][0]);
  const newInst = between.some(l => /creating instance|has asked you to join the instance/.test(l.b));
  const doneBefore = lines.some(l => l.t >= segs[0][0] && l.t <= last[1] && /completed the Dungeon Crawl/.test(l.b));
  if (gap <= 600 && !newInst && !doneBefore) segs.push(inside[i]); else break;
}
const vStart = segs[0][0], vOut = segs[segs.length - 1][1];
const inSeg = (t) => segs.some(([a, b]) => t >= a && t < b);
const win = lines.filter(l => inSeg(l.t));
const pauses = segs.slice(1).map((s, i) => [segs[i][1], s[0]]);
for (const l of win) {
  if (/^You have completed the Dungeon Crawl/.test(l.b) && completed == null) completed = l.t;
  if (/^You earned a refund/.test(l.b)) refund = true;
  if (/^You have been slain by|^You died\./.test(l.b)) deaths++;
}
// kills: a kill is its slain line (kills-at-cap)
const slain = [], xp = [];
const oneWord = (s) => /^[A-Z][a-z`']+$/.test(s);
for (const l of win) {
  let m;
  if ((m = /^You have slain (.+)!$/.exec(l.b))) slain.push({ t: l.t, mob: m[1], by: "you" });
  else if ((m = /^(.+) has been slain by (.+)!$/.exec(l.b))) {
    const victim = m[1], killer = m[2];
    if (killer === ME || pets.has(killer)) slain.push({ t: l.t, mob: victim, by: "you/pet" });
    else if (oneWord(victim)) { /* a player's death: never a kill */ }
    else slain.push({ t: l.t, mob: victim, by: "other", killer });
  }
  if ((m = /^You gain (party )?experience( \(with a bonus\))?!( \(([\d.]+)%\))?$/.exec(l.b))) xp.push({ t: l.t, party: !!m[1], pct: m[4] != null ? +m[4] : null, used: false });
  if (/^You receive no experience for defeating this creature as you are in a raid/.test(l.b)) xp.push({ t: l.t, raid: true, pct: 0, used: false });
}
const kills = slain;
for (const k of kills) { const x = xp.find(x => !x.used && Math.abs(x.t - k.t) <= 1); if (x) { x.used = true; k.xp = x; } }
const rewards = xp.filter(x => !x.used);
const end = completed != null ? Math.max(completed, ...kills.map(k => k.t)) : vOut;
const pauseIn = (a, b) => pauses.reduce((s, [f, to]) => s + Math.max(0, Math.min(to, b) - Math.max(f, a)), 0);
const insideSec = end - vStart - pauseIn(vStart, end);
let idle = 0; const gaps = [];
if (kills.length) {
  const pts = [vStart, ...kills.map(k => k.t), end];
  for (let i = 1; i < pts.length; i++) {
    const g = pts[i] - pts[i - 1] - pauseIn(pts[i - 1], pts[i]);
    if (g > 180) { idle += g - 180; gaps.push(`${fmt(pts[i - 1])}->${fmt(pts[i])} ${g}s`); }
  }
}
const active = insideSec - idle;
// loot: motes at 2^(tier-5), fragments at that times the fragment value
let t5 = 0, chest = 0, high = 0; const loot = [];
for (const l of win) {
  const m = /^You looted (a|an|\d+) (Mote|Fragment) of (?:(\w+) )?Potential from (.+?) and stored/.exec(l.b) || /^--You have looted (a|an|\d+) (Mote|Fragment) of (?:(\w+) )?Potential from (.+?)\.--$/.exec(l.b);
  if (!m) continue;
  const n = /\d+/.test(m[1]) ? +m[1] : 1, tier = m[3] ? TIER[m[3]] : 4, fr = m[2] === "Fragment";
  const v = n * 2 ** (tier - 5) * (fr ? FV : 1); t5 += v;
  const isChest = /Reward Chest/.test(m[4]); if (isChest) chest += v; if (tier > 5) high += v;
  loot.push(`${fmt(l.t)} ${n}x ${m[2]} T${tier}${isChest ? " (chest)" : ""} = ${v.toFixed(3)}`);
}
const kxp = kills.filter(k => k.xp && k.xp.pct != null).reduce((a, k) => a + k.xp.pct, 0);
const unknown = kills.filter(k => k.xp && k.xp.pct == null && !k.xp.raid).length, noxp = kills.filter(k => !k.xp).length;
// damage: yours and your pet's (melee, spells, DoTs, damage shield)
const dmgRe = [/^You \w+ .+ for (\d+) points? of damage/, /^You hit .+ for (\d+) points? of .*damage/, /^.+ has taken (\d+) damage from your /, /^.+ is \w+ by YOUR .+ for (\d+) points? of non-melee damage/];
let dmg = 0;
for (const l of win) {
  if (/YOU for|^You have taken|^You hit yourself/.test(l.b)) continue;
  let m = null;
  for (const re of dmgRe) if ((m = re.exec(l.b))) { dmg += +m[1]; break; }
  if (!m) for (const p of pets) { const pm = new RegExp(`^${p} (?!is )\\w+ .+ for (\\d+) points? of (?:[\\w-]+ )?damage`).exec(l.b); if (pm) { dmg += +pm[1]; break; } }
}
const has = completed != null || kills.length >= 8, ah = active / 3600;
const mins = (s) => (s / 60).toFixed(3) + " min";
console.log(`zone-in ${fmt(vStart)}  completed ${completed ? fmt(completed) : "-"}  refund ${refund}  zone-out ${fmt(vOut)}  segments ${segs.length} pauses ${pauses.map(([a, b]) => fmt(a) + "-" + fmt(b)).join(",") || "-"}  deaths ${deaths}  pets ${[...pets].join(",") || "-"}`);
console.log(`kills ${kills.length} (you/pet ${kills.filter(k => k.by != "other").length}, other ${kills.filter(k => k.by == "other").length}); with (x%) ${kills.length - unknown - noxp}, XP line no % ${unknown}, no XP line ${noxp}; party-XP kills ${kills.filter(k => k.xp?.party).length}; unpaired XP lines (rewards) ${rewards.length} ${rewards.map(r => fmt(r.t) + (r.pct != null ? " " + r.pct + "%" : "")).join(" ")}`);
console.log(`kill XP ${kxp.toFixed(3)}% = ${(kxp / 100).toFixed(5)} levels;  last kill ${kills.length ? fmt(kills[kills.length - 1].t) : "-"}; time-inside end ${fmt(end)}`);
console.log(`inside ${mins(insideSec)}  idle ${mins(idle)} [${gaps.join("; ")}]  active ${mins(active)}  whole ${mins(vOut - vStart)}  after ${mins(vOut - end - pauseIn(end, vOut))}  outside ${mins(pauseIn(vStart, vOut))}`);
console.log(`T5 ${t5.toFixed(4)}  chest ${chest.toFixed(4)}  above-T5 ${high.toFixed(4)}  | rate? ${has}  T5/h ${has ? (t5 / ah).toFixed(3) : "-"}  K/min ${has ? (kills.length / (active / 60)).toFixed(4) : "-"}  damage ${dmg} DPS ${has ? (dmg / active).toFixed(2) : "-"}`);
loot.forEach(x => console.log("   loot " + x));
if (LIST) kills.forEach(k => console.log(`   ${fmt(k.t)} ${k.by.padEnd(7)} ${k.mob} ${k.xp ? (k.xp.raid ? "raid" : k.xp.pct ?? "no%") : "noXP"}`));
