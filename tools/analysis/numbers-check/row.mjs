// Print one History row's numbers: node row.mjs <history.json> <visit id>
import fs from "node:fs";
const h = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const r = h.find(r => r.id === process.argv[3]);
const k = ["start", "zone", "levelStart", "levelEnd", "zonedOut", "minutes", "idleMinutes", "activeMinutes", "wholeMinutes", "afterMinutes", "outsideMinutes", "kills", "xpKills", "killLevels", "rewardLevels", "levelsGained", "levelsPerHour", "t5", "chestT5", "highTierT5", "t5PerHour", "killsPerMinute", "medianKillSeconds", "damage", "dps", "deaths", "completed", "refunded", "raid", "boss", "kind", "tooShort", "farmVerdict", "farmUsual", "farmRuns", "farmPct", "farmBand", "pace", "killSpeed", "xpVerdict"];
console.log("APP: " + k.map(x => x + "=" + (typeof r[x] === "number" ? +r[x].toFixed(4) : r[x])).join(" "));
