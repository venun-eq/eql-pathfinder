"use strict";
// ---------------------------------------------------------------- loot
// Every pickup arrives separately (it's the biggest list), only while this page is open: {t, k: 0 mote / 1 fragment,
// r: tier 1-10, n: count, c: from a Reward Chest, s: source, p: place, i: inside an instance visit}.
let lootRows = null, lastLoot = null, dropLimit = 300;
let dropView = store.get("drops", "frag"), lootZone = store.get("lootZone", ""), lootSource = store.get("lootSource", "all");
let lootTime = store.get("lootTime", "all"), lootMeasure = store.get("lootMeasure", "count");
const HOURS = { all: 0, "30d": 720, "7d": 168, "24h": 24 };
const MOTE_COLOR = "#7d8894";
const t5Each = v => v >= 1 ? String(v) : "1/" + Math.round(1 / v);
const tierValue = i => 2 ** (i - 5);
const big = v => v >= 10000 ? fmt(v / 1000, 0) + "k" : v >= 1000 ? fmt(v / 1000, 1).replace(/\.0$/, "") + "k" : String(Math.round(v));

/** Before the full list arrives, the per-tier totals and the notable drops stand in (no filters). */
function fallbackRows() {
  const rows = (data.drops ?? []).map(d => ({ t: d.time, k: d.kind === "Fragment" ? 1 : 0, r: TIERS.indexOf(d.tier), n: d.count, c: d.fromChest ? 1 : 0, s: d.source, p: zoneShort(d.zone), i: 0 }));
  return rows;
}

