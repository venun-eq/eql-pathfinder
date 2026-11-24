"use strict";
// ---------------------------------------------------------------- roster
let rosterSel = null, editingCounts = false;
/** A tile in "set counts" mode: tap adds one, − takes one away. The total includes what the log saw. */
function countTile(kind, x, label, sub, small) {
  const key = `${kind}:${x.key}`;
  return `<div class="tapwrap"><button class="tile editing${x.rank > 0 ? " has" : ""}" data-count="${esc(key)}" data-to="${x.rank + 1}"
      aria-label="${esc(sub || label)}: ${x.rank} so far. Add one."${small ? ' style="padding: 8px 10px"' : ""}>
      <div class="between" style="gap: 4px"><b class="ellip" style="font-size: ${small ? 12.5 : 15}px">${esc(label)}</b><span class="${x.rank > 0 ? "gold" : "dim"} num" style="font-weight: 700">×${x.rank}</span></div>
      ${sub ? `<div class="dim" style="font-size: 12px">${esc(sub)}</div>` : ""}
      <div style="margin-top: 4px; font-size: 11px" class="${x.entered != null ? "gold" : "dim"}">${x.entered != null ? "set by you" : x.logged > 0 ? "from your log" : "tap to add"}</div></button>
    ${x.rank > 0 ? `<button class="minus" data-count="${esc(key)}" data-to="${x.rank - 1}" aria-label="${esc(sub || label)}: one fewer">−</button>` : ""}</div>`;
}

function renderCountsBanner(r) {
  const anyEntered = [...r.classes, ...r.races].some(x => x.entered != null);
  $("#editCounts").hidden = editingCounts;
  if (editingCounts) {
    $("#countsBanner").innerHTML = `<div class="banner"><div><div style="font-weight: 650">Set how many times each has reincarnated so far</div>
      <div class="muted" style="font-size: 12px; margin-top: 2px">Include any from while your log was off. Tap a tile to add one, − to take one away. Reincarnations your log sees from now on add to these.</div></div>
      <div class="row">${anyEntered ? `<button class="btn" id="countsReset">Just my log</button>` : ""}<button class="btn primary" id="countsDone">Done</button></div></div>`;
    return;
  }
  // Home, Campaign and Ask show the same banner while it's up (countsNudge in core.js).
  $("#countsBanner").innerHTML = ranksUnconfirmed(r) ? countsNudge(r) : "";
}
/** Any "Set counts" (countsNudge, on any page): Roster with its counts open. */
function openCounts() {
  editingCounts = true;
  if (page !== "roster") show("roster");
  renderRoster();
  $("#countsDone")?.focus();
}
document.addEventListener("click", e => { if (e.target.closest("[data-set-counts]")) openCounts(); });

