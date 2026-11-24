"use strict";
// ---------------------------------------------------------------- ask
// The question lives here; answers come back from the window ({kind: "ask"}). Only what you changed is kept, so a question
// you never touched (the levels, counting motes) keeps following your level and your Goal.
let askState = (() => { try { return JSON.parse(store.get("askState", "null")) || { q: "level" }; } catch { return { q: "level" }; } })();
let askAnswer = null, askFocus = null;
const ASK_COLORS = ["#f0c860", "#5b9cf0", "#8fd18f", "#c79bf2", "#e8907e", "#7fd1c7"];
const ASK_TABS = [["level", "Level from A to B"], ["farm", "Mote farm at my level"], ["diff", "Which difficulty"], ["dmg", "Is my damage normal"], ["places", "Every place"]];
const fmtMin = m => { m = Math.round(m); return m < 60 ? `${m} min` : `${Math.floor(m / 60)} h ${String(m % 60).padStart(2, "0")}`; };
const bandText = b => b.replace("-", "–");
function ask(changes) {
  Object.assign(askState, changes || {});
  store.set("askState", JSON.stringify(askState));
  post("ask", askState);
}
const stepper = (key, v, what) => `<span class="stepper"><button data-ask-step="${key}" data-d="-1" aria-label="${what} one level lower">−</button><span class="v num">${v}</span><button data-ask-step="${key}" data-d="1" aria-label="${what} one level higher">+</button></span>`;
const seg2 = (attr, cur, opts, label) => `<div class="seggroup" role="group" aria-label="${esc(label)}">${opts.map(([v, t]) =>
  `<button class="${v === cur ? "on" : ""}" aria-pressed="${v === cur}" data-${attr}="${v}">${esc(t)}</button>`).join("")}</div>`;
const pres = (attr, items, isOn) => `<div class="chips" style="margin-top: 10px; align-items: center"><span class="ak" style="margin-right: 2px">Try</span>${items.map(([v, t]) =>
  `<button class="askpre num${isOn(v) ? " on" : ""}" aria-pressed="${isOn(v)}" data-${attr}="${v}">${esc(t)}</button>`).join("")}</div>`;

function askQuestion(a) {
  const r = a.route, f = a.farm;
  if (a.q === "places") {
    const m = metricNow();
    return `
    <div class="askq"><span>Every place you've leveled, ranked by</span>${seg2("ask-metric", m, Object.entries(GOAL_NAMES), "Rank by")}
      <span class="grow"></span>${seg2("ask-scope", a.places?.scope ?? f.scope, [["patch", "Since the 11-10 patch"], ["all", "All time"]], "Which visits")}</div>
    <div class="ak" style="margin-top: 8px; line-height: 1.5">${esc(METRICS[m].help)}${m === "balanced" ? " " + farmLine(true) : ""}</div>`;
  }
  if (a.q === "level") return `
    <div class="askq"><span>Where to level from</span>${stepper("from", r.from, "Start")}<span>to</span>${stepper("to", r.to, "End")}
      <span class="grow"></span>${seg2("ask-goal", r.goal, [["fast", "Fastest"], ["motes", "Count motes too"]], "What counts")}</div>
    ${pres("ask-range", [["1,50", "1–50"], ["1,20", "1–20"], ["20,30", "20–30"], ["30,40", "30–40"], ["40,50", "40–50"], ["46,50", "46–50"]], v => v === `${r.from},${r.to}`)}`;
  if (a.q === "farm" || a.q === "dmg") return `
    <div class="askq"><span>${a.q === "farm" ? "Best mote farm at level" : "Is my damage normal at level"}</span>${stepper("at", f.at, "Level")}
      ${f.band === "50" ? "" : `<span class="ak" style="font-size: 12.5px">counts levels ${bandText(f.band)}</span>`}<span class="grow"></span>
      ${a.q === "farm" ? seg2("ask-scope", f.scope, [["patch", "Since the 11-10 patch"], ["all", "All time"]], "Which crawls") : ""}</div>
    ${pres("ask-at", [10, 20, 30, 40, 46, 50].map(v => [String(v), String(v)]), v => v === String(f.at))}`;
  const d = a.difficulty, z = d.pick ?? d.community;   // one zone per answer: yours, or theirs when you have none
  return `
    <div class="askq"><span>${z ? `Which difficulty is worth it in <span class="gold" style="font-weight: 650">${esc(z.zone)} at levels ${z.from}–${z.to}</span>?` : "Which difficulty is worth it?"}</span></div>
    <div class="chips" style="margin-top: 10px">${d.options.map(o => `<button class="askpre${z && o.key === z.key ? " on" : ""}" aria-pressed="${!!(z && o.key === z.key)}" data-ask-diff="${esc(o.key)}">${esc(o.label)}</button>`).join("")}</div>`;
}

// Where and who with: every question can narrow to instances or the open world, and to solo or grouped play.
const askFilters = a => `<div class="row" style="flex-wrap: wrap; gap: 10px; margin-top: 12px">
    ${seg2("ask-where", a.where, [["all", "Everywhere"], ["instances", "Instances"], ["open", "Open world"]], "Where")}
    ${seg2("ask-with", a.with, [["either", "Solo or grouped"], ["solo", "Solo"], ["group", "In a group"]], "Who with")}
  </div>`;

// Solo and grouped side by side, always: this is a social game, so the grouped answer shows even when it's empty.
function askCompare(a) {
  const r = a.route, span = r.to - r.from;
  const tile = (label, s, empty, on) => `<div class="dcol${on ? " win" : ""}" style="flex: 1 1 240px; min-width: 0">
      <div class="ak">${label}</div>
      ${s ? `<div class="num" style="font-size: 20px; font-weight: 650">≈ ${fmt(s.minutes / 60)} h${s.covered < span ? ` <span class="ak">for ${s.covered} of ${span} levels</span>` : ""}</div>
        <div class="ak" style="margin-top: 2px; line-height: 1.45">${esc(s.places.join(" → "))}</div>`
      : `<div class="muted" style="font-size: 12.5px; margin-top: 4px; line-height: 1.45">${empty}</div>`}
    </div>`;
  const pool = r.community;
  const poolTile = a.community || a.communityGap ? `<div class="dcol" style="flex: 1 1 240px; min-width: 0">
      <div class="ak">Players who share <span class="dim">· ${esc(poolSlice(a))}</span></div>
      ${pool ? `<div class="num" style="font-size: 20px; font-weight: 650">≈ ${fmt(pool.minutes / 60)} h${pool.covered < span ? ` <span class="ak">for ${pool.covered} of ${span} levels</span>` : ""}</div>
        <div class="ak" style="margin-top: 2px; line-height: 1.45">${esc(pool.places.join(" → "))} · ${plural(Math.min(...pool.legs.map(l => l.players)), "player")}</div>`
      : `<div class="muted" style="font-size: 12.5px; margin-top: 4px; line-height: 1.45">${a.communityGap ? `Shared numbers don't cover ${esc(a.communityGap)} yet.` : "Not enough players at these levels yet."}</div>`}
    </div>` : "";
  return `<div class="row" style="gap: 12px; flex-wrap: wrap; align-items: stretch">
    ${tile("Solo", r.solo, "No solo play at these levels in your logs.", a.with === "solo")}
    ${tile("In a group", r.group, "No grouped play at these levels in your logs yet. Other players' grouped runs will answer this once they share.", a.with === "group")}
    ${poolTile}
  </div>`;
}

