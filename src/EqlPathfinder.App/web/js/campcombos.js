"use strict";
// ---------------------------------------------------------------- Your combos 
// Rules written once (classes together/apart, or a trio to play), kept through every rebuild.
// The engine sends every number (a combo's price, each row's state, the runs, the goals a Must goes past); this file draws the words.
// Edits go through campaign.js's sendCamp as a plan edit, so the one saved last wins.
const COMBO_ORDER = ["WAR", "CLR", "PAL", "RNG", "SHD", "DRU", "MNK", "BRD", "ROG", "SHM", "NEC", "WIZ", "MAG", "ENC", "BST", "BER"];
/** The open form: { index (null when adding), picks, together, every, times, unlock, must, price, last, fromPin }; comboN numbers its price questions. */
let comboForm = null, comboN = 0, comboFlash = "", comboFlashAt = 0;

const comboList = p => p.combos ?? [];
const comboKind = c => c.apart ? "Never" : c.must || c.unlockNow ? "Must" : "Prefer";
/** The name a combo goes by in a line: "PAL with MNK", "SHD / ROG / SHM", "Never WAR with BER". */
function comboName(c) {
  const k = c.classes;
  if (c.apart) return `Never ${k.length === 2 ? `${k[0]} with ${k[1]}` : `two of ${k.join(", ")}`}`;
  return k.length === 2 ? `${k[0]} with ${k[1]}` : COMBO_ORDER.filter(x => k.includes(x)).join(" / ");
}
const comboTimes = n => n === 1 ? "once" : `${n} times`;
const comboRunNos = ns => campNums(ns.map(campRunNo));
const comboRunsWord = ns => `${ns.length === 1 ? "run" : "runs"} ${comboRunNos(ns)}`;
const comboAnd = a => a.length < 2 ? a.join("") : `${a.slice(0, -1).join(", ")} and ${a.at(-1)}`;
const comboAsMust = a => a.runs > 0 ? `+${plural(a.runs, "run")}` : "more XP to level";

/** The little role line under a class in the picker: carry, tank, heals, mez, slow (Class roles), at most two. */
function comboRoles(abbrev) {
  const np = campView().newPlanner, t = (np?.table ?? []).find(x => x.abbrev === abbrev);
  if (!np) return "";
  const has = c => !!c && !c.startsWith("~"), rank = c => has(c) ? Number(c.split("|")[0]) : 99;
  const out = [];
  if ((np.carries ?? []).includes(abbrev)) out.push("carry");
  if (t) { if (has(t.tank)) out.push("tank"); if (has(t.heals)) out.push("heals"); if (rank(t.crowd) <= 2) out.push("mez"); if (has(t.slow)) out.push("slow"); }
  return out.slice(0, 2).join(" · ");
}

