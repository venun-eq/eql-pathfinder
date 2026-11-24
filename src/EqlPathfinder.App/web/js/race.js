"use strict";
// ---------------------------------------------------------------- relevels, shared by Home and History → Relevels
// A relevel's route as stops on a bar, its splits, and the ghost race: your best, the middle half of your relevels and
// the typical one, which reads the same at 5 relevels or 40.

const PLACE_SHORT = { "Estate of Unrest": "Unrest", "Castle of Mistmoore": "Mistmoore", "Kedge Keep": "Kedge", "Plane of Hate": "Hate",
  "Plane of Fear": "Fear", "Plane of Sky": "Sky", "Ruins of Old Guk": "Old Guk", "Nagafen's Lair": "Nagafen", "Lair of the Splitpaw": "Splitpaw",
  "Nektropos Castle": "Nektropos", "Ruins of Old Paineel": "Old Paineel" };
// One colour per place everywhere (Home, Relevels, Ask's route), so the same place reads the same down a column.
const PLACE_HUE = { "Befallen": "#6ea6f2", "Estate of Unrest": "#8fd18f", "Castle of Mistmoore": "#c3a0f0", "Kedge Keep": "#5fc4b8",
  "Plane of Hate": "#ec9a86", "Ruins of Old Guk": "#d9b46a", "Nagafen's Lair": "#f2a65a", "Lair of the Splitpaw": "#b2d17a", "Plane of Fear": "#e88ac0" };
const EXTRA_HUES = ["#7fb3d5", "#d4a5a5", "#a3d9a5", "#e0c27a", "#9fa8da", "#f0a6ca", "#80cbc4", "#c5b358"];
const OPEN_HUE = "#4a5563";

/** "The Plane of Hate - Solo" → "Plane of Hate": the place a colour belongs to. */
const placeName = z => (z || "").replace(/^The /, "").replace(/ - (Solo|Group)$/, "");
function placeHue(z) {
  const p = placeName(z);
  if (PLACE_HUE[p]) return PLACE_HUE[p];
  let h = 0;
  for (const ch of p) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return EXTRA_HUES[h % EXTRA_HUES.length];
}
const shortPlace = z => { const s = (z || "").replace(/^The /, ""), p = placeName(s); return (PLACE_SHORT[p] ?? p) + s.slice(p.length); };

/** The XP a relevel needed, from the engine's list for each reincarnation rank of its trio (Planner.XpByRank). */
const xpOf = rank => { const t = data?.reinc?.xpByRank ?? []; return t.length ? t[Math.min(Math.max(rank ?? 0, 0), t.length - 1)] : 1; };
/**
 * An XP requirement as players say it: 1.2 → "+20% XP to level" (docs/pace.md: never "1.2×" or "multiplier" on screen).
 * The penalty is a bigger bar, never less XP from a kill: mobs give the same, so a kill fills a smaller share of the bar.
 */
const xpWords = x => `+${fmt((x - 1) * 100, 0)}% XP to level`;
/** The same under an "XP to level" heading: "+20%". */
const xpPct = x => `+${fmt((x - 1) * 100, 0)}%`;
/** What the penalty means, for a tooltip beside it. */
const xpTip = x => x > 1.0001
  ? `Each level needs ${fmt((x - 1) * 100, 0)}% more XP than before reincarnating. Mobs give the same XP: a kill fills a smaller share of the bar only because the bar is bigger.`
  : "No reincarnation penalty: each level needs its usual XP.";
/** A trio's like-for-like pace in words (1.08 → "8% faster than your usual"), never "×1.08". */
function paceVs(p, short) {
  if (p == null) return "—";
  const d = Math.round(Math.abs(p - 1) * 100);
  return d === 0 ? (short ? "usual" : "at your usual pace") : `${d}% ${p > 1 ? "faster" : "slower"}${short ? "" : " than your usual"}`;
}

// ---------------------------------------------------------------- pace, like for like (docs/pace.md)
/** The crawls a relevel played, newest first, from History: each carries its kill speed against your usual and a verdict. */
function journeyVisits(j) {
  const a = new Date(j.start), b = j.done ? new Date(new Date(j.end).getTime() + 5 * 60e3) : null;
  return (data.history ?? []).filter(v => v.difficulty != null && new Date(v.start) >= a && (!b || new Date(v.start) <= b));
}
// Kill speed over a relevel or a stop, its word and tone come from the engine (journey.killPace, row.stopPace): the page draws them.
/**
 * A race the engine worked out (reinc.journeyRaces, Analysis/JourneyRace.cs: hours and curves re-figured to one XP, the band,
 * splits, best of each) as rows with their relevels; the first row is the best and carries its curve.
 */
