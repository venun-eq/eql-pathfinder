// Run list B, as the spec words it. MODE ("full", "open" or "narrow") is prepended by run.ps1.
// Replaces campList, so the real renderCampaign, Swap… and its answer all run.
const RL_CSS = `
.rl-key { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 14px; font-size: 12px; color: var(--muted); margin-top: 8px; }
.rl-key .pips3 { display: inline-flex; vertical-align: -1px; margin-right: 5px; } .rl-key .pips3 i { width: 8px; height: 8px; }
.rl-key b { color: var(--text); font-weight: 650; } .rl-key b.g { color: var(--gold); } .rl-key b.p { color: var(--faint); }
.rl-cols, .rline { display: grid; grid-template-columns: 50px 150px repeat(3, 84px) minmax(90px, 1fr) 104px 128px; gap: 0 12px; align-items: center; }
.rl-cols { font-size: 10.5px; letter-spacing: .08em; text-transform: uppercase; color: var(--faint); font-weight: 600; padding: 12px 6px 4px; }
.rl-cols .r, .rline .lxp { text-align: right; }
@media (max-width: 1150px) { .rl-cols, .rline { grid-template-columns: 40px 128px repeat(3, 76px) minmax(70px, 1fr) 92px 116px; gap: 0 10px; } .rline .lact { gap: 4px; } }
.rline { padding: 7px 6px; border-bottom: 1px solid var(--line2); border-radius: 6px; font-size: 13px; }
.rline.next { background: linear-gradient(90deg, var(--gold-bg), transparent 75%); }
.rline.pinned { box-shadow: inset 3px 0 0 var(--gold-dim); }
.rline .ln { text-align: right; padding-right: 4px; }
.rcell { display: flex; align-items: center; gap: 6px; font-weight: 600; white-space: nowrap; min-width: 0; }
.rcell .pips3 i { width: 8px; height: 8px; }
.rcell.top b { color: var(--gold); } .rcell.past b { color: var(--faint); font-weight: 500; }
.rline .st { font-size: 12px; color: var(--muted); line-height: 1.35; }
.rline .lact { justify-content: flex-end; }
.rline .lswap { grid-column: 2 / -1; padding: 6px 0 6px; }
.rline .rnote { grid-column: 2 / -1; font-size: 12px; color: var(--muted); padding-top: 2px; }
.rline.played { color: var(--muted); } .rline.played .rcell b { color: var(--muted); }
.rhead { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; padding: 16px 6px 6px; border-bottom: 1px solid var(--line2); }
.rhead .rt { font-weight: 700; } .rhead .rs { font-size: 12px; color: var(--muted); } .rhead .grow { flex: 1; }
.rhead.fold { background: var(--inset); border-radius: 8px; border: 0; padding: 8px 10px; margin-top: 10px; }
.rearns { font-size: 12.5px; margin-bottom: 8px; } .rearns b { font-weight: 650; }
`;
(function wait() {
  if (!(typeof data !== 'undefined' && data?.reinc?.campaign?.runs?.length)) return setTimeout(wait, 1000);
  const st = document.createElement("style"); st.textContent = RL_CSS; document.head.appendChild(st);
  if (MODE === "open") campShowPlayed = true;
  const folded = new Set();
  campList = function (v, p, busy) {
    const pips = (have, end) => `<span class="pips3">${[1, 2, 3].map(k => `<i class="${k <= have ? "have" : k <= end ? "plan" : ""}"></i>`).join("")}</span>`;
    const c = { ...v.classRanks }, r = { ...v.raceRanks }, now = { ...v.classRanks }, rnow = { ...v.raceRanks };
    // the lines: consecutive runs of one loadout in one group
    const groupOf = x => x.state === "done" ? "played" : x.state === "leveling" ? "leveling"
      : (() => { const ks = x.gains.filter(g => g.kind === "class").map(g => g.rank); return ks.length ? `r${Math.min(...ks)}` : "races"; })();
    const lines = [];
    for (const x of v.runs) {
      const g = groupOf(x), last = lines.at(-1);
      if (x.state === "next" || x.state === "planned") { x.classes.forEach(k => c[k] = (c[k] ?? 0) + 1); r[x.race] = (r[x.race] ?? 0) + 1; }
      const snap = { c: { ...c }, r: { ...r } };
      if (last && last.g === g && last.loadout === x.loadout && x.loadout >= 0) { last.runs.push(x); last.snap = snap; }
      else lines.push({ g, loadout: x.loadout, runs: [x], snap });
    }
    const line = L => {
      const x0 = L.runs[0], i = L.loadout, l = p.loadouts[i], nums = L.runs.map(x => campRunNo(x.n));
      const toPlay = L.g !== "played" && L.g !== "leveling", isNext = L.runs.some(x => x.state === "next");
      const top = new Set(), past = new Set(); let comp = 0;
      for (const x of L.runs) { x.gains.forEach(g => { if (g.kind === "completionist") comp = g.rank; else if (g.rank === 3) top.add(g.key); }); x.past.forEach(k => past.add(k)); }
      const cell = (k, rk, have, name) => `<span class="rcell${top.has(k) ? " top" : past.has(k) ? " past" : ""}" title="${esc(name ?? k)} ${rk}/3 once ${nums.length > 1 ? "these runs are" : "this run is"} played${past.has(k) ? ": 3/3 already, so the run adds XP to level and no passive" : ""}"><b>${esc(name ?? k)}</b>${pips(have, rk)}</span>`;
      const race = cell(x0.race, L.snap.r[x0.race] ?? 0, rnow[x0.race] ?? 0, x0.race);
      const cls = x0.classes.map(k => cell(k, L.snap.c[k] ?? 0, now[k] ?? 0)).join("");
      const lv = L.runs[0];
      const xs = L.runs.map(x => x.xp).filter(x => x > 0);
      const xp = !xs.length ? "" : Math.min(...xs) === Math.max(...xs) ? xpPct(xs[0]) : `${xpPct(Math.min(...xs))} → ${xpPct(Math.max(...xs))}`;
      const stat = L.g === "leveling" ? `<span class="num">${lv.level} / ${cap()}</span>${lv.etaHours != null ? ` · <span class="gold">≈ ${fmt(lv.etaHours)} h to go</span>` : lv.crawls === 0 ? " · not played yet" : ""}`
        : isNext ? `<span class="chip gold">next</span>` : "";
      const open = toPlay && campSwap === i, pinned = toPlay && l?.pinned;
      const acts = !toPlay ? `<span class="lact"><span class="dim" title="${L.g === "played" ? "Played" : "Reincarnated"}: nothing in the plan changes it">${LOCK}</span></span>`
        : `<span class="lact"><button class="iconbtn" data-cl="${i}" data-act="pin" aria-pressed="${!!pinned}"${pinned ? ' style="color: var(--gold); border-color: var(--gold-dim)"' : ""} title="${pinned ? "Pinned: Rebuild keeps it as it is" : "Pin: Rebuild keeps it as it is"}">${PIN}</button><button class="btn sm" data-cl="${i}" data-camp="swap" aria-expanded="${open}">${open ? "Close" : "Swap…"}</button><button class="iconbtn" data-cl="${i}" data-act="menu" aria-haspopup="menu">⋯</button></span>`;
      const earnsWords = () => {
        const parts = [];
        const want = {}; for (const x of L.runs) x.gains.forEach(g => { if (g.kind !== "completionist") want[g.key] = Math.max(want[g.key] ?? 0, g.rank); });
        Object.entries(want).forEach(([k, rk]) => parts.push(`${k} ${rk}/3`)); if (comp) parts.push(`Completionist ${comp}`);
        return `<div class="rearns"><b>${nums.length > 1 ? `Runs ${campNums(nums)}` : `Run ${nums[0]}`} earn${nums.length > 1 ? "" : "s"}</b> ${esc(parts.join(" · ") || "no new passive")} · ${xp} XP to level${past.size ? `<div class="dim" style="margin-top: 2px">${esc([...past].join(", "))} ${past.size > 1 ? "are" : "is"} 3/3 already: ${past.size > 1 ? "their ranks go" : "its rank goes"} up anyway, which adds XP to level and no passive.</div>` : ""}</div>`;
      };
      return `<div class="rline${isNext ? " next" : ""}${pinned ? " pinned" : ""}${L.g === "played" ? " played" : ""}">
        <span class="ln num">${nums.length > 1 ? `${nums[0]}–${nums.at(-1)}` : nums[0]}</span>${race}${cls}
        <span class="st">${stat}</span><span class="lxp num">${xp}</span>${acts}
        ${open ? `<div class="lswap">${earnsWords()}${campSwapPanel(i, l).replace('<div class="lswap">', "<div>")}</div>` : ""}</div>`;
    };
    const rng = ns => ns.length === 1 ? `run ${ns[0]}` : `runs ${ns[0]}–${ns.at(-1)}`;
    const head = (key, title, sub, n) => {
      const f = folded.has(key);
      return `<div class="rhead"><span class="rt">${title}</span><span class="rs">${sub}${f ? ` · ${plural(n, "loadout")}` : ""}</span><span class="grow"></span><button class="link" aria-expanded="${!f}">${f ? "Show" : "Fold"}</button></div>`;
    };
    let html = "";
    const played = lines.filter(L => L.g === "played"), before = v.before ?? 0;
    if (before || played.length) {
      const last = Math.max(before, ...played.flatMap(L => L.runs.map(x => campRunNo(x.n))));
      html += `<div class="rhead fold"><span class="rt">Played · ${last === 1 ? "run 1" : `runs 1–${last}`}</span><span class="rs">in your ranks already: nothing in the plan changes them</span><span class="grow"></span><button class="link" data-camp="played" aria-expanded="${campShowPlayed}">${campShowPlayed ? "Hide" : "Show"}</button></div>`;
      if (campShowPlayed) {
        const pl = v.played ?? [];
        html += pl.map(x => `<div class="rline played"><span class="ln num">${x.n}</span>
          ${(() => { const rk = {}; x.gains.forEach(g => rk[g.key] = g.rank); const cc = (k, nm) => `<span class="rcell${rk[k] === 3 ? " top" : rk[k] == null ? " past" : ""}"><b>${esc(nm ?? k)}</b>${pips(rk[k] ?? 3, rk[k] ?? 3)}</span>`; return cc(x.race) + x.classes.map(k => cc(k)).join(""); })()}
          <span class="st">${esc(dt(x.at).split(",")[0])}</span><span class="lxp"></span><span class="lact"><span class="dim" title="Played: nothing in the plan changes it">${LOCK}</span></span></div>`).join("") + played.map(line).join("");
      }
    }
    const lev = lines.filter(L => L.g === "leveling");
    if (lev.length) html += `<div class="rhead"><span class="rt">Leveling now</span><span class="rs">${plural(lev.length, "run")} reincarnated, below ${cap()}</span></div>` + lev.map(line).join("");
    for (const g of ["r1", "r2", "r3", "races"]) {
      const ls = lines.filter(L => L.g === g); if (!ls.length) continue;
      const runs = ls.flatMap(L => L.runs), ns = runs.map(x => campRunNo(x.n)), xs = runs.map(x => x.xp);
      const xpw = Math.min(...xs) === Math.max(...xs) ? xpWords(xs[0]) : `${xpPct(Math.min(...xs))} to ${xpWords(Math.max(...xs))}`;
      const comp = runs.flatMap(x => x.gains.filter(q => q.kind === "completionist").map(q => `Completionist ${q.rank} at run ${campRunNo(x.n)}`));
      const title = g === "races" ? "Races" : `Round ${g[1]} · classes to ${g[1]}/3`;
      const sub = [rng(ns), xpw, ...comp, g === "races" ? "every class is 3/3 by now" : ""].filter(Boolean).join(" · ");
      if (MODE === "full" && g === "r3" && false) folded.add(g);
      html += head(g, title, sub, ls.length) + (folded.has(g) ? "" : ls.map(line).join(""));
    }
    const key = `<div class="rl-key"><span>Pips show each rank once the line is played:</span>
      <span><span class="pips3"><i class="have"></i></span>you have it</span>
      <span><span class="pips3"><i class="plan"></i></span>this plan earns it</span>
      <span><span class="pips3"><i></i></span>not yet</span>
      <span><b class="g">WAR</b> reaches 3/3 here</span>
      <span><b class="p">WAR</b> 3/3 already: more XP to level, no passive</span></div>`;
    return `<section class="card camp-list" style="padding: 14px 18px 12px" aria-label="Your runs">
    <div class="between"><div class="h2">Your runs</div><span class="dim num">${`${v.before ? `${v.before} played before · ` : ""}${v.done - v.leveling} done · ${v.leveling ? `${v.leveling} leveling · ` : ""}${v.left} to go · ${plural(p.loadouts.length, "loadout")}`}</span></div>
    <div class="dim" style="font-size: 12px; margin-top: 2px; line-height: 1.4">In the order you'd play them, a round at a time. Swap… shows the cheapest changes that keep every goal, or lets you pick a race or class yourself. A loadout you change by hand is pinned: changing your goals re-plans the rest around it.</div>
    ${key}
    <div class="rl-cols"><span class="r">Run</span><span>Race</span><span style="grid-column: span 3">Classes</span><span></span><span class="r">XP to level</span><span></span></div>
    <div>${html}</div>
    <div class="row" style="margin-top: 10px; flex-wrap: wrap"><button class="btn" data-camp="add">Add a loadout</button>
      <span class="dim" style="font-size: 11.5px; line-height: 1.45; flex: 1; min-width: 260px">A run raises its race and its three classes one rank each, and relevels at the highest of them: each rank makes every level need 10% more XP. Mobs give the same XP; the bar is bigger. No hours: how long a trio takes depends on what its classes do together, so Pathfinder doesn't guess it.</span></div>
  </section>`;
  };
  show("campaign"); renderCampaign();
  if (MODE === "open") setTimeout(() => {
    const b = document.querySelector('.rline.next [data-camp="swap"]') || document.querySelector('.rline [data-camp="swap"]');
    b?.click();
  }, 600);
  if (MODE === "narrow") setTimeout(() => {
    document.querySelector('.camp-list').scrollIntoView();
  }, 600);
})();
