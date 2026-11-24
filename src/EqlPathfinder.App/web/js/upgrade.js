"use strict";
// ---------------------------------------------------------------- Upgrades
// The page keeps what you asked for; the app answers with your gear (inventory export), your mote bank as it stands now,
// and the cheapest plan. The bank's inputs aren't redrawn while you're editing them.
let upg = null, upTimer = 0, bankDirty = false, upItemsKey = "";
// gear: every copy of the item you have, {tier, progress, place, on}. The inventory export lists them with their +N and
// where they are (place); a copy you add by hand has no place. The copy with the most points is the one the others merge
// into; a copy that's off is left alone. Spells have no copies: tier and progress are the spell's.
const UP_ROW = { tier: 0, progress: 0, place: "", on: true };
const UP_DEFAULT = { target: "gear", item: "", tier: 0, progress: 0, goal: 10, vtps: 0, gear: [{ ...UP_ROW }] };
let upReq = (() => {
  let r;
  try { r = Object.assign({}, UP_DEFAULT, JSON.parse(store.get("upgradeReq", "{}"))); } catch { r = { ...UP_DEFAULT }; }
  // Migrate legacy single-item + copies[] format into unified gear[] rows.
  if (!Array.isArray(r.gear) || !r.gear.length || r.copies)
    r.gear = [{ ...UP_ROW, tier: r.tier, progress: r.progress }, ...(r.copies ?? []).map(t => ({ ...UP_ROW, tier: t }))];
  if (r.copies) r.item = String(r.item ?? "").replace(/ \+\d+$/, "");   // the list was "Name +N"; now it's one entry per name
  delete r.copies; delete r.merge;
  return r;
})();
const upTier = t => upReq.target === "spell" ? `tier ${t}` : `+${t}`;
const tierPts = t => 2 ** t;
const n0 = v => Math.round(v).toLocaleString();
const hoursText = h => h < 1 ? `${Math.max(1, Math.round(h * 60))} min` : h < 10 ? `${h.toFixed(1)} h` : `${Math.round(h).toLocaleString()} h`;
const atRate = t5 => { const r = data?.reinc?.farmRate; return r > 0 && t5 > 0 ? `≈ ${hoursText(t5 / r)} at ${fmt(r, 0)} T5-eq/h` : ""; };
const cumPts = (t, p) => tierPts(t) - 1 + p;

/** The row the other copies merge into: the one with the most points (the first, on a tie). */
function upMainRow() {
  let best = -1;
  upReq.gear.forEach((r, i) => { if (r.on && (best < 0 || cumPts(r.tier, r.progress) > cumPts(upReq.gear[best].tier, upReq.gear[best].progress))) best = i; });
  return best;
}
/** Which row each of the plan's copies is: the request's copy l is row upSent[l - 1]. */
let upSent = [];

function requestUpgrade(now) {
  clearTimeout(upTimer);
  const spell = upReq.target === "spell", main = upMainRow();
  upSent = spell ? [] : upReq.gear.map((r, i) => i).filter(i => i !== main && upReq.gear[i].on);
  const value = { target: upReq.target, tier: upReq.tier, progress: upReq.progress, goal: upReq.goal, vtps: upReq.vtps,
    copies: upSent.map(i => ({ tier: upReq.gear[i].tier, progress: upReq.gear[i].progress })) };
  upTimer = setTimeout(() => post("upgrade", { value }), now ? 0 : 120);
}

/** Applies the changes and keeps everything in range. rows: false when only a number in the copies list changed (it isn't redrawn under your typing). */
function setUp(changes, rows = true) {
  Object.assign(upReq, changes);
  const clampRow = r => {
    r.tier = Math.max(0, Math.min(10, r.tier | 0));
    r.progress = Math.max(0, Math.min(tierPts(r.tier) - 1, r.progress | 0));
  };
  if (!upReq.gear?.length) upReq.gear = [{ ...UP_ROW }];
  upReq.gear.forEach(clampRow);
  if (!upReq.gear.some(r => r.on)) upReq.gear[0].on = true;
  if (upReq.target === "gear") {
    const m = upReq.gear[upMainRow()];
    upReq.tier = m.tier; upReq.progress = m.progress;
  }
  upReq.tier = Math.max(0, Math.min(10, upReq.tier | 0));
  upReq.goal = Math.max(upReq.tier, Math.min(10, upReq.goal | 0));
  upReq.progress = Math.max(0, Math.min(tierPts(upReq.tier) - 1, upReq.progress | 0));
  upReq.vtps = Math.max(0, Math.min(3, upReq.goal - upReq.tier, upReq.vtps | 0));
  store.set("upgradeReq", JSON.stringify(upReq));
  syncUpForm(rows);
  requestUpgrade();
}

