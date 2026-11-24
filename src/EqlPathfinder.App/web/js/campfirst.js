"use strict";
let campLineGone = false;   // the one-time race list line is over: the plan changed, or Campaign was left
let campLineShown = false;  // the last Campaign draw showed it
/** The page left Campaign: if it showed the one-time line, the engine hears it (it never shows again). */
function campLeft() {
  if (!campLineShown) return;
  campLineShown = false; campLineGone = true;
  post("setting", { key: "raceListSeen", value: true });
}
// ---------------------------------------------------------------- Plan → Campaign: What you want first
// Ranked priority lists for classes and races, most wanted first, each with
// goal pips, Spread out | Unlock now, and the engine's price line. Every edit sends the lists and the goals,
// never `first` (the engine derives it): campGoalsChanged re-plans at once when there are runs.
let campFirstClash = null;   // the row whose Unlock now clashes with a pinned loadout, until a choice is made ("class:ROG")
let campDrag = null;          // the row being dragged
const firstKind = fk => fk.slice(0, fk.indexOf(":"));
const firstKey = fk => fk.slice(fk.indexOf(":") + 1);
const firstGoals = (p, kind) => kind === "class" ? p.classes : p.races;
/** The ranked rows as drawn: entries in the list that have a goal. A goal alone doesn't rank a race. */
function firstRanked(p, kind) {
  const m = firstGoals(p, kind), list = (kind === "class" ? p.classOrder : p.raceOrder) ?? [];
  const out = list.filter(w => (m[w.key] ?? 0) > 0).map(w => ({ key: w.key, unlockNow: !!w.unlockNow }));
  return out;
}
/** The plan's lists as the page sends them: entries `{ key, target, unlockNow }`, in the order drawn. */
function firstWrite(p, kind, rows) {
  const m = firstGoals(p, kind), list = rows.map(w => ({ key: w.key, target: m[w.key] ?? PASSIVE_MAX, unlockNow: !!w.unlockNow }));
  if (kind === "class") p.classOrder = list; else p.raceOrder = list;
}
/** The sum of ranks you have across a kind (the race tile with none ranked). */
const rosterSum = kind => (kind === "class" ? data.reinc.classes : data.reinc.races).reduce((a, x) => a + x.rank, 0);
/** Every race a goal of 3/3. The races already ranked stay ranked, at 3/3; this ranks none. */
function firstEveryRace(p) {
  data.reinc.races.forEach(x => p.races[x.key] = PASSIVE_MAX);
  p.raceOrder = (p.raceOrder ?? []).map(w => ({ ...w, target: PASSIVE_MAX }));
}
/** The engine's words, safe to draw, with the other timing's name in bold. */
const firstWords = s => esc(s).replace(/(?<!^)\b(Spread out|Unlock now)\b(?= (?:would|costs))/g, "<b>$1</b>");

