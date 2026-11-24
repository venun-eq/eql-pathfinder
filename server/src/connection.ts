// Brakes on made-up players, keyed by a daily code for the internet connection (docs/community-job.md, "Fake players").
import type { Env } from "./env.ts";
import { HttpError } from "./http.ts";
import { hex, secondsToUtcMidnight, today } from "./util.ts";

/**
 * A code for this connection today, made from its address with HMAC-SHA256 using the IP_SECRET Worker secret and the day:
 * the address itself is never stored, and codes from different days can't be matched. Rows keyed by it are deleted after
 * 2 days. Null when the secret isn't set (then the brakes are skipped and the gap is logged, rather than refusing everyone).
 */
export async function connectionCode(request: Request, env: Env, day: string): Promise<string | null> {
  if (!env.IP_SECRET) {
    console.error("IP_SECRET isn't set: the per-connection brakes are off");
    return null;
  }
  const ip = request.headers.get("cf-connecting-ip") || "unknown";
  const enc = new TextEncoder();
  const key = await crypto.subtle.importKey(
    "raw",
    enc.encode(env.IP_SECRET),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const mac = await crypto.subtle.sign("HMAC", key, enc.encode("eqlp-ip:" + day + ":" + ip));
  return hex(new Uint8Array(mac)).slice(0, 32);
}

/**
 * One character per game server per connection (EverQuest Legends allows one character per server
 * on an account). A second character, or the same one under a second player
 * (whose character codes are salted differently, so it looks like a second character), is refused for the rest of the day.
 * Config one_character_per_server = "0" turns it off. Returns the statement that notes this upload's character, or null.
 */
export async function oneCharacterPerServer(
  request: Request,
  env: Env,
  server: string | null,
  character: string,
  player: string,
): Promise<D1PreparedStatement | null> {
  const off = await env.DB.prepare("SELECT value FROM config WHERE key = 'one_character_per_server'").first<{
    value: string;
  }>();
  if (off && off.value === "0") return null;
  const day = today();
  const code = await connectionCode(request, env, day);
  if (!code) return null;
  const srv = server || "";
  const row = await env.DB.prepare("SELECT character, player FROM upload_ips WHERE code = ? AND day = ? AND server = ?")
    .bind(code, day, srv)
    .first<{ character: string; player: string }>();
  if (row && (row.character !== character || row.player !== player))
    throw new HttpError(
      409,
      "one_character_per_server",
      "Another character on this game server already shares from this internet connection today. Pathfinder shares one character per server per connection.",
      { retryAfter: secondsToUtcMidnight() },
    );
  if (row) return null;
  return env.DB.prepare(
    "INSERT OR IGNORE INTO upload_ips (code, day, server, character, player) VALUES (?, ?, ?, ?, ?)",
  ).bind(code, day, srv, character, player);
}

/** Report screenshots one connection may attach in a UTC day, unless config max_shots_per_ip says otherwise ("0" = off). */
export const MAX_SHOTS_PER_IP = 5;
/** Feedback reports one connection may send in a UTC day, unless config max_feedback_per_ip says otherwise ("0" = off). */
export const MAX_FEEDBACK_PER_IP = 10;
/** Roadmap votes one connection may cast in a UTC day, unless config max_votes_per_ip says otherwise ("0" = off). */
export const MAX_VOTES_PER_IP = 50;

/**
 * At most `max` new players a day from one connection (config max_new_players_per_ip; 0 or missing = off).
 * Returns the statement that counts this one, to run with the registration, or throws when the connection is at its limit.
 */
export async function newPlayerLimit(request: Request, env: Env, max: number): Promise<D1PreparedStatement | null> {
  const day = today();
  const code = await connectionCode(request, env, day);
  if (!code) return null;
  const row = await env.DB.prepare("SELECT n FROM register_ips WHERE code = ? AND day = ?")
    .bind(code, day)
    .first<{ n: number }>();
  if (row && row.n >= max)
    throw new HttpError(
      429,
      "too_many_players",
      "Too many new players from this connection today. Try again tomorrow, or link this PC to the player you already have.",
      { retryAfter: secondsToUtcMidnight() },
    );
  return env.DB.prepare(
    "INSERT INTO register_ips (code, day, n) VALUES (?, ?, 1) ON CONFLICT (code, day) DO UPDATE SET n = n + 1",
  ).bind(code, day);
}

/**
 * At most `max_shots_per_ip` (default 5) report screenshots a day from one connection, so one caller can't exhaust the
 * global daily screenshot quota for every other player. Returns whether another screenshot is allowed and the D1 statement
 * that counts it.
 */
export async function shotLimit(
  request: Request,
  env: Env,
): Promise<{ allowed: boolean; note: D1PreparedStatement | null }> {
  const cfg = await env.DB.prepare("SELECT value FROM config WHERE key = 'max_shots_per_ip'").first<{
    value: string;
  }>();
  const parsed = cfg ? parseInt(cfg.value, 10) : NaN;
  const max = Number.isFinite(parsed) && parsed >= 0 ? parsed : MAX_SHOTS_PER_IP;
  if (max === 0) return { allowed: true, note: null };
  const day = today();
  const code = await connectionCode(request, env, day);
  if (!code) return { allowed: true, note: null };
  const row = await env.DB.prepare("SELECT n FROM shot_ips WHERE code = ? AND day = ?")
    .bind(code, day)
    .first<{ n: number }>();
  if (row && row.n >= max) return { allowed: false, note: null };
  return {
    allowed: true,
    note: env.DB.prepare(
      "INSERT INTO shot_ips (code, day, n) VALUES (?, ?, 1) ON CONFLICT (code, day) DO UPDATE SET n = n + 1",
    ).bind(code, day),
  };
}

async function connectionCounterLimit(
  request: Request,
  env: Env,
  configKey: string,
  defaultMax: number,
  column: "feedback" | "votes",
  errorCode: string,
  message: string,
): Promise<D1PreparedStatement | null> {
  const cfg = await env.DB.prepare("SELECT value FROM config WHERE key = ?").bind(configKey).first<{ value: string }>();
  const parsed = cfg ? parseInt(cfg.value, 10) : NaN;
  const max = Number.isFinite(parsed) && parsed >= 0 ? parsed : defaultMax;
  if (max === 0) return null;
  const day = today();
  const code = await connectionCode(request, env, day);
  if (!code) return null;
  const selectSql =
    column === "feedback"
      ? "SELECT feedback AS n FROM feedback_ips WHERE code = ? AND day = ?"
      : "SELECT votes AS n FROM feedback_ips WHERE code = ? AND day = ?";
  const row = await env.DB.prepare(selectSql).bind(code, day).first<{ n: number }>();
  if (row && row.n >= max) throw new HttpError(429, errorCode, message, { retryAfter: secondsToUtcMidnight() });
  const upsertSql =
    column === "feedback"
      ? "INSERT INTO feedback_ips (code, day, feedback, votes) VALUES (?, ?, 1, 0) ON CONFLICT (code, day) DO UPDATE SET feedback = feedback + 1"
      : "INSERT INTO feedback_ips (code, day, feedback, votes) VALUES (?, ?, 0, 1) ON CONFLICT (code, day) DO UPDATE SET votes = votes + 1";
  return env.DB.prepare(upsertSql).bind(code, day);
}

/** At most `max_feedback_per_ip` (default 10) feedback reports a day from one connection ("0" = off). */
export const feedbackLimit = (request: Request, env: Env): Promise<D1PreparedStatement | null> =>
  connectionCounterLimit(
    request,
    env,
    "max_feedback_per_ip",
    MAX_FEEDBACK_PER_IP,
    "feedback",
    "daily_limit",
    "You've sent a lot of feedback today. Try again tomorrow.",
  );

/** At most `max_votes_per_ip` (default 50) roadmap votes cast a day from one connection ("0" = off). */
export const voteLimit = (request: Request, env: Env): Promise<D1PreparedStatement | null> =>
  connectionCounterLimit(
    request,
    env,
    "max_votes_per_ip",
    MAX_VOTES_PER_IP,
    "votes",
    "daily_limit",
    "Too many votes from this connection today. Try again tomorrow.",
  );
