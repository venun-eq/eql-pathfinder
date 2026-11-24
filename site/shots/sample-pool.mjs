// Makes the website's sample sharing pool from a real community file (community/v1.json, schema 2).
//
//   node site/shots/sample-pool.mjs <real community.json> <out sample-pool.json> [players, default 32]
//
// What changes: how many players and crawls stand behind each number. Every player count (the file's total, each
// band's, each level's in places, each difficulty column's) is multiplied by players ÷ the real total and capped at
// it; crawls, hours, leftOut and stretches by the same factor. What stays exactly as the real file has it: every rate
// and minutes a level, every place, band, level and difficulty, the "since the patch" flags and the last-played
// dates. So the sample shows only places and levels the game has, at rates real players reached, as Ask would read
// them with that many players sharing. Nothing is added: a number the real file doesn't have, the sample doesn't either.
import { readFileSync, writeFileSync } from "node:fs";

const [src, out, want = "32"] = process.argv.slice(2);
if (!src || !out) { console.error("usage: node sample-pool.mjs <community.json> <out.json> [players]"); process.exit(2); }
const real = JSON.parse(readFileSync(src, "utf8"));
if (real.schema !== 2) { console.error(`schema ${real.schema}: this script knows schema 2`); process.exit(2); }

const target = Number(want), k = target / real.players;
const players = n => (n > 0 ? Math.min(target, Math.max(1, Math.round(n * k))) : 0);
const count = n => (n > 0 ? Math.round(n * k) : 0);
const hours = h => (h > 0 ? Math.round(h * k * 100) / 100 : 0);

function places(list) {
  for (const p of list) {
    p.players = p.players.map(players);
    p.crawls = p.crawls.map(count);
    if (p.hours) p.hours = p.hours.map(hours);
  }
}
function bands(list) {
  for (const b of list) { b.players = players(b.players); b.crawls = count(b.crawls); b.hours = hours(b.hours); }
}
function difficulties(list) {
  for (const d of list) for (const c of d.columns) { c.players = players(c.players); c.crawls = count(c.crawls); }
}
function slice(s) {
  if (!s) return;
  places(s.places ?? []);
  bands(s.bands ?? []);
  difficulties(s.difficulties ?? []);
}

slice(real);
slice(real.solo);
slice(real.group);
real.players = target;
real.crawls = count(real.crawls);
real.leftOut = count(real.leftOut);
if (typeof real.stretches === "number") real.stretches = count(real.stretches);
real.generated = new Date().toISOString();

writeFileSync(out, JSON.stringify(real));
console.log(`${out}: ${real.players} players, ${real.crawls} crawls (×${k.toFixed(2)} of ${src})`);