function firstRow(kind, w, i, n, v, p, busy, lane, ends) {
  const r = data.reinc, fk = `${kind}:${w.key}`, goal = firstGoals(p, kind)[w.key] ?? 0;
  const info = (kind === "class" ? r.classes : r.races).find(x => x.key === w.key);
  const name = info?.name ?? w.key, now = info?.rank ?? 0, end = (kind === "class" ? ends.c : ends.r)[w.key] ?? now;
  const locked = kind === "race" && p.locked.includes(w.key), done = now >= goal;
  const lr = (kind === "class" ? lane?.classes : lane?.races)?.find(x => x.key === w.key);
  const pips = [1, 2, 3].map(k => `<button class="gpip ${k <= now ? "have" : k <= end ? "plan" : ""}${k <= goal ? " want" : " past"}" data-first="pip" data-fk="${esc(fk)}" data-k="${k}"
    aria-pressed="${k === goal}" aria-label="${esc(name)}: ${k === goal ? `your goal is ${k}/3, click to lower it` : `want ${k}/3`}"></button>`).join("");
  const seg = done ? "" : `<span class="seggroup sm" role="group" aria-label="${esc(name)}: Spread out or unlock now">
      <button data-first="spread" data-fk="${esc(fk)}" class="${w.unlockNow ? "" : "on"}" aria-pressed="${!w.unlockNow}">Spread out</button><button data-first="unlock" data-fk="${esc(fk)}" class="${w.unlockNow ? "on" : ""}" aria-pressed="${w.unlockNow}"${locked ? ` disabled title="${esc(name)} is locked"` : ""}>Unlock now</button></span>`;
  const line = lr?.line ?? (busy ? "Working it out…" : "");
  const clashHtml = campFirstClash === fk && lr?.clash && !w.unlockNow
    ? `<div class="price clash"><div class="warn">${firstClashWords(lr.clash)}</div><div class="row" style="gap: 8px; margin-top: 6px; flex-wrap: wrap">
        <button class="btn sm" data-first="clash-yes" data-fk="${esc(fk)}">Unlock now and unpin loadout ${(lr.clashLoadout ?? 0) + 1}</button><button class="btn sm" data-first="clash-no" data-fk="${esc(fk)}">Keep it Spread out</button></div></div>` : "";
  return `<div class="first-row"${i === n - 1 ? ' data-last="1"' : ""} data-fk="${esc(fk)}">
    <span class="n num" title="Drag to reorder" draggable="true" data-fdrag="${esc(fk)}">${i + 1}</span><span class="nm" title="${esc(name)}">${esc(kind === "class" ? w.key : name)}</span>
    <span class="gp" role="group" aria-label="${esc(name)} goal">${pips}</span><span class="seg">${seg}</span>
    <button class="iconbtn" data-first="menu" data-fk="${esc(fk)}" aria-haspopup="menu" aria-label="More for ${esc(name)}">⋯</button>
    ${clashHtml || (line ? `<div class="price${busy ? " stale-line" : ""}${done ? " dim" : ""}">${firstWords(line)}</div>` : "")}</div>`;
}
/** "Clashes with your pinned loadout 12 (Half Elf · WAR / RNG / ROG):" in bold, the rest plain. */
const firstClashWords = s => { const i = s.indexOf(":"); return i < 0 ? esc(s) : `<b>${esc(s.slice(0, i + 1))}</b>${esc(s.slice(i + 1))}`; };

