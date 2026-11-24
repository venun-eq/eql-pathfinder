// Server hardening tests: base64/gzip validation on facts, daily upload request fuse, per-connection screenshot cap,
// atomic screenshot token claiming, per-route body caps, API_LIMIT rate limiting, minApp check, and security headers.
import { createExecutionContext, createScheduledController, waitOnExecutionContext } from "cloudflare:test";
import { env, exports } from "cloudflare:workers";
import { describe, expect, it } from "vitest";
import worker from "../src/index.ts";
import { MAX_BYTES_PER_DAY, MAX_FACTS, MAX_REQUESTS_PER_DAY } from "../src/sharing.ts";
import { call, newClient, registered, setConfig, upload } from "./helpers.ts";

const VOTER = "11223344556677889900aabbccddeeff";
const PNG = new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52]);
// Minimal valid gzip header + payload (gzip of "{}")
const VALID_GZIP_B64 = "H4sIAAAAAAAAA6uuBQBDv6ajAgAAAA==";

describe("sharing upload validation and daily request fuse", () => {
  it("rejects invalid base64 or non-gzip facts with 400 instead of 500, and accepts valid gzip", async () => {
    const { client } = await registered("198.51.100.101");

    const badB64 = upload("aaaaaaaa", "oggok", 1, "1111111111111111");
    (badB64.rows[0] as Record<string, unknown>).facts = "!!!not-base64!!!";
    const r1 = await call("POST", "/v1/summaries", badB64, { client, ip: "198.51.100.101" });
    expect(r1.status).toBe(400);
    expect(r1.body).toMatchObject({ error: "bad_field", field: "rows[0].facts" });

    const notGzip = upload("aaaaaaaa", "oggok", 1, "1111111111111111");
    (notGzip.rows[0] as Record<string, unknown>).facts = btoa('{"not":"gzipped"}');
    const r2 = await call("POST", "/v1/summaries", notGzip, { client, ip: "198.51.100.101" });
    expect(r2.status).toBe(400);
    expect(r2.body).toMatchObject({ error: "bad_field", field: "rows[0].facts" });

    const good = upload("aaaaaaaa", "oggok", 1, "1111111111111111");
    (good.rows[0] as Record<string, unknown>).facts = VALID_GZIP_B64;
    const r3 = await call("POST", "/v1/summaries", good, { client, ip: "198.51.100.101" });
    expect(r3.status).toBe(200);
    expect(r3.body).toEqual({ accepted: 1, ignored: 0, unchanged: 0 });

    // Re-sending the exact same gzip unit is still recognized as unchanged.
    const r4 = await call("POST", "/v1/summaries", good, { client, ip: "198.51.100.101" });
    expect(r4.body).toEqual({ accepted: 0, ignored: 0, unchanged: 1 });
  });

  it("rejects zone, server, trio, race, and app with control characters", async () => {
    const { client } = await registered("198.51.100.102");
    const badZone = upload("aaaaaaaa", "oggok", 1, "2222222222222222");
    (badZone.rows[0] as Record<string, unknown>).zone = "Befallen\nInjected";
    expect((await call("POST", "/v1/summaries", badZone, { client, ip: "198.51.100.102" })).body).toMatchObject({
      error: "bad_field",
      field: "rows[0].zone",
    });

    const badServer = upload("aaaaaaaa", "oggok\r\n", 1, "2222222222222223");
    expect((await call("POST", "/v1/summaries", badServer, { client, ip: "198.51.100.102" })).body).toMatchObject({
      error: "bad_field",
      field: "server",
    });

    const badTrio = upload("aaaaaaaa", "oggok", 1, "2222222222222224");
    (badTrio.rows[0] as Record<string, unknown>).trio = "BER\x00";
    expect((await call("POST", "/v1/summaries", badTrio, { client, ip: "198.51.100.102" })).body).toMatchObject({
      error: "bad_field",
      field: "rows[0].trio",
    });

    const badRace = upload("aaaaaaaa", "oggok", 1, "2222222222222225");
    (badRace.rows[0] as Record<string, unknown>).race = "Ogre\tTab";
    expect((await call("POST", "/v1/summaries", badRace, { client, ip: "198.51.100.102" })).body).toMatchObject({
      error: "bad_field",
      field: "rows[0].race",
    });
  });

  it("rejects invalid calendar dates and far-future days in summaries", async () => {
    const { client } = await registered("198.51.100.105");
    for (const badDay of ["2026-02-31", "9999-99-99", "2099-01-01"]) {
      const payload = upload("aaaaaaaa", "oggok", 1, "4444444444444444");
      payload.rows[0].day = badDay;
      const r = await call("POST", "/v1/summaries", payload, { client, ip: "198.51.100.105" });
      expect(r.status).toBe(400);
      expect(r.body).toMatchObject({ error: "bad_field", field: "rows[0].day" });
    }
  });

  it("enforces min_app on summaries for already-registered installs", async () => {
    const { client } = await registered("198.51.100.106");
    await setConfig("min_app", "0.9.50");
    const r = await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok", 1, "5555555555555555"), {
      client,
      ip: "198.51.100.106",
    });
    expect(r.status).toBe(426);
    expect(r.body.error).toBe("update_app");
  });

  it("treats missing or empty X-EQLP-Time as unsigned rather than clock skew", async () => {
    const { client } = await registered("198.51.100.107");
    const res = await exports.default.fetch(
      new Request("https://api.test/v1/me", {
        method: "GET",
        headers: {
          "cf-connecting-ip": "198.51.100.107",
          "x-eqlp-install": client.install,
          "x-eqlp-time": "",
          "x-eqlp-signature": "AAAA",
        },
      }),
    );
    expect(res.status).toBe(401);
    expect(await res.json()).toMatchObject({ error: "unsigned" });
  });

  it("enforces the daily request-count fuse even on unchanged re-sends and emits Retry-After", async () => {
    const { client } = await registered("198.51.100.103");
    await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok", 1, "3333333333333333"), {
      client,
      ip: "198.51.100.103",
    });
    await env.DB.prepare("UPDATE usage SET requests = ?").bind(MAX_REQUESTS_PER_DAY).run();
    const body = new TextEncoder().encode(JSON.stringify(upload("aaaaaaaa", "oggok", 1, "3333333333333333")));
    const headers = {
      "content-type": "application/json",
      "cf-connecting-ip": "198.51.100.103",
      ...(await client.sign("POST", "/v1/summaries", body)),
    };
    const blocked = await exports.default.fetch(
      new Request("https://api.test/v1/summaries", { method: "POST", headers, body }),
    );
    expect(blocked.status).toBe(429);
    expect(Number(blocked.headers.get("retry-after"))).toBeGreaterThan(0);
    expect(await blocked.json()).toMatchObject({ error: "daily_limit" });
  });

  it("lets a changed upload through at the request-count fuse (the unit and byte caps bound it)", async () => {
    const { client } = await registered("198.51.100.108");
    await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok", 1, "4444444444444441"), {
      client,
      ip: "198.51.100.108",
    });
    await env.DB.prepare("UPDATE usage SET requests = ?").bind(MAX_REQUESTS_PER_DAY).run();
    const res = await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok", 1, "4444444444444442"), {
      client,
      ip: "198.51.100.108",
    });
    expect(res.status).toBe(200);
  });

  it("does not allow bypassing min_app by omitting app on register", async () => {
    const client = await newClient();
    const r = await call("POST", "/v1/register", { publicKey: client.publicKey }, { client, ip: "198.51.100.104" });
    expect(r.status).toBe(426);
    expect(r.body.error).toBe("update_app");
  });
});

