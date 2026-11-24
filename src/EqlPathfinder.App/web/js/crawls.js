"use strict";
// ---------------------------------------------------------------- crawls
let sortKey = "start", sortDir = -1;
// How each visit's trio is known (the log never says when a loadout changes, so spells check the carried one).
const trioHow = {
  who: "Shown by /who during this visit.",
  spells: "Checked against the spells you cast here.",
  partial: "* Worked out from the spells you cast here; other trios would fit them too.",
  slot: v => `Your last /who on loadout ${v.slot}; Pathfinder saw you swap to it.`,
  unmet: "Played on a loadout Pathfinder hadn't met; no /who named it.",
  carried: "Carried over from your last /who or reincarnation; nothing cast here to check it by.",
};
/** A visit's whole time as a player reads it: "14 min" under an hour, "1 h 07" from one. */
function clockWords(m) {
  const r = Math.round(m ?? 0);
  return r < 60 ? `${r} min` : `${Math.floor(r / 60)} h ${String(r % 60).padStart(2, "0")}`;
}
/** Where a visit's minutes went, from the engine's whole and four parts (the page works out none of them). */
function clockTip(v) {
  const head = v.zonedOut == null ? `Zoned in ${hm(v.start)}, still inside: ${fmt(v.wholeMinutes)} min so far`
    : v.state === "paused" ? `Zoned in ${hm(v.start)}, out ${hm(v.zonedOut)}, not back yet: ${fmt(v.wholeMinutes)} min so far`
    : `Zoned in ${hm(v.start)}, out ${hm(v.zonedOut)}: ${fmt(v.wholeMinutes)} min`;
  return [head, `Active ${fmt(v.activeMinutes)}`, `Idle ${fmt(v.idleMinutes)}`,
    ...(v.kind === "raid" ? [] : [`After it completed (looting, walking out) ${fmt(v.afterMinutes)}`]),
    `Outside the instance (a death, an errand) ${fmt(v.outsideMinutes)}`].join("\n");
}
/** A visit that starts and ends at the cap: the bar fills there but no level follows, so Gained and Lv/h do not apply (empty, not 0). */
const atCapVisit = v => v.levelStart != null && v.levelStart >= cap() && (v.levelEnd ?? v.levelStart) >= cap();

