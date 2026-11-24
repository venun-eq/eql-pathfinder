"use strict";
// ---------------------------------------------------------------- one-time notices
// A banner across the top of Home for news an update brings that people should see once: shown until any of its
// buttons is clicked, then never again on this copy (settings NoticesSeen). New installs are marked as having seen
// what the welcome tour already told them. To add one: an entry here (newest last), and the app shows the first
// one not yet seen whose "when" holds.
const NOTICES = [
  {
    id: "usage-counts",
    when: s => s.usage === true,
    icon: '<path d="M3 3v18h18"/><path d="M7 15l4-4 3 3 5-6"/>',
    title: "New: Pathfinder sends anonymous usage counts",
    text: "A daily summary: which pages you open, which Ask questions, how long it runs, your version, and crashes. Never your logs, " +
      "character or play, and never a file path. It shows us what crashes and which pages earn their place.",
    buttons: [["see", "See what's sent"], ["off", "Turn off"], ["ok", "OK", true]],
  },
];

// Settings → the usage switch scrolls into view once its preview arrives (core.js).
let usageScroll = false;

function renderHomeNotice() {
  const s = data?.settings, el = $("#homeNotice");
  if (!el) return;
  const n = s ? NOTICES.find(x => !(s.noticesSeen ?? []).includes(x.id) && x.when(s)) : null;
  el.hidden = !n;
  if (!n) { el.dataset.id = ""; return; }
  if (el.dataset.id === n.id) return;   // already drawn: don't redraw under the pointer
  el.dataset.id = n.id;
  el.innerHTML = `
    <svg class="ni" width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${n.icon}</svg>
    <div class="nt"><b>${esc(n.title)}</b><div class="muted">${esc(n.text)}</div></div>
    <div class="nb">${n.buttons.map(([a, label, primary]) => `<button class="btn${primary ? " primary" : ""}" data-notice="${a}">${esc(label)}</button>`).join("")}</div>`;
}

$("#homeNotice").addEventListener("click", e => {
  const b = e.target.closest("[data-notice]");
  const id = $("#homeNotice").dataset.id;
  if (!b || !id) return;
  const action = b.dataset.notice;
  post("notice", { id, action });
  data.settings.noticesSeen = [...(data.settings.noticesSeen ?? []), id];
  if (id === "usage-counts" && action === "off") { post("setting", { key: "usage", value: false }); data.settings.usage = false; renderSettings(); }
  if (id === "usage-counts" && action === "see") { usageScroll = true; show("settings"); post("usagePreview"); }
  renderHomeNotice();
});