/** Time a level against theirs: "17% more" or "26% less" (time, not speed: 26% less time is 35% faster). */
const vsWords = g => `${Math.round(Math.abs(g) * 100)}% ${g >= 0 ? "more" : "less"}`;

// Where a leg's time comes from: your logs, the players who share, or both (yours counting more the more visits you have).
// The first leg that's both carries the (i) for how they combine.
function legSource(l, tip) {
  const kind = l.openWorld ? (l.crawls === 1 ? "hunt" : "hunts") : (l.crawls === 1 ? "crawl" : "crawls");
  if (l.mineShare <= 0) return `<div class="ak">Levels ${l.from}–${l.to} · <span class="gold">new to you</span>: ${plural(l.typicalPlayers, "player")} who share ${l.typicalPlayers === 1 ? "has" : "have"} leveled here</div>`;
  const yours = `<div class="ak">Levels ${l.from}–${l.to} · up to ${l.crawls} ${kind} a level${l.lastRun ? ` · last run ${l.lastRun.slice(5)}` : ""}${l.beforePatch ? ` <span class="warn">· before the patch</span>` : ""}</div>`;
  if (l.typical == null) return yours;
  if (l.mine == null) return yours + `<div class="ak">Players who share: <b class="num soft">${fmtMin(l.typical)}</b> here (${plural(l.typicalPlayers, "player")}). You haven't played every level here, so some are theirs alone.</div>`;
  const g = l.mine / l.typical - 1;
  return yours + `<div class="ak">You <b class="num soft">${fmtMin(l.mine)}</b> · players who share <b class="num soft">${fmtMin(l.typical)}</b> (${plural(l.typicalPlayers, "player")})
    · <span class="${l.tone}">${vsWords(g)} time</span> · the time above is ${Math.round(l.mineShare * 100)}% yours${tip ? infoTip("blend") : ""}</div>`;
}

// You against the players who share, place by place, at the levels you've both played: where you're slower, and why.
// Levels an hour = kill speed × XP per kill, so the gap splits exactly into the two (docs/pace.md).
function askVersus(a) {
  const v = a.route.versus ?? [];
  if (!a.community || !v.length) return "";
  const gap = x => x.gap, all = a.route.versusGap;
  // The engine says which part of the gap it is (Ask.Versus.Why) and by how much; the page words it.
  const why = x => {
    if (!x.why) return "";
    const k = `${fmt(x.killsMine)} kills a minute against their ${fmt(x.killsTheirs)}`;
    if (x.why === "both") return `Both: you kill ${x.killPercent}% slower (${k}), and each kill fills ${x.perKillPercent}% less of a level.`;
    if (x.why === "kill") return `Kill speed: ${k}.`;
    return `XP per kill: you kill as fast as they do (${fmt(x.killsMine)} a minute), but each kill fills ${x.perKillPercent}% less of a level.`;
  };
  const slow = v.filter(x => x.slower);
  const head = a.route.versusSame
    ? `About the same time a level as players who share (${vsWords(all)}) at the ${plural(v.length, "place")} you've both played at these levels.`
    : `You take ${vsWords(all)} time a level than players who share at the ${plural(v.length, "place")} you've both played at these levels.`;
  return `<div class="lbl" style="margin-top: 6px">You against players who share${infoTip("versus")}</div>
    <div class="soft" style="font-size: 13px; line-height: 1.5">${esc(head)} ${slow.length ? `Slower at ${esc(slow.map(x => x.place).join(", "))}.` : "Nowhere clearly slower."}</div>
    <div>${v.map(x => {
      const g = gap(x), w = why(x);
      return `
      <div class="askleg">
        <span class="dot" style="background: var(--${x.tone === "soft" ? "line2" : x.tone})"></span>
        <div style="min-width: 0"><div style="font-weight: 650; font-size: 14px">${esc(x.place)}</div>
          <div class="ak">Levels ${x.from}–${x.to} · your ${plural(x.crawls, x.place.endsWith(" · open world") ? "hunt" : "crawl")} · ${plural(x.players, "player")} who share</div>
          ${w ? `<div class="ak" style="color: var(--muted)">${esc(w)}</div>` : ""}</div>
        <div><div class="num" style="font-weight: 650; font-size: 14px">${fmt(x.mine)}</div><div class="ak">your min a level</div></div>
        <div><div class="num" style="font-size: 14px">${fmt(x.theirs)}</div><div class="ak">theirs</div></div>
        <div><div class="num ${x.tone}" style="font-size: 14px">${vsWords(g)}</div><div class="ak">time than theirs</div></div>
      </div>`;
    }).join("")}</div>
    <div class="ak" style="line-height: 1.5; max-width: 780px">Levels an hour = kill speed × XP per kill. Kill speed is yours to change: gear, your trio, how much you pull. XP per kill is what you kill and who you share it with: grouped kills split the XP, so set Who with to Solo to compare solo play with solo play. Their trios and gear differ from yours, so this is a comparison, not a verdict.${a.community.leftYouOut ? " Your own shared runs are left out of theirs." : ""}</div>`;
}