function renderRoster() {
  const r = data.reinc;
  if (!r) return;
  const act = activeJourney(), busy = new Set(act ? act.trioKey.split("/") : []);
  if (!rosterSel) rosterSel = (r.classes.find(c => c.rank === 0) ?? r.classes[0])?.key;
  renderCountsBanner(r);
  const done = r.classes.filter(c => c.rank > 0).length, never = r.classes.filter(c => c.rank === 0).map(c => c.key);
  $("#classCount").innerHTML = `<b style="color: var(--text)">${done}</b> of ${r.classes.length} reincarnated${never.length ? ` · Completionist needs ${never.join(", ")}` : " · Completionist ✓"}`;
  if (editingCounts) {
    $("#classTiles").innerHTML = r.classes.map(c => countTile("class", c, c.key, c.name)).join("");
    $("#raceTiles").innerHTML = r.races.map(x => countTile("race", x, x.name, null, true)).join("");
    $("#raceCount").innerHTML = `<b style="color: var(--text)">${r.races.filter(x => x.rank > 0).length}</b> of ${r.races.length} reincarnated`;
    $("#rosterDetail").innerHTML = rosterDetail(r, act);
    return;
  }
  $("#classTiles").innerHTML = r.classes.map(c => {
    const now = busy.has(c.key), lv = c.level;
    const status = c.rank === 0 ? (lv != null ? `at ${lv} · never reincarnated` : "never reincarnated")
      : now ? `leveling now · ${lv ?? "?"}` : lv != null ? (lv >= cap() ? `at ${cap()}` : `parked at ${lv}`) : "level unknown";
    const color = c.rank === 0 ? "var(--warn)" : now ? "var(--gold)" : "var(--muted)";
    const barColor = c.rank === 0 ? "var(--warn)" : now ? "var(--gold)" : lv >= cap() ? "var(--good)" : "var(--muted)";
    return `<button class="tile${c.rank === 0 ? " never" : now ? " now" : ""}${c.key === rosterSel ? " sel" : ""}" data-pick="${c.key}" aria-pressed="${c.key === rosterSel}">
      <div class="between"><b style="font-size: 15px">${c.key}</b>${pips(c.rank)}</div>
      <div class="dim" style="font-size: 12px">${esc(c.name)}</div>
      <div class="mini"><i style="width: ${lv != null ? Math.min(100, lv / cap() * 100) : 0}%; background: ${barColor}"></i></div>
      <div style="margin-top: 5px; font-size: 11.5px; color: ${color}">${status}</div></button>`;
  }).join("");
  const racesDone = r.races.filter(x => x.rank > 0).length;
  $("#raceCount").innerHTML = `<b style="color: var(--text)">${racesDone}</b> of ${r.races.length} reincarnated`;
  $("#raceTiles").innerHTML = r.races.map(x => {
    const now = act && act.race === x.key, lv = x.level;
    const status = now ? `leveling now · ${act.levelNow}` : x.rank > 0 ? `rank ${x.rank}${lv != null ? " · " + lv : ""}` : lv != null ? `at ${lv} · not yet` : "not played";
    const color = now ? "var(--gold)" : x.rank > 0 ? "var(--muted)" : lv != null ? "var(--warn)" : "var(--faint)";
    return `<button class="tile${x.rank === 0 ? " dark" : ""}${now ? " now" : ""}${x.key === rosterSel ? " sel" : ""}" data-pick="${esc(x.key)}" aria-pressed="${x.key === rosterSel}" style="padding: 8px 10px">
      <div class="between" style="gap: 4px"><b class="ellip" style="font-size: 12.5px">${esc(x.name)}</b>${pips(x.rank)}</div>
      <div style="margin-top: 3px; font-size: 11px; color: ${color}">${status}</div></button>`;
  }).join("");
  $("#rosterDetail").innerHTML = rosterDetail(r, act);
}

/** Where a rank comes from when you set it: your count, plus what the log saw after you set it. */
function rankSource(x) {
  if (x.entered == null) return "";
  const after = x.rank - x.entered;
  return `<div class="dim" style="font-size: 12px; margin-top: 2px">You set ${x.entered} on ${dt(x.enteredAt).split(",")[0]}${after > 0 ? `, and your log has shown ${after} since` : ""}.
    <button class="link" id="editCounts2">Change</button></div>`;
}

