"use strict";
// ---------------------------------------------------------------- plan → campaign
// Every reincarnation you mean to do (Analysis/Campaign.cs). You say what you want with the pips; Pathfinder plans the
// cheapest way there (fewest runs, then the least XP to level, then your classes first), says whether the plan is the
// cheapest and why, and prices every fix, swap and pick the same way. Edits show at once; Pathfinder saves them and
// sends the plan played out a moment later.
let campFirstFlash = "";   // the line the What you want first card shows after an edit (campfirst.js)
let campCardClosed = false;   // Got it on "Campaign has a new planner" was pressed: the card is gone at once, before the window answers
let campLocal = null, campMore = false, campClearAt = 0, campSentFrom = null, campSentAt = 0;
let campSent = 0;   // the number of the last edit sent; the window answers with it (settings.campaignAnswered)
const campAnswered = () => data?.settings?.campaignAnswered ?? 0;
/** The loadout whose Swap… is open; its options from Pathfinder ({ index, plan, options }); the open popover. */
let campSwap = null, campOpts = null, campPop = null, campPopHtml = "", campPopAt = null;   // campPopAt: where the popover's button was when it was placed
const campView = () => data?.reinc?.campaign;
const EMPTY_CAMP = { classes: {}, races: {}, locked: [], first: [], loadouts: [], since: null, prefer: ["carry", "heals"] };
const campPlan = () => campLocal ?? campView()?.plan ?? EMPTY_CAMP;
const campCopy = p => JSON.parse(JSON.stringify(p ?? EMPTY_CAMP));
const PASSIVE_MAX = 3;
const rank3 = n => `${Math.min(n, PASSIVE_MAX)}/${PASSIVE_MAX}`;
const trioOf = l => l.classes.join(" / ");
/** Races as the game's Reincarnation tab writes them. */
const RACE_ABBR = { "Human": "HUM", "Barbarian": "BAR", "Erudite": "ERU", "Wood Elf": "ELF", "High Elf": "HIE", "Dark Elf": "DEF", "Half Elf": "HEF",
  "Dwarf": "DWF", "Troll": "TRL", "Ogre": "OGR", "Halfling": "HFL", "Gnome": "GNM", "Iksar": "IKS", "Kerran": "KRN", "Froglok": "FRG", "Half Dark Elf": "HDE" };
const raceAbbr = r => RACE_ABBR[r] ?? r.slice(0, 3).toUpperCase();
const PIN = '<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9 4h6l-1 6 3 3H7l3-3-1-6z"/><path d="M12 13v7"/></svg>';
const LOCK = '<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="5" y="11" width="14" height="9" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/></svg>';
/**
 * A plan run's number in the whole campaign: runs played before the plan come first;
 * the engine numbers the plan's own runs starting from 1.
 */
const campRunNo = n => n + (campView()?.before ?? 0);
/** The runs you played before the plan's own are listed (they start folded away). */
let campShowPlayed = false;

/**
 * What you want changed (a goal, a ready-made goal, earn first, locked races, what you prefer): with a plan, Pathfinder
 * plans the cheapest way to the new goals at once, keeping pinned loadouts and runs played. With none yet you're still
 * choosing, so building waits for "Build the plan".
 */
const campGoalsChanged = (p, focus) => sendCamp(p, p.loadouts.length > 0, focus);

function sendCamp(p, rebuild, focus) {
  if (campLocal == null) campSentFrom = JSON.stringify(campView()?.plan ?? null);
  campSentAt = Date.now();
  campSent = Math.max(campSent, campAnswered()) + 1;   // past any the window has answered, after a reload too
  campLocal = p;
  campSwap = null; campOpts = null; campClosePop(); campFirstFlash = ""; campLineGone = true;
  post("campaign", { value: p, rebuild: !!rebuild, n: campSent });
  renderCampaign();
  if (focus) $(focus)?.focus();
}

/**
 * New data from Pathfinder: drop your unsaved copy once the window has answered your last edit (saved, even unchanged,
 * or dropped), the saved plan has changed, or after 5 s. A refresh that went out before your edit was saved keeps
 * showing your edit. A rebuild that changes nothing saves the same plan: without the answer, the runs stayed blank
 * under "updating…" until the next edit (report 14).
 */
function campOnData() {
  rolesLocal = null;
  if (campLocal == null) return;
  if (campAnswered() >= campSent || JSON.stringify(campView()?.plan ?? null) !== campSentFrom || Date.now() - campSentAt > 5000) campLocal = null;
}

/** What has focus in the page, as a selector that finds it again after the page is drawn anew. */
function campFocusKey() {
  const a = document.activeElement, body = $("#campBody");
  if (!a || !body?.contains(a)) return null;
  const sel = ["data-cl", "data-act", "data-camp", "data-goal", "data-k", "data-lock", "data-first", "data-fk", "data-preset", "data-fix", "data-swap", "data-pick", "data-cp", "data-cf", "data-ci", "data-combo-act"]
    .filter(n => a.hasAttribute(n)).map(n => `[${n}="${CSS.escape(a.getAttribute(n))}"]`).join("");
  return sel ? a.tagName.toLowerCase() + sel : null;
}

/** The relevel your campaign has next (Home leads with it), or null. No hours: a trio's aren't knowable before you play it. */
function campNext() {
  const v = data?.reinc?.campaign, n = v?.runs?.find(x => x.state === "next");
  if (!n) return null;
  return { classes: n.classes, race: n.race, xpMultiplier: n.xp, rankAfter: n.rankAfter, n: campRunNo(n.n), of: campRunNo(v.runs.length) };
}
/** "+28% XP to level on average" over the runs still to play. */
const campAvgXp = v => v.left ? `${xpWords(v.totalXp / v.left)} on average` : "";

/** "Everything 3/3", "Every class 3/3", or how many passives you picked. */
function campName(p) {
  const r = data.reinc, at3 = (m, keys) => keys.filter(k => (m[k] ?? 0) >= PASSIVE_MAX).length;
  const nc = Object.values(p.classes).filter(v => v > 0).length, nr = Object.values(p.races).filter(v => v > 0).length;
  const cAll = at3(p.classes, r.classes.map(c => c.key)) === r.classes.length, rAll = at3(p.races, r.races.map(x => x.key)) === r.races.length;
  if (cAll && rAll) return "Everything 3/3";
  if (cAll && nr === 0) return "Every class 3/3";
  if (rAll && nc === 0) return "Every race 3/3";
  return nc + nr ? `${plural(nc + nr, "passive")} you picked` : "no goals yet";
}

// ---- prices: what a change does (Campaign.cs CampaignDelta), runs first, then XP
const campKey = k => k.slice(k.indexOf(":") + 1);
const campXp = x => `${x > 0 ? "+" : "−"}${fmt(Math.abs(x) * 100, 0)}% XP`;
/** "+1 run", "1 run fewer", "+10% XP" (one run's bar 10% bigger, over the whole plan), "same XP"; and classes sooner or later. */
function campCost(d) {
  const parts = [d.runs > 0 ? `+${plural(d.runs, "run")}` : d.runs < 0 ? `${plural(-d.runs, "run")} fewer` : Math.abs(d.xp) < 0.005 ? "same XP" : campXp(d.xp)];
  if (d.classesSooner > 0) parts.push(`classes done ${plural(d.classesSooner, "run")} sooner`);
  if (d.classesSooner < 0) parts.push(`classes done ${plural(-d.classesSooner, "run")} later`);
  return parts.join(" · ");
}
/** The one thing to say first about a change: a goal it misses or reaches, then heals or a carry. */
function campStatus(d) {
  if (d.breaks.length) return ["warn", `leaves ${d.breaks.map(campKey).join(", ")} short`];
  if (d.fixes.length) return ["good", `${d.fixes.map(campKey).join(", ")} reach${d.fixes.length === 1 ? "es" : ""} your goal`];
  if (d.lost.length) return ["warn", `trio loses ${d.lost.join(" and ")}`];
  if (d.gained.length) return ["good", `adds ${d.gained.join(" and ")}`];
  return ["dim", "goals unchanged"];
}
const campNotes = d => [campCost(d), d.lost.length ? `trio loses ${d.lost.join(" and ")}` : "", d.gained.length ? `adds ${d.gained.join(" and ")}` : ""].filter(Boolean).join(" · ");