function askLevel(a) {
  const r = a.route, span = r.to - r.from, pct = L => (L - r.from) / span * 100;
  if (!r.legs.length) return `<div class="h2">No crawls between levels ${r.from} and ${r.to}</div>
    <div class="muted">${a.community ? "Neither your logs nor the players who share have these levels yet." : "Your logs have nothing at these levels. Once other players share, their places show here."}</div>`;
  const blended = r.legs.some(l => l.typical != null || l.mineShare < 1);
  const blendAt = r.legs.findIndex(l => l.mineShare > 0 && l.typical != null && l.mine != null);
  const ticks = [r.from];
  r.legs.forEach(l => { if (!ticks.includes(l.to) && l.to - ticks[ticks.length - 1] >= 2) ticks.push(l.to); });
  if (!ticks.includes(r.to)) ticks.push(r.to);
  const same = r.fastest.places.join("|") === r.withMotes.places.join("|");
  const note = r.goal === "motes"
    ? `Counting motes: a place gets credit for the farming time its motes save you. ` +
      (same ? `Same route as the fastest, looting about ${Math.round(r.t5)} T5-eq on the way.`
            : `You'd loot about ${Math.round(r.withMotes.t5)} T5-eq on the way. The fastest route takes ${fmt(r.fastest.minutes / 60)} h and loots about ${Math.round(r.fastest.t5)}.`)
    : `The quickest place at each level ${blended ? "from your logs and the players who share, yours counting more the more visits you have" : "in your logs"}, with ${r.switchMinutes} minutes for each change of place. You'd loot about ${Math.round(r.t5)} T5-eq on the way` +
      (same ? "." : `; counting motes, the route changes (${fmt(r.withMotes.minutes / 60)} h, about ${Math.round(r.withMotes.t5)} T5-eq).`);
  // The hours cover only the levels someone has numbers for: say how many, and which have none, before anything else.
  const covered = span - r.gaps.length;
  const missing = r.gaps.length ? `No numbers ${gapWords(r.gaps)}: ${a.community ? "neither your logs nor the players who share have them" : "your logs have no visit with a rate there, and other players' numbers haven't arrived yet"}, so the hours leave ${r.gaps.length === 1 ? "that level" : "those levels"} out.` : "";
  return `
    <div class="row" style="align-items: baseline; flex-wrap: wrap; gap: 12px"><span class="ask-big num">≈ ${fmt(r.minutes / 60)} h${infoTip("askEstimate")}</span>
      ${r.gaps.length ? `<span class="num warn" style="font-size: 18px; font-weight: 650">for ${covered} of ${span} levels</span>` : ""}
      <span style="font-size: 16px; font-weight: 600">${r.legs.map(l => esc(l.place)).join(" → ")}</span></div>
    ${missing ? `<div class="warn" style="font-size: 13px; line-height: 1.45">${esc(missing)}</div>` : ""}
    <div class="muted" style="line-height: 1.5; max-width: 780px">${esc(note)}</div>
    ${r.goal === "motes" ? `<div class="ak" style="font-size: 12.5px; line-height: 1.5">${farmRateNote()}</div>` : ""}
    ${a.rank > 0 ? `<div class="ak" style="font-size: 12.5px">Times are for the XP ${esc(a.trio || "your trio")} needs now: +${a.xpExtra}% for ${a.rank} ${a.rank === 1 ? "reincarnation" : "reincarnations"} of its most-reincarnated class.${infoTip("xp")}</div>`
      : ranksUnconfirmed() ? countsNudge(data.reinc, `Times assume ${esc(a.trio || "your trio")} has never reincarnated: no extra XP to level.`, "margin: 0") : ""}
    ${askCompare(a)}
    <div class="askbar" role="img" aria-label="${esc(`Route from level ${r.from} to ${r.to}: ` + r.legs.map(l => `${l.place} ${l.from} to ${l.to}`).join(", "))}">
      <div class="track"></div>
      ${r.legs.map((l, i) => `<div class="leg" title="${esc(l.place)}, levels ${l.from}–${l.to}" style="left: calc(${pct(l.from)}% + 1px); width: calc(${pct(l.to) - pct(l.from)}% - 2px); background: ${ASK_COLORS[i % ASK_COLORS.length]}"></div>`).join("")}
      ${r.gaps.map(L => `<div class="gap" title="Level ${L} to ${L + 1}: no numbers" style="left: calc(${pct(L)}% + 1px); width: calc(${100 / span}% - 2px)"></div>`).join("")}
      ${ticks.map(L => `<span class="t num" style="left: ${pct(L)}%; transform: translateX(${L === r.from ? "0" : L === r.to ? "-100%" : "-50%"})">${L}</span>`).join("")}
    </div>
    <div>${r.legs.map((l, i) => `
      <div class="askleg">
        <span class="dot" style="background: ${ASK_COLORS[i % ASK_COLORS.length]}"></span>
        <div style="min-width: 0"><div style="font-weight: 650; font-size: 14px">${esc(l.place)}</div>
          ${legSource(l, i === blendAt)}</div>
        <div><div class="num" style="font-weight: 650; font-size: 14px">${fmtMin(l.minutes)}</div><div class="ak">time</div></div>
        <div><div class="num" style="font-size: 14px">${fmt(l.perLevel)}</div><div class="ak">min a level</div></div>
        <div><div class="num" style="font-size: 14px">${Math.round(l.t5PerHour)}</div><div class="ak">T5-eq/h</div></div>
      </div>`).join("")}</div>
    ${askVersus(a)}
    ${r.alternatives.length ? `<div class="lbl" style="margin-top: 6px">Other ways</div>
    <div>${r.alternatives.map(x => `
      <div class="row" style="padding: 6px 0; border-bottom: 1px solid #1f2731; align-items: baseline">
        <span style="flex: 1; min-width: 0">${esc(x.place)}${x.whole ? " the whole way" : ""}</span>
        <span class="num soft">${x.whole ? fmtMin(x.minutes) : `only levels ${x.from}–${x.to} in your logs`}</span>
        <span class="num dim" style="width: 70px; text-align: right">${x.whole ? (x.minutes >= r.minutes ? "+" + fmtMin(x.minutes - r.minutes) : "−" + fmtMin(r.minutes - x.minutes)) : ""}</span>
      </div>`).join("")}</div>` : ""}`;
}

// A farm table: yours (crawls and hours under each place) or the players who share (how many players too).
function farmTable(rows, shared) {
  const max = rows.length ? rows[0].t5PerHour : 1;
  return `<div class="askrow askhead farmgrid"><span></span><span>Place</span><span></span><span style="text-align: right">T5-eq/h</span><span style="text-align: right">above T5</span><span style="text-align: right">fragments/h</span><span style="text-align: right">deaths/h</span></div>
    ${rows.map((x, i) => `
      <div class="askrow farmgrid">
        <span class="num dim" style="font-weight: 700">#${i + 1}</span>
        <div style="min-width: 0"><div style="font-weight: 650">${esc(x.place)}</div><div class="ak">${shared ? plural(x.players, "player") + " · " : ""}${x.crawls} crawls · ${fmt(x.hours)} h</div></div>
        <div class="bar"><i style="width: ${x.t5PerHour / max * 100}%; background: ${i === 0 && !shared ? "var(--gold)" : shared ? "var(--blue)" : "#5a6572"}"></i></div>
        <span class="num" style="text-align: right; font-weight: 650">${fmt(x.t5PerHour)}</span>
        <span class="num" style="text-align: right">${fmt(x.highTierPerHour)}</span>
        <span class="num" style="text-align: right">${fmt(x.fragmentsPerHour)}</span>
        <span class="num" style="text-align: right">${x.deathsPerHour ? fmt(x.deathsPerHour, 2) : "0"}</span>
      </div>`).join("")}`;
}

function askFarm(a) {
  const f = a.farm, rows = f.rows, where = f.band === "50" ? "50" : `levels ${bandText(f.band)}`;
  // No shared farm has enough players since the patch: theirs from all time stand in, said so.
  const allTime = !f.community?.length && f.communityAllTime?.length, pool = allTime ? f.communityAllTime : f.community ?? [];
  const when = f.scope === "patch" && !allTime ? "Since the 11-10 patch" : "All time (before the 11-10 patch there were no fragments, so older rates run lower)";
  if (!rows.length) return `
    <div class="h2" style="font-size: 22px">${f.crawls || f.unrated ? "Too little farming" : "No farming"} at ${where}${f.scope === "patch" ? " since the patch" : ""} in your logs${f.crawls || f.unrated ? " to rank a place" : ""}</div>
    <div class="muted" style="line-height: 1.55; max-width: 720px">${f.crawls ? `Your logs have ${plural(f.crawls, "crawl")} with a rate there, but no place has ${f.minCrawls} of them and ${fmt(f.minHours * 60, 0)} minutes yet; a place ranks once it does. ` : f.unrated ? `Your logs have ${plural(f.unrated, "crawl")} there, none with a rate yet: a rate starts at ${rateFromKills() ?? "enough"} kills, or when the crawl is finished. ` : ""}${f.scope === "patch" ? "Fragments arrived with the 11-10 patch, so rates from before it undercount what a farm gives now." : ""}
      ${f.before ? `Before the patch your best was ${esc(f.before.place)}: ${fmt(f.before.t5PerHour)} T5-eq/h over ${f.before.crawls} crawls.` : ""}</div>
    ${pool.length ? `<div class="lbl" style="margin-top: 6px">Players who share${allTime ? ` <span class="dim" style="font-weight: 400">· all time: none has enough players since the patch yet</span>` : ""}</div>
      <div class="row" style="align-items: baseline; flex-wrap: wrap; gap: 12px"><span class="ask-big num">${fmt(pool[0].t5PerHour)}</span>
        <span class="muted" style="font-size: 15px">T5-eq/h</span><span style="font-size: 16px; font-weight: 600">${esc(pool[0].place)}</span></div>
      <div class="muted" style="line-height: 1.5">${when}, at ${where}. Fragments count at your value from Settings${infoTip("fragment")}; Reward Chest loot included.</div>
      ${farmTable(pool, true)}`
    : `<div class="waiting">${ASK_PEOPLE}<div><div style="font-weight: 650">Other players answer this one</div>
      <div class="ak" style="font-size: 12.5px; margin-top: 2px">It shows here once ${a.community ? plural(a.community.minPlayers, "player") : "enough players"} have farmed at this level${f.scope === "patch" ? " since the patch" : ""}.</div></div></div>`}`;
  return `
    <div class="row" style="align-items: baseline; flex-wrap: wrap; gap: 12px"><span class="ask-big num">${fmt(rows[0].t5PerHour)}</span>
      <span class="muted" style="font-size: 15px">T5-eq/h</span><span style="font-size: 16px; font-weight: 600">${esc(rows[0].place)}</span></div>
    <div class="muted" style="line-height: 1.5">${when}, at ${where}. Fragments count at your fragment value; Reward Chest loot included.</div>
    ${farmTable(rows, false)}
    ${pool.length ? `<div class="lbl" style="margin-top: 10px">Players who share <span class="dim" style="font-weight: 400">· ${allTime ? "all time: none has enough players since the patch yet · " : ""}fragments at your value${infoTip("fragment")}</span></div>${farmTable(pool, true)}` : ""}`;
}