// ---- the words for a price (the engine's CampaignComboPrice). A line appears only for a cost, a broken promise or a choice (docs/design.md).
const comboFree = pr => !!pr.free;
const comboLater = pr => (pr.lands ?? []).filter(l => l.with == null ? l.without != null : l.without != null && l.with > l.without);
/** "+1 run: 17 runs, not 16. Runs 2 and 3 need +20% XP to level, not +10%." Only what costs. */
function comboCostWords(pr) {
  const parts = [];
  if (pr.delta.runs > 0) parts.push(`+${plural(pr.delta.runs, "run")}: ${plural(pr.runsWith, "run")}, not ${pr.runsWithout}`);
  else if (pr.delta.runs < 0) parts.push(`${plural(-pr.delta.runs, "run")} fewer`);
  const xs = pr.xpRuns ?? [];
  if (pr.delta.xp > 0 && xs.length) {
    const some = xs.slice(0, 3).map(x => `run ${campRunNo(x.n)} needs ${xpPct(x.with)} XP to level, not ${xpPct(x.without)}`).join("; ");
    parts.push((pr.delta.runs > 0 ? "" : "Same runs, more XP to level. ") + some.replace(/^./, m => m.toUpperCase()) + (xs.length > 3 ? `. XP to level changes in ${xs.length - 3} more runs` : ""));
  }
  const land = comboLandWords(pr);
  if (land.length) parts.push(land.join(". "));
  return parts.join(". ") + (parts.length ? "." : "");
}
/** "This combo puts Completionist 2 at run 14, not 11": only what comes later. */
function comboLandWords(pr) {
  return comboLater(pr).map(l => {
    const n = l.what.split(":")[1], cls = l.what === "classes", w = campRunNo(l.without);
    if (l.with == null) return cls ? `This combo leaves your classes unfinished, where they were done at run ${w}` : `This combo leaves Completionist ${n} out, where it came at run ${w}`;
    return `This combo ${cls ? "finishes your classes" : `puts Completionist ${n}`} at run ${campRunNo(l.with)}, not ${w}`;
  });
}
const comboSamePrice = (a, b) => a.delta.runs === b.delta.runs && a.delta.xp === b.delta.xp && JSON.stringify(comboLater(a)) === JSON.stringify(comboLater(b));
/** The past-3/3 clause, for a pair. */
const comboPastWords = (pr, c) => pr.pastOf > 0 && c.classes.length === 2 ? ` Past 3/3 they sit together only where that's free: ${pr.pastKept} of the ${pr.pastOf} runs after.` : "";
/** Where a trio lands, then what the other placement costs, when this one costs or is Unlock now and the other is not free. */
function comboTrioWords(c, pr) {
  const o = pr.other;
  if (comboFree(pr) && (!c.unlockNow || !o || comboFree(o))) return "";
  const where = pr.runs.length === 1 ? `Its last run is ${comboRunsWord(pr.runs)}.`
    : c.unlockNow ? (pr.after != null ? `Unlock now: ${comboRunsWord(pr.runs)}, after ${comboName(campPlan().combos[pr.after] ?? { classes: [] })}.` : `Unlock now: ${comboRunsWord(pr.runs)}, back to back.`)
    : `It lands in ${comboRunsWord(pr.runs)}.`;
  const cost = comboFree(pr) ? "" : comboCostWords(pr);
  const name = c.unlockNow ? "Spread out" : "Unlock now";
  const other = !o || pr.runs.length === 1 ? "" : comboSamePrice(pr, o) ? ` <b>${name}</b> costs the same.`
    : comboFree(o) ? ` <b>${name}</b> costs nothing: ${comboRunsWord(o.runs)}.` : ` <b>${name}</b> would cost ${comboCostWords(o).replace(/\.$/, "")}.`;
  return `${cost}${cost ? " " : ""}${where}${other}`;
}
const orunRange = ns => ns.length ? campNums(ns.map(campRunNo)) : "";