/** Where each class and race ends once the plan is played: your ranks now plus the runs still to play. */
function campEnds(v) {
  const c = { ...v.classRanks }, r = { ...v.raceRanks };
  for (const x of v.runs) if (x.state === "next" || x.state === "planned") { x.classes.forEach(k => c[k] = (c[k] ?? 0) + 1); r[x.race] = (r[x.race] ?? 0) + 1; }
  return { c, r };
}
/** Three pips: the ranks you have, the ones this plan earns, and a goal it misses. */
const campPips = (now, end, goal) => `<span class="pips3">${[1, 2, 3].map(k => `<i class="${k <= now ? "have" : k <= end ? "plan" : k <= goal ? "miss" : ""}"></i>`).join("")}</span>`;

function renderCampaign() {
  const v = campView(), r = data?.reinc;
  if (!v || !r) return;
  const p = campPlan(), busy = campLocal != null, focus = campFocusKey();
  if (campSwap != null && !busy && (campOpts?.index !== campSwap || campOpts.plan !== JSON.stringify(p))) campAskOptions(campSwap);
  const planned = p.loadouts.length > 0;
  // Three cells of a grid: the band (with the new-planner card and the fixes), Your runs, and the side column. Wide,
  // the side column is beside the other two; stacked (app.css), it comes between them.
  $("#campBody").innerHTML = `<div class="camp-grid">`
    + `<div class="camp-head">${planned ? campHero(v, p, busy) : ""}${campAcross(v)}${planned ? campFixes(v, busy) : ""}</div>`
    + `<div class="camp-side" id="campSide">${campGoalsCard(v, p, busy, planned)}${campFirstCard(v, p, busy)}${campCombosCard(v, p, busy)}</div>`
    + `<div class="camp-runs" id="campMain">${planned ? campList(v, p, busy) : campStart(v, p, busy)}</div></div>`;
  if (focus) $(focus)?.focus();
  if (campPop) campRenderPop();
}

// ---- the goals: ready-made goals, then a click on a pip for the rank you want
/** One ready-made goal: lit (a state, not a switch) while it's your goal; its count is the runs to play from here if it were set now. */
function campReady(v, key, act, name, on) {
  const x = (v.presets ?? []).find(y => y.key === key), runs = on ? v.left : x?.runs;
  const count = runs == null ? "" : runs === 0 ? ` <span class="dim">· done</span>` : ` <span class="dim">· ${plural(runs, "run")}</span>`;
  const tip = runs == null ? (on ? "Your goal." : "Make it your goal.") : on
    ? `Your goal: ${plural(v.left, "run")} to go.` : `${plural(runs, "run")} from here${v.before ? `, ${v.before + runs} with the ${v.before} you've played` : ""}${runs && x ? ` · ${xpWords(x.totalXp / x.runs)} on average · the last needs ${xpWords(x.lastXp)}` : ""}`;
  return `<button class="btn sm${on ? " on" : ""}" data-camp="${act}" aria-pressed="${on}" title="${esc(tip)}">${on ? "✓ " : ""}${name}${count}</button>`;
}

function campGoalTiles(v, p) {
  // With no loadouts yet (a new plan, or right after Start over) there are no runs: everything ends where it is now.
  const r = data.reinc, planned = p.loadouts.length > 0, ends = planned ? campEnds(v) : { c: v.classRanks, r: v.raceRanks };
  let sawMiss = false, sawHave = false, sawPlan = false, sawNot = false;
  const seen = (k, now, end) => { if (k <= now) sawHave = true; else if (k <= end) sawPlan = true; else sawNot = true; };
  const tile = (label, kind, rows, gs, extra) => {
    const goal = rows.filter(x => x.goal > 0);
    const have = goal.reduce((a, x) => a + Math.min(x.now, x.goal), 0), want = goal.reduce((a, x) => a + x.goal, 0);
    const cells = rows.map(x => {
      const miss = planned && x.goal > 0 && x.end < x.goal;   // with no plan yet, nothing has missed it
      if (miss) sawMiss = true;
      // One ring, on the rank you want; ranks past it are dimmed (you have them, or the plan takes them there, but you didn't ask for them).
      const pips = [1, 2, 3].map(k => { seen(k, x.now, x.end); return `<button class="gpip ${k <= x.now ? "have" : k <= x.end ? "plan" : ""}${k === x.goal ? ` goal${miss ? " miss" : ""}` : x.goal && k > x.goal ? " past" : ""}"
        data-goal="${kind}:${esc(x.key)}" data-k="${k}" aria-pressed="${k === x.goal}" aria-label="${esc(x.name)}: ${k === x.goal ? `your goal is ${k}/3, click to lower it` : `want ${k}/3`}"></button>`; }).join("");
      const tip = `${x.name}: has ${rank3(x.now)}${x.end > x.now ? `, this plan takes it to ${rank3(x.end)}` : ""}${x.goal ? `; your goal ${x.goal}/3` : "; not a goal"}${kind === "class" && planned ? comboPastTip(v, p, x.key) : ""}`;
      return `<div class="gcell${x.goal ? " on" : ""}${miss ? " miss" : ""}" title="${esc(tip)}"><span>${esc(x.short)}</span><span class="gpips">${pips}</span></div>`;
    }).join("");
    let line;
    if (!goal.length) line = `${rows.reduce((a, x) => a + x.now, 0)} of ${rows.length * PASSIVE_MAX} ranks earned · no goal yet`;
    else if (have >= want) line = `${have} of ${want} ranks you want · all yours already`;
    else if (!planned) line = `${have} of ${want} ${want === 1 ? "rank" : "ranks"} you want`;
    else { const by = campBy(v.runs, gs); line = `${have} of ${want} ${want === 1 ? "rank" : "ranks"} you want · ${by === "not all in this plan" ? `<span class="warn">${by}</span>` : by}`; }
    return `<div class="gblock"><div class="glabel lbl">${label}</div><div class="gbody"><div class="pipgrid">${cells}</div><div class="gcount">${line}${extra}</div></div></div>`;
  };
  const cls = v.goals.filter(g => g.kind === "class"), races = v.goals.filter(g => g.kind === "race");
  const classRows = r.classes.map(c => ({ key: c.key, short: c.key, name: c.name, now: c.rank, end: ends.c[c.key] ?? c.rank, goal: p.classes[c.key] ?? 0 }));
  const raceRows = r.races.map(x => ({ key: x.key, short: raceAbbr(x.key), name: x.name, now: x.rank, end: ends.r[x.key] ?? x.rank, goal: p.races[x.key] ?? 0 }));
  const compAt = k => v.runs.find(x => x.gains.some(g => g.kind === "completionist" && g.rank === k))?.n;
  const compNext = planned && v.completionistNow < PASSIVE_MAX ? compAt(v.completionistNow + 1) : null;
  const comp = [1, 2, 3].map(k => { seen(k, v.completionistNow, planned ? v.completionistAtEnd : v.completionistNow); return `<span class="gpip ${k <= v.completionistNow ? "have" : planned && k <= v.completionistAtEnd ? "plan" : ""}"></span>`; }).join("");
  const compLine = `${v.completionistNow} of 3 · ${v.completionistNow >= PASSIVE_MAX ? "done" : !planned ? "goes up when every class reaches 1, 2 and 3"
    : compNext ? `rank ${v.completionistNow + 1} at run ${campRunNo(compNext)}` : "this plan doesn't raise it"}`;
  const tiles = tile("Classes", "class", classRows, cls, planned ? comboPastLines(v, p) : "") + tile("Races", "race", raceRows, races, "")
    + `<div class="gblock"><div class="glabel lbl">Completionist${infoTip("completionist")}</div><div class="gbody"><div class="gpips">${comp}</div><div class="gcount">${compLine}</div></div></div>`;
  const mark = (cls, text) => `<span class="gmark"><span class="gpip ${cls}"></span>${text}</span>`;
  const legend = `<div class="glegend">${sawHave ? mark("have", "you have it") : ""}${planned && sawPlan ? mark("plan", "this plan earns it") : ""}${planned && sawNot ? mark("", "not planned") : ""}${mark("goal", "your goal")}${sawMiss ? mark("goal miss", "a goal this plan misses") : ""}</div>
    <div class="dim" style="font-size: 12px; margin-top: 6px">Click a pip for the rank you want. Click your goal's pip again to lower it.</div>`;
  const allC = r.classes.every(c => (p.classes[c.key] ?? 0) >= PASSIVE_MAX), anyRace = Object.keys(p.races).length > 0;
  const allOn = allC && r.races.every(x => (p.races[x.key] ?? 0) >= PASSIVE_MAX), clsOn = allC && !anyRace;
  const ready = planned ? `<div class="row camp-tools">${campReady(v, "all", "goal-all", "Everything 3/3", allOn)}${campReady(v, "classes", "goal-classes", "Every class 3/3", clsOn)}<button class="btn sm" data-camp="no-goals" title="Takes away every goal. Runs that were only for them go too; pinned runs and runs you played stay."${Object.keys(p.classes).length + anyRace ? "" : " disabled"}>Clear goals</button></div>` : "";
  return `${ready}
    <div style="margin-top: 8px"><button class="link" data-camp="more" aria-expanded="${campMore}">${campMore ? "Fewer options" : "More options"}</button></div>
    ${campMore ? campOptionsPanel(p) : ""}
    ${tiles}${legend}`;
}

