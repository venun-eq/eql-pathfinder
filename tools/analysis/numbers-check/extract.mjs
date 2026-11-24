// Copy the log lines between two times (inclusive) into a file, by binary search on byte offsets (the log is in time order).
// node extract.mjs <log> "2026-11-18T22:40:00" "2026-11-18T23:15:00" <out>
import fs from "node:fs";
const [file, fromS, toS, outPath] = process.argv.slice(2);
const M = { Jan: 0, Feb: 1, Mar: 2, Apr: 3, May: 4, Jun: 5, Jul: 6, Aug: 7, Sep: 8, Oct: 9, Nov: 10, Dec: 11 };
const parse = (l) => { const m = /^\[\w{3} (\w{3}) (\d\d) (\d\d):(\d\d):(\d\d) (\d{4})\]/.exec(l); return m ? Date.UTC(+m[6], M[m[1]], +m[2], +m[3], +m[4], +m[5]) : null; };
const from = Date.parse(fromS + "Z"), to = Date.parse(toS + "Z");
const fd = fs.openSync(file, "r"), size = fs.fstatSync(fd).size, buf = Buffer.alloc(65536);
const timeAt = (off) => { const n = fs.readSync(fd, buf, 0, buf.length, off); const s = buf.toString("latin1", 0, n); const lines = s.split("\n").slice(off ? 1 : 0); for (const l of lines) { const t = parse(l); if (t != null) return t; } return null; };
let lo = 0, hi = size;
while (hi - lo > 65536) { const mid = Math.floor((lo + hi) / 2); const t = timeAt(mid); if (t == null || t >= from) hi = mid; else lo = mid; }
const out = fs.createWriteStream(outPath); let pos = lo, rest = "", done = false, n = 0;
const big = Buffer.alloc(1 << 20);
while (!done) { const r = fs.readSync(fd, big, 0, big.length, pos); if (!r) break; pos += r; const s = rest + big.toString("latin1", 0, r); const lines = s.split("\n"); rest = lines.pop();
  for (const l of lines) { const t = parse(l); if (t == null) continue; if (t > to) { done = true; break; } if (t >= from) { out.write(l.replace(/\r$/, "") + "\n"); n++; } } }
out.end(); console.log(`${n} lines -> ${outPath}`);