function renderLoot() {
  if (!data) return;
  const fv = data.settings?.fragmentValue || data.fragmentValue || 3.56;
  const L = data.ledger ?? { tiers: [] };
  const full = lootRows != null;
  const since = HOURS[lootTime] ? Date.now() - HOURS[lootTime] * 3600e3 : null;
  const keepRow = x => (!since || Date.parse(x.t) >= since) && (!lootZone || x.p === lootZone) && (lootSource === "all" || (lootSource === "chest") === !!x.c);
  const keepVisit = v => (!since || Date.parse(v.start) >= since) && (!lootZone || zoneShort(v.baseZone, v.difficulty) === lootZone);
  $("#lootSince").textContent = L.since ? `Looted since your logs began, ${dt(L.since)}` : "";
  $("#lootStatus").textContent = full ? "" : "loading every pickup…";
  ["lootZone"].forEach(id => $("#" + id).disabled = !full);
  [["#lootSourceSeg", lootSource], ["#lootTimeSeg", lootTime], ["#lootMeasureSeg", lootMeasure], ["#dropSeg", dropView]].forEach(([sel, v]) =>
    $$(sel + " button").forEach(b => { const on = (b.dataset.v ?? b.dataset.drops) === v; b.classList.toggle("on", on); b.setAttribute("aria-pressed", on); b.disabled = !full && sel !== "#lootMeasureSeg" && sel !== "#dropSeg"; }));

  // Per tier, from every pickup (or the totals until they arrive).
  const T = Array.from({ length: 11 }, (_, i) => ({ tier: i, name: TIERS[i], value: tierValue(i), motes: 0, frags: 0, chestMotes: 0, chestFrags: 0, killItems: 0, chestItems: 0 }));
  if (full) {
    for (const x of lootRows) {
      if (!keepRow(x)) continue;
      const t = T[x.r];
      if (!t) continue;
      if (x.k === 1) t.frags += x.n; else t.motes += x.n;
      if (x.c) { if (x.k === 1) t.chestFrags += x.n; else t.chestMotes += x.n; if (x.i) t.chestItems += x.n; } else if (x.i) t.killItems += x.n;
    }
  } else for (const t of L.tiers) Object.assign(T[t.tier], { motes: t.motes, frags: t.fragments, chestMotes: t.chestMotes, chestFrags: t.chestFragments });
  const visits = full ? (data.history ?? []).filter(keepVisit) : [];
  const kills = visits.reduce((a, v) => a + (v.kills || 0), 0);
  const crawls = visits.filter(v => v.completed).length;
  // The tile is the rate over the visits that have one, as on Crawls (the engine sends null for a visit with no rate).
  const rated = visits.filter(v => v.t5PerHour != null), ratedH = rated.reduce((a, v) => a + (v.activeMinutes || 0), 0) / 60;
  const ratedT5 = rated.reduce((a, v) => a + (v.t5 || 0), 0);
  const tierT5 = t => (t.motes + t.frags * fv) * t.value;
  const live = T.slice(1);
  const total = live.reduce((a, t) => a + tierT5(t), 0), items = live.reduce((a, t) => a + t.motes + t.frags, 0);
  const fragN = live.reduce((a, t) => a + t.frags, 0), fragT5 = live.reduce((a, t) => a + t.frags * fv * t.value, 0);
  const high = live.filter(t => t.tier >= 6).reduce((a, t) => a + tierT5(t), 0);
  const chestT5 = live.reduce((a, t) => a + (t.chestMotes + t.chestFrags * fv) * t.value, 0);
  $("#lootTiles").innerHTML = [
    [`<span class="gold">${fmt(total, 0)}</span>`, `T5-eq${infoTip("t5eq")} · ${items.toLocaleString()} motes and fragments`],
    [`${fragN}`, `fragments · ${fmt(fragT5, 0)} T5-eq`],
    [fmt(high, 0), "T5-eq of it above T5"],
    ratedH > 0 ? [
                  `<span class="gold">${fmt(ratedT5 / ratedH, 0)}</span>`, `T5-eq per active hour in instances (${fmt(ratedH, 1)} h)`]
                  : [total ? fmt(chestT5 / total * 100, 0) + "%" : "—", "from Reward Chests"],
  ].map(([v, k]) => `<div class="stat"><div class="v num">${v}</div><div class="k">${k}</div></div>`).join("");

  // Zone list: every place you've looted, most T5-eq first.
  if (full) {
    const places = new Map();
    for (const x of lootRows) { const k = x.p ?? ""; places.set(k, (places.get(k) ?? 0) + x.n * tierValue(x.r) * (x.k === 1 ? fv : 1)); }
    const opts = [...places.entries()].filter(([k]) => k).sort((a, b) => b[1] - a[1]);
    const sel = $("#lootZone");
    if (sel.dataset.n !== String(opts.length)) {
      sel.innerHTML = `<option value="">Everywhere</option>` + opts.map(([k, v]) => `<option value="${esc(k)}">${esc(k)} · ${fmt(v, 0)} T5-eq</option>`).join("");
      sel.dataset.n = String(opts.length);
    }
    sel.value = lootZone;
    if (sel.value !== lootZone) { lootZone = ""; sel.value = ""; }
  }

  // Table: counts and how often each tier drops.
  const rate = (n, d) => d > 0 && n > 0 ? (n / d < 0.1 ? fmt(n / d, 3) : n / d < 10 ? fmt(n / d, 2) : fmt(n / d, 1)) : "";
  $("#rateNote").textContent = full ? (kills ? `${kills.toLocaleString()} kills and ${plural(crawls, "finished crawl")} in instances behind the rates` : "no instance kills in this selection") : "";
  const shown = live.filter(t => t.motes + t.frags > 0).sort((a, b) => b.tier - a.tier);
  $("#ledgerTable thead").innerHTML = `<tr><th class="l">Tier</th><th>T5-eq each</th><th>Motes</th><th>Fragments</th><th>% of items</th><th>Per 100 kills</th><th>Per crawl (chest)</th><th>T5-eq</th></tr>`;
  $("#ledgerTable tbody").innerHTML = shown.length ? shown.map(t => `<tr>
      <td class="l">${t.tier >= 6 ? "<b>" + esc(t.name) + "</b>" : esc(t.name)}</td><td class="dim">${t5Each(t.value)}</td>
      <td>${t.motes ? t.motes.toLocaleString() : ""}</td><td class="gold">${t.frags || ""}</td>
      <td class="dim">${items ? fmt((t.motes + t.frags) / items * 100, (t.motes + t.frags) / items < 0.01 ? 2 : 1) + "%" : ""}</td>
      <td>${full ? rate(t.killItems * 100, kills) : ""}</td><td>${full ? rate(t.chestItems, crawls) : ""}</td>
      <td>${fmt(tierT5(t), tierT5(t) < 10 ? 1 : 0)}</td></tr>`).join("") +
      `<tr style="font-weight: 650"><td class="l">Total</td><td></td><td>${live.reduce((a, t) => a + t.motes, 0).toLocaleString()}</td><td class="gold">${fragN}</td><td></td>
        <td>${full ? rate(live.reduce((a, t) => a + t.killItems, 0) * 100, kills) : ""}</td><td>${full ? rate(live.reduce((a, t) => a + t.chestItems, 0), crawls) : ""}</td><td>${fmt(total, 0)}</td></tr>`
    : `<tr><td class="empty" colspan="8">Nothing looted in this selection.</td></tr>`;

  lastLoot = { T, fv, kills, crawls, items };
  drawLootChart();

  // Drops
  const pool = (full ? lootRows.filter(keepRow) : fallbackRows()).filter(x => dropView === "frag" ? x.k === 1 : dropView === "high" ? x.r >= 6 : true);
  $("#dropTable thead").innerHTML = `<tr><th class="l">When</th><th class="l">What</th><th>Count</th><th>T5-eq</th><th class="l">From</th><th class="l">Where</th></tr>`;
  $("#dropTable tbody").innerHTML = pool.length ? pool.slice(0, dropLimit).map(x => {
    const frag = x.k === 1, v = x.n * tierValue(x.r) * (frag ? fv : 1);
    return `<tr><td class="l">${dt(x.t)}</td><td class="l"><span class="tag ${frag ? "frag" : ""}">${frag ? "fragment" : "mote"}</span> ${x.r >= 6 ? "<b>" + esc(TIERS[x.r]) + "</b>" : esc(TIERS[x.r])}</td>
      <td>${x.n}</td><td class="${frag ? "gold" : ""}">${fmt(v, v < 1 ? 2 : 1)}</td><td class="l">${esc(x.s ?? "")}</td><td class="l dim">${esc(x.p ?? "")}</td></tr>`;
  }).join("") : `<tr><td class="empty" colspan="6">Nothing here in this selection.</td></tr>`;
  const n = pool.reduce((a, x) => a + x.n, 0);
  $("#dropCount").textContent = `${pool.length.toLocaleString()} pickups · ${n.toLocaleString()} ${dropView === "frag" ? "fragments" : "items"}`;
  $("#dropMore").innerHTML = pool.length > dropLimit ? `<button class="btn" id="dropMoreBtn">Show ${Math.min(500, pool.length - dropLimit)} more (${(pool.length - dropLimit).toLocaleString()} left)</button>` : "";
}

