// Sharing: registering an install, uploading units, what the server holds, deleting it all, linking PCs, invite codes.
// Spec: "EQL Pathfinder: Sharing spec". The Worker only checks who is asking and stores what they share; the community
// numbers are worked out by the app's own C# code on the NAS (the free plan allows about 10 ms of CPU per request).

import { authenticate, limitCodeGuesses, takeCode } from "./auth.ts";
import { newPlayerLimit, oneCharacterPerServer } from "./connection.ts";
import type { Env, Who } from "./env.ts";
import { bad, HttpError, int, json, optInt, optStr, parseJson, str } from "./http.ts";
import {
  b64d,
  codeHash,
  isValidDay,
  newCode,
  nowS,
  older,
  pretty,
  randomHex,
  secondsToUtcMidnight,
  today,
} from "./util.ts";

export const MAX_ROWS_PER_UPLOAD = 200;
const MAX_ROWS_PER_DAY = 3000; // per install; a first upload of a long history spreads over a few days
export const MAX_REQUESTS_PER_DAY = 200; // per install; stops runaway identical re-sends (changed == 0) only: the unit and byte caps bound changed uploads
export const MAX_BYTES_PER_DAY = 16 * 1024 * 1024; // per install (~12x heaviest real day); stored facts + derived bytes
// The two global fuses are sized for the public launch on Workers Paid (11-24), so a missing config row never stops
// real players: 200,000 units is about 140 of the heaviest first syncs (1,431 units) a day, and a month at the fuse
// is about 36M rows written against the 50M the plan includes. What's over goes up the next day.
export const MAX_GLOBAL_BYTES_PER_DAY = 200 * 1024 * 1024; // all installs together (config max_upload_bytes_per_day; "0" = off)
// Raise this (config max_units_per_day) when real daily units, from SUM(units) in usage, pass about half of it. The busiest day before the launch was 4,537.
export const MAX_GLOBAL_UNITS_PER_DAY = 200_000; // all installs together (config max_units_per_day; "0" = off)
export const MAX_FACTS = 128 * 1024; // base64 characters per row (~96 KiB compressed; >10x largest real crawl)
// biome-ignore lint/suspicious/noControlCharactersInRegex: intentional rejection of ASCII control characters
const CONTROL_CHARS = /[\x00-\x1f\x7f]/;
const LINK_TTL_S = 600;
const ENROLL_TTL_S = 24 * 3600;
const MAX_ENROLL_ACTIVE = 3; // enrollment codes a player may have waiting at once
const KINDS = new Set(["crawl", "instance", "stretch", "reincarnation", "relevel"]);

export interface Config {
  requireEnrollment: boolean;
  minApp: string | null;
  community: string | null;
  limits: { rowsPerUpload: number; rowsPerDay: number };
  maxNewPlayersPerIp: number;
}

/** The switches the operator flips in the config table, as the app reads them (GET /v1/config). */
export async function config(env: Env): Promise<Config> {
  const { results } = await env.DB.prepare("SELECT key, value FROM config").all<{ key: string; value: string }>();
  const c = Object.fromEntries(results.map(r => [r.key, r.value]));
  return {
    requireEnrollment: c.require_enrollment === "1",
    minApp: c.min_app || null,
    community: c.community_url || null,
    limits: { rowsPerUpload: MAX_ROWS_PER_UPLOAD, rowsPerDay: MAX_ROWS_PER_DAY },
    maxNewPlayersPerIp: Number(c.max_new_players_per_ip) || 0,
  };
}

/**
 * A new install proves it holds the key it registers. With a link code it joins that player (and receives the character
 * salt); otherwise it starts a new player, which needs an enrollment code while the switch is on.
 */