/** What the runs a Prefer isn't kept in would cost: "Not in run 3: there it needs +20% XP to level, not +10%." */
function comboNotKept(pr, kept) {
  const xs = (pr.asMustXp ?? []).filter(x => !kept.includes(x.n) && x.with > x.without);   // only where it costs more
  if (xs.length && !(pr.asMust?.runs > 0)) return xs.slice(0, 3).map(x => `Not in run ${campRunNo(x.n)}: there it needs ${xpPct(x.with)} XP to level, not ${xpPct(x.without)}.`).join(" ");
  return `The rest would cost ${pr.asMust ? comboAsMust(pr.asMust) : "more"}, so ${pr.of - pr.kept === 1 ? "it isn't" : "they aren't"} kept.`;
}
/** A row's price line: [class, html]; an empty html says nothing (a free combo has nothing to say). */
function comboLine(c, row, plan) {
  const pr = row.price;
  if (row.state === "hand" || row.state === "pin") {
    const why = row.state === "pin" ? `loadout ${(row.loadout ?? 0) + 1} is pinned` : "you changed it by hand";
    if (!c.apart && !c.must && !c.unlockNow) return ["", `Not kept in run ${campRunNo(row.run)}: ${why}.`];
    const fix = row.state === "pin" ? `<button class="link" data-combo-act="unpin" data-ci="${row.index}" data-cl="${row.loadout}">Unpin it</button>` : `<button class="link" data-camp="rebuild">Rebuild the rest to keep it</button>`;
    return ["warn", `<span class="warn">Not held in run ${campRunNo(row.run)}: ${why}.</span> ${fix}${c.apart || c.unlockNow ? "" : ` <button class="link" data-combo-act="prefer" data-ci="${row.index}">Make it a Prefer</button>`}`];
  }
  let words = "";
  const kept = pr.keptRuns ?? pr.runs;
  if (row.state === "notKept") words = `Not kept: it would cost ${pr.asMust ? comboAsMust(pr.asMust) : "more"}. <button class="link" data-combo-act="must" data-ci="${row.index}">Make it a Must</button>`;
  else if (row.state === "partial") words = `Kept in ${pr.kept} of ${pr.of} runs${kept.length ? ` (${comboRunNos(kept)})` : ""}. ${comboNotKept(pr, kept)}`;
  else if (pr.allPast) words = `${c.classes.join(", ")} ${c.classes.length === 1 ? "is" : "are"} 3/3 already, so it's kept only where it's free: ${pr.pastKept} of the ${pr.pastOf} runs left.`;
  else if (c.apart || c.classes.length === 2) words = comboFree(pr) ? "" : comboCostWords(pr) + comboPastWords(pr, c);
  else words = comboTrioWords(c, pr);
  if (row.playedBroke?.length) {
    const runs = campView().runs, byN = n => runs.find(r => r.n === n);
    const what = row.playedBroke.map(n => {
      const r = byN(n), has = r ? c.classes.filter(k => r.classes.includes(k)) : [], lack = c.classes.filter(k => !has.includes(k));
      return `Run ${campRunNo(n)}${r?.state === "leveling" ? ", leveling," : ", played,"} has ${has.join(" and ")} without ${lack.join(" and ")}.`;
    });
    const togo = row.state === "held" ? " The runs to go keep them together." : row.state === "notKept" ? " The runs to go don't keep them together." : "";   // the engine's answer; a partial row already says how many runs
    words = `${words ? words + " " : ""}${what.join(" ")}${togo}`;
  }
  return ["", words];
}

// ---- the card
function campCombosCard(v, p, busy) {
  const list = comboList(p), saved = comboList(v.plan ?? {}), rows = v.comboRows ?? [];
  const same = i => busy && JSON.stringify(list[i]) !== JSON.stringify(saved[i]);
  const changed = JSON.stringify(list) !== JSON.stringify(saved);   // "updating…" only when a combo of this card is what is being rebuilt
  const full = list.length >= 32;
  const html = list.map((c, i) => {
    const row = rows[i], kind = comboKind(c);
    const what = c.apart ? esc(comboName(c).replace(/^Never /, "").replace(/^two of (.*)$/, "No two of $1"))
      : c.classes.length === 2 ? `${esc(comboName(c))} <span class="chip">${c.everyRun ? "every run" : "at least once"}</span>`
      : `Play ${esc(comboName(c))} <span class="chip">${comboTimes(c.times)}</span> <span class="seggroup sm" role="group" aria-label="Spread out or unlock now">
          <button data-combo-act="spread" data-ci="${i}" class="${c.unlockNow ? "" : "on"}" aria-pressed="${!c.unlockNow}">Spread out</button><button data-combo-act="unlock" data-ci="${i}" class="${c.unlockNow ? "on" : ""}" aria-pressed="${!!c.unlockNow}">Unlock now</button></span>`;
    const line = !row || same(i) ? ["dim", "Working it out…"] : comboLine(c, row, p);
    return `<div class="combo-row${busy ? " stale" : ""}"><span class="kind${kind === "Prefer" ? "" : " must"}">${kind}</span>
      <div class="combo-what">${what}</div>
      <button class="iconbtn" data-ci="${i}" data-combo-act="menu" aria-haspopup="menu" aria-label="More for ${esc(comboName(c).replace(/^Never /, ""))}">⋯</button>
      ${line[1] ? `<div class="price ${line[0]}">${line[1]}</div>` : ""}</div>`;
  }).join("");
  const ex = list.length ? null : comboExample(v, p);
  const none = ex ? (comboForm ? "" : comboTryHtml(ex, busy))
    : `<div class="muted combo-none">Nothing here yet. A combo can be a trio you want to play, say 3 times, or two classes you never want in one run.</div>`;
  const foot = comboStillFlash() ? `<div class="banner flash" role="status" style="margin: 10px 0 0"><div>${esc(comboFlash)}</div></div>` : "";
  const slow = campView().newPlanner?.oneStrongSlow ? `<div class="dim combo-foot">Also kept where free: one strong slow per trio, from <button class="link" data-go="classroles">Class roles</button>.</div>` : "";
  const hint = full ? "You have 32 combos, the most there can be. Remove one to add another." : list.length ? "Or a loadout's ⋯ → Make this a combo." : "";
  return `<section class="card combos" style="padding: 14px 18px 14px; margin-top: 14px" aria-label="Your combos">
    <div class="between"><div class="h2">Your combos${busy && changed ? ` <span class="chip" style="margin-left: 6px">updating…</span>` : ""}</div><span class="dim" style="font-size: 12.5px">Kept through every rebuild and goal change</span></div>
    <div class="dim" style="font-size: 12.5px; margin-top: 2px; line-height: 1.45">Classes you want together or apart, or a trio to play. A Must is held above the cheapest path, at the price shown. A Prefer is kept wherever it costs nothing.</div>
    ${list.length ? `<div class="combo-rows">${html}</div>` : none}${foot}${slow}
    ${comboForm ? comboFormHtml(v, p) : `<div class="row" style="margin-top: 12px; gap: 12px; flex-wrap: wrap"><button class="btn" data-combo-act="add"${full ? " disabled" : ""}>Add a combo</button>
      ${hint ? `<span class="dim" style="font-size: 12px">${hint}</span>` : ""}</div>`}
  </section>`;
}

