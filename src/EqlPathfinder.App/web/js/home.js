"use strict";
// ---------------------------------------------------------------- home
// Home answers the one question for what you're doing. Releveling: am I on pace? (the race, as a ghost against your best).
// At the cap and crawling: is this a good farm? At the cap otherwise: what's next? Auto picks from the log and says so; at
// the cap you can flip it, and the pick holds until auto changes its mind.

let homeModeNow = "between";
// Label for an open relevel that is not currently active.
const PARKED = "Parked";
/** The player's pick at the cap ("between" = Next relevel). Kept while the app runs and Home stays at the cap: every start opens on Farming. */
let homePick = null;
/** What Home shows ("relevel", "leveling", "swapped", "farming" or "between") and what auto picked. The engine picks (reinc.homeNow). */
function homeMode(r) {
  const auto = r.homeNow?.mode ?? "leveling";
  if (auto === "farming") return { mode: homePick === "between" ? "between" : "farming", auto };
  return { mode: auto === "nolevel" ? "swapped" : auto === "newloadout" || auto === "classes" ? "loadout" : auto, auto };
}
const modeSwitch = (mode, auto) => `<div style="text-align: right; flex: none">
    <div class="seggroup" role="group" aria-label="Home shows">${[["between", "Next relevel"], ["farming", "Farming"]].map(([v, label]) =>
      `<button data-home-mode="${v}" class="${mode === v ? "on" : ""}" aria-pressed="${mode === v}">${label}</button>`).join("")}</div>
    <div class="dim" style="font-size: 11.5px; margin-top: 6px">${mode !== auto ? "Your pick · auto says farming" : `Auto: you're at ${cap()}`}</div>
  </div>`;
// ---------------------------------------------------------------- what a relevel is, and what it is compared with
// Inferred from where the relevel was played: `reinc.homeKind` names the relevel's dominant difficulty
// (current relevel once it has crawl hours, else latest finished) and whether it counts motes.
// Home compares only with relevels of the same kind; Ask handles explicit goal selection.
const homeKind = () => data?.reinc?.homeKind ?? null;
/** The farmed difficulties were just changed in Settings and the new verdicts haven't come: the pages show theirs faded. */
const goalWaiting = () => farmWaiting;
const staleIf = on => on ? " stale" : "";
/** What Home compares a relevel with: the relevel's own kind. */
const kindOf = act => homeKind()?.mostly ?? act?.mostly ?? null;
const mostlyWord = k => k === "mixed" ? "mixed" : `mostly ${k}`;
/** "at D4", "on a mixed relevel": for "your best at D4". */
const atWord = k => k === "mixed" ? "on a mixed relevel" : `at ${k}`;
/** Why nothing compares: no finished relevel is of this kind. */
const noneLike = (k, n) => `none of your ${plural(n, "relevel")} was ${mostlyWord(k)}`;
/** The one dim line that says what Home counts and why: Ask's route counts motes when the relevel is farmed, else it is fastest. */
function kindLine() {
  const k = homeKind();
  if (!k) return `<div class="dim" style="font-size: 11.5px; margin-top: 4px">Ask's route is the fastest: no relevel of yours to go by yet.</div>`;
  const w = mostlyWord(k.mostly), what = k.source === "now" ? `this relevel is ${w}` : `your last relevel, ${esc(k.trio)}, was ${w}`;
  return `<div class="dim${staleIf(farmWaiting)}" style="font-size: 11.5px; margin-top: 4px">${k.farmed
    ? `Counting motes: ${what}${k.mostly === "mixed" ? ", most of it at a difficulty you farm" : ", a difficulty you farm"}. <button class="link" data-go="ask" data-ask-q="level" data-ask-pick="fast">Ask it another way →</button>`
    : `Fastest: ${what}. <button class="link" data-go="ask" data-ask-q="level" data-ask-pick="motes">Ask it another way →</button>`}</div>`;
}
$("#homeHero").addEventListener("click", e => {
  const b = e.target.closest("[data-home-mode]");
  if (!b || !data?.reinc) return;
  homePick = b.dataset.homeMode === "between" ? "between" : null;
  renderHome();
});

/** The journey of a relevel row the engine lists (Home's now or a parked row); none for one not played yet. */
const journeyOf = (r, p) => p.journeyIndex != null ? r.journeys[p.journeyIndex] ?? null : null;
/** The level a relevel is at, whole: a curve's level has a fraction in it. */
const levelOf = (r, p) => { const j = journeyOf(r, p); return j ? Math.floor(j.reachedLevel) : p.level; };
/** The open relevels that aren't on now, under the trio: trio and race, level, and the hours to go. */
function parkedBlock(r, label) {
  const js = r.homeNow?.parked ?? [];
  if (!js.length) return "";
  const shown = js.slice(0, 3), more = js.length - shown.length;
  return `<div style="margin-top: 12px"><div class="lbl">${label ?? PARKED + " relevel" + (js.length > 1 ? "s" : "")}</div>
    <div class="parked">${shown.map(p => { const j = journeyOf(r, p); return `<span><b style="font-weight: 600">${esc(p.trio)}</b> <span class="muted">${esc(p.race ?? "")}</span></span>
      <span class="num muted">${levelOf(r, p)} / ${cap()}</span>
      ${j?.etaHours != null ? `<span class="num gold" style="white-space: nowrap">≈ ${fmt(j.etaHours)} h ${etaWords(j)}${infoTip("estimate")}</span>` : !j || j.crawls === 0 ? `<span class="dim">not played yet</span>` : "<span></span>"}`; }).join("")}</div>
    ${more > 0 ? `<button class="link" data-go="relevels" style="margin-top: 4px">+ ${more} more in History → Relevels</button>` : ""}</div>`;
}

function renderHome() {
  renderHomeNotice();
  renderHomeShare();
  const r = data.reinc;
  if (!r) return;
  let { mode, auto } = homeMode(r);
  const act = activeJourney();
  if (auto !== "farming") homePick = null;
  // On a relevel not played yet there is no journey to draw: Leveling's card, headed with the relevel the engine lists.
  const unplayed = mode === "relevel" && !act ? r.homeNow.now : null;
  if (unplayed) mode = "leveling";
  homeModeNow = mode;
  $("#homeHero").innerHTML = mode === "relevel" ? relevelHero(r, act) : mode === "farming" ? farmingHero(r, auto)
    : mode === "between" ? betweenHero(r, auto) : mode === "swapped" ? swappedHero(r) : mode === "loadout" ? loadoutHero(r) : levelingHero(r, unplayed);
  $("#homeMain").innerHTML = mode === "relevel" ? raceCard(act) : mode === "farming" ? dropsCard() : relevelsCard();
  $("#homeSide").innerHTML = mode === "relevel" ? splitsCard(act) + placesCard(act) : mode === "farming" ? farmList(r, `Where to farm at ${cap()}`) + doneCard(r)
    : mode === "between" ? farmList(r, `Or farm at ${cap()}`) : "";
  // Farming is about this session: Up next and the roster wait for when you're done.
  const usual = mode !== "farming";
  $("#homeUpNextCard").hidden = $("#homeRosterCard").hidden = !usual;
  if (usual) { renderUpNext(r, act, mode !== "between" && mode !== "swapped" && mode !== "loadout"); renderHomeRoster(r, act); }
  drawRace();
}

// ---------------------------------------------------------------- releveling
const fmtDelta = h => { const m = Math.round(h * 60); return m === 0 ? "even" : `${m > 0 ? "+" : "−"}${Math.abs(m)} min`; };
const deltaClass = h => Math.round(h * 60) > 0 ? "warn" : Math.round(h * 60) < 0 ? "good" : "muted";