const raceRows = race => (race?.rows ?? []).map((r, i) => ({ ...r, j: data.reinc.journeys[r.journey], curve: i === 0 ? race.bestCurve : null }));

/**
 * A relevel's route as stops: stops under half a level fold into the one before (or the next, at the start), back-to-back
 * stops at one place and difficulty join, and the stops are scaled to span the levels the relevel covered.
 */
function relevelStops(j, to) {
  const out = [];
  let carry = null;
  const add = (a, b) => { a.lv += b.lv; a.min += b.min; a.n += b.n; a.t5 += b.t5; };
  for (const l of j.route ?? []) {
    if (l.levelFrom == null) continue;
    const open = l.kind === "open world";
    const name = open ? "open world" : (l.baseZone || "").replace(/^The /, "");
    const x = { key: open ? "open" : name + "|" + (l.difficulty ?? ""), name, open, d: open ? null : l.difficulty,
      lv: l.levels || 0, min: l.activeMinutes || 0, n: l.crawls || 0, t5: l.t5 || 0 };
    const last = out[out.length - 1];
    if (x.lv < 0.5) { if (last) add(last, x); else if (carry) add(carry, x); else carry = x; continue; }
    if (last && last.key === x.key) { add(last, x); continue; }
    if (carry) { add(x, carry); carry = null; }
    out.push(x);
  }
  if (carry) out.push(carry);
  const from = j.route?.find(l => l.levelFrom != null)?.levelFrom ?? j.levelFrom;
  const span = Math.max(0, (to ?? j.levelNow) - from), total = out.reduce((s, x) => s + x.lv, 0);
  let at = from, m = 0;
  out.forEach(x => { x.lv = total > 0 ? x.lv * span / total : 0; x.a = at; at += x.lv; x.b = at; x.m0 = m; m += x.min; });
  return out;
}

