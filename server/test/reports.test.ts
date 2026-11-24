// Report a problem: a report's window details, its screenshot (a one-time upload into the SHOTS bucket), Help → Your
// reports, and the screenshots' 90-day life.
import { createExecutionContext, createScheduledController, waitOnExecutionContext } from "cloudflare:test";
import { env, exports } from "cloudflare:workers";
import { describe, expect, it } from "vitest";
import worker from "../src/index.ts";
import { call, setConfig } from "./helpers.ts";

const VOTER = "0123456789abcdef0123456789abcdef";
const OTHER = "fedcba9876543210fedcba9876543210";
const PNG = new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52]);
const WEBP = new TextEncoder().encode("RIFF\x10\x00\x00\x00WEBPVP8 ");

// Each call from its own address: the per-address limit (10 a minute) is real here, and these tests make more than that.
let calls = 0;
const ip = () => {
  calls++;
  return `198.51.${Math.floor(calls / 250)}.${calls % 250}`;
};

async function put(id: number, bytes: Uint8Array, token?: string): Promise<{ status: number; body: any }> {
  const headers: Record<string, string> = { "content-type": "application/octet-stream", "cf-connecting-ip": ip() };
  if (token) headers["x-eqlp-upload"] = token;
  const res = await exports.default.fetch(
    new Request(`https://api.test/v1/feedback/${id}/shot`, { method: "PUT", headers, body: bytes }),
  );
  return { status: res.status, body: await res.json() };
}

const report = (extra: Record<string, unknown> = {}) =>
  call(
    "POST",
    "/v1/feedback",
    { kind: "bug", text: "The mote boxes clip their numbers", page: "upgrade", app: "0.9.37", voter: VOTER, ...extra },
    { ip: ip() },
  );

describe("a report", () => {
  it("keeps the window details, and asks for no screenshot unless one is coming", async () => {
    const r = await report({ details: "1280×800 window · 125% scaling · zoom 100% · Windows 10.0.26300" });
    expect(r.body).toEqual({ ok: true, id: expect.any(Number) });
    const row = await env.DB.prepare("SELECT details, shot, shot_token FROM feedback_extra WHERE id = ?")
      .bind(r.body.id)
      .first();
    expect(row).toEqual({
      details: "1280×800 window · 125% scaling · zoom 100% · Windows 10.0.26300",
      shot: null,
      shot_token: null,
    });
  });

  it("from an older app (no details, no screenshot) adds nothing beside the feedback row", async () => {
    await call("POST", "/v1/feedback", { kind: "idea", text: "more maps" });
    expect(await env.DB.prepare("SELECT COUNT(*) AS n FROM feedback_extra").first()).toEqual({ n: 0 });
  });
});

describe("a report's screenshot", () => {
  it("goes into the bucket once, with the report's own token", async () => {
    const r = await report({ shot: true });
    expect(r.body.upload).toMatch(/^[0-9a-f]{32}$/);
    expect((await put(r.body.id, PNG, r.body.upload)).body).toEqual({ ok: true });
    const row = await env.DB.prepare("SELECT shot, shot_token FROM feedback_extra WHERE id = ?")
      .bind(r.body.id)
      .first<{ shot: string; shot_token: string | null }>();
    expect(row!.shot).toMatch(new RegExp(`^\\d{4}-\\d{2}-\\d{2}/${r.body.id}\\.png$`));
    expect(row!.shot_token).toBeNull();
    const obj = await env.SHOTS!.get(row!.shot);
    expect(new Uint8Array(await obj!.arrayBuffer())).toEqual(PNG);
    expect(obj!.httpMetadata?.contentType).toBe("image/png");
    // The token is spent: a second upload can't replace it.
    expect((await put(r.body.id, WEBP, r.body.upload)).status).toBe(400);
  });

  it("is refused without the right token, or on another report", async () => {
    const a = await report({ shot: true });
    const b = await report({ shot: true });
    expect((await put(a.body.id, PNG)).body.field).toBe("upload");
    expect((await put(a.body.id, PNG, "0".repeat(32))).body.field).toBe("upload");
    expect((await put(b.body.id, PNG, a.body.upload)).body.field).toBe("upload");
    expect((await put(a.body.id, PNG, a.body.upload)).body).toEqual({ ok: true });
  });

  it("must be a PNG, JPEG or WebP image, and not too big", async () => {
    const r = await report({ shot: true });
    expect(
      (await put(r.body.id, new TextEncoder().encode("<html>not an image</html>"), r.body.upload)).body.field,
    ).toBe("shot");
    const big = new Uint8Array(1_500_001);
    big.set(PNG);
    expect((await put(r.body.id, big, r.body.upload)).status).toBe(413);
    expect((await put(r.body.id, WEBP, r.body.upload)).body).toEqual({ ok: true });
    const row = await env.DB.prepare("SELECT shot FROM feedback_extra WHERE id = ?")
      .bind(r.body.id)
      .first<{ shot: string }>();
    expect(row!.shot.endsWith(".webp")).toBe(true);
  });

  it("stops being offered once the day's screenshots are used up, and the report still goes", async () => {
    await setConfig("max_shots_per_day", "1");
    const first = await report({ shot: true });
    expect(first.body.upload).toBeTruthy();
    const second = await report({ shot: true });
    expect(second.body).toEqual({ ok: true, id: expect.any(Number), shot: "limit" });
    expect(await env.DB.prepare("SELECT COUNT(*) AS n FROM feedback").first()).toEqual({ n: 2 });
  });
});