/** "all by run 14", "done", or "not all in this plan". */
function campBy(runs, goals) {
  const at = goals.map(g => campReach(runs, g.kind, g.key, g.target));
  if (at.every(n => n === 0)) return "done";
  return at.some(n => n == null) ? "not all in this plan" : `all by run ${campRunNo(Math.max(...at))}`;
}

/** The run a goal is reached at, or null when the plan never gets it there (already there: 0). */
function campReach(runs, kind, key, target) {
  const v = campView(), now = (kind === "class" ? v.classRanks : v.raceRanks)[key] ?? 0;
  if (now >= target) return 0;
  return runs.find(x => x.gains.some(g => g.kind === kind && g.key === key && g.rank >= target))?.n ?? null;
}

/** Races you haven't unlocked, and where "between plans that cost the same" is decided. */
function campOptionsPanel(p) {
  const r = data.reinc, locked = new Set(p.locked);
  return `<div class="camp-more">
    <div><div class="lbl">Races you haven't unlocked</div>
      <div class="dim" style="font-size: 12px; margin-top: 2px">Max faction with their three city factions first. They're planned last.</div>
      <div class="chips" style="margin-top: 6px">${r.races.map(x => `<button class="chip${locked.has(x.key) ? " gold" : ""}" data-lock="${esc(x.key)}" aria-pressed="${locked.has(x.key)}">${esc(x.name)}${locked.has(x.key) ? " · locked" : ""}</button>`).join("")}</div></div>
    <div><div class="lbl">Between plans that cost the same</div>
      <div class="muted" style="font-size: 12.5px; margin-top: 6px">Each trio holds up where you level, and gets your carries, best first.</div>
      <button class="link" style="margin-top: 8px" data-go="classroles">Class roles</button></div>
  </div>`;
}