/**
 * Example combo suggested on an empty card (never stored until Add combo): the trio
 * played most (twice or more), then the one played last, as History → Relevels shows them (before the plan, off it and
 * in it; a run still leveling isn't played yet); else the first two classes, in the card's order, of the plan's first
 * run that isn't played, only when the page lists the plan's runs (it has loadouts). Both are Prefers that change
 * nothing in the plan. Null when there's neither.
 */
function comboExample(v, p) {
  const trios = [...(v.played ?? []).map(r => r.classes), ...(v.runs ?? []).filter(r => r.state === "done").map(r => r.classes)]
    .map(k => COMBO_ORDER.filter(x => (k ?? []).includes(x))).filter(k => k.length === 3);
  const seen = new Map();
  trios.forEach((k, i) => { const key = k.join("/"), s = seen.get(key) ?? { classes: k, count: 0, last: 0 }; s.count++; s.last = i; seen.set(key, s); });
  const best = [...seen.values()].filter(s => s.count >= 2).sort((a, b) => b.count - a.count || b.last - a.last)[0];
  if (best) return { classes: best.classes, count: best.count };
  const run = p.loadouts?.length ? (v.runs ?? []).find(r => r.state !== "done") : null;   // the no-plan page lists no runs to point at
  const pair = run ? COMBO_ORDER.filter(x => (run.classes ?? []).includes(x)).slice(0, 2) : [];
  return pair.length === 2 ? { classes: pair, n: run.n } : null;
}
function comboTryHtml(ex, busy) {
  const trio = ex.classes.length === 3;
  const what = trio ? `Play ${ex.classes.join(" / ")} <span class="chip">${comboTimes(3)}</span>` : `${ex.classes.join(" with ")} <span class="chip">at least once</span>`;   // classes are COMBO_ORDER's own
  const why = trio ? `You've played this trio ${ex.count === 2 ? "twice" : `${ex.count} times`} (History → Relevels).` : `Together in run ${campRunNo(ex.n)} already.`;
  return `<div class="combo-try${busy ? " stale" : ""}" role="group" aria-label="Try one"><div class="combo-try-h">Try one</div>
    <div class="combo-row"><span class="kind">Prefer</span><div class="combo-what">${what}</div>
    <button class="btn sm" data-combo-act="try">Add this</button><div class="price">${why}</div></div></div>`;
}
const comboStillFlash = () => comboFlash && Date.now() - comboFlashAt < 6000;

