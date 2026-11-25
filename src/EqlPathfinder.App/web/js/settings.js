"use strict";
// ---------------------------------------------------------------- settings
const sw = (key, on, label, hint) => `<div class="setrow"><div><div>${label}</div>${hint ? `<div class="dim" style="font-size: 12px">${hint}</div>` : ""}</div>
  <button class="switch" role="switch" aria-checked="${!!on}" aria-label="${esc(label)}" data-setting="${key}" data-bool="${!on}"></button></div>`;
const seg = (key, value, options, label, hint) => `<div class="setrow"><div><div>${label}</div>${hint ? `<div class="dim" style="font-size: 12px">${hint}</div>` : ""}</div>
  <div class="seggroup" role="group" aria-label="${esc(label)}">${options.map(o => { const [v, t] = Array.isArray(o) ? o : [o, o];
    return `<button class="${v === value ? "on" : ""}" aria-pressed="${v === value}" data-setting="${key}" data-value="${v}">${t}</button>`; }).join("")}</div></div>`;

function renderSettings() {
  const s = data.settings;
  if (!s) return;
  syncShareCard();
  // "Not sure" follows the recommended fragment value; any custom value saved previously stays selectable.
  const frags = [["recommended", `Not sure: recommended · ${s.fragmentRecommendedValue ?? 3.56}× today`],
    ...(s.fragmentChoices ?? []).map(c =>
      [c.value, c.goal >= 7 ? `+${c.goal} · a fragment is ${c.value}× a mote` : `+${c.goal} or lower, or spells only · ${c.value.toFixed(1)}×`])];
  if (!s.fragmentRecommended && !frags.some(([v]) => v === s.fragmentValue)) frags.push([s.fragmentValue, `${s.fragmentValue}× · set before`]);
  const fragOn = v => v === "recommended" ? !!s.fragmentRecommended : !s.fragmentRecommended && Math.abs(v - s.fragmentValue) < 0.001;
  const server = data.server ? data.server[0].toUpperCase() + data.server.slice(1) : "";
  const update = s.pendingUpdate
    ? `<div class="setrow"><div><div>Version ${esc(s.pendingUpdate)} is ready</div><div class="dim" style="font-size: 12px">It installs by itself once the game is closed and Pathfinder is in the background, or now.</div></div><button class="btn primary" data-action="restartUpdate">Restart to update</button></div>`
    : `<div class="setrow"><div><div>${s.updatesEnabled ? "Pathfinder checks for updates by itself" : "This copy can't update itself"}</div><div class="dim" style="font-size: 12px">${s.updatesEnabled ? "Shortly after it starts and every 3 hours." : "It wasn't installed with Setup, or has no update source."}</div></div>${s.updatesEnabled ? '<button class="btn" data-action="checkUpdates">Check now</button>' : ""}</div>`;
  // Difficulties farmed for motes (judged by T5-eq/h; others by levels/h). Verdicts fade until refreshed.
  const farmed = s.farmDifficulties ?? [3, 4];
  $("#settingsBody").innerHTML = `
    <section class="card" id="howYouPlay" style="grid-column: 1 / -1"><div class="h">How you play</div>
      <div class="setrow"><div><div>Which difficulties do you farm for motes?</div>
        <div class="dim" style="font-size: 12px; line-height: 1.5; max-width: 640px">Pathfinder judges a visit at these by T5-eq per hour against your usual there, and the rest as leveling, by levels per hour. Grouped play, instances you never finished and play at ${cap()} are always judged by T5-eq per hour. To rank places your own way, use Ask.</div></div>
        <div class="seggroup" role="group" aria-label="Difficulties you farm for motes">${[0, 1, 2, 3, 4].map(d => { const on = farmed.includes(d);
          return `<button class="${on ? "on" : ""}" aria-pressed="${on}" data-farm-diff="${d}">${on ? "✓ " : ""}D${d}</button>`; }).join("")}</div></div>
    </section>
    ${overlaysCard(s)}
    <section class="card"><div class="h">Window</div>
      ${seg("startWindow", s.startWindow ?? "Open", [["Open", "Open it"], ["Minimized", "Minimized"], ["Tray", "Tray only"]], "When Pathfinder starts",
        "Minimized waits on the taskbar. Tray only keeps just the compass by the clock: right-click it and choose Open, or launch Pathfinder again.")}
      <div class="setrow"><div><div>Closing the window</div><div class="dim" style="font-size: 12px">Pathfinder keeps tracking in the tray. Exit from the tray menu.</div></div></div>
    </section>
    <section class="card"><div class="h">Counting</div>
      <div class="setrow" id="fragmentSetting"><div><div>What's a fragment worth?${infoTip("fragment")}</div><div class="dim" style="font-size: 12px">Not sure? Keep Recommended: take gear to +9 with motes and fragments, and save Void-Touched
        Potentials for +9 → +10, the costliest tier. It values fragments the way Upgrades spends them. If you take gear higher or lower with motes, pick that instead. Used on Loot, Ask, Home and the drop banner.</div></div>
        <select data-setting-select="fragmentValue" aria-label="How high you take mote-fed gear">${frags.map(([v, t]) => `<option value="${v}"${fragOn(v) ? " selected" : ""}>${esc(t)}</option>`).join("")}</select></div>
      <div id="farmRateSetting">
      ${seg("farmRateMode", s.farmRateMode ?? "best", [["best", "Your best farm"], ["usual", "Your typical"], ["manual", "Set it"]], "Farm rate",
        "What a level costs is measured against this: the motes you'd farm in the same time at the cap. Ask and Upgrades use it too.")}
      <div class="dim${farmWaiting ? " stale" : ""}" style="font-size: 12px; line-height: 1.5; padding-bottom: 8px">${farmLine()}${data.reinc?.farmSource?.kind === "default" ? " Pick Set it to use your own." : ""}</div>
      ${(s.farmRateMode ?? "best") === "manual" ? `<div class="setrow"><div><div>Your farm rate</div>${(data.reinc?.farm?.crawls ?? 0) >= (data.reinc?.farmSource?.needs ?? Infinity) ? "" : `<div class="dim" style="font-size: 12px">Also used until ${farmNeeds()} of your ${farmWords(farmCounted())} crawls at ${cap()} since the patch have a rate.</div>`}</div>
        <span class="muted" style="display: flex; gap: 6px; align-items: center; white-space: nowrap; flex: none"><input type="number" class="farmRate" min="1" max="500" step="1" style="width: 64px" value="${s.farmRate}" aria-label="Farm rate"> T5-eq/h</span></div>` : ""}
      </div>
      ${sw("sound", s.sound, "Sound on drops above T5")}
    </section>
    <section class="card"><div class="h">Updates</div>
      <div class="setrow"><div><div>Version</div></div><span class="num muted">${esc(s.version ?? "development build")}</span></div>
      ${update}
      ${sw("beta", s.beta, "Join the beta", `Try new features a few days before everyone else. Beta builds arrive as well as regular ones and can have rough edges. Leave any time: you keep your build until a regular release passes it.${s.onBetaBuild ? " You're on a beta build now." : ""}`)}
      <div class="setrow"><div><div>Open source</div><div class="dim" style="font-size: 12px">MIT license. Made by Venun.</div></div>
        <span class="row" style="gap: 14px"><a class="link" href="https://eqlpathfinder.com" target="_blank" rel="noopener">Website</a> · <a class="link" href="https://eqlpathfinder.com/github" target="_blank" rel="noopener">GitHub</a> · <a class="link" href="https://eqlpathfinder.com/discord" target="_blank" rel="noopener">Discord</a></span></div>
    </section>
    <section class="card"><div class="h">Character and data</div>
      <div class="setrow"><div style="min-width: 0"><div>Tracking <b data-private>${esc(data.character)}</b> on ${esc(server)}</div><div class="dim ellip" data-private style="font-size: 12px; max-width: 360px" title="${esc(s.logsDir)}">${esc(s.logsDir ?? "")}</div></div>
        <button class="btn" data-action="changeCharacter">Change or re-import…</button></div>
      <div class="setrow"><div><div>Your data stays on this PC</div><div class="dim" style="font-size: 12px">Settings, history database and error log.${s.shareEnabled ? " Sharing sends only what Settings → Sharing shows." : ""}${s.usage ? " Usage counts, below, are the only other thing sent." : ""}</div></div>
        <button class="btn" data-action="openData">Open data folder</button></div>
      ${sw("usage", s.usage, "Send anonymous usage counts",
        "Which pages you open, which Ask questions and features you use, which notices you answer, setup steps, starts and updates, how long Pathfinder runs with and without the game, how long your log took to read and its size, its version, stable or beta, your Windows version and the day this copy first counted, which settings are on (sharing, beta, overlays, start window, goal, self-update), and errors (their kind and where in Pathfinder, never a message or a file path). One summary per day, kept up to date while Pathfinder runs, under a random id of its own. Never your logs, character or play. Switching it off deletes what this copy sent.")}
      ${s.usage ? `<div class="setrow"><div><div>See exactly what's sent</div><div class="dim" style="font-size: 12px">Today's summary, as it would go now.</div></div>
        <button class="btn" data-usage="preview">${usagePreview ? "Refresh" : "Show"}</button></div>
        ${usagePreview ? `<pre tabindex="0" style="margin: 4px 0 8px; max-height: 320px; overflow: auto; background: var(--inset); border: 1px solid var(--line); border-radius: 10px; padding: 12px 14px; font-size: 12px; line-height: 1.45; color: var(--soft)">${esc(usagePreview)}</pre>` : ""}` : ""}
    </section>`;
}
// Settings → See what's sent: today's usage summary as the app would send it (null until asked).
let usagePreview = null;
$("#page-help").addEventListener("click", e => {
  const a = e.target.closest("[data-action]");
  if (a) post("action", { value: a.dataset.action });
});
$("#settingsBody").addEventListener("click", e => {
  const a = e.target.closest("[data-action]");
  if (a) { post("action", { value: a.dataset.action }); return; }
  if (e.target.closest("[data-usage]")) { post("usagePreview"); return; }
  const fd = e.target.closest("[data-farm-diff]");
  if (fd) {
    const d = Number(fd.dataset.farmDiff), now = data.settings.farmDifficulties ?? [3, 4];
    const next = (now.includes(d) ? now.filter(x => x !== d) : [...now, d]).sort((a, b) => a - b);
    data.settings.farmDifficulties = next;
    farmWaiting = true;
    post("setting", { key: "farmDifficulties", value: next });
    renderSettings(); renderHome();
    $(`[data-farm-diff="${d}"]`)?.focus();
    return;
  }
  const b = e.target.closest("[data-setting]");
  if (!b) return;
  const key = b.dataset.setting, value = b.dataset.bool != null ? b.dataset.bool === "true" : b.dataset.value;
  if (key === "usage") usagePreview = null;
  // An overlay's switch ("overlay.<id>.<what>") changes that overlay here at once; the app's answer follows.
  if (key.startsWith("overlay.")) {
    const [, id, what] = key.split("."), o = data.settings.overlays?.find(x => x.id === id);
    if (o) { if (what in o) o[what] = what === "opacity" ? Number(value) : value; else o.options[what] = value; }
  }
  else if (key === "overlaysLocked") data.settings.overlays?.forEach(o => o.locked = value);
  else data.settings[key] = value;
  post("setting", { key, value });
  renderSettings();
  $(`[data-setting="${key}"]${b.dataset.value ? `[data-value="${b.dataset.value}"]` : ""}`)?.focus();
});
$("#settingsBody").addEventListener("change", e => {
  const sel = e.target.closest("[data-setting-select]");
  if (!sel) return;
  const key = sel.dataset.settingSelect;
  if (key === "fragmentValue" && sel.value === "recommended") {   // follows the app's recommendation
    Object.assign(data.settings, { fragmentRecommended: true, fragmentValue: data.settings.fragmentRecommendedValue ?? 3.56 });
    post("setting", { key, value: "recommended" });
    return;
  }
  data.settings[key] = Number(sel.value);
  if (key === "fragmentValue") data.settings.fragmentRecommended = false;
  post("setting", { key, value: Number(sel.value) });
});

