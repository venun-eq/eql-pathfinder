// Applies ./migrations to one database:  node migrate.mjs production|staging [more cf arguments, such as --local]
// (npm run migrate, npm run migrate:staging). The database's ID comes from the environment through databaseId, as a
// deploy's does, so a migration stops here, naming the variable, when it wasn't told which database: the cf CLI takes
// only an ID for `d1 migrations apply`, and the repository holds none (docs/specs/private-settings.md).
import { spawnSync } from "node:child_process";
import { databaseId } from "./database-ids.ts";

const [mode, ...rest] = process.argv.slice(2);
if (mode !== "production" && mode !== "staging") {
  console.error("migrate: say which database, production or staging (node migrate.mjs staging).");
  process.exit(1);
}
let id;
try {
  id = databaseId(mode, process.env);
} catch (e) {
  console.error(`migrate: ${e.message}`);
  process.exit(1);
}
// The command goes through a shell (cf is a .cmd on Windows), so the ID must be an ID and nothing a shell would read.
if (!/^[A-Za-z0-9_-]+$/.test(id)) {
  console.error(`migrate: the ${mode} database's ID has characters an ID never has. Nothing was run.`);
  process.exit(1);
}
const run = spawnSync("cf", ["d1", "migrations", "apply", id, "--mode", mode, ...rest], {
  stdio: "inherit",
  shell: true,
});
process.exit(run.status ?? 1);
