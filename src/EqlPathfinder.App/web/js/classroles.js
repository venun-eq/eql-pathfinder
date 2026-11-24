"use strict";
// ---------------------------------------------------------------- Class roles
// Carries (best first), survival check for leveling difficulty, and Pathfinder's class rankings.
// Edits show immediately and trigger a plan rebuild.
let rolesLocal = null;   // { carries, oneStrongSlow } until the saved ones come back
const rolesView = () => data?.reinc?.campaign?.newPlanner;
const ORD = ["", "1st", "2nd", "3rd", "4th"];

const rolesNow = n => rolesLocal ?? { carries: n.carries, oneStrongSlow: n.oneStrongSlow };

function sendRoles(r, focus) {
  rolesLocal = r;
  post("setting", { key: "carries", value: r });
  renderClassRoles();
  if (focus) $(focus)?.focus();
}

/** "1st", "3rd · from 10", a faint dashed chip for a part one, a dim dash for none. */
function roleCell(c) {
  if (!c) return `<span class="dim">–</span>`;
  if (c.startsWith("~")) return `<span class="chip part">${esc(c.slice(1))}</span>`;
  const [rank, note] = c.split("|"), n = Number(rank);
  return `<span class="chip${n === 1 ? " gold" : ""}">${ORD[n]}${note ? ` · ${esc(note)}` : ""}</span>`;
}
const wantsCell = c => !c ? `<span class="dim">–</span>` : `<span class="chip${c === "a lot" ? " amber" : ""}">${esc(c)}</span>`;

/** What the check says: a status box, gold while trios are checked. */
function rolesStatus(n) {
  const ck = n.check, d = ck.difficulty;
  const last = "your latest relevel";
  const farm = (n.farmed ?? []).map(x => "D" + x);
  const where = '<button class="link" data-go="settings" data-anchor="howYouPlay">Settings → How you play</button>';
  const rows = {
    checked: [`<b class="gold">You level at D${d}</b>, a difficulty you farm for motes, so trios are checked.`, `D${d} had over half the hours of ${last}. What you farm is in ${where}.`],
    quiet: [`<b>You level at D${d}</b>, which you don't farm for motes, so the check stays quiet: any trio makes it.`, `D${d} had over half the hours of ${last}. What you farm is in ${where}.`],
    spread: [`<b class="gold">No one difficulty</b> had over half the hours of ${last}, so trios are checked. It only chooses between seatings that cost the same.`, `What you farm is in ${where}.`],
    norelevel: [`No relevels yet, so trios are checked as if you level where you farm for motes.`, `You farm ${farm.join(" and ")} for motes (${where}).`],
    off: [`You don't farm any difficulty for motes, so the check is off: any trio makes it.`, `Pick what you farm in ${where}.`],
  }[ck.state] ?? ["", ""];
  return `<div class="check-state${ck.checks ? " on" : ""}"><div>${rows[0]}</div><div class="dim" style="font-size: 12px; margin-top: 2px">${rows[1]}</div></div>`;
}

