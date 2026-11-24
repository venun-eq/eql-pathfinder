import { env } from "cloudflare:workers";
import { describe, expect, it } from "vitest";
import { call, newClient, registered, setConfig, upload } from "./helpers.ts";

describe("public endpoints", () => {
  it("says what it is and hands out the config", async () => {
    expect((await call("GET", "/")).body.name).toBe("EQL Pathfinder community API");
    const cfg = await call("GET", "/v1/config");
    expect(cfg.status).toBe(200);
    expect(cfg.body).toMatchObject({ requireEnrollment: false, minApp: "0.8.0", maxNewPlayersPerIp: 0 });
  });

  it("refuses unknown endpoints and unsigned requests", async () => {
    expect((await call("GET", "/v1/nothing")).status).toBe(401); // everything past the public routes needs a signature
    expect((await call("GET", "/v1/me")).body.error).toBe("unsigned");
  });
});

describe("signatures", () => {
  it("rejects a request signed by another key", async () => {
    const { client } = await registered();
    const other = await newClient();
    const forged = {
      ...client,
      sign: (m: string, p: string, b: Uint8Array) =>
        other.sign(m, p, b).then(h => ({ ...h, "x-eqlp-install": client.install })),
    };
    const r = await call("GET", "/v1/me", undefined, { client: forged });
    expect(r.status).toBe(401);
  });

  it("rejects a body changed after signing", async () => {
    const { client } = await registered();
    const signed = { ...client, sign: (m: string, p: string) => client.sign(m, p, new TextEncoder().encode("{}")) };
    const r = await call("POST", "/v1/summaries", upload("aaaaaaaa"), { client: signed });
    expect(r.body.error).toBe("bad_signature");
  });
});

describe("registering", () => {
  it("makes a player once, then says it exists", async () => {
    const client = await newClient();
    const first = await call("POST", "/v1/register", { publicKey: client.publicKey, app: "0.9.30" }, { client });
    const again = await call("POST", "/v1/register", { publicKey: client.publicKey, app: "0.9.30" }, { client });
    expect(first.status).toBe(200);
    expect(again.body).toMatchObject({ existing: true, player: first.body.player });
  });

  it("asks old apps to update", async () => {
    const client = await newClient();
    const r = await call("POST", "/v1/register", { publicKey: client.publicKey, app: "0.7.5" }, { client });
    expect(r.body.error).toBe("update_app");
  });

  it("needs an invite code while sharing is invite-only, and a code works once", async () => {
    const { client: a } = await registered("198.51.100.1");
    await setConfig("require_enrollment", "1");
    const b = await newClient();
    expect(
      (await call("POST", "/v1/register", { publicKey: b.publicKey, app: "0.9.30" }, { client: b, ip: "198.51.100.2" }))
        .body.error,
    ).toBe("enrollment_required");
    const code = (await call("POST", "/v1/enroll", {}, { client: a, ip: "198.51.100.1" })).body.code;
    const ok = await call(
      "POST",
      "/v1/register",
      { publicKey: b.publicKey, app: "0.9.30", enroll: code },
      { client: b, ip: "198.51.100.2" },
    );
    expect(ok.status).toBe(200);
    const c = await newClient();
    expect(
      (
        await call(
          "POST",
          "/v1/register",
          { publicKey: c.publicKey, app: "0.9.30", enroll: code },
          { client: c, ip: "198.51.100.3" },
        )
      ).body.error,
    ).toBe("bad_code");
  });

  it("links a second PC to the same player", async () => {
    const { client: a, player } = await registered("198.51.100.10");
    const link = (await call("POST", "/v1/link", { salt: "c2FsdA==" }, { client: a, ip: "198.51.100.10" })).body.code;
    const b = await newClient();
    const r = await call(
      "POST",
      "/v1/register",
      { publicKey: b.publicKey, app: "0.9.30", link },
      { client: b, ip: "198.51.100.10" },
    );
    expect(r.body).toMatchObject({ player, salt: "c2FsdA==" });
  });
});

