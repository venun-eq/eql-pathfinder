// EQL Pathfinder community API: a Cloudflare Worker over one D1 database (binding DB). cloudflare.config.ts has the
// configuration and how it deploys; docs/community-job.md and docs/usage.md say what's kept and why.

import { authenticate, limitApi } from "./auth.ts";
import type { Env } from "./env.ts";
import { attachShot, cleanShots, feedback, limitFeedback, MAX_SHOT, myReports, roadmap, vote } from "./feedback.ts";
import { HttpError, json } from "./http.ts";
import { claim, config, enroll, forget, link, me, register, summaries, withdraw } from "./sharing.ts";
import { forgetUsage, limitUsage, USAGE_MAX_BODY, usage } from "./usage.ts";
import { nowS } from "./util.ts";

const MAX_BODY = 6 * 1024 * 1024;
const MAX_SMALL_BODY = 16 * 1024;
const MAX_TINY_BODY = 4 * 1024;
const MAX_FEEDBACK_BODY = 64 * 1024;

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    try {
      return await route(request, env);
    } catch (e) {
      if (e instanceof HttpError) {
        const headers =
          typeof e.extra.retryAfter === "number" && e.extra.retryAfter > 0
            ? { "retry-after": String(Math.ceil(e.extra.retryAfter)) }
            : undefined;
        return json({ error: e.code, message: e.message, ...e.extra }, e.status, headers);
      }
      console.error(e instanceof Error && e.stack ? e.stack : String(e));
      return json({ error: "server", message: "Something went wrong on the server." }, 500);
    }
  },

  // Daily: raw rows are kept 12 months; codes and counters only as long as they're useful.
  async scheduled(_event: ScheduledController, env: Env, _ctx: ExecutionContext): Promise<void> {
    const now = nowS();
    await env.DB.batch([
      env.DB.prepare("DELETE FROM units WHERE day < date('now', '-365 days')"),
      env.DB.prepare("DELETE FROM codes WHERE expires < ?").bind(now),
      env.DB.prepare("DELETE FROM usage WHERE day < date('now', '-7 days')"),
      env.DB.prepare("DELETE FROM app_usage WHERE day < date('now', '-400 days')"),
      env.DB.prepare("DELETE FROM register_ips WHERE day < date('now', '-2 days')"),
      env.DB.prepare("DELETE FROM upload_ips WHERE day < date('now', '-2 days')"),
      env.DB.prepare("DELETE FROM shot_ips WHERE day < date('now', '-2 days')"),
      env.DB.prepare("DELETE FROM feedback_ips WHERE day < date('now', '-2 days')"),
    ]);
    // Report screenshots after 90 days (a failure here is logged and tried again tomorrow).
    try {
      await cleanShots(env);
    } catch (e) {
      console.error(e instanceof Error && e.stack ? e.stack : String(e));
    }
  },
} satisfies ExportedHandler<Env>;

async function readBody(request: Request, maxBytes: number): Promise<Uint8Array> {
  const length = Number(request.headers.get("content-length") || 0);
  if (Number.isFinite(length) && length > maxBytes) throw new HttpError(413, "too_big", "That upload is too big.");
  if (!request.body) return new Uint8Array(0);
  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      total += value.byteLength;
      if (total > maxBytes) {
        await reader.cancel().catch(() => {});
        throw new HttpError(413, "too_big", "That upload is too big.");
      }
      chunks.push(value);
    }
  } finally {
    reader.releaseLock();
  }
  if (chunks.length === 0) return new Uint8Array(0);
  if (chunks.length === 1) return chunks[0];
  const out = new Uint8Array(total);
  let offset = 0;
  for (const c of chunks) {
    out.set(c, offset);
    offset += c.byteLength;
  }
  return out;
}

async function route(request: Request, env: Env): Promise<Response> {
  const url = new URL(request.url);
  const path = url.pathname.replace(/\/+$/, "") || "/";
  const method = request.method;

  if (method === "GET" && path === "/") return json({ name: "EQL Pathfinder community API", versions: ["v1"] });
  if (method === "GET" && path === "/v1/config") {
    await limitApi(request, env, "rd:");
    return json(await config(env));
  }

  if (method === "POST" && path === "/v1/register") {
    await limitApi(request, env, "reg:");
    return register(request, env, path, await readBody(request, MAX_SMALL_BODY));
  }

  // Feedback and the roadmap: no account, an anonymous voter id from the app, limited per address.
  if (method === "GET" && path === "/v1/roadmap") {
    await limitApi(request, env, "rd:");
    return roadmap(env, url.searchParams.get("voter"));
  }
  if (method === "POST" && path === "/v1/vote") {
    await limitFeedback(request, env);
    return vote(request, env, await readBody(request, MAX_TINY_BODY));
  }
  if (method === "POST" && path === "/v1/feedback") {
    await limitFeedback(request, env);
    return feedback(request, env, await readBody(request, MAX_FEEDBACK_BODY));
  }
  // A report's screenshot, as the raw image, with the one-time token the report was given.
  const shot = method === "PUT" ? /^\/v1\/feedback\/(\d{1,12})\/shot$/.exec(path) : null;
  if (shot) {
    await limitFeedback(request, env);
    return attachShot(request, env, Number(shot[1]), await readBody(request, MAX_SHOT));
  }
  // Help → Your reports: what this copy sent, with its status.
  if (method === "GET" && path === "/v1/feedback") {
    await limitApi(request, env, "rd:");
    return myReports(env, url.searchParams.get("voter"));
  }

  // Anonymous usage counts: one summary per copy per day, replaced when sent again. No key, no address kept.
  if (method === "POST" && path === "/v1/usage") {
    await limitUsage(request, env);
    return usage(env, await readBody(request, USAGE_MAX_BODY));
  }
  if (method === "DELETE" && path === "/v1/usage") {
    await limitUsage(request, env);
    return forgetUsage(env, await readBody(request, MAX_TINY_BODY));
  }

  await limitApi(request, env, "api:");
  const maxSignedBody =
    method === "POST" && path === "/v1/summaries"
      ? MAX_BODY
      : method === "POST" && (path === "/v1/link" || path === "/v1/link/claim")
        ? MAX_SMALL_BODY
        : method === "POST" && path === "/v1/units/withdraw"
          ? MAX_SMALL_BODY // 200 ids are about 9 KB
          : MAX_TINY_BODY;
  const body = await readBody(request, maxSignedBody);
  const who = await authenticate(request, env, method, path, body);
  if (method === "POST" && path === "/v1/summaries") return summaries(request, env, who, body);
  if (method === "POST" && path === "/v1/units/withdraw") return withdraw(env, who, body);
  if (method === "GET" && path === "/v1/me") return me(env, who);
  if (method === "DELETE" && path === "/v1/me") return forget(env, who);
  if (method === "POST" && path === "/v1/link") return link(env, who, body);
  if (method === "POST" && path === "/v1/link/claim") return claim(request, env, who, body);
  if (method === "POST" && path === "/v1/enroll") return enroll(env, who);
  throw new HttpError(404, "not_found", "No such endpoint.");
}
