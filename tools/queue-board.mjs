// The work queue as a board. docs/queue.md is the source; this only reads it.
//   node tools/queue-board.mjs <out.html>        writes one self-contained page (tools/queue-board/template.html)
//   node tools/queue-board.mjs --check [file]    parses only; exit 1 naming the problem (tools\check.ps1 runs it)

import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const LANES = [
  ["progress", "In progress"],
  ["now", "Now"],
  ["next", "Next"],
  ["later", "Later"],
  ["waiting", "Waiting on the user"],
  ["parked", "Parked"],
  ["inbox", "Inbox"],
  ["dropped", "Dropped"],
];
const COLS = 8; // #, Row, What the player gets, From, Tier/budget, Waits for, P, You

const split = line =>
  line
    .trim()
    .replace(/^\||\|$/g, "")
    .split(/(?<!\\)\|/)
    .map(c => c.replace(/\\\|/g, "|").trim());
const list = (s, sep) =>
  s
    .split(sep)
    .map(x => x.trim())
    .filter(Boolean);

function parseQueue(text) {
  const problems = [];
  const lines = text.replace(/\r/g, "").split("\n");
  const sections = {};
  let cur = null;
  for (const l of lines) {
    const h = /^## (.+?)\s*$/.exec(l);
    if (h) {
      cur = h[1];
      sections[cur] = [];
    } else if (cur) sections[cur].push(l);
  }
  const lanes = {},
    seen = new Map();
  for (const [id, label] of LANES) {
    lanes[id] = [];
    const body = sections[label];
    if (!body) {
      problems.push(`the lane heading "## ${label}" is missing`);
      continue;
    }
    let header = false,
      n = 0;
    // A table holds the rows. Bullets are rows only in Parked or Dropped with no table; elsewhere they are notes.
    const hasTable = body.some(l => l.trim().startsWith("|"));
    const bulletLane = id === "parked" || id === "dropped";
    const hasBullets = bulletLane && body.some(l => /^[-*] /.test(l.trim()));
    if (!hasTable && !hasBullets && !body.some(l => /^empty\b/i.test(l.trim())))
      problems.push(`${label} has no table and no "Empty." line`);
    for (const l of body) {
      const t = l.trim();
      if (t.startsWith("|")) {
        const c = split(t);
        if (!header) {
          header = true;
          if (c.length !== COLS) problems.push(`${label}: the table header has ${c.length} columns, expected ${COLS}`);
          continue;
        }
        if (c.every(x => /^:?-{2,}:?$/.test(x))) continue;
        n++;
        if (c.length !== COLS) {
          problems.push(`${label} row ${n} has ${c.length} cells, expected ${COLS}`);
          continue;
        }
        addRow(id, label, n, c[1], {
          num: c[0],
          player: c[2],
          from: list(c[3], ";"),
          tier: c[4],
          waits: list(c[5], ","),
          p: c[6],
          you: c[7],
        });
      } else if (/^[-*] /.test(t) && !hasTable && bulletLane) {
        n++;
        const m = /^[-*] (.+?)(?:\s+[-–:]\s+(.*))?$/.exec(t);
        addRow(id, label, n, m ? m[1] : "", {
          num: String(n),
          player: m?.[2] || "",
          from: [],
          tier: "",
          waits: [],
          p: "",
          you: "",
        });
      }
    }
  }
  for (const [h, body] of Object.entries(sections)) {
    if (!LANES.some(([, label]) => label === h) && body.some(l => l.trim().startsWith("|")))
      problems.push(`the table under "## ${h}" is not a lane, so its rows would be dropped`);
  }
  function addRow(id, label, n, rawName, r) {
    const link = /^\[(.+?)\]\((.+?)\)$/.exec(rawName);
    const name = (link ? link[1] : rawName).trim();
    if (!name) {
      problems.push(`${label} row ${n} has no name`);
      return;
    }
    if (seen.has(name)) problems.push(`the row name "${name}" is in ${seen.get(name)} and ${label}`);
    seen.set(name, label);
    lanes[id].push({ name, spec: link ? link[2] : "", ...r, lane: id });
  }
  return { lanes, problems };
}