export async function register(request: Request, env: Env, path: string, body: Uint8Array): Promise<Response> {
  const req = parseJson(body);
  const publicKey = str(req.publicKey, "publicKey", 300);
  const who = await authenticate(request, env, "POST", path, body, publicKey);
  const existing = await env.DB.prepare("SELECT id, player FROM installs WHERE id = ?")
    .bind(who.id)
    .first<{ id: string; player: string }>();
  if (existing) return json({ install: existing.id, player: existing.player, existing: true });

  const cfg = await config(env);
  if (cfg.minApp && (typeof req.app !== "string" || older(req.app, cfg.minApp)))
    throw new HttpError(426, "update_app", "Sharing needs EQL Pathfinder " + cfg.minApp + " or newer.");

  const now = nowS();
  let player: string | null = null,
    salt: string | null = null,
    enrolledBy: string | null = null;
  let ipCount: D1PreparedStatement | null = null;
  // New players per connection per day, with registration open (config max_new_players_per_ip; 0 = off). Linking another PC to a player never counts.
  if (!req.link && cfg.maxNewPlayersPerIp > 0) ipCount = await newPlayerLimit(request, env, cfg.maxNewPlayersPerIp);
  if (req.link) {
    await limitCodeGuesses(request, env);
    const c = await takeCode(env, "link", req.link);
    player = c.player;
    salt = c.payload;
  } else if (cfg.requireEnrollment) {
    if (!req.enroll)
      throw new HttpError(
        403,
        "enrollment_required",
        "Sharing needs an enrollment code from someone who already shares.",
      );
    await limitCodeGuesses(request, env);
    const c = await takeCode(env, "enroll", req.enroll);
    enrolledBy = c.install;
  }
  const stmts: D1PreparedStatement[] = [];
  if (!player) {
    player = randomHex(16);
    stmts.push(env.DB.prepare("INSERT INTO players (id, created) VALUES (?, ?)").bind(player, now));
  }
  stmts.push(
    env.DB.prepare(
      "INSERT INTO installs (id, player, public_key, created, last_seen, app, enrolled_by) VALUES (?, ?, ?, ?, ?, ?, ?)",
    ).bind(who.id, player, publicKey, now, now, optStr(req.app, 40), enrolledBy),
  );
  if (ipCount) stmts.push(ipCount);
  await env.DB.batch(stmts);
  return json({ install: who.id, player, salt });
}

const UPSERT_UNIT = [
  "INSERT INTO units (id, player, install, kind, character, server, day, seq, zone, difficulty, level_start, level_end, trio, race, rank,",
  "                   schema, app, engine, derived, facts, received, content_hash)",
  "VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14, ?15, ?16, ?17, ?18, ?19, ?20, ?21, ?22)",
  "ON CONFLICT (id) DO UPDATE SET",
  "  install = excluded.install, kind = excluded.kind, character = excluded.character, server = excluded.server, day = excluded.day,",
  "  seq = excluded.seq, zone = excluded.zone, difficulty = excluded.difficulty, level_start = excluded.level_start,",
  "  level_end = excluded.level_end, trio = excluded.trio, race = excluded.race, rank = excluded.rank, schema = excluded.schema,",
  "  app = excluded.app, engine = excluded.engine, derived = excluded.derived, facts = excluded.facts, received = excluded.received,",
  "  content_hash = excluded.content_hash, quarantined = 0",
  "WHERE units.player = excluded.player",
].join("\n");