function relevelHero(r, act) {
  const fin = finishedJourneys(), legs = r.home?.route?.legs ?? [], next = suggestedRoute();
  const kind = kindOf(act), wait = goalWaiting(), nFin = fin.length;
  // The race is the engine's (reinc.journeyRaces.now): your best and typical at your level, and how far ahead you are.
  const race = data.reinc.journeyRaces.now, nw = race?.now, cmp = raceRows(race), lv = nw?.level ?? act.levelNow;
  const bestAt = nw?.bestAt ?? null, typAt = nw?.typicalAt ?? null;
  const extra = [act.instanceHours > 0 ? `${fmt(act.instanceHours * 60, 0)} min in other instances` : "", act.fieldHours > 0 ? `${fmt(act.fieldHours * 60, 0)} min open world` : ""].filter(Boolean);
  // Am I on pace (docs/pace.md)? Kill speed is yours; hours against your best are re-figured for this trio's XP penalty.
  const kp = act.killPace, word = kp?.word, best = cmp[0];
  const top = (act.trio ?? "").split("/").map(k => r.classes?.find(c => c.key === k)).filter(Boolean).sort((a, b) => b.rank - a.rank)[0];
  const pastOld = newLv() && lv >= newLv().from;
  const xpChip = pastOld ? xpPastChip(act.trio, act.rank) : act.rank > 0 ? `<span class="chip xp" title="${xpTip(xpOf(act.rank))}${xpNewTip()} The most-reincarnated of your trio and race sets it.">${xpWords(xpOf(act.rank))}${top?.rank ? ` · ${esc(top.name)} ×${top.rank}` : ""}</span>` : "";
  const ahead = nw?.ahead ?? null;
  const aheadText = ahead == null ? "" : Math.abs(ahead) < 1 / 60 ? "Even with your best"
    : `${Math.abs(ahead) >= 1 ? fmt(Math.abs(ahead)) + " h" : fmt(Math.abs(ahead) * 60, 0) + " min"} ${ahead > 0 ? "ahead of" : "behind"} your best ${atWord(kind)}`;
  const bestOwn = nw?.bestOwn ?? null;
  const sentence = best && bestOwn != null ? `Your best ${atWord(kind)}, ${esc(best.j.trio)}, reached ${Math.floor(lv)} after ${fmt(bestOwn)} h${best.f !== 1
    ? ` needing ${xpWords(xpOf(best.j.rank))}: about ${fmt(bestAt)} h at your ${xpWords(xpOf(act.rank))}` : ""}.${cmp.length > 1 && typAt != null ? ` Typical ${atWord(kind)}: ${fmt(typAt)} h.` : ""}`
    : nFin && kind ? `Nothing to race ${atWord(kind)} yet: ${noneLike(kind, nFin)}.` : "";
  return `
    <div class="between" style="align-items: flex-start">
      <div><div class="lbl">Leveling now · relevel ${r.homeNow?.now?.number ?? fin.length + 1}</div>
        <div class="trio">${trioSpaced(act.trio)} <span class="race">${esc(act.race ?? "")}</span></div>
        ${xpChip ? `<div style="margin-top: 8px">${xpChip}${infoTip("xp")}</div>` : ""}
        ${parkedBlock(r)}
        ${bestAt != null || sentence ? `<div class="${staleIf(wait).trim()}"><div class="row" style="gap: 10px; margin-top: 12px; align-items: center; flex-wrap: wrap">
          ${word ? `<span class="chip ${kp.tone}" style="font-size: 13px" title="Kill speed this relevel against your usual at the same places and levels">${word}</span>` : ""}
          ${bestAt != null ? `<span class="delta ${ahead > 1 / 60 ? "good" : ahead < -1 / 60 ? "warn" : "muted"}">${aheadText}</span>` : ""}</div>
          <div class="dim" style="font-size: 12px; margin-top: 4px; max-width: 620px; line-height: 1.45">${sentence}</div></div>` : ""}</div>
      <div style="text-align: right">
        <div class="big num">${act.levelNow}<span style="font-size: 18px; color: var(--faint); font-weight: 500"> / ${cap()}</span></div>
        <div class="num muted" style="margin-top: 6px">${fmt(act.activeHours)} h so far${act.etaHours != null ? ` · <span style="white-space: nowrap"><b class="gold">≈ ${fmt(act.etaHours)} h ${etaWords(act)}</b>${infoTip("estimate")}</span>` : ""}</div>
      </div>
    </div>
    <div class="rstrip${staleIf(wait)}" style="margin-top: 16px">${relevelStops(act, lv).map(x => stopSeg(x, { mode: "level" })).join("")}${askSegs(legs, { dashed: true })}${gapSegs(r.home?.route?.gaps)}
      <div class="rhere" style="left: calc(${pct(lv)}% - 1px)" title="level ${fmt(lv, 1)}"></div></div>
    ${levelTicks()}
    <div class="between" style="margin-top: 8px; gap: 8px 12px; align-items: flex-start; flex-wrap: wrap">
      <span class="sub num" style="flex: 1 1 100%">${next.length ? `Next: ${esc(next[0].zone)}${next[0].to < cap() ? " until " + next[0].to : ""}${next[1] ? ", then " + esc(next[1].zone) : ""} · ` : ""}${plural(act.crawls, "crawl").replace(" ", "\u00a0")}${extra.length ? " + " + extra.map(x => x.replace(/(d) /g, "$1\u00a0")).join(" + ") : ""} · ${fmt(act.t5, 0)}\u00a0T5-eq${infoTip("t5eq")} · ${plural(act.deaths, "death").replace(" ", "\u00a0")}${act.etaNote ? " · " + esc(act.etaNote) : ""}</span>
      <div style="text-align: right; flex: none; margin-left: auto">
        ${next.length ? `<div class="dim${staleIf(wait)}" style="font-size: 12px">dashed = Ask's route from here${infoTip("likeforlike")}</div>` : ""}</div>
    </div>
    ${kindLine()}
    ${paceTiles(act, lv)}`;
}

/** You and the place, like for like (docs/pace.md): this relevel's kill speed, and what the place you're in gives a kill. */
function paceTiles(act, lv) {
  const kp = act.killPace, ks = kp?.speed, counted = kp?.runs ?? 0;
  const others = finishedJourneys().map(j => j.killPace?.speed).filter(x => x != null);
  const range = others.length > 1 ? ` Your other ${others.length} relevels: ${fmt(Math.min(...others) * 100, 0)}% to ${fmt(Math.max(...others) * 100, 0)}%.`
    : others.length ? ` Your other relevel: ${fmt(others[0] * 100, 0)}%.` : "";
  const speed = ks == null
    ? `<div class="pbig dim">No usual yet</div><div class="psub">None of this relevel's runs has ${usualRuns()} earlier ones near its level, at its place, to compare with.</div>`
    : `<div class="pbig num ${kp.tone}">${fmt(ks * 100, 0)}% <span class="pk">of your usual</span></div>
       <div class="psub">Over the ${plural(counted, "run")} with ${usualRuns()}+ earlier ones near their level to compare with.${range}</div>`;
  // The place: the crawl going now, else the latest one this relevel.
  const s = data.snapshot, live = s?.current && s.current.state !== "finished" && s.pace?.run ? s : null, last = journeyVisits(act)[0];
  const xp = live ? live.pace.run.xp : last?.xpVerdict;
  const place = live ? live.pace.place : last?.placeVerdict ? { kind: last.placeVerdict, better: last.betterPlace } : null;
  const zone = live ? zoneShort(live.current.baseZone, live.current.difficulty) : last ? zoneShort(last.baseZone, last.difficulty) : "";
  // A finished crawl was judged at the level it started at, not at today's (you may have dinged since).
  const at = `at ${Math.floor(live || last?.levelStart == null ? lv : last.levelStart)}`;
  // Farming and grouped visits are judged in T5-eq/h against the player's usual there.
  const fv = live ? (live.pace.farm && ["farming", "grouped"].includes(live.pace.kind) ? { ...live.pace.farm, kind: live.pace.kind } : null)
    : last && ["farming", "grouped"].includes(last.kind) && last.farmVerdict ? { verdict: last.farmVerdict, pct: last.farmPct, usual: last.farmUsual, t5PerHour: last.t5PerHour, basis: last.farmBasis, levels: last.farmLevels, kind: last.kind } : null;
  const [head, text] = fv
    ? [`${farmChip(fv, fv.kind === "grouped")} ${esc(zone)}`,
       fv.t5PerHour == null ? `Too short for a rate yet: the rate starts at ${rateFromKills()} kills, or when the crawl is finished.`
         : fv.usual ? `${fmt(fv.t5PerHour, 0)} T5-eq/h here, your usual ${fmt(fv.usual, 0)} at ${esc(zone)} (${esc(fv.basis ?? "visits like this one")}).`
         : `${fmt(fv.t5PerHour, 0)} T5-eq/h here. Under ${usualRuns()} visits like this one at ${esc(zone)}${fv.levels ? ", " + fv.levels : ""} to say what is usual.`]
    : place
    ? [`<span class="chip amber">${place.kind === "fading" ? "Fading · " : ""}${esc(place.better)} levels faster</span> ${esc(zone)}`,
       `${esc(place.better)} ${live ? "is" : "was"} clearly faster at the same difficulty ${at}${place.betterLevelsPerHour ? `: ${fmt(place.betterLevelsPerHour)} levels/h against ${fmt(place.levelsPerHour)} here` : ""}${live ? "" : ", by your crawls before it"}.`]
    : xp === "low" ? [`<span class="chip warn">Low</span> ${esc(zone)} ${at}`, "Less XP per kill than this place gives at your level: mobs may be conning low, or a group is splitting it."]
    : xp ? [`<span class="chip good">Normal</span> ${esc(zone)} ${at}`, `What this place gives a kill at your level${act.rank > 0 ? `, as a share of your bigger bar (${xpWords(xpOf(act.rank))}: mobs give the same XP)` : ""}. Every place gives about 8% less a level, so a falling number alone is no problem.`]
    : [`<span class="chip">No usual yet</span> ${esc(zone)}`, `Under ${usualRuns()} runs here to say what it gives at your level.`];
  return `<div class="ptiles${staleIf(goalWaiting())}">
      <div class="ptile"><div class="lbl">Kill speed · you${infoTip("killSpeed")}</div>${speed}</div>
      <div class="ptile"><div class="lbl">${fv ? "T5-eq/h · the place" : `XP per kill · the place${infoTip("xpPerKill")}`}</div><div style="font-size: 15px; font-weight: 650; margin-top: 8px">${head}</div>
        <div class="psub">${text} <button class="link" data-go="ask" data-ask-q="level">Ask's route →</button></div></div>
    </div>`;
}