// ---- no plan yet: the ready-made goals and what each costs from where you are
function campStart(v, p, busy) {
  const pre = v.presets ?? [], all = pre.find(x => x.key === "all"), cls = pre.find(x => x.key === "classes");
  const guess = ranksUnconfirmed();
  const tile = x => `<button class="preset" data-preset="${esc(x.key)}">
      <div style="font-size: 16px; font-weight: 650">${esc(x.name)}</div>
      <div class="num" style="margin-top: 6px; font-size: 24px; font-weight: 700"><span style="white-space: nowrap">${x.runs} <span class="pk">${x.runs === 1 ? "run" : "runs"} ${guess ? "from no reincarnations" : "from here"}</span></span>${v.before
        ? `<div class="pk" style="margin-top: 2px">${v.before + x.runs} with the ${v.before} you've played</div>` : ""}</div>
      <div class="dim" style="font-size: 12px; margin-top: 4px; line-height: 1.45">${x.runs ? `${xpWords(x.totalXp / x.runs)} on average · ` : ""}the last needs <span title="${esc(xpTip(x.lastXp))}">${xpWords(x.lastXp)}</span></div></button>`;
  const note = all && cls && all.runs > cls.runs
    ? `Every class reaches 3/3 in ${cls.runs} runs. The other ${all.runs - cls.runs} runs of Everything 3/3 are for race passives: each still raises three classes past 3, which adds XP to level and no passive, so the last ones need ${xpWords(all.lastXp)}. Mobs give the same XP; the bar is bigger.`
    : "";
  return `<section class="card" style="padding: 18px 20px">
    <div class="h2">Plan every reincarnation</div>
    <div class="muted" style="margin-top: 6px; line-height: 1.5; max-width: 860px">Each reincarnation is one race and three classes. Every class and race passive has 3 ranks, and Completionist goes up each time every class reaches 1, 2 and 3. Pick what you're after: Pathfinder builds the cheapest way there from your ranks now: the fewest runs, the least XP to level, your classes first. Between plans that cost the same, each trio holds up where you level and gets your carries from Class roles, best first. It ticks each run off when your log sees you reincarnate.</div>
    ${guess ? countsNudge(data.reinc, "", "margin: 14px 0 0") : ""}
    <div class="presets" style="margin-top: 14px">${pre.map(tile).join("")}
      <button class="preset" data-preset="custom"><div style="font-size: 16px; font-weight: 650">Choose my own</div>
        <div class="dim" style="font-size: 12px; margin-top: 8px; line-height: 1.45">Your main and a few others: click the pips below for the rank you want.</div></button></div>
    ${note ? `<div class="dim" style="font-size: 12px; margin-top: 12px; line-height: 1.5; max-width: 860px">${note}</div>` : ""}
  </section>
  </section>
  <section class="card" style="padding: 18px 20px" aria-label="Your runs">
    <div class="h2">Your runs</div>
    <div class="dim" style="margin-top: 6px">Your runs show here once you pick a goal: a ready-made one, or your own with the pips.</div>
  </section>`;
}

/** Your goals: the pips for the ranks you want; with no plan yet it ends with Build the plan. */
function campGoalsCard(v, p, busy, planned) {
  const any = Object.keys(p.classes).length + Object.keys(p.races).length > 0;
  return `<section class="card" style="padding: 16px 20px" aria-label="Your goals">
    <div class="between"><div class="h2">Your goals</div>${busy ? `<span class="chip">updating…</span>` : ""}</div>
    ${campGoalTiles(v, p)}
    ${planned ? "" : `<div class="row" style="margin-top: 14px"><button class="btn primary" data-camp="rebuild"${any ? "" : " disabled"}>Build the plan</button>
      ${any ? (!busy && v.floor?.runs ? `<span class="dim" style="font-size: 12px">The fewest runs that reach these goals: ${v.floor.runs} (${esc(v.floor.why)}).</span>` : "")
        : `<span class="dim" style="font-size: 12px">Click a pip, or pick a ready-made goal.</span>`}</div>`}
  </section>`;
}

// ---- the plan: where you are in it, and whether it's the cheapest way
/** "32 / 50 · ≈ 6.2 h to go", or "1 / 50 · not played yet": a leveling run's level out of the cap, and Home's hours for it. */
const campLevelBits = x => `<span class="num">${x.level} / ${cap()}</span>${x.etaHours != null ? ` · <span class="num gold">≈&nbsp;${fmt(x.etaHours)}&nbsp;h&nbsp;to&nbsp;go</span>` : x.crawls === 0 ? ` · <span class="dim nw">not played yet</span>` : ""}`;
/** Run numbers as "7, 9–10". */
function campNums(ns) {
  const out = [];
  for (const n of ns) { const last = out.at(-1); if (last && last[1] === n - 1) last[1] = n; else out.push([n, n]); }
  return out.map(([a, b]) => a === b ? `${a}` : `${a}–${b}`).join(", ");
}
/** What the (i) on the totals line adds for this plan: the fewest runs, when the classes are done, what keeping a combo costs. */
function campCheapWords(v, met) {
  const f = v.floor;
  if (!met || !f?.runs) return "";
  const costs = v.combosCost === true, ref = v.comboFloor ?? f.runs, musts = v.comboFloor != null && v.comboFloor > f.runs;
  let t = `<p><b>This plan:</b> ${plural(v.left, "run")} to go. The fewest that reaches your goals${musts ? " and keeps your Musts" : ""} is ${plural(ref, "run")} (${esc(f.why)}).${musts ? ` Without your Musts, ${plural(f.runs, "run")} would do.` : ""}</p>`;
  if (f.classRuns && v.classesDoneAt != null) {
    const best = v.done + f.classRuns;
    t += `<p>${v.classesDoneAt === best ? `Your classes are done at run ${campRunNo(v.classesDoneAt)}, the earliest they can be.` : `Your classes are done at run ${campRunNo(v.classesDoneAt)}; they could be by run ${campRunNo(best)}${costs ? (musts ? " without your Musts" : " without your combos") : ""}.`}</p>`;
  }
  if (costs && !musts && v.comboWhy && v.comboCostWhat) t += `<p>Keeping ${esc(v.comboWhy)} costs ${esc(v.comboCostWhat)}.</p>`;
  return t;
}
/** Start over's class and words: for 4 seconds after its first click, it asks for a second. */
const campClearLook = () => Date.now() - campClearAt < 4000 ? ["btn danger", "Click again to clear the plan"] : ["btn", "Start over"];
function campHero(v, p, busy) {
  const runs = v.runs, next = runs.find(x => x.state === "next"), levs = runs.filter(x => x.state === "leveling"), lev = levs[0], others = levs.slice(1);
  const bold = x => `<b style="color: var(--text)">${esc(x.race)} · ${esc(trioOf(x))}</b>`;
  const nextLine = next
    ? `${lev ? `Next to reincarnate: <b style="color: var(--text)">run ${campRunNo(next.n)} · ${esc(next.race)} · ${esc(trioOf(next))}</b>` : `Next: ${bold(next)}`} at <span class="nw" title="${esc(xpTip(next.xp))}">${xpWords(next.xp)}</span>${infoTip("xp")}`
    : lev ? "Every run in this plan is reincarnated. It's done when the last one is back at the cap." : "Every run in this plan is played.";
  const levLine = lev ? `<div class="muted">Leveling: ${bold(lev)} · ${campLevelBits(lev)}</div>` : "";
  const alsoLine = others.length ? `<div class="muted" style="margin-top: 4px; font-size: 12.5px">Also leveling: ${others.slice(0, 2).map(x => `run ${campRunNo(x.n)} · <b style="color: var(--text)">${esc(x.race)} · ${esc(trioOf(x))}</b> · ${campLevelBits(x)}`).join("; ")}${others.length > 2 ? `; and ${others.length - 2} more below` : ""}</div>` : "";
  // The totals line takes in what the Cheapest path card said: only a plan dearer than the cheapest says so, in plain words.
  const f = v.floor, met = !v.goals.some(g => g.atEnd < g.target), mustsCost = v.comboFloor != null && v.comboFloor > (f?.runs ?? 0);
  const more = met && f?.runs && !mustsCost && v.left > f.runs ? v.left - f.runs : 0;
  const none = met && f && !f.runs && v.left > 0;
  const total = `${plural(v.left, "run")} to go${v.left ? ` · <span class="nw">${campAvgXp(v)}</span> · the last needs <span class="nw">${xpWords(v.lastXp)}</span>` : ""}${none ? ` · No goals left to earn: none of these runs is needed` : more ? ` · <span class="nw">${plural(more, "run")} more than the cheapest path${infoTip("cheapest", campCheapWords(v, met))}</span>` : infoTip("cheapest", campCheapWords(v, met))}`;
  const off = v.offPlan ?? [];
  const offText = off.length ? `<div class="banner" style="margin: 12px 0 0"><div><div style="font-weight: 650">Your log saw ${plural(off.length, "reincarnation")} this plan doesn't have</div>
      <div class="muted" style="font-size: 12px; margin-top: 2px">${off.slice(-3).map(o => `${esc(o.race)} · ${esc(o.classes.join(" / "))} (${dt(o.at).split(",")[0]})`).join("; ")}. Your ranks already count ${off.length === 1 ? "it" : "them"}; rebuild to plan the rest from here.</div></div>
      <button class="btn primary" data-camp="rebuild">Rebuild the rest</button></div>` : "";
  const [clearClass, clearWords] = campClearLook(), guess = ranksUnconfirmed();
  return `<section class="card camp-band" id="campBand">
    <div class="cb-name"><div class="lbl">Your campaign · ${esc(campName(p))}${guess ? " · from no reincarnations" : ""}${busy ? ` <span class="chip" style="margin-left: 6px">updating…</span>` : ""}</div>
      <div class="fc-big num" style="margin-top: 6px">${lev || next ? `Run ${campRunNo((lev ?? next).n)} <span style="font-size: 18px; color: var(--faint); font-weight: 500">of ${campRunNo(runs.length)}</span>` : "Done"}</div></div>
    <div class="cb-lines">${levLine}${alsoLine}
      <div class="muted" style="margin-top: ${lev ? 4 : 0}px">${nextLine}</div>
      <div class="muted" style="margin-top: 4px">${total}</div></div>
    <div class="cb-btns">
      <button class="btn" data-camp="rebuild" title="Builds the loadouts again from your ranks now, the cheapest way. Pinned loadouts and runs already done stay.">Rebuild the rest</button>
      <button class="${clearClass}" data-camp="clear">${clearWords}</button>
    </div>
    ${guess ? countsNudge(data.reinc, "", "margin: 4px 0 0; grid-column: 1 / -1") : ""}
    ${offText.replace("margin: 12px 0 0", "margin: 4px 0 0; grid-column: 1 / -1")}
  </section>`;
}

// ---- fixes: when the plan misses a goal or has runs to spare, the cheapest changes first (Campaign.Fixes)
function campFixes(v, busy) {
  if (busy) return campFixes(v).replace(/class="fix( spare)?"/, m => m.slice(0, -1) + ' stale"');
  const short = v.goals.filter(g => g.atEnd < g.target), fx = v.fixes ?? [];
  const spare = !short.length && v.floor?.runs != null && v.left > (v.comboFloor ?? v.floor.runs);
  if (!short.length && (!spare || !fx.length)) return "";   // spare runs with only Rebuild the rest to offer: the band already says so
  const title = short.length
    ? `This plan leaves ${short.slice(0, 4).map(g => `${esc(g.key)} at ${rank3(g.atEnd)}`).join(", ")}${short.length > 4 ? ` and ${short.length - 4} more` : ""} short of your goal.${fx.length ? " The cheapest fixes first:" : ""}`
    : "The cheapest fixes:";
  return `<section class="fix${short.length ? "" : " spare"}" role="status">
    <div style="font-weight: 650">${title}</div>
    <div class="fixes">${fx.map((f, j) => `<button class="fixbtn" data-fix="${j}"><b>${esc(f.label)}</b><span>${esc(campNotes(f.delta))}</span></button>`).join("")
      || `<button class="fixbtn" data-camp="rebuild"><b>Rebuild the rest</b><span>Keeps pinned loadouts and runs played</span></button>`}</div>
  </section>`;
}

// ---- Loadouts and runs: grouped by round, one line per loadout
/** The groups to come that are folded: the page's state, not stored (like Show on played runs). */
const campFolded = new Set();
/** Three pips: solid the ranks you have, dark gold the ones this plan earns by then, empty not yet. */
const campRlPips = (have, end) => `<span class="pips3" aria-hidden="true">${[1, 2, 3].map(k => `<i class="${k <= have ? "have" : k <= end ? "plan" : ""}"></i>`).join("")}</span>`;
const campRunsText = ns => ns.length > 1 ? `${ns[0]}–${ns.at(-1)}` : `${ns[0]}`;

/** Which group a run is in: played, leveling now, Round 1-3 (its lowest class rank, from the engine) or Races. */
const campGroupOf = x => x.state === "done" ? "played" : x.state === "leveling" ? "leveling" : x.round > 0 ? `r${x.round}` : "races";

/** A run's rank in a name once it is played: its last gain's rank, or 3 when the name was past 3/3 already. */
const campRankOf = (x, name) => x.gains.findLast(g => g.key === name && g.kind !== "completionist")?.rank ?? (x.past.includes(name) ? PASSIVE_MAX : 0);

/** The runs as lines: consecutive runs of one loadout in one group are one line, ranked by its last run. */
function campLines(v) {
  const lines = [];
  for (const x of v.runs) {
    const g = campGroupOf(x), last = lines.at(-1);
    if (last && last.g === g && last.loadout === x.loadout && x.loadout >= 0) last.runs.push(x);
    else lines.push({ g, loadout: x.loadout, runs: [x] });
  }
  return lines;
}
/** One loadout's line: run, race, three classes with their pips, status, XP to level, actions; Swap… opens under it. */
function campLine(v, L, loadouts) {
  const i = L.loadout, l = loadouts[i], row = v.loadouts[i], x0 = L.runs[0], nums = L.runs.map(x => campRunNo(x.n));
  const toPlay = L.g !== "played" && L.g !== "leveling", isNext = L.runs.some(x => x.state === "next");
  const top = new Set(), past = new Set(), want = new Map();
  let comp = 0;
  for (const x of L.runs) {
    x.gains.forEach(g => { if (g.kind === "completionist") comp = g.rank; else { want.set(g.key, Math.max(want.get(g.key) ?? 0, g.rank)); if (g.rank === PASSIVE_MAX) top.add(g.key); } });
    x.past.forEach(k => past.add(k));
  }
  const once = `once ${nums.length > 1 ? "these runs are" : "this run is"} played`;
  const cell = (name, rk, now) => {
    const tip = `${name} ${rank3(rk)} ${once}${top.has(name) ? "" : past.has(name) ? ": 3/3 already, so the run adds XP to level and no passive" : ""}`;
    return `<span class="rcell${top.has(name) ? " top" : past.has(name) ? " past" : ""}" title="${esc(tip)}"><b aria-label="${esc(tip)}">${esc(name)}</b>${campRlPips(Math.min(now, PASSIVE_MAX), Math.min(rk, PASSIVE_MAX))}</span>`;
  };
  const xz = L.runs.at(-1);
  const cells = cell(x0.race, campRankOf(xz, x0.race), v.raceRanks[x0.race] ?? 0) + x0.classes.map(k => cell(k, campRankOf(xz, k), v.classRanks[k] ?? 0)).join("");
  const xs = L.runs.map(x => x.xp).filter(x => x > 0);
  const xp = !xs.length ? "" : Math.min(...xs) === Math.max(...xs) ? xpPct(xs[0]) : `${xpPct(Math.min(...xs))} → ${xpPct(Math.max(...xs))}`;
  const played = row?.done > 0 && toPlay ? `${row.done} of ${l.times} played` : "";
  const status = L.g === "leveling" ? campLevelBits(x0) : `${isNext ? `<span class="chip gold">next</span> ` : ""}${played}`;
  const open = toPlay && campSwap === i, pinned = toPlay && l?.pinned;
  const acts = !toPlay ? `<span class="lact dim" title="${L.g === "played" ? "Played: nothing in the plan changes it" : "Reincarnated: this run keeps its loadout"}">${LOCK}</span>`
    : `<span class="lact">
        <button class="iconbtn" data-cl="${i}" data-act="pin" aria-pressed="${!!pinned}" title="${pinned ? "Pinned: Rebuild keeps it as it is" : "Pin: Rebuild keeps it as it is"}" aria-label="${pinned ? "Unpin" : "Pin"} loadout ${i + 1}">${PIN}</button>
        <button class="btn sm" data-cl="${i}" data-camp="swap" aria-expanded="${open}"${row?.done ? ` title="Changes the ${plural(row.left, "run")} still to play; the ${row.done} played stay as they are"` : ""}>${open ? "Close" : "Swap…"}</button>
        <button class="iconbtn" data-cl="${i}" data-act="menu" aria-haspopup="menu" aria-label="More for loadout ${i + 1}">⋯</button></span>`;
  const earns = () => {
    const parts = [...want].map(([k, rk]) => `${k} ${rank3(rk)}`).concat(comp ? [`Completionist ${comp}`] : []);
    return `<div class="rearns"><b>${nums.length > 1 ? `Runs ${campNums(nums)} earn` : `Run ${nums[0]} earns`}</b> ${esc(parts.join(" · ") || "no new passive")} · ${xp} XP to level${past.size ? `<div class="dim" style="margin-top: 2px">${esc([...past].join(", "))} ${past.size > 1 ? "are" : "is"} 3/3 already: ${past.size > 1 ? "their ranks go" : "its rank goes"} up anyway, which adds XP to level and no passive.</div>` : ""}</div>`;
  };
  const note = toPlay && row?.warnings?.length ? `<div class="rnote">${row.warnings.map(esc).join(" ")}</div>` : "";
  return `<div class="rline${isNext ? " next" : ""}${pinned ? " pinned" : ""}${L.g === "played" ? " played" : ""}">
      <span class="ln num" title="${nums.length > 1 ? "Its runs" : "Its place"} in your campaign">${campRunsText(nums)}</span>${cells}
      <span class="st">${status}</span><span class="lxp num" title="XP to level: the bar is that much bigger. Mobs give the same XP.">${xp}</span>${acts}${note}
      ${open ? campSwapPanel(i, l, earns()) : ""}</div>`;
}

/** A played run from before the plan or off it: its race and classes with the ranks it reached then, its date, a lock. */
function campPlayedLine(x, no = x.n) {
  const rk = {};
  x.gains.forEach(g => { if (g.kind !== "completionist") rk[g.key] = g.rank; });
  const cell = name => `<span class="rcell"><b>${esc(name)}</b>${campRlPips(rk[name] ?? PASSIVE_MAX, rk[name] ?? PASSIVE_MAX)}</span>`;
  return `<div class="rline played"><span class="ln num">${no}</span>${cell(x.race)}${x.classes.map(cell).join("")}
      <span class="st">${x.at ? esc(dt(x.at).split(",")[0]) : ""}</span><span class="lxp"></span><span class="lact dim" title="Played: nothing in the plan changes it">${LOCK}</span></div>`;
}

/** A group's header: its title, what the round does, and Fold or Show. */
function campGroupHead(g, title, sub, n) {
  const f = campFolded.has(g);
  return `<div class="rhead"><span class="rt">${title}</span><span class="rs">${sub}${f ? ` · ${plural(n, "loadout")}` : ""}</span><span class="grow"></span>
      <button class="link" data-camp="fold" data-g="${g}" aria-expanded="${!f}">${f ? "Show" : "Fold"}</button></div>`;
}

/** The key for the pips and names, naming only what is drawn. */
function campRunKey(lines) {
  const runs = lines.filter(L => L.g !== "played").flatMap(L => L.runs);
  const anyTop = runs.some(x => x.gains.some(g => g.kind !== "completionist" && g.rank === PASSIVE_MAX)), anyPast = runs.some(x => x.past.length);
  return `<div class="rl-key"><span>Pips show each rank once the line is played:</span>
      <span><span class="pips3" aria-hidden="true"><i class="have"></i></span>you have it</span>
      <span><span class="pips3" aria-hidden="true"><i class="plan"></i></span>this plan earns it</span>
      <span><span class="pips3" aria-hidden="true"><i></i></span>not yet</span>
      ${anyTop ? `<span><b class="g">WAR</b> reaches 3/3 here</span>` : ""}${anyPast ? `<span><b class="p">WAR</b> 3/3 already: more XP to level, no passive</span>` : ""}</div>`;
}

function campList(v, p, busy) {
  const loadouts = v.plan.loadouts, lines = campLines(v), line = L => campLine(v, L, loadouts);
  let html = "";
  const donePlan = lines.filter(L => L.g === "played"), before = v.before ?? 0;
  if (before || donePlan.length) {
    const last = Math.max(before, ...donePlan.flatMap(L => L.runs.map(x => campRunNo(x.n)))), unseen = v.unseen ?? 0;
    html += `<div class="rhead fold"><span class="rt">Played · ${last === 1 ? "run 1" : `runs 1–${last}`}</span><span class="rs">in your ranks already: nothing in the plan changes them</span><span class="grow"></span>
      <button class="link" data-camp="played" aria-expanded="${campShowPlayed}">${campShowPlayed ? "Hide" : "Show"}</button></div>`;
    if (campShowPlayed) html += (unseen ? `<div class="rline played"><span class="ln num">${unseen === 1 ? "1" : `1–${unseen}`}</span><span class="dim" style="grid-column: 2 / -2">${plural(unseen, "run")} your log didn't see: in your ranks from the counts on Roster</span><span class="lact dim">${LOCK}</span></div>` : "")
      + (v.played ?? []).map(x => campPlayedLine(x)).join("") + donePlan.flatMap(L => L.runs).map(x => campPlayedLine(x, campRunNo(x.n))).join("");
  }
  const lev = lines.filter(L => L.g === "leveling");
  if (lev.length) html += `<div class="rhead"><span class="rt">Leveling now</span><span class="rs">${plural(lev.length, "run")} reincarnated, below ${cap()}</span></div>${lev.map(line).join("")}`;
  for (const g of ["r1", "r2", "r3", "races"]) {
    const ls = lines.filter(L => L.g === g);
    if (!ls.length) continue;
    const runs = ls.flatMap(L => L.runs), ns = runs.map(x => campRunNo(x.n)), xs = runs.map(x => x.xp);
    const xpw = Math.min(...xs) === Math.max(...xs) ? xpWords(xs[0]) : `${xpPct(Math.min(...xs))} to ${xpWords(Math.max(...xs))}`;
    const comp = runs.flatMap(x => x.gains.filter(q => q.kind === "completionist").map(q => `Completionist ${q.rank} at run ${campRunNo(x.n)}`));
    const sub = [Math.min(...ns) === Math.max(...ns) ? `run ${ns[0]}` : `runs ${Math.min(...ns)}–${Math.max(...ns)}`, xpw, ...comp, g === "races" ? "every class is 3/3 by now" : ""].filter(Boolean).join(" · ");
    html += campGroupHead(g, g === "races" ? "Races" : `Round ${g[1]} · classes to ${g[1]}/3`, sub, ls.length) + (campFolded.has(g) ? "" : ls.map(line).join(""));
  }
  return `<section class="card camp-list" style="padding: 14px 18px 12px" aria-label="Your runs">
    <div class="between"><div class="h2">Your runs</div><span class="dim num">${busy ? "updating…" : `${v.before ? `${v.before} played before · ` : ""}${v.done - v.leveling} done · ${v.leveling ? `${v.leveling} leveling · ` : ""}${v.left} to go · ${plural(loadouts.length, "loadout")}`}</span></div>
    <div class="dim" style="font-size: 12px; margin-top: 2px; line-height: 1.4">In the order you'd play them, a round at a time. Swap… shows the cheapest changes that keep every goal, or lets you pick a race or class yourself. A loadout you change by hand is pinned: changing your goals re-plans the rest around it.</div>
    ${campRunKey(lines)}
    <div class="rl-cols"><span class="r">Run</span><span>Race</span><span style="grid-column: span 3">Classes</span><span></span><span class="r">XP to level</span><span></span></div>
    <div${busy ? ' style="opacity: .55; pointer-events: none"' : ""}>${html}</div>
    <div class="row" style="margin-top: 10px; flex-wrap: wrap"><button class="btn" data-camp="add">Add a loadout</button>
      <span class="dim" style="font-size: 11.5px; line-height: 1.45; flex: 1; min-width: 260px">A run raises its race and its three classes one rank each, and relevels at the highest of them: each rank makes every level need 10% more XP. Mobs give the same XP; the bar is bigger. No hours: how long a trio takes depends on what its classes do together, so Pathfinder doesn't guess it.</span></div>
  </section>`;
}