/** One stop as a bar segment: placed by level (1 to the cap) or by active minutes (out of maxMin). */
function stopSeg(x, o) {
  const top = o.cap ?? cap();
  const left = o.mode === "time" ? x.m0 / o.maxMin * 100 : (x.a - 1) / (top - 1) * 100;
  const width = o.mode === "time" ? x.min / o.maxMin * 100 : (x.b - x.a) / (top - 1) * 100;
  const c = x.open ? OPEN_HUE : placeHue(x.name);
  const bg = !x.open && x.d && x.d < 4 ? `repeating-linear-gradient(135deg, ${c} 0 6px, ${c}b0 6px 9px)` : c;
  const label = x.open ? "open world" : shortPlace(x.name) + (x.d && x.d !== 4 ? " D" + x.d : "");
  const units = x.open ? (x.n === 1 ? "1 stretch" : `${x.n} stretches`) : plural(x.n, "visit");
  const title = `${x.open ? "Open world" : x.name + (x.d ? " D" + x.d : "")} · levels ${Math.floor(x.a)}–${Math.round(x.b)} · ${fmt(x.min, 0)} min · ${units} · ${fmt(x.t5, 0)} T5-eq`;
  return `<div class="rseg${x.open ? " open" : ""}" style="left: calc(${left.toFixed(2)}% + 1px); width: calc(${width.toFixed(2)}% - 2px); background: ${bg}" title="${esc(title)}">${width >= 4 ? esc(label) : ""}</div>`;
}
/** Ask's route legs ("Befallen D1", "… · open world") as dashed or solid segments by level; o.minutes: the legs' minutes re-figured by the engine. */
function askSegs(legs, o = {}) {
  const top = cap();
  return legs.map((l, i) => {
    const open = / · open world$/.test(l.place), m = /^(.*?)(?: D(\d+))?(?: · open world)?$/.exec(l.place);
    const name = m[1], d = m[2] ? +m[2] : null, c = open ? OPEN_HUE : placeHue(name);
    const left = (l.from - 1) / (top - 1) * 100, width = (l.to - l.from) / (top - 1) * 100;
    const label = open ? "open world" : shortPlace(name) + (d && d !== 4 ? " D" + d : "");
    const mins = l.minutes != null ? ` · about ${fmt(o.minutes ? o.minutes[i] : l.minutes, 0)} min at your pace` : "";
    const bg = o.dashed ? "transparent" : (d && d < 4 ? `repeating-linear-gradient(135deg, ${c} 0 6px, ${c}b0 6px 9px)` : c);
    return `<div class="rseg${o.dashed ? " next" : ""}" style="left: calc(${left.toFixed(2)}% + 1px); width: calc(${width.toFixed(2)}% - 2px); background: ${bg};${o.dashed ? ` border-color: ${c}; color: ${c}` : ""}"
      title="${esc(`${o.dashed ? "Ask's route: " : ""}${l.place}, levels ${l.from}–${l.to}${mins}`)}">${width >= 4 ? esc(label) : ""}</div>`;
  }).join("");
}
/** Ask's route.gaps (level L: nobody has numbers from L to L + 1) as runs: [1, 2, … 8] → [[1, 9]]. */
function gapRuns(gaps) {
  const runs = [];
  for (const L of [...(gaps ?? [])].sort((a, b) => a - b)) {
    const last = runs[runs.length - 1];
    if (last && last[1] === L) last[1] = L + 1; else runs.push([L, L + 1]);
  }
  return runs;
}
/** "from level 1 to 9", "from level 1 to 9 and 23 to 24". */
const gapWords = gaps => "from level " + gapRuns(gaps).map(([a, b]) => `${a} to ${b}`).join(", ").replace(/, ([^,]*)$/, " and $1");
/** The levels a route has no numbers for, as labelled blanks on a level bar (a route's hours leave them out). */
function gapSegs(gaps) {
  const top = cap();
  return gapRuns(gaps).map(([a, b]) => {
    const left = (a - 1) / (top - 1) * 100, width = (b - a) / (top - 1) * 100;
    const label = width >= 12 ? `no numbers for ${a}–${b}` : width >= 7 ? "no numbers" : "";
    return `<div class="rseg gapseg" style="left: calc(${left.toFixed(2)}% + 1px); width: calc(${width.toFixed(2)}% - 2px)"
      title="${esc(`No numbers from level ${a} to ${b}: no crawls there in your logs or from the players who share. The route's hours leave them out.`)}">${label}</div>`;
  }).join("");
}
/** Level ticks under a level bar: 1, 10, 20 … the cap. */
function levelTicks(first = "") {
  const top = cap(), ls = [1]; for (let l = 10; l < top; l += 10) ls.push(l); ls.push(top);
  return `<div class="rticks">${ls.map((l, i) => `<span class="num" style="${i === 0 ? "left: 0" : i === ls.length - 1 ? "right: 0" : `left: ${((l - 1) / (top - 1) * 100).toFixed(2)}%; transform: translateX(-50%)`}">${i === 0 ? first : ""}${l}</span>`).join("")}</div>`;
}

/**
 * The ghost race: your best (blue), the middle half of your relevels (band) and the typical one (dashed), with a relevel in
 * progress on top in gold and its projection dashed. cfg: { race (from the engine, reinc.journeyRaces), you, proj, height }.
 */