const tierOpts = (from, sel, label = upTier) => Array.from({ length: 11 - from }, (_, i) => from + i)
  .map(t => `<option value="${t}"${t === sel ? " selected" : ""}>${label(t)}</option>`).join("");

function syncUpForm(rows = true) {
  const spell = upReq.target === "spell";
  $$("#upTarget button").forEach(b => b.classList.toggle("on", b.dataset.v === upReq.target));
  $("#upItemRow").hidden = spell;
  $("#upSpellNow").hidden = !spell;
  $("#upCopies").hidden = spell;
  $("#upTierSel").innerHTML = tierOpts(0, upReq.tier);
  $("#upGoalSel").innerHTML = tierOpts(upReq.tier, upReq.goal);
  const maxV = Math.min(3, upReq.goal - upReq.tier);
  $("#upVtpSel").innerHTML = Array.from({ length: maxV + 1 }, (_, i) => `<option value="${i}"${i === upReq.vtps ? " selected" : ""}>${i}</option>`).join("");
  const prog = $("#upProg");
  prog.max = tierPts(upReq.tier) - 1;
  if (document.activeElement !== prog) prog.value = upReq.progress;
  $("#upProgMax").textContent = `/ ${tierPts(upReq.tier).toLocaleString()}`;
  if (document.activeElement !== $("#upItem")) $("#upItem").value = upReq.item;
  if (!spell) rows ? renderUpCopies() : renderUpRoles();
}

/** What each copy does in the plan, in a word. */
function upRole(i) {
  const g = upReq.gear, main = upMainRow(), others = g.filter((r, j) => r.on && j !== main).length;
  if (g.length === 1) return "";
  if (!g[i].on) return `<span class="dim">Not merged</span>`;
  if (i === main) return others ? `<span class="gold">Kept</span> · the others merge into it` : `<span class="gold">Kept</span>`;
  return "Merges in";
}
const upRowName = r => r.place || "Added by you";

/** Every copy of the item: where it is, its +N and points (typed), and whether it's merged in. One copy is just "Now". */
function renderUpCopies() {
  const g = upReq.gear, one = g.length === 1, fromExport = g.some(r => r.place);
  const row = (r, i) => `<div class="cprow${r.on ? "" : " off"}">
      ${one ? `<span class="muted">Now</span>` : `<label class="cpwho"><input type="checkbox" data-row="${i}" data-f="on"${r.on ? " checked" : ""} aria-label="Merge ${esc(upRowName(r))} +${r.tier} in"><span>${esc(upRowName(r))}</span></label>`}
      <select data-row="${i}" data-f="tier" aria-label="Its +N">${tierOpts(0, r.tier, t => `+${t}`)}</select>
      <label class="muted cppts">Points in <input type="number" min="0" max="${tierPts(r.tier) - 1}" step="1" value="${r.progress}" data-row="${i}" data-f="progress" aria-label="Points already in +${r.tier}"><span class="dim num">/ ${tierPts(r.tier).toLocaleString()}</span></label>
      <span class="cprole" data-role="${i}">${upRole(i)}</span>
      ${!one && !r.place ? `<button class="link cpdel" data-del="${i}" aria-label="Remove this copy">Remove</button>` : `<span></span>`}
    </div>`;
  $("#upCopies").innerHTML = `
    ${one ? "" : `<div class="cphead"><span class="sub">Your copies</span><span class="dim">Merging adds all of a copy's points to the one you keep, so they're raised together on cheaper motes.</span></div>`}
    ${g.map(row).join("")}
    <div class="cpfoot"><button class="link" id="upAddCopy">+ Add a copy</button>
      <span class="dim">${fromExport ? "Found in your inventory export. It doesn't show points within a tier: read them off each item." : one ? "Have another one? Merging copies is often the cheapest way up." : ""}</span></div>`;
}
function renderUpRoles() { $$("#upCopies [data-role]").forEach(e => e.innerHTML = upRole(+e.dataset.role)); }

