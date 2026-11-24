// Copy the app's database in one consistent read, safe while the app is writing to it: node copydb.mjs <from> <to>
// (copying the .db file and its -wal by hand can catch them mid-write). <to> must not exist yet.
import { DatabaseSync } from "node:sqlite";
const [from, to] = process.argv.slice(2);
if (!to) throw new Error("usage: node copydb.mjs <from> <to>");
const db = new DatabaseSync(from, { readOnly: true });
db.prepare("VACUUM INTO ?").run(to);
console.log(`copied ${from} -> ${to}`);