function drawGhost(host, cfg) {
  const w = host.clientWidth;
  if (w < 120) return;
  const top = cap(), race = cfg.race, best = raceRows(race)[0];
  if (!best && !cfg.you) { host.innerHTML = `<div class="empty">The race starts with your first relevel after a reincarnation.</div>`; return; }
  const band = race.band;
  const maxH = Math.max(race.maxHours, ...[cfg.you, cfg.proj].filter(c => c?.length).flatMap(c => c.map(p => p.hours)));
  const step = maxH > 14 ? 4 : maxH > 6 ? 2 : 1, xMax = Math.ceil(maxH / step) * step;
  const H = cfg.height ?? 300, L = 40, R = 24, T = 26, B = 28, pw = w - L - R, ph = H - T - B;
  const X = h => L + h / xMax * pw, Y = l => T + (top - l) / (top - 1) * ph;
  const path = pts => pts.map((p, i) => (i ? "L" : "M") + X(p.hours).toFixed(1) + " " + Y(p.level).toFixed(1)).join(" ");
  let s = `<svg width="${w}" height="${H}" viewBox="0 0 ${w} ${H}" role="img" aria-label="Level against active hours: ${cfg.you ? "this relevel, " : ""}your best and the middle half of your relevels. History → Relevels has the numbers.">`;
  for (let l = 10; l <= top; l += 10) s += `<line x1="${L}" x2="${w - R}" y1="${Y(l)}" y2="${Y(l)}" stroke="#262f3a"/><text x="${L - 8}" y="${Y(l) + 4}" fill="#8d959e" font-size="11" text-anchor="end">${l}</text>`;
  s += `<line x1="${L}" x2="${w - R}" y1="${T + ph}" y2="${T + ph}" stroke="#3a4552"/>`;
  for (let h = 0; h <= xMax; h += step) s += `<text x="${X(h)}" y="${H - 8}" fill="#8d959e" font-size="11" text-anchor="middle">${h}${h === 0 || h === xMax ? " h" : ""}</text>`;
  if (band.lo.length) s += `<path d="${path(band.lo)} ${band.hi.slice().reverse().map(p => "L" + X(p.hours).toFixed(1) + " " + Y(p.level).toFixed(1)).join(" ")} Z" fill="#3b4552" fill-opacity=".75"/>`;
  if (band.med.length) s += `<path d="${path(band.med)}" fill="none" stroke="#a9b1ba" stroke-width="1.5" stroke-dasharray="5 4"/>`;
  if (best) s += `<path d="${path(best.curve)}" fill="none" stroke="var(--blue)" stroke-width="2.2" stroke-linejoin="round"/>`;
  if (cfg.proj?.length > 1) s += `<path d="${path(cfg.proj)}" fill="none" stroke="var(--gold)" stroke-width="2" stroke-dasharray="5 5" opacity=".85"/>`;
  if (cfg.you?.length > 1) {
    const p = cfg.you[cfg.you.length - 1];
    s += `<path d="${path(cfg.you)}" fill="none" stroke="var(--gold)" stroke-width="2.8" stroke-linejoin="round" stroke-linecap="round"/>`;
    s += `<circle cx="${X(p.hours)}" cy="${Y(p.level)}" r="5" fill="var(--gold)" stroke="var(--panel)" stroke-width="2.5"/>`;
    s += `<text x="${X(p.hours) + 10}" y="${Y(p.level) + 18}" fill="#ece7da" font-size="12" font-weight="600">${Math.floor(p.level)} at ${fmt(p.hours)} h</text>`;
    // How far ahead of (or behind) your best you are at this level, drawn as the gap itself (docs/pace.md).
    const now = race.now, bestAt = now?.curveBestAt;
    if (bestAt != null && Math.abs(now.gap) >= 1 / 60) {
      const ahead = now.gap > 0, color = ahead ? "var(--good)" : "var(--warn)", gap = Math.abs(now.gap);
      const x1 = X(Math.min(p.hours, bestAt)), x2 = X(Math.max(p.hours, bestAt)), y = Y(p.level);
      // The words go in the legend (gapWords): on the line they'd sit on your projection.
      s += `<line x1="${x1 + 6}" x2="${x2}" y1="${y}" y2="${y}" stroke="${color}" stroke-width="1.5" stroke-dasharray="3 3"><title>${gap >= 1 ? fmt(gap) + " h" : fmt(gap * 60, 0) + " min"} ${ahead ? "ahead of" : "behind"} your best at ${Math.floor(p.level)}</title></line>`;
    }
  }
  // Finish labels above the top line: the earlier on its left, the later on its right.
  const ends = [];
  if (best) ends.push({ h: best.hours, text: `best ${fmt(best.hours)} h`, color: "#8fb8f5" });
  if (cfg.proj?.length) { const e = cfg.proj[cfg.proj.length - 1]; ends.push({ h: e.hours, text: `you ≈ ${fmt(e.hours)} h`, color: "var(--gold)", ring: true }); }
  else if (band.hi.length) ends.push({ h: band.hi[band.hi.length - 1].hours, text: `middle half ${fmt(band.lo[band.lo.length - 1].hours)}–${fmt(band.hi[band.hi.length - 1].hours)} h`, color: "#a9b1ba" });
  ends.sort((a, b) => a.h - b.h).forEach((e, i) => {
    if (e.ring) s += `<circle cx="${X(e.h)}" cy="${Y(top)}" r="4.5" fill="var(--panel)" stroke="var(--gold)" stroke-width="2"/>`;
    const left = ends.length > 1 && i === 0;
    s += `<text x="${X(e.h) + (left ? -8 : 8)}" y="${T - 9}" fill="${e.color}" font-size="12" text-anchor="${left ? "end" : "start"}">${esc(e.text)}</text>`;
  });
  s += `<line class="cursor" x1="0" x2="0" y1="${T}" y2="${T + ph}" stroke="#ece7da" stroke-opacity=".35" visibility="hidden"/>`;
  s += `<rect class="hit" x="${L}" y="${T}" width="${pw}" height="${ph}" fill="transparent"/></svg>`;
  host.innerHTML = s;

  const tip = $("#tip"), cursor = host.querySelector(".cursor"), hit = host.querySelector(".hit");
  const lv = (c, h) => { const v = levelAt(c, h); return v == null ? null : Math.min(top, v); };
  hit.addEventListener("mousemove", e => {
    const box = hit.getBoundingClientRect(), h = (e.clientX - box.left) / box.width * xMax;
    cursor.setAttribute("x1", X(h)); cursor.setAttribute("x2", X(h)); cursor.setAttribute("visibility", "visible");
    const row = (color, name, v, dashed) => v == null ? "" : `<div class="r"><i style="background: ${color}"></i>${esc(name)}<span style="margin-left: auto; padding-left: 12px" class="num">${dashed ? "≈ " : ""}${fmt(v, 1)}</span></div>`;
    const youNow = cfg.you ? lv(cfg.you, h) : null;
    const lines = [
      youNow != null && h <= cfg.you[cfg.you.length - 1].hours ? row("var(--gold)", "You", youNow) : cfg.proj ? row("var(--gold)", "You, projected", lv(cfg.proj, h), true) : "",
      best ? row("var(--blue)", `Best: ${best.j.trio}`, lv(best.curve, h)) : "",
      band.med.length ? row("#a9b1ba", "Typical", lv(band.med, h)) : "",
      band.lo.length && lv(band.hi, h) != null ? `<div class="r dim">Middle half<span style="margin-left: auto; padding-left: 12px" class="num">${fmt(lv(band.hi, h), 1)}–${fmt(lv(band.lo, h) ?? top, 1)}</span></div>` : "",
    ].join("");
    tip.innerHTML = `<div style="font-weight: 600; margin-bottom: 4px">${fmt(h, 1)} active hours</div>${lines}`;
    tip.hidden = false;
    tip.style.left = Math.min(e.clientX + 14, window.innerWidth - tip.offsetWidth - 8) + "px";
    tip.style.top = Math.max(8, e.clientY - tip.offsetHeight - 10) + "px";
  });
  hit.addEventListener("mouseleave", () => { tip.hidden = true; cursor.setAttribute("visibility", "hidden"); });
}