function askDiff(a) {
  const p = a.difficulty.pick, pool = a.difficulty.community;
  if (!p && !pool) return `<div class="h2">No zone in your logs has two difficulties at the same levels yet</div>`;
  // One zone: yours on top, theirs below, each saying when it has none there.
  const z = p ?? pool, none = t => `<div class="muted" style="font-size: 12.5px; margin-top: 4px; line-height: 1.45">${t}</div>`;
  const label = `<div class="lbl" style="margin-top: 10px">Players who share · ${esc(z.zone)} ${z.from}–${z.to}</div>`;
  const theirs = pool ? label + diffBlock(pool, true)
    : a.communityGap ? label + none(`Shared numbers don't cover ${esc(a.communityGap)} yet.`)
    : a.community ? label + none("Not enough players who share have played two difficulties here yet.") : "";
  return (p ? diffBlock(p, false) : none("Your logs don't have two difficulties here yet.")) + theirs;
}

// One zone's difficulties side by side: yours, or the players who share (with how many players stand behind each).
function diffBlock(p, shared) {
  const cols = p.columns, maxL = Math.max(...cols.map(c => c.levelsPerHour)), maxT = Math.max(...cols.map(c => c.t5PerHour));
  const fast = cols.find(c => c.levelsPerHour === maxL), rich = cols.find(c => c.t5PerHour === maxT);
  const thin = cols.filter(c => c.crawls === 1).map(c => "D" + c.d);
  const head = fast === rich ? `D${fast.d} wins on both` : `D${fast.d} for speed, D${rich.d} for motes`;
  const note = (fast === rich ? `D${fast.d} is both the fastest and the best for motes here.`
    : `D${fast.d} levels ${fmt(fast.levelsPerHour / rich.levelsPerHour)}× faster than D${rich.d}; D${rich.d} earns ${fmt(rich.t5PerHour / Math.max(0.1, fast.t5PerHour))}× the motes.`) +
    (thin.length ? ` ${thin.join(" and ")} ${thin.length === 1 ? "rests" : "rest"} on one crawl.` : "");
  return `
    <div class="h2" style="font-size: 20px">${head}</div>
    <div class="muted" style="line-height: 1.5">${esc(note)}</div>
    <div class="dcols">${cols.map(c => {
      const badges = [c === fast ? "Fastest" : "", c === rich ? "Most motes" : ""].filter(Boolean);
      return `<div class="dcol${badges.length ? " win" : ""}">
        <div class="between" style="align-items: center"><span style="font-size: 20px; font-weight: 700">D${c.d}</span><span class="ak">${shared ? plural(c.players, "player") + " · " : ""}${c.crawls} ${c.crawls === 1 ? "crawl" : "crawls"}</span></div>
        <div style="min-height: 22px; margin-top: 4px">${badges.length ? `<span class="chip gold">${badges.join(" · ")}</span>` : ""}</div>
        <div class="ak" style="margin-top: 8px">Levels an hour</div><div class="num" style="font-size: 18px; font-weight: 650">${fmt(c.levelsPerHour)}</div>
        <div class="slim"><i style="width: ${c.levelsPerHour / maxL * 100}%; background: var(--blue)"></i></div>
        <div class="ak" style="margin-top: 10px">T5-eq an hour</div><div class="num" style="font-size: 18px; font-weight: 650">${fmt(c.t5PerHour)}</div>
        <div class="slim"><i style="width: ${maxT > 0 ? c.t5PerHour / maxT * 100 : 0}%; background: var(--gold)"></i></div>
        <div class="between ak" style="margin-top: 10px"><span>deaths/h <b class="num soft">${c.deathsPerHour ? fmt(c.deathsPerHour, 2) : "0"}</b></span><span>kills/min <b class="num soft">${fmt(c.killsPerMinute)}</b></span></div>
      </div>`;
    }).join("")}</div>`;
}

function askDamage(a) {
  const m = a.damage, rows = m.rows, max = rows.length ? Math.max(...rows.map(x => Math.max(x.dps, x.typical ?? 0))) : 1;
  const any = rows.some(x => x.typical != null);
  const head = any ? "Your damage beside the players who share" : "Not answerable from one player's logs yet";
  const note = any
    ? `Damage per active second at levels ${bandText(m.band)}. Typical is the players who share in the same place and levels, every trio together: classes aren't matched yet, so a gap can be the trio as much as the play.`
    : `"Normal" needs other players at your level with your classes in the same place. Until enough share, here is your own damage per active second at levels ${bandText(m.band)}.`;
  const none = a.community ? "too few players" : "needs players who share";
  return `
    <div class="h2" style="font-size: 20px">${head}</div>
    <div class="muted" style="line-height: 1.5; max-width: 780px">${esc(note)}</div>
    ${rows.length ? `<div class="askrow askhead dmggrid"><span>Place</span><span></span><span style="text-align: right">Yours</span><span style="text-align: right">Typical</span></div>
    ${rows.map(x => `
      <div class="askrow dmggrid">
        <div style="min-width: 0"><div style="font-weight: 650">${esc(x.place)}</div><div class="ak">${x.crawls} crawls${x.typical != null ? ` · typical from ${plural(x.typicalPlayers, "player")}` : ""}</div></div>
        <div class="bar" style="position: relative"><i style="width: ${x.dps / max * 100}%; background: var(--blue)"></i>${x.typical != null ? `<b title="Typical" style="position: absolute; top: 0; bottom: 0; left: calc(${x.typical / max * 100}% - 2px); width: 2px; background: var(--gold)"></b>` : ""}</div>
        <span class="num" style="text-align: right; font-weight: 650">${Math.round(x.dps)}</span>
        ${x.typical != null ? `<span class="num soft" style="text-align: right">${Math.round(x.typical)}</span>` : `<span class="dim" style="text-align: right">${none}</span>`}
      </div>`).join("")}` : `<div class="dim">No crawls in your logs at these levels.</div>`}`;
}