// The farm rate typed in Settings (Ask counts motes against it).
document.addEventListener("change", e => {
  if (e.target.classList?.contains("farmRate")) { const v = Number(e.target.value); if (v > 0 && v <= 1000) post("farmRate", { value: v }); }
});

// Every overlay in one card: what applies to all of them, then each one's switch, lock, background and own choices.
const OVERLAY_BG = [[1, "100%"], [0.9, "90%"], [0.8, "80%"], [0.65, "65%"], [0.5, "50%"]];
// The fight meter's "A fight ends" choices, in seconds; a saved value that isn't one shows as the nearest.
const FIGHT_END = ["3", "5", "8", "10", "15", "30"];
const nearestEnd = x => FIGHT_END.reduce((a, v) => Math.abs(v - (Number(x) || 8)) < Math.abs(a - (Number(x) || 8)) ? v : a, "8");
function overlaysCard(s) {
  const nearest = x => String(OVERLAY_BG.reduce((a, [v]) => Math.abs(v - x) < Math.abs(a - x) ? v : a, 1));
  return `<section class="card"><div class="h">Overlays</div>
      ${sw("overlaysOnlyWithGame", s.overlaysOnlyWithGame, "Only while EverQuest Legends is running", "Hidden the rest of the time.")}
      ${sw("overlaysLocked", s.overlaysLocked, "Lock the overlays", "Clicks pass through to the game. Double-click the tray compass to switch.")}
      <div class="setrow"><div><div>Arrange the overlays</div><div class="dim" style="font-size: 12px">Every overlay that's on shows, unlocked and named, game running or not, so you can drag them where you want them.</div></div>
        <button class="btn${s.arranging ? " primary" : ""}" data-setting="arrangeOverlays" data-bool="${!s.arranging}">${s.arranging ? "Done arranging" : "Arrange"}</button></div>
      ${(s.overlays ?? []).map(o => `
        <div${o.id === "meter" ? ' id="meterSettings"' : ""}><div class="lbl" style="margin-top: 14px">${esc(o.name)}</div>
        ${sw(`overlay.${o.id}.visible`, o.visible, `Show the ${esc(o.name.toLowerCase())}`, esc(o.about))}
        ${o.visible ? `${sw(`overlay.${o.id}.locked`, o.locked, "Lock it", "Clicks pass through to the game.")}
          ${seg(`overlay.${o.id}.opacity`, nearest(o.opacity), OVERLAY_BG.map(([v, t]) => [String(v), t]), "Background", "The text stays solid.")}
          ${o.id === "crawl" ? `${seg("overlay.crawl.size", o.options.size ?? "Compact", ["Compact", "Standard", "Detailed"], "Size", "Double-click the overlay to switch Compact and Standard.")}
            ${seg("overlay.crawl.mode", o.options.mode ?? "Auto", ["Auto", "Leveling", "Farming"], "Mode", "Auto: leveling below the cap, farming at it.")}` : ""}
          ${o.id === "meter" ? seg("overlay.meter.clock", o.options.clock ?? "active", [["active", "Active time"], ["kill", "Kill time"]], "DPS counts",
            "Active time: only the seconds you were fighting. Kill time: first swing to the death, feigns and waits included. The other one shows under the bars; click it there to swap.") : ""}
          ${o.id === "meter" ? seg("overlay.meter.fightEnd", nearestEnd(o.options.fightEnd), FIGHT_END.map(v => [v, `${v} s`]), "A fight ends",
            "After a mob that hasn't died goes this long with nothing hitting it or you; another one with the same name after that is a new fight. Below 5 s a pause mid-fight (a DoT ticks every 6) starts cutting kills in two.") : ""}` : ""}</div>`).join("")}
    </section>`;
}