/** The race legend's line for the gap drawn on the chart: "2.1 h ahead of your best at 46", or "". */
function gapLegend(race, who = "your best") {
  const now = race?.now;
  if (now?.gap == null || Math.abs(now.gap) < 1 / 60) return "";
  const gap = Math.abs(now.gap), ahead = now.gap > 0, color = ahead ? "var(--good)" : "var(--warn)";
  return `<span style="color: ${color}"><i style="border-top: 2px dashed ${color}; height: 0; background: none"></i>${gap >= 1 ? fmt(gap) + " h" : fmt(gap * 60, 0) + " min"} ${ahead ? "ahead of" : "behind"} ${who} at ${Math.floor(now.curveLevel)}</span>`;
}

/** Level against hours, linear between points (null before the first point or past the last). */
function levelAt(pts, h) {
  if (!pts?.length || h < pts[0].hours) return null;
  for (let i = 1; i < pts.length; i++)
    if (h <= pts[i].hours) {
      const a = pts[i - 1], b = pts[i];
      return b.hours === a.hours ? b.level : a.level + (h - a.hours) / (b.hours - a.hours) * (b.level - a.level);
    }
  return null;
}

// ---------------------------------------------------------------- History → Relevels
let relView = store.get("relView", "routes"), relAxis = store.get("relAxis", "level"), relSort = store.get("relSort", "fast");

