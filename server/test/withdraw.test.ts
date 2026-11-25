// share-without-derived (docs/specs/share-without-derived.md): a PC withdraws the units it sent and no longer builds
// (a hunt whose id moved, a visit that stopped being shared), one id at a time, and never anyone else's.
import { describe, expect, it } from "vitest";
import { call, newClient, registered, upload } from "./helpers.ts";

const WITHDRAW = "/v1/units/withdraw";

describe("withdrawing units", () => {
  it("drops the units this PC names and keeps the rest", async () => {
    const { client } = await registered("198.51.100.20");
    const up = upload("aaaaaaaa", "oggok", 3, "1111111111111111");
    expect((await call("POST", "/v1/summaries", up, { client, ip: "198.51.100.20" })).body.accepted).toBe(3);

    const r = await call(
      "POST",
      WITHDRAW,
      { character: "aaaaaaaa", ids: [up.rows[1].id] },
      { client, ip: "198.51.100.20" },
    );
    expect(r.status).toBe(200);
    expect(r.body.withdrawn).toBe(1);
    const me = await call("GET", "/v1/me", undefined, { client, ip: "198.51.100.20" });
    expect(me.body.counts).toEqual({ reincarnation: 2 });
  });

  it("an id it doesn't hold is no error, and a second withdrawal of the same id withdraws nothing", async () => {
    const { client } = await registered("198.51.100.21");
    const up = upload("bbbbbbbb", "oggok", 2, "2222222222222222");
    await call("POST", "/v1/summaries", up, { client, ip: "198.51.100.21" });
    const body = { character: "bbbbbbbb", ids: [up.rows[0].id, "2222222222229999"] };
    expect((await call("POST", WITHDRAW, body, { client, ip: "198.51.100.21" })).body.withdrawn).toBe(1);
    const again = await call("POST", WITHDRAW, body, { client, ip: "198.51.100.21" });
    expect(again.status).toBe(200);
    expect(again.body.withdrawn).toBe(0);
  });

  it("never withdraws another player's unit, nor one its linked PC sent", async () => {
    const { client: a } = await registered("198.51.100.22");
    const up = upload("cccccccc", "oggok", 2, "3333333333333333");
    await call("POST", "/v1/summaries", up, { client: a, ip: "198.51.100.22" });
    const ids = up.rows.map(r => r.id);

    // Another player names A's ids.
    const { client: b } = await registered("198.51.100.23");
    const other = await call("POST", WITHDRAW, { character: "cccccccc", ids }, { client: b, ip: "198.51.100.23" });
    expect(other.status).toBe(200);
    expect(other.body.withdrawn).toBe(0);

    // A PC linked to A's player names them: they're the other PC's to withdraw, which still builds them.
    const code = (await call("POST", "/v1/link", { salt: "c2FsdA==" }, { client: a, ip: "198.51.100.22" })).body.code;
    const c = await newClient();
    const joined = await call(
      "POST",
      "/v1/register",
      { publicKey: c.publicKey, app: "0.10.35", link: code },
      { client: c, ip: "198.51.100.24" },
    );
    expect(joined.status).toBe(200);
    const linked = await call("POST", WITHDRAW, { character: "cccccccc", ids }, { client: c, ip: "198.51.100.24" });
    expect(linked.status).toBe(200);
    expect(linked.body.withdrawn).toBe(0);

    const me = await call("GET", "/v1/me", undefined, { client: a, ip: "198.51.100.22" });
    expect(me.body.counts).toEqual({ reincarnation: 2 });
  });

  it("refuses an unsigned call, a malformed id and more ids than an upload holds", async () => {
    const { client } = await registered("198.51.100.25");
    expect((await call("POST", WITHDRAW, { character: "dddddddd", ids: ["4444444444444444"] })).status).toBe(401);
    const bad = await call(
      "POST",
      WITHDRAW,
      { character: "dddddddd", ids: ["not hex"] },
      { client, ip: "198.51.100.25" },
    );
    expect(bad.status).toBe(400);
    const many = Array.from({ length: 201 }, (_, i) => "4444444444" + String(i).padStart(6, "0"));
    const tooMany = await call("POST", WITHDRAW, { character: "dddddddd", ids: many }, { client, ip: "198.51.100.25" });
    expect(tooMany.status).toBe(413);
  });
});