// ---- the form
function comboOfForm(f) {
  const apart = !f.together, three = f.picks.length === 3;
  return { classes: [...f.picks], apart, everyRun: f.every, times: three ? f.times : 3, must: apart ? false : f.unlock || f.must, unlockNow: !apart && three && f.unlock };
}
function comboAsk() {
  const f = comboForm;
  if (!f || f.picks.length < 2) return;
  post("comboPrice", { value: campPlan(), combo: comboOfForm(f), index: f.index, n: ++comboN });
}
onHost(e => {
  if (e.data?.kind !== "comboPrice" || e.data.n !== comboN || !comboForm) return;
  comboForm.price = e.data.price ?? null;
  if (e.data.price) comboForm.last = e.data.price;
  if (page === "campaign") renderCampaign();
});

function comboFormHtml(v, p) {
  const f = comboForm, r = data.reinc, n = f.picks.length, three = n === 3, apart = !f.together;
  const order = COMBO_ORDER.filter(k => r.classes.some(c => c.key === k)), pr = f.price, shown = pr ?? f.last;
  const picks = order.map(k => `<button class="combo-pick" data-cp="${k}" aria-pressed="${f.picks.includes(k)}"${three && !f.picks.includes(k) ? " disabled" : ""}><b>${k}</b><span>${esc(comboRoles(k))}</span></button>`).join("");
  const seg = (key, opts, label, disabled) => `<span class="seggroup sm" role="group" aria-label="${label}">${opts.map(([val, text, on, dis, title]) =>
    `<button data-cf="${key}:${val}" class="${on ? "on" : ""}" aria-pressed="${!!on}"${dis ? ` disabled title="${esc(title ?? "")}"` : ""}>${text}</button>`).join("")}</span>`;
  const allPast = !!shown?.allPast;
  let controls = "";
  if (n >= 2) {
    const name = (n === 2 ? f.picks : COMBO_ORDER.filter(x => f.picks.includes(x))).join(n === 2 ? " and " : " / ");
    controls = `<div class="row combo-ctl"><b>${esc(name)}:</b>${seg("together", [["1", "Together", f.together], ["0", "Apart", !f.together]], "Together or apart")}
      ${!apart && !three ? seg("every", [["1", "Every run", f.every], ["0", "At least once", !f.every]], "How often") : ""}
      ${!apart && three ? `<span class="dim">Play this trio</span>${seg("times", [1, 2, 3].map(t => [String(t), String(t), f.times === t]), "Times")}<span class="dim">${f.times === 1 ? "time" : "times"}</span>${seg("unlock", [["0", "Spread out", !f.unlock], ["1", "Unlock now", f.unlock]], "Spread out or unlock now")}` : ""}
      ${apart ? `<span class="dim">Held like a Must.</span>` : seg("must", [["1", "Must", f.must || f.unlock, allPast, "Past 3/3 a combo is kept only where it's free"], ["0", "Prefer", !(f.must || f.unlock), f.unlock, "Unlock now is held like a Must"]], "Must or prefer")}</div>`;
  }
  let box = "";
  const stale = pr === undefined || (f.price === undefined);
  if (n >= 2) {
    const c = comboOfForm(f);
    if (!shown) box = `<div class="cheap">${pr === null && f.price === null ? "No answer for this combo." : "Working it out…"}</div>`;
    else if (shown.same != null) box = `<div class="cheap${stale ? " stale" : ""}">You have this combo already.</div>`;
    else if (shown.full) box = `<div class="cheap">You have 32 combos, the most there can be. Remove one to add another.</div>`;
    else if (shown.clash) box = comboClashHtml(shown.clash, p, c);
    else box = `<div class="cheap${stale && f.price === undefined ? " stale" : ""}">${f.price === undefined ? "Working it out… " : ""}${comboWhatItDoes(shown, c, p)}</div>`;
  }
  const blocked = n < 2 || !f.price || f.price.same != null || f.price.full || !!f.price.clash;
  const pin = f.fromPin != null && p.loadouts[f.fromPin]?.pinned ? `<div class="dim" style="font-size: 12px; margin-top: 6px">Loadout ${f.fromPin + 1}'s pin goes: the combo keeps the trio, and Pathfinder picks its runs and race.</div>` : "";
  return `<div class="combo-add" role="group" aria-label="${f.index == null ? "Add a combo" : "Change this combo"}">
    <div class="between"><div style="font-weight: 650">${f.index == null ? "Add a combo" : "Change this combo"}</div><span class="dim" style="font-size: 12px">Pick 2 or 3 classes</span></div>
    <div class="combo-picks">${picks}</div>${controls}${box}${pin}
    <div class="row" style="margin-top: 12px; justify-content: flex-end; gap: 8px"><button class="btn" data-combo-act="cancel">Cancel</button>
      <button class="btn primary" data-combo-act="save"${blocked ? ` disabled${f.price?.clash ? ' title="Settle the clash first"' : ""}` : ""}>${f.index == null ? "Add combo" : "Save combo"}</button></div></div>`;
}
/** "What it does:", from the engine's answer for the form: in runs and time to relevel, never run-by-run XP shifts. */
function comboWhatItDoes(pr, c, p) {
  const past = (pr.pastGoals ?? []).length ? ` It takes ${comboAnd(pr.pastGoals)} to 3/3, past ${pr.pastGoals.length === 1 ? "your goal" : "your goals"} of ${comboAnd(pr.pastGoals.map(k => rank3(p.classes[k] ?? 0)))}.` : "";
  const held = c.must || c.unlockNow || c.apart;
  let body;
  if (pr.allPast) body = `${c.classes.join(", ")} ${c.classes.length === 1 ? "is" : "are"} 3/3 already, so it's kept only where it's free.`;
  else if (!held && pr.asMust && pr.kept < pr.of)
    body = `as a Prefer it's kept in ${pr.kept} of ${pr.of} runs, at no cost. As a Must it's kept in all ${pr.of}, for ${comboAsMust(pr.asMust)}.`;
  else if (comboFree(pr)) body = `Still ${plural(pr.runsWith, "run")} to go, and no more XP to level.`;
  else body = comboCostWords(pr);
  return `<b>What it does:</b> ${body}${past}`;
}
function comboClashHtml(cl, p, c) {
  let lead, why, drop, dropAct;
  if (cl.combo != null) {
    const o = comboList(p)[cl.combo];
    if (!o) return "";
    lead = `Clashes with your combo "${esc(comboName(o))}" (${comboKind(o)})`;
    why = (c.apart || o.apart) ? `a combo can't have ${c.classes.filter(k => o.classes.includes(k)).join(" and ") || "these classes"} both together and apart.`
      : cl.class ? `${cl.class} has ${plural(cl.runs, "run")} left to earn its ranks: this trio needs ${comboAnd(c.classes.filter(k => k !== cl.class))} beside it there, and "${esc(comboName(o))}" needs ${comboAnd(o.classes.filter(k => k !== cl.class))}.`
      : `${c.classes.filter(k => o.classes.includes(k)).join(", ")} has too few runs left to earn its ranks for both, so one of them would never sit together.`;
    drop = `Add it and drop "${esc(comboName(o))}"`; dropAct = "clash-drop";
  } else {
    const l = p.loadouts[cl.loadout];
    if (!l) return "";
    lead = `Clashes with your pinned loadout ${cl.loadout + 1} (${esc(l.race)} · ${esc(trioOf(l))})`;
    why = `it seats ${c.classes.filter(k => l.classes.includes(k)).join(", ")} in a run this combo needs.`;
    drop = `Add it and unpin loadout ${cl.loadout + 1}`; dropAct = "clash-unpin";
  }
  return `<div class="cheap"><span class="warn"><b>${lead}:</b></span> ${why}
    <div class="row" style="margin-top: 8px; gap: 8px"><button class="btn sm" data-combo-act="${dropAct}">${drop}</button><button class="btn sm" data-combo-act="clash-prefer">Add it as Prefer</button></div></div>`;
}