/** Up to 200 units; each stored once per id (a re-send overwrites, and never another player's row). */
export async function summaries(request: Request, env: Env, who: Who, body: Uint8Array): Promise<Response> {
  const req = parseJson(body);
  const schema = int(req.schema, "schema", 1, 99);
  const app = str(req.app, "app", 40);
  if (CONTROL_CHARS.test(app)) throw bad("app");
  const minApp = await env.DB.prepare("SELECT value FROM config WHERE key = 'min_app'").first<{ value: string }>();
  if (minApp?.value && older(app, minApp.value))
    throw new HttpError(426, "update_app", "Sharing needs EQL Pathfinder " + minApp.value + " or newer.");
  const engine = int(req.engine, "engine", 0, 9999);
  const server = optStr(req.server, 40);
  if (server && CONTROL_CHARS.test(server)) throw bad("server");
  const character = str(req.character, "character", 32);
  if (!/^[0-9a-f]{8,32}$/.test(character)) throw bad("character");
  if (!Array.isArray(req.rows) || req.rows.length === 0) throw bad("rows");
  if (req.rows.length > MAX_ROWS_PER_UPLOAD)
    throw new HttpError(413, "too_many_rows", "At most " + MAX_ROWS_PER_UPLOAD + " rows per upload.");
  const player = who.player!;
  const maxDay = new Date(Date.now() + 2 * 86400000).toISOString().slice(0, 10);

  // Every row is checked before anything is written; a wrong one refuses the whole upload.
  const rows = (req.rows as unknown[]).map((raw, i) => {
    const r = (raw && typeof raw === "object" ? raw : {}) as Record<string, unknown>;
    const kind = str(r.kind, "rows[" + i + "].kind", 16);
    if (!KINDS.has(kind)) throw bad("rows[" + i + "].kind");
    const id = str(r.id, "rows[" + i + "].id", 40);
    if (!/^[0-9a-f]{16,40}$/.test(id)) throw bad("rows[" + i + "].id");
    const rday = str(r.day, "rows[" + i + "].day", 10);
    if (!isValidDay(rday) || rday > maxDay) throw bad("rows[" + i + "].day");
    const derived = r.derived == null ? null : JSON.stringify(r.derived);
    if (derived && derived.length > 4000) throw bad("rows[" + i + "].derived");
    const zone = optStr(r.zone, 80);
    if (zone && CONTROL_CHARS.test(zone)) throw bad("rows[" + i + "].zone");
    const trio = optStr(r.trio, 20);
    if (trio && CONTROL_CHARS.test(trio)) throw bad("rows[" + i + "].trio");
    const race = optStr(r.race, 30);
    if (race && CONTROL_CHARS.test(race)) throw bad("rows[" + i + "].race");
    let facts: string | null = null;
    let factsBytes: Uint8Array | null = null;
    if (r.facts != null) {
      facts = str(r.facts, "rows[" + i + "].facts", MAX_FACTS);
      try {
        factsBytes = b64d(facts);
      } catch {
        throw bad("rows[" + i + "].facts");
      }
      if (factsBytes.length < 10 || factsBytes[0] !== 0x1f || factsBytes[1] !== 0x8b)
        throw bad("rows[" + i + "].facts");
    }
    const row = {
      id,
      kind,
      day: rday,
      seq: optInt(r.seq, 0, 1e9),
      zone,
      difficulty: optInt(r.difficulty, 0, 20),
      levelStart: optInt(r.levelStart, 0, 200),
      levelEnd: optInt(r.levelEnd, 0, 200),
      trio,
      race,
      rank: optInt(r.rank, 0, 999) ?? 0,
      derived,
      facts,
    };
    return { ...row, factsBytes, hash: "" };
  });

  // A re-send of a unit the server already holds unchanged (a reinstall, a lost ledger, a linked PC) is skipped: not
  // written, and not counted against the daily limit. The limit stays a fuse against runaway copies, and the same unit
  // again can't fill the database. The app version alone isn't a change.
  for (const r of rows)
    r.hash = await contentHash([{ ...r, factsBytes: undefined, hash: undefined }, character, server, schema, engine]);
  const held = await env.DB.prepare(
    "SELECT id, content_hash FROM units WHERE player = ? AND id IN (SELECT value FROM json_each(?))",
  )
    .bind(player, JSON.stringify(rows.map(r => r.id)))
    .all<{ id: string; content_hash: string | null }>();
  const heldHash = new Map(held.results.map(h => [h.id, h.content_hash]));
  const changed = rows.filter(r => heldHash.get(r.id) !== r.hash);

  const day = today();
  const noteCharacter = await oneCharacterPerServer(request, env, server, character, player);
  const changedBytes = changed.reduce((n, r) => n + (r.factsBytes?.byteLength ?? 0) + (r.derived?.length ?? 0), 0);
  const used = await env.DB.prepare("SELECT units, requests, bytes FROM usage WHERE install = ? AND day = ?")
    .bind(who.id, day)
    .first<{ units: number; requests: number; bytes: number }>();
  const usedUnits = used?.units ?? 0,
    usedRequests = used?.requests ?? 0,
    usedBytes = used?.bytes ?? 0;
  // First upload of the day (usedBytes === 0) always fits the per-install byte budget so a large batch never wedges an install.
  if (
    (changed.length === 0 && usedRequests >= MAX_REQUESTS_PER_DAY) ||
    usedUnits + changed.length > MAX_ROWS_PER_DAY ||
    (changedBytes > 0 && usedBytes > 0 && usedBytes + changedBytes > MAX_BYTES_PER_DAY)
  )
    throw new HttpError(429, "daily_limit", "Today's upload limit is reached; the rest goes tomorrow.", {
      retryAfter: secondsToUtcMidnight(),
    });

  if (changed.length > 0) {
    const [globalUsed, globalCfg] = await env.DB.batch([
      env.DB.prepare(
        "SELECT COALESCE(SUM(units), 0) AS units, COALESCE(SUM(bytes), 0) AS bytes FROM usage WHERE day = ?",
      ).bind(day),
      env.DB.prepare("SELECT key, value FROM config WHERE key IN ('max_units_per_day', 'max_upload_bytes_per_day')"),
    ]);
    const totals = (globalUsed.results[0] as { units: number; bytes: number } | undefined) ?? { units: 0, bytes: 0 };
    const cfgMap = Object.fromEntries(
      (globalCfg.results as { key: string; value: string }[]).map(r => [r.key, r.value]),
    );
    const maxGlobalUnits = cfgLimit(cfgMap.max_units_per_day, MAX_GLOBAL_UNITS_PER_DAY);
    const maxGlobalBytes = cfgLimit(cfgMap.max_upload_bytes_per_day, MAX_GLOBAL_BYTES_PER_DAY);
    if (
      (maxGlobalUnits > 0 && totals.units + changed.length > maxGlobalUnits) ||
      (maxGlobalBytes > 0 && totals.bytes + changedBytes > maxGlobalBytes)
    )
      throw new HttpError(429, "daily_limit", "Today's upload limit is reached; the rest goes tomorrow.", {
        retryAfter: secondsToUtcMidnight(),
      });
  }

  const now = nowS();
  const upsert = env.DB.prepare(UPSERT_UNIT);
  const stmts = changed.map(r =>
    upsert.bind(
      r.id,
      player,
      who.id,
      r.kind,
      character,
      server,
      r.day,
      r.seq,
      r.zone,
      r.difficulty,
      r.levelStart,
      r.levelEnd,
      r.trio,
      r.race,
      r.rank,
      schema,
      app,
      engine,
      r.derived,
      r.factsBytes,
      now,
      r.hash,
    ),
  );
  stmts.push(
    env.DB.prepare(
      "INSERT INTO usage (install, day, units, requests, bytes) VALUES (?, ?, ?, 1, ?) " +
        "ON CONFLICT (install, day) DO UPDATE SET units = units + excluded.units, requests = requests + 1, bytes = bytes + excluded.bytes",
    ).bind(who.id, day, changed.length, changedBytes),
  );
  stmts.push(env.DB.prepare("UPDATE installs SET last_seen = ?, app = ? WHERE id = ?").bind(now, app, who.id));
  if (noteCharacter) stmts.push(noteCharacter);
  const results = await env.DB.batch(stmts);
  const accepted = results.slice(0, changed.length).reduce((n, r) => n + (r.meta?.changes || 0), 0);
  return json({ accepted, ignored: changed.length - accepted, unchanged: rows.length - changed.length });
}