/** A farming or grouped verdict as a chip: T5-eq an hour against your usual (a visit's, or a whole stop's). `levels` comes from the engine (FarmPace.Levels), below the cap only. */
function farmChip(f, grouped) {
  const pre = grouped ? "Grouped · " : "", pct = fmt(Math.abs(f.pct ?? 0) * 100, 0);
  return f.verdict === "more" ? `<span class="chip good">${pre}${pct}% more T5-eq/h</span>` : f.verdict === "less" ? `<span class="chip warn">${pre}${pct}% less T5-eq/h</span>`
    : f.verdict === "usual" ? `<span class="chip good">${pre}Usual T5-eq/h</span>`
    : f.verdict === "early" ? `<span class="chip" title="No rate yet: a finished crawl, or ${rateFromKills()} kills">${pre}Too short to judge yet</span>`
    : `<span class="chip" title="Under ${usualRuns()} visits like this one here${f.levels ? " at " + f.levels : ""}">${pre}No usual yet</span>`;
}

/** This relevel place by place: whether a stop's slower levels were you (kill speed) or the place (docs/pace.md). */
function placesCard(act) {
  const vis = journeyVisits(act).slice().reverse(), legs = [];
  for (const v of vis) {
    const last = legs[legs.length - 1];
    if (last && last.zone === v.baseZone && last.diff === v.difficulty) last.runs.push(v); else legs.push({ zone: v.baseZone, diff: v.difficulty, runs: [v] });
  }
  if (!legs.length) return "";
  const rows = legs.map(l => {
    const name = zoneShort(l.zone, null) + ` D${l.diff}`;
    const first = l.runs[0], end = l.runs[l.runs.length - 1];
    const kp = l.runs.find(v => v.stopPace)?.stopPace, ks = kp?.speed, pl = l.runs.find(v => v.placeVerdict), slow = l.runs.filter(v => v.pace === "slow").length;
    // Judge by play type: farming/grouped in T5-eq/h, raids by boss kill, everything else as leveling.
    // The engine judges the whole stop (its T5-eq over its active hours) against the same usual, not its first visit.
    const farm = l.runs.find(v => v.stopFarm)?.stopFarm, raid = l.runs.every(v => v.kind === "raid");
    const motes = farm?.t5PerHour;   // the engine's one rate over the stop; null under the rule
    const boss = l.runs.find(v => v.boss)?.boss;
    const chip = raid ? (boss ? `<span class="chip good">${esc(boss)} killed</span>` : `<span class="chip dim">No boss kill</span>`)
      : farm ? farmChip(farm, l.runs.some(v => v.kind === "grouped"))
      : pl ? `<span class="chip amber" title="${esc(pl.betterPlace ?? "")} levels faster than this place at the same difficulty, by your crawls before this one">${pl.placeVerdict === "fading" ? "Fading · " : ""}${esc(pl.betterPlace ?? "Another place")} levels faster</span>`
      : ks != null ? `<span class="chip ${kp.tone}">${kp.word}</span>` : `<span class="chip" title="Under ${usualRuns()} earlier runs near these levels, at this place and difficulty">No usual yet</span>`;
    const lvhNum = [first.levelsPerHour, ...(l.runs.length > 1 ? [end.levelsPerHour] : [])];
    const lvh = (lvhNum.every(x => x == null) && (!farm || motes == null)) ? `Too short for a rate yet`
      : [lvhNum.some(x => x != null) ? `${lvhNum.map(x => x == null ? "—" : fmt(x)).join(" → ")} levels/h` : null, farm && motes != null ? `${fmt(motes, 0)} T5-eq/h` : null].filter(Boolean).join(" · ");
    const note = pl && !farm ? `${lvh}. ${esc(pl.betterPlace)} levels faster at this difficulty.` : slow && ks != null && ks >= 0.85 ? `${lvh}. ${plural(slow, "run")} slow on kills.` : lvh;
    return `<div class="legrow"><div style="min-width: 0"><div>${esc(name)} <span class="dim num">${first.levelStart ?? ""}–${end.levelEnd ?? end.levelStart ?? ""}</span></div>
      <div class="dim num" style="font-size: 11.5px; margin-top: 2px">${note}</div></div>${chip}</div>`;
  }).join("");
  return `<section class="card${staleIf(goalWaiting())}" style="padding: 16px 18px" aria-label="This relevel, place by place">
    <div class="h">This relevel, place by place</div>
    <div class="dim" style="font-size: 11.5px; margin-top: 2px; line-height: 1.45">Levels/h falls about 4–7% a level everywhere. Farmed difficulties are judged in T5-eq an hour; the rest say when it was you, or the place.</div>
    ${rows}
  </section>`;
}