function comboOpen(f, focus) {
  comboForm = { index: null, picks: [], together: true, every: true, times: 3, unlock: false, must: false, price: undefined, last: null, fromPin: null, ...f };
  comboFlash = "";
  renderCampaign();
  comboAsk();
  $(focus ?? "#campBody .combo-pick:not([disabled])")?.focus();
}
function comboClose(back) {
  const from = comboForm?.fromTry ? "try" : "add";
  comboForm = null; comboN++;
  renderCampaign();
  if (back) $(`[data-combo-act="${from}"]`)?.focus();
}
/** Saves the form as a plan edit: asMust false makes it a Prefer, drop removes a clashing combo first, unpin frees a pinned loadout. */
function comboSave(p, opt = {}) {
  const f = comboForm, c = comboOfForm(f);
  if (opt.prefer && !c.apart && !c.unlockNow) c.must = false;
  p.combos = comboList(p).map(x => x);
  const price = f.price, drop = opt.drop && price?.clash?.combo != null ? price.clash.combo : null;
  let idx = f.index;
  if (idx == null) { p.combos.push(c); idx = p.combos.length - 1; } else p.combos[idx] = c;
  if (drop != null) { p.combos.splice(drop, 1); if (idx > drop) idx--; }
  const unpin = opt.unpin && price?.clash?.loadout != null ? price.clash.loadout : f.fromPin;
  if (unpin != null && p.loadouts[unpin]) p.loadouts[unpin].pinned = false;
  comboForm = null; comboN++;
  sendCamp(p, true, `[data-ci="${idx}"][data-combo-act="menu"]`);
}