describe("uploading", () => {
  it("stores units once each and reports what it holds", async () => {
    const { client } = await registered();
    const r = await call("POST", "/v1/summaries", upload("aaaaaaaa"), { client });
    expect(r.body).toEqual({ accepted: 2, ignored: 0, unchanged: 0 });
    const me = await call("GET", "/v1/me", undefined, { client });
    expect(me.body.counts).toEqual({ reincarnation: 2 });
  });

  it("checks every row and refuses the upload when one is wrong", async () => {
    const { client } = await registered();
    const bad = upload("aaaaaaaa");
    bad.rows[1].kind = "spaceship";
    expect((await call("POST", "/v1/summaries", bad, { client })).body).toMatchObject({
      error: "bad_field",
      field: "rows[1].kind",
    });
    expect(
      (await call("POST", "/v1/summaries", { ...upload("aaaaaaaa"), character: "not hex!" }, { client })).body.field,
    ).toBe("character");
  });

  it("never overwrites another player's row", async () => {
    const { client: a } = await registered("198.51.100.20");
    const { client: b } = await registered("198.51.100.21");
    await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok", 2, "aaaaaaaaaaaaaaaa"), {
      client: a,
      ip: "198.51.100.20",
    });
    const r = await call("POST", "/v1/summaries", upload("bbbbbbbb", "oggok", 2, "aaaaaaaaaaaaaaaa"), {
      client: b,
      ip: "198.51.100.21",
    });
    expect(r.body).toEqual({ accepted: 0, ignored: 2, unchanged: 0 });
  });

  it("skips a unit it already holds unchanged, and doesn't count it against the daily limit", async () => {
    const { client } = await registered();
    const units = () => env.DB.prepare("SELECT units FROM usage").first<{ units: number }>();
    await call("POST", "/v1/summaries", upload("aaaaaaaa"), { client });
    expect((await units())!.units).toBe(2);

    // The same two again (a reinstall, a lost ledger): nothing written, nothing counted. A newer app alone isn't a change.
    const again = await call("POST", "/v1/summaries", { ...upload("aaaaaaaa"), app: "0.10.9" }, { client });
    expect(again.body).toEqual({ accepted: 0, ignored: 0, unchanged: 2 });
    expect((await units())!.units).toBe(2);

    // One of them changed: only that one goes in and counts.
    const one = upload("aaaaaaaa");
    one.rows[1].trio = "WIZ";
    expect((await call("POST", "/v1/summaries", one, { client })).body).toEqual({
      accepted: 1,
      ignored: 0,
      unchanged: 1,
    });
    expect((await units())!.units).toBe(3);
    expect(
      (await env.DB.prepare("SELECT trio FROM units WHERE id = ?").bind(one.rows[1].id).first<{ trio: string }>())!
        .trio,
    ).toBe("WIZ");
  });

  it("lets an unchanged re-send through at the daily limit, but not a new unit", async () => {
    const { client } = await registered();
    await call("POST", "/v1/summaries", upload("aaaaaaaa"), { client });
    await env.DB.prepare("UPDATE usage SET units = 3000").run();
    expect((await call("POST", "/v1/summaries", upload("aaaaaaaa"), { client })).body).toEqual({
      accepted: 0,
      ignored: 0,
      unchanged: 2,
    });
    const more = await call("POST", "/v1/summaries", upload("aaaaaaaa", "oggok", 3), { client });
    expect(more.status).toBe(429);
    expect(more.body.error).toBe("daily_limit");
  });

  it("deletes everything a player shared", async () => {
    const { client } = await registered();
    await call("POST", "/v1/summaries", upload("aaaaaaaa"), { client });
    const gone = await call("DELETE", "/v1/me", undefined, { client });
    expect(gone.body).toEqual({ deleted: 2, pcs: 1 });
    expect((await call("GET", "/v1/me", undefined, { client })).body.error).toBe("unknown_install");
  });
});