function renderRelevels() {
  const el = $("#relevelsBody");
  if (!el || !data?.reinc) return;
  const act = activeJourney(), race = data.reinc.journeyRaces.all, cmp = raceRows(race), fin = cmp.map(c => c.j);
  const best = cmp[0], typical = race.typicalHours;
  const last = fin.slice().sort((a, b) => new Date(b.end) - new Date(a.end))[0];
  const xpNote = race.mixedRanks || (act && fin.some(j => j.rank !== act.rank))
    ? `Hours are re-figured for ${xpWords(act ? xpOf(act.rank) : xpOf(last?.rank))} (${act ? "this relevel's" : "your latest relevel's"}); under each is its hours as played. Kill speed needs no re-figuring.`
    : fin.length ? `All needed ${xpWords(xpOf(fin[0].rank))}, so their hours compare as they are.` : "";
  const segBtn = (key, cur, items) => `<div class="seggroup" role="group">${items.map(([v, label]) =>
    `<button data-rel="${key}" data-v="${v}" class="${v === cur ? "on" : ""}" aria-pressed="${v === cur}">${label}</button>`).join("")}</div>`;
  el.innerHTML = `
    <section class="card" style="padding: 16px 20px 12px" aria-label="Relevel race">
      <div class="between" style="align-items: flex-start">
        <div><div class="h">The race</div>
          <div class="sub" style="margin-top: 2px">${fin.length ? `${plural(fin.length, "relevel")} · best ${fmt(best.hours)} h · typical ${fmt(typical)} h${last ? ` · last ${fmt(cmp.find(c => c.j === last).hours)} h` : ""}` : "No finished relevels yet"}${act ? ` · ${esc(act.trio)} in progress` : ""}</div></div>
      </div>
      <div class="legend" style="margin-top: 10px">
        ${act ? `<span><i style="background: var(--gold)"></i>${esc(act.trio)} now · dashed = projected</span>` : ""}
        ${best ? `<span><i style="background: var(--blue)"></i>Your best: ${esc(best.j.trio)} ${esc(best.j.race ?? "")}</span>` : ""}
        ${fin.length > 1 ? `<span><i style="background: #3b4552; height: 10px"></i>Middle half of your relevels</span><span><i style="border-top: 2px dashed #a9b1ba; height: 0; background: none"></i>Typical</span>` : ""}
        ${act ? gapLegend(race) : ""}
      </div>
      <div class="chart" id="relevelChart"></div>
    </section>
    <section class="card" style="padding: 16px 20px 14px; margin-top: 16px" aria-label="Every relevel">
      <div class="between" style="flex-wrap: wrap; gap: 10px">
        <div><div class="h">Every relevel</div><div class="sub" style="margin-top: 2px">${esc(xpNote)} · +≈ is hours still to go${infoTip("estimate")}</div></div>
        <div class="row" style="gap: 8px; flex-wrap: wrap">
          ${segBtn("view", relView, [["routes", "Routes"], ["splits", "Splits"]])}
          ${relView === "routes" ? segBtn("axis", relAxis, [["level", "By level"], ["time", "By hours"]]) : ""}
          ${segBtn("sort", relSort, [["fast", "Fastest"], ["new", "Newest"]])}
        </div>
      </div>
      ${relView === "splits" ? relevelSplits(race, cmp, act) : relevelRoutes(race, cmp, act)}
    </section>
    ${relevelPaces(data.reinc)}`;
  drawRelevels();
}

/**
 * The relevels you played against each other in the same places, and the stretches that cost the most hours. Facts about
 * what happened, never a forecast for a trio (docs/pace.md).
 */
function relevelPaces(r) {
  const paces = (r.paces?.paces ?? []).slice().reverse(), detours = r.paces?.detours ?? [];
  const rows = paces.length ? paces.map(p => {
    const c = p.pace == null ? "var(--faint)" : p.pace < 0.97 ? "var(--warn)" : p.pace > 1.02 ? "var(--good)" : "var(--text)";
    const bits = [p.done ? `${fmt(p.activeHours)} h to ${cap()}` : "leveling now", p.sharedHours > 0 ? `${fmt(p.sharedHours)} h to compare` : "too little to compare",
      plural(p.deaths, "death"), p.groupedShare >= 0.3 ? `${fmt(p.groupedShare * 100, 0)}% grouped` : ""].filter(Boolean).join(" · ");
    return `<div class="between" style="padding: 7px 0; border-bottom: 1px solid var(--line2); align-items: center">
      <div style="min-width: 0"><div style="font-weight: 600">${esc(p.trio)} <span class="dim" style="font-weight: 400; font-size: 11.5px">relevel ${p.number}</span></div>
        <div class="dim" style="font-size: 11.5px">${esc(bits)}</div></div>
      <div class="num" style="font-weight: 650; color: ${c}">${paceVs(p.pace, true)}</div></div>`;
  }).join("") : `<div class="dim">No relevel yet.</div>`;
  const maxL = Math.max(1, ...detours.map(l => l.hours));
  const went = detours.length ? detours.map(l => `
    <div class="lever">
      <div class="between"><span style="font-weight: 600">${esc(l.name)}</span><span class="num" style="font-weight: 650">+${fmt(l.hours)} h</span></div>
      <div class="bar"><i style="width: ${Math.min(100, l.hours / maxL * 100)}%"></i></div>
      <div class="dim" style="font-size: 11.5px; margin-top: 3px; line-height: 1.4">${esc(l.source)}</div>
    </div>`).join("") : `<div class="dim">Finish two relevels to see where the hours went.</div>`;
  return `<div class="plan" style="margin-top: 16px">
    <section class="card" style="padding: 16px 20px 12px" aria-label="Your relevels, like-for-like">
      <div class="h">Your relevels, like-for-like</div>
      <div class="sub" style="margin-top: 2px; line-height: 1.4">How fast each relevel went against your other relevels in the same zones, difficulties and levels: solo crawls, with the reincarnation penalty taken out. It says what happened, not what another trio would do.</div>
      <div style="margin-top: 8px">${rows}</div>
    </section>
    <section class="card" style="padding: 16px 18px 12px" aria-label="Where the hours went">
      <div class="h">Where the hours went</div>
      <div class="sub" style="margin-top: 2px">Stretches that took longer than your usual for those levels</div>
      <div style="margin-top: 12px">${went}</div>
      <button class="link" data-go="route" style="margin-top: 10px">See every place →</button>
    </section>
  </div>`;
}