function rosterDetail(r, act) {
  const c = r.classes.find(x => x.key === rosterSel);
  const race = c ? null : r.races.find(x => x.key === rosterSel);
  if (!c && !race) return "";
  const relevels = js => js.length
    ? js.map(j => `${esc(j.trio)} ${esc(j.race ?? "")}: ${j.done ? fmt(j.activeHours) + " h" + (j === finishedJourneys()[0] ? ", your fastest" : "") : "leveling now, " + j.levelNow}`).join("<br>")
    : "Not in a relevel since reincarnation began.";
  if (c) {
    const js = r.journeys.filter(j => j.trioKey.split("/").includes(c.key));
    const lastNever = r.classes.filter(x => x.rank === 0).length === 1;
    return `
      <div><div class="lbl">Class</div><div style="font-size: 22px; font-weight: 650; margin-top: 3px">${esc(c.name)} (${c.key})</div>
        <div class="muted num">${c.rank ? "rank " + c.rank : "not reincarnated yet"}${c.level != null ? " · level " + c.level : ""}${c.reincarnated ? " · reincarnated " + dt(c.reincarnated) : ""}</div>
        ${rankSource(c)}</div>
      <div class="inset"><div class="lbl">Reincarnation passive</div><div style="margin-top: 6px; line-height: 1.45; color: var(--text2)">${esc(c.passive || "Not in your game files yet.")}</div></div>
      <div><div class="lbl">In your relevels</div><div style="margin-top: 6px; line-height: 1.55; color: var(--soft)">${relevels(js)}</div></div>
      <div class="dim" style="font-size: 12px; line-height: 1.45; margin-top: auto">${c.rank === 0
        ? `Reincarnating it adds rank 1${lastNever ? " and completes Completionist" : ""}.`
        : `Next reincarnation: rank ${c.rank + 1}${c.rank >= 3 ? ", past its passive's 3 ranks: it adds XP to level and no passive" : ""}. A trio and its race relevel at their highest rank, so that relevel needs at least ${xpWords(xpOf(c.rank + 1))}: every level takes that much more XP (mobs give the same; the bar is bigger).`}</div>`;
  }
  const js = r.journeys.filter(j => j.race === race.key);
  return `
    <div><div class="lbl">Race</div><div style="font-size: 22px; font-weight: 650; margin-top: 3px">${esc(race.name)}</div>
      <div class="muted num">${race.rank ? "rank " + race.rank : "not reincarnated yet"}${race.level != null ? " · level " + race.level : " · not played"}${race.reincarnated ? " · reincarnated " + dt(race.reincarnated) : ""}</div>
      ${rankSource(race)}</div>
    <div class="inset"><div class="lbl">Reincarnation passive</div><div style="margin-top: 6px; line-height: 1.45; color: var(--text2)">${esc(race.passive || "Not in your game files yet.")}</div></div>
    <div><div class="lbl">In your relevels</div><div style="margin-top: 6px; line-height: 1.55; color: var(--soft)">${relevels(js)}</div></div>
    <div class="dim" style="font-size: 12px; line-height: 1.45; margin-top: auto">${race.rank > 0 ? `Next reincarnation: rank ${race.rank + 1}${race.rank >= 3 ? ", past its passive's 3 ranks: it adds XP to level and no passive" : ""}.` : "A race needs max faction with its three city factions before you can use it."}</div>`;
}
$("#page-roster").addEventListener("click", e => {
  if (e.target.closest("#editCounts, #editCounts2")) { openCounts(); return; }
  if (e.target.closest("#countsDone")) { editingCounts = false; renderRoster(); return; }
  if (e.target.closest("#countsReset")) { post("countsReset"); return; }
  const n = e.target.closest("[data-count]");
  if (n) {
    const key = n.dataset.count, to = Math.max(0, Number(n.dataset.to)), i = key.indexOf(":");
    const kind = key.slice(0, i), name = key.slice(i + 1);
    // Show it at once; Pathfinder recounts and sends the page again.
    const row = (kind === "race" ? data.reinc.races : data.reinc.classes).find(x => x.key === name);
    if (row) { row.rank = to; row.entered = to; row.enteredAt = new Date().toISOString(); }
    post("counts", { value: { [key]: to } });
    renderRoster();
    $(`[data-count="${CSS.escape(key)}"]${n.classList.contains("minus") ? ".minus" : ".tile"}`)?.focus();
    return;
  }
  const t = e.target.closest("[data-pick]");
  if (!t) return;
  rosterSel = t.dataset.pick;
  renderRoster();
  $(`[data-pick="${CSS.escape(rosterSel)}"]`)?.focus();
});