const ASK_PEOPLE = `<svg width="34" height="34" viewBox="0 0 24 24" fill="none" stroke="#8d959e" stroke-width="1.5" stroke-linecap="round" aria-hidden="true" style="flex: none"><circle cx="8" cy="9" r="3"/><circle cx="16.5" cy="9.5" r="2.5"/><path d="M3 19c.6-3 2.6-4.5 5-4.5s4.4 1.5 5 4.5M13.5 15.2c.9-.5 1.9-.7 3-.7 2.2 0 3.8 1.4 4.3 4.5"/></svg>`;
// Every place, level by level, from the same per-level table as the route (each level read with the ones beside it), at your
// XP penalty (docs/pace.md): places down the side and 12 levels across, levels/h in each cell, with your level, Ask's route,
// the ★ by your ranking and Fading marked; why levels/h falls at the place you pick; then every place at the level you pick
// and your runs there, as they happened.
let placeLevel = null, placeSel = null, gridStart = null;
const GRID_COLS = 12;

function placeGrid(a, levels, cols) {
  const lvl = a.level, legs = a.route?.legs ?? [];
  const first = new Map();
  for (const L of cols) for (const o of levels[L - 1].rows) if (!first.has(o.place)) first.set(o.place, L);
  const places = [...first.keys()].sort((p, q) => first.get(p) - first.get(q) || p.localeCompare(q));
  if (!places.length) return `<div class="dim" style="margin-top: 10px">Nothing played at levels ${cols[0]}–${cols.at(-1)}.</div>`;
  const vals = cols.flatMap(L => levels[L - 1].rows.map(o => o.levelsPerHour)), lo = Math.min(...vals), hi = Math.max(...vals);
  const alpha = v => hi > lo ? .08 + .5 * (v - lo) / (hi - lo) : .3;
  const onRoute = (p, L) => legs.some(l => l.place === p && L >= l.from && L < l.to);
  let h = `<div class="pgrid" role="grid" aria-label="Levels per hour by place and level"><span></span>${cols.map(L =>
    `<span class="gh${L === lvl ? " you" : ""}${L === placeLevel ? " at" : ""}">${L}${L === lvl ? " you" : ""}</span>`).join("")}`;
  for (const p of places) {
    h += `<span class="pl${p === placeSel ? " sel" : ""}" title="${esc(p)}">${esc(p)}</span>`;
    for (const L of cols) {
      const lv = levels[L - 1], o = lv.rows.find(r => r.place === p);
      if (!o) { h += `<span class="gc e" aria-hidden="true">·</span>`; continue; }
      const star = lv.star?.place === p, route = onRoute(p, L);
      const cls = ["gc", o.exact ? "" : "near", o.enough ? "" : "hint", route ? "route" : "", o.fading ? "fd" : "", L === placeLevel ? "atcol" : "",
        L === placeLevel && p === placeSel ? "sel" : ""].filter(Boolean).join(" ");
      const tip = `${p} at ${L}: ${fmt(o.levelsPerHour)} levels/h${o.exact ? "" : ", from the levels beside it"} · ${plural(o.crawls, "visit")}`
        + `${star ? " · ★ best by your ranking" : ""}${route ? " · on Ask's route" : ""}${o.fading ? " · Fading" : ""}`;
      h += `<button class="${cls}" style="background: rgba(240,200,96,${alpha(o.levelsPerHour).toFixed(2)})" data-cell-level="${L}" data-cell-place="${esc(p)}"
        title="${esc(tip)}" aria-label="${esc(tip)}">${fmt(o.levelsPerHour)}${star ? '<span class="st">★</span>' : ""}</button>`;
    }
  }
  return h + `</div><div class="ak pglegend"><span><i class="sw route"></i>Ask's route</span><span><i class="sw fd"></i>Fading: your best 3 levels back, now clearly passed</span>
    <span><span class="gold">★</span> best by your ranking</span><span><i>3.8</i> from the levels beside it</span><span>faint = under 3 visits</span>
    ${cols[0] < 46 && cols.at(-1) >= 46 ? "<span>Planes open at 46</span>" : ""}</div>`;
}

/** Ask's route from here, under the grid. */
function routeBox(a) {
  const r = a.route, legs = r?.legs ?? [];
  if (!legs.length) return "";
  const thin = legs.filter(l => l.crawls <= 1).map(l => l.place);
  return `<div class="routebox"><div class="lbl" style="color: var(--gold)">${r.goal === "motes" ? "Best counting motes" : "Fastest"} from ${r.from} to ${r.to} · ≈${fmt(r.minutes / 60)} h${r.gaps?.length ? ` for ${r.to - r.from - r.gaps.length} of ${r.to - r.from} levels` : ""}</div>
    ${r.gaps?.length ? `<div class="ak warn" style="margin-top: 3px">No numbers ${esc(gapWords(r.gaps))}.</div>` : ""}
    <div style="margin-top: 5px; line-height: 1.6">${legs.map(l => `${esc(l.place)} ${l.from}–${l.to}${l.perLevel > 0 ? ` <span class="ak">${fmt(60 / l.perLevel)} levels/h</span>` : ""}`).join(" → ")}</div>
    ${thin.length ? `<div class="ak" style="margin-top: 4px">${esc(thin.join(", "))} ${thin.length === 1 ? "rests" : "rest"} on one run at a level.</div>` : ""}</div>`;
}

/** Why levels/h falls at the picked place: kill speed and XP per kill by level, from your runs there as they happened. */
function whyPanel(a, P, levels) {
  const p = placeSel;
  if (!p) return "";
  let runs = P.map.filter(c => c.place === p && c.killsPerMinute > 0 && c.minutes >= 5);
  const mine = runs.filter(c => c.trio && c.trio === a.trio), own = mine.length >= 3;
  if (own) runs = mine;
  runs = runs.slice().sort((x, y) => new Date(y.start) - new Date(x.start)).slice(0, 8).sort((x, y) => x.level - y.level || x.gained - y.gained);
  const head = `<div class="lbl">Why levels/h falls at ${esc(p)}</div>`;
  if (runs.length < 2) return `<aside class="whypanel">${head}<div class="ak" style="margin-top: 6px">Under 2 runs here to show it.</div></aside>`;
  // The percent a level levels/h changes at this place, from every run here at rank 0: the engine's (Ask.Falls).
  const per = P.falls?.[p] ?? null;
  const kMax = Math.max(...runs.map(c => c.killsPerMinute)), xMax = Math.max(...runs.map(c => c.xpPerKill), 0.01);
  const bars = (f, max, color, show) => runs.map(c => `<div class="mini2"><span class="ak num">${c.level}</span>
    <span class="mb" style="width: ${(f(c) / max * 100).toFixed(0)}%; background: ${color}"></span><span class="num" style="text-align: right">${show(f(c))}</span></div>`).join("");
  const fa = runs[0], fz = runs.at(-1);
  const fading = levels[placeLevel - 1]?.rows.find(o => o.place === p)?.fading;
  const verdict = per == null ? "" : per > -2 ? `Levels/h holds here (${per >= 0 ? "+" : "−"}${fmt(Math.abs(per), 0)}% a level): this place keeps its value.`
    : `About ${fmt(-per, 0)}% fewer levels an hour each level: ${per >= -9 ? "about what every place does (4–7%)" : "faster than most places (4–7%)"}.`;
  return `<aside class="whypanel" aria-label="Why levels/h falls at ${esc(p)}">${head}
    <div class="ak" style="margin-top: 4px; line-height: 1.5">${own ? `Your ${esc(a.trio)} runs here` : "Your runs here"}, as they happened:
      kills got ${fz.killsPerMinute >= fa.killsPerMinute ? "quicker" : "slower"} (${fmt(fa.killsPerMinute)} → ${fmt(fz.killsPerMinute)} a minute) and each gave
      ${fz.xpPerKill <= fa.xpPerKill ? "less" : "more"} (${fmt(fa.xpPerKill, 2)}% → ${fmt(fz.xpPerKill, 2)}%).</div>
    <div class="ak" style="margin-top: 8px">Kill speed (kills a minute)</div>${bars(c => c.killsPerMinute, kMax, "var(--good)", v => fmt(v, 2))}
    <div class="ak" style="margin-top: 8px">XP per kill</div>${bars(c => c.xpPerKill, xMax, "#d9b86a", v => fmt(v, 2) + "%")}
    <div class="ak" style="margin-top: 6px">XP per kill is the share of a level a kill fills${own ? "" : ", at each run's own XP to level: a run that needed more XP shows a smaller % for the same kill, because its bar was bigger"}.</div>
    ${verdict || fading ? `<div class="whyfoot">${verdict}${fading ? ` <span class="chip amber">Fading</span> at ${placeLevel}: another place clearly beats it.` : verdict ? " It isn't Fading." : ""}</div>` : ""}
  </aside>`;
}