describe("Your reports", () => {
  it("lists only this copy's reports, newest first, with the day and never a time", async () => {
    const a = await report({ text: "first" });
    await call("POST", "/v1/feedback", { kind: "idea", text: "someone else's", voter: OTHER });
    const b = await report({ text: "second", kind: "confusing", shot: true });
    await put(b.body.id, PNG, b.body.upload);
    await env.DB.batch([
      env.DB.prepare("UPDATE feedback SET status = 'done', received = received - 100 WHERE id = ?").bind(a.body.id),
      env.DB.prepare("INSERT INTO feedback_extra (id, reply) VALUES (?, 'Fixed in 0.9.38, thanks!')").bind(a.body.id),
    ]);
    const r = await call("GET", "/v1/feedback?voter=" + VOTER);
    expect(r.body.items).toEqual([
      {
        id: b.body.id,
        day: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
        kind: "confusing",
        text: "second",
        page: "upgrade",
        status: "new",
        reply: null,
        shot: true,
      },
      {
        id: a.body.id,
        day: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
        kind: "bug",
        text: "first",
        page: "upgrade",
        status: "done",
        reply: "Fixed in 0.9.38, thanks!",
        shot: false,
      },
    ]);
    expect(JSON.stringify(r.body)).not.toContain("someone else");
  });

  it("needs a voter id", async () => {
    expect((await call("GET", "/v1/feedback")).body.field).toBe("voter");
    expect((await call("GET", "/v1/feedback?voter=abc")).body.field).toBe("voter");
  });
});

describe("the daily cleanup of screenshots", () => {
  it("deletes screenshots after 90 days, strays from an upload cut off halfway, and unused tokens after a day", async () => {
    const old = await report({ shot: true });
    await put(old.body.id, PNG, old.body.upload);
    const fresh = await report({ shot: true });
    await put(fresh.body.id, PNG, fresh.body.upload);
    const unused = await report({ shot: true });
    const day = 86400;
    await env.DB.batch([
      env.DB.prepare("UPDATE feedback SET received = received - ? WHERE id = ?").bind(91 * day, old.body.id),
      env.DB.prepare("UPDATE feedback SET received = received - ? WHERE id = ?").bind(2 * day, unused.body.id),
    ]);
    const stray = new Date(Date.now() - 95 * day * 1000).toISOString().slice(0, 10) + "/999999.png";
    await env.SHOTS!.put(stray, PNG);
    const keys = await env.DB.prepare("SELECT id, shot FROM feedback_extra WHERE shot IS NOT NULL").all<{
      id: number;
      shot: string;
    }>();
    const oldKey = keys.results.find(k => k.id === old.body.id)!.shot,
      freshKey = keys.results.find(k => k.id === fresh.body.id)!.shot;

    const ctx = createExecutionContext();
    await worker.scheduled(createScheduledController(), env, ctx);
    await waitOnExecutionContext(ctx);

    expect(await env.SHOTS!.head(oldKey)).toBeNull();
    expect(await env.SHOTS!.head(stray)).toBeNull();
    expect(await env.SHOTS!.head(freshKey)).not.toBeNull();
    const rows = await env.DB.prepare("SELECT id, shot, shot_token FROM feedback_extra ORDER BY id").all<{
      id: number;
      shot: string | null;
      shot_token: string | null;
    }>();
    expect(rows.results).toEqual([
      { id: old.body.id, shot: null, shot_token: null },
      { id: fresh.body.id, shot: freshKey, shot_token: null },
      { id: unused.body.id, shot: null, shot_token: null },
    ]);
    // The report itself stays.
    expect(await env.DB.prepare("SELECT COUNT(*) AS n FROM feedback").first()).toEqual({ n: 3 });
  });
});
