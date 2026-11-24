// Anonymous usage counts (docs/usage.md): one summary per copy per day, replaced when sent again. No key, no address kept.

import { limitPerAddress } from "./auth.ts";
import type { Env } from "./env.ts";
import { bad, HEX32, HttpError, json, optStr, parseJson, str } from "./http.ts";

const USAGE_KEY = /^[a-z][a-z0-9_.-]{0,63}$/;
const USAGE_DAY = /^\d{4}-\d{2}-\d{2}$/;
export const USAGE_MAX_BODY = 32 * 1024;

export const limitUsage = (request: Request, env: Env) =>
  limitPerAddress(request, env, "use:", "That's a lot at once. Wait a minute and try again.");

/**
 * One copy's day: the app's random usage id, the day, and only the fields below, each checked and cut to size. Whatever
 * else arrives is dropped. Sent again during the day, the row is replaced.
 */
export async function usage(env: Env, body: Uint8Array): Promise<Response> {
  if (body.length > USAGE_MAX_BODY) throw new HttpError(413, "too_big", "That summary is too big.");
  const req = parseJson(body);
  const id = str(req.id, "id", 32);
  if (!HEX32.test(id)) throw bad("id");
  const day = str(req.day, "day", 10);
  const t = Date.parse(day + "T00:00:00Z");
  if (!USAGE_DAY.test(day) || !(t > Date.now() - 16 * 86400000 && t < Date.now() + 2 * 86400000)) throw bad("day");
  const clean = {
    v: Number.isInteger(req.v) ? req.v : 0,
    app: optStr(req.app, 40),
    channel: req.channel === "beta" ? "beta" : "stable",
    os: optStr(req.os, 40),
    firstSeen: typeof req.firstSeen === "string" && USAGE_DAY.test(req.firstSeen) ? req.firstSeen : null,
    updatedFrom: optStr(req.updatedFrom, 40),
    counts: usageMap(req.counts, 200, v =>
      Number.isInteger(v) && (v as number) >= 0 && (v as number) <= 10000000 ? v : undefined,
    ),
    stats: usageMap(req.stats, 50, v => {
      const s = v as { n?: unknown; sum?: unknown; max?: unknown } | null;
      return s &&
        typeof s === "object" &&
        Number.isInteger(s.n) &&
        (s.n as number) >= 0 &&
        Number.isFinite(s.sum) &&
        Number.isFinite(s.max)
        ? { n: s.n, sum: s.sum, max: s.max }
        : undefined;
    }),
    state: usageMap(req.state, 20, v => (typeof v === "string" ? v.slice(0, 40) : undefined)),
    errors: Array.isArray(req.errors)
      ? (req.errors as unknown[])
          .slice(0, 20)
          .filter(
            (e): e is Record<string, unknown> =>
              !!e &&
              typeof e === "object" &&
              Number.isInteger((e as Record<string, unknown>).n) &&
              ((e as Record<string, unknown>).n as number) > 0,
          )
          .map(e => ({
            kind: optStr(e.kind, 40),
            type: optStr(e.type, 100),
            at: optStr(e.at, 160),
            n: Math.min(e.n as number, 10000000),
          }))
      : [],
  };
  await env.DB.prepare(
    "INSERT INTO app_usage (id, day, app, channel, body) VALUES (?, ?, ?, ?, ?) " +
      "ON CONFLICT (id, day) DO UPDATE SET app = excluded.app, channel = excluded.channel, body = excluded.body",
  )
    .bind(id, day, clean.app, clean.channel, JSON.stringify(clean))
    .run();
  return json({ ok: true });
}

/** Switching usage counts off in the app deletes every day that copy sent. */
export async function forgetUsage(env: Env, body: Uint8Array): Promise<Response> {
  const req = parseJson(body);
  const id = str(req.id, "id", 32);
  if (!HEX32.test(id)) throw bad("id");
  const res = await env.DB.prepare("DELETE FROM app_usage WHERE id = ?").bind(id).run();
  return json({ ok: true, deleted: res.meta ? res.meta.changes : 0 });
}

/** An object of at most `max` entries whose keys look like "page.ask" and whose values pass `check` (undefined = dropped). */
function usageMap(v: unknown, max: number, check: (value: unknown) => unknown): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  if (!v || typeof v !== "object" || Array.isArray(v)) return out;
  let n = 0;
  for (const [k, value] of Object.entries(v)) {
    if (n >= max) break;
    if (!USAGE_KEY.test(k)) continue;
    const c = check(value);
    if (c === undefined) continue;
    out[k] = c;
    n++;
  }
  return out;
}