/** Every relevel as a row (the engine's, best first, or newest first), the one on now on top with its level and splits. */
function relevelRows(race, cmp, act) {
  const rows = cmp.map(c => ({ j: c.j, hours: c.hours, f: c.f, splitMinutes: c.splitMinutes }));
  if (relSort === "new") rows.sort((a, b) => new Date(b.j.end) - new Date(a.j.end));
  if (act) rows.unshift({ j: act, hours: act.activeHours, f: 1, splitMinutes: race.now?.splitMinutes ?? [], level: race.now?.level ?? act.levelNow, now: true });
  return rows;
}
function relevelName(r, best) {
  return `<div style="font-weight: 600${r.now ? "; color: var(--gold)" : r.j === best?.j ? "; color: var(--gold)" : ""}">${r.j === best?.j ? "★ " : ""}${esc(r.j.trio)}</div>
    <div class="dim" style="font-size: 11.5px; margin-top: 2px">${esc(r.j.race ?? "?")} · ${r.now ? `now, level ${r.j.levelNow}` : dt(r.j.start).split(",")[0]}</div>`;
}

function relevelRoutes(race, cmp, act) {
  const rows = relevelRows(race, cmp, act);
  if (!rows.length) return `<div class="empty">Your relevels show here once a reincarnated trio reaches the cap.</div>`;
  const maxMin = Math.max(...rows.map(r => r.j.activeHours * 60), 1);
  const seen = new Set();
  const body = rows.map(r => {
    const stops = relevelStops(r.j, r.now ? r.level : undefined);
    stops.forEach(x => seen.add(x.open ? "open world" : placeName(x.name)));
    const own = ` title="${esc(`${fmt(r.j.activeHours)} h as played, at ${xpWords(xpOf(r.j.rank))} · ${fmt(r.j.wallHours, 0)} h wall`)}"`;
    const kp = r.j.killPace;
    return `<div class="relrow">
      <div>${relevelName(r, cmp[0])}</div>
      <div style="text-align: right"${own}><div class="num" style="font-weight: 600">${fmt(r.hours)} h${r.now && r.j.etaHours != null ? ` <span class="dim">+≈${fmt(r.j.etaHours)}</span>` : ""}</div><div class="dim num" style="font-size: 11.5px; margin-top: 2px">${r.f !== 1 ? `${fmt(r.j.activeHours)} h played` : `${fmt(r.j.wallHours, 0)} h wall`}</div></div>
      <div class="num" style="text-align: right" title="${kp?.speed == null ? `No run in it had ${usualRuns()} earlier runs near its level to compare with` : `Kill speed against your usual at each place and level: ${kp.word.toLowerCase()}`}">${kp?.speed == null ? '<span class="dim">·</span>' : `<span class="${kp.tone}">${fmt(kp.speed * 100, 0)}%</span>`}</div>
      <div class="rstrip">${stops.map(x => stopSeg(x, { mode: relAxis, maxMin })).join("")}</div>
      <span class="num" style="text-align: right">${fmt(r.j.t5, 0)}</span>
      <span class="num" style="text-align: right">${r.j.deaths || ""}</span>
    </div>`;
  }).join("");
  const ticks = relAxis === "time"
    ? `<div class="rticks">${Array.from({ length: Math.floor(maxMin / 120) + 1 }, (_, i) => i * 2).map(h => `<span class="num" style="${h === 0 ? "left: 0" : `left: ${(h * 60 / maxMin * 100).toFixed(2)}%; transform: translateX(-50%)`}">${h}${h === 0 ? " h" : ""}</span>`).join("")}</div>`
    : levelTicks("level ");
  const legend = [...seen].sort((a, b) => (a === "open world") - (b === "open world")).map(p =>
    `<span><i style="background: ${p === "open world" ? OPEN_HUE : placeHue(p)}; width: 10px; height: 10px"></i>${esc(p === "open world" ? "Open world" : p)}</span>`).join("");
  return `
    <div class="relrow head"><span>Trio</span><span style="text-align: right">Active</span><span style="text-align: right" title="Kills a minute against your usual at the same place and level, over the runs that had ${usualRuns()} earlier ones to compare with">Kill speed</span>${ticks}<span style="text-align: right">T5-eq</span><span style="text-align: right">Deaths</span></div>
    ${body}
    <div class="legend" style="margin-top: 12px">${legend}<span><i style="width: 18px; height: 10px; background: repeating-linear-gradient(135deg, #8d959e 0 6px, #8d959eb0 6px 9px)"></i>striped = D1 to D3</span></div>
    <p class="note">${relAxis === "time" ? "Each bar is as long as the relevel's active hours, so the race reads straight down." : "Each bar runs from level 1 to the cap, so the same levels line up down the column."}
      Stops under half a level fold into the one before; back-to-back stops at one place join. Hover a stop for its minutes, visits and motes.</p>`;
}