const textHash = text => createHash("sha1").update(text.replace(/\r/g, "")).digest("hex").slice(0, 8);

function gitInfo() {
  try {
    const [hash, date] = execFileSync("git", ["-C", root, "log", "-1", "--format=%h %cs", "--", "docs/queue.md"], {
      encoding: "utf8",
    })
      .trim()
      .split(" ");
    return { hash, date };
  } catch {
    return { hash: "unknown", date: "" };
  }
}

function frontMatter(text) {
  const m = /^---\r?\n([\s\S]*?)\r?\n---\r?\n?([\s\S]*)$/.exec(text);
  const fm = {};
  if (m)
    for (const l of m[1].split(/\r?\n/)) {
      const k = /^(\w+):\s*(.*)$/.exec(l);
      if (k) fm[k[1]] = k[2].trim();
    }
  return { fm, body: m ? m[2] : text };
}

function onMain() {
  const dir = path.join(root, "docs/releases/next");
  if (!fs.existsSync(dir)) return [];
  return fs
    .readdirSync(dir)
    .filter(f => f.endsWith(".md") && f !== "README.md")
    .sort()
    .map(f => {
      const { fm, body } = frontMatter(fs.readFileSync(path.join(dir, f), "utf8"));
      return { name: f.replace(/\.md$/, ""), title: fm.title || body.trim().split("\n")[0] || f };
    });
}

function shipped() {
  const dir = path.join(root, "docs/releases");
  const vs = fs
    .readdirSync(dir)
    .filter(f => /^\d+\.\d+\.\d+\.md$/.test(f))
    .map(f => f.replace(/\.md$/, ""))
    .sort((a, b) => {
      const x = a.split(".").map(Number),
        y = b.split(".").map(Number);
      return x[0] - y[0] || x[1] - y[1] || x[2] - y[2];
    });
  return vs
    .slice(-3)
    .reverse()
    .map(v => {
      const { fm, body } = frontMatter(fs.readFileSync(path.join(dir, v + ".md"), "utf8"));
      const head = body.trim().split("\n")[0] || "";
      return { version: v, date: fm.date || "", titles: list(head.trim().replace(/\.$/, ""), ";") };
    });
}

const args = process.argv.slice(2);
const checking = args[0] === "--check";
const queueFile = checking && args[1] ? args[1] : path.join(root, "docs/queue.md");
const queueText = fs.readFileSync(queueFile, "utf8");
const { lanes, problems } = parseQueue(queueText);

function fail(list) {
  console.error("queue-board: " + list.join("\nqueue-board: "));
  process.exit(1);
}
if (problems.length) fail(problems);

if (checking) {
  const total = Object.values(lanes).reduce((s, a) => s + a.length, 0);
  console.log(`queue-board: ${total} rows in ${LANES.length} lanes parse`);
  if (!args[1]) {
    // The page itself: every row inlined once, and its script parses.
    const page = build();
    const inline = /<script>([\s\S]*)<\/script>/.exec(page);
    try {
      new Function(inline[1]);
    } catch (e) {
      fail([`the page's script doesn't parse: ${e.message}`]);
    }
    const data = JSON.parse(/\/\*DATA\*\/(.*?)\/\*END\*\//s.exec(page)[1]);
    const rows = Object.values(data.lanes).reduce((s, a) => s + a.length, 0);
    if (rows !== total) fail([`the page holds ${rows} rows, queue.md has ${total}`]);
  }
  process.exit(0);
}

function build() {
  const git = gitInfo();
  const data = {
    source: textHash(queueText),
    sourceCommit: git.hash,
    sourceDate: git.date,
    lanes,
    onMain: onMain(),
    shipped: shipped(),
  };
  const json = JSON.stringify(data)
    .split("<")
    .join(String.fromCharCode(92) + "u003c"); // no "</script>" inside the page
  const tpl = fs.readFileSync(path.join(root, "tools/queue-board/template.html"), "utf8");
  return tpl.replace("/*DATA*/null/*END*/", () => `/*DATA*/${json}/*END*/`);
}

if (!args[0]) fail(["usage: node tools/queue-board.mjs <out.html> | --check [file]"]);
fs.writeFileSync(args[0], build());
console.log(`queue-board: wrote ${args[0]}`);
