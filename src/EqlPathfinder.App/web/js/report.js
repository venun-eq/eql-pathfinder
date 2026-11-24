"use strict";
// ---------------------------------------------------------------- Report a problem
// From any page: Report on the rail, or Ctrl+Shift+R. Before the window takes its picture (MainWindow.ReportShotAsync:
// the page alone, never the desktop, the game or the title bar), the page hides what never leaves this PC: the
// character's name, every clock time it has shown (core.js shownClocks) and anything else written like a time, folders,
// and what's typed in the feedback boxes. Messages from the window wait meanwhile (core.js heldMessages), so nothing
// redraws unhidden; then everything is put back. The panel shows the picture before anything goes: the player can
// untick it, highlight a spot or black out more, and sees every detail sent with it. Help → Your reports shows what
// happened to each report afterwards.

const REPORT_KINDS = [["bug", "Bug"], ["confusing", "Confusing"], ["idea", "Idea"], ["other", "Other"]];
const REPORT_PROMPTS = {
  bug: "What went wrong, and what did you expect?", confusing: "What was confusing?", idea: "What would help here?", other: "What's on your mind?",
};
/** A screenshot's most bytes (FeedbackClient.MaxShot, the server's MAX_SHOT), with room to spare. */
const REPORT_MAX_SHOT = 1_490_000;
const BLOCK = "█";

const rp = {
  kind: "bug", text: "", errors: false, keep: true, shot: null, marks: [], tool: "mark", draft: null,
  details: "", page: "home", view: "form", busy: false, status: "", sentShot: null,
};
let rpOpening = false, rpShotWaiter = null;

const reEsc = s => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

/**
 * Hides, in place on the page being shown, what never leaves this PC; returns how to put it all back. Anything that only
 * looks like a time (a colon between digits) goes too: hiding a little too much is the safe side.
 */
function hidePrivate() {
  const names = [data?.character, $("#fbContact")?.value.trim()].filter(n => n && n.length >= 2);
  const parts = [
    ...names.map(reEsc),
    ...[...shownClocks].sort((a, b) => b.length - a.length).map(reEsc),
    String.raw`\b\d{1,2}:\d{2}(?::\d{2})?(?:\s?[AaPp]\.?[Mm]\.?)?`,
    String.raw`\b[A-Za-z]:\\[^\s<>"|?*]*`, String.raw`\\\\[^\s<>"|?*]+`,
  ];
  const re = new RegExp(parts.join("|"), "gi");
  const has = s => { re.lastIndex = 0; return re.test(s); };
  const hide = s => s.replace(re, m => BLOCK.repeat(Math.min(m.length, 24)));
  const undo = [];
  const root = $("#page-" + page) ?? $("#main");
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
    acceptNode: n => n.parentElement?.closest("script, style") ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT,
  });
  for (let n = walker.nextNode(); n; n = walker.nextNode()) {
    const was = n.nodeValue;
    if (!has(was)) continue;
    const now = hide(was);
    n.nodeValue = now;
    undo.push(() => { if (n.nodeValue === now) n.nodeValue = was; });
  }
  for (const box of root.querySelectorAll("input:not([type]), input[type=text], input[type=search], textarea")) {
    const was = box.value, whole = box.id === "fbText" || box.id === "fbContact";
    if (!was || (!whole && !has(was))) continue;
    const now = whole ? BLOCK.repeat(Math.min(was.length, 40)) : hide(was);
    box.value = now;
    undo.push(() => { if (box.value === now) box.value = was; });
  }
  for (const el of root.querySelectorAll("[data-private]")) { el.classList.add("pv-hidden"); undo.push(() => el.classList.remove("pv-hidden")); }
  const tip = $("#tip");
  if (tip && !tip.hidden) { tip.hidden = true; undo.push(() => { tip.hidden = false; }); }
  return () => undo.reverse().forEach(f => f());
}

const frames = n => new Promise(done => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : done(); step(n); });
const loadImage = src => new Promise(done => { const i = new Image(); i.onload = () => done(i); i.onerror = () => done(null); i.src = src; });