/**
 * Up to 200 unit ids of one character that this install sent last and its app no longer builds (a hunt whose id moved, a
 * crawl a re-read put together differently), deleted. Only this install's own rows: a linked PC's stay, as it may still
 * build them, and another player's are never touched. An id that isn't held is no error. One request of the day, no units.
 */
export async function withdraw(env: Env, who: Who, body: Uint8Array): Promise<Response> {
  const req = parseJson(body);
  const character = str(req.character, "character", 32);
  if (!/^[0-9a-f]{8,32}$/.test(character)) throw bad("character");
  if (!Array.isArray(req.ids) || req.ids.length === 0) throw bad("ids");
  if (req.ids.length > MAX_ROWS_PER_UPLOAD)
    throw new HttpError(413, "too_many_rows", "At most " + MAX_ROWS_PER_UPLOAD + " ids per call.");
  const ids = (req.ids as unknown[]).map((id, i) => {
    if (typeof id !== "string" || !/^[0-9a-f]{16,40}$/.test(id)) throw bad("ids[" + i + "]");
    return id;
  });
  const day = today();
  const used = await env.DB.prepare("SELECT requests FROM usage WHERE install = ? AND day = ?")
    .bind(who.id, day)
    .first<{ requests: number }>();
  if ((used?.requests ?? 0) >= MAX_REQUESTS_PER_DAY)
    throw new HttpError(429, "daily_limit", "Today's limit is reached; the rest goes tomorrow.", {
      retryAfter: secondsToUtcMidnight(),
    });
  const [deleted] = await env.DB.batch([
    env.DB.prepare(
      "DELETE FROM units WHERE install = ? AND player = ? AND character = ? AND id IN (SELECT value FROM json_each(?))",
    ).bind(who.id, who.player, character, JSON.stringify(ids)),
    env.DB.prepare(
      "INSERT INTO usage (install, day, units, requests, bytes) VALUES (?, ?, 0, 1, 0) " +
        "ON CONFLICT (install, day) DO UPDATE SET requests = requests + 1",
    ).bind(who.id, day),
  ]);
  return json({ withdrawn: deleted.meta?.changes || 0 });
}

