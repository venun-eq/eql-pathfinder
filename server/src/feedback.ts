// Feedback and the roadmap: no account, an anonymous voter id from the app, limited per address.

import { limitPerAddress } from "./auth.ts";
import { feedbackLimit, shotLimit, voteLimit } from "./connection.ts";
import type { Env } from "./env.ts";
import { bad, HEX32, HttpError, json, optStr, parseJson, str } from "./http.ts";
import { nowS, randomHex, secondsToUtcMidnight } from "./util.ts";

const FEEDBACK_KINDS = ["bug", "idea", "confusing", "other"];

export const limitFeedback = (request: Request, env: Env) =>
  limitPerAddress(request, env, "fb:", "That's a lot at once. Wait a minute and try again.");

interface RoadmapRow {
  id: string;
  title: string;
  about: string;
  status: string;
  shipped: string | null;
  votes: number;
  mine: number;
}

/** What's coming (the roadmap table, edited by hand), each item with its votes and whether this voter wants it. */
export async function roadmap(env: Env, voter: string | null): Promise<Response> {
  const v = typeof voter === "string" && HEX32.test(voter) ? voter : "";
  const { results } = await env.DB.prepare(
    "SELECT r.id, r.title, r.about, r.status, r.shipped, " +
      "(SELECT COUNT(*) FROM votes x WHERE x.item = r.id) AS votes, " +
      "EXISTS (SELECT 1 FROM votes x WHERE x.item = r.id AND x.voter = ?) AS mine " +
      "FROM roadmap r ORDER BY r.sort, r.id",
  )
    .bind(v)
    .all<RoadmapRow>();
  return json({
    items: results.map(r => ({
      id: r.id,
      title: r.title,
      about: r.about,
      status: r.status,
      shipped: r.shipped,
      votes: r.votes,
      mine: !!r.mine,
    })),
  });
}

/** One vote per voter per item; on: false takes it back (always allowed, even at the daily vote cap). Shipped items take no votes. */
export async function vote(request: Request, env: Env, body: Uint8Array): Promise<Response> {
  const req = parseJson(body);
  const item = str(req.item, "item", 40);
  const voter = str(req.voter, "voter", 32);
  if (!HEX32.test(voter)) throw bad("voter");
  if (!(await env.DB.prepare("SELECT 1 FROM roadmap WHERE id = ? AND status != 'shipped'").bind(item).first()))
    throw bad("item");
  if (req.on === false) {
    await env.DB.prepare("DELETE FROM votes WHERE item = ? AND voter = ?").bind(item, voter).run();
  } else if (!(await env.DB.prepare("SELECT 1 FROM votes WHERE item = ? AND voter = ?").bind(item, voter).first())) {
    // A vote the voter already has changes nothing, so it doesn't count against the cap.
    const noteVote = await voteLimit(request, env);
    const stmts = [
      env.DB.prepare("INSERT OR IGNORE INTO votes (item, voter, at) VALUES (?, ?, ?)").bind(item, voter, nowS()),
    ];
    if (noteVote) stmts.push(noteVote);
    await env.DB.batch(stmts);
  }
  return roadmap(env, voter);
}

/** A screenshot's most bytes (the app sends PNG, or WebP when PNG is bigger than this). */
export const MAX_SHOT = 1_500_000;
/** Screenshots are deleted this many days after the report. */
export const KEEP_SHOT_DAYS = 90;
/** Screenshots taken in a UTC day, all players together, unless config max_shots_per_day says otherwise: a bound on storage. */
const SHOTS_PER_DAY = 50;
/** Feedback reports accepted in a UTC day, all players together, unless config max_feedback_per_day says otherwise ("0" = off).
 *  Sized for the public launch on Workers Paid (11-24): a first week of strangers can send hundreds, and a row is small. */
const FEEDBACK_PER_DAY = 1000;

/**
 * What a player wrote, as they wrote it: the kind, the text, the app version and page, a name only if they gave one, and
 * their error log only if they ticked the box. A report from "Report a problem" adds its window details and, when shot is
 * true, gets a one-time upload token for its screenshot (PUT /v1/feedback/<id>/shot); shot: "limit" when today's are used up.
 */