describe("per-connection screenshot cap and atomic token consumption", () => {
  it("caps screenshots per connection per day without blocking other connections, and prunes shot_ips after 2 days", async () => {
    await setConfig("max_shots_per_ip", "2");
    const addr = "198.51.100.120";
    const first = await call(
      "POST",
      "/v1/feedback",
      { kind: "bug", text: "one", voter: VOTER, shot: true },
      { ip: addr },
    );
    const second = await call(
      "POST",
      "/v1/feedback",
      { kind: "bug", text: "two", voter: VOTER, shot: true },
      { ip: addr },
    );
    const third = await call(
      "POST",
      "/v1/feedback",
      { kind: "bug", text: "three", voter: VOTER, shot: true },
      { ip: addr },
    );
    expect(first.body.upload).toMatch(/^[0-9a-f]{32}$/);
    expect(second.body.upload).toMatch(/^[0-9a-f]{32}$/);
    expect(third.body).toEqual({ ok: true, id: expect.any(Number), shot: "limit" });

    // Another connection still gets a screenshot token.
    const other = await call(
      "POST",
      "/v1/feedback",
      { kind: "bug", text: "other", voter: VOTER, shot: true },
      { ip: "198.51.100.121" },
    );
    expect(other.body.upload).toMatch(/^[0-9a-f]{32}$/);

    // shot_ips stores only 32-hex daily codes (never IPs) and daily cleanup prunes rows older than 2 days.
    const rows = (await env.DB.prepare("SELECT * FROM shot_ips").all<{ code: string; day: string; n: number }>())
      .results;
    expect(rows).toHaveLength(2);
    for (const r of rows) {
      expect(r.code).toMatch(/^[0-9a-f]{32}$/);
      expect(JSON.stringify(r)).not.toContain("198.51.100");
    }
    const oldDay = new Date(Date.now() - 3 * 86400000).toISOString().slice(0, 10);
    await env.DB.prepare("INSERT INTO shot_ips (code, day, n) VALUES ('deadbeefdeadbeefdeadbeefdeadbeef', ?, 1)")
      .bind(oldDay)
      .run();
    const ctx = createExecutionContext();
    await worker.scheduled(createScheduledController(), env, ctx);
    await waitOnExecutionContext(ctx);
    expect((await env.DB.prepare("SELECT COUNT(*) AS n FROM shot_ips").first<{ n: number }>())!.n).toBe(2);
  });

  it("allows only one winner when the same screenshot upload token is sent concurrently", async () => {
    const r = await call(
      "POST",
      "/v1/feedback",
      { kind: "bug", text: "race", voter: VOTER, shot: true },
      { ip: "198.51.100.130" },
    );
    const putOnce = (addr: string) =>
      exports.default.fetch(
        new Request(`https://api.test/v1/feedback/${r.body.id}/shot`, {
          method: "PUT",
          headers: {
            "content-type": "application/octet-stream",
            "cf-connecting-ip": addr,
            "x-eqlp-upload": r.body.upload,
          },
          body: PNG,
        }),
      );
    const [a, b] = await Promise.all([putOnce("198.51.100.131"), putOnce("198.51.100.132")]);
    const statuses = [a.status, b.status].sort();
    expect(statuses).toEqual([200, 400]);
  });
});