/** The kinds of visit the engine judges by (VisitKinds), and what each holds. A visit with no kind is in no pace or rate. */
const KINDS = [
  ["all", "All", () => `Every visit your search and "Hide visits too short to judge" let through.`],
  ["leveling", "Leveling", () => `Judged by levels per hour, for crawls below ${cap()} at difficulties you don't farm.`],
  ["farming", "Farming", () => `Judged by T5-eq per hour, for your farmed difficulties (${farmWords()}; Settings → How you play), instances you left before they completed, and everything at ${cap()}.`],
  ["grouped", "Grouped", () => "Played in a group: judged by T5-eq per hour, against your other grouped visits."],
  ["raid", "Raids", () => "Raid instances (- Solo, - Group): judged by their boss."],
  ["none", "Not judged", () => "In no pace or rate."],
];
let visitKind = store.get("visitKind", "all");
if (!KINDS.some(k => k[0] === visitKind)) visitKind = "all";
const visitKindOf = v => v.kind ?? "none";
const COLS = [
  { k: "start", t: "Start", l: true, f: v => dt(v.start) },
  { k: "wholeMinutes", t: "Time", tip: "From zoning in to zoning out. Hover a visit for where its minutes went.", f: v => `<span title="${esc(clockTip(v))}">${clockWords(v.wholeMinutes)}</span>` },
  { k: "trio", t: "Trio", l: true, f: v => `<span title="${esc(typeof trioHow[v.trioSource] === "function" ? trioHow[v.trioSource](v) : trioHow[v.trioSource] ?? "")}">${esc(v.trio || "?")}${v.trioSource === "partial" ? '<span class="dim">*</span>' : ""}</span>`
      + (v.race ? ` <span class="dim">${esc(v.race)}</span>` : "") },
  { k: "baseZone", t: "Zone", l: true, f: v => esc(zoneShort(v.baseZone, v.difficulty)) },
  { k: "levelStart", t: "Levels", f: v => v.levelStart != null ? `${v.levelStart}→${v.levelEnd ?? ""}` : "" },
  { k: "levelsGained", t: "Gained", f: v => atCapVisit(v) ? "" : fmt(v.killLevels + v.rewardLevels, 2) + (v.rewardSizing ? ' <span class="gold">+R?</span>' : v.rewardEstimated ? ' <span class="dim">~</span>' : "") },
  { k: "levelsPerHour", t: "Lv/h", get tip() { return `Levels an active hour (time inside, idle taken out). The rate: ${rateRule()}`; }, f: v => atCapVisit(v) ? "" : rateCell(v.levelsPerHour) },
  { k: "pace", t: "Verdict", l: true, get tip() { return `Judged by what you played. ${farmWords()} (your farmed difficulties), grouped play, instances you did not finish and play at ${cap()}: T5-eq per active hour against your usual of the same kind there. The rest is leveling: kill speed against your usual (on pace is within one run's normal swing), and another place at the same difficulty that levels faster, by your crawls before that one. Raids: the boss.`; },
    f: v => `<span class="${farmWaiting ? "stale" : ""}">${paceChips(v)}</span>` },
  { k: "t5", t: "T5", f: v => fmt(v.t5) },
  { k: "t5PerHour", t: "T5/h", get tip() { return `T5-eq an active hour. The rate: ${rateRule()}`; }, f: v => rateCell(v.t5PerHour, 0, "gold") },
  { k: "highTierT5", t: "Above T5", f: v => v.highTierT5 > 0 ? fmt(v.highTierT5) : "" },
  { k: "chestT5", t: "Chest", f: v => v.chestT5 > 0 ? fmt(v.chestT5) : "" },
  { k: "kills", t: "Kills", f: v => v.kills },
  { k: "killsPerMinute", t: "K/min", get tip() { return `Kills a minute of active time. The rate: ${rateRule()}`; }, f: v => rateCell(v.killsPerMinute) },
  { k: "killSpeed", t: "Kill speed", get tip() { return `Kills a minute against your usual at the same place and level, solo or group the same, from ${usualRuns()} or more runs.`; },
    // A small bar against a tick for your usual (the tick sits at two thirds, so up to 1.5× fits).
    f: v => v.killSpeed != null ? `<span class="ksbar"><i style="width: ${Math.min(100, v.killSpeed / 1.5 * 100).toFixed(0)}%; background: ${v.pace === "slow" ? "var(--warn)" : "var(--good)"}"></i><b></b></span>`
      + `<span class="${v.pace === "slow" ? "warn" : "good"}">${fmt(v.killSpeed * 100, 0)}%</span>` : "" },
  { k: "dps", t: "DPS", get tip() { return `Damage a second of active time, idle taken out. The fight meter counts only seconds in a fight, so its DPS is higher. The rate: ${rateRule()}`; }, f: v => rateCell(v.dps, 0) },
  { k: "medianKillSeconds", t: "Kill s", f: v => fmt(v.medianKillSeconds, 0) },
  { k: "activeMinutes", t: "Active", f: v => fmt(v.activeMinutes) },
  { k: "idleMinutes", t: "Idle", f: v => v.idleMinutes >= 0.5 ? fmt(v.idleMinutes) : "" },
  { k: "resetMinutes", t: "Reset", f: v => fmt(v.resetMinutes) },
  { k: "deaths", t: "Deaths", f: v => v.deaths ? `<span class="warn">${v.deaths}</span>` : "" },
  { k: "completed", t: "Done", tip: "The instance said it was completed. A raid never says so: it's done when its boss dies.",
    f: v => v.kind === "raid" ? (v.boss ? `<span class="good" title="Its boss died: ${esc(v.boss)}">✓</span>` : "") : v.completed ? '<span class="good">✓</span>' : "" },
];
// A leveling crawl like for like (docs/pace.md): kill speed against your usual there, a low XP per kill for that level, and
// another place clearly faster at that level. The run's own numbers stay as they happened.
// A visit with no rate is "too short", at any level (a raid never gets here: its verdict is its boss).
function noRateChip(v, pre = "") {
  return `<span class="dim" title="${esc(noRateWhy())}">${pre}too short</span>`;
}
function paceChips(v) {
  const out = [];
  // A raid is judged by its boss; farming and grouped play by motes/h; leveling by levels/h.
  if (v.kind === "raid") return v.boss ? `<span class="good">${esc(v.boss)} killed</span>` : '<span class="dim">No boss kill</span>';
  if (v.farmVerdict) {
    const pre = v.kind === "grouped" ? "Grouped · " : "", pct = fmt(Math.abs(v.farmPct ?? 0) * 100, 0);
    const tip = `T5-eq per active hour against your usual at ${zoneShort(v.baseZone, v.difficulty)}: ${v.farmBasis ?? "visits like this one"}`;
    if (v.farmVerdict === "more") return `<span class="good" title="${esc(tip)}">${pre}${pct}% more T5-eq/h</span>`;
    if (v.farmVerdict === "less") return `<span class="warn" title="${esc(tip)}">${pre}${pct}% less T5-eq/h</span>`;
    if (v.farmVerdict === "usual") return `<span class="good" title="${esc(tip)}">${pre}Usual T5-eq/h</span>`;
    return v.t5PerHour == null ? noRateChip(v, pre)
      : `<span class="dim" title="Under ${usualRuns()} visits like this one here, or too little of it yet">${pre}no usual yet</span>`;
  }
  if (v.pace === "pace") out.push('<span class="good">On pace</span>');
  else if (v.pace === "slow") out.push(`<span class="warn">${fmt((1 - v.killSpeed) * 100, 0)}% slow</span>`);
  else if (v.pace === "fast") out.push(`<span class="good">${fmt((v.killSpeed - 1) * 100, 0)}% fast</span>`);
  else if (v.pace === "none") out.push(v.levelsPerHour == null ? noRateChip(v)
    : `<span class="dim" title="Under ${usualRuns()} runs here near that level, solo or group the same">no usual yet</span>`);
  if (v.xpVerdict === "low") out.push('<span class="warn" title="Less XP per kill than this place gives at that level">low XP per kill</span>');
  if (v.placeVerdict) out.push(`<span class="gold" title="${esc(v.betterPlace ?? "")} levels faster than this place at the same difficulty, at that level, by your crawls before this one">${v.placeVerdict === "fading" ? "Fading · " : ""}${esc(v.betterPlace ?? "Another place")} levels faster</span>`);
  return out.join(" · ");
}
/**
 * Header rows for rows in time order: one where each relevel's crawls start ("Relevel 6 · SHD/ROG/BER · +20% XP" and how
 * it went), and one over play outside a relevel. A map from the first row of each stretch to its header. The numbers are the whole
 * relevel's; <paramref name="counts"/> (stretch → visits the boxes let through, and how many of them the search shows) says when it hides some.
 */