function cfgLimit(raw: string | undefined, fallback: number): number {
  const n = raw !== undefined ? parseInt(raw, 10) : NaN;
  return Number.isFinite(n) && n >= 0 ? n : fallback;
}

/** SHA-256 (hex) of everything a unit row stores but the app version and when it came in. */
async function contentHash(what: unknown): Promise<string> {
  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(JSON.stringify(what))));
  return Array.from(digest, b => b.toString(16).padStart(2, "0")).join("");
}

/** What the server holds for this player, for the Settings card. */
export async function me(env: Env, who: Who): Promise<Response> {
  const [kinds, installs] = await env.DB.batch<{ kind?: string; n: number; last?: number }>([
    env.DB.prepare("SELECT kind, COUNT(*) AS n, MAX(received) AS last FROM units WHERE player = ? GROUP BY kind").bind(
      who.player,
    ),
    env.DB.prepare("SELECT COUNT(*) AS n FROM installs WHERE player = ?").bind(who.player),
  ]);
  const counts: Record<string, number> = {};
  let last: number | null = null;
  for (const r of kinds.results) {
    counts[r.kind!] = r.n;
    if (r.last && (!last || r.last > last)) last = r.last;
  }
  return json({ player: who.player, install: who.id, counts, lastUpload: last, pcs: installs.results[0]?.n || 1 });
}