/** Swap…: the cheapest changes that keep every goal (from Pathfinder), or a race or class picked by hand. */
function campSwapPanel(i, l, earns = "") {
  const o = campOpts?.index === i ? campOpts.options : undefined;
  const swaps = o === undefined ? `<span class="dim">Working it out…</span>`
    : !o?.swaps?.length ? `<span class="dim">No change keeps every goal.</span>`
    : o.swaps.map((s, j) => `<button class="fixbtn" data-swap="${j}"><b>${esc(s.label)}</b><span>${esc(campNotes(s.delta))}</span></button>`).join("");
  const chip = (slot, text, title) => `<button class="chip" data-cl="${i}" data-pick="${slot}" title="${esc(title)}" aria-haspopup="dialog">${esc(text)}</button>`;
  return `<div class="lswap">${earns}
    <div class="lbl">Changes that keep every goal, cheapest first</div>
    <div class="fixes" style="margin-top: 6px">${swaps}</div>
    <div class="row" style="margin-top: 8px; gap: 6px; flex-wrap: wrap"><span class="dim" style="font-size: 12px">Or pick one yourself:</span>
      ${chip("race", raceAbbr(l.race), `Change the race (${l.race})`)}${l.classes.map((c, k) => chip(k, c, `Change ${c}`)).join("")}</div>
  </div>`;
}