/** Every click on the card, its ⋯ menu, the form and the goal tile's line. True when it was one of ours. */
function campComboClick(t, p) {
  const d = t.dataset;
  if (d.cp) {
    const f = comboForm, k = d.cp;
    f.picks = f.picks.includes(k) ? f.picks.filter(x => x !== k) : f.picks.length < 3 ? [...f.picks, k] : f.picks;
    f.price = undefined; renderCampaign(); comboAsk(); return true;
  }
  if (d.cf && comboForm) {
    const [key, val] = d.cf.split(":"), f = comboForm;
    if (key === "together") f.together = val === "1";
    if (key === "every") f.every = val === "1";
    if (key === "times") f.times = Number(val);
    if (key === "unlock") { f.unlock = val === "1"; if (f.unlock) f.must = true; }
    if (key === "must") f.must = val === "1";
    f.price = undefined; renderCampaign(); comboAsk(); return true;
  }
  if (d.camp === "pg-remove" || d.camp === "pg-make") {
    const pg = campView().pastGoals?.[Number(d.pg)];
    if (!pg) return true;
    for (const k of pg.classes) { if (d.camp === "pg-remove") { delete p.classes[k]; p.first = p.first.filter(x => x !== "class:" + k); } else p.classes[k] = PASSIVE_MAX; }
    sendCamp(p, false); return true;
  }
  const act = d.comboAct;
  if (!act) return false;
  const i = Number(d.ci), c = comboList(p)[i];
  comboFlash = "";
  if (act === "add") { comboOpen({}); return true; }
  if (act === "try") {
    const ex = comboExample(campView(), p);
    if (ex) comboOpen({ picks: [...ex.classes], every: false, times: 3, fromTry: true }, '[data-combo-act="save"]');
    return true;
  }
  if (act === "cancel") { comboClose(true); return true; }
  if (act === "save") { comboSave(p); return true; }
  if (act === "clash-drop") { comboSave(p, { drop: true }); return true; }
  if (act === "clash-unpin") { comboSave(p, { unpin: true }); return true; }
  if (act === "clash-prefer") { comboSave(p, { prefer: true }); return true; }
  if (act === "menu") { campOpenPop({ kind: "combo", i }, t); return true; }
  if (act === "mkcombo") {
    const l = p.loadouts[Number(d.cl)];
    campClosePop();
    if (l) comboOpen({ picks: [...l.classes], times: l.times, must: true, fromPin: l.pinned ? Number(d.cl) : null }, '[data-combo-act="save"]');
    $("#campBody .combos")?.scrollIntoView({ block: "center" });
    return true;
  }
  if (!c) return true;
  if (act === "change" || act === "must") {
    campClosePop();
    comboOpen({ index: i, picks: [...c.classes], together: !c.apart, every: c.everyRun, times: c.times, unlock: !!c.unlockNow, must: act === "must" ? true : !!c.must }, '[data-combo-act="save"]');
    return true;
  }
  if (act === "prefer") { c.must = false; c.unlockNow = false; campClosePop(); sendCamp(p, true, `[data-ci="${i}"][data-combo-act="menu"]`); return true; }
  if (act === "spread" || act === "unlock") { c.unlockNow = act === "unlock"; if (c.unlockNow) c.must = true; sendCamp(p, true, `[data-ci="${i}"][data-combo-act="${act}"]`); return true; }
  if (act === "unpin") { const l = p.loadouts[Number(d.cl)]; if (l) l.pinned = false; sendCamp(p, true, null); return true; }
  if (act === "remove") {
    const name = comboName(c).replace(/^Never /, "");
    p.combos.splice(i, 1);
    if (!p.combos.length) p.combos = null;
    campClosePop();
    comboFlash = `Removed "${name}". Runs you've played or pinned stay as they are.`; comboFlashAt = Date.now();
    sendCamp(p, true, '[data-combo-act="add"]');
    // The notice goes by itself, where it is: the page drawn anew would scroll back to the button with the focus (#24).
    // A later remove's notice is left for its own timer.
    setTimeout(() => { if (!comboStillFlash()) $("#campBody .combos .flash")?.remove(); }, 6100);
    return true;
  }
  return false;
}

