// MODE is set by the line run.ps1 prepends: "full", "mix" or "empty".
(function wait() {
  if (!(typeof data !== 'undefined' && data?.reinc)) return setTimeout(wait, 1000);
  show("campaign");
  const CL = "WAR CLR PAL RNG SHD DRU MNK BRD ROG SHM NEC WIZ MAG ENC BST BER".split(" ");
  const RC = "HUM BAR ERU ELF HIE DEF HEF DWF TRL OGR HFL GNM IKS KRN FRG HDE".split(" ");
  const cNow = [2,2,1,2,2,2,2,2,2,2,2,1,2,1,1,1], rNow = [1,1,1,1,0,0,0,0,0,1,0,0,1,1,1,1];
  const mix = MODE === "mix", empty = MODE.startsWith("empty");
  // goal, end (what the plan reaches) per cell
  let cGoal, cEnd, rGoal, rEnd;
  if (MODE === "full") { cGoal = CL.map(() => 3); cEnd = CL.map(() => 3); rGoal = RC.map(() => 3); rEnd = RC.map(() => 3); }
  else if (empty) { cGoal = CL.map(() => 0); cEnd = cNow; rGoal = RC.map(() => 0); rEnd = rNow; }
  else {
    const g = { WAR: 3, CLR: 3, PAL: 2, SHM: 3, ENC: 2, BER: 2, NEC: 1 };
    cGoal = CL.map(k => g[k] ?? 0);
    // the runs for these goals carry a few classes along
    const e = { WAR: 3, CLR: 3, PAL: 2, SHM: 3, ENC: 2, BER: 2, ROG: 3, MNK: 3, BST: 2 };
    cEnd = CL.map((k, i) => Math.max(cNow[i], e[k] ?? 0));
    const rg = { HIE: 3, FRG: 3 }, re = { HIE: 3, FRG: 2, HUM: 2, ELF: 2 };
    rGoal = RC.map(k => rg[k] ?? 0); rEnd = RC.map((k, i) => Math.max(rNow[i], re[k] ?? 0));
  }
  const pips = (now, end, goal, planned) => [1, 2, 3].map(k => {
    const st = k <= now ? "have" : k <= end ? "plan" : "";
    const miss = planned && goal && end < goal && k > end && k <= goal;
    return `<button class="gpip ${st}${k === goal ? " goal" : ""}${miss ? " miss" : ""}${goal && k > goal && st ? " past" : ""}"></button>`;
  }).join("");
  const cells = (keys, now, end, goal) => keys.map((k, i) => {
    const miss = !empty && goal[i] && end[i] < goal[i];
    return `<div class="gcell${miss ? " miss" : ""}"><span>${k}</span><span class="gpips">${pips(now[i], end[i], goal[i], !empty)}</span></div>`;
  }).join("");
  const sum = a => a.reduce((x, y) => x + y, 0);
  const want = (now, goal) => sum(goal.map((g, i) => g ? Math.min(now[i], g) : 0));
  const line = (now, goal, by) => sum(goal) ? `<b class="num">${want(now, goal)}</b> of ${sum(goal)} ranks you want · ${by}` : `<b class="num">${sum(now)}</b> of 48 ranks earned · no goal yet`;
  const cBy = MODE === "full" ? "all by run 16" : mix ? "all by run 12" : "";
  const rBy = MODE === "full" ? "all by run 48" : mix ? `<span class="warn">not all in this plan</span>` : "";
  const sw = (c, t) => `<span><button class="gpip ${c}" tabindex="-1"></button>${t}</span>`;
  const legend = `<div class="glegend">${sw("have", "you have it")}${empty ? "" : sw("plan", "this plan earns it") + sw("", "not planned")}${sw("goal", "your goal")}${mix ? sw("goal miss", "a goal this plan misses") : ""}</div>
    <div class="dim" style="font-size: 12px; margin-top: 4px">Click a pip for the rank you want. Click your goal's pip again to lower it.</div>`;
  const presets = empty ? "" : `<div class="row" style="gap: 6px; flex-wrap: wrap; margin-top: 10px">
      <button class="btn sm${MODE === "full" ? " on" : ""}">${MODE === "full" ? "✓ " : ""}Everything 3/3 <span class="dim">· 39 runs</span></button>
      <button class="btn sm">Every class 3/3 <span class="dim">· 7 runs</span></button>
      <button class="btn sm">Clear goals</button><span class="grow"></span><button class="link">More options</button></div>`;
  const comp = MODE === "full" ? [["have"], ["plan"], ["plan"]] : mix ? [["have"], [""], [""]] : [["have"], [""], [""]];
  const compLine = MODE === "full" ? "rank 2 at run 11" : mix ? "this plan doesn't raise it" : "goes up when every class reaches 1, 2 and 3";
  const block = (label, body) => `<div class="gblock"><div class="lbl glabel">${label}</div><div>${body}</div></div>`;
  const goals = `<section class="card side-card" aria-label="Your goals">
    <div class="between"><div class="h2">Your goals</div></div>
    ${presets}
    ${block("Classes", `<div class="pipgrid">${cells(CL, cNow, cEnd, cGoal)}</div><div class="gline">${line(cNow, cGoal, cBy)}</div>`)}
    ${block("Races", `<div class="pipgrid">${cells(RC, rNow, rEnd, rGoal)}</div><div class="gline">${line(rNow, rGoal, rBy)}</div>`)}
    ${block(`Completionist${infoTip("completionist")}`, `<div class="gpips" style="margin-top: 2px">${comp.map(c => `<button class="gpip ${c[0]}" tabindex="-1" style="cursor: default"></button>`).join("")}</div><div class="gline"><b class="num">1</b> of 3 · ${compLine}</div>`)}
    <div style="margin-top: 14px">${legend}</div>
    ${empty ? `<div class="row" style="margin-top: 14px"><button class="btn primary" disabled>Build the plan</button><span class="dim" style="font-size: 12px">Click a pip, or pick a ready-made goal.</span></div>` : ""}
  </section>`;
  const chips = ks => `<div class="chips" style="margin-top: 6px">${ks.map(k => `<button class="chip">${k}</button>`).join("")}</div>`;
  const first = `<section class="card side-card" aria-label="What you want first">
    <div class="between"><div class="h2">What you want first</div><span class="dim" style="font-size: 12px">The rest are Pathfinder's to place</span></div>
    <div style="font-weight: 650; margin-top: 10px">Classes you want first</div>
    <div class="muted" style="font-size: 12.5px; margin-top: 4px">Nothing ranked: Pathfinder places every class the cheapest way. Click one below to earn it first, or to unlock it now.</div>
    <div class="lbl" style="margin-top: 8px">Pathfinder places (16) · click one to rank it</div>${chips(CL)}
    <div style="font-weight: 650; margin-top: 14px">Races you want first</div>
    <div class="muted" style="font-size: 12.5px; margin-top: 4px">Nothing ranked: Pathfinder places every race the cheapest way. Click one below to earn it first, or to unlock it now.</div>
    <div class="lbl" style="margin-top: 8px">Pathfinder places (15) · click one to rank it</div>${chips(RC.filter(k => k !== "HIE").map(k => k === "FRG" ? "FRG · locked" : k))}
    <div class="dim" style="font-size: 12px; margin-top: 6px">3/3 already: HIE</div>
  </section>`;
  const combos = `<section class="card side-card" aria-label="Your combos">
    <div class="between"><div class="h2">Your combos</div><span class="dim" style="font-size: 12px">Kept through every rebuild and goal change</span></div>
    <div class="muted" style="font-size: 12.5px; margin-top: 6px">Classes you want together or apart, or a trio to play. A Must is held above the cheapest path, at the price shown. A Prefer is kept wherever it costs nothing.</div>
    <div class="row" style="margin-top: 10px; gap: 8px"><button class="btn sm">Add a combo</button></div>
  </section>`;
  const head = MODE === "full"
    ? { name: "Everything 3/3", run: 8, of: 48, lines: [`Leveling: <b>Barbarian · DRU / BRD / NEC</b> · <span class="num">25 / 50</span> · <span class="num gold">≈ 9.8 h to go</span>`,
        `Also leveling: run 9 · <b>Wood Elf · MAG / MNK / SHM</b> · 1 / 50 · <span class="dim">not played yet</span>`,
        `Next to reincarnate: <b>run 10 · High Elf · BST / ENC / BER</b> at +20% XP to level${infoTip("xp")}`,
        `39 runs to go · +60% XP to level on average · the last needs +100% XP to level${infoTip("cheapest")}`] }
    : { name: "7 passives you picked", run: 8, of: 19, lines: [`Leveling: <b>Barbarian · DRU / BRD / NEC</b> · <span class="num">25 / 50</span> · <span class="num gold">≈ 9.8 h to go</span>`,
        `Next to reincarnate: <b>run 9 · High Elf · WAR / CLR / SHM</b> at +30% XP to level${infoTip("xp")}`,
        `11 runs to go · +34% XP to level on average · the last needs +40% XP to level · 1 run more than the cheapest path${infoTip("cheapest")}`] };
  const header = empty ? `<section class="card camp-start" style="padding: 18px 20px; grid-area: head">
      <div class="h2">Plan every reincarnation</div>
      <div class="muted" style="margin-top: 6px; line-height: 1.5">Each reincarnation is one race and three classes, and every class and race passive has 3 ranks. Pick what you're after: Pathfinder builds the cheapest way there from your ranks now.</div>
      <div class="presets" style="margin-top: 14px">
        ${[["Everything 3/3", 39, "+60% XP to level on average · the last needs +100% XP to level"], ["Every class 3/3", 7, "+29% XP to level on average · the last needs +30% XP to level"]].map(([n, r, x]) => `<button class="preset"><div style="font-size: 16px; font-weight: 650">${n}</div>
          <div class="num" style="margin-top: 6px; font-size: 24px; font-weight: 700">${r} <span class="pk">runs from here</span></div>
          <div class="dim" style="font-size: 12px; margin-top: 2px">${r + 7} with the 7 you've played</div>
          <div class="dim" style="font-size: 12px; margin-top: 6px; line-height: 1.45">${x}</div></button>`).join("")}
        <button class="preset"><div style="font-size: 16px; font-weight: 650">Choose my own</div><div class="dim" style="font-size: 12px; margin-top: 8px; line-height: 1.45">Click the pips in Your goals for the rank you want, then Build the plan.</div></button></div>
    </section>`
    : `<section class="card camp-head">
      <div class="headrun"><div class="lbl">Your campaign · ${head.name}</div><div class="fc-big num" style="margin-top: 8px">Run ${head.run} <span style="font-size: 18px; color: var(--faint); font-weight: 500">of ${head.of}</span></div></div>
      <div class="headlines muted">${head.lines.map(l => `<div>${l}</div>`).join("")}</div>
      <div class="headbtns"><button class="btn sm">Rebuild the rest</button><button class="btn sm">Start over</button></div>
    </section>`;
  const R = MODE === "full" ? [["Barbarian · DRU / BRD / NEC", "run 8 · ≈ 9.8 h to go", "leveling", "25 / 50"], ["Wood Elf · MAG / MNK / SHM", "run 9 · not played yet", "leveling", "1 / 50"],
      ["High Elf · BST / ENC / BER", "run 10 · BST 2/3 · ENC 2/3 · BER 2/3 · High Elf 1/3", "next", "+20%"], ["High Elf · PAL / WIZ / BST", "run 11 · PAL 2/3 · WIZ 2/3 · BST 3/3 · High Elf 2/3", "", "+30%"],
      ["High Elf · WAR / RNG / SHM", "run 12 · WAR 3/3 · RNG 3/3 · SHM 3/3 · High Elf 3/3", "", "+30%"], ["Dark Elf · CLR / MNK / ENC", "run 13 · CLR 3/3 · MNK 3/3 · ENC 3/3 · Dark Elf 1/3", "", "+30%"],
      ["Dark Elf · BRD / ROG / WIZ", "run 14 · BRD 3/3 · ROG 3/3 · WIZ 3/3 · Dark Elf 2/3", "", "+30%"], ["Dark Elf · SHD / DRU / MAG", "run 15 · SHD 3/3 · DRU 3/3 · MAG 3/3 · Dark Elf 3/3", "", "+30%"],
      ["Half Elf · PAL / NEC / BER", "run 16 · PAL 3/3 · NEC 3/3 · BER 3/3 · Half Elf 1/3", "", "+30%"], ["Half Elf · WAR / RNG / SHM", "runs 17–18 · Half Elf 3/3 · 6 ranks past 3/3", "", "+40% → +50%"],
      ["Dwarf · CLR / MNK / ENC", "runs 19–21 · Dwarf 3/3 · 9 ranks past 3/3", "", "+40% → +60%"], ["Troll · BRD / ROG / WIZ", "runs 22–24 · Troll 3/3 · 9 ranks past 3/3", "", "+40% → +60%"]]
    : [["Barbarian · DRU / BRD / NEC", "run 8 · ≈ 9.8 h to go", "leveling", "25 / 50"], ["High Elf · WAR / CLR / SHM", "run 9 · WAR 3/3 · CLR 3/3 · SHM 3/3 · High Elf 1/3", "next", "+30%"],
      ["High Elf · PAL / ENC / BER", "run 10 · PAL 2/3 · ENC 2/3 · BER 2/3 · High Elf 2/3", "", "+30%"], ["High Elf · ROG / MNK / BST", "run 11 · ROG 3/3 · MNK 3/3 · BST 2/3 · High Elf 3/3", "", "+30%"],
      ["Froglok · WAR / CLR / SHM", "run 12 · Froglok 2/3 · 3 ranks past 3/3", "", "+40%"]];
  const runs = empty ? `<section class="card camp-runs"><div class="h2">Your runs</div><div class="muted" style="margin: 14px 0 4px">Your runs show here once you pick a goal: a ready-made one, or your own with the pips.</div></section>`
    : `<section class="card camp-runs"><div class="between"><div class="h2">Your runs</div><span class="dim">7 played before · ${MODE === "full" ? "2 leveling · 39 to go" : "1 leveling · 11 to go"}</span></div>
      <div class="dim" style="font-size: 12px; margin-top: 4px">(Your runs as today: campaign-run-list redraws it.)</div>
      ${R.map((x, i) => `<div class="mrow${x[2] === "next" ? " next" : ""}"><span class="num dim" style="width: 26px">${x[1].match(/runs? ([0-9]+)/)[1]}</span><div class="grow"><b>${x[0]}</b>${x[2] ? ` <span class="chip${x[2] === "next" ? " gold" : ""}" style="margin-left: 6px">${x[2]}</span>` : ""}<div class="dim" style="font-size: 12px; margin-top: 2px">${x[1]}</div></div><span class="num muted" style="width: 110px; text-align: right">${x[3]}</span>${x[2] === "leveling" ? `<span style="width: 152px"></span>` : `<button class="btn sm">⚑</button><button class="btn sm">Swap…</button><button class="btn sm">⋯</button>`}</div>`).join("")}
    </section>`;
  const css = `<style>
    .camp-grid { display: grid; grid-template-columns: minmax(0, 1fr) 460px; grid-template-areas: "head side" "runs side"; grid-template-rows: auto 1fr; gap: 14px; align-items: start; }
    .camp-head { grid-area: head; padding: 16px 20px; display: grid; grid-template-columns: auto minmax(0, 1fr) auto; gap: 4px 24px; align-items: start; }
    .camp-head .headlines { font-size: 13px; line-height: 1.6; } .camp-head .headlines b { color: var(--text); }
    .camp-head .headbtns { display: flex; gap: 8px; }
    .camp-runs { grid-area: runs; padding: 16px 20px; }
    .camp-side { grid-area: side; display: flex; flex-direction: column; gap: 14px; min-width: 0; }
    .side-card { padding: 16px 18px; }
    .gblock { margin-top: 14px; } .gblock .glabel { margin-bottom: 6px; }
    .gline { font-size: 12.5px; color: var(--muted); margin-top: 8px; } .gline b { color: var(--text); font-size: 15px; }
    .pipgrid { gap: 10px 4px; margin-top: 0; }
    .gcell, .gcell.on { background: none; border-color: transparent; color: var(--muted); padding: 2px 0; }
    .gcell.miss { color: var(--warn); background: none; border-color: transparent; }
    .gpips { gap: 6px; }
    .gpip { width: 11px; height: 11px; }
    .gpip.goal { box-shadow: 0 0 0 1.5px var(--panel), 0 0 0 3px var(--gold); }
    .gpip.goal.miss { box-shadow: 0 0 0 1.5px var(--panel), 0 0 0 3px var(--warn); }
    .gpip.past { opacity: .4; }
    .glegend { display: flex; flex-wrap: wrap; gap: 6px 16px; font-size: 12px; color: var(--soft); }
    .glegend span { display: flex; align-items: center; gap: 7px; } .glegend .gpip { cursor: default; }
    .mrow { display: flex; align-items: center; gap: 10px; padding: 9px 8px; border-bottom: 1px solid var(--line); }
    .mrow.next { background: var(--gold-bg); border-radius: 6px; }
    .mrow .btn.sm { min-width: 0; }
    .camp-grid.stacked { grid-template-columns: minmax(0, 1fr); grid-template-areas: "head" "side" "runs"; }
    .stacked .side-card .gblock { display: grid; grid-template-columns: 140px minmax(0, 1fr); align-items: start; }
    .stacked .side-card .gblock .glabel { margin-top: 18px; }
    .stacked .side-card .pipgrid { max-width: 560px; }
    @media (max-width: 1400px) {
      .camp-grid { grid-template-columns: minmax(0, 1fr); grid-template-areas: "head" "side" "runs"; }
      .side-card .gblock { display: grid; grid-template-columns: 140px minmax(0, 1fr); align-items: start; }
      .side-card .gblock .glabel { margin-top: 18px; }
      .side-card .pipgrid { max-width: 560px; }
    }
  </style>`;
  renderCampaign = function () {};
  $("#campBody").innerHTML = css + `<div class="camp-grid${mix || MODE === "empty-n" ? " stacked" : ""}"${mix || MODE === "empty-n" ? ' style="max-width: 900px"' : ""}>${header}<div class="camp-side">${goals}${first}${combos}</div>${runs}</div>`;
  document.querySelector("#main").scrollTop = 0;
})();
