"use strict";
// ---------------------------------------------------------------- sharing
// Rendered on its own, not by renderSettings, so the 15-second refresh doesn't reset the JSON you're reading or a code you're typing.
let sharePreview = null, shareBusy = false, shareStatus = null, shareHistory = "all", shareLinkOpen = false, shareConfirm = 0;
const ago = iso => {
  if (!iso) return "";
  const m = Math.max(0, (Date.now() - new Date(iso).getTime()) / 60000);
  return m < 1 ? "just now" : m < 60 ? `${Math.round(m)} min ago` : m < 48 * 60 ? `${fmt(m / 60, 0)} h ago` : `${fmt(m / 1440, 0)} days ago`;
};
const clock = iso => iso ? clockShown(new Date(iso).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })) : "";
function renderShare() {
  const st = shareStatus || {}, p = sharePreview, on = !!st.enabled;
  const keep = id => { const el = document.getElementById(id); return el ? el.value : ""; };
  const enrollValue = keep("shareEnroll"), linkValue = keep("shareLinkCode");
  const never = ["Character names: a code made from a random key on this PC stands in", "Chat, tells, guild and group messages, and other players' names",
    "Mob names, item names, and loot other than motes and fragments", "Exact times (the day only), log files and folders"];
  const c = st.counts || {};
  const crawls = (c.crawl || 0) + (c.instance || 0);
  const chip = on ? (st.paused ? `<span class="chip" style="margin-left: 6px">paused</span>` : `<span class="chip gold" style="margin-left: 6px">on</span>`)
    : `<span class="chip" style="margin-left: 6px">off</span>`;
  const line = st.busy ? (st.pending > 0 ? `Sending ${st.pending.toLocaleString()} more…` : "Talking to the server…")
    : on && st.paused ? "Paused: nothing new is sent. What you shared stays until you delete it."
    : on && st.pending > 0 ? `${st.pending.toLocaleString()} waiting to send.` : "";
  const codeBox = (id, label, value, button, action) => `<div style="display: flex; gap: 8px; align-items: center; margin-top: 10px; flex-wrap: wrap">
      <span class="muted" style="font-size: 12.5px">${label}</span>
      <input id="${id}" type="search" spellcheck="false" autocomplete="off" placeholder="XXXX-XXXX" maxlength="9" value="${esc(value)}"
        style="width: 120px; text-transform: uppercase; letter-spacing: 1px">
      <button class="btn" data-share="${action}">${button}</button></div>`;
  const needEnroll = !on && st.requireEnrollment;
  $("#shareCard").innerHTML = `
    <div class="between" style="align-items: flex-start; gap: 16px">
      <div style="min-width: 0">
        <div class="h">Sharing${chip}</div>
        <div class="muted" style="margin-top: 4px; line-height: 1.5; max-width: 760px">${on
          ? `Your crawls go to the community pool${st.from ? ` from ${esc(st.from)} on` : ""}, checked every 10 minutes. Names, chat and clock times never leave your PC.`
          : `Share your play, and Pathfinder can answer what your own logs can't: the fastest places to level any range, the best mote farms at your level, and how long a relevel takes. Names, chat and clock times never leave your PC.`}</div>
      </div>
      ${on ? "" : `<button class="btn primary" data-share="on"${st.busy ? " disabled" : ""}>${st.busy ? "Turning on…" : "Turn on sharing"}</button>`}
    </div>
    ${on ? `
      <div class="tiles" style="margin: 14px 0 6px">
        <div class="stat"><div class="v num">${crawls.toLocaleString()}</div><div class="k">crawls and instances shared</div></div>
        <div class="stat"><div class="v num">${(c.stretch || 0).toLocaleString()}</div><div class="k">open-world stretches</div></div>
        <div class="stat"><div class="v num">${c.reincarnation || 0} · ${c.relevel || 0}</div><div class="k">reincarnations · relevels</div></div>
        <div class="stat"><div class="v num">${st.pcs || 1}</div><div class="k">${(st.pcs || 1) > 1 ? "linked PCs" : "PC"}</div></div>
        <div class="stat"><div class="v num" style="font-size: 18px">${st.lastUpload ? ago(st.lastUpload) : "not yet"}</div><div class="k">last upload</div></div>
      </div>
      ${line ? `<div class="dim" style="font-size: 12.5px">${esc(line)}</div>` : ""}
      <div style="display: flex; gap: 8px; flex-wrap: wrap; margin-top: 12px">
        ${st.paused ? `<button class="btn" data-share="resume">Resume</button>` : `<button class="btn" data-share="pause">Pause</button>`}
        <button class="btn" data-share="sync"${st.busy || st.paused ? " disabled" : ""}>Send now</button>
        <button class="btn" data-share="link"${st.busy ? " disabled" : ""}>Link another PC</button>
        ${st.requireEnrollment ? `<button class="btn" data-share="enroll"${st.busy ? " disabled" : ""}>Code for a friend</button>` : ""}
        <button class="btn" data-share="delete" style="border-color: var(--warn); color: var(--warn)"${st.busy ? " disabled" : ""}>${shareConfirm > Date.now() ? "Click again to delete everything" : "Delete everything I shared"}</button>
      </div>
      ${st.link ? `<div class="card" style="margin-top: 12px; padding: 12px 14px; background: var(--inset)">
        <div>Link code <b class="num gold" style="font-size: 18px; letter-spacing: 1px; margin: 0 6px">${esc(st.link.code)}</b> <span class="dim">good until ${clock(st.link.expires)}, once</span></div>
        <div class="muted" style="font-size: 12.5px; margin-top: 4px">On your other PC, open Settings → Sharing, choose "I already share from another PC", and enter it. Both PCs then count as one player, and deleting covers both.</div></div>` : ""}
      ${st.enroll ? `<div class="card" style="margin-top: 12px; padding: 12px 14px; background: var(--inset)">
        <div>Invite code <b class="num gold" style="font-size: 18px; letter-spacing: 1px; margin: 0 6px">${esc(st.enroll.code)}</b> <span class="dim">good until ${clock(st.enroll.expires)} tomorrow, once</span></div>
        <div class="muted" style="font-size: 12.5px; margin-top: 4px">Give it to one friend. They enter it when they turn sharing on.</div></div>` : ""}
      ${shareLinkOpen ? codeBox("shareLinkCode", "Share as the same player as my other PC:", linkValue, "Link this PC", "claim")
        : `<div style="margin-top: 10px"><button class="chip" data-share="openLink">This PC should count as my other PC</button></div>`}`
    : `
      <div style="display: flex; gap: 10px; align-items: center; margin-top: 14px; flex-wrap: wrap">
        <span class="muted" style="font-size: 12.5px">What to share:</span>
        <div class="seggroup" role="group" aria-label="What to share">
          <button class="${shareHistory === "all" ? "on" : ""}" aria-pressed="${shareHistory === "all"}" data-share-history="all">Everything in my logs</button>
          <button class="${shareHistory === "new" ? "on" : ""}" aria-pressed="${shareHistory === "new"}" data-share-history="new">Only play from now on</button>
        </div>
      </div>
      ${needEnroll ? codeBox("shareEnroll", "Invite code from a guildmate who shares:", enrollValue, "Turn on with this code", "on") : ""}
      ${shareLinkOpen ? codeBox("shareLinkCode", "Link code from your other PC:", linkValue, "Link this PC", "onLink")
        : `<div style="margin-top: 10px"><button class="chip" data-share="openLink">I already share from another PC</button></div>`}`}
    ${st.error ? `<div class="warn" style="font-size: 12.5px; margin-top: 10px">${esc(st.error)}</div>` : ""}
    ${st.notice ? `<div class="good" style="font-size: 12.5px; margin-top: 10px">${esc(st.notice)}</div>` : ""}
    <details style="margin-top: 16px"${p ? " open" : ""}>
      <summary class="lbl" style="cursor: pointer">See exactly what's sent</summary>
      <div class="between" style="align-items: center; margin-top: 10px">
        <div class="dim" style="font-size: 12px">${on ? "The full set the server holds once everything is sent." : "Building it reads your history; it takes a moment. Nothing is sent."}</div>
        <button class="btn" data-share="build"${shareBusy ? " disabled" : ""}>${shareBusy ? "Building…" : p ? "Rebuild" : "Build the preview"}</button>
      </div>
      ${p ? `
        <div class="tiles" style="margin: 14px 0 10px">
          <div class="stat"><div class="v num">${p.crawls}</div><div class="k">crawls and instances</div></div>
          <div class="stat"><div class="v num">${p.kills.toLocaleString()}</div><div class="k">kills, each one</div></div>
          <div class="stat"><div class="v num">${p.stretches}</div><div class="k">open-world stretches</div></div>
          <div class="stat"><div class="v num">${p.reincarnations} · ${p.relevels}</div><div class="k">reincarnations · relevels</div></div>
          <div class="stat"><div class="v num">${fmt(p.compressed / 1024, 0)} KB</div><div class="k">compressed (${fmt(p.bytes / 1024, 0)} KB as JSON)</div></div>
        </div>
        <div class="muted" style="font-size: 12.5px; line-height: 1.55; max-width: 900px">Each crawl carries the facts it's made of: every kill (seconds into the crawl, XP %, solo or party, how long each kill took, level), crawl rewards, dings and deaths, damage per spell or ability (the game's names), the spells you began casting, and motes and fragments by tier. Hunts in the open world carry their kills the same way. The community numbers are worked out from these facts each time they're rebuilt, the same way for everyone's play, whichever version of Pathfinder sent it.</div>
        <div class="dim" style="font-size: 12px; line-height: 1.5; margin-top: 6px">${p.since ? `Play from ${esc(p.since)} on.` : "Everything in your logs."} Instead of clock times, rows have a day and an order. ${esc(data?.character)} appears as <span class="num" style="color: var(--soft)">${esc(p.character)}</span>, the same code every time, which can't be turned back into a name.</div>
        <div class="lbl" style="margin-top: 14px">Never sent</div>
        <ul class="muted" style="margin: 6px 0 0 18px; padding: 0; line-height: 1.6">${never.map(x => `<li>${esc(x)}</li>`).join("")}</ul>
        <div class="between" style="margin-top: 14px; align-items: center"><div class="lbl">The data</div><button class="btn" data-share="copy">Copy</button></div>
        <pre id="shareJson" tabindex="0" style="margin: 8px 0 0; max-height: 380px; overflow: auto; background: var(--inset); border: 1px solid var(--line); border-radius: 10px; padding: 12px 14px; font-size: 12px; line-height: 1.45; color: var(--soft)">${esc(p.text)}</pre>` : ""}
    </details>`;
}
const shareCode = id => (document.getElementById(id)?.value || "").trim();
function syncShareCard() {
  const show = !!data?.settings;
  const card = $("#shareCard");
  if (card.hidden === !show) return;
  card.hidden = !show;
  if (show) { renderShare(); post("share", { action: "status" }); }
}
$("#shareCard").addEventListener("click", async e => {
  const h = e.target.closest("[data-share-history]");
  if (h) { shareHistory = h.dataset.shareHistory; renderShare(); return; }
  const b = e.target.closest("[data-share]");
  if (!b) return;
  const a = b.dataset.share;
  if (a === "build") { shareBusy = true; renderShare(); post("sharePreview"); }
  else if (a === "copy" && sharePreview) {
    try { await navigator.clipboard.writeText(sharePreview.text); b.textContent = "Copied"; }
    catch { const r = document.createRange(); r.selectNodeContents($("#shareJson")); getSelection().removeAllRanges(); getSelection().addRange(r); b.textContent = "Selected: press Ctrl+C"; }
    setTimeout(() => { b.textContent = "Copy"; }, 2500);
  }
  else if (a === "openLink") { shareLinkOpen = true; renderShare(); document.getElementById("shareLinkCode")?.focus(); }
  else if (a === "on") post("share", { action: "on", history: shareHistory, enroll: shareCode("shareEnroll") });
  else if (a === "onLink") { const code = shareCode("shareLinkCode"); if (code) post("share", { action: "on", history: shareHistory, link: code }); }
  else if (a === "claim") { const code = shareCode("shareLinkCode"); if (code) { shareLinkOpen = false; post("share", { action: "claim", code }); } }
  else if (a === "delete") {
    if (shareConfirm > Date.now()) { shareConfirm = 0; post("share", { action: "delete" }); }
    else { shareConfirm = Date.now() + 6000; renderShare(); setTimeout(renderShare, 6100); }
  }
  else post("share", { action: a });
});
renderShare();

