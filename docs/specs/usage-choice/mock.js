// Mock-up of the usage-choice step, drawn on the tour's own page (no app run).
(() => {
  const state = new URLSearchParams(location.search).get("state") || "choose";
  const css = document.createElement("style");
  css.textContent = `
    .progress .seg { flex: 1 1 auto; }
    .usage .choice { margin-top: 16px; }
    .usage .opt { display: grid; grid-template-columns: 18px minmax(0, 1fr); gap: 10px; align-items: start; }
    .usage .opt .radio { width: 14px; height: 14px; margin-top: 3px; border-radius: 50%; border: 1.5px solid #5a6572; }
    .usage .opt.sel .radio { border-color: var(--gold); box-shadow: inset 0 0 0 3px #1f2731; background: var(--gold); }
    .usage .what { margin-top: 12px; display: grid; grid-template-columns: 1.25fr 1fr; gap: 0 18px; }
    .usage .what .sublbl { margin: 2px 0 4px; }
    .usage .what ul { margin: 0; padding: 0; display: grid; gap: 1px; }
    .usage .what li { list-style: none; font-size: 12.5px; color: var(--soft); display: flex; gap: 7px; }
    .usage .what li::before { content: "▸"; color: var(--gold-dim); }
    .usage .what .never li { color: var(--muted); }
    .usage .what .never li::before { content: "✕"; color: var(--faint); font-size: 11px; }
    .usage .seebar { margin-top: 10px; }
    .usage pre { margin: 6px 0 0; max-height: 150px; overflow: auto; background: var(--inset); border: 1px solid var(--line); border-radius: 10px;
      padding: 10px 12px; font: 11.5px/1.45 "Cascadia Mono", Consolas, monospace; color: var(--soft); }
    .summ { width: 250px; border-radius: 12px; background: #141920eb; border: 1px solid #2a333e; box-shadow: 0 18px 50px #000b; padding: 12px 14px; font-size: 12px; }
    .summ .h { display: flex; justify-content: space-between; color: var(--text); font-weight: 650; margin-bottom: 6px; }
    .summ .h span:last-child { color: var(--muted); font-weight: 500; }
    .summ .r { display: flex; justify-content: space-between; padding: 3px 0; border-top: 1px solid #252e38; color: var(--soft); font-variant-numeric: tabular-nums; }
    .summ .r b { color: var(--gold); font-weight: 600; }
    .summ .x { color: #6d7680; text-decoration: line-through; }
    .summ .x b { color: #6d7680; }
  `;
  document.head.appendChild(css);

  const picked = state === "choose" ? null : "yes";
  const open = state === "preview";
  const sec = document.createElement("section");
  sec.className = "step usage";
  sec.dataset.step = "usage";
  sec.innerHTML = `
    <div class="eyebrow">Usage counts</div>
    <h1>Send anonymous usage counts?</h1>
    <p class="lead">Once a day, a short summary of how Pathfinder ran: pages opened, minutes, errors. It shows what to fix first. Nothing is sent unless you say yes.</p>
    <div class="choice" role="radiogroup" aria-label="Usage counts">
      <button class="opt${picked === "yes" ? " sel" : ""}" type="button" role="radio" aria-checked="${picked === "yes"}"><span class="radio"></span>
        <span><b>Yes, send them</b><span>Turn it off any time in Settings, and what was sent is deleted.</span></span></button>
      <button class="opt${picked === "no" ? " sel" : ""}" type="button" role="radio" aria-checked="${picked === "no"}"><span class="radio"></span>
        <span><b>No, don't send</b><span>Nothing is counted or sent. Turn it on any time in Settings.</span></span></button>
    </div>
    <div class="card what">
      <div><div class="sublbl">In the summary</div><ul>
        <li>Its own random id, not your sharing key</li>
        <li>The date, Pathfinder and Windows versions</li>
        <li>Pages opened, Ask questions, setup steps</li>
        <li>Settings on: sharing, beta, overlays</li>
        <li>Minutes run, with and without the game</li>
        <li>Errors: where, never the message</li></ul></div>
      <div class="never"><div class="sublbl">Never in it</div><ul>
        <li>Your log, or any line from it</li>
        <li>Character, server or guild names</li>
        <li>Chat, your play's numbers, file paths</li>
        <li>The time of day: only the date</li>
        <li>Your IP address: not kept</li></ul>
        <div class="seebar"><button class="link" type="button">${open ? "Hide what's sent" : "See exactly what's sent"}</button></div></div>
    </div>
    ${open ? `<p class="small" style="margin: 12px 0 0">Today's summary, as it would go now:</p><pre>{
  "id": "3f9c0d1e7a4b4c21a8e5d06b9f12ce47",
  "day": "2026-11-24",
  "app": "0.10.31",
  "channel": "stable",
  "os": "10.0.26300.0",
  "firstSeen": "2026-11-24",
  "counts": {
    "life.start": 1,
    "feature.setup-welcome": 1,
    "feature.setup-usage": 1
  },
  "state": { "sharing": "off", "beta": "off", "overlays": "shown" }
}</pre>` : ""}`;
  document.querySelector('[data-step="welcome"]').after(sec);

  const art = document.createElement("div");
  art.className = "scene";
  art.dataset.art = "usage";
  art.innerHTML = `<div class="summ"><div class="h"><span>Today's summary</span><span>Nov 24</span></div>
    <div class="r"><span>Home opened</span><b>4</b></div>
    <div class="r"><span>Ask questions</span><b>2</b></div>
    <div class="r"><span>Setup steps</span><b>3</b></div>
    <div class="r"><span>Minutes, game on</span><b>96</b></div>
    <div class="r x"><span>Character name</span><b>never</b></div>
    <div class="r x"><span>Chat, log lines</span><b>never</b></div>
    <div class="r x"><span>Time of day</span><b>never</b></div></div>`;
  document.querySelector('.scene[data-art="welcome"]').after(art);

  STEPS.splice(1, 0, { key: "usage", label: "Usage" });
  flow = STEPS.map(s => s.key);
  at = 1;
  render();
  if (open) setTimeout(() => { $("#content").scrollTop = 9999; }, 50);
  if (!picked) {
    $("#next").disabled = true;
    $("#where").textContent = "Step 2 of 7 · Choose Yes or No to go on";
  }
})();