function campAskOptions(i) {
  const p = campPlan();
  campOpts = { index: i, plan: JSON.stringify(p), options: undefined };
  post("campaignOptions", { value: p, index: i });
}
onHost(e => {   // through core.js, so a report's screenshot can hold it
  if (e.data?.kind !== "campaignOptions" || campOpts?.index !== e.data.index) return;
  campOpts.options = e.data.options ?? null;
  if (page === "campaign") renderCampaign();
});

// ---- the popover: the picker (every race or class for one slot, with what it does) and a loadout's ⋯ menu
function campPopEl() {
  let el = $("#campPop");
  if (!el) {
    el = document.createElement("div");
    el.id = "campPop"; el.className = "camp-pop"; el.hidden = true;
    el.addEventListener("click", e => { const t = e.target.closest("button"); if (t) campClick(t); });
    document.body.appendChild(el);
  }
  return el;
}
function campOpenPop(pop, anchor) {
  if (campPop && campPop.kind === pop.kind && campPop.i === pop.i && campPop.slot === pop.slot) return campClosePop(true);
  campPop = { ...pop, anchor: campFocusKeyOf(anchor) };
  if (pop.kind === "pick" && (campOpts?.index !== pop.i || campOpts.plan !== JSON.stringify(campPlan()))) campAskOptions(pop.i);
  campRenderPop(anchor);
  campPopEl().querySelector(".pt.best, button:not([disabled])")?.focus();
}
const campFocusKeyOf = el => {
  const a = el; if (!a) return null;
  const sel = ["data-cl", "data-act", "data-pick", "data-ci", "data-combo-act", "data-first", "data-fk"].filter(n => a.hasAttribute(n)).map(n => `[${n}="${CSS.escape(a.getAttribute(n))}"]`).join("");
  return sel ? "#campBody " + a.tagName.toLowerCase() + sel : null;
};
function campClosePop(refocus) {
  const el = $("#campPop");
  if (el) { el.hidden = true; el.innerHTML = ""; }
  campPopHtml = "";
  const back = campPop?.anchor;
  campPop = null; campPopAt = null;
  if (refocus && back) $(back)?.focus();
}
function campRenderPop(anchorEl) {
  const el = campPopEl(), pop = campPop, p = campPlan(), l = p.loadouts[pop.i];
  if (!l && pop.kind !== "combo" && pop.kind !== "first") return campClosePop();
  let html;
  if (pop.kind === "combo") html = comboMenuHtml(pop.i, p);
  else if (pop.kind === "first") { html = firstMenuHtml(pop.i, p); if (!html) return campClosePop(); }
  else if (pop.kind === "menu") {
    const v = campView(), row = campLocal ? null : v.loadouts[pop.i], left = row ? row.left : l.times, done = row?.done ?? 0;
    // Played runs are history: they keep their place, ahead of every run still to play, and can't be removed.
    const doneAt = j => campLocal ? 0 : v.loadouts[j]?.done ?? 0;
    const remove = !done ? `<button role="menuitem" class="danger" data-cl="${pop.i}" data-act="del">Remove loadout</button>`
      : left ? `<button role="menuitem" class="danger" data-cl="${pop.i}" data-act="trim">Remove the ${plural(left, "run")} still to play<span>The ${done} played stay</span></button>`
      : `<button role="menuitem" disabled>Remove loadout<span>Played: it stays in your campaign</span></button>`;
    html = `<div class="cmenu" role="menu" aria-label="Loadout ${pop.i + 1}">
      <div class="cm-row"><span>Runs</span><div class="seggroup sm" role="group" aria-label="Runs">${[1, 2, 3].map(t =>
        `<button data-cl="${pop.i}" data-times="${t}" class="${t === l.times ? "on" : ""}" aria-pressed="${t === l.times}"${t < done ? " disabled" : ""}>${t}</button>`).join("")}</div></div>
      <button role="menuitem" data-cl="${pop.i}" data-act="up"${pop.i === 0 || done || doneAt(pop.i - 1) ? " disabled" : ""}>Move up</button>
      <button role="menuitem" data-cl="${pop.i}" data-act="down"${pop.i === p.loadouts.length - 1 || done ? " disabled" : ""}>Move down</button>
      ${comboMakeItem(pop.i, l, p)}
      <button role="menuitem" data-cl="${pop.i}" data-act="mark"${left ? "" : " disabled"}>Mark a run done<span>For a reincarnation while your log was off</span></button>
      ${l.marked ? `<button role="menuitem" data-cl="${pop.i}" data-act="unmark">Undo a mark (${l.marked})</button>` : ""}
      ${remove}
    </div>`;
  } else {
    const r = data.reinc, v = campView(), ends = campEnds(v), race = pop.slot === "race";
    const o = campOpts?.index === pop.i ? campOpts.options : undefined;
    const cur = race ? l.race : l.classes[pop.slot];
    const head = `<div class="pop-h"><div><b>${race ? "Race" : "Class"} for loadout ${pop.i + 1}, in place of ${esc(cur)}</b>
        <div class="dim" style="font-size: 12px; margin-top: 2px">${esc(l.race)} · ${esc(trioOf(l))}. Pips: where each one ends in this plan. Under it: what the change does, runs first, then XP.</div></div>
      <button class="iconbtn" data-camp="close-pop" aria-label="Close">✕</button></div>`;
    const tile = (c, key, short, name, extra) => {
      const goal = race ? (p.races[key] ?? 0) : (p.classes[key] ?? 0);
      const now = race ? (r.races.find(x => x.key === key)?.rank ?? 0) : (r.classes.find(x => x.key === key)?.rank ?? 0);
      const end = race ? (ends.r[key] ?? now) : (ends.c[key] ?? now);
      const top = `<span class="pt-top"><b>${esc(short)}</b>${campPips(now, end, goal)}</span><span class="pt-n">${esc(name)}${extra ? ` · ${esc(extra)}` : ""}</span>`;
      if (!c || c.state === "current" || c.state === "trio")
        return `<button class="pt${c?.state === "current" ? " cur" : ""}" disabled>${top}<span class="pt-s dim">${c?.state === "current" ? "in this slot" : c?.state === "trio" ? "already in this trio" : "…"}</span></button>`;
      const [cls, text] = campStatus(c.delta), best = c.state === "best";
      return `<button class="pt${best ? " best" : ""}" data-choose="${esc(key)}" aria-label="${esc(name)}: ${esc(text)}, ${esc(campCost(c.delta))}${best ? ", the cheapest pick" : ""}">${top}
        <span class="pt-s ${cls}">${esc(text)}</span><span class="pt-h">${best ? `<b class="gold">Cheapest</b> · ` : ""}${esc(campCost(c.delta))}</span></button>`;
    };
    let body;
    if (o === undefined) body = `<div class="dim" style="padding: 10px 0">Working it out…</div>`;
    else if (!o) body = `<div class="dim" style="padding: 10px 0">Nothing to show for this loadout.</div>`;
    else if (race) {
      const by = Object.fromEntries(o.races.map(c => [c.value, c])), locked = new Set(p.locked);
      const grp = (title, list) => list.length ? `<div class="pgrp"><div class="lbl">${title}</div><div class="ptl">${list.map(x => tile(by[x.key], x.key, raceAbbr(x.key), x.name, "")).join("")}</div></div>` : "";
      body = grp("Unlocked", r.races.filter(x => !locked.has(x.key))) + grp("Not unlocked yet: planned last", r.races.filter(x => locked.has(x.key)));
    } else {
      const by = Object.fromEntries(o.classes[pop.slot].map(c => [c.value, c]));
      const role = c => c.healer ? "heal" : c.carry ? "carry" : "other";
      const grp = (title, key) => `<div class="pgrp"><div class="lbl">${title}</div><div class="ptl">${r.classes.filter(c => role(c) === key).map(c => tile(by[c.key], c.key, c.key, c.name, c.carry ? c.carry.replace(c.key + " ", "") : "")).join("")}</div></div>`;
      body = grp("Heals", "heal") + grp("A carry", "carry") + grp("Others", "other");
    }
    html = `<div role="dialog" aria-label="Pick a ${race ? "race" : "class"}">${head}${body}</div>`;
  }
  // The page redraws on every refresh from Pathfinder: rewrite the popover only when it changed, so focus stays put.
  if (campPopHtml !== html) {
    const inside = el.contains(document.activeElement);
    el.innerHTML = campPopHtml = html;
    if (inside) el.querySelector(".pt.best, button:not([disabled])")?.focus();
  }
  el.hidden = false;
  const anchor = anchorEl ?? (pop.anchor ? $(pop.anchor) : null);
  if (!anchor) return;
  const a = anchor.getBoundingClientRect(); campPopAt = { top: a.top, left: a.left };
  const w = Math.min(pop.kind !== "pick" ? 250 : 640, innerWidth - 24);
  el.style.width = w + "px";
  el.style.left = Math.max(12, Math.min(a.left, innerWidth - w - 12)) + "px";
  const h = el.offsetHeight, below = a.bottom + 6;
  el.style.top = (below + h <= innerHeight - 12 ? below : a.top - h - 6 >= 12 ? a.top - h - 6 : Math.max(12, innerHeight - h - 12)) + "px";
}
document.addEventListener("mousedown", e => { if (campPop && !$("#campPop")?.contains(e.target) && !e.target.closest("[data-pick], [data-act='menu'], [data-combo-act='menu'], [data-first='menu']")) campClosePop(); });
document.addEventListener("keydown", e => { if (e.key === "Escape" && campPop) campClosePop(true); });
// A scroll closes the popover only when it moved the button it hangs from: the browser delivers the scroll of a focus a
// frame late, after the popover opened over a button that is where it was.
const campPopMoved = () => {
  const a = campPop?.anchor ? $(campPop.anchor) : null, at = campPopAt;
  if (!a || !at) return true;
  const r = a.getBoundingClientRect();
  return Math.abs(r.top - at.top) > 1 || Math.abs(r.left - at.left) > 1;
};
$("#main").addEventListener("scroll", () => { if (campPop && campPopMoved()) campClosePop(); }, { passive: true });