/** Delete everything this player shared, on every linked PC, and forget the installs. */
export async function forget(env: Env, who: Who): Promise<Response> {
  const res = await env.DB.batch([
    env.DB.prepare("DELETE FROM units WHERE player = ?").bind(who.player),
    env.DB.prepare("DELETE FROM codes WHERE player = ?").bind(who.player),
    env.DB.prepare("DELETE FROM usage WHERE install IN (SELECT id FROM installs WHERE player = ?)").bind(who.player),
    env.DB.prepare("DELETE FROM installs WHERE player = ?").bind(who.player),
    env.DB.prepare("DELETE FROM players WHERE id = ?").bind(who.player),
    // Starting over the same day is a new player: the connection's note of this one mustn't refuse it.
    env.DB.prepare("DELETE FROM upload_ips WHERE player = ?").bind(who.player),
  ]);
  return json({ deleted: res[0].meta?.changes || 0, pcs: res[3].meta?.changes || 0 });
}

/**
 * A one-time code (10 minutes) that lets another PC join this player. It carries the character salt so both PCs give
 * the same character codes; the salt is deleted with the code.
 */
export async function link(env: Env, who: Who, body: Uint8Array): Promise<Response> {
  const req = parseJson(body);
  const salt = str(req.salt, "salt", 100);
  const code = newCode();
  const expires = nowS() + LINK_TTL_S;
  await env.DB.batch([
    env.DB.prepare("DELETE FROM codes WHERE kind = 'link' AND install = ?").bind(who.id),
    env.DB.prepare(
      "INSERT INTO codes (hash, kind, player, install, expires, uses_left, payload) VALUES (?, 'link', ?, ?, ?, 1, ?)",
    ).bind(await codeHash(code), who.player, who.id, expires, salt),
  ]);
  return json({ code: pretty(code), expires });
}

/**
 * An install that already shares joins another player. Its rows were made with the old salt, so they're deleted and the
 * app sends them again with the new one.
 */
export async function claim(request: Request, env: Env, who: Who, body: Uint8Array): Promise<Response> {
  const req = parseJson(body);
  await limitCodeGuesses(request, env);
  const c = await takeCode(env, "link", str(req.code, "code", 20));
  if (c.player === who.player) return json({ player: who.player, salt: c.payload, moved: 0 });
  const res = await env.DB.batch([
    env.DB.prepare("DELETE FROM units WHERE install = ?").bind(who.id),
    env.DB.prepare("UPDATE installs SET player = ? WHERE id = ?").bind(c.player, who.id),
    env.DB.prepare("DELETE FROM players WHERE id = ? AND NOT EXISTS (SELECT 1 FROM installs WHERE player = ?)").bind(
      who.player,
      who.player,
    ),
    // Its rows come again under the new player and salt: the note of the old player mustn't refuse them.
    env.DB.prepare("DELETE FROM upload_ips WHERE player = ?").bind(who.player),
  ]);
  return json({ player: c.player, salt: c.payload, dropped: res[0].meta?.changes || 0 });
}

/** An enrollment code (24 hours, 1 use) for a friend, while the server requires one. */
export async function enroll(env: Env, who: Who): Promise<Response> {
  const now = nowS();
  const active = await env.DB.prepare(
    "SELECT COUNT(*) AS n FROM codes WHERE kind = 'enroll' AND player = ? AND expires > ? AND uses_left > 0",
  )
    .bind(who.player, now)
    .first<{ n: number }>();
  if ((active?.n || 0) >= MAX_ENROLL_ACTIVE)
    throw new HttpError(429, "too_many_codes", "You already have " + MAX_ENROLL_ACTIVE + " codes waiting to be used.");
  const code = newCode();
  await env.DB.prepare(
    "INSERT INTO codes (hash, kind, player, install, expires, uses_left) VALUES (?, 'enroll', ?, ?, ?, 1)",
  )
    .bind(await codeHash(code), who.player, who.id, now + ENROLL_TTL_S)
    .run();
  return json({ code: pretty(code), expires: now + ENROLL_TTL_S });
}