function raceCard(act) {
  const kind = kindOf(act), race = data.reinc.journeyRaces.now, cmp = raceRows(race), best = cmp[0], nFin = finishedJourneys().length;
  const refigured = race.refigured;
  return `<section class="card${staleIf(goalWaiting())}" style="padding: 16px 20px 12px" aria-label="The race">
    <div class="between"><div class="h">The race${kind ? " " + atWord(kind) : ""}${act.rank > 0 && refigured ? `, all at your ${xpWords(xpOf(act.rank))}` : ""}</div><button class="link" data-go="relevels">All relevels →</button></div>
    <div class="legend" style="margin-top: 10px">
      <span><i style="background: var(--gold)"></i>You · dashed = projected</span>
      ${best ? `<span><i style="background: var(--blue)"></i>Your best ${atWord(kind)}: ${esc(best.j.trio)} ${fmt(best.hours)} h</span>` : nFin && kind ? `<span class="dim">No ${kind === "mixed" ? "mixed relevel" : "relevel"} of yours ${kind === "mixed" ? "" : "mostly "}${kind === "mixed" ? "" : atWord(kind) + " "}to race yet</span>` : ""}
      ${cmp.length > 1 ? `<span><i style="background: #3b4552; height: 10px"></i>Middle half of your ${plural(cmp.length, "relevel")} ${atWord(kind)}</span>` : ""}
      ${gapLegend(race, kind === "mixed" ? "your mixed best" : `your ${kind} best`)}
    </div>
    <div class="chart" id="raceChart"></div>
    ${refigured ? `<div class="dim" style="font-size: 11.5px; margin-top: 4px">Your ${kind === "mixed" ? "mixed" : kind} relevels are re-figured for this trio's ${xpWords(xpOf(act.rank))}. History → Relevels has their hours as played.</div>` : ""}
  </section>`;
}

/** Splits against your best: reached levels from your curve, later ones from the projection (≈). */
function splitsCard(act) {
  const kind = kindOf(act), race = data.reinc.journeyRaces.now, best = raceRows(race)[0], nw = race?.now;
  if (!best || !nw) return "";
  const lv = nw.level, rows = [];
  const row = (label, you, b, delta, opts = {}) => `<div class="splitmini${opts.now ? " now" : ""}">
      <span style="${opts.now ? "color: var(--gold); font-weight: 650" : "color: var(--soft)"}">${label}</span>
      <span class="num" style="text-align: right; ${opts.projected ? "color: var(--muted)" : "color: var(--gold); font-weight: 600"}">${you == null ? "·" : (opts.projected ? "≈ " : "") + fmt(you)}</span>
      <span class="num" style="text-align: right; color: #8fb8f5">${b == null ? "·" : fmt(b)}</span>
      <span class="num ${delta != null ? deltaClass(delta) : ""}" style="text-align: right${opts.projected ? "; opacity: .75" : ""}">${delta != null ? fmtDelta(delta) : ""}</span>
    </div>`;
  let nowDone = false;
  for (const s of nw.splits) {
    if (!nowDone && s.level > lv) { rows.push(row(`now ${Math.floor(lv)}`, act.activeHours, nw.bestAt, nw.ahead == null ? null : -nw.ahead, { now: true })); nowDone = true; }
    rows.push(row(String(s.level), s.you, s.best, s.delta, { projected: s.projected }));
  }
  return `<section class="card${staleIf(goalWaiting())}" style="padding: 16px 18px" aria-label="Splits against your best">
    <div class="h">Splits against your best ${atWord(kind)}</div>
    <div class="dim" style="font-size: 11.5px; margin-top: 2px">Active hours to reach each level · best ${atWord(kind)} is ${esc(best.j.trio)} (${fmt(best.j.activeHours)}\u00a0h as played · ${fmt(best.j.t5, 0)}\u00a0T5-eq)</div>
    <div class="splitmini head"><span>Level</span><span style="text-align: right">You</span><span style="text-align: right">Best</span><span style="text-align: right">+/−</span></div>
    ${rows.join("")}
    <div class="dim" style="font-size: 11.5px; margin-top: 8px; line-height: 1.45">Past your level, "You" is the projection: the dashed line on the race.</div>
  </section>`;
}

// ---------------------------------------------------------------- at the cap, between relevels
function betweenHero(r, auto) {
  // The next relevel is your campaign's next run. No hours for it: how long a trio takes isn't knowable before you play it.
  const top = campNext();
  // The next run's XP, Ask's route at it and the relevel to beat at it are the engine's (reinc.journeyRaces.next).
  const next = r.journeyRaces.next, nextXp = next.xp;
  const route = r.home?.route, legs = route?.legs ?? [];
  const kind = homeKind()?.mostly ?? null, wait = staleIf(goalWaiting()), nFin = finishedJourneys().length;
  const pb = raceRows(next.race)[0];
  const toBeat = !legs.length || !nFin || !kind ? "" : pb
    ? `To beat: <b style="color: var(--text)">${fmt(pb.j.activeHours)} h · ${fmt(pb.j.t5, 0)} T5-eq</b> (${esc(pb.j.trio)}, ${mostlyWord(pb.j.mostly)})${Math.abs(pb.f - 1) > 0.001 ? `, which is <b style="color: var(--text)">≈ ${fmt(pb.hours)} h</b> at this trio's ${xpWords(nextXp)}` : ""}`
    : `Nothing to beat ${atWord(kind)} yet: ${noneLike(kind, nFin)}.`;
  const gapNote = route?.gaps?.length ? ` for ${route.to - route.from - route.gaps.length} of ${route.to - route.from} levels` : "";
  const firsts = top ? r.classes.filter(c => c.rank === 0 && top.classes.includes(c.key)) : [];
  const never = r.classes.filter(c => c.rank === 0);
  // With no reincarnation in the log and no counts set, the ranks are a guess: say so, and offer Roster's counts.
  const guess = ranksUnconfirmed(r), names = firsts.map(c => esc(c.name)).join(" and ");
  const why = !firsts.length ? "" : guess ? `First for ${names} if you haven't reincarnated before. `
    : never.length === 1 ? `${esc(firsts[0].name)} is your last class for Completionist. ` : `First reincarnation for ${names}. `;
  return `
    <div class="between" style="align-items: flex-start">
      <div><div class="lbl">At ${cap()} · between relevels</div>
        <div class="trio">${r.currentTrio ? trioSpaced(r.currentTrio) + ` <span class="race">${esc(data.snapshot?.race ?? "")}</span>` : "Loadout not known yet"}</div>
        ${parkedBlock(r)}</div>
      ${modeSwitch("between", auto)}
    </div>
    <div class="divider"></div>
    ${top ? `
    <div class="between" style="align-items: flex-end; gap: 20px">
      <div style="min-width: 0">
        <div class="lbl gold">Up next · ${guess ? "" : `relevel ${r.homeNow.nextNumber} · `}run ${top.n} of ${top.of} in your campaign</div>
        <div style="font-size: 22px; font-weight: 700; margin-top: 4px">${esc(top.classes.join(" / "))} <span class="race">${esc(top.race)}</span></div>
        <div class="muted" style="margin-top: 5px; line-height: 1.45">${why}From your campaign.</div>
      </div>
      <div style="text-align: right; flex: none">
        <div class="big num gold" title="${xpTip(nextXp)}${xpNewTip()}">+${fmt((nextXp - 1) * 100, 0)}%</div>
        <div class="dim num" style="margin-top: 6px; font-size: 12px">XP to level, ${guess ? "if it's your first" : `rank ${top.rankAfter}`}${infoTip("xp")}</div>
      </div>
    </div>` : `<div class="muted">Pick the passive ranks you're after and Campaign lays out every reincarnation toward them. <button class="link" data-go="campaign">Plan it →</button></div>`}
    ${guess ? countsNudge(r, "", "margin: 16px 0 0") : ""}
    <div style="margin-top: 16px">
      <div class="between" style="margin-bottom: 6px; gap: 6px 14px; flex-wrap: wrap; align-items: center">
        <span class="row" style="gap: 10px; flex-wrap: wrap; align-items: center"><span class="lbl">Ask's route</span>
          ${legs.length ? `<span class="num${wait}">≈ <b>${fmt(next.routeMinutes / 60)} h</b>${gapNote} · ${fmt(next.routeT5, 0)} T5-eq${infoTip("t5eq")}${kind ? ` · ${mostlyWord(kind)}${infoTip("likeforlike")}` : ""}</span>`
            : `<span class="muted">Ask has no route to ${cap()} yet.</span>`}</span>
        ${legs.length ? `<span class="dim" style="font-size: 11.5px">hover a stop for its time</span>` : ""}</div>
      ${legs.length ? `<div class="rstrip${wait}">${askSegs(legs, { minutes: next.legMinutes })}${gapSegs(route.gaps)}</div>${levelTicks()}` : ""}${kindLine()}</div>
    <div class="row" style="gap: 14px; margin-top: 10px; flex-wrap: wrap">
      ${toBeat ? `<span class="muted${wait}">${toBeat}</span>` : ""}
      <span style="flex: 1"></span>
      ${top ? `<button class="link" data-go="campaign">Your campaign →</button>` : ""}
    </div>`;
}

/**
 * Your relevels, one bar each as long as its active hours, coloured by where they went: the comparable best and the latest
 * few. Comparable = mostly the same difficulty as Ask's route; non-comparable runs render faded, in hours as played.
 */
function relevelsCard() {
  const fin = finishedJourneys();
  if (!fin.length) return "";
  const kind = homeKind()?.mostly ?? null, own = data.reinc.journeyRaces.own, cmp = raceRows(own), best = cmp[0];
  const typical = own.typicalPlayedHours;
  const byEnd = fin.slice().sort((a, b) => new Date(b.end) - new Date(a.end));
  const shown = [...(best ? [best.j] : []), ...byEnd.filter(j => j !== best?.j).slice(0, 5)];
  const maxMin = Math.max(...shown.map(j => j.activeHours * 60), 1);
  const head = !kind ? `<span class="muted">Ask has no route to compare with yet</span>`
    : best ? `<span class="muted num">${kind === "mixed" ? "Mixed" : "Mostly " + kind}, like Ask's route: best <b style="color: var(--text)">${fmt(best.j.activeHours)} h</b>${cmp.length >= 2 ? ` · typical <b style="color: var(--text)">${fmt(typical)} h</b>` : ""}</span>`
    : `<span class="muted">None ${mostlyWord(kind)} like Ask's route yet</span>`;
  return `<section class="card${staleIf(goalWaiting())}" style="padding: 16px 20px 14px" aria-label="Your relevels">
    <div class="between" style="gap: 14px; flex-wrap: wrap">
      <div class="row" style="gap: 16px; align-items: baseline; flex-wrap: wrap"><div class="h">Your relevels</div>${head}</div>
      <button class="link" data-go="relevels">All ${fin.length > shown.length ? plural(fin.length, "relevel") : "relevels"} →</button>
    </div>
    <div class="dim" style="font-size: 11.5px; margin-top: 3px">One bar per relevel, as long as its active hours, coloured by where the hours went.${kind ? ` Faded: ${kind === "mixed" ? "mostly at one difficulty" : "mostly at another difficulty"}, so not compared.` : ""}${fin.length > shown.length ? (best ? " Your best and the latest five." : " Your latest five.") : ""}</div>
    <div style="margin-top: 12px; display: flex; flex-direction: column; gap: 7px">
      ${shown.map(j => `<div class="timebar${kind && j.mostly !== kind ? " other" : ""}">
        <span style="font-weight: 600; font-size: 12.5px; color: ${j === best?.j ? "var(--gold)" : "var(--text2)"}">${j === best?.j ? "★ " : ""}${esc(j.trio)}</span>
        <div class="rstrip">${relevelStops(j).map(x => stopSeg(x, { mode: "time", maxMin })).join("")}</div>
        <span class="dim" style="font-size: 11.5px">${mostlyWord(j.mostly ?? "mixed")}</span>
        <span class="num muted" style="text-align: right; font-size: 11.5px">${fmt(j.t5, 0)} T5-eq</span>
        <span class="num" style="text-align: right">${fmt(j.activeHours)} h</span></div>`).join("")}
    </div>
  </section>`;
}