/** Grouped bars per tier, motes and fragments side by side; counts on a log scale so the rare tiers stay visible. */
function drawLootChart() {
  const host = $("#lootChart");
  if (!host || !lastLoot || page !== "loot") return;
  const w = host.clientWidth;
  if (w < 240) return;
  const { T, fv, kills, crawls, items } = lastLoot;
  const tiers = T.slice(1), count = lootMeasure === "count";
  const val = (t, frag) => count ? (frag ? t.frags : t.motes) : (frag ? t.frags * fv : t.motes) * t.value;
  const max = Math.max(...tiers.flatMap(t => [val(t, false), val(t, true)]), 0);
  $("#lootScaleNote").textContent = count ? "· count, log scale: each gridline is 10× the one below" : "· T5-equivalent";
  if (max <= 0) { host.innerHTML = `<div class="empty">Nothing looted in this selection.</div>`; return; }
  const H = 290, L = 46, R = 10, Tp = 24, B = 34, pw = w - L - R, ph = H - Tp - B;
  let top, Y, ticks;
  if (count) {
    top = 10 ** Math.max(1, Math.ceil(Math.log10(max)));
    const lo = 0.5;
    Y = v => v <= 0 ? 0 : Math.log10(v / lo) / Math.log10(top / lo) * ph;
    ticks = []; for (let t = 1; t <= top; t *= 10) ticks.push(t);
  } else {
    const p = 10 ** Math.floor(Math.log10(max)), m = max / p;
    top = (m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10) * p;
    Y = v => v / top * ph;
    ticks = [0, top / 4, top / 2, top * 3 / 4, top];
  }
  const gw = pw / tiers.length, bw = Math.min(28, gw * 0.3);
  const bar = (x, h) => { const y = Tp + ph - h, r = Math.min(4, h / 2, bw / 2); return `M${x},${Tp + ph}V${y + r}Q${x},${y} ${x + r},${y}H${x + bw - r}Q${x + bw},${y} ${x + bw},${y + r}V${Tp + ph}Z`; };
  let s = `<svg width="${w}" height="${H}" viewBox="0 0 ${w} ${H}" role="img" aria-label="Motes and fragments by tier, ${count ? "count on a log scale" : "in T5-equivalent"}. The table below has the numbers.">`;
  for (const t of ticks) { const y = Tp + ph - Y(t); s += `<line x1="${L}" x2="${w - R}" y1="${y}" y2="${y}" stroke="#262f3a"/><text x="${L - 8}" y="${y + 4}" fill="#8d959e" font-size="11" text-anchor="end">${big(t)}</text>`; }
  s += `<line x1="${L}" x2="${w - R}" y1="${Tp + ph}" y2="${Tp + ph}" stroke="#3a4552"/>`;
  tiers.forEach((t, i) => {
    const cx = L + gw * i + gw / 2;
    [[false, cx - bw - 1, MOTE_COLOR], [true, cx + 1, "var(--gold)"]].forEach(([frag, x, color]) => {
      const v = val(t, frag);
      if (v <= 0) return;
      const h = Math.max(2, Y(v));
      s += `<path d="${bar(x, h)}" fill="${color}"/>`;
      s += `<text x="${x + bw / 2}" y="${Tp + ph - h - 5}" fill="#c9cfd6" font-size="10.5" text-anchor="middle">${count ? big(v) : fmt(v, v < 10 ? 1 : 0)}</text>`;
    });
    s += `<text x="${cx}" y="${H - 12}" fill="${t.tier >= 6 ? "#ece7da" : "#a9b1ba"}" font-size="11.5" text-anchor="middle"${t.tier >= 6 ? ' font-weight="600"' : ""}>${esc(t.name)}</text>`;
    s += `<rect class="hitTier" data-tier="${t.tier}" x="${L + gw * i}" y="${Tp}" width="${gw}" height="${ph + 20}" fill="transparent"/>`;
  });
  host.innerHTML = s + "</svg>";
  const tip = $("#tip");
  host.querySelectorAll(".hitTier").forEach(r => {
    r.addEventListener("mousemove", e => {
      const t = T[Number(r.dataset.tier)];
      const all = t.motes + t.frags;
      tip.innerHTML = `<div style="font-weight: 600; margin-bottom: 4px">${esc(t.name)} · ${t5Each(t.value)} T5-eq each</div>
        <div class="r"><i style="background: ${MOTE_COLOR}"></i>motes<span style="margin-left: auto; padding-left: 12px" class="num">${t.motes.toLocaleString()}</span></div>
        <div class="r"><i style="background: var(--gold)"></i>fragments<span style="margin-left: auto; padding-left: 12px" class="num">${t.frags.toLocaleString()}</span></div>
        <div class="r dim">${items ? fmt(all / items * 100, all / items < 0.01 ? 2 : 1) : 0}% of items · ${fmt((t.motes + t.frags * fv) * t.value, 1)} T5-eq</div>
        ${kills ? `<div class="r dim">${fmt(t.killItems / kills * 100, 2)} per 100 kills · ${crawls ? fmt(t.chestItems / crawls, 2) : "—"} per crawl from chests</div>` : ""}`;
      tip.hidden = false;
      tip.style.left = Math.min(e.clientX + 14, window.innerWidth - tip.offsetWidth - 8) + "px";
      tip.style.top = Math.max(8, e.clientY - tip.offsetHeight - 10) + "px";
    });
    r.addEventListener("mouseleave", () => { tip.hidden = true; });
  });
}

function lootSeg(sel, set) {
  $(sel).addEventListener("click", e => {
    const b = e.target.closest("button");
    if (!b || b.disabled) return;
    set(b.dataset.v ?? b.dataset.drops);
    dropLimit = 300;
    renderLoot();
  });
}
lootSeg("#lootSourceSeg", v => { lootSource = v; store.set("lootSource", v); });
lootSeg("#lootTimeSeg", v => { lootTime = v; store.set("lootTime", v); });
lootSeg("#lootMeasureSeg", v => { lootMeasure = v; store.set("lootMeasure", v); });
lootSeg("#dropSeg", v => { dropView = v; store.set("drops", v); });
$("#lootZone").addEventListener("change", e => { lootZone = e.target.value; store.set("lootZone", lootZone); dropLimit = 300; renderLoot(); });
$("#dropMore").addEventListener("click", e => { if (e.target.id === "dropMoreBtn") { dropLimit += 500; renderLoot(); } });