export async function feedback(request: Request, env: Env, body: Uint8Array): Promise<Response> {
  const req = parseJson(body);
  const kind = typeof req.kind === "string" && FEEDBACK_KINDS.includes(req.kind) ? req.kind : "other";
  const text = str(typeof req.text === "string" ? req.text.trim() : req.text, "text", 4000);
  const app = optStr(req.app, 40);
  const page = optStr(req.page, 40);
  const contact = optStr(req.contact, 60);
  const errors = optStr(req.errors, 20000);
  const details = optStr(req.details, 200);
  const voter = typeof req.voter === "string" && HEX32.test(req.voter) ? req.voter : null;
  const now = nowS();
  const noteFeedbackIp = await feedbackLimit(request, env);
  const maxFbDay = await feedbackPerDay(env);
  if (maxFbDay > 0 && (await feedbackToday(env, now)) >= maxFbDay)
    throw new HttpError(429, "daily_limit", "Today's feedback limit is reached. Try again tomorrow.", {
      retryAfter: secondsToUtcMidnight(),
    });

  const row = await env.DB.prepare(
    "INSERT INTO feedback (received, kind, text, app, page, contact, voter, errors) VALUES (?, ?, ?, ?, ?, ?, ?, ?) RETURNING id",
  )
    .bind(now, kind, text, app, page, contact, voter, errors)
    .first<{ id: number }>();
  const id = row!.id;
  let upload: string | null = null,
    shot: string | undefined;
  let noteShotIp: D1PreparedStatement | null = null;
  if (req.shot === true && env.SHOTS) {
    const ipShot = await shotLimit(request, env);
    if (ipShot.allowed && (await shotsToday(env, now)) < (await shotsPerDay(env))) {
      upload = randomHex(16);
      noteShotIp = ipShot.note;
    } else {
      shot = "limit";
    }
  }
  const extraStmts: D1PreparedStatement[] = [];
  if (details || upload)
    extraStmts.push(
      env.DB.prepare("INSERT INTO feedback_extra (id, details, shot_token) VALUES (?, ?, ?)").bind(id, details, upload),
    );
  if (noteFeedbackIp) extraStmts.push(noteFeedbackIp);
  if (noteShotIp) extraStmts.push(noteShotIp);
  if (extraStmts.length) await env.DB.batch(extraStmts);
  return json({ ok: true, id, ...(upload ? { upload } : {}), ...(shot ? { shot } : {}) });
}

async function feedbackPerDay(env: Env): Promise<number> {
  const v = await env.DB.prepare("SELECT value FROM config WHERE key = 'max_feedback_per_day'").first<{
    value: string;
  }>();
  const n = v ? parseInt(v.value, 10) : NaN;
  return Number.isFinite(n) && n >= 0 ? n : FEEDBACK_PER_DAY;
}

async function feedbackToday(env: Env, now: number): Promise<number> {
  const dayStart = now - (now % 86400);
  const r = await env.DB.prepare("SELECT COUNT(*) AS n FROM feedback WHERE received >= ?")
    .bind(dayStart)
    .first<{ n: number }>();
  return r?.n ?? 0;
}

async function shotsPerDay(env: Env): Promise<number> {
  const v = await env.DB.prepare("SELECT value FROM config WHERE key = 'max_shots_per_day'").first<{ value: string }>();
  const n = v ? parseInt(v.value, 10) : NaN;
  return Number.isFinite(n) && n >= 0 ? n : SHOTS_PER_DAY;
}

/** Screenshots taken or promised since this UTC day began. */
async function shotsToday(env: Env, now: number): Promise<number> {
  const dayStart = now - (now % 86400);
  const r = await env.DB.prepare(
    "SELECT COUNT(*) AS n FROM feedback_extra x JOIN feedback f ON f.id = x.id " +
      "WHERE f.received >= ? AND (x.shot IS NOT NULL OR x.shot_token IS NOT NULL)",
  )
    .bind(dayStart)
    .first<{ n: number }>();
  return r?.n ?? 0;
}

/** PNG, JPEG or WebP, told by the file's first bytes (the content-type header isn't trusted). */
function imageType(b: Uint8Array): { ext: string; type: string } | null {
  if (
    b.length > 8 &&
    b[0] === 0x89 &&
    b[1] === 0x50 &&
    b[2] === 0x4e &&
    b[3] === 0x47 &&
    b[4] === 0x0d &&
    b[5] === 0x0a &&
    b[6] === 0x1a &&
    b[7] === 0x0a
  )
    return { ext: "png", type: "image/png" };
  if (b.length > 3 && b[0] === 0xff && b[1] === 0xd8 && b[2] === 0xff) return { ext: "jpg", type: "image/jpeg" };
  const ascii = (from: number, n: number) => String.fromCharCode(...b.subarray(from, from + n));
  if (b.length > 12 && ascii(0, 4) === "RIFF" && ascii(8, 4) === "WEBP") return { ext: "webp", type: "image/webp" };
  return null;
}

/**
 * The screenshot for report `id`, as the raw image: only with that report's upload token (header x-eqlp-upload), only once,
 * and only within a day of the report. Read as bytes and stored as they are: nothing is decoded on the Worker.
 */