/** Farms at the cap: yours since the patch, then the players who share (since the patch, else all time). */
function farmList(r, title) {
  const a = r.home?.farm, mine = (a?.rows ?? []).slice(0, 3);
  const since = !!a?.community?.length, pool = (since ? a.community : a?.communityAllTime ?? []).slice(0, mine.length ? 3 : 5);
  const max = Math.max(...[...mine, ...pool].map(x => x.t5PerHour || 0), 1);
  const patch = farmPatch();
  const item = (x, first) => `<div>
      <div class="between" style="gap: 8px"><span>${esc(x.place)}</span><span class="num"><b>${fmt(x.t5PerHour)}</b> <span class="dim" style="font-size: 11.5px">${plural(x.crawls, "crawl")}</span></span></div>
      <div class="bar" style="margin-top: 4px"><i style="width: ${(x.t5PerHour / max * 100).toFixed(1)}%; background: ${first ? "var(--gold)" : "var(--gray)"}"></i></div></div>`;
  const players = pool.length ? Math.max(...pool.map(x => x.players || 0)) : 0;
  return `<section class="card" style="padding: 16px 18px" aria-label="${esc(title)}">
    <div class="between"><div class="h">${esc(title)}</div><button class="link" data-go="ask" data-ask-q="farm">Every farm at ${cap()} →</button></div>
    ${mine.length ? `<div class="dim" style="font-size: 11.5px; margin-top: 10px">Yours, since the ${esc(patch)} patch</div>
      <div style="margin-top: 6px; display: flex; flex-direction: column; gap: 9px">${mine.map((x, i) => item(x, i === 0)).join("")}</div>`
      : `<div class="muted" style="margin-top: 6px; line-height: 1.45">${a?.crawls
        ? `Too little at ${cap()} since the ${esc(patch)} patch to rank a place yet. A place ranks at ${plural(a.minCrawls, "crawl")} and ${fmt(a.minHours * 60, 0)} min.`
        : `You haven't farmed at ${cap()} since the ${esc(patch)} patch.`}</div>`}
    ${pool.length ? `<div class="dim" style="font-size: 11.5px; margin-top: 12px">Players who share, ${since ? "since the patch" : "all time"} (${plural(players, "player")})</div>
      <div style="margin-top: 6px; display: flex; flex-direction: column; gap: 9px">${pool.map((x, i) => item(x, !mine.length && i === 0)).join("")}</div>` : ""}
    <div class="dim" style="font-size: 11.5px; margin-top: 9px">T5-eq an hour in crawls${infoTip("t5eq")}</div>
  </section>`;
}