// ---- edits
function campNewLoadout(p) {
  const r = data.reinc, used = new Set(p.loadouts.map(l => l.race));
  const race = [...r.races].sort((a, b) => (used.has(a.key) - used.has(b.key)) || a.rank - b.rank)[0].key;
  const classes = [...r.classes].sort((a, b) => a.rank - b.rank).slice(0, 3).map(c => c.key);
  return { race, classes, times: 1, pinned: false, marked: 0 };
}

function campClick(t) {
  if (!t || t.disabled || !campView()) return;
  const p = campCopy(campPlan());
  if (t.dataset.preset) {
    if (t.dataset.preset === "custom") { $("#campBody [data-goal]")?.focus(); return; }
    const r = data.reinc;
    p.classes = {}; p.races = {}; p.loadouts = []; p.since = null;
    r.classes.forEach(c => p.classes[c.key] = PASSIVE_MAX);
    // Everything 3/3 makes every race a goal, keeps the races you ranked (at 3/3) and ranks no other, as Campaign.Presets prices it; Every class 3/3 ranks none
    if (t.dataset.preset === "all") firstEveryRace(p);
    else p.raceOrder = [];
    return sendCamp(p, true);
  }
  if (campComboClick(t, p) || campFirstClick(t, p)) return;
  const act = t.dataset.camp;
  if (act === "got") { campCardClosed = true; post("setting", { key: "plannerCard", value: "seen" }); renderCampaign(); return; }
  if (act === "close-pop") return campClosePop(true);
  if (act === "rebuild") return sendCamp(p, true);
  if (act === "more") { campMore = !campMore; renderCampaign(); return; }
  if (act === "clear") {
    if (Date.now() - campClearAt < 4000) { campClearAt = 0; return sendCamp(campCopy(EMPTY_CAMP), false); }
    campClearAt = Date.now(); renderCampaign(); $('[data-camp="clear"]')?.focus();
    // It goes back by itself, where it is: the page drawn anew would scroll back up to the button with the focus (#24).
    setTimeout(() => { const b = $('[data-camp="clear"]'); if (b) [b.className, b.textContent] = campClearLook(); }, 4100);
    return;
  }
  // A loadout you add or change by hand is pinned, so a change to your goals re-plans around it.
  if (act === "add") { p.loadouts.push({ ...campNewLoadout(p), pinned: true }); return sendCamp(p, false, `[data-cl="${p.loadouts.length - 1}"][data-camp="swap"]`); }
  if (act === "goal-all" || act === "goal-classes") {
    // A lit one is a state, not a switch. An unlit one sets those goals and rebuilds around the plan's pins, played runs, combos and timings.
    if (t.getAttribute("aria-pressed") === "true") return;
    const all = act === "goal-all";
    data.reinc.classes.forEach(c => p.classes[c.key] = PASSIVE_MAX);
    if (all) {
      firstEveryRace(p);
    } else { p.races = {}; p.first = p.first.filter(g => !g.startsWith("race:")); p.raceOrder = []; }
    campGoalsChanged(p, `[data-camp="${act}"]`);
    return;
  }
  if (act === "no-goals") { p.classes = {}; p.races = {}; p.first = []; p.classOrder = []; p.raceOrder = []; return campGoalsChanged(p, `[data-camp="goal-classes"]`); }
  if (act === "fold") { const g = t.dataset.g; if (!campFolded.delete(g)) campFolded.add(g); renderCampaign(); return $(`[data-camp="fold"][data-g="${g}"]`)?.focus(); }
  if (act === "played") { campShowPlayed = !campShowPlayed; renderCampaign(); return $('[data-camp="played"]')?.focus(); }
  if (act === "swap") {
    const i = Number(t.dataset.cl), row = campLocal ? null : campView().loadouts[i];
    if (campSwap !== i && row?.done && row.left) {
      // Part played: the played runs keep this loadout, and the runs still to play become a loadout of their own to change.
      const l = p.loadouts[i];
      p.loadouts.splice(i, 1, { ...l, classes: [...l.classes], times: row.done }, { ...l, classes: [...l.classes], times: row.left, marked: 0 });
      sendCamp(p, false);
      campSwap = i + 1;
      campAskOptions(i + 1);
      renderCampaign();
      return $(`[data-cl="${i + 1}"][data-camp="swap"]`)?.focus();
    }
    campSwap = campSwap === i ? null : i;
    if (campSwap != null) campAskOptions(i);
    renderCampaign();
    return $(`[data-cl="${i}"][data-camp="swap"]`)?.focus();
  }
  if (t.dataset.goal && t.dataset.k) {
    const g = t.dataset.goal, kind = g.slice(0, g.indexOf(":")), key = g.slice(g.indexOf(":") + 1), k = Number(t.dataset.k);
    const m = kind === "class" ? p.classes : p.races, to = (m[key] ?? 0) === k ? k - 1 : k;
    if (to > 0) m[key] = to; else {
      delete m[key]; p.first = p.first.filter(x => x !== g);
      const ord = kind === "class" ? p.classOrder : p.raceOrder;   // a rank needs a goal
      if (ord) { const left = ord.filter(w => w.key !== key); if (kind === "class") p.classOrder = left; else p.raceOrder = left; }
    }
    return campGoalsChanged(p, `[data-goal="${CSS.escape(g)}"][data-k="${k}"]`);
  }
  if (t.dataset.fix) {
    const f = campView().fixes?.[Number(t.dataset.fix)];
    if (f) { p.loadouts = campCopy({ loadouts: f.loadouts }).loadouts; return sendCamp(p, false); }
    return;
  }
  if (t.dataset.swap) {
    const s = campOpts?.options?.swaps?.[Number(t.dataset.swap)], i = campSwap;
    if (s) {
      // The loadouts the swap changed (one, or two for a trade) are yours now: pinned.
      const was = p.loadouts.map(l => l.race + "|" + l.classes.join("/"));
      p.loadouts = campCopy({ loadouts: s.loadouts }).loadouts;
      p.loadouts.forEach((l, j) => { if (was[j] !== l.race + "|" + l.classes.join("/")) l.pinned = true; });
      return sendCamp(p, false, `[data-cl="${i}"][data-camp="swap"]`);
    }
    return;
  }
  if (t.dataset.choose != null && campPop) {
    const { i, slot } = campPop, l = p.loadouts[i];
    if (!l) return;
    if (slot === "race") l.race = t.dataset.choose; else l.classes[slot] = t.dataset.choose;
    l.pinned = true;
    return sendCamp(p, false, `[data-cl="${i}"][data-camp="swap"]`);
  }
  if (t.dataset.pick != null) {
    const i = Number(t.dataset.cl);
    return campOpenPop({ kind: "pick", i, slot: t.dataset.pick === "race" ? "race" : Number(t.dataset.pick) }, t);
  }
  if (t.dataset.lock) {
    const k = t.dataset.lock;
    p.locked = p.locked.includes(k) ? p.locked.filter(x => x !== k) : [...p.locked, k];
    return campGoalsChanged(p, `[data-lock="${CSS.escape(k)}"]`);
  }
  if (t.dataset.cl == null) return;
  const i = Number(t.dataset.cl), l = p.loadouts[i];
  if (!l) return;
  if (t.dataset.act === "menu") return campOpenPop({ kind: "menu", i }, t);
  const menu = `[data-cl="${i}"][data-act="menu"]`;
  if (t.dataset.times) { l.times = Number(t.dataset.times); l.marked = Math.min(l.marked ?? 0, l.times); l.pinned = true; return sendCamp(p, false, menu); }
  // Played runs stay put (the menu greys these out; this holds even if a click slips through).
  const doneAt = j => campLocal ? 0 : campView().loadouts[j]?.done ?? 0;
  if ((t.dataset.act === "del" || t.dataset.act === "down") && doneAt(i)) return;
  if (t.dataset.act === "up" && (doneAt(i) || doneAt(i - 1))) return;
  switch (t.dataset.act) {
    case "trim": if (doneAt(i)) { l.times = doneAt(i); return sendCamp(p, false, menu); } return;
    case "pin": l.pinned = !l.pinned; return sendCamp(p, false, `[data-cl="${i}"][data-act="pin"]`);
    case "up": if (i > 0) { [p.loadouts[i - 1], p.loadouts[i]] = [l, p.loadouts[i - 1]]; return sendCamp(p, false, `[data-cl="${i - 1}"][data-act="menu"]`); } return;
    case "down": if (i < p.loadouts.length - 1) { [p.loadouts[i + 1], p.loadouts[i]] = [l, p.loadouts[i + 1]]; return sendCamp(p, false, `[data-cl="${i + 1}"][data-act="menu"]`); } return;
    case "del": p.loadouts.splice(i, 1); return sendCamp(p, false, p.loadouts.length ? `[data-cl="${Math.min(i, p.loadouts.length - 1)}"][data-act="menu"]` : null);
    case "mark": l.marked = Math.min(l.times, (l.marked ?? 0) + 1); return sendCamp(p, false, menu);
    case "unmark": l.marked = Math.max(0, (l.marked ?? 0) - 1); return sendCamp(p, false, menu);
  }
}