/** Your runs at the picked place within a level of the picked level, as they happened. */
function runsList(P) {
  if (!placeSel) return "";
  const rs = P.map.filter(c => c.place === placeSel && Math.abs(c.level - placeLevel) <= 1).sort((x, y) => new Date(y.start) - new Date(x.start));
  if (!rs.length) return `<div class="ak" style="margin-top: 12px">No runs at ${esc(placeSel)} within a level of ${placeLevel}.</div>`;
  return `<div style="margin-top: 16px; font-weight: 650">Your runs at ${esc(placeSel)} near ${placeLevel}, as they happened</div>
    <div class="tablewrap" style="margin-top: 8px"><table>
    <thead><tr><th class="l">When</th><th class="l">Trio</th><th title="The reincarnation penalty: how much more XP each level needed. Mobs give the same XP; the bar is bigger.">XP to level</th><th>Levels</th><th>Min</th><th>Levels/h</th><th>Kills/min</th><th>XP per kill</th></tr></thead>
    <tbody>${rs.slice(0, 12).map(c => `<tr><td class="l">${dt(c.start)}</td><td class="l">${esc(c.trio ?? "?")}</td><td>${xpPct(xpOf(c.rank))}</td>
      <td>${c.level} → ${fmt(c.level + c.gained, 1)}</td><td>${fmt(c.minutes, 0)}</td><td>${fmt(c.levelsPerHour)}</td><td>${c.killsPerMinute ? fmt(c.killsPerMinute, 2) : ""}</td>
      <td>${c.xpPerKill > 0 ? fmt(c.xpPerKill, 2) + "%" : ""}</td></tr>`).join("")}</tbody></table></div>`;
}

/** Every place with nothing of yours to rank: where an answer is today (Level from A to B, from the players who share), and how to fill it. */
function placesEmpty(a) {
  const c = a.community, patch = a.places?.scope === "patch";
  const pool = c
    ? `Level from A to B answers today from the ${plural(c.players, "player")} who share: where to level, and about how long it takes.`
    : "Level from A to B answers from the players who share once their numbers arrive: Pathfinder looks for them every 30 minutes.";
  return `
    <div class="h2" style="font-size: 20px">No leveling in your logs${patch ? " since the 11-10 patch" : ""} yet</div>
    <div class="muted" style="line-height: 1.55; max-width: 720px">Every place ranks the places you've leveled, level by level.
      It fills in as you play: keep <b style="color: var(--text)">/log on</b> in the game and run a crawl or two.${patch ? " All time, above, adds your leveling from before the patch." : ""}</div>
    <div class="waiting" style="margin-top: 4px">${ASK_PEOPLE}<div style="min-width: 0"><div style="font-weight: 650">${c ? "Players who share have numbers now" : "Other players' numbers"}</div>
      <div class="ak" style="font-size: 12.5px; margin-top: 2px; line-height: 1.45">${esc(pool)}</div>
      <button class="link" data-ask-q="level" style="margin-top: 6px">Level from A to B →</button></div></div>`;
}

function askPlaces(a) {
  const P = a.places;
  if (!P) return `<div class="muted">Working out every place…</div>`;
  const m = metricNow(), lvl = a.level;
  const levels = P.levels.map(x => {
    const rows = x.rows.map(o => ({ ...o, label: o.place }));   // o.cost, the T5-eq given up for each level, is the engine's
    const rk = ranked(rows);
    // Like the route, a place played at the level itself beats one only played beside it.
    return { ...x, ranked: rk, star: rk.find(o => o.enough && o.exact) ?? rk.find(o => o.enough) ?? null };
  });
  const top = levels.length;
  if (!top || !levels.some(x => x.rows.length)) return placesEmpty(a);
  if (placeLevel == null) placeLevel = lvl < cap() ? lvl : (P.map.at(-1)?.level ?? top);
  placeLevel = Math.min(top, Math.max(1, placeLevel));
  // Twelve levels across, holding the picked level (your level starts near the middle).
  if (gridStart == null || placeLevel < gridStart || placeLevel >= gridStart + GRID_COLS) gridStart = placeLevel - GRID_COLS / 2 + 1;
  gridStart = Math.max(1, Math.min(top - GRID_COLS + 1, gridStart));
  const cols = Array.from({ length: Math.min(GRID_COLS, top) }, (_, i) => gridStart + i);

  const x = levels[placeLevel - 1];
  // The place the panel and the runs list are about: the one picked, else where you played last, else the ★ at the picked level.
  if (!placeSel || !P.map.some(c => c.place === placeSel)) placeSel = P.map.at(-1)?.place ?? x.star?.place ?? null;
  const bold = k => k === m ? ' style="font-weight: 700"' : "";
  const table = x.ranked.length ? `<div class="tablewrap" style="margin-top: 10px"><table>
    <thead><tr><th class="l">Place</th><th>Visits</th><th>Lv/h</th><th>T5/h</th><th>Above T5/h</th><th>Cost/level</th><th>Kills/min</th><th>Deaths/h</th><th class="l">Last</th></tr></thead>
    <tbody>${x.ranked.map(o => `<tr>
      <td class="l">${o === x.star ? '<span class="gold">★</span> ' : ""}${esc(o.place)}${o.exact ? ""
        : ` <span class="tag" title="Not played at level ${placeLevel} itself: these numbers come from the levels beside it">near</span>`}${o.fading
        ? ` <span class="tag fade" title="It was your best place 3 levels back, and another place now beats it here by more than a run's normal swing">Fading</span>` : ""}</td>
      <td>${o.crawls}${o.enough ? "" : ' <span class="dim">(hint)</span>'}</td>
      <td${bold("speed")}>${fmt(o.levelsPerHour)}</td><td class="gold"${bold("motes")}>${fmt(o.t5PerHour, 0)}</td>
      <td${bold("rare")}>${o.highTierPerHour > 0 ? fmt(o.highTierPerHour, 1) : ""}</td><td${bold("balanced")}>${signed(o.cost)}</td>
      <td>${fmt(o.killsPerMinute)}</td><td>${o.deathsPerHour ? fmt(o.deathsPerHour, 2) : ""}</td><td class="l dim">${esc(o.last.slice(5))}</td></tr>`).join("")}</tbody></table></div>`
    : `<div class="dim" style="margin-top: 10px">Nothing played within a level of ${placeLevel}${P.scope === "patch" ? " since the patch" : ""}.</div>`;

  return `
    <div class="between" style="flex-wrap: wrap; gap: 8px; align-items: center">
      <div class="row" style="gap: 10px; flex-wrap: wrap; align-items: center"><span style="font-weight: 650">Levels/h at each level${P.scope === "patch" ? " since the 11-10 patch" : ""}, for your trio</span>
        ${a.rank > 0 ? `<span class="chip xp" title="${xpTip(xpOf(a.rank))}">at your ${xpWords(xpOf(a.rank))}</span>${infoTip("xp")}` : ""}</div>
      <div class="row" style="gap: 6px"><button class="btn" data-grid-shift="-6"${gridStart <= 1 ? " disabled" : ""}>‹ Lower</button><button class="btn" data-grid-shift="6"${gridStart + GRID_COLS > top ? " disabled" : ""}>Higher ›</button></div>
    </div>
    <div class="ak" style="margin-top: 4px">${a.rank > 0 ? "Runs that needed other XP are re-figured for yours. " : ""}Pick a cell: every place at that level, and your runs at that place, as they happened, follow below.</div>
    <div style="margin-top: 12px; min-width: 0">${placeGrid(a, levels, cols)}</div>
    <div class="pgridsub">${routeBox(a) || "<div></div>"}${whyPanel(a, P, levels)}</div>
    <div class="askq" style="margin-top: 16px"><span style="font-weight: 650">Every place at level</span>${stepper("place", placeLevel, "Level")}
      <span class="ak" style="font-size: 12.5px">counts levels ${bandText(x.window)}</span><span class="grow"></span>
      ${lvl < cap() && lvl !== placeLevel ? `<button class="btn" data-place-level="${lvl}">Your level (${lvl})</button>` : ""}</div>
    ${table}
    ${runsList(P)}`;
}

