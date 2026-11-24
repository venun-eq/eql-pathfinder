// The per-connection brakes on made-up players (docs/community-job.md, "Fake players").
import { env } from "cloudflare:workers";
import { describe, expect, it } from "vitest";
import { connectionCode } from "../src/connection.ts";
import { call, newClient, registered, setConfig, upload } from "./helpers.ts";

describe("one character per game server per connection", () => {
  it("refuses a second character on the same server from the same connection, for the rest of the day", async () => {
    const { client } = await registered();
    expect((await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok"), { client })).status).toBe(200);
    const second = await call("POST", "/v1/summaries", upload("bbbbbbbb", "oggok", 2, "1111111111111111"), { client });
    expect(second.status).toBe(409);
    expect(second.body.error).toBe("one_character_per_server");
    expect(second.body.retryAfter).toBeGreaterThan(0);
    // The first character keeps going.
    expect((await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok"), { client })).status).toBe(200);
  });

  it("allows one character on each server, and the same server from another connection", async () => {
    const { client } = await registered();
    await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok"), { client });
    expect(
      (await call("POST", "/v1/summaries", upload("bbbbbbbb", "lanys", 2, "2222222222222222"), { client })).status,
    ).toBe(200);
    expect(
      (
        await call("POST", "/v1/summaries", upload("cccccccc", "oggok", 2, "3333333333333333"), {
          client,
          ip: "192.0.2.50",
        })
      ).status,
    ).toBe(200);
  });

  it("treats a second player from the same connection as a second character", async () => {
    const { client: a } = await registered("192.0.2.1");
    const { client: b } = await registered("192.0.2.2");
    await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok"), { client: a, ip: "192.0.2.9" });
    const r = await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok", 2, "4444444444444444"), {
      client: b,
      ip: "192.0.2.9",
    });
    expect(r.body.error).toBe("one_character_per_server");
  });

  it("lets a player who deleted everything start over the same day", async () => {
    const { client: a } = await registered();
    await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok"), { client: a });
    await call("DELETE", "/v1/me", undefined, { client: a });
    const { client: again } = await registered();
    expect(
      (await call("POST", "/v1/summaries", upload("dddddddd", "oggok", 2, "5555555555555555"), { client: again }))
        .status,
    ).toBe(200);
  });

  it("lets a PC that joined another player send its rows again under the new salt", async () => {
    const { client: home } = await registered("192.0.2.20");
    const { client: laptop } = await registered("192.0.2.20");
    // The laptop shared on its own first, then joins the home PC's player (same person, so the same connection).
    await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok"), { client: laptop, ip: "192.0.2.20" });
    const link = (await call("POST", "/v1/link", { salt: "c2FsdA==" }, { client: home, ip: "192.0.2.20" })).body.code;
    expect((await call("POST", "/v1/link/claim", { code: link }, { client: laptop, ip: "192.0.2.20" })).status).toBe(
      200,
    );
    expect(
      (
        await call("POST", "/v1/summaries", upload("eeeeeeee", "oggok", 2, "6666666666666666"), {
          client: laptop,
          ip: "192.0.2.20",
        })
      ).status,
    ).toBe(200);
  });

  it("is off when the config says so", async () => {
    await setConfig("one_character_per_server", "0");
    const { client } = await registered();
    await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok"), { client });
    expect(
      (await call("POST", "/v1/summaries", upload("bbbbbbbb", "oggok", 2, "7777777777777777"), { client })).status,
    ).toBe(200);
  });
});

describe("new players per connection", () => {
  it("is off by default", async () => {
    await registered("192.0.2.30");
    await registered("192.0.2.30");
    await registered("192.0.2.30");
  });

  it("caps new players from one connection a day; linking a PC doesn't count", async () => {
    await setConfig("max_new_players_per_ip", "1");
    const { client: a } = await registered("192.0.2.40");
    const b = await newClient();
    const refused = await call(
      "POST",
      "/v1/register",
      { publicKey: b.publicKey, app: "0.9.30" },
      { client: b, ip: "192.0.2.40" },
    );
    expect(refused.status).toBe(429);
    expect(refused.body.error).toBe("too_many_players");
    await registered("192.0.2.41"); // another connection is fine
    const link = (await call("POST", "/v1/link", { salt: "c2FsdA==" }, { client: a, ip: "192.0.2.40" })).body.code;
    const pc2 = await newClient();
    expect(
      (
        await call(
          "POST",
          "/v1/register",
          { publicKey: pc2.publicKey, app: "0.9.30", link },
          { client: pc2, ip: "192.0.2.40" },
        )
      ).status,
    ).toBe(200);
  });
});

describe("addresses", () => {
  it("are never stored: only 32-hex daily codes", async () => {
    await setConfig("max_new_players_per_ip", "5");
    const { client } = await registered("203.0.113.99");
    await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok"), { client, ip: "203.0.113.99" });
    const rows = [
      ...(await env.DB.prepare("SELECT * FROM upload_ips").all()).results,
      ...(await env.DB.prepare("SELECT * FROM register_ips").all()).results,
    ];
    expect(rows.length).toBe(2);
    for (const r of rows) {
      expect(JSON.stringify(r)).not.toContain("203.0.113");
      expect(r.code).toMatch(/^[0-9a-f]{32}$/);
    }
  });

  it("make a different code each day, and none without the secret", async () => {
    const req = new Request("https://api.test/", { headers: { "cf-connecting-ip": "203.0.113.5" } });
    const monday = await connectionCode(req, env, "2026-11-16");
    expect(monday).toMatch(/^[0-9a-f]{32}$/);
    expect(await connectionCode(req, env, "2026-11-17")).not.toBe(monday);
    expect(await connectionCode(req, { ...env, IP_SECRET: undefined }, "2026-11-16")).toBeNull();
  });
});