/** A combo's ⋯ menu. */
function comboMenuHtml(i, p) {
  const c = comboList(p)[i];
  if (!c) return "";
  const held = comboKind(c) === "Must";
  return `<div class="cmenu" role="menu" aria-label="${esc(comboName(c))}">
    <button role="menuitem" data-ci="${i}" data-combo-act="change">Change…</button>
    ${c.apart ? "" : held ? `<button role="menuitem" data-ci="${i}" data-combo-act="prefer"${c.unlockNow ? ` disabled>Make it a Prefer<span>Unlock now is held like a Must</span>` : ">Make it a Prefer"}</button>`
      : `<button role="menuitem" data-ci="${i}" data-combo-act="must">Make it a Must</button>`}
    <button role="menuitem" class="danger" data-ci="${i}" data-combo-act="remove">Remove combo<span>Pathfinder plans the cheapest way without it</span></button>
  </div>`;
}
/** A loadout's "Make this a combo" item. */
function comboMakeItem(i, l, p) {
  const has = comboList(p).some(c => !c.apart && c.classes.length === 3 && l.classes.every(k => c.classes.includes(k)));
  return `<button role="menuitem" data-cl="${i}" data-combo-act="mkcombo"${has ? ` disabled>Make this a combo<span>Already your combo</span>` : `>Make this a combo<span>Keeps ${esc(trioOf(l))} through every rebuild</span>`}</button>`;
}

// ---- the goal tile's line: a combo that takes classes past their goals
function comboPastLines(v, p) {
  if (!v.pastGoals?.length) return "";
  const ends = campEnds(v);
  return v.pastGoals.map((g, j) => {
    const c = comboList(campPlan())[g.combo];
    if (!c || !g.classes.length) return "";
    const many = g.classes.length > 1, list = comboAnd(g.classes), goals = comboAnd(g.classes.map(k => rank3(p.classes[k] ?? 0)));
    const end = rank3(ends.c[g.classes[0]] ?? PASSIVE_MAX);
    return `<div class="over">${esc(list)}: your combo ${esc(comboName(c))} takes ${many ? "them" : "it"} to ${end}, past your goal${many ? "s" : ""} of ${goals}, so ${many ? "these goals have" : "this goal has"} no effect.
      <div><button class="link" data-camp="pg-remove" data-pg="${j}">Remove the goal${many ? "s" : ""}</button> <button class="link" data-camp="pg-make" data-pg="${j}">Make ${many ? "them" : "it"} 3/3</button></div></div>`;
  }).join("");
}

/** The combo that takes a class past its goal, for the cell's tooltip. */
function comboPastTip(v, p, key) {
  const g = v.pastGoals?.find(x => x.classes.includes(key)), c = g && comboList(campPlan())[g.combo];
  return c ? `; your combo ${comboName(c)} takes it to ${rank3(campEnds(v).c[key] ?? PASSIVE_MAX)}` : "";
}

// Capture phase: with a ⋯ menu open, Escape closes the menu only (campaign.js closes it on the way back up).
document.addEventListener("keydown", e => {
  if (e.key === "Escape" && comboForm && !campPop && !$("#campPop .cmenu") && page === "campaign") comboClose(true);
}, true);