/** One entry per item name, with every copy: its +N and where it is, highest first. */
function upItemList() {
  const by = new Map();
  for (const i of upg?.items ?? []) {
    const e = by.get(i.name) ?? { name: i.name, copies: [] };
    for (let n = 0; n < Math.max(1, i.count); n++) e.copies.push({ tier: i.tier, place: i.place });
    by.set(i.name, e);
  }
  for (const e of by.values()) e.copies.sort((a, b) => b.tier - a.tier);
  return by;
}
const upWhere = e => e.copies.map(c => `+${c.tier} ${c.place}`).join(" · ");

// Custom item suggestion popup (WebView2's native datalist does not filter as you type).
// Open while the input has focus; `at` is the highlighted entry key selected by Enter.
const upPick = { open: false, at: null, keys: [] };
const upWords = q => q.toLowerCase().split(/\s+/).filter(Boolean);

/** Your gear with every typed word somewhere in its name, in any order: names that start with what you typed first, then
 * names with a word that starts with it, then the rest, each in inventory order (what you wear first). */
function upMatches(q, list) {
  const words = upWords(q), keys = [...list.keys()];
  if (!words.length) return keys;
  const whole = words.join(" ");
  const rank = s => s.startsWith(whole) ? 0 : s.split(/[\s'(),:-]+/).some(w => w.startsWith(words[0])) ? 1 : 2;
  return keys.map(k => [k, k.toLowerCase()]).filter(([, s]) => words.every(w => s.includes(w)))
    .map(([k, s]) => [k, rank(s)]).sort((a, b) => a[1] - b[1]).map(([k]) => k);
}

/** The name, escaped, with the typed words marked. */
function upMark(text, q) {
  const s = text.toLowerCase();
  if (s.length !== text.length) return esc(text);
  const on = new Array(text.length).fill(false);
  for (const w of upWords(q))
    for (let i = s.indexOf(w); i >= 0; i = s.indexOf(w, i + 1)) on.fill(true, i, i + w.length);
  let out = "";
  for (let i = 0; i < text.length;) {
    let j = i;
    while (j < text.length && on[j] === on[i]) j++;
    out += on[i] ? `<mark>${esc(text.slice(i, j))}</mark>` : esc(text.slice(i, j));
    i = j;
  }
  return out;
}

function renderUpPick() {
  const box = $("#upItem"), el = $("#upItems"), list = upItemList();
  if (!upPick.open || !list.size) {
    el.hidden = true;
    box.setAttribute("aria-expanded", "false");
    box.removeAttribute("aria-activedescendant");
    return;
  }
  const q = box.value;
  upPick.keys = upMatches(q, list);
  if (!upPick.keys.includes(upPick.at)) upPick.at = upWords(q).length ? upPick.keys[0] ?? null : null;
  el.innerHTML = upPick.keys.length
    ? upPick.keys.map((k, i) => `<div class="opt${k === upPick.at ? " on" : ""}" role="option" id="upOpt${i}" data-i="${i}" aria-selected="${k === upPick.at}">` +
        `<span>${upMark(k, q)}${list.get(k).copies.length > 1 ? ` <span class="chip">${list.get(k).copies.length} copies</span>` : ""}</span><span class="where">${esc(upWhere(list.get(k)))}</span></div>`).join("")
    : `<div class="none">None of your gear matches. Set its +N below.</div>`;
  el.hidden = false;
  box.setAttribute("aria-expanded", "true");
  const i = upPick.keys.indexOf(upPick.at), o = el.children[i];
  if (!o) { box.removeAttribute("aria-activedescendant"); return; }
  box.setAttribute("aria-activedescendant", o.id);
  // Keep the highlighted entry in view inside the list, without scrolling the page.
  if (o.offsetTop < el.scrollTop) el.scrollTop = o.offsetTop;
  else if (o.offsetTop + o.offsetHeight > el.scrollTop + el.clientHeight) el.scrollTop = o.offsetTop + o.offsetHeight - el.clientHeight;
}

function openUpPick() { upPick.open = true; upPick.at = null; renderUpPick(); }
function closeUpPick() { if (!upPick.open) return; upPick.open = false; renderUpPick(); }

/** The export's copies of the item as rows. Points and on/off you set carry over to the same +N in the same place. */
function upRowsFrom(hit, old = []) {
  const left = [...old];
  const rows = hit.copies.map(c => {
    const k = left.findIndex(r => r.place === c.place && r.tier === c.tier);
    const was = k >= 0 ? left.splice(k, 1)[0] : null;
    return { ...UP_ROW, tier: c.tier, place: c.place, progress: was?.progress ?? 0, on: was?.on ?? true };
  });
  return [...rows, ...left.filter(r => !r.place)];
}

/** The item box says v: a piece of your gear lists its copies (and a goal above the best); any other name is just a name. */
function setUpItem(v) {
  const hit = upItemList().get(v);
  if (hit) setUp({ item: v, gear: upRowsFrom(hit), invTime: upg?.inventory?.time, goal: Math.max(upReq.goal, Math.min(10, hit.copies[0].tier + 1)) });
  else {
    const typed = upReq.gear.filter(r => !r.place);
    setUp({ item: v, gear: typed.length ? typed : [{ ...UP_ROW, tier: upReq.tier }] });
  }
}

function pickUpItem(k) {
  $("#upItem").value = k;
  closeUpPick();
  setUpItem(k);
}

$("#upItem").addEventListener("focus", openUpPick);
$("#upItem").addEventListener("mousedown", () => { if (!upPick.open && document.activeElement === $("#upItem")) openUpPick(); });
$("#upItem").addEventListener("blur", closeUpPick);
$("#upItem").addEventListener("keydown", e => {
  if (e.key === "ArrowDown" || e.key === "ArrowUp") {
    e.preventDefault();
    const down = e.key === "ArrowDown";
    if (!upPick.open) { openUpPick(); if (!down) return; }
    const keys = upPick.keys, i = keys.indexOf(upPick.at);
    if (!keys.length) return;
    upPick.at = keys[i < 0 ? (down ? 0 : keys.length - 1) : (i + (down ? 1 : -1) + keys.length) % keys.length];
    renderUpPick();
  } else if (e.key === "Enter" && upPick.open && upPick.keys.includes(upPick.at)) { e.preventDefault(); pickUpItem(upPick.at); }
  else if (e.key === "Escape" && upPick.open) { e.preventDefault(); closeUpPick(); }   // a second Escape clears the box
});
// mousedown, not click: the box keeps focus, so the list doesn't close before the pick lands.
$("#upItems").addEventListener("mousedown", e => {
  e.preventDefault();
  const o = e.target.closest(".opt");
  if (o) pickUpItem(upPick.keys[+o.dataset.i]);
});

function renderUpgrade() {
  if (!upg) return;
  const inv = upg.inventory;
  $("#upInvNote").innerHTML = inv.exists
    ? `Gear from ${esc(inv.file)}, ${dt(inv.time)} · <b>/outputfile inventory</b> in game refreshes it`
    : `No inventory export yet: type <b>/outputfile inventory</b> in game, or set the +N by hand`;
  const list = upItemList();
  const key = `${inv.time}|${list.size}`;
  if (key !== upItemsKey) {   // a new inventory export: redraw the suggestions if they're open, and list the copies again
    upItemsKey = key;
    renderUpPick();
    // Only an export newer than the one the copies came from lists them again, so a +N you corrected by hand stays.
    const hit = list.get(upReq.item);
    if (hit && upReq.target === "gear" && upReq.invTime !== inv.time) {
      const rows = upRowsFrom(hit, upReq.gear);
      if (JSON.stringify(rows) !== JSON.stringify(upReq.gear)) { setUp({ gear: rows, invTime: inv.time }); return; }
      upReq.invTime = inv.time;
    }
  }
  syncUpForm(document.activeElement?.closest?.("#upCopies") == null);
  renderUpPlan();
  if (!bankDirty) renderUpBank();
}

// ---- The plan
// Mote tiers by colour, so a step's chips, the chart's bars and the legend match. Fragments are the same colour, striped.
const tcls = r => `t${Math.max(4, r)}`;
const unitName = (kind, tier, n) => kind === "Fragment" ? `${n === 1 ? "Fragment" : "Fragments"} of ${tier}` : `${tier} mote${n === 1 ? "" : "s"}`;
const unitChip = (kind, tier, n, sub) =>
  `<span class="uchip"><i class="sw ${tcls(TIERS.indexOf(tier))}${kind === "Fragment" ? " frag" : ""}"></i><b class="num">${n.toLocaleString()}</b> ${unitName(kind, tier, n)}${sub ? `<span class="dim"> ${sub}</span>` : ""}</span>`;

function renderUpPlan() {
  const el = $("#upPlan"), p = upg?.plan;
  if (!p) { el.innerHTML = `<div class="empty">Working it out…</div>`; return; }
  const spell = upReq.target === "spell";
  const name = spell ? "the spell" : (upReq.item ? esc(upReq.item) : "the item");
  if (p.goal <= p.fromTier) { el.innerHTML = `<div class="empty">${name} is already at ${upTier(p.fromTier)}. Pick a higher goal.</div>`; return; }

  const bankMoteT5 = p.motesFromBank.reduce((s, n, r) => s + (r ? n * 2 ** (r - 5) : 0), 0);
  const bankFrags = p.fragmentsFromBank.reduce((s, n) => s + n, 0);
  const c = p.costByVtps, v = p.vtpsUsed;
  const vtpSave = v + 1 < c.length ? c[v] - c[v + 1] : null;
  const tile = (val, k, sub) => `<div class="stat"><div class="v num">${val}</div><div class="k">${k}</div>${sub ? `<div class="dim" style="font-size: 11.5px">${sub}</div>` : ""}</div>`;
  const tiles = [
    tile(`${n0(p.costT5)} <span style="font-size: 13px; font-weight: 500" class="muted">T5-eq</span>${infoTip("t5eq")}`, "Cost", "motes at T5-eq, fragments at their best other use"),
    tile(n0(bankMoteT5) + (bankFrags ? ` <span style="font-size: 13px; font-weight: 500" class="gold">+ ${bankFrags} frag</span>` : ""), "From your bank", "T5-eq of motes"),
    tile(`<span class="${p.farmT5 > 0 ? "warn" : "good"}">${n0(p.farmT5)}</span>`, "Still to farm (T5-eq)", atRate(p.farmT5) || (p.farmT5 > 0 ? "" : "your bank covers it")),
  ];
  if (vtpSave != null && vtpSave > 0) tiles.push(tile(n0(vtpSave), v ? "Another VTP saves" : "A VTP here saves", atRate(vtpSave)));
  const merges = p.merges ?? [];
  if (merges.length && p.costOtherWay != null && p.costOtherWay - p.costT5 >= 0.5)
    tiles.push(tile(n0(p.costOtherWay - p.costT5), "Merging saves", atRate(p.costOtherWay - p.costT5) || "against raising one alone"));

  // Lane l is the item (0) or the plan's copy l. Each is named by where it is and the +N it's at now.
  const main = upReq.gear[upMainRow()];
  const laneRow = l => spell ? null : l === 0 ? main : upReq.gear[upSent[l - 1]];
  const laneName = l => {
    const r = laneRow(l);
    if (!r) return "The spell";
    if (!r.place && !(p.merges?.length)) return "Your item";
    const dup = upReq.gear.filter(x => x.on && x.place === r.place && x.tier === r.tier).length > 1;
    return `${esc(upRowName(r))} +${r.tier}${dup && l ? ` <span class="dim">(${l + 1})</span>` : ""}`;
  };
  const at = (t, pr) => `${upTier(t)}${pr ? ` <span class="dim">${pr.toLocaleString()}/${tierPts(t).toLocaleString()}</span>` : ""}`;
  const lanes = [0, ...merges.map(m => m.copy)];
  const laneSteps = l => p.steps.filter(s => (s.copy ?? 0) === l);
  const laneStart = l => l === 0 ? [p.fromTier, p.progress] : (r => [r.tier, r.progress])(laneRow(l));
  const laneEnd = l => {
    const ss = laneSteps(l);
    return ss.length ? [ss.at(-1).endTier, ss.at(-1).endProgress] : laneStart(l);
  };

  // ---- 1. What it takes: every mote and fragment the plan puts in, and where they come from.
  const used = new Map();
  for (const s of p.steps) { const k = `${s.kind}|${s.tier}`; used.set(k, (used.get(k) ?? 0) + s.count); }
  const usedChips = [...used].sort((a, b) => TIERS.indexOf(a[0].split("|")[1]) - TIERS.indexOf(b[0].split("|")[1]))
    .map(([k, n]) => { const [kind, tier] = k.split("|"); return unitChip(kind, tier, n); });
  const motes = (n, r) => `${n.toLocaleString()} ${TIERS[r]}`;
  const gets = [];
  const farm = p.motesToFarm.map((n, r) => [n, r]).filter(([n, r]) => r && n > 0);
  if (farm.length) gets.push(`<div class="getrow"><span class="gk">Farm</span><span><b>${n0(p.farmT5)} Majors</b>${atRate(p.farmT5) ? ` <span class="dim">${atRate(p.farmT5)}</span>` : ""}</span></div>`);
  if (p.condense.length) gets.push(`<div class="getrow"><span class="gk">Condense</span><span>at Constructed Potential, in this order: ${p.condense.map(x => {
    const r = TIERS.indexOf(x.from);
    // e.g. "50 Major motes → 25 Greater", "32 Fragments of Major → 16 of Greater"
    return x.kind === "Fragment"
      ? `<span class="cond"><i class="sw ${tcls(r)} frag"></i> ${x.offered.toLocaleString()} Fragments of ${TIERS[r]} → ${(x.offered / 2).toLocaleString()} of ${TIERS[r + 1]}</span>`
      : `<span class="cond">${motes(x.offered, r)} motes → ${motes(x.offered / 2, r + 1)}</span>`;
  }).join(`<span class="dim"> then </span>`)}</span></div>`);
  if (!farm.length && !p.condense.length && used.size) gets.push(`<div class="getrow"><span class="gk">Bank</span><span class="good">All of it is in your bank already.</span></div>`);

  // ---- 2. The chart: each copy's bar on one +N scale, coloured by the motes that fill it, then the merges and VTPs.
  const lo = Math.min(...lanes.map(l => laneStart(l)[0])), hi = p.goal;
  const span = Math.max(1, hi - lo);
  const pos = (t, pr) => Math.max(0, Math.min(100, ((t + pr / tierPts(t)) - lo) / span * 100));
  const seg = (a, b, cls, title, label) => {
    const w = b - a;
    return w > 0 ? `<i class="seg ${cls}" style="left: ${a}%; width: ${w}%" title="${title}">${label && w > 9 ? `<em>${label}</em>` : ""}</i>` : "";
  };
  const ticks = Array.from({ length: span + 1 }, (_, i) => `<span style="left: ${i / span * 100}%">${upTier(lo + i)}</span>`).join("");
  const showMerged = merges.length > 0;
  const vtpBar = v ? seg(pos(p.goal - v, 0), 100, "vtp", `${plural(v, "Void-Touched Potential")}`, `${v} VTP`) : "";
  const laneBar = l => {
    const [st, sp] = laneStart(l), [et, ep] = laneEnd(l);
    const segs = laneSteps(l).map(s => seg(pos(s.startTier, s.startProgress), pos(s.endTier, s.endProgress),
      `${tcls(TIERS.indexOf(s.tier))}${s.kind === "Fragment" ? " frag" : ""}`, `${s.count} × ${unitName(s.kind, s.tier, s.count)}`, s.count.toLocaleString())).join("");
    return `<div class="lab">${laneName(l)}</div>
      <div class="lane"><i class="have" style="width: ${pos(st, sp)}%"></i>${segs}${!showMerged ? vtpBar : ""}</div>
      <div class="end num">${showMerged || !v ? at(et, ep) : upTier(p.goal)}</div>`;
  };
  let mergedBar = "";
  if (showMerged) {
    const [mt, mp] = laneEnd(0);
    mergedBar = `<div class="lab"><b>Merged</b></div>
      <div class="lane"><i class="have" style="width: ${pos(mt, mp)}%"></i>${merges.map(m =>
        seg(pos(m.startTier, m.startProgress), pos(m.endTier, m.endProgress), "merge", `Merge ${laneName(m.copy).replace(/<[^>]+>/g, "")} in`, "merge")).join("")}${vtpBar}</div>
      <div class="end num"><b>${v ? upTier(p.goal) : at(p.endTier, p.endProgress)}</b></div>`;
  }
  const legendTiers = [...new Set(p.steps.map(s => `${s.kind}|${s.tier}`))].sort((a, b) => TIERS.indexOf(a.split("|")[1]) - TIERS.indexOf(b.split("|")[1]) || a.localeCompare(b) * -1);
  const legend = [...legendTiers.map(k => { const [kind, tier] = k.split("|"); return `<span><i class="sw ${tcls(TIERS.indexOf(tier))}${kind === "Fragment" ? " frag" : ""}"></i>${kind === "Fragment" ? `Fragment of ${tier}` : tier}</span>`; }),
    showMerged ? `<span><i class="sw merge"></i>Merge</span>` : "", v ? `<span><i class="sw vtp"></i>VTP</span>` : "",
    `<span><i class="sw have"></i>Already in</span>`].join("");

  // ---- 3. The steps, in order: one per copy (everything that goes into it), each merge, then the VTPs.
  const steps = [];
  for (const l of lanes) {
    const ss = laneSteps(l);
    if (!ss.length) continue;
    const [st, sp] = laneStart(l), [et, ep] = laneEnd(l);
    steps.push([`Raise ${showMerged ? laneName(l) : name}`, `${at(st, sp)} → ${at(et, ep)}`,
      ss.map(s => unitChip(s.kind, s.tier, s.count, `· ${s.pointsEach} pts each`)).join("")]);
  }
  for (const m of merges)
    steps.push([`Merge ${laneName(m.copy)} into ${laneName(0)}`, `${at(m.startTier, m.startProgress)} → ${at(m.endTier, m.endProgress)}`,
      `<span class="dim">It brings all ${(2 ** m.copyTier - 1 + m.copyProgress + 1).toLocaleString()} of its points.</span>`]);
  // A VTP upgrades one whole tier; +9 → +10 is the costliest (512 points), so apply VTPs to the top tiers first.
  if (v) steps.push([`Use ${plural(v, "Void-Touched Potential")}`, `${upTier(p.goal - v)} → ${upTier(p.goal)}`,
    `<span class="dim">One whole tier each, so they take the top ${v === 1 ? "tier" : v + " tiers"}.</span>${p.goal < 10
      ? ` <span class="warn">A VTP saves the most on +9 → +10 (512 points): worth keeping for an item you'll take to +10.</span>` : ""}`]);

  const left = p.overshoot > 0
    ? (v ? `${plural(p.overshoot, "point")} past ${upTier(p.goal - v)} before the VTP: whether a VTP keeps them isn't known yet.`
         : p.overshoot === 1 ? `1 point left over carries into ${upTier(p.endTier)}.` : `${p.overshoot} points left over carry into ${upTier(p.endTier)}.`) : "";
  el.innerHTML = `
    <div class="between"><span class="h">${name}: ${upTier(p.fromTier)} → ${upTier(p.goal)}</span>
      <span class="dim num">${p.pointsNeeded.toLocaleString()} points${v ? ` + ${plural(v, "VTP")}` : ""}</span></div>
    <div class="tiles" style="margin: 12px 0 6px">${tiles.join("")}</div>
    ${tiles.join("").includes("T5-eq/h") ? `<div class="dim" style="font-size: 12px; line-height: 1.45; margin-bottom: 6px">${farmRateNote("The hours are at")}</div>` : ""}
    ${used.size ? `<div class="upsec"><div class="uph">What it takes</div><div class="uchips">${usedChips.join("")}</div>${gets.join("")}</div>` : ""}
    <div class="upsec"><div class="uph">The path</div>
      <div class="uptrack" style="--tick: ${100 / span}%"><div></div><div class="axis">${ticks}</div><div></div>${lanes.map(laneBar).join("")}${mergedBar}</div>
      <div class="uplegend">${legend}</div></div>
    <div class="upsec"><div class="uph">Step by step</div>
      <ol class="upsteps">${steps.map(([what, move, detail], i) => `<li><span class="badge">${i + 1}</span>
        <div class="what">${what}</div><div class="move num">${move}</div><div class="stepunits">${detail}</div></li>`).join("")}</ol></div>
    ${left ? `<div class="dim" style="font-size: 12px; padding: 6px 0 8px; border-top: 1px solid var(--line2)">${left}</div>` : ""}`;
}

function renderUpBank() {
  const el = $("#upBank"), b = upg?.bank;
  if (!b) return;
  const sum = a => (a ?? []).reduce((s, n) => s + n, 0);
  const t5 = b.motes.reduce((s, n, r) => s + (r ? n * 2 ** (r - 5) : 0), 0);
  const s = b.since, looted = sum(s?.motesLooted) + sum(s?.fragmentsLooted), offered = sum(s?.motesOffered) + sum(s?.fragmentsOffered);
  const rows = [];
  for (let r = 10; r >= 1; r--)
    rows.push(`<tr><td class="l">${r >= 6 ? `<b>${TIERS[r]}</b>` : TIERS[r]}</td>
      <td><input type="number" min="0" step="1" data-bank="motes" data-r="${r}" value="${b.motes[r]}" aria-label="${TIERS[r]} motes"></td>
      <td><input type="number" min="0" step="1" data-bank="fragments" data-r="${r}" value="${b.fragments[r]}" aria-label="Fragments of ${TIERS[r]}"></td></tr>`);
  el.innerHTML = `
    <div class="between"><span class="h">Your motes</span><span class="dim num">${n0(t5)} T5-eq in motes</span></div>
    <div class="sub" style="margin: 4px 0 10px">${b.enteredAt
      ? `Entered ${dt(b.enteredAt)}${looted || offered ? ` · since then ${looted.toLocaleString()} looted${offered ? `, ${offered.toLocaleString()} condensed` : ""}, already added in` : ""}`
      : "Type what your currency window shows, then save."}</div>
    <table class="banktbl"><thead><tr><th class="l">Tier</th><th>Motes</th><th>Fragments</th></tr></thead><tbody>${rows.join("")}</tbody></table>
    <div class="row" style="margin-top: 12px"><label class="muted" style="display: flex; gap: 8px; align-items: center">VTPs
      <input type="number" min="0" max="9" step="1" data-bank="vtps" value="${b.vtps ?? 0}" style="width: 56px" aria-label="Void-Touched Potentials"></label>
      <span class="grow"></span><button class="btn" id="bankUndo"${bankDirty ? "" : " hidden"}>Undo</button><button class="btn primary" id="bankSave">Save counts</button></div>
    <p class="note" style="margin: 10px 0 0">The log shows loot and condensing, so those are added for you. It doesn't show motes going into items or spells:
      after spending some, enter the counts again.</p>`;
}

function saveBank() {
  const read = kind => { const a = new Array(11).fill(0); $$(`#upBank input[data-bank="${kind}"]`).forEach(i => a[+i.dataset.r] = Math.max(0, Math.floor(+i.value || 0))); return a; };
  const vtps = Math.max(0, Math.floor(+($("#upBank input[data-bank=vtps]")?.value) || 0));
  bankDirty = false;
  post("upgradeBank", { value: { motes: read("motes"), fragments: read("fragments"), vtps }, request: upReq });
}

/** A change to copy row i: the rows are copied so a half-typed number never edits the saved request in place. */
function setUpRow(i, change, rows) {
  const gear = upReq.gear.map((r, j) => j === i ? { ...r, ...change } : r);
  setUp({ gear }, rows);
}

$("#page-upgrade").addEventListener("click", e => {
  const seg = e.target.closest("#upTarget button");
  if (seg) { setUp({ target: seg.dataset.v }); return; }
  if (e.target.closest("#upAddCopy")) { setUp({ gear: [...upReq.gear, { ...UP_ROW, tier: upReq.gear.at(-1)?.tier ?? 0 }] }); return; }
  const del = e.target.closest("[data-del]");
  if (del) { setUp({ gear: upReq.gear.filter((r, j) => j !== +del.dataset.del) }); return; }
  if (e.target.closest("#bankSave")) { saveBank(); return; }
  if (e.target.closest("#bankUndo")) { bankDirty = false; renderUpBank(); }
});
$("#page-upgrade").addEventListener("input", e => {
  const t = e.target;
  if (t.id === "upItem") { setUpItem(t.value); openUpPick(); }
  else if (t.id === "upProg") setUp({ progress: +t.value || 0 });
  else if (t.dataset.row != null && t.dataset.f === "progress") setUpRow(+t.dataset.row, { progress: +t.value || 0 }, false);
  else if (t.dataset.bank && !bankDirty) { bankDirty = true; $("#bankUndo").hidden = false; }
});
$("#page-upgrade").addEventListener("change", e => {
  const t = e.target;
  if (t.id === "upTierSel") setUp({ tier: +t.value, progress: 0 });
  else if (t.id === "upGoalSel") setUp({ goal: +t.value });
  else if (t.id === "upVtpSel") setUp({ vtps: +t.value });
  else if (t.dataset.row != null && t.dataset.f === "tier") setUpRow(+t.dataset.row, { tier: +t.value, progress: 0 });
  else if (t.dataset.row != null && t.dataset.f === "on") setUpRow(+t.dataset.row, { on: t.checked });
  else if (t.dataset.row != null && t.dataset.f === "progress") {   // leaving the box: show the number clamped, without redrawing under the next click
    setUpRow(+t.dataset.row, { progress: +t.value || 0 }, false);
    t.value = upReq.gear[+t.dataset.row].progress;
  }
});
$("#page-upgrade").addEventListener("keydown", e => { if (e.key === "Enter" && e.target.dataset?.bank) saveBank(); });
// Typing a count replaces the old one instead of appending to it.
$("#page-upgrade").addEventListener("focusin", e => { if (e.target.dataset?.bank || e.target.dataset?.f === "progress") e.target.select(); });