const ASK_ADDS = {
  level: ["Places you've never run at these levels join the route, at the players' time.", "Each leg shows your time and theirs side by side; yours count more the more visits you have.", "Where you're slower than players who share, and whether it's kill speed or XP per kill."],
  farm: ["Farms at levels you haven't farmed since the patch.", "The odds of each fragment tier per crawl, from many chests.", "How many players stand behind each farm."],
  diff: ["Difficulties you skipped get numbers.", "Death risk from many players, not a handful of crawls.", "Solo and grouped play kept apart."],
  dmg: ["Typical damage at your level, with your classes, in each place.", "Where your damage ranks among those players.", "Which spells and abilities they lean on (names only)."],
  places: ["Places you have never played show up, ranked with yours.", "How many players stand behind each place.", "Grouped and solo rates side by side."],
};

// Which slice of the shared numbers the answer shows: follows Where and Who with.
function poolSlice(a) {
  const c = a.community;
  const where = a.where === "open" ? "open world" : a.where === "instances" || !c?.stretches ? "instances" : "instances and open world";
  const who = a.with === "solo" ? "solo" : a.with === "group" ? "in a group" : "solo and grouped together";
  return `${where}, ${who}`;
}

// What stands behind the community numbers (or why there are none).
function poolSource(a) {
  const c = a.community;
  if (a.communityGap) return `The numbers players share don't cover ${a.communityGap} yet; switch ${a.where === "open" ? "Where to Everywhere or Instances" : "Who with to Solo or grouped"} to see them.`;
  if (!c) return "Other players' numbers haven't arrived yet: Pathfinder looks for them every 30 minutes.";
  const share = c.players < 2 ? "" : c.players * c.maxShare >= 1
    ? `, and no one player counts for more than ${Math.round(c.maxShare * 100)}%` : ", and each player counts equally";
  // You share: your own play is taken out of their numbers, and a number that rested on you and one other player is gone.
  const two = c.youAndOne ?? [];
  const you = !c.leftYouOut ? ""
    : ` You share, so your own play is left out of their numbers${two.length
      ? `. Where only you and one other player have played (${two.slice(0, 3).join(", ")}${two.length > 3 ? ` and ${two.length - 3} more` : ""}), there's no number for you: it would be one player's.`
      : "."}`;
  return `${plural(c.players, "player")}${c.leftYouOut ? " (you among them)" : ""}, ${c.crawls} shared crawls${c.stretches ? ` and ${c.stretches} open-world hunts` : ""}, updated ${dt(c.generated)}. ` +
    `A number shows once ${plural(c.minPlayers, "player")} and ${c.minCrawls} visits stand behind it${share}. Shown: ${poolSlice(a)}, every trio.${you}`;
}