// ---------------------------------------------------------------- at the cap, farming
function farmingHero(r, auto) {
  const s = data.snapshot?.session ?? {}, a = r.home?.farm, top = cap();
  const classesChanged = r.homeNow?.loadout?.state === "classes";
  const loadoutNew = r.homeNow?.loadout?.state === "new";
  const place = s.zone ? zoneShort(s.zone, s.difficulty) : null;
  const here = place ? [...(a?.community ?? []), ...(a?.communityAllTime ?? [])].find(x => x.place === place) : null;
  const usual = r.farm?.crawls ? r.farm.usual : null;
  // The engine says when the session has no rate (a finished crawl, or rateFromKills kills): until then it shows what's in so far.
  const early = !!s.early, rate = s.visits && !early ? s.t5PerHourCrawl : null;
  const took = m => m < 60 ? `${fmt(m, 0)} min` : `${fmt(m / 60)} h`;
  const max = Math.max(rate ?? 0, here?.t5PerHour ?? 0, usual ?? 0, 1);
  const cmpRow = (what, v, note, color, attrs = "") => `<div${attrs}>
      <div class="between" style="gap: 10px"><span>${what}</span><span class="num"><b>${v == null ? "—" : fmt(v)}</b> <span class="dim" style="font-size: 11.5px">${note}</span></span></div>
      <div class="bar" style="margin-top: 4px"><i style="width: ${v == null ? 0 : (v / max * 100).toFixed(1)}%; background: ${color}"></i></div></div>`;
  return `
    <div class="between" style="align-items: flex-start">
      <div><div class="lbl">Farming at ${top}${place ? " · " + esc(place) : ""}</div>
        <div class="trio">${data.snapshot?.trio || r.currentTrio ? trioSpaced(data.snapshot?.trio || r.currentTrio) + ` <span class="race">${esc(data.snapshot?.race ?? "")}</span>`
          : classesChanged ? `Classes not known yet${data.snapshot?.race ? ` <span class="race">${esc(data.snapshot.race)}</span>` : ""}` : "Loadout not known yet"}</div>
        ${classesChanged ? `<div class="muted" style="font-size: 12.5px; margin-top: 4px">You changed a class on loadout ${slotNo(r)}. Type /who in game and Home follows the new trio.</div>` : ""}
        ${loadoutNew && !data.snapshot?.trio && !r.currentTrio ? `<div class="muted" style="font-size: 12.5px; margin-top: 4px">Pathfinder hasn't met loadout ${slotNo(r)}. Type /who in game and Home follows it.</div>` : ""}
        ${parkedBlock(r)}</div>
      ${modeSwitch("farming", auto)}
    </div>
    <div class="divider"></div>
    ${s.visits ? `
    <div style="display: grid; grid-template-columns: 200px minmax(0, 1fr); gap: 28px; align-items: center">
      ${early ? `<div><div class="num gold" style="font-size: 42px; font-weight: 700; line-height: 1">${fmt(s.t5, 0)}</div>
        <div class="muted" style="margin-top: 6px">T5-eq so far this session${infoTip("t5eq")}</div>
        <div class="dim" style="font-size: 11.5px; margin-top: 2px">${took(s.crawlMinutes)} in crawls. The rate starts at ${s.rateFromKills} kills, or when a crawl is finished.</div></div>`
      : `<div><div class="num gold" style="font-size: 42px; font-weight: 700; line-height: 1">${fmt(rate)}</div>
        <div class="muted" style="margin-top: 6px">T5-eq an hour this session, in crawls${infoTip("t5eq")}</div>
        <div class="dim" style="font-size: 11.5px; margin-top: 2px">${fmt(s.t5PerHourWall)} counting time between crawls</div></div>`}
      <div style="display: flex; flex-direction: column; gap: 10px">
        ${cmpRow("This session", rate, early ? `${plural(s.visits, "crawl")}, too soon to say` : plural(s.visits, "crawl"), "var(--gold)")}
        ${cmpRow("Players who share, here", here?.t5PerHour ?? null, here ? `${plural(here.crawls, "crawl")}, ${plural(here.players, "player")}` : "not enough shared yet", "var(--gray)")}
        ${cmpRow(`Your typical farm at ${top} since the patch`, usual, usual == null ? (!farmCounted().length ? "no difficulty farmed" : r.farm?.unrated > 0 ? `${plural(r.farm.unrated, "crawl")} at ${farmWords(farmCounted())}, no rate yet` : `no ${farmWords(farmCounted(), "or")} crawls yet`) : `${plural(r.farm.crawls, "crawl")}, ${farmWords(farmCounted())}`, "var(--gray)", ` id="homeTypicalFarm" class="${staleIf(farmWaiting).trim()}"`)}
      </div>
    </div>
    <div class="chips" style="margin-top: 16px">
      <span class="chip num">${plural(s.visits, "crawl")}</span><span class="chip num">${took(s.crawlMinutes)} in crawls · ${took(s.wallMinutes)} in all</span>
      <span class="chip num">${fmt(s.t5, 0)} T5-eq · ${fmt(s.moteT5, 0)} from motes</span><span class="chip num">${plural(s.kills, "kill")}</span>
      <span class="chip num${s.highTierDrops ? " gold" : ""}">${plural(s.highTierDrops, "drop")} above T5</span>
    </div>` : `<div class="muted">No crawl yet this session. Where to farm is on the right.</div>`}`;
}

function dropsCard() {
  const d = (data.snapshot?.sessionHighTier ?? []).slice().sort((a, b) => new Date(b.time) - new Date(a.time));
  const big = new Set(["Superior", "Grand", "Ascendant", "Infinite"]);
  return `<section class="card" style="padding: 16px 20px" aria-label="Drops above T5 this session">
    <div class="between"><div class="h">Drops above T5 this session</div><button class="link" data-go="loot">All loot →</button></div>
    ${d.length ? `<div style="margin-top: 8px">${d.slice(0, 12).map(x => `<div class="droprow">
        <span class="dim num">${hm(x.time)}</span>
        <span style="font-weight: 600${big.has(String(x.tier)) ? "; color: var(--gold)" : ""}">${esc(x.tier)} ${esc(String(x.kind).toLowerCase())}${x.count > 1 ? " ×" + x.count : ""}</span>
        <span class="muted">${esc(x.source ?? "")}</span></div>`).join("")}</div>`
      : `<div class="empty" style="padding: 20px">None yet this session.</div>`}
  </section>`;
}

function doneCard(r) {
  const s = data.snapshot?.session ?? {}, cn = campNext(), nFin = finishedJourneys().length, kind = homeKind()?.mostly ?? null;
  const cmp = raceRows(data.reinc.journeyRaces.own);
  const line = (text, go, label) => `<div class="between" style="gap: 10px; margin-top: 8px"><span class="muted">${text}</span><button class="link" data-go="${go}">${label} →</button></div>`;
  return `<section class="card" style="padding: 16px 18px 14px" aria-label="When you're done">
    <div class="h">When you're done</div>
    ${line(`${fmt(s.t5 ?? 0, 0)} T5-eq this session`, "upgrade", "Upgrades")}
    ${cn ? line(`Next relevel: ${esc(cn.classes.join(" / "))} ${esc(cn.race)}`, "campaign", "Campaign") : line("No campaign yet", "campaign", "Plan it")}
    ${nFin ? line(!kind ? `Relevels: ${nFin} done` : cmp.length ? `Relevels: best ${atWord(kind)} ${fmt(cmp[0].j.activeHours)} h, ${nFin} done` : `Relevels: ${nFin} done, none ${mostlyWord(kind)} like Ask's route`, "relevels", "History") : ""}
  </section>`;
}

// ---------------------------------------------------------------- below the cap, no relevel going
// First time leveling, or levels the log can't tie to a reincarnation: where to go from here.
/** Below the cap with the loadout not known and a relevel open, or no level seen at all: Home says so and asks for a /who. */
function swappedHero(r) {
  const none = r.homeNow.mode === "nolevel", lvl = r.homeNow.level;
  return `<div class="between" style="align-items: flex-start; gap: 20px">
      <div style="min-width: 0"><div class="lbl">${none ? "Your character" : "Swapped loadouts"}</div>
        <div class="trio">Type /who in game</div>
        <div class="muted" style="margin-top: 8px; max-width: 620px; line-height: 1.45">${none ? "Pathfinder hasn't seen your level in this log yet. /who shows your level, classes and race, and Home follows it from there."
          : `Your level changed to ${lvl} without a ding, so you're on another loadout. The log names it at your next /who, and Home follows it from there.`}</div></div>
      ${none ? "" : `<div style="text-align: right; flex: none"><div class="big num">${lvl}<span style="font-size: 18px; color: var(--faint); font-weight: 500"> / ${cap()}</span></div>
        <div class="num muted" style="margin-top: 6px">loadout not known yet</div></div>`}
    </div>
    ${parkedBlock(r, "Open relevels")}`;
}

/** The slot the player sees in the game's loadout window (the engine's number, 1 to 9). */
const slotNo = r => r.homeNow?.loadout?.slot ?? "";
/** Below the cap on a loadout the app hasn't met (or whose classes changed): the same card as Swapped, asking for one /who. */
function loadoutHero(r) {
  const classes = r.homeNow.mode === "classes", lvl = r.homeNow.level, race = data.snapshot?.race, slot = slotNo(r);
  return `<div class="between" style="align-items: flex-start; gap: 20px">
      <div style="min-width: 0"><div class="lbl">${classes ? "Classes changed" : "New loadout"}</div>
        <div class="trio">Type /who in game${classes ? "" : " once"}</div>
        <div class="muted" style="margin-top: 8px; max-width: 620px; line-height: 1.45">${classes
          ? `You changed a class on loadout ${slot}. One /who names the new trio, and Pathfinder remembers it from then on.`
          : `Pathfinder hasn't met loadout ${slot}, the one you're on, yet. One /who names its classes, race and level, and Pathfinder remembers them from then on, even after you swap loadouts.`}</div></div>
      ${lvl != null ? `<div style="text-align: right; flex: none"><div class="big num">${lvl}<span style="font-size: 18px; color: var(--faint); font-weight: 500"> / ${cap()}</span></div>
        <div class="num muted" style="margin-top: 6px">${classes ? `${race ? esc(race) + " · " : ""}classes not known yet` : "loadout not known yet"}</div></div>` : ""}
    </div>
    ${parkedBlock(r, "Open relevels")}`;
}

/** `relevel`: the open relevel on now that has no crawl yet (the engine's row), so the card says which one it is. */
function levelingHero(r, relevel = null) {
  const lvl = relevel ? relevel.level : r.currentLevel ?? 1, route = r.home?.route, legs = route?.legs ?? [], next = suggestedRoute(), nl = newLv();
  const noRoute = nl && lvl >= nl.from && !legs.length;
  const trioRank = (r.currentTrio ?? "").split("/").map(k => r.classes?.find(c => c.key === k)?.rank ?? 0).reduce((a, b) => Math.max(a, b), 0);
  // A relevel not played yet has no journey to take the penalty from: the trio's top class sets it, at every level.
  const top = relevel ? (relevel.trio ?? "").split("/").map(k => r.classes?.find(c => c.key === k)).filter(Boolean).sort((a, b) => b.rank - a.rank)[0] : null;
  const chip = nl && lvl >= nl.from ? xpPastChip(r.currentTrio, trioRank)
    : relevel && top?.rank > 0 ? `<span class="chip xp" title="${xpTip(xpOf(top.rank))}${xpNewTip()} The most-reincarnated of your trio and race sets it.">${xpWords(xpOf(top.rank))} · ${esc(top.name)} ×${top.rank}</span>` : "";
  return `
    <div class="between" style="align-items: flex-start">
      <div><div class="lbl">${relevel ? `Leveling now · relevel ${relevel.number}` : "Leveling"}</div>
        <div class="trio">${r.currentTrio ? trioSpaced(r.currentTrio) : "Your character"}${relevel?.race ? ` <span class="race">${esc(relevel.race)}</span>` : ""}</div>
        ${chip ? `<div style="margin-top: 8px">${chip}${infoTip("xp")}</div>` : ""}
        ${parkedBlock(r)}</div>
      <div style="text-align: right">
        <div class="big num">${lvl}<span style="font-size: 18px; color: var(--faint); font-weight: 500"> / ${cap()}</span></div>
        ${relevel ? `<div class="num muted" style="margin-top: 6px">not played yet</div>` : ""}
        ${legs.length ? `<div class="num muted${staleIf(goalWaiting())}" style="margin-top: 6px"><b class="gold">≈ ${fmt(route.minutes / 60)} h to ${cap()}</b> by Ask's route${route.gaps?.length ? `, for ${route.to - route.from - route.gaps.length} of ${route.to - route.from} levels` : ""}</div>` : ""}
      </div>
    </div>
    ${legs.length ? `<div class="rstrip${staleIf(goalWaiting())}" style="margin-top: 16px">${askSegs(legs, { dashed: true })}${gapSegs(route.gaps)}<div class="rhere" style="left: calc(${pct(lvl)}% - 1px)" title="level ${lvl}"></div></div>${levelTicks()}` : ""}
    <div class="between" style="margin-top: 8px">
      <span class="sub">${next.length ? `Next: ${esc(next[0].zone)}${next[0].to < cap() ? " until " + next[0].to : ""}${next[1] ? ", then " + esc(next[1].zone) : ""}.` : noRoute ? `${esc(noRouteHead())}: ${esc(noRouteSub(!!r.home?.community))}` : "Ask fills in a route once you've played a few places at these levels."}</span>
      <div style="text-align: right; flex: none; margin-left: auto"><button class="link" data-go="ask" data-ask-q="level">Ask: where to level →</button></div>
    </div>
    ${noRoute ? "" : kindLine()}`;
}

// ---------------------------------------------------------------- Up next and the roster
function renderUpNext(r, act, showIdea) {
  const steps = [], guess = ranksUnconfirmed(r);
  // One step for each open relevel, the one on now first: its route sentence; a parked one says so, once the loadout on now is known.
  const known = !["swapped", "nolevel", "newloadout", "classes"].includes(r.homeNow.mode);
  for (const p of [...(r.homeNow.now ? [r.homeNow.now] : []), ...r.homeNow.parked]) {
    const j = journeyOf(r, p), isNow = p === r.homeNow.now;
    const nx = isNow ? suggestedRoute() : [];
    const where = nx.length ? ` Next: ${esc(nx[0].zone)}${nx[0].to < cap() ? " until " + nx[0].to : ""}${nx[1] ? ", then " + esc(nx[1].zone) : ""}.` : "";
    steps.push({ title: `Finish ${esc(p.trio)}${p.race ? " " + esc(p.race) : ""}`,
      text: `${levelOf(r, p)} → ${cap()}${j?.etaHours != null ? (etaWords(j) === "to go" ? ` in about ${fmt(j.etaHours)} active hours` : `, about ${fmt(j.etaHours)} active hours ${etaWords(j)}`) : !j || j.crawls === 0 ? ", not played yet" : ""}.${where}${isNow || !known ? "" : " " + PARKED + "."}` });
  }
  const never = r.classes.filter(c => c.rank === 0);
  const top = campNext();
  if (guess) {
    steps.push({
      title: `${never.length} classes left for Completionist if you haven't reincarnated before`,
      text: `Your log hasn't seen a reincarnation. <button class="link" data-set-counts>Set your counts</button> if you have.`,
      idea: showIdea ? top : null,
    });
  } else if (never.length) {
    steps.push({
      title: never.length === 1 ? `Reincarnate ${esc(never[0].name)}` : `${never.length} classes left for Completionist`,
      text: never.length === 1
        ? `The last class for Completionist. ${never[0].key} ${never[0].level != null ? `is at ${never[0].level} and ` : ""}has never reincarnated.`
        : `Completionist needs ${never.map(c => c.key).join(", ")}.`,
      idea: showIdea ? top : null,
    });
  } else if (r.classes.length) {
    const minRank = Math.min(...r.classes.map(c => c.rank));
    const behind = r.classes.filter(c => c.rank === minRank).map(c => c.key);
    steps.push({ title: `Every class has reincarnated ${minRank}×`, text: `Next Completionist rank: ${behind.join(", ")} ${behind.length === 1 ? "is" : "are"} at rank ${minRank}.`, idea: showIdea ? top : null });
  }
  const racesDone = r.races.filter(x => x.rank > 0).length;
  const ready = r.races.filter(x => x.rank === 0 && x.level >= cap()).map(x => x.name);
  steps.push(guess ? {
    title: "Races: none in your log yet",
    text: ready.length ? `At ${cap()} with none in your log: ${ready.join(", ")}.` : "A race needs max faction with its three city factions first.",
  } : {
    title: `Races: ${racesDone} of ${r.races.length}`,
    text: ready.length ? `At ${cap()} but never reincarnated: ${ready.join(", ")}.` : `${r.races.length - racesDone} not reincarnated yet. A race needs max faction with its three city factions first.`,
  });
  // Releveling or leveling, the hero has no room for it: the counts nudge leads this card instead.
  $("#upNext").innerHTML = (guess && showIdea ? countsNudge(r, "", "margin: 0 0 12px") : "") + steps.map((s, i) => `
    <div class="step"><span class="badge">${i + 1}</span><div>
      <div style="font-weight: 600">${s.title}</div>
      <div class="muted" style="margin-top: 3px">${s.text}</div>
      ${s.idea ? `<div class="row" style="margin-top: 9px; flex-wrap: wrap; gap: 8px"><span class="dim" style="font-size: 12px">Next in your campaign</span>
        <button class="chip gold" data-go="campaign">${esc(s.idea.classes.join(" / "))} ${esc(s.idea.race)}</button></div>` : ""}
    </div></div>`).join("");
}