function relevelGroups(rows, counts) {
  const js = (data.reinc?.journeys ?? []).slice().sort((a, b) => new Date(a.start) - new Date(b.start));
  // The engine says which stretch each visit is in (a relevel, at the cap, leveling outside one) and works out its numbers.
  const stretches = data.reinc?.stretches ?? [];
  const heads = new Map();
  let last = null;
  for (const v of rows) {
    if (v.stretch === last) continue;
    last = v.stretch;
    const st = stretches[v.stretch];
    if (!st) continue;
    const span = COLS.length;
    if (st.relevel == null) { heads.set(v, `<tr class="grp"><td colspan="${span}"><b>${st.kind === "cap" ? `At ${cap()}` : "Leveling outside a relevel"}</b></td></tr>`); continue; }
    const j = js[st.relevel];
    const parts = [`<b>Relevel ${st.relevel + 1} · ${esc(j.trio)}</b>${j.rank > 0 ? ` · ${xpWords(xpOf(j.rank))}` : ""}${j.done ? "" : " · now"}`];
    // What the relevel was: read from where it was played. Its motes an hour, against your other relevels of the same kind.
    if (j.mostly && j.activeHours > 0) {
      // The engine gives the relevel's T5-eq/h and the typical of its kind (your other finished ones, 2 or more).
      parts[0] += ` · ${j.mostly === "mixed" ? "mixed" : `mostly ${esc(j.mostly)}`} · ${fmt(j.t5PerHour, 0)} T5-eq/h`
        + (j.typicalOfKind != null ? ` (your ${j.mostly === "mixed" ? "mixed" : esc(j.mostly)} relevels: ${fmt(j.typicalOfKind, 0)} T5-eq/h typical)` : "");
    }
    if (st.oldLevelsPerHour != null)
      parts.push(`levels/h ${st.fell ? "fell" : "rose"} from ${fmt(st.oldLevelsPerHour)} to ${fmt(st.newLevelsPerHour)}`
        + (st.perLevelPercent != null ? `, about ${st.perLevelPercent}% a level (every place falls 4–7%)` : ""));
    const kp = st.killPace;
    parts.push(kp.speed == null ? "no usual yet to judge kill speed" : `kill speed ${fmt(kp.speed * 100, 0)}% of your usual over ${plural(kp.runs, "run")}: <span class="${kp.tone}">${kp.word.toLowerCase()}</span>`);
    const c = counts?.get(v.stretch);
    const cue = c && c.shown < c.all ? ` <span class="dim">· numbers for the whole relevel; your search shows ${c.shown} of its ${plural(c.all, "visit")}</span>` : "";
    heads.set(v, `<tr class="grp"><td colspan="${span}"><div class="grpline">${parts.join(" · ")}${cue}</div></td></tr>`);
  }
  return { heads };
}
["q", "hideShort"].forEach(id => $("#" + id).addEventListener("input", renderCrawls));
$("#visitKindSeg").addEventListener("click", e => {
  const b = e.target.closest("button");
  if (!b) return;
  visitKind = b.dataset.v; store.set("visitKind", visitKind);
  renderCrawls();
});
$("#crawlTable thead").addEventListener("click", e => {
  const th = e.target.closest("th[data-k]");
  if (!th) return;
  if (sortKey === th.dataset.k) sortDir = -sortDir; else { sortKey = th.dataset.k; sortDir = -1; }
  renderCrawls();
});