function renderClassRoles() {
  const n = rolesView(), el = $("#rolesBody");
  if (!n) { el.innerHTML = ""; return; }
  const r = rolesNow(n), busy = rolesLocal != null, quiet = !n.check.checks;
  const all = (data.reinc.classes ?? []).map(c => c.key), addable = all.filter(c => !r.carries.includes(c));
  const rows = r.carries.map((c, i) => `<div class="carry-row"><span class="num gold">${i + 1}</span><b class="gold">${esc(c)}</b><span class="dim grow">${esc(n.kills[c] ?? "none")}</span>
      <button class="iconbtn" data-carry-up="${c}" aria-label="Move ${c} up"${i === 0 ? ' disabled aria-disabled="true"' : ""}>▲</button>
      <button class="iconbtn" data-carry-down="${c}" aria-label="Move ${c} down"${i === r.carries.length - 1 ? ' disabled aria-disabled="true"' : ""}>▼</button>
      <button class="iconbtn" data-carry-del="${c}" aria-label="Remove ${c}">✕</button></div>`).join("");
  const empty = r.carries.length ? "" : `<div class="muted" style="margin: 10px 0">${n.check.checks ? "No carries. Between plans that cost the same, the survival check and haste decide." : "No carries, and the check is off: any seating that costs the same will do."}</div>`;
  const table = (n.table ?? []).map(t => `<tr><td class="l"><b>${esc(t.abbrev)}</b></td><td class="l">${roleCell(t.tank)}</td><td class="l">${roleCell(t.heals)}</td><td class="l">${roleCell(t.slow)}</td>
      <td class="l">${roleCell(t.crowd)}</td><td class="l">${roleCell(t.gives)}</td><td class="l">${wantsCell(t.wants)}</td><td class="l muted">${esc(t.notes)}</td></tr>`).join("");
  $("#rolesBody").innerHTML = `<div class="roles-grid">
    <section class="card" style="padding: 16px 18px" aria-label="Your carries">
      <div class="between"><div class="h2">Your carries, best first</div><span class="row" style="gap: 10px">${busy ? `<span class="chip">updating…</span>` : ""}${n.carriesAreDefault ? `<span class="dim" style="font-size: 12px">Pathfinder's list</span>` : `<button class="link" data-carry-reset>Back to Pathfinder's</button>`}</span></div>
      <div class="dim" style="font-size: 12.5px; margin: 6px 0 10px; line-height: 1.5">The classes that do your killing. Between plans that cost the same runs and XP, each trio gets a carry, the best ones spread out, in this order.</div>
      ${rows}${empty}
      ${addable.length ? `<div class="lbl" style="margin: 14px 0 8px">Add a carry</div><div class="chips">${addable.map(c => `<button class="chip" data-carry-add="${c}" aria-label="Add ${c}">${c}</button>`).join("")}</div>` : ""}
      <div class="dim" style="font-size: 12px; margin-top: 12px; line-height: 1.5">Your plan follows this list at once. Runs you've played or pinned never move.</div>
    </section>
    <section class="card" style="padding: 16px 18px" aria-label="The survival check">
      <div class="h2">The survival check</div>
      ${rolesStatus(n)}
      <ul class="roles-rules${quiet ? " dim" : ""}">
        <li><b>A trio holds up with ENC in it, or with two of: a tank, heals, a slow, crowd control.</b> <span class="muted">ENC's area mez holds up to 30 mobs, so you take them one at a time. BRD slows and mezzes, so a trio with BRD holds up too. WAR / CLR / RNG holds up (tank and heals); RNG / DRU / BER doesn't (heals only).</span></li>
        <li><b>ENC and BRD go where they're needed most.</b> <span class="muted">Each has 3 runs and holds a trio up on its own, so they go to trios that would otherwise fall short.</span></li>
        <li><b>Haste goes to the classes that hit with weapons.</b> <span class="muted">A trio with RNG, MNK, BER or ROG gets ENC, SHM or BRD where it's free, the earliest haste first.</span></li>
        <li><b>The best go where they're missing.</b> <span class="muted">A trio with a top tank gets heals or a slow before a trio that has both.</span></li>
      </ul>
      <button class="tog" data-slow aria-pressed="${r.oneStrongSlow}"><span class="box">${r.oneStrongSlow ? CHECK : ""}</span>
        <span><span style="display: block; font-weight: 600">One strong slow per trio</span><span class="dim" style="display: block; font-size: 11.5px">SHM and ENC never share a trio, so each slows, heals and hastes for another one.</span></span></button>
      <div class="dim" style="font-size: 12.5px; margin-top: 8px">None of this adds a run or XP to level: it only chooses between seatings that cost the same.</div>
    </section>
    <section class="card roles-table" style="padding: 16px 18px" aria-label="How Pathfinder ranks the classes">
      <div class="between"><div class="h2">How Pathfinder ranks the classes</div><span class="dim" style="font-size: 12px">From the EQL wikis and guides, checked against the patch notes to Nov 10</span></div>
      <div class="tablewrap" style="margin-top: 10px"><table><thead><tr><th class="l">Class</th><th class="l">Tank</th><th class="l">Heals</th><th class="l">Slow</th><th class="l">Mez, charm</th><th class="l">Gives haste</th><th class="l">Wants haste</th><th class="l">Notes</th></tr></thead>
        <tbody>${table}</tbody></table></div>
      <div class="dim" style="font-size: 12px; margin-top: 10px; line-height: 1.55">1st is the best at it. A dashed cell counts for less, or not at all: NEC's charm works on undead only and DRU's on animals only, and ROG's poison and BST's weak slows don't count as a slow.<br>
        <b>Haste: the earlier, the better.</b> "From 16" is the level the class gets it. Item haste and spell haste add up, but two spell hastes don't, so SHM and ENC together waste one. From 40, Distillate of Alacrity VI hastes any trio.</div>
    </section></div>`;
}

$("#page-classroles").addEventListener("click", e => {
  const n = rolesView(), t = e.target.closest("button");
  if (!n || !t) return;
  const r = rolesNow(n), list = [...r.carries], d = t.dataset;
  if (d.carryUp != null) { const i = list.indexOf(d.carryUp); if (i > 0) { [list[i - 1], list[i]] = [list[i], list[i - 1]]; sendRoles({ ...r, carries: list }, `[data-carry-up="${d.carryUp}"]`); } return; }
  if (d.carryDown != null) { const i = list.indexOf(d.carryDown); if (i >= 0 && i < list.length - 1) { [list[i + 1], list[i]] = [list[i], list[i + 1]]; sendRoles({ ...r, carries: list }, `[data-carry-down="${d.carryDown}"]`); } return; }
  if (d.carryDel != null) return sendRoles({ ...r, carries: list.filter(c => c !== d.carryDel) });
  if (d.carryAdd != null) return sendRoles({ ...r, carries: [...list, d.carryAdd] });
  if (d.carryReset != null) return sendRoles({ ...r, carries: [...n.defaultCarries] });
  if (d.slow != null) return sendRoles({ ...r, oneStrongSlow: !r.oneStrongSlow }, "[data-slow]");
});