function renderHomeRoster(r, act) {
  const busy = new Set(act ? act.trioKey.split("/") : (r.currentTrio ?? "").split("/"));
  const done = r.classes.filter(c => c.rank > 0).length;
  const racesDone = r.races.filter(x => x.rank > 0).length;
  const racesList = r.races.filter(x => x.rank > 0);
  const count = (n, of) => ranksUnconfirmed(r) ? "none in your log yet" : `<b style="color: var(--text)">${n}</b> / ${of} reincarnated`;
  $("#homeRoster").innerHTML = `
    <div class="between" style="margin-top: 12px"><span class="sub">Classes</span><span class="sub num">${count(done, r.classes.length)}</span></div>
    <div class="cells" style="margin-top: 8px">${r.classes.map(c => `<div class="cell${busy.has(c.key) ? " now" : ""}${c.rank === 0 ? " never" : ""}" title="${esc(c.name)} · ${c.rank ? `rank ${c.rank}` : "not reincarnated yet"}${c.level != null ? " · level " + c.level : ""}">${c.key}<span class="dots">${c.rank ? "●".repeat(Math.min(3, c.rank)) : "–"}</span></div>`).join("")}</div>
    <div class="between" style="margin-top: 16px"><span class="sub">Races</span><span class="sub num">${count(racesDone, r.races.length)}</span></div>
    <div class="bar" style="margin-top: 8px"><i style="width: ${racesDone / Math.max(1, r.races.length) * 100}%"></i></div>
    <div class="chips" style="margin-top: 10px">${racesList.map(x => `<span class="chip${act && act.race === x.key ? " gold" : ""}">${esc(x.name)}${act && act.race === x.key ? " · now" : ""}</span>`).join("")}</div>`;
}