function renderAsk() {
  const el = $("#askBody"), a = askAnswer;
  if (!a) { el.innerHTML = `<section class="card empty" style="grid-column: 1 / -1">Working out answers from your logs…</section>`; return; }
  const r = a.route, f = a.farm, p = a.difficulty.pick;
  const mine = {
    level: `${r.placesSeen} ${r.placesSeen === 1 ? "place" : "places"} in your logs ${r.placesSeen === 1 ? "has" : "have"} play between levels ${r.from} and ${r.to}. Behind every answer: ${a.sources.instances} instance visits and ${a.sources.open} open-world hunts, ${a.sources.grouped} of them grouped.`,
    farm: (f.rows.length ? `${f.rows.reduce((t, x) => t + x.crawls, 0)} crawls` : "None") + ` at ${f.band === "50" ? "50" : "levels " + bandText(f.band)}${f.scope === "patch" ? " since the patch" : ""}` + (f.rows.length ? "." : " with 2 or more crawls."),
    diff: p ? `${p.columns.reduce((t, c) => t + c.crawls, 0)} crawls in ${p.zone} at levels ${p.from}–${p.to}.`
      : a.difficulty.community ? `Nothing at two difficulties in ${a.difficulty.community.zone} at levels ${a.difficulty.community.from}–${a.difficulty.community.to} yet.`
      : "No zone with two difficulties at the same levels.",
    dmg: `${a.damage.rows.length} ${a.damage.rows.length === 1 ? "place" : "places"} at ${a.damage.band === "50" ? "50" : "levels " + bandText(a.damage.band)}, your damage only.`,
    places: a.places ? `${a.places.map.length} leveling ${a.places.map.length === 1 ? "stretch" : "stretches"}${a.places.scope === "patch" ? " since the patch" : ""}, ${new Set(a.places.levels.flatMap(x => x.rows.map(o => o.place))).size} places between levels 1 and ${a.places.levels.length}.` : "",
  }[a.q];
  const method = {
    level: `Minutes per level are active time, in instances and open-world hunts, spread over the levels each covered and adjusted for the reincarnation XP requirement. Older crawls count less: half at ${a.halfLifeDays} days, a quarter at ${a.halfLifeDays * 2}. A level seen in only one crawl counts 2 minutes slower until there are more.${a.community ? ` Where players who share have a place too, its time is yours and theirs together: their number counts as much as one of your visits, so with one visit the time is half yours and with four it's 80% yours. A place only they have played uses their time.` : ""}`,
    farm: `T5-eq per active hour, motes and fragments together, Reward Chest included. Above T5 counts only Major and higher. Older crawls count less: half at ${a.halfLifeDays} days.`,
    diff: `Rates per active hour across every crawl at that difficulty and level band, solo and grouped together, with levels adjusted for the reincarnation XP requirement. Older crawls count less: half at ${a.halfLifeDays} days.`,
    dmg: "Damage per active second: melee, spells, damage over time, damage shield and your pet, at the level and the ones beside it.",
    places: `The same tables as every other answer: active time, older visits counting less (half at ${a.halfLifeDays} days), levels for the XP your trio needs. Each level is read with the ones beside it (nearer and better-covered levels counting more), the way the route reads it. ★ is the best place at each level by your ranking among places with 2 or more visits and 20 minutes; one visit is a hint, not a verdict.`,
  }[a.q];
  const body = a.q === "level" ? askLevel(a) : a.q === "farm" ? askFarm(a) : a.q === "diff" ? askDiff(a) : a.q === "places" ? askPlaces(a) : askDamage(a);
  el.innerHTML = `
    <div class="ask-main">
      <section class="card" style="padding: 14px 20px" aria-label="Your question">
        <div class="between" style="align-items: center"><div class="h2" style="font-size: 18px">Ask</div>
          ${seg2("ask-q", a.q, ASK_TABS, "Question")}</div>
        ${askQuestion(a)}
        ${askFilters(a)}
      </section>
      <section class="card" style="padding: 16px 20px 14px; display: flex; flex-direction: column; gap: 10px" aria-label="Answer" aria-live="polite">${body}</section>
    </div>
    <div class="stack" style="gap: 14px">
      <section class="card" style="padding: 16px 18px 14px" aria-label="Where this answer comes from">
        <div class="h">Where this answer comes from</div>
        <div class="row" style="align-items: flex-start; margin-top: 12px"><span class="chip gold" style="flex: none">Your logs</span><span class="soft" style="line-height: 1.45">${esc(mine)}</span></div>
        <div class="row" style="align-items: flex-start; margin-top: 10px"><span class="chip" style="flex: none">Community</span><span class="muted" style="line-height: 1.45">${esc(poolSource(a))}</span></div>
        <div class="ak" style="margin-top: 12px; line-height: 1.5">${a.q === "dmg" ? "" : esc("Estimates for where to go next, from your past visits. How a visit went against your usual is on the overlay and in History. ")}${esc(method)}${a.driftPerMonth != null ? " " + esc(driftNote(a.driftPerMonth)) : ""}</div>
        ${a.dddLeftOut ? `<div class="ak" style="margin-top: 8px; line-height: 1.5">Left out: ${a.dddLeftOut} crawls from before the 11-10 patch where DDD did most of the damage${a.openLeftOut ? `, and ${a.openLeftOut} open-world hunts from before it on a bard (or unknown) trio, since damage outside instances isn't kept` : ""}. DDD since the nerf counts like any other spell.</div>` : ""}
        ${a.noRateLeftOut && a.q !== "dmg" ? `<div class="ak" style="margin-top: 8px; line-height: 1.5">Left out: ${a.q === "farm" ? `${plural(a.noRateLeftOut, "crawl")} at ${a.farm.at >= a.levelCap ? a.levelCap : `levels ${bandText(a.farm.band)}`}${a.farm.scope === "patch" ? " since the patch" : ""} with no rate (a crawl you didn't finish, under ${rateFromKills() ?? 8} kills)` : `${plural(a.noRateLeftOut, "visit")} with no rate (under ${rateFromKills() ?? 8} kills, in the open world or an instance you didn't finish)`}. History shows them as they happened.</div>` : ""}
        ${a.bonusLeftOut ? `<div class="ak" style="margin-top: 8px; line-height: 1.5">Left out: ${a.bonusLeftOut} ${a.bonusLeftOut === 1 ? "visit or hunt" : "visits and hunts"} during bonus-XP events, which make a place look faster than it is. History shows them as they happened.</div>` : ""}
      </section>
      <section class="card" style="padding: 16px 18px 14px" aria-label="When others share">
        <div class="h">When others share</div>
        <div style="display: flex; flex-direction: column; gap: 10px; margin-top: 12px">${ASK_ADDS[a.q].map(x => `
          <div class="row" style="align-items: flex-start"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="var(--gold)" stroke-width="2" stroke-linecap="round" aria-hidden="true" style="flex: none; margin-top: 1px"><path d="M12 5v14M5 12h14"/></svg><span class="soft" style="line-height: 1.45">${esc(x)}</span></div>`).join("")}</div>
        <div class="ak" style="margin-top: 12px; line-height: 1.5">Your own numbers count most wherever you have them. Other players' numbers fill the rest and say how many players stand behind them.</div>
      </section>
    </div>`;
  if (askFocus) { el.querySelector(askFocus)?.focus(); askFocus = null; }
}

$("#askBody").addEventListener("click", e => {
  const b = e.target.closest("button");
  if (!b || !askAnswer) return;
  const a = askAnswer, d = b.dataset;
  const remember = attr => { askFocus = `[${attr}="${CSS.escape(b.getAttribute(attr))}"]` + (d.d ? `[data-d="${d.d}"]` : ""); };
  if (d.askQ) { remember("data-ask-q"); ask({ q: d.askQ }); }
  else if (d.askGoal) { remember("data-ask-goal"); ask({ goal: d.askGoal }); }
  else if (d.askScope) { remember("data-ask-scope"); ask({ scope: d.askScope }); }
  else if (d.askRange) { remember("data-ask-range"); const [from, to] = d.askRange.split(",").map(Number); ask({ from, to }); }
  else if (d.askAt) { remember("data-ask-at"); ask({ at: Number(d.askAt) }); }
  else if (d.askDiff) { remember("data-ask-diff"); ask({ diff: d.askDiff }); }
  else if (d.askWhere) { remember("data-ask-where"); ask({ where: d.askWhere }); }
  else if (d.askWith) { remember("data-ask-with"); ask({ with: d.askWith }); }
  else if (d.askMetric) {
    // Ranking places is your Goal (Settings): it also picks the overlay's NEXT choices.
    remember("data-ask-metric");
    metric = d.askMetric;
    if (data?.settings) data.settings.goal = metric;
    post("setting", { key: "goal", value: metric });
    renderAsk(); renderHome(); renderSettings();
  }
  else if (d.placeLevel) { remember("data-place-level"); placeLevel = Number(d.placeLevel); renderAsk(); }
  else if (d.cellLevel) {
    // A grid cell: that level for the table below, that place for the panel and the runs list.
    placeLevel = Number(d.cellLevel); placeSel = d.cellPlace;
    askFocus = `[data-cell-level="${d.cellLevel}"][data-cell-place="${CSS.escape(d.cellPlace)}"]`;
    renderAsk();
  }
  else if (d.gridShift) {
    remember("data-grid-shift");
    const shift = Number(d.gridShift);
    gridStart = (gridStart ?? 1) + shift; placeLevel = Math.min(49, Math.max(1, (placeLevel ?? 1) + shift));
    renderAsk();
  }
  else if (d.askStep) {
    remember("data-ask-step");
    if (d.askStep === "place") { placeLevel = Math.min(49, Math.max(1, (placeLevel ?? 1) + Number(d.d))); renderAsk(); return; }
    const step = Number(d.d), r = a.route;
    if (d.askStep === "from") { const from = Math.min(49, Math.max(1, r.from + step)); ask({ from, to: Math.max(r.to, from + 1) }); }
    else if (d.askStep === "to") { const to = Math.min(50, Math.max(2, r.to + step)); ask({ to, from: Math.min(r.from, to - 1) }); }
    else ask({ at: Math.min(50, Math.max(1, a.farm.at + step)) });
  }
});
renderAsk();


// Older visits are scaled to how fast you play now (the engine's PaceDrift): said in one line under every answer.
function driftNote(perMonth) {
  const p = Math.round(Math.abs(perMonth));
  return perMonth < 0
    ? `You've been getting faster, about ${p}% less time a level each month, so older visits are scaled to your pace now.`
    : `You've been getting slower, about ${p}% more time a level each month, so older visits are scaled to your pace now.`;
}