/** The window's picture of the page as it is now, with its details; null when it can't be had. */
function askShot() {
  return new Promise(done => {
    if (!window.chrome?.webview) { done(null); return; }
    const t = setTimeout(() => { rpShotWaiter = null; done(null); }, 6000);
    rpShotWaiter = d => { clearTimeout(t); done(d); };
    post("reportShot");
  });
}
onHost(e => {
  if (e.data?.kind === "reportShot" && rpShotWaiter) { const w = rpShotWaiter; rpShotWaiter = null; w(e.data); }
  if (e.data?.kind === "feedbackSent" && e.data.from === "report") {
    rp.busy = false;
    if (e.data.ok) {
      Object.assign(rp, { view: "sent", sentShot: e.data.shot ?? null, text: "", errors: false, status: "", marks: [] });
      if ($("#report").open) renderReport();
    } else {
      rp.status = e.data.error ?? "It didn't send. Try again in a minute.";
      if ($("#report").open) renderReport("#rpText");
    }
  }
});

async function openReport() {
  const dlg = $("#report");
  if (dlg.open || rpOpening) return;
  rpOpening = true;
  Object.assign(rp, { page, marks: [], shot: null, draft: null, view: "form", status: "", keep: true, sentShot: null });
  heldMessages = heldMessages ?? [];
  let restore = () => {}, reply = null;
  try {
    restore = hidePrivate();
    await frames(2);   // drawn hidden before the picture is taken
    reply = await askShot();
  } finally {
    restore();
    releaseHeld();
    rpOpening = false;
  }
  rp.details = reply?.details ?? "";
  rp.shot = reply?.image ? await loadImage(reply.image) : null;
  renderReport();
  dlg.showModal();
  $("#rpText")?.focus();
}

/** The screenshot with the player's marks, at w × h: highlights as gold outlines, blacked-out areas solid. */
function drawShot(ctx, w, h, withDraft) {
  const s = rp.shot, k = w / s.naturalWidth;
  ctx.drawImage(s, 0, 0, w, h);
  const marks = withDraft && rp.draft ? [...rp.marks, rp.draft] : rp.marks;
  for (const m of marks) {
    const x = Math.min(m.x0, m.x1) * k, y = Math.min(m.y0, m.y1) * k, mw = Math.abs(m.x1 - m.x0) * k, mh = Math.abs(m.y1 - m.y0) * k;
    if (m.tool === "hide") { ctx.fillStyle = "#000"; ctx.fillRect(x, y, mw, mh); continue; }
    ctx.lineWidth = Math.max(2, Math.round(Math.max(3, s.naturalWidth / 420) * k));
    ctx.strokeStyle = "#f0c860";
    ctx.strokeRect(x, y, mw, mh);
  }
}

function drawCanvas(c, cssWidth) {
  if (!c || !rp.shot) return;
  const w = cssWidth ? Math.round(cssWidth * devicePixelRatio) : rp.shot.naturalWidth;
  c.width = w;
  c.height = Math.round(w * rp.shot.naturalHeight / rp.shot.naturalWidth);
  drawShot(c.getContext("2d"), c.width, c.height, !cssWidth);
}

/** The picture as sent: PNG when it fits, else WebP, else smaller; base64 without its data: prefix. Marks are burned in. */
function exportShot() {
  if (!rp.shot) return null;
  const c = document.createElement("canvas");
  let w = rp.shot.naturalWidth;
  for (let i = 0; i < 5; i++, w = Math.round(w * 0.75)) {
    c.width = w;
    c.height = Math.round(w * rp.shot.naturalHeight / rp.shot.naturalWidth);
    drawShot(c.getContext("2d"), c.width, c.height, false);
    for (const [type, q] of [["image/png", undefined], ["image/webp", 0.92]]) {
      const url = c.toDataURL(type, q);
      if (!url.startsWith("data:" + type)) continue;
      const b64 = url.slice(url.indexOf(",") + 1);
      if (b64.length * 3 / 4 <= REPORT_MAX_SHOT) return b64;
    }
  }
  return null;
}