function campFirstCard(v, p, busy) {
  const r = data.reinc, lane = v.firstLane, planned = p.loadouts.length > 0, ends = planned ? campEnds(v) : { c: v.classRanks, r: v.raceRanks };
  const cls = firstRanked(p, "class"), rcs = firstRanked(p, "race"), inCls = new Set(cls.map(w => w.key)), inRcs = new Set(rcs.map(w => w.key));
  const locked = new Set(p.locked);
  const rows = (kind, list) => list.map((w, i) => firstRow(kind, w, i, list.length, v, p, busy, lane, ends)).join("");
  const at3 = r.classes.filter(c => !inCls.has(c.key) && c.rank >= PASSIVE_MAX), open = r.classes.filter(c => !inCls.has(c.key) && c.rank < PASSIVE_MAX);
  const openR = r.races.filter(x => !inRcs.has(x.key) && x.rank < PASSIVE_MAX), at3r = r.races.filter(x => !inRcs.has(x.key) && x.rank >= PASSIVE_MAX);
  const raceChip = x => {
    const full = x.rank >= PASSIVE_MAX, lock = locked.has(x.key);
    return `<button class="chip${lock ? " locked" : ""}" data-first="rank" data-fk="race:${esc(x.key)}" title="${lock ? `You haven't unlocked ${esc(x.name)} yet: it's planned last` : esc(x.name)}">${esc(raceAbbr(x.key))}${lock ? " · locked" : ""}</button>`;
  };
  // One-time migration note when a legacy plan's all-races list was cleared; hides once the plan changes or Campaign is left.
  const emptied = v.newPlanner?.raceListEmptied ?? null;
  if (!emptied) campLineGone = false;
  campLineShown = !!emptied && !campLineGone && !rcs.length;
  const none = campLineShown
    ? `<div class="banner flash" role="status" style="margin: 0 0 8px"><div>${esc(emptied)}</div></div>`
    : `<div class="muted first-none">Nothing ranked: Pathfinder places every race the cheapest way. Click one below to earn it first, or to unlock it now.</div>`;
  return `<section class="card first" aria-label="What you want first" id="campFirst">
    ${campFirstFlash ? `<div class="banner flash" role="status" style="margin: 0 0 14px"><div>${esc(campFirstFlash)}</div></div>` : ""}
    <div class="first-sec">
      <div class="between"><div class="h2">Classes you want first</div><span class="dim" style="font-size: 12.5px">The rest are Pathfinder's to place</span></div>
      <div class="dim first-intro">Ranked classes are earned first wherever that's free. Unlock now plays a class's runs back to back to the rank its pips set, held like a Must, at the price shown.</div>
      <div class="first-rows">${cls.length ? rows("class", cls) : `<div class="muted first-none">Nothing ranked: Pathfinder places every class the cheapest way. Click one below to earn it first, or to unlock it now.</div>`}</div>
      ${open.length ? `<div class="lbl" style="margin-top: 12px">Pathfinder places (${open.length}) · click one to rank it</div>
      <div class="first-rest">${open.map(c => `<button class="chip" data-first="rank" data-fk="class:${esc(c.key)}" title="${esc(c.name)}">${esc(c.key)}</button>`).join("")}</div>` : ""}
      ${at3.length ? `<div class="dim" style="font-size: 12px; margin-top: 8px">3/3 already: ${at3.map(c => esc(c.key)).join(", ")}</div>` : ""}
    </div>
    <div class="first-sec" id="campFirstRaces">
      <div class="between"><div class="h2">Races you want first</div><span class="dim" style="font-size: 12.5px">The rest are Pathfinder's to place</span></div>
      <div class="dim first-intro">Ranked races are earned first wherever that's free. Unlock now plays a race's runs back to back, one race at a time, held like a Must, at the price shown.</div>
      <div class="first-rows">${rcs.length ? rows("race", rcs) : none}</div>
      ${openR.length ? `<div class="lbl" style="margin-top: 12px">Pathfinder places (${openR.length}) · click one to rank it</div>
      <div class="first-rest">${openR.map(raceChip).join("")}</div>` : ""}
      ${at3r.length ? `<div class="dim" style="font-size: 12px; margin-top: 8px">3/3 already: ${at3r.map(x => esc(raceAbbr(x.key))).join(", ")}</div>` : ""}
      ${rcs.length > 1 ? `<div class="dim" style="font-size: 12px; margin-top: 10px">Drag a row's number, or use its ⋯, to reorder.</div>` : ""}
    </div>
  </section>`;
}

/** A row's ⋯ menu: Move up, Move down, and Leave it to Pathfinder (it unranks; the goal stays). */
function firstMenuHtml(fk, p) {
  const kind = firstKind(fk), key = firstKey(fk), list = firstRanked(p, kind), i = list.findIndex(w => w.key === key);
  if (i < 0) return "";
  const name = (kind === "class" ? data.reinc.classes : data.reinc.races).find(x => x.key === key)?.name ?? key;
  return `<div class="cmenu" role="menu" aria-label="${esc(name)}">
    <button role="menuitem" data-first="up" data-fk="${esc(fk)}"${i === 0 ? " disabled" : ""}>Move up</button>
    <button role="menuitem" data-first="down" data-fk="${esc(fk)}"${i === list.length - 1 ? " disabled" : ""}>Move down</button>
    <button role="menuitem" data-first="unrank" data-fk="${esc(fk)}">Leave it to Pathfinder<span>Unranks ${esc(kind === "class" ? key : name)}. Its goal stays ${rank3(firstGoals(p, kind)[key] ?? 0)}.</span></button>
  </div>`;
}

/** A click in the card or its menu. Returns true when it was one of ours. */
function campFirstClick(t, p) {
  const act = t.dataset.first, fk = t.dataset.fk;
  if (!act) return false;
  if (act === "menu") { campOpenPop({ kind: "first", i: fk }, t); return true; }
  const kind = firstKind(fk), key = firstKey(fk), m = firstGoals(p, kind), list = firstRanked(p, kind), i = list.findIndex(w => w.key === key);
  const name = (kind === "class" ? data.reinc.classes : data.reinc.races).find(x => x.key === key)?.name ?? key;
  const lane = campLocal ? null : campView().firstLane, lr = (kind === "class" ? lane?.classes : lane?.races)?.find(x => x.key === key);
  const focus = `[data-first="menu"][data-fk="${CSS.escape(fk)}"]`;
  const send = (flash, f = `[data-fk="${CSS.escape(fk)}"][data-first="${act}"]`) => {
    firstWrite(p, kind, list); campFirstClash = null;
    campGoalsChanged(p, f);
    if (flash) { campFirstFlash = flash; renderCampaign(); }
  };
  switch (act) {
    case "rank":
      if (i >= 0) return true;
      m[key] = m[key] || PASSIVE_MAX; list.push({ key, unlockNow: false });
      return send("", `[data-first="menu"][data-fk="${CSS.escape(fk)}"]`), true;
    case "pip": {
      const k = Number(t.dataset.k), to = (m[key] ?? 0) === k ? k - 1 : k;
      if (to > 0) m[key] = to; else { delete m[key]; list.splice(i, 1); }
      return send("", `[data-first="pip"][data-fk="${CSS.escape(fk)}"][data-k="${k}"]`), true;
    }
    case "spread": if (i >= 0) list[i].unlockNow = false; return send(), true;
    case "unlock": {
      if (i < 0 || list[i].unlockNow) return true;
      if (lr?.clash && campFirstClash !== fk) { campFirstClash = fk; renderCampaign(); $(`[data-first="clash-no"][data-fk="${CSS.escape(fk)}"]`)?.focus(); return true; }
      const raised = (m[key] ?? 0) < PASSIVE_MAX;
      list[i].unlockNow = true; if (raised) m[key] = PASSIVE_MAX;
      return send(raised ? `${name}'s goal is 3/3 now. Click a pip to stop sooner.` : "", `[data-first="unlock"][data-fk="${CSS.escape(fk)}"]`), true;
    }
    case "clash-yes": {
      if (i < 0) return true;
      const at = lr?.clashLoadout, raised = (m[key] ?? 0) < PASSIVE_MAX;
      list[i].unlockNow = true; if (raised) m[key] = PASSIVE_MAX;
      if (at != null && p.loadouts[at]) p.loadouts[at].pinned = false;
      return send(raised ? `${name}'s goal is 3/3 now. Click a pip to stop sooner.` : "", `[data-first="unlock"][data-fk="${CSS.escape(fk)}"]`), true;
    }
    case "clash-no": campFirstClash = null; renderCampaign(); $(`[data-first="spread"][data-fk="${CSS.escape(fk)}"]`)?.focus(); return true;
    case "up": case "down": {
      const j = act === "up" ? i - 1 : i + 1;
      if (i < 0 || j < 0 || j >= list.length) return true;
      [list[i], list[j]] = [list[j], list[i]];
      return send("", focus), true;
    }
    case "unrank":
      if (i < 0) return true;
      list.splice(i, 1);
      return send(`${kind === "class" ? key : name} is Pathfinder's to place again.`, `[data-first="rank"][data-fk="${CSS.escape(fk)}"]`), true;
  }
  return false;
}