// Offered once to copies that haven't chosen yet: sharing and the beta. While sharing is invite-only (the server says so in
// the sharing status) it takes the code a guildmate made, and stays up with the reason when a code doesn't work.
let homeShareAsked = false, homeShareNote = "";
function renderHomeShare() {
  const s = data.settings, el = $("#homeShare"), st = shareStatus || {};
  const needCode = !!st.requireEnrollment;
  const show = !!s && !s.shareEnabled && !st.enabled && (!s.shareIntroSeen || (needCode && !!st.error));
  el.hidden = !show;
  if (!show) return;
  if (!shareStatus && !homeShareAsked) { homeShareAsked = true; post("share", { action: "status" }); }
  if (document.activeElement?.id === "homeShareCode") return;   // don't redraw under someone typing their code
  const kept = document.getElementById("homeShareCode")?.value ?? "";
  el.innerHTML = `
    <div class="between"><div class="h">New: better together</div><button class="link" data-home-share="later">Not now</button></div>
    <div class="muted" style="margin-top: 8px; line-height: 1.5">Share your play and Ask shows how fast other players level and farm where you haven't been.
      Only numbers go (levels, times, kills, motes, damage): no character names, no chat, no clock times. See exactly what's sent, pause it or delete it all in Settings → Sharing.</div>
    ${needCode ? `<div style="margin-top: 10px; line-height: 1.5">Invite-only for now: ask a guildmate who shares for a code (they make one in Settings → Sharing → Code for a friend).</div>
      <div class="row" style="gap: 8px; margin-top: 8px"><input id="homeShareCode" type="search" spellcheck="false" autocomplete="off" placeholder="XXXX-XXXX" maxlength="9"
        value="${esc(kept)}" aria-label="Invite code" style="width: 130px; text-transform: uppercase; letter-spacing: 1px"></div>` : ""}
    ${homeShareNote || (needCode && st.error) ? `<div class="warn" style="margin-top: 8px; font-size: 12.5px">${esc(homeShareNote || st.error)}</div>` : ""}
    <div class="row" style="gap: 10px; margin-top: 12px; flex-wrap: wrap">
      <button class="btn primary" data-home-share="share"${st.busy ? " disabled" : ""}>${st.busy ? "Turning on…" : "Share my play"}</button>
      ${s.beta ? "" : `<button class="btn" data-home-share="beta">Join the beta</button>`}
      <button class="link" data-go="help">Send feedback or vote on what's next →</button>
    </div>`;
}
$("#homeShare").addEventListener("click", e => {
  const b = e.target.closest("[data-home-share]");
  if (!b) return;
  const what = b.dataset.homeShare, needCode = !!shareStatus?.requireEnrollment;
  homeShareNote = "";
  if (what === "share" && needCode) {
    // Stays up until the server says yes: the sharing status re-renders this card with the result.
    const code = ($("#homeShareCode")?.value || "").trim();
    if (!code) { homeShareNote = "Type the invite code first."; return renderHomeShare(); }
    post("share", { action: "on", history: "all", enroll: code });
    return;
  }
  if (what === "share") { post("share", { action: "on", history: "all" }); data.settings.shareEnabled = true; }
  if (what === "beta") { post("setting", { key: "beta", value: true }); data.settings.beta = true; return renderHomeShare(); }
  post("setting", { key: "shareIntroSeen", value: true });
  data.settings.shareIntroSeen = true;
  renderHomeShare();
});

/** Home's race chart, while releveling: you against your best and the middle half of your relevels. */
function drawRace() {
  const host = $("#raceChart");
  if (!host || !data?.reinc || page !== "home" || homeModeNow !== "relevel") return;
  const act = activeJourney();
  if (!act) return;
  drawGhost(host, { race: data.reinc.journeyRaces.now, you: act.curve, proj: act.projection, height: 300 });
}
