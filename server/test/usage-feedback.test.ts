import { createExecutionContext, createScheduledController, waitOnExecutionContext } from "cloudflare:test";
import { env } from "cloudflare:workers";
import { describe, expect, it } from "vitest";
import worker from "../src/index.ts";
import { call } from "./helpers.ts";

const ID = "00000000000000000000000000c0ffee";
const today = () => new Date().toISOString().slice(0, 10);

describe("usage counts", () => {
  // docs/specs/usage-day-only.md: a summary goes up hourly, so any time kept beside its day would say when a copy ran.
  // A table with no time column can't store one, whatever a later Worker does: this fails if one comes back.
  it("keeps a summary's day and no time: its row is id, day, app, channel and body", async () => {
    const cols = await env.DB.prepare("SELECT name FROM pragma_table_info('app_usage') ORDER BY cid").all<{
      name: string;
    }>();
    expect(cols.results.map(c => c.name)).toEqual(["id", "day", "app", "channel", "body"]);
  });

  it("keeps only the known fields, each checked", async () => {
    const r = await call("POST", "/v1/usage", {
      v: 1,
      id: ID,
      day: today(),
      app: "0.9.30",
      channel: "weird",
      os: "10.0",
      firstSeen: "nope",
      counts: { "page.ask": 2, "BAD KEY": 5, "neg.count": -1 },
      stats: { "import.seconds": { n: 1, sum: 4.5, max: 4.5 }, bad: { n: "x" } },
      state: { sharing: "off" },
      errors: [{ kind: "probe", type: "System.Exception", at: "App.X.Y", n: 1 }, { n: 0 }],
      ip: "1.2.3.4",
      name: "Venun",
      received: 1795530185,
      time: "14:23:05",
      sentAt: "2026-11-24T14:23:05Z",
    });
    expect(r.body).toEqual({ ok: true });
    const row = await env.DB.prepare("SELECT * FROM app_usage WHERE id = ?")
      .bind(ID)
      .first<{ channel: string; body: string }>();
    expect(Object.keys(row!)).toEqual(["id", "day", "app", "channel", "body"]);
    expect(JSON.stringify(row)).not.toMatch(/14:23|1795530185|received|sentAt/);
    const body = JSON.parse(row!.body);
    expect(row!.channel).toBe("stable");
    expect(body.counts).toEqual({ "page.ask": 2 });
    expect(body.stats).toEqual({ "import.seconds": { n: 1, sum: 4.5, max: 4.5 } });
    expect(body.errors).toHaveLength(1);
    expect(body.firstSeen).toBeNull();
    expect(row!.body).not.toContain("Venun");
    expect(row!.body).not.toContain("1.2.3.4");
  });

  it("replaces a day sent again, and deletes a copy's days", async () => {
    await call("POST", "/v1/usage", { id: ID, day: today(), counts: { "page.ask": 1 } });
    await call("POST", "/v1/usage", { id: ID, day: today(), counts: { "page.ask": 3 } });
    expect((await env.DB.prepare("SELECT COUNT(*) AS n FROM app_usage").first<{ n: number }>())!.n).toBe(1);
    expect((await call("DELETE", "/v1/usage", { id: ID })).body).toEqual({ ok: true, deleted: 1 });
  });

  // docs/specs/usage-choice.md: the app asks again until a 2xx, so an id with no rows must answer 200 and not fail forever.
  it("answers a delete for an id with no rows with 200 and deleted: 0", async () => {
    const r = await call("DELETE", "/v1/usage", { id: ID });
    expect(r.status).toBe(200);
    expect(r.body).toEqual({ ok: true, deleted: 0 });
  });

  it("refuses a bad id or a day out of range", async () => {
    expect((await call("POST", "/v1/usage", { id: "nothex", day: today() })).body.field).toBe("id");
    expect((await call("POST", "/v1/usage", { id: ID, day: "2026-01-01" })).body.field).toBe("day");
  });
});

describe("feedback", () => {
  it("stores text as written, even text shaped like SQL", async () => {
    const text = "'); DROP TABLE units; --";
    expect((await call("POST", "/v1/feedback", { kind: "bug", text })).body).toEqual({
      ok: true,
      id: expect.any(Number),
    });
    const row = await env.DB.prepare("SELECT text FROM feedback").first<{ text: string }>();
    expect(row!.text).toBe(text);
    expect(await env.DB.prepare("SELECT COUNT(*) AS n FROM units").first()).toEqual({ n: 0 });
  });

  it("takes votes only on items that exist", async () => {
    expect((await call("POST", "/v1/vote", { item: "nope", voter: ID })).body.field).toBe("item");
  });
});

describe("the daily cleanup", () => {
  it("drops expired rows and keeps current ones", async () => {
    const old = new Date(Date.now() - 3 * 86400000).toISOString().slice(0, 10);
    await env.DB.batch([
      env.DB.prepare(
        "INSERT INTO upload_ips (code, day, server, character, player) VALUES ('a', ?, 's', 'c', 'p'), ('b', ?, 's', 'c', 'p')",
      ).bind(old, today()),
      env.DB.prepare("INSERT INTO register_ips (code, day, n) VALUES ('a', ?, 1), ('b', ?, 1)").bind(old, today()),
      env.DB.prepare("INSERT INTO app_usage (id, day, body) VALUES ('x', '2025-01-01', '{}'), ('y', ?, '{}')").bind(
        today(),
      ),
    ]);
    const ctx = createExecutionContext();
    await worker.scheduled(createScheduledController(), env, ctx);
    await waitOnExecutionContext(ctx);
    for (const table of ["upload_ips", "register_ips", "app_usage"])
      expect((await env.DB.prepare("SELECT COUNT(*) AS n FROM " + table).first<{ n: number }>())!.n).toBe(1);
  });
});
