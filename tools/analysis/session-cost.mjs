// Where Claude Code sessions on this repository spent their tokens. It reads the transcripts Claude Code keeps under
// ~/.claude/projects (one folder per directory a session started in; this repository's begin with the main checkout's
// path, with - for every separator) and prints totals: by session, by how big the context was on each turn, by tool,
// by file read, and the largest shell outputs. The session-cost skill (.claude/skills/session-cost) says how to read it.
//
// Usage: node tools/analysis/session-cost.mjs [--since 2026-11-18] [--session <id>] [--prompts] [--dir <projects folder>]
//   --since    only sessions that began on or after this day (UTC)
//   --session  only one session and its agents, and each of them with its role, model, size and price. The id is
//              the start of the session's id, as the sessions table prints it
//   --prompts  also list what the user asked, in order (the first 200 characters of each)
//
// Transcripts hold everything said and read in a session. Never copy them, or what --prompts prints, into the repository.
import { execFileSync } from "node:child_process";
import { createReadStream, existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { homedir } from "node:os";
import { basename, dirname, join } from "node:path";
import { createInterface } from "node:readline";
import { fileURLToPath } from "node:url";

const args = process.argv.slice(2);
const opt = name => { const i = args.indexOf(name); return i < 0 ? null : args[i + 1] ?? ""; };
const since = opt("--since"), only = opt("--session"), showPrompts = args.includes("--prompts");
if (only !== null && (!only || only.startsWith("--"))) { console.error("--session needs the start of a session's id, as the sessions table prints it"); process.exit(1); }
const here = dirname(fileURLToPath(import.meta.url));
const common = execFileSync("git", ["-C", here, "rev-parse", "--path-format=absolute", "--git-common-dir"], { encoding: "utf8" }).trim();
const prefix = dirname(common).replace(/[:\\/]/g, "-");   // C:\Users\<name>\EQLPathfinder -> C--Users-<name>-EQLPathfinder
const projects = opt("--dir") || join(homedir(), ".claude", "projects");
if (!existsSync(projects)) { console.error(`No transcripts folder at ${projects}`); process.exit(1); }

const files = [];
for (const d of readdirSync(projects)) {
  if (!d.toLowerCase().startsWith(prefix.toLowerCase())) continue;
  (function walk(dir) {
    for (const n of readdirSync(dir)) {
      const p = join(dir, n);
      if (statSync(p).isDirectory()) walk(p);
      else if (n.endsWith(".jsonl")) files.push(p);
    }
  })(join(projects, d));
}

const BANDS = [[0, 100], [100, 200], [200, 400], [400, 700], [700, Infinity]].map(([a, b]) => ({ a, b, turns: 0, tokens: 0 }));
const size = c => typeof c === "string" ? c.length
  : Array.isArray(c) ? c.reduce((n, b) => n + (b.type === "text" ? (b.text ?? "").length : b.type === "image" ? 4000 : JSON.stringify(b).length), 0) : 0;
const sessions = [], prompts = [], shell = [];
const toolChars = new Map(), toolCalls = new Map(), reads = new Map();
let oneCall = 0, manyCalls = 0;

for (const f of files) {
  const sub = f.includes("subagents");
  const s = { id: basename(f).slice(0, 8), sub, first: "", turns: 0, asks: 0, fresh: 0, read: 0, write: 0, out: 0, max: 0, start: null, end: null,
    role: sub ? "agent" : "lead", feature: "", model: "", begin: 0, rewrote: 0 };
  // Beside a subagent's transcript, <its name>.meta.json says which agent it ran as.
  if (sub) { try { s.role = JSON.parse(readFileSync(f.replace(/\.jsonl$/, ".meta.json"), "utf8")).agentType ?? s.role; } catch { } }
  // Counted per file first: a session that --since leaves out mustn't reach the totals.
  const usage = new Map(), calls = new Map(), tools = new Map(), mine = [];
  const fChars = new Map(), fCalls = new Map(), fReads = new Map(), fShell = [];
  for await (const line of createInterface({ input: createReadStream(f), crlfDelay: Infinity })) {
    if (!line) continue;
    let o; try { o = JSON.parse(line); } catch { continue; }
    if (o.timestamp) { s.start ??= o.timestamp; s.end = o.timestamp; }
    const m = o.message;
    if (!m) continue;
    if (o.type === "assistant" && m.id) {
      if (m.usage) usage.set(m.id, m.usage);   // one reply spans several lines, and the last has its whole usage
      if (m.model && !m.model.startsWith("<")) s.model = m.model;
      const made = (Array.isArray(m.content) ? m.content : []).filter(b => b.type === "tool_use");
      calls.set(m.id, (calls.get(m.id) ?? 0) + made.length);
      for (const b of made) { tools.set(b.id, b); fCalls.set(b.name, (fCalls.get(b.name) ?? 0) + 1); }
    } else if (o.type === "user" && !o.isMeta) {
      const blocks = typeof m.content === "string" ? [{ type: "text", text: m.content }] : Array.isArray(m.content) ? m.content : [];
      for (const b of blocks) {
        if (b.type === "text" && b.text && !/^(<|\[Request interrupted|This session is being continued)/.test(b.text)) {
          const t = b.text.replace(/\s+/g, " ");
          s.asks++; s.first ||= t.slice(0, 90);
          if (s.asks === 1) s.feature = (t.match(/Feature: (\S+)/) ?? [])[1] ?? "";   // a brief's first line (the feature skill)
          if (!sub) mine.push({ at: o.timestamp, id: s.id, t: t.slice(0, 200) });
        }
        if (b.type !== "tool_result") continue;
        const call = tools.get(b.tool_use_id), name = call?.name ?? "?", n = size(b.content);
        fChars.set(name, (fChars.get(name) ?? 0) + n);
        if (name === "Read" && call?.input?.file_path) {
          const k = call.input.file_path.replace(/\\/g, "/").replace(/^.*?EQLPathfinder(-wt\/[^/]+)?\//, "");
          const r = fReads.get(k) ?? { n: 0, chars: 0 };
          r.n++; r.chars += n; fReads.set(k, r);
        }
        if ((name === "Bash" || name === "PowerShell") && n > 6000) fShell.push({ n, id: s.id, cmd: String(call?.input?.command ?? "").replace(/\s+/g, " ").slice(0, 120) });
      }
    }
  }
  if (since && (!s.start || s.start.slice(0, 10) < since)) continue;
  // A session's own file is <id>.jsonl, and its agents' are under <id>\subagents. Nothing else in the path counts.
  const parts = f.split(/[\\/]/), owner = sub ? parts[parts.indexOf("subagents") - 1] : basename(f);
  if (only && !owner.startsWith(only)) continue;
  for (const [n, c] of fChars) toolChars.set(n, (toolChars.get(n) ?? 0) + c);
  for (const [n, c] of fCalls) toolCalls.set(n, (toolCalls.get(n) ?? 0) + c);
  for (const [p, r] of fReads) { const t = reads.get(p) ?? { n: 0, chars: 0 }; t.n += r.n; t.chars += r.chars; reads.set(p, t); }
  shell.push(...fShell);
  for (const u of usage.values()) {
    const fresh = u.input_tokens ?? 0, read = u.cache_read_input_tokens ?? 0, write = u.cache_creation_input_tokens ?? 0, ctx = fresh + read + write;
    // A later turn that wrote most of a large context to the cache anew: the conversation had waited past the cache's
    // five minutes, and paid for all of it again.
    if (!s.turns) s.begin = ctx; else if (write > ctx / 2 && ctx > 60000) s.rewrote += write;
    s.turns++; s.fresh += fresh; s.read += read; s.write += write; s.out += u.output_tokens ?? 0; s.max = Math.max(s.max, ctx);
    const b = BANDS.find(x => ctx / 1000 >= x.a && ctx / 1000 < x.b);
    b.turns++; b.tokens += ctx;
  }
  for (const n of calls.values()) { if (n === 1) oneCall++; else if (n > 1) manyCalls++; }
  prompts.push(...mine);
  sessions.push(s);
}

const M = n => (n / 1e6).toFixed(1).padStart(7) + "M", k = n => Math.round(n / 1000).toString().padStart(6) + "k";
const pct = (a, b) => (b ? Math.round(a / b * 100) : 0).toString().padStart(3) + "%";
const ctxOf = s => s.fresh + s.read + s.write;
// Dollars per million tokens at Anthropic's API prices on 2026-11-18: input, output, cache read (Haiku's cache read is
// a guess at a tenth of its input). A cache write is 1.25 times input. It's a yardstick for comparing sessions, roles
// and models: a subscription's limits weigh tokens their own way, and an older model is priced as today's of its family.
const PRICES = { fable: [8, 40, 0.2], opus: [4, 20, 0.2], sonnet: [2, 10, 0.1], haiku: [0.1, 0.5, 0.01] };
const family = s => Object.keys(PRICES).find(n => s.model.includes(n)) ?? "fable";
const price = s => { const [i, o, r] = PRICES[family(s)]; return (s.fresh * i + s.write * i * 1.25 + s.read * r + s.out * o) / 1e6; };
const usd = n => "$" + n.toFixed(1);
const total = sessions.reduce((a, s) => ({ ctx: a.ctx + ctxOf(s), read: a.read + s.read, write: a.write + s.write, out: a.out + s.out, turns: a.turns + s.turns, asks: a.asks + (s.sub ? 0 : s.asks) }),
  { ctx: 0, read: 0, write: 0, out: 0, turns: 0, asks: 0 });
if (!sessions.length) { console.log(`No sessions${only ? ` whose id starts with ${only}` : ""}${since ? ` since ${since}` : ""} under ${projects}\\${prefix}*`); process.exit(0); }

console.log(`${sessions.filter(s => !s.sub).length} sessions and ${sessions.filter(s => s.sub).length} agents${since ? ` since ${since}` : ""}: ${total.turns} turns for ${total.asks} requests from the user`);
console.log(`context processed${M(total.ctx)}  (re-read from cache${M(total.read)}, new${M(total.write)})   written${M(total.out)}`);
console.log(`at API prices about ${usd(sessions.reduce((a, s) => a + price(s), 0))} (a yardstick, not the bill)`);
console.log(`mean context per turn ${k(total.ctx / total.turns).trim()}; agents' share of the context ${pct(sessions.filter(s => s.sub).reduce((a, s) => a + ctxOf(s), 0), total.ctx).trim()}`);
console.log(`turns that made one tool call: ${oneCall}; several at once: ${manyCalls}`);

console.log("\n== how big the context was when each turn ran ==");
for (const b of BANDS) console.log(`${String(b.a).padStart(4)}k to ${b.b === Infinity ? "    " : String(b.b).padStart(3) + "k"}   turns ${String(b.turns).padStart(5)} ${pct(b.turns, total.turns)}   tokens ${M(b.tokens)} ${pct(b.tokens, total.ctx)}`);

console.log("\n== sessions, largest first (agent = a subagent's own transcript) ==");
console.log("id       kind  turns asks   context  largest  hours  began with");
for (const s of [...sessions].sort((a, b) => ctxOf(b) - ctxOf(a)).slice(0, 20)) {
  const hours = s.start && s.end ? ((new Date(s.end) - new Date(s.start)) / 36e5).toFixed(1) : "?";
  console.log(`${s.id} ${s.sub ? "agent" : "     "} ${String(s.turns).padStart(5)} ${String(s.asks).padStart(4)} ${M(ctxOf(s))} ${k(s.max)} ${hours.padStart(6)}  ${showPrompts || s.sub ? s.first : ""}`);
}

if (only) {
  const sum = (rows, f) => rows.reduce((a, s) => a + f(s), 0), whole = sum(sessions, price);
  const named = s => s.feature || (s.sub ? "-" : "(the lead)");
  const shares = (label, key) => {
    const groups = new Map();
    for (const s of sessions) groups.set(key(s), [...(groups.get(key(s)) ?? []), s]);
    console.log(label + [...groups].sort((a, b) => sum(b[1], price) - sum(a[1], price))
      .map(([n, rows]) => `${n} ${usd(sum(rows, price))}${pct(sum(rows, price), whole)}`).join("  |  "));
  };
  console.log("\n== each agent, in the order they started (size: its context at the end, which is what the harness reports) ==");
  console.log("role       feature                    model   turns  begins    size  context re-wrote    price");
  for (const s of [...sessions].sort((a, b) => String(a.start).localeCompare(String(b.start)))) {
    console.log(`${s.role.padEnd(10)} ${named(s).slice(0, 26).padEnd(26)} ${family(s).padEnd(6)} ${String(s.turns).padStart(6)} ${k(s.begin)} ${k(s.max)} ${M(ctxOf(s))} ${k(s.rewrote)} ${usd(price(s)).padStart(8)}`);
  }
  console.log(`the agents' sizes added up: ${k(sum(sessions.filter(s => s.sub), s => s.max)).trim()} (a branch's budget counts its agents this way, docs/roles.md)`);
  shares("by role:    ", s => s.role);
  shares("by feature: ", named);
}

console.log("\n== what tools put into context (characters, about 4 to a token) ==");
const all = [...toolChars.values()].reduce((a, b) => a + b, 0);
for (const [n, c] of [...toolChars].sort((a, b) => b[1] - a[1]).slice(0, 8)) console.log(`${n.padEnd(22)} ${String(toolCalls.get(n) ?? 0).padStart(5)} calls ${M(c)} ${pct(c, all)}`);

console.log("\n== files read most ==");
for (const [p, r] of [...reads].sort((a, b) => b[1].chars - a[1].chars).slice(0, 15)) console.log(`${String(r.n).padStart(4)} times ${k(r.chars)}  ${p}`);

console.log("\n== largest shell outputs ==");
for (const c of shell.sort((a, b) => b.n - a.n).slice(0, 10)) console.log(`${k(c.n)} ${c.id}  ${c.cmd}`);

if (showPrompts) {
  console.log("\n== what the user asked, in order ==");
  for (const p of prompts.sort((a, b) => String(a.at).localeCompare(String(b.at)))) console.log(`${String(p.at).slice(5, 16)} ${p.id}  ${p.t}`);
}