describe("body caps, rate limits and response headers", () => {
  it("rejects oversized request bodies per route with 413", async () => {
    const bigText = "x".repeat(70 * 1024);
    const fb = await call("POST", "/v1/feedback", { kind: "bug", text: bigText }, { ip: "198.51.100.140" });
    expect(fb.status).toBe(413);
    expect(fb.body.error).toBe("too_big");

    const client = await newClient();
    const reg = await call(
      "POST",
      "/v1/register",
      { publicKey: client.publicKey, app: "0.9.30", pad: "x".repeat(20 * 1024) },
      { client, ip: "198.51.100.141" },
    );
    expect(reg.status).toBe(413);
    expect(reg.body.error).toBe("too_big");
  });

  it("aborts oversized streamed bodies without Content-Length before buffering them", async () => {
    let pulled = 0;
    const stream = new ReadableStream<Uint8Array>({
      pull(controller) {
        pulled++;
        if (pulled > 100) {
          controller.close();
          return;
        }
        controller.enqueue(new Uint8Array(16 * 1024));
      },
    });
    const res = await exports.default.fetch(
      new Request("https://api.test/v1/feedback", {
        method: "POST",
        headers: { "content-type": "application/json", "cf-connecting-ip": "198.51.100.142" },
        body: stream,
      }),
    );
    expect(res.status).toBe(413);
    expect(await res.json()).toMatchObject({ error: "too_big" });
    expect(pulled).toBeLessThan(15);
  });

  it("creates D1 indexes for pruning and quota lookups", async () => {
    const { results } = await env.DB.prepare(
      "SELECT name FROM sqlite_master WHERE type = 'index' AND name IN ('units_day', 'units_install', 'feedback_received', 'codes_player', 'codes_expires', 'usage_day')",
    ).all<{ name: string }>();
    expect(results.map(r => r.name).sort()).toEqual([
      "codes_expires",
      "codes_player",
      "feedback_received",
      "units_day",
      "units_install",
      "usage_day",
    ]);
  });

  it("rate-limits read and registration routes via API_LIMIT and sets security headers on JSON responses", async () => {
    const res = await exports.default.fetch(
      new Request("https://api.test/v1/config", { headers: { "cf-connecting-ip": "198.51.100.150" } }),
    );
    expect(res.headers.get("x-content-type-options")).toBe("nosniff");
    expect(res.headers.get("referrer-policy")).toBe("no-referrer");
    expect(res.headers.get("cache-control")).toBe("no-store");

    // 60/min on GET /v1/roadmap from one dedicated address: the 61st is slowed down with Retry-After.
    const spamIp = "198.51.100.199";
    let throttled = false;
    for (let i = 0; i < 65; i++) {
      const r = await exports.default.fetch(
        new Request("https://api.test/v1/roadmap", { headers: { "cf-connecting-ip": spamIp } }),
      );
      if (r.status === 429) {
        expect(r.headers.get("retry-after")).toBe("60");
        expect(await r.json()).toMatchObject({ error: "slow_down" });
        throttled = true;
        break;
      }
      expect(r.status).toBe(200);
    }
    expect(throttled).toBe(true);
  });
});