function renderReport(focus) {
  const dlg = $("#report");
  dlg.classList.toggle("wide", rp.view === "markup");
  const head = (title, id = "reportTitle") => `<div class="rp-head"><div class="h" id="${id}">${title}</div>
    <button class="rp-x" data-rp="close" aria-label="Close" title="Close (Esc)">×</button></div>`;

  if (rp.view === "sent") {
    const shot = { sent: " The screenshot went with it.", limit: " The screenshot didn't: today's screenshots are used up. What you wrote went.",
      failed: " The screenshot didn't go through, but what you wrote did." }[rp.sentShot] ?? "";
    dlg.innerHTML = `${head("Thanks: it's been sent")}
      <p class="muted" style="margin: 0; line-height: 1.55">It goes straight to whoever builds Pathfinder.${shot}
        <b>Help → Your reports</b> shows what happens to it, and any reply.</p>
      <div class="rp-foot"><span class="grow"></span><button class="btn" data-rp="reports">See your reports</button><button class="btn primary" data-rp="close">Close</button></div>`;
    dlg.querySelector("[data-rp=close].primary")?.focus();
    return;
  }

  if (rp.view === "markup") {
    dlg.innerHTML = `${head("Mark up the screenshot")}
      <div class="row" style="gap: 10px; align-items: center">
        <div class="seggroup" role="group" aria-label="Tool">${[["mark", "Highlight"], ["hide", "Black out"]].map(([t, label]) =>
          `<button class="${rp.tool === t ? "on" : ""}" aria-pressed="${rp.tool === t}" data-tool="${t}">${label}</button>`).join("")}</div>
        <span class="dim" style="font-size: 12.5px">${rp.tool === "mark" ? "Drag a box around what's wrong." : "Drag over anything you'd rather not send: it's covered for good."}</span>
        <span class="grow"></span>
        <button class="btn" data-rp="undo" ${rp.marks.length ? "" : "disabled"}>Undo</button>
        <button class="btn primary" data-rp="done">Done</button></div>
      <div class="rp-stage"><canvas id="rpCanvas" aria-label="The screenshot: drag to draw a box"></canvas></div>`;
    drawCanvas($("#rpCanvas"));
    if (focus) dlg.querySelector(focus)?.focus();
    return;
  }

  const version = data?.settings?.version ?? "development build";
  const marks = rp.marks.length;
  dlg.innerHTML = `${head("Report a problem")}
    <div class="seggroup" role="group" aria-label="What kind" id="rpKind">${REPORT_KINDS.map(([k, t]) =>
      `<button class="${k === rp.kind ? "on" : ""}" aria-pressed="${k === rp.kind}" data-rp-kind="${k}">${t}</button>`).join("")}</div>
    <textarea id="rpText" rows="4" maxlength="4000" aria-label="What happened" placeholder="${esc(REPORT_PROMPTS[rp.kind])}">${esc(rp.text)}</textarea>
    <div class="rp-shot">${rp.shot
      ? `<button class="rp-thumb${rp.keep ? "" : " off"}" data-rp="markup" title="Mark it up" aria-label="Mark up the screenshot"><canvas id="rpThumb"></canvas></button>
        <div class="rp-shotside">
          <button class="tog" data-rp="keep" aria-pressed="${rp.keep}"><span class="box">${rp.keep ? CHECK : ""}</span><span>Include this screenshot
            <span class="dim" style="display: block; font-size: 11.5px">Pathfinder's page only. Your character's name, clock times and folders are already hidden.</span></span></button>
          <div class="row" style="gap: 10px; align-items: center"><button class="btn" data-rp="markup" ${rp.keep ? "" : "disabled"}>Mark it up…</button>
            <span class="dim" style="font-size: 12px">${marks ? plural(marks, "mark") : "Highlight what's wrong, or black out more"}</span></div>
        </div>`
      : `<div class="dim" style="line-height: 1.5">No screenshot this time: it couldn't be taken. Say where it happened, and what you see.</div>`}</div>
    <label class="rp-check dim"><input type="checkbox" id="rpErrors" ${rp.errors ? "checked" : ""}> Include my error log (helps with bugs; it names files on this PC)</label>
    <details class="rp-what"><summary>What's sent</summary><ul>
      <li>What you wrote, and its kind.</li>
      ${rp.shot ? `<li>The screenshot above with your marks, only while it's ticked.</li>` : ""}
      <li>Where: ${esc(PAGE_NAMES[rp.page] ?? rp.page)}, in Pathfinder ${esc(version)}.</li>
      ${rp.details ? `<li>The window: ${esc(rp.details)}.</li>` : ""}
      <li>Your error log, only if you tick it.</li>
      <li>An anonymous id for this copy, so Help → Your reports can show you what happened. Never your name, your chat or your logs.</li>
    </ul></details>
    <div class="rp-foot"><span class="dim" id="rpStatus" aria-live="polite" style="font-size: 12.5px">${esc(rp.status)}</span><span class="grow"></span>
      <button class="btn" data-rp="close">Cancel</button>
      <button class="btn primary" data-rp="send" ${rp.busy ? "disabled" : ""}>${rp.busy ? "Sending…" : "Send"}</button></div>`;
  drawCanvas($("#rpThumb"), 240);
  if (focus) dlg.querySelector(focus)?.focus();
}

function sendReport() {
  const text = $("#rpText").value.trim();
  if (!text) { rp.status = "Write a line about it first."; renderReport("#rpText"); return; }
  rp.text = text;
  rp.busy = true;
  rp.status = "";
  renderReport();
  const shot = rp.shot && rp.keep ? exportShot() : null;
  post("feedback", { from: "report", kind: rp.kind, text, errors: rp.errors, page: rp.page, shot });
}

// ---- events
(() => {
  const dlg = $("#report");
  $(".rail [data-report]").addEventListener("click", openReport);
  document.addEventListener("keydown", e => {
    if (e.ctrlKey && e.shiftKey && !e.altKey && e.code === "KeyR") { e.preventDefault(); openReport(); }
  }, true);
  dlg.addEventListener("cancel", e => {
    if (rp.view === "markup") { e.preventDefault(); rp.draft = null; rp.view = "form"; renderReport("[data-rp=markup]"); }
  });
  dlg.addEventListener("close", () => { if (rp.view === "sent") rp.view = "form"; });
  dlg.addEventListener("input", e => {
    if (e.target.id === "rpText") rp.text = e.target.value;
    if (e.target.id === "rpErrors") rp.errors = e.target.checked;
  });
  dlg.addEventListener("click", e => {
    const kind = e.target.closest("[data-rp-kind]");
    if (kind) {
      rp.kind = kind.dataset.rpKind;
      $$("#rpKind button").forEach(b => { const on = b.dataset.rpKind === rp.kind; b.classList.toggle("on", on); b.setAttribute("aria-pressed", on); });
      $("#rpText").placeholder = REPORT_PROMPTS[rp.kind];
      return;
    }
    const tool = e.target.closest("[data-tool]");
    if (tool) { rp.tool = tool.dataset.tool; renderReport(`[data-tool=${rp.tool}]`); return; }
    const a = e.target.closest("[data-rp]")?.dataset.rp;
    if (!a) return;
    if (a === "close") dlg.close();
    else if (a === "send") sendReport();
    else if (a === "keep") { rp.keep = !rp.keep; renderReport("[data-rp=keep]"); }
    else if (a === "markup" && rp.shot && rp.keep) { rp.view = "markup"; renderReport("[data-rp=done]"); }
    else if (a === "undo") { rp.marks.pop(); renderReport("[data-rp=undo]"); }
    else if (a === "done") { rp.view = "form"; renderReport("[data-rp=markup]"); }
    else if (a === "reports") { dlg.close(); show("help"); $("#myReports")?.scrollIntoView({ block: "center" }); }
  });

  // Drawing a box: the pointer in the screenshot's own pixels.
  const at = (c, e) => {
    const r = c.getBoundingClientRect(), k = rp.shot.naturalWidth / r.width;
    return [Math.max(0, Math.min(rp.shot.naturalWidth, (e.clientX - r.left) * k)), Math.max(0, Math.min(rp.shot.naturalHeight, (e.clientY - r.top) * k))];
  };
  dlg.addEventListener("pointerdown", e => {
    const c = e.target.closest("#rpCanvas");
    if (!c || e.button !== 0) return;
    const [x, y] = at(c, e);
    rp.draft = { tool: rp.tool, x0: x, y0: y, x1: x, y1: y };
    c.setPointerCapture(e.pointerId);
  });
  dlg.addEventListener("pointermove", e => {
    const c = e.target.closest("#rpCanvas");
    if (!c || !rp.draft) return;
    [rp.draft.x1, rp.draft.y1] = at(c, e);
    drawShot(c.getContext("2d"), c.width, c.height, true);
  });
  dlg.addEventListener("pointerup", e => {
    const c = e.target.closest("#rpCanvas");
    if (!c || !rp.draft) return;
    [rp.draft.x1, rp.draft.y1] = at(c, e);
    const d = rp.draft;
    rp.draft = null;
    if (Math.abs(d.x1 - d.x0) >= 4 && Math.abs(d.y1 - d.y0) >= 4) rp.marks.push(d);
    renderReport();
  });
})();
