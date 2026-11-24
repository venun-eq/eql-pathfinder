// docs/code-map.md is only worth reading while what it names exists. This fails when a file or a name on that page is
// gone from the repository, so a rename comes with its line in the map. Part of tools/check.ps1.
//
// A name is anything in backticks that has no spaces and no placeholder in it. A path must exist (one that starts at
// the repository's root) or be the tail of a file's path (a short form such as `Parsing/LogEvents.cs`). Any other name
// is split at dots and the like, and each part of three letters or more must appear in the source, tests, tools,
// server, site, infra or .claude files, or in a file's path. That it's still in the right file is the reviewer's job.
// Usage: node tools/mapcheck.mjs [another map]     (tools/check.ps1 gives it one that names something made up)
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, statSync } from "node:fs";
import { dirname, extname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const mapPath = process.argv[2] ?? join(root, "docs", "code-map.md");
if (!existsSync(mapPath)) {
  console.log(`mapcheck: no ${mapPath}`);
  process.exit(0);
}

const ROOTS = ["src", "tests", "tools", "server", "site", "infra", ".claude", "docs"];
const TEXT = new Set([
  ".cs",
  ".xaml",
  ".js",
  ".mjs",
  ".ts",
  ".html",
  ".css",
  ".ps1",
  ".json",
  ".sql",
  ".md",
  ".csproj",
  ".slnx",
  ".tf",
  ".bicep",
]);
// Tracked files and new ones not yet added: never node_modules, bin or obj.
const paths = execFileSync(
  "git",
  ["-C", root, "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", ...ROOTS, "CLAUDE.md"],
  { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 },
)
  .split("\0")
  .filter(Boolean);
let corpus = paths.join("\n") + "\n";
for (const p of paths) {
  if (p.startsWith("docs/") || !TEXT.has(extname(p).toLowerCase())) continue; // the docs can't vouch for themselves
  const full = join(root, p);
  if (!existsSync(full) || statSync(full).size > 1024 * 1024) continue;
  corpus += readFileSync(full, "utf8") + "\n";
}

const gone = [];
const lines = readFileSync(mapPath, "utf8").split(/\r?\n/);
lines.forEach((line, i) => {
  for (const [, name] of line.matchAll(/`([^`]+)`/g)) {
    if (/[\s…*<>(){}=|,"'[\]]/.test(name)) continue; // a phrase, a pattern or a placeholder, not a name
    if (name.includes("/")) {
      const path = name.replace(/\/+$/, "");
      const rooted = ROOTS.some(r => path === r || path.startsWith(r + "/"));
      // A path with a host in front (https://preview.local/) falls through to the parts below.
      if (rooted ? !existsSync(join(root, path)) : !path.includes("://") && !paths.some(p => p.includes(path)))
        gone.push(`line ${i + 1}: \`${name}\` (no such path)`);
      if (rooted || !path.includes("://")) continue;
    }
    const parts = name.split(/[^A-Za-z0-9_-]+/).filter(s => s.length >= 3 && !/^[-\d]+$/.test(s));
    const missing = parts.filter(s => !corpus.includes(s));
    if (missing.length) gone.push(`line ${i + 1}: \`${name}\` (${missing.join(", ")} appears nowhere)`);
  }
});
if (gone.length) {
  console.error("mapcheck failed: docs/code-map.md names things that are gone. Update its lines:\n" + gone.join("\n"));
  process.exit(1);
}
console.log(`mapcheck: every file and name in docs/code-map.md exists (${lines.length} lines)`);