function renderCrawls() {
  if (!data) return;
  const q = $("#q").value.trim().toLowerCase();
  const short = v => !$("#hideShort").checked || !v.tooShort;   // the engine says which visits are too short to judge (no rate, not a raid)
  const kindOk = v => visitKind === "all" || visitKindOf(v) === visitKind;   // the engine's kind: the rule Verdict uses
  const boxes = v => short(v) && kindOk(v);
  const matches = v => !q || `${v.baseZone} d${v.difficulty} ${v.trio} ${v.race}`.toLowerCase().includes(q);
  const rows = (data.history ?? []).filter(v => boxes(v) && matches(v));
  // One button a kind, its count the visits of that kind the search and the box let through; a kind with none is left out unless chosen.
  const byKind = {}, counted = (data.history ?? []).filter(v => short(v) && matches(v));
  for (const v of counted) byKind[visitKindOf(v)] = (byKind[visitKindOf(v)] ?? 0) + 1;
  const seg = $("#visitKindSeg");
  seg.classList.toggle("stale", farmWaiting);
  seg.innerHTML = KINDS.filter(([k]) => k === "all" || k === visitKind || byKind[k]).map(([k, name, tip]) =>
    `<button data-v="${k}" class="${k === visitKind ? "on" : ""}" aria-pressed="${k === visitKind}" title="${esc(tip())}">${name} <span class="dim">${k === "all" ? counted.length : byKind[k] ?? 0}</span></button>`).join("");
  // A relevel header's numbers are the whole relevel's: with a search typed, how many of its visits the search shows.
  const counts = new Map();
  if (q) for (const v of data.history ?? []) if (boxes(v)) { const c = counts.get(v.stretch) ?? { all: 0, shown: 0 }; c.all++; if (matches(v)) c.shown++; counts.set(v.stretch, c); }
  // Visits the box hides, so an empty list can say why (the engine's tooShort).
  const hiddenShort = $("#hideShort").checked ? (data.history ?? []).filter(v => v.tooShort && kindOk(v) && matches(v)).length : 0;
  const sum = f => rows.reduce((a, v) => a + (f(v) || 0), 0);
  const act = sum(v => v.activeMinutes), t5 = sum(v => v.t5);
  // The tile is the rate over the shown visits that have one (the engine's null says which): none, no rate.
  const rated = rows.filter(v => v.t5PerHour != null), ratedAct = rated.reduce((a, v) => a + (v.activeMinutes || 0), 0);
  const tileRate = rated.length && ratedAct > 0 ? rated.reduce((a, v) => a + (v.t5 || 0), 0) * 60 / ratedAct : null;
  $("#crawlTiles").innerHTML = [
    [rows.length, "visits shown"],
    [fmt(act / 60, 1) + " h", "active time"],
    [fmt(sum(v => v.killLevels + v.rewardLevels), 1), "levels gained"],
    [tileRate == null || !isFinite(tileRate) ? `<span class="dim">—</span>` : `<span class="gold">${fmt(tileRate, 0)}</span>`, tileRate == null ? `T5-eq per active hour. The rate: ${rateRule()}` : "T5-eq per active hour"],
    [fmt(t5, 0), "T5-eq looted in these"],
    [fmt(sum(v => v.highTierT5), 0), "of it above T5"],
  ].map(([v, k]) => `<div class="stat"><div class="v num">${v}</div><div class="k">${k}</div></div>`).join("");
  rows.sort((a, b) => { const x = a[sortKey], y = b[sortKey]; return (x == null) - (y == null) || (x < y ? -1 : x > y ? 1 : 0) * sortDir; });
  $("#crawlTable thead").innerHTML = `<tr>${COLS.map(c => `<th data-k="${c.k}"${c.tip ? ` title="${esc(c.tip)}"` : ""} class="sortable ${c.l ? "l " : ""}${c.k === sortKey ? "sorted" + (sortDir > 0 ? " asc" : "") : ""}">${c.t}</th>`).join("")}</tr>`;
  // In time order, each relevel's crawls sit under a line saying how it went, like for like (docs/pace.md).
  const shown = rows.slice(0, 2000), groups = sortKey === "start" ? relevelGroups(shown, counts) : null;
  $("#crawlTable tbody").innerHTML = shown.length
    ? shown.map(v => `${groups?.heads.get(v) ?? ""}<tr>${COLS.map(c => `<td class="${c.l ? "l" : ""}">${c.f(v)}</td>`).join("")}</tr>`).join("")
    : `<tr><td class="empty" colspan="${COLS.length}">${hiddenShort ? `${plural(hiddenShort, "visit")} too short to judge ${hiddenShort === 1 ? "is" : "are"} hidden: ${esc(rateRule())}` : "No visits match."}</td></tr>`;
  $("#crawlCount").textContent = `${rows.length} of ${(data.history ?? []).length}`;
}