function relevelSplits(race, cmp, act) {
  const rows = relevelRows(race, cmp, act);
  if (!rows.length) return `<div class="empty">Your relevels show here once a reincarnated trio reaches the cap.</div>`;
  const marks = race.splitLevels, mins = rows.map(r => r.splitMinutes), bestCol = race.bestOfEach;
  const where = (j, lo, hi) => [...new Set(relevelStops(j).filter(x => x.b > lo + 0.2 && x.a < hi - 0.2).map(x => x.open ? "open world" : shortPlace(x.name) + (x.d ? " D" + x.d : "")))].join(", ");
  const head = marks.map((L, k) => `<span class="num" style="text-align: right">${k ? marks[k - 1] : 1} → ${L}</span>`).join("");
  const body = rows.map((r, i) => `<div class="splitrow">
      <div>${relevelName(r, cmp[0])}</div>
      ${mins[i].map((v, k) => v == null ? `<span class="dim" style="text-align: right">·</span>`
        : `<span class="num" style="text-align: right${!r.now && Math.abs(v - bestCol[k]) < 0.01 ? "; color: var(--gold); font-weight: 700" : ""}" title="${esc(where(r.j, k ? marks[k - 1] : 1, marks[k]))}">${fmt(v, 0)}</span>`).join("")}
      <span class="num" style="text-align: right; font-weight: 600">${r.now ? `${fmt(r.hours)} h <span class="dim">so far</span>` : `${fmt(r.hours)} h`}</span>
    </div>`).join("");
  const sum = race.bestOfEachHours;
  return `
    <div class="splitrow head"><span>Trio</span>${head}<span style="text-align: right">Total</span></div>
    ${body}
    ${sum != null && cmp.length > 1 ? `<div class="splitrow" style="border-bottom: 0"><div style="font-weight: 650; color: var(--gold)">Best of each</div>
      ${bestCol.map(v => `<span class="num" style="text-align: right; color: var(--gold); font-weight: 700">${fmt(v, 0)}</span>`).join("")}
      <span class="num" style="text-align: right; color: var(--gold); font-weight: 700">${fmt(sum)} h</span></div>` : ""}
    <p class="note">Active minutes for each stretch of levels. Gold is your fastest at that stretch.${sum != null && cmp.length > 1 ? ` Your best stretches add up to ${fmt(sum)} h: the relevel you'd have if one run did them all, ${fmt(race.bestOfEachUnderBest)} h under your best.` : ""} Hover a number for where you were.</p>`;
}

function drawRelevels() {
  const host = $("#relevelChart");
  if (!host || page !== "relevels" || !data?.reinc) return;
  const act = activeJourney();
  drawGhost(host, { race: data.reinc.journeyRaces.all, you: act?.curve, proj: act?.projection, height: 320 });
}

$("#relevelsBody")?.addEventListener("click", e => {
  const b = e.target.closest("[data-rel]");
  if (!b) return;
  const v = b.dataset.v;
  if (b.dataset.rel === "view") store.set("relView", relView = v);
  if (b.dataset.rel === "axis") store.set("relAxis", relAxis = v);
  if (b.dataset.rel === "sort") store.set("relSort", relSort = v);
  renderRelevels();
});