export async function attachShot(request: Request, env: Env, id: number, body: Uint8Array): Promise<Response> {
  if (!env.SHOTS) throw new HttpError(503, "no_shots", "Screenshots can't be stored right now.");
  const token = request.headers.get("x-eqlp-upload") ?? "";
  if (!HEX32.test(token)) throw bad("upload");
  if (body.length === 0 || body.length > MAX_SHOT) throw new HttpError(413, "too_big", "That screenshot is too big.");
  const image = imageType(body);
  if (!image) throw bad("shot");
  // Claim the one-time token atomically before writing to R2, so concurrent uploads with the same token can't both win.
  const claimed = await env.DB.prepare(
    "UPDATE feedback_extra SET shot_token = NULL " +
      "WHERE id = ? AND shot_token = ? AND shot IS NULL " +
      "AND EXISTS (SELECT 1 FROM feedback f WHERE f.id = feedback_extra.id AND f.received >= ?) " +
      "RETURNING id",
  )
    .bind(id, token, nowS() - 86400)
    .first<{ id: number }>();
  if (!claimed) throw bad("upload");
  const row = await env.DB.prepare("SELECT received FROM feedback WHERE id = ?").bind(id).first<{ received: number }>();
  if (!row) throw bad("upload");
  const key = `${new Date(row.received * 1000).toISOString().slice(0, 10)}/${id}.${image.ext}`;
  try {
    await env.SHOTS.put(key, body, { httpMetadata: { contentType: image.type } });
  } catch (e) {
    // Give the one-time token back when the bucket write fails, so a retry can still attach the screenshot.
    await env.DB.prepare(
      "UPDATE feedback_extra SET shot_token = ? WHERE id = ? AND shot_token IS NULL AND shot IS NULL",
    )
      .bind(token, id)
      .run()
      .catch(() => {});
    throw e;
  }
  await env.DB.prepare("UPDATE feedback_extra SET shot = ? WHERE id = ?").bind(key, id).run();
  return json({ ok: true });
}

interface MineRow {
  id: number;
  received: number;
  kind: string;
  text: string;
  page: string | null;
  status: string;
  reply: string | null;
  shot: number;
}

/** Help → Your reports: this copy's last 30 reports, each with its status and any note back. The day only, never a time. */
export async function myReports(env: Env, voter: string | null): Promise<Response> {
  if (typeof voter !== "string" || !HEX32.test(voter)) throw bad("voter");
  const { results } = await env.DB.prepare(
    "SELECT f.id, f.received, f.kind, substr(f.text, 1, 300) AS text, f.page, f.status, x.reply, x.shot IS NOT NULL AS shot " +
      "FROM feedback f LEFT JOIN feedback_extra x ON x.id = f.id WHERE f.voter = ? ORDER BY f.received DESC, f.id DESC LIMIT 30",
  )
    .bind(voter)
    .all<MineRow>();
  return json({
    items: results.map(r => ({
      id: r.id,
      day: new Date(r.received * 1000).toISOString().slice(0, 10),
      kind: r.kind,
      text: r.text,
      page: r.page,
      status: r.status,
      reply: r.reply,
      shot: !!r.shot,
    })),
  });
}

/**
 * Daily: screenshots older than 90 days go, from the bucket and from their rows, and upload tokens nobody used expire.
 * Days 91–98 back are swept by prefix too, so a screenshot whose row never got its key (an upload cut off halfway) goes as well.
 */
export async function cleanShots(env: Env): Promise<void> {
  const now = nowS(),
    cutoff = now - KEEP_SHOT_DAYS * 86400;
  if (env.SHOTS) {
    const { results } = await env.DB.prepare(
      "SELECT x.shot FROM feedback_extra x JOIN feedback f ON f.id = x.id " +
        "WHERE x.shot IS NOT NULL AND f.received < ? LIMIT 1000",
    )
      .bind(cutoff)
      .all<{ shot: string }>();
    if (results.length) await env.SHOTS.delete(results.map(r => r.shot));
    for (let d = KEEP_SHOT_DAYS + 1; d <= KEEP_SHOT_DAYS + 8; d++) {
      const prefix = new Date((now - d * 86400) * 1000).toISOString().slice(0, 10) + "/";
      let cursor: string | undefined;
      do {
        const page = await env.SHOTS.list({ prefix, cursor });
        if (page.objects.length) await env.SHOTS.delete(page.objects.map(o => o.key));
        cursor = page.truncated ? page.cursor : undefined;
      } while (cursor);
    }
  }
  await env.DB.batch([
    env.DB.prepare(
      "UPDATE feedback_extra SET shot = NULL WHERE shot IS NOT NULL AND id IN (SELECT id FROM feedback WHERE received < ?)",
    ).bind(cutoff),
    env.DB.prepare(
      "UPDATE feedback_extra SET shot_token = NULL WHERE shot_token IS NOT NULL AND id IN (SELECT id FROM feedback WHERE received < ?)",
    ).bind(now - 86400),
  ]);
}