describe("upload byte budgets, R2 token recovery, feedback/vote daily caps, and workers.dev config", () => {
  it("enforces the 128 KiB per-row facts cap and tracks per-install and global daily upload byte/unit budgets", async () => {
    expect(MAX_FACTS).toBe(128 * 1024);
    const { client: a } = await registered("198.51.100.201");

    const tooBigFacts = upload("aaaaaaaa", "oggok", 1, "6666666666666661");
    // Starts with valid gzip magic ("H4sI" = 1f 8b 08), so only MAX_FACTS rejects it.
    (tooBigFacts.rows[0] as Record<string, unknown>).facts = "H4sI" + "A".repeat(128 * 1024);
    const rBig = await call("POST", "/v1/summaries", tooBigFacts, { client: a, ip: "198.51.100.201" });
    expect(rBig.status).toBe(400);
    expect(rBig.body).toMatchObject({ error: "bad_field", field: "rows[0].facts" });

    // Valid upload records stored facts (22 B) + derived ('{"k":1}' = 7 B) = 29 B in usage.bytes; unchanged re-send adds 0 bytes.
    const good = upload("aaaaaaaa", "oggok", 1, "6666666666666662");
    (good.rows[0] as Record<string, unknown>).facts = VALID_GZIP_B64;
    (good.rows[0] as Record<string, unknown>).derived = { k: 1 };
    expect((await call("POST", "/v1/summaries", good, { client: a, ip: "198.51.100.201" })).status).toBe(200);
    const used1 = await env.DB.prepare("SELECT units, bytes FROM usage WHERE install = ?")
      .bind(a.install)
      .first<{ units: number; bytes: number }>();
    expect(used1!.bytes).toBe(29);
    await call("POST", "/v1/summaries", good, { client: a, ip: "198.51.100.201" });
    const used2 = await env.DB.prepare("SELECT bytes FROM usage WHERE install = ?")
      .bind(a.install)
      .first<{ bytes: number }>();
    expect(used2!.bytes).toBe(used1!.bytes);

    // Near MAX_BYTES_PER_DAY: unchanged re-send still passes, but a new row that crosses the byte budget gets 429 daily_limit.
    await env.DB.prepare("UPDATE usage SET bytes = ? WHERE install = ?")
      .bind(MAX_BYTES_PER_DAY - 5, a.install)
      .run();
    expect((await call("POST", "/v1/summaries", good, { client: a, ip: "198.51.100.201" })).status).toBe(200);
    const extra = upload("aaaaaaaa", "oggok", 1, "6666666666666663");
    (extra.rows[0] as Record<string, unknown>).facts = VALID_GZIP_B64;
    const overInstall = await call("POST", "/v1/summaries", extra, { client: a, ip: "198.51.100.201" });
    expect(overInstall.status).toBe(429);
    expect(overInstall.body.error).toBe("daily_limit");

    // Global daily fuses block another install once today's total across installs hits max_upload_bytes_per_day or max_units_per_day.
    const { client: b } = await registered("198.51.100.202");
    await setConfig("max_upload_bytes_per_day", "100");
    const bUpload = upload("bbbbbbbb", "oggok", 1, "6666666666666664");
    (bUpload.rows[0] as Record<string, unknown>).facts = VALID_GZIP_B64;
    expect((await call("POST", "/v1/summaries", bUpload, { client: b, ip: "198.51.100.202" })).status).toBe(429);

    await setConfig("max_upload_bytes_per_day", "0");
    await setConfig("max_units_per_day", "1");
    expect((await call("POST", "/v1/summaries", bUpload, { client: b, ip: "198.51.100.202" })).status).toBe(429);

    await setConfig("max_units_per_day", "0");
    expect((await call("POST", "/v1/summaries", bUpload, { client: b, ip: "198.51.100.202" })).status).toBe(200);
  });

  it("restores the one-time screenshot upload token when R2 put fails so a retry succeeds", async () => {
    const r = await call(
      "POST",
      "/v1/feedback",
      { kind: "bug", text: "r2 retry", voter: VOTER, shot: true },
      { ip: "198.51.100.210" },
    );
    const brokenEnv = {
      ...env,
      SHOTS: { ...env.SHOTS, put: () => Promise.reject(new Error("R2 transient failure")) } as unknown as R2Bucket,
    };
    const makeReq = (addr: string) =>
      new Request(`https://api.test/v1/feedback/${r.body.id}/shot`, {
        method: "PUT",
        headers: {
          "content-type": "application/octet-stream",
          "cf-connecting-ip": addr,
          "x-eqlp-upload": r.body.upload,
        },
        body: PNG,
      });
    const failed = await worker.fetch(makeReq("198.51.100.211"), brokenEnv);
    expect(failed.status).toBe(500);
    // The token was claimed, then handed back: a 500 raised before the claim would leave it unchanged but this proves the restore.
    const restored = await env.DB.prepare("SELECT shot_token FROM feedback_extra WHERE id = ?")
      .bind(r.body.id)
      .first<{ shot_token: string | null }>();
    expect(restored!.shot_token).toBe(r.body.upload);

    const retried = await exports.default.fetch(makeReq("198.51.100.212"));
    expect(retried.status).toBe(200);
    expect(await retried.json()).toEqual({ ok: true });
  });

  it("caps daily feedback reports and roadmap votes per connection and globally, and prunes feedback_ips after 2 days", async () => {
    await setConfig("max_feedback_per_ip", "2");
    const addr = "198.51.100.220";
    // Malformed 400 request does not consume the connection's daily feedback allowance.
    expect((await call("POST", "/v1/feedback", { kind: "bug", text: "   ", voter: VOTER }, { ip: addr })).status).toBe(
      400,
    );
    expect((await call("POST", "/v1/feedback", { kind: "bug", text: "fb 1", voter: VOTER }, { ip: addr })).status).toBe(
      200,
    );
    expect((await call("POST", "/v1/feedback", { kind: "bug", text: "fb 2", voter: VOTER }, { ip: addr })).status).toBe(
      200,
    );
    const fb3 = await call("POST", "/v1/feedback", { kind: "bug", text: "fb 3", voter: VOTER }, { ip: addr });
    expect(fb3.status).toBe(429);

    // Another connection works until the global max_feedback_per_day fuse is reached; "0" disables both caps.
    await setConfig("max_feedback_per_day", "3");
    expect(
      (await call("POST", "/v1/feedback", { kind: "bug", text: "fb other", voter: VOTER }, { ip: "198.51.100.221" }))
        .status,
    ).toBe(200);
    expect(
      (
        await call(
          "POST",
          "/v1/feedback",
          { kind: "bug", text: "fb global cap", voter: VOTER },
          { ip: "198.51.100.222" },
        )
      ).status,
    ).toBe(429);
    await setConfig("max_feedback_per_day", "0");
    await setConfig("max_feedback_per_ip", "0");
    expect(
      (await call("POST", "/v1/feedback", { kind: "bug", text: "fb lifted", voter: VOTER }, { ip: addr })).status,
    ).toBe(200);

    // Roadmap votes per connection: 400 doesn't count, capped when adding votes, on: false always works, and "0" disables.
    await env.DB.prepare(
      "INSERT INTO roadmap (id, title, about, status, sort) VALUES ('item-a', 'A', 'a', 'planned', 1), ('item-b', 'B', 'b', 'planned', 2), ('item-c', 'C', 'c', 'planned', 3)",
    ).run();
    await setConfig("max_votes_per_ip", "2");
    const voteIp = "198.51.100.223";
    expect(
      (await call("POST", "/v1/vote", { item: "nonexistent", voter: VOTER, on: true }, { ip: voteIp })).status,
    ).toBe(400);
    expect((await call("POST", "/v1/vote", { item: "item-a", voter: VOTER, on: true }, { ip: voteIp })).status).toBe(
      200,
    );
    expect((await call("POST", "/v1/vote", { item: "item-b", voter: VOTER, on: true }, { ip: voteIp })).status).toBe(
      200,
    );
    expect((await call("POST", "/v1/vote", { item: "item-c", voter: VOTER, on: true }, { ip: voteIp })).status).toBe(
      429,
    );
    expect((await call("POST", "/v1/vote", { item: "item-a", voter: VOTER, on: false }, { ip: voteIp })).status).toBe(
      200,
    );
    // Casting a vote the voter already has (item-b) doesn't count against the cap, even at the cap.
    expect((await call("POST", "/v1/vote", { item: "item-b", voter: VOTER, on: true }, { ip: voteIp })).status).toBe(
      200,
    );
    await setConfig("max_votes_per_ip", "0");
    expect((await call("POST", "/v1/vote", { item: "item-c", voter: VOTER, on: true }, { ip: voteIp })).status).toBe(
      200,
    );

    // feedback_ips stores only 32-hex daily codes and prunes rows older than 2 days.
    const oldDay = new Date(Date.now() - 3 * 86400000).toISOString().slice(0, 10);
    await env.DB.prepare(
      "INSERT INTO feedback_ips (code, day, feedback, votes) VALUES ('deadbeefdeadbeefdeadbeefdeadbeef', ?, 1, 1)",
    )
      .bind(oldDay)
      .run();
    const before = (await env.DB.prepare("SELECT * FROM feedback_ips").all<{ code: string }>()).results;
    for (const r of before) {
      expect(r.code).toMatch(/^[0-9a-f]{32}$/);
      expect(JSON.stringify(r)).not.toContain("198.51.100");
    }
    const ctx = createExecutionContext();
    await worker.scheduled(createScheduledController(), env, ctx);
    await waitOnExecutionContext(ctx);
    const after = (await env.DB.prepare("SELECT * FROM feedback_ips WHERE day = ?").bind(oldDay).all()).results;
    expect(after).toHaveLength(0);
  });
});
