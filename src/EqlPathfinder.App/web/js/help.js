"use strict";
// ---------------------------------------------------------------- help: feedback, your reports and what's coming
// Feedback goes to the community server as written (a name and the error log only if given); the roadmap is the
// server's list with everyone's votes; Your reports is what this copy sent (Report a problem or this form), each with
// what happened to it. All need the network: nothing here works offline, and it says so.
let fbKind = "idea", roadmapItems = null, roadmapError = null, myReports = null, myReportsError = null, myReportsAll = false;
const FB_KINDS = [["idea", "Idea"], ["bug", "Bug"], ["confusing", "Confusing"], ["other", "Other"]];
const ROADMAP_STATUS = { building: "Building now", next: "Next", maybe: "Being considered", shipped: "Shipped" };
/** A report's status (the server's feedback.status, set by hand) in the player's words, with its chip colour. */
const REPORT_STATUS = {
  new: ["Received", ""], read: ["Seen", ""], planned: ["Planned", "xp"], done: ["Done", "good"], wontdo: ["Not planned", ""],
};
const KIND_NAMES = Object.fromEntries(FB_KINDS);

function renderFeedbackKinds() {
  $("#fbKind").innerHTML = `<div class="seggroup" role="group" aria-label="What kind">${FB_KINDS.map(([k, t]) =>
    `<button class="${k === fbKind ? "on" : ""}" aria-pressed="${k === fbKind}" data-fb-kind="${k}">${t}</button>`).join("")}</div>`;
}

function renderRoadmap() {
  const el = $("#roadmap");
  if (roadmapError) { el.innerHTML = `<div class="dim">${esc(roadmapError)}</div>`; return; }
  if (!roadmapItems) { el.innerHTML = `<div class="dim">Loading…</div>`; return; }
  const open = roadmapItems.filter(i => i.status !== "shipped"), shipped = roadmapItems.filter(i => i.status === "shipped");
  const row = i => `<div class="row" style="gap: 12px; align-items: flex-start; padding: 8px 0; border-top: 1px solid var(--line2, #1f2731)">
      <div style="flex: 1; min-width: 0"><div style="font-weight: 600">${esc(i.title)} <span class="chip" style="margin-left: 4px">${esc(ROADMAP_STATUS[i.status] ?? i.status)}</span></div>
        <div class="dim" style="font-size: 12.5px; margin-top: 2px; line-height: 1.45">${esc(i.about)}</div></div>
      <button class="btn${i.mine ? " primary" : ""}" style="flex: none; min-width: 92px" data-vote="${esc(i.id)}" aria-pressed="${i.mine}"
        title="${i.mine ? "Take your vote back" : "Vote for this"}">${i.mine ? "✓ " : "▲ "}${i.votes}</button></div>`;
  el.innerHTML = open.map(row).join("") +
    (shipped.length ? `<div class="dim" style="font-size: 12px; margin-top: 10px">Shipped lately: ${shipped.map(i => `${esc(i.title)} (${esc(i.shipped ?? "")})`).join(" · ")}</div>` : "");
}

/** "Nov 17" from the server's day (yyyy-mm-dd, UTC): a report shows its day, never a time. */
const reportDay = d => { const x = new Date(d + "T12:00:00Z"); return isNaN(x) ? d : x.toLocaleDateString(undefined, { month: "short", day: "numeric" }); };

function renderMyReports() {
  const el = $("#myReports");
  if (myReportsError) { el.innerHTML = `<div class="dim">${esc(myReportsError)}</div>`; return; }
  if (!myReports) { el.innerHTML = `<div class="dim">Loading…</div>`; return; }
  if (!myReports.length) {
    el.innerHTML = `<div class="dim" style="line-height: 1.5">Nothing sent from this copy yet. What you send, from here or from <b>Report</b> on the left,
      shows here with what happened to it, and any note back.</div>`;
    return;
  }
  const shown = myReportsAll ? myReports : myReports.slice(0, 5);
  el.innerHTML = shown.map(r => {
    const [word, tone] = REPORT_STATUS[r.status] ?? [r.status, ""];
    const where = r.page && PAGE_NAMES[r.page] ? ` · ${esc(PAGE_NAMES[r.page])}` : "";
    return `<div class="myrep">
      <div class="between" style="gap: 12px; align-items: flex-start">
        <div style="min-width: 0"><div class="clamp2">${esc(r.text)}</div>
          <div class="dim" style="font-size: 12px; margin-top: 2px">${esc(KIND_NAMES[r.kind] ?? "Other")} · ${esc(reportDay(r.day))}${where}${r.shot ? " · with a screenshot" : ""}</div></div>
        <span class="chip ${tone}" style="flex: none">${esc(word)}</span></div>
      ${r.reply ? `<div class="reply"><span class="gold">Reply:</span> ${esc(r.reply)}</div>` : ""}</div>`;
  }).join("") + (myReports.length > shown.length ? `<button class="link" id="myReportsAll" style="margin-top: 8px">Show all ${myReports.length}</button>` : "");
}

onHost(e => {
  if (e.data?.kind === "roadmap") { roadmapItems = e.data.items ?? null; roadmapError = e.data.error ?? null; renderRoadmap(); }
  if (e.data?.kind === "myReports") { myReports = e.data.items ?? null; myReportsError = e.data.error ?? null; renderMyReports(); }
  if (e.data?.kind === "feedbackSent" && e.data.from !== "report") {
    $("#fbSend").disabled = false;
    $("#fbStatus").textContent = e.data.ok ? "Thanks: it's been sent. It shows under Your reports." : (e.data.error ?? "It didn't send. Try again in a minute.");
    if (e.data.ok) { $("#fbText").value = ""; $("#fbErrors").checked = false; }
  }
  if (e.data?.kind === "feedbackSent" && e.data.ok && page === "help") post("myReports");
});

$("#page-help").addEventListener("click", e => {
  const k = e.target.closest("[data-fb-kind]");
  if (k) { fbKind = k.dataset.fbKind; renderFeedbackKinds(); return; }
  const v = e.target.closest("[data-vote]");
  if (v) { const item = roadmapItems?.find(i => i.id === v.dataset.vote); if (item) post("vote", { item: item.id, on: !item.mine }); return; }
  if (e.target.closest("#myReportsAll")) { myReportsAll = true; renderMyReports(); return; }
  if (e.target.closest("#fbSend")) {
    const text = $("#fbText").value.trim();
    if (!text) { $("#fbStatus").textContent = "Write something first."; $("#fbText").focus(); return; }
    $("#fbSend").disabled = true;
    $("#fbStatus").textContent = "Sending…";
    post("feedback", { kind: fbKind, text, contact: $("#fbContact").value.trim(), errors: $("#fbErrors").checked, page: "help", from: "help" });
  }
});
renderFeedbackKinds();
