"use strict";
let data = null;
/** The farmed difficulties were just changed in Settings: Home and Crawls show their verdicts faded until the next data message. */
let farmWaiting = false;
const $ = s => document.querySelector(s);
const $$ = s => [...document.querySelectorAll(s)];
const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const fmt = (v, d = 1) => (v == null || isNaN(v)) ? "" : Number(v).toFixed(d);
const signed = (v, d = 1) => v == null ? "—" : (v < 0 ? "−" : "") + Math.abs(v).toFixed(d);
const plural = (n, w) => `${n} ${w}${n === 1 ? "" : "s"}`;
const zoneShort = (z, d) => (z || "").replace(/^The /, "") + (d != null ? " D" + d : "");
/** The runs a usual needs, from the engine (RunPace.MinRuns); 3 only until the first data arrives. */
const usualRuns = () => data?.reinc?.usualRuns ?? 3;
/** Every clock time the page has shown, as written: Report a problem hides each one from its screenshot (report.js), in any locale. */
const shownClocks = new Set();
const clockShown = t => { if (t) shownClocks.add(t); return t; };
const dt = s => {
  const d = new Date(s);
  if (isNaN(d)) return "";
  clockShown(d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", hour12: false }));
  return d.toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit", hour12: false });
};
/** A clock time, hours and minutes (the page's rule for a clock time: it stays on this PC and is hidden from a report's screenshot). */
const hm = t => clockShown(new Date(t).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", hour12: false }));
const trioSpaced = t => esc(t || "").replaceAll("/", " / ");
const TIERS = ["", "Infinitesimal", "Minor", "Lesser", "Potential", "Major", "Greater", "Superior", "Grand", "Ascendant", "Infinite"];
const post = (type, extra) => window.chrome?.webview?.postMessage(Object.assign({ type }, extra || {}));
const store = {
  get(k, d) { try { return localStorage.getItem(k) ?? d; } catch { return d; } },
  set(k, v) { try { localStorage.setItem(k, v); } catch { /* private storage off */ } },
};

/** The tick in a checkbox-style toggle (.tog .box). */
const CHECK = '<svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="#11161c" stroke-width="3.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg>';

// ---------------------------------------------------------------- pages
const PAGES = ["home", "ask", "campaign", "classroles", "roster", "crawls", "loot", "relevels", "upgrade", "help", "settings"];
/** Each page as the rail and its tabs name it (Report a problem and Your reports say where a report came from). */
const PAGE_NAMES = {
  home: "Home", ask: "Ask", campaign: "Plan → Campaign", classroles: "Plan → Class roles", roster: "Plan → Roster", crawls: "History → Visits", loot: "History → Loot",
  relevels: "History → Relevels", upgrade: "Upgrades", help: "Help", settings: "Settings",
};
// Rail entries with more than one page: a tab strip at the top switches between them, and the rail opens the last one used.
// A group's name must never match a page's name: show() sends a group to its last tab, so a tab sharing
// its group name would become unreachable once another tab was opened.
const GROUPS = { planning: [["campaign", "Campaign"], ["classroles", "Class roles"], ["roster", "Roster"]], history: [["crawls", "Visits"], ["loot", "Loot"], ["relevels", "Relevels"]] };
const tabsOf = g => GROUPS[g];
const groupOf = p => Object.keys(GROUPS).find(g => GROUPS[g].some(([x]) => x === p)) ?? p;
/** Pages that moved: Route is Ask's "Every place"; Plan → Next trio (hours forecast for every trio) became Campaign. */
const MOVED = { route: "ask", plan: "campaign" };
let page = store.get("page", "home");
page = MOVED[page] ?? page;
if (!PAGES.includes(page)) page = "home";

let seenPage = null;   // usage counts: the page last reported as opened
function show(p, keepScroll) {
  if (p === "route") askState.q = "places";   // old links to Route
  p = MOVED[p] ?? p;
  if (GROUPS[p]) p = MOVED[store.get("tab-" + p, "")] ?? store.get("tab-" + p, GROUPS[p][0][0]);
  if (!PAGES.includes(p)) p = "home";
  const changed = p !== page;
  const was = page;
  page = p;
  if (changed && was === "campaign") window.campLeft?.();   // the one-time race list line is over
  if (p !== seenPage) { seenPage = p; post("seen", { page: p }); }
  store.set("page", p);
  const g = groupOf(p);
  if (GROUPS[g]) store.set("tab-" + g, p);
  $$(".rail button[data-page]").forEach(b => { const on = b.dataset.page === g; b.classList.toggle("on", on); if (on) b.setAttribute("aria-current", "page"); else b.removeAttribute("aria-current"); });
  $$(".subtabs").forEach(el => {
    if (el.dataset.group !== g) return;
    el.innerHTML = `<div class="seggroup" role="tablist" aria-label="${esc(g)}">${tabsOf(g).map(([x, label]) =>
      `<button role="tab" class="${x === p ? "on" : ""}" aria-selected="${x === p}" data-go="${x}">${esc(label)}</button>`).join("")}</div>`;
  });
  $$("section.page").forEach(s => s.hidden = s.id !== "page-" + p);
  if (changed || !keepScroll) $("#main").scrollTop = 0;
  if (p === "home") drawRace();
  if (p === "relevels") drawRelevels();
  if (p === "loot") { if (changed) post("loot"); drawLootChart(); }
  if (p === "upgrade") { syncUpForm(); if (changed) requestUpgrade(true); }
  if (p === "settings" && changed) post("share", { action: "status" });
  if (p === "help" && changed) { post("roadmap"); post("myReports"); }
  if (p === "ask" && changed) { if (!askAnswer) renderAsk(); post("ask", askState); }
}
window.show = show;
$$(".rail button[data-page]").forEach(b => b.addEventListener("click", () => show(b.dataset.page)));
document.addEventListener("click", e => {
  const g = e.target.closest("[data-go]");
  if (!g) return;
  // A link can open Ask on a question: data-ask-q="farm" (Home's "Every farm at 50").
  if (g.dataset.askQ) { if (g.dataset.askPick) askState.goal = g.dataset.askPick; askState.q = g.dataset.askQ; store.set("askState", JSON.stringify(askState)); askAnswer = null; }
  show(g.dataset.go);
  // A link can point at one setting (data-anchor="farmRateSetting", FARM_CHANGE) or a Help card (an (i)'s link): scroll to
  // it and mark it for a moment. A card taller than half the window shows from its top.
  const spot = g.dataset.anchor && document.getElementById(g.dataset.anchor);
  if (spot) {
    spot.scrollIntoView({ block: spot.offsetHeight > innerHeight / 2 ? "start" : "center" });
    spot.classList.remove("flash"); void spot.offsetWidth; spot.classList.add("flash");
  }
});

// ---------------------------------------------------------------- messages from the window
// Every script listens through onHost. While Report a problem takes its screenshot (report.js), messages wait in
// heldMessages and run afterwards, so nothing redraws with a name or a clock time showing in the middle of it.
const hostListeners = [];
let heldMessages = null;
const onHost = fn => hostListeners.push(fn);
// Every listener gets the message even when one throws, as with separate listeners; then the first error is thrown on,
// so it still reaches the console and tools/webcheck.mjs (which fails on a name nothing declares).
function dispatchHost(e) {
  let first = null;
  for (const fn of hostListeners) {
    try { fn(e); } catch (err) { first ??= err; }
  }
  if (first) throw first;
}
function releaseHeld() {
  const held = heldMessages ?? [];
  heldMessages = null;
  for (const e of held) {
    try { dispatchHost(e); } catch (err) { console.error(err); }
  }
}
window.chrome?.webview?.addEventListener("message", e => {
  if (heldMessages && e.data?.kind !== "reportShot") { heldMessages.push(e); return; }
  dispatchHost(e);
});

onHost(e => {
  if (e.data?.kind === "loot") { lootRows = e.data.rows; renderLoot(); return; }
  if (e.data?.kind === "sharePreview") { sharePreview = e.data; shareBusy = false; renderShare(); return; }
  if (e.data?.kind === "shareStatus") { shareStatus = e.data; renderShare(); syncShareCard(); renderHomeShare(); return; }
  if (e.data?.kind === "ask") { askAnswer = e.data; renderAsk(); return; }
  if (e.data?.kind === "upgrade") { upg = e.data; renderUpgrade(); return; }
  if (e.data?.kind === "usagePreview") {
    usagePreview = e.data.text;
    if (page === "settings") renderSettings();
    if (usageScroll) { usageScroll = false; document.querySelector("[data-usage]")?.scrollIntoView({ block: "center" }); }
    return;
  }
  // Any other message with a kind answers one page (Help's roadmap, Campaign's options, Report a problem): it is never the page data,
  // which has no kind. Taking it for the data blanked every page until the next refresh.
  if (e.data?.kind) return;
  data = e.data;
  farmWaiting = false;
  campOnData();
  render();
  if (page === "loot") post("loot");
  if (page === "ask") post("ask", askState);
  if (page === "upgrade") requestUpgrade(); // loot since the counts were entered adds to the bank
});

/** What a level needs, from the expansion's list (reinc.newLevels): shown in the (i) and on Help, empty before the cap rises. */
const newLevelsText = () => { const n = newLv(); return n ? `A level needs the extra XP only for the reincarnations made after your class or race reached it: levels ${n.from + 1} to ${n.to}, new with ${n.expansion}, need their usual XP until then.` : ""; };
const newLevelsHelp = () => newLevelsText() ? `<p>${newLevelsText()}</p>` : "";
function render() {
  if (!data) return;
  $("#helpNewLevels").textContent = newLevelsText() ? " " + newLevelsText() : "";
  if (data.lootRows) lootRows = data.lootRows; // developer preview: inlined
  renderHome();
  renderCampaign();
  renderClassRoles();
  renderRoster();
  renderCrawls();
  renderLoot();
  renderRelevels();
  renderSettings();
  show(page, true);
}


// ---------------------------------------------------------------- shared helpers
const cap = () => data.reinc?.levelCap ?? 50;
/** The difficulties you farm for motes (Settings → How you play), in words: "D3 and D4", "D3", "none". */
const farmWords = (diffs = data?.settings?.farmDifficulties ?? [3, 4], join = "and") => { const f = diffs.map(d => "D" + d); return f.length ? f.length === 1 ? f[0] : f.slice(0, -1).join(", ") + " " + join + " " + f.at(-1) : "no difficulty"; };
const pct = l => (l - 1) / (cap() - 1) * 100;
/** The open relevel of the trio and race on now, as the engine says (reinc.homeNow). None at the cap, or when the loadout isn't known. */
const activeJourney = () => {
  const n = data.reinc?.homeNow?.now;
  return n?.journeyIndex != null ? data.reinc.journeys[n.journeyIndex] ?? null : null;
};
const finishedJourneys = () => (data.reinc?.journeys ?? []).filter(j => j.done).sort((a, b) => a.activeHours - b.activeHours);
const pips = (rank, max = 3) => rank > 0 ? `<span class="gold" style="font-size: 9px; letter-spacing: 2px" title="rank ${rank}">${"●".repeat(Math.min(rank, max))}${rank > max ? "+" : ""}</span>` : "";
/**
 * Whether your ranks are only a guess: the log has seen no reincarnation and you set no counts, so every rank is taken
 * as none. A player who turned on /log after years of play gets a plan from scratch until they set their counts.
 */
const ranksUnconfirmed = (r = data?.reinc) => !!r && ![...r.classes, ...r.races].some(x => x.logged > 0 || x.entered != null);
/** Roster's banner, wherever a plan or a time leans on those ranks. Set counts opens Roster's count entry (roster.js). */
const countsNudge = (r, lead = "", style = "") => `<div class="banner"${style ? ` style="${style}"` : ""}><div>
    <div style="font-weight: 650">Your log shows no reincarnations${r.logStart ? ` since ${dt(r.logStart).split(",")[0]}` : ""}</div>
    <div class="muted" style="font-size: 12px; margin-top: 2px">${lead ? lead + " " : ""}Reincarnated while logging was off? Set your counts so the plan and relevel times are right.</div></div>
    <button class="btn primary" style="flex: none" data-set-counts>Set counts</button></div>`;

let metric = null;
const metricNow = () => { const g = metric || data?.settings?.goal || "balanced"; return METRICS[g] ? g : "balanced"; };
const METRICS = {
  balanced: { sort: (a, b) => (a.cost ?? Infinity) - (b.cost ?? Infinity), show: o => o.cost == null ? "—" : `${signed(o.cost)} T5 per level`,
    help: "Levels weighed against motes: the motes you give up for each level compared with farming at the farm rate. Lower is better; below zero, the crawl earns more motes an hour than the farm rate." },
  speed: { sort: (a, b) => b.levelsPerHour - a.levelsPerHour, show: o => `${fmt(o.levelsPerHour)} Lv/h`,
    help: "Levels per active hour, nothing else: for reincarnating as fast as you can." },
  motes: { sort: (a, b) => b.t5PerHour - a.t5PerHour, show: o => `${fmt(o.t5PerHour, 0)} T5-eq/h`,
    help: "T5-equivalent per active hour, whatever the levels." },
  rare: { sort: (a, b) => (b.highTierPerHour - a.highTierPerHour) || (b.t5PerHour - a.t5PerHour), show: o => `${fmt(o.highTierPerHour, 1)} above-T5 T5-eq/h`,
    help: "T5-equivalent per hour from drops above T5 only (Greater and up, fragments included): where the big ones drop." },
};
const GOAL_NAMES = { balanced: "Levels and motes", speed: "Fastest leveling", motes: "Most motes", rare: "Rare drops" };
// ---- The farm rate: what an hour at the cap is worth in motes. Ask's "Count motes too", Every place's ranking and the hours
// on Upgrades all use it, so each says where it comes from (TrackerService's farmSource): never "your farm rate" for the default.
/** The patch is named, never timed: its hour is one instant, a different clock time on every PC. */
const farmPatch = () => "11-10";
/** The difficulties the engine counted for the farm rate (sent beside it), never the setting a player just clicked: no new words on an old number. */
const farmCounted = () => data?.reinc?.farmSource?.difficulties ?? data?.reinc?.farm?.difficulties ?? [3, 4];
/** Where the farm rate in use comes from, in words (HTML): "your best farm, Plane of Hate D4 (5 crawls at 50 since the 11-10 patch)". */
/** Crawls with a rate the logs need before they set the farm rate: the engine's sample (Samples.MinFarmCrawls), never typed in a page. */
const farmNeeds = () => data?.reinc?.farmSource?.needs ?? "enough";
function farmFrom() {
  const s = data?.reinc?.farmSource;
  if (!s) return "";
  const at = `at ${cap()} since the ${farmPatch()} patch`, ds = farmCounted(), dw = farmWords(ds), dor = farmWords(ds, "or");
  const where = "(Settings → How you play)";
  if (s.kind === "best") return `your best farm, ${esc(s.zone)} (${plural(s.crawls, "crawl")} ${at})`;
  if (s.kind === "usual") return `your typical farm (the middle of your ${plural(s.crawls, "farming crawl")} ${at}, at ${dw})`;
  if (s.kind === "set") return s.fallback ? `the rate you set in Settings, used until ${farmNeeds()} of your ${dw} crawls ${at} have a rate` : "the rate you set in Settings";
  return `the default: ${!ds.length ? `you farm no difficulty for motes ${where}`
    : s.crawls ? `only ${s.crawls} of your ${dw} crawls ${at} ${s.crawls === 1 ? "has" : "have"} a rate, and it takes ${farmNeeds()}`
    : s.unrated ? `none of your ${plural(s.unrated, "farming crawl")} ${at}, at ${dw}, ${s.unrated === 1 ? "has" : "have"} a rate yet (${rateFromKills() ?? "enough"} kills, or a finished crawl), and it takes ${farmNeeds()}`
    : s.notFarmed ? `${s.notFarmed === 1 ? `your 1 crawl ${at} wasn't` : `none of your ${s.notFarmed} crawls ${at} were`} at ${dor}, the ${ds.length === 1 ? "difficulty" : "difficulties"} you farm for motes ${where}`
    : `your logs have no crawls ${at}`}`;
}
/** A link to the farm rate in Settings. */
const FARM_CHANGE = `<button class="link" data-go="settings" data-anchor="farmRateSetting">Change it in Settings →</button>`;
/** The farm rate in use and where it's from, with the link to change it (Ask, Upgrades). */
const farmRateNote = (lead = "Farm rate") => data?.reinc?.farmSource
  ? `${lead} <b style="color: var(--text)">${fmt(data.reinc.farmRate, 0)} T5-eq/h</b>${infoTip("farmRate")}, ${farmFrom()}. ${FARM_CHANGE}` : "";
/** A farm number: whole from 1 up, one decimal under 1, "under 0.1" under 0.05 (never a 0 that reads as a fact). */
const farmNum = v => v == null || isNaN(v) ? "" : v < 0.05 ? "under 0.1" : v < 0.95 ? Number(v).toFixed(1) : Number(v).toFixed(0);
/** The farm rate, where it's from, and your farming before and since the patch (Every place, Settings). */
function farmLine(link) {
  const r = data.reinc, f = r?.farm, kind = r?.farmSource?.kind;
  if (!r) return "";
  // Your farming since the patch, less what the source above already says.
  const mine = f?.crawls ? [kind !== "usual" && f.usual != null ? `${f.crawls === 1 ? `${farmNum(f.usual)} from 1 crawl` : `typical ${farmNum(f.usual)} over ${plural(f.crawls, "crawl")}`}` : "",
    kind !== "best" && f.best != null ? `best ${esc(f.bestZone)} ${farmNum(f.best)}` : ""].filter(Boolean) : [];
  const since = mine.length ? ` Your farming at ${farmWords(farmCounted())} since the patch: ${mine.join(", ")}.` : "";
  const before = f?.preCrawls ? ` Before it: typical ${farmNum(f.preUsual)}` + (f.preBest != null ? `, best ${esc(f.preBestZone)} ${farmNum(f.preBest)}` : "") + "." : "";
  return `Farm rate <b style="color: var(--text)">${fmt(r.farmRate, 0)} T5-eq/h</b>, ${farmFrom()}.${since}${before}${link ? " " + FARM_CHANGE : ""}`;
}
function ranked(opts) {
  const m = METRICS[metricNow()];
  return [...opts].sort((a, b) => { const d = m.sort(a, b); return (isNaN(d) ? 0 : d) || b.crawls - a.crawls; });
}
/** The rest of the way from where you are, as Ask answers it (its route from your level to the cap), leg by leg. */
function suggestedRoute() {
  return (data.reinc?.home?.route?.legs ?? []).map(l => ({ zone: l.place, from: l.from, to: l.to }));
}

// ---------------------------------------------------------------- (i): what a number or a word means
// A small round (i) beside a number or a term players can't decode: infoTip("t5eq"). Every text lives in INFO, worded
// from docs/ (motes-and-fragments.md, pace.md, the Help cards): a title, 1–4 short sentences, at most a tiny table, and
// the Help card that says more. HTML written here, never from data; nothing in it is sent anywhere. Not title= hover
// text: nobody found that. Click, Enter or Space opens one shared popover beside the (i) (the Campaign popover's look).
const INFO = {
  fragment: {
    title: "What's a fragment worth?",
    body: "<p>How many motes one fragment does the work of. Fragments condense 2:1 like motes but give more points the higher the item, so it's set by where your last fragment lands:</p>",
    table: { head: ["Your last fragment goes on", "Worth"], rows: [["Gear +4 or lower", "1.6 motes"], ["Spells", "2.0"], ["Gear +6 → +7", "2.29"],
      ["Gear +7 → +8", "2.75"], ["Gear +8 → +9", "3.56", "recommended"], ["Gear +9 → +10", "4.5"]] },
    foot: "<p>Not sure? Keep Recommended: take gear to +9 with motes and fragments, and save Void-Touched Potentials for +9 → +10. This only changes how loot counts in T5-eq.</p>",
    help: "help-fragment",
  },
  t5eq: {
    title: "T5-eq", q: "What's T5-eq?",
    body: "<p>T5-equivalent: every mote on one scale. A Major mote (tier 5) is 1, and each tier is worth twice the one below: Greater 2, Superior 4, up to Infinite 32.</p><p>It's also how many Majors make one by condensing. Fragments count at the value you pick in Settings → What's a fragment worth?</p>",
    help: "help-motes",
  },
  farmRate: {
    title: "Farm rate", q: "What's the farm rate?",
    get body() { return `<p>What an hour of farming at the cap earns, in T5-eq. Ask weighs levels against it, and Upgrades turns what's left to farm into hours with it.</p><p>It's your best or typical farm at the cap since the ${farmPatch()} patch, from your crawls at ${farmCounted().length ? farmWords(farmCounted()) : "the difficulties you farm for motes (none ticked, in Settings → How you play)"}${farmCounted().length ? `: the ${farmCounted().length === 1 ? "difficulty" : "difficulties"} you farm for motes, in Settings → How you play` : ""}. It takes ${farmNeeds()} of those crawls with a rate (${rateFromKills() ?? "enough"} kills, or a finished crawl). Until then it's the rate you set in Settings, or 50.</p>`; },
    help: "help-farm",
  },
  xp: {
    title: "+% XP to level", q: "What's the XP to level?",
    get body() { return "<p>Your reincarnation penalty: every level needs more XP, so the bar to fill is bigger. Mobs give the same XP; each kill just fills a smaller share of the bar.</p><p>It's 10% for each reincarnation of the most-reincarnated of your trio and race: a class reincarnated twice means +20%. Times Pathfinder compares are re-figured to the same XP to level.</p>" + newLevelsHelp(); },
    help: "help-pace",
  },
  estimate: {
    title: "An estimate", q: "What does ≈ mean?",
    get body() { const n = newLv(); return "<p>An estimate, not a verdict: the hours a level your finished trios took at each level still ahead, for this trio's XP to level, scaled by how this relevel has gone so far. It moves as you play.</p>" + (n ? `<p>Past ${n.from} there's nothing to time yet: nobody has played levels ${n.from + 1} to ${n.to}.</p>` : "") + "<p>Whether a crawl went well is judged against your usual, on the overlay and in History.</p>"; },
  },
  askEstimate: {
    title: "An estimate", q: "What does ≈ mean?",
    body: "<p>An estimate from your past visits and, where you have few, from players who share. Your verdicts (on pace, slow) come from your usual instead, on the overlay and in History.</p>",
  },
  likeforlike: {
    title: "Mostly D4", q: "What does mostly D4 mean?",
    body: "<p>More than half of a relevel's active hours were in crawls at that difficulty. Home compares a relevel only with yours played mostly at the same difficulty as Ask's route: a D4 relevel loots far more and levels slower than a D0 one, so their hours don't compare.</p><p>When no difficulty has more than half, the relevel is mixed, and compares only with other mixed ones. Ask's route counts motes when this relevel, or your last, was mostly at difficulties you farm (<button class=\"link\" data-go=\"settings\" data-anchor=\"howYouPlay\">Settings → How you play</button>), and is the fastest otherwise. Home says which, with a link to ask it the other way.</p>",
    help: "help-pace",
  },
  completionist: {
    title: "Completionist", q: "What's Completionist?",
    body: "<p>A passive of its own, ranks 1 to 3. It goes up when every class reaches that rank: every class reincarnated once for rank 1, twice for 2, three times for 3.</p><p>Only classes count, so the plan brings every class to 1, then 2, then 3, and it comes as soon as it can.</p>",
    help: "help-campaign",
  },
  cheapest: {
    title: "Cheapest path", q: "What's the cheapest path?",
    body: "<p>The fewest runs that reach your goals. A run earns one race rank and three class ranks, so 24 class ranks to earn take at least 8 runs.</p><p>Between plans with as many runs, the least XP to level wins, then the one that finishes your classes first. A carry or heals only break ties.</p><p>Your Must combos and anything set to Unlock now come first, at the price they show. Everything else only where it's free.</p>",
    help: "help-campaign",
  },
  killSpeed: {
    title: "Kill speed", q: "What's kill speed?",
    body: "<p>Kills a minute against your own usual: the same place and difficulty, within 2 levels, solo or grouped the same, from 3 or more runs.</p><p>It's the part you control: gear, your trio, how much you pull. Your XP to level doesn't change it.</p>",
    help: "help-pace",
  },
  xpPerKill: {
    title: "XP per kill", q: "What's XP per kill?",
    body: "<p>The share of a level one kill fills. It's the place's part: what you kill there, and whether a group splits it.</p><p>Every place gives about 8% less a kill each level, so it's compared with what this place gives at your level. After a reincarnation it reads smaller because your bar is bigger, not because mobs give less.</p>",
    help: "help-pace",
  },
  versus: {
    title: "You against players who share", q: "How are you compared with players who share?",
    body: "<p>Levels an hour = kill speed × XP per kill, so a gap splits into the two. Kill speed is how fast you kill. XP per kill is what you kill and who you share it with.</p><p>Only places and levels you've both played count. Their trios and gear differ from yours: it's a comparison, not a verdict.</p>",
    help: "help-pace",
  },
  blend: {
    title: "Your time and theirs", q: "How do your times and the players' combine?",
    body: "<p>Where players who share have played a place too, its time is yours and theirs together. Their number counts as much as one of your visits: with one visit the time is half yours, with four it's 80% yours.</p><p>A place only they've played uses their time.</p>",
  },
  vtp: {
    title: "Void-Touched Potential", q: "Where's a Void-Touched Potential best used?",
    body: "<p>A VTP adds one whole tier, whatever that tier costs. So it saves the most on the costliest: +9 → +10 is 512 points, about 1,640 Majors' worth of Infinite motes.</p><p>Take gear to +9 with motes and fragments, and keep VTPs for +9 → +10.</p>",
    help: "help-fragment",
  },
};
/** The (i) for one INFO entry, to put right after the number or the word it explains. */
const infoTip = (key, extra = "") => INFO[key] ? `<button type="button" class="infotip" data-info="${key}"${extra ? ` data-info-extra="${esc(extra)}"` : ""} aria-label="${esc(INFO[key].q ?? INFO[key].title)}" aria-haspopup="dialog" aria-expanded="false" aria-controls="infoPop">i</button>` : "";
// In index.html's own markup an (i) is a placeholder: <span data-info-slot="vtp"></span>.
$$("[data-info-slot]").forEach(s => { s.outerHTML = infoTip(s.dataset.infoSlot); });

// The popover. Escape, a click outside or scrolling closes it; Escape, ✕ and Tab past its end give focus back to the (i).
// Pages redraw their HTML (every 15 seconds, and on every answer), so the listeners sit on document, and an open popover
// moves to the same (i) in the redrawn page, or closes when that (i) is gone.
let infoAt = null;   // { key, n: which (i) with that key on the page, btn }
const infoPop = document.createElement("div");
infoPop.id = "infoPop"; infoPop.className = "info-pop"; infoPop.hidden = true; infoPop.tabIndex = -1;
infoPop.setAttribute("role", "dialog");
infoPop.setAttribute("aria-labelledby", "infoPopTitle");
document.body.appendChild(infoPop);
const infoButtons = key => $$(`#main .infotip[data-info="${key}"]`).filter(b => b.offsetParent);

function infoOpen(btn) {
  const key = btn.dataset.info, it = INFO[key];
  if (!it) return;
  const again = infoAt?.btn === btn;
  infoClose(again);
  if (again) return;
  const t = it.table;
  infoPop.innerHTML = `<div class="pop-h"><b id="infoPopTitle">${esc(it.title)}</b><button type="button" class="iconbtn" data-info-close aria-label="Close">✕</button></div>
    ${btn.dataset.infoExtra ?? ""}${it.body}${t ? `<table><thead><tr>${t.head.map((h, i) => `<th${i ? "" : ' class="l"'}>${h}</th>`).join("")}</tr></thead><tbody>${t.rows.map(([a, b, on]) =>
      `<tr${on ? ' class="on"' : ""}><td class="l">${a}</td><td>${b}${on ? ` · ${on}` : ""}</td></tr>`).join("")}</tbody></table>` : ""}${it.foot ?? ""}
    ${it.help ? `<button type="button" class="link more" data-go="help" data-anchor="${it.help}">More in Help →</button>` : ""}`;
  infoPop.hidden = false;
  btn.setAttribute("aria-expanded", "true");
  infoAt = { key, n: infoButtons(key).indexOf(btn), btn };
  infoPlace();
  infoPop.focus({ preventScroll: true });
}
function infoClose(refocus) {
  if (!infoAt) return;
  const b = infoAt.btn;
  infoAt = null;
  infoPop.hidden = true;
  infoPop.innerHTML = "";
  b.setAttribute("aria-expanded", "false");
  if (refocus && b.isConnected) b.focus();
}
/** Below the (i), or above it when there's no room, and always inside the window. */
function infoPlace() {
  if (!infoAt) return;
  const a = infoAt.btn.getBoundingClientRect(), w = Math.min(340, innerWidth - 24);
  infoPop.style.width = w + "px";
  infoPop.style.left = Math.max(12, Math.min(a.left - 12, innerWidth - w - 12)) + "px";
  const h = infoPop.offsetHeight, below = a.bottom + 8, above = a.top - h - 8;
  infoPop.style.top = (below + h <= innerHeight - 12 ? below : above >= 12 ? above : Math.max(12, innerHeight - h - 12)) + "px";
}
document.addEventListener("click", e => {
  const b = e.target.closest?.(".infotip");
  if (b) { e.preventDefault(); infoOpen(b); return; }
  if (e.target.closest?.("#infoPop [data-info-close]")) infoClose(true);
  else if (e.target.closest?.("#infoPop [data-go]")) infoClose(false);   // core's [data-go] handler above opens the Help card
});
document.addEventListener("mousedown", e => {
  if (infoAt && !infoPop.contains(e.target) && e.target.closest?.(".infotip") !== infoAt.btn) infoClose(false);
});
document.addEventListener("keydown", e => {
  if (!infoAt) return;
  if (e.key === "Escape") { e.preventDefault(); infoClose(true); return; }
  if (e.key !== "Tab" || !infoPop.contains(document.activeElement)) return;
  const stops = $$("#infoPop button"), i = stops.indexOf(document.activeElement);
  // Back past the start: to the (i). On past the end: the (i) takes focus and Tab carries on from it, in the page.
  if (e.shiftKey && i <= 0) { e.preventDefault(); infoClose(true); }
  else if (!e.shiftKey && i === stops.length - 1) infoClose(true);
});
$("#main").addEventListener("scroll", () => infoClose(false), { passive: true });
addEventListener("resize", () => infoPlace());
new MutationObserver(() => {
  if (!infoAt) return;
  if (!infoAt.btn.isConnected || !infoAt.btn.offsetParent) {
    const next = infoButtons(infoAt.key)[infoAt.n];
    if (!next) return infoClose(false);
    infoAt.btn = next;
    next.setAttribute("aria-expanded", "true");
  }
  infoPlace();
}).observe($("#main"), { childList: true, subtree: true, attributes: true, attributeFilter: ["hidden"] });


// ---------------------------------------------------------------- rate display rules
// The engine works out every rate and sends null when there is none: a finished crawl, or Rates.MinKills kills, makes one.
// The pages never recompute that; they draw "—" for null and read the number of kills from the data.
const rateFromKills = () => data?.snapshot?.session?.rateFromKills;
const rateRule = () => `it starts at ${rateFromKills() ?? "enough"} kills, or when the crawl is finished.`;
/** Why a past visit has no rate (it never will get one): under the kills and not finished. */
const noRateWhy = () => `No rate: under ${rateFromKills() ?? "enough"} kills and not finished.`;
/** A rate or "—" in .dim: null is no rate yet, 0 is a fact. */
const rateCell = (x, d = 1, cls = "") => x == null ? `<span class="dim" title="${esc(noRateWhy())}">—</span>` : cls ? `<span class="${cls}">${fmt(x, d)}</span>` : fmt(x, d);