// Drag a row's number onto another row of the same list.
{
  const page = $("#page-campaign");
  page.addEventListener("dragstart", e => {
    const h = e.target.closest?.("[data-fdrag]");
    if (!h) return;
    campDrag = h.dataset.fdrag;
    try { e.dataTransfer.effectAllowed = "move"; e.dataTransfer.setData("text/plain", campDrag); } catch { /* a synthetic drag has none */ }
  });
  page.addEventListener("dragover", e => {
    const row = e.target.closest?.(".first-row");
    if (campDrag && row && firstKind(row.dataset.fk) === firstKind(campDrag)) { e.preventDefault(); row.classList.add("over"); }
  });
  page.addEventListener("dragleave", e => e.target.closest?.(".first-row")?.classList.remove("over"));
  page.addEventListener("dragend", () => { campDrag = null; page.querySelectorAll(".first-row.over").forEach(x => x.classList.remove("over")); });
  page.addEventListener("drop", e => {
    const row = e.target.closest?.(".first-row"), from = campDrag;
    campDrag = null;
    if (!row || !from || !campView() || firstKind(row.dataset.fk) !== firstKind(from) || row.dataset.fk === from) return;
    e.preventDefault();
    const p = campCopy(campPlan()), kind = firstKind(from), list = firstRanked(p, kind);
    const a = list.findIndex(w => w.key === firstKey(from)), b = list.findIndex(w => w.key === firstKey(row.dataset.fk));
    if (a < 0 || b < 0) return;
    list.splice(b, 0, list.splice(a, 1)[0]);
    firstWrite(p, kind, list);
    campGoalsChanged(p, `[data-first="menu"][data-fk="${CSS.escape(from)}"]`);
  });
}