$("#page-campaign").addEventListener("click", e => campClick(e.target.closest("button")));

// ---- Legacy planner migration notice (shown when legacy plans remain, until dismissed)

/** At the top of Campaign until Got it, which removes the old plans; the engine says whether it shows (newPlanner.oldPlans). */
function campAcross(v) {
  if (!v.newPlanner?.oldPlans || campCardClosed) return "";
  const line = t => `<div class="muted" style="margin-top: 4px">· ${t}</div>`;
  return `<section class="card camp-across" style="padding: 16px 20px" aria-label="The new planner">
    <div class="h2">Campaign has a new planner</div>
    <div class="muted" style="margin-top: 6px; line-height: 1.5; max-width: 860px">Plans made on the old planner have been removed: they don't carry over to the new one. Sorry for the inconvenience. Your ranks and the runs you've played are untouched, and so is any plan you made with the new planner in the beta.</div>
    <div class="lbl" style="margin-top: 12px">The short version</div>
    ${line("Your goals: pick a ready-made goal, or click a pip for the rank you want. Pathfinder plans the cheapest way there: the fewest runs, then the least XP to level.")}
    ${line("Classes you want first and Races you want first: click one to earn it first, or Unlock now to play its runs back to back.")}
    ${line("Your combos: classes you want together or apart, or a trio to play. A Must is held at the price shown; a Prefer is kept wherever it costs nothing.")}
    ${line("Class roles, the tab beside Campaign: your carries, best first, and the survival check: each trio holds up where you level.")}
    <div class="row" style="margin-top: 14px; gap: 10px; flex-wrap: wrap"><button class="btn primary" data-camp="got">Got it</button></div>
  </section>`;
}
