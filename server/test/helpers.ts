// Calling the Worker as the app does: JSON bodies, an address per caller, and requests signed with an install's own key.
import { env, exports } from "cloudflare:workers";

export interface Reply {
  status: number;
  body: any;
}

/** A copy of the app: its own ECDSA P-256 key, and the install id the server derives from it. */
export interface Client {
  install: string;
  publicKey: string;
  sign(method: string, path: string, body: Uint8Array): Promise<Record<string, string>>;
}

const hex = (b: ArrayBuffer | Uint8Array) =>
  Array.from(new Uint8Array(b), x => x.toString(16).padStart(2, "0")).join("");
const b64 = (b: ArrayBuffer) => btoa(String.fromCharCode(...new Uint8Array(b)));

export async function newClient(): Promise<Client> {
  const keys = (await crypto.subtle.generateKey({ name: "ECDSA", namedCurve: "P-256" }, true, [
    "sign",
    "verify",
  ])) as CryptoKeyPair;
  const spki = (await crypto.subtle.exportKey("spki", keys.publicKey)) as ArrayBuffer;
  const install = hex(await crypto.subtle.digest("SHA-256", spki)).slice(0, 32);
  return {
    install,
    publicKey: b64(spki),
    async sign(method, path, body) {
      const time = Math.floor(Date.now() / 1000);
      const canonical = ["EQLP1", method, path, time, install, hex(await crypto.subtle.digest("SHA-256", body))].join(
        "\n",
      );
      const sig = await crypto.subtle.sign(
        { name: "ECDSA", hash: "SHA-256" },
        keys.privateKey,
        new TextEncoder().encode(canonical),
      );
      return { "x-eqlp-install": install, "x-eqlp-time": String(time), "x-eqlp-signature": b64(sig) };
    },
  };
}

/** One request to the Worker. `ip` stands for the caller's internet connection. */
export async function call(
  method: string,
  path: string,
  body?: unknown,
  opts: { client?: Client; ip?: string } = {},
): Promise<Reply> {
  const bytes = body === undefined ? new Uint8Array() : new TextEncoder().encode(JSON.stringify(body));
  const headers: Record<string, string> = {
    "content-type": "application/json",
    "cf-connecting-ip": opts.ip ?? "203.0.113.7",
  };
  if (opts.client) Object.assign(headers, await opts.client.sign(method, path.split("?")[0], bytes));
  const res = await exports.default.fetch(
    new Request("https://api.test" + path, { method, headers, body: method === "GET" ? undefined : bytes }),
  );
  return { status: res.status, body: await res.json() };
}

/** A registered copy (the server's enrollment switch must be off, or pass an invite code). */
export async function registered(ip = "203.0.113.7", enroll?: string): Promise<{ client: Client; player: string }> {
  const client = await newClient();
  const r = await call("POST", "/v1/register", { publicKey: client.publicKey, app: "0.9.30", enroll }, { client, ip });
  if (r.status !== 200) throw new Error("register: " + JSON.stringify(r));
  return { client, player: r.body.player };
}

/** Sets a row in the server's config table (the switches the operator flips). */
export async function setConfig(key: string, value: string): Promise<void> {
  await env.DB.prepare(
    "INSERT INTO config (key, value) VALUES (?, ?) ON CONFLICT (key) DO UPDATE SET value = excluded.value",
  )
    .bind(key, value)
    .run();
}

/** An upload of `n` reincarnation units for one character on one game server. */
export function upload(character: string, server = "oggok", n = 2, idPrefix = "0123456789abcdef") {
  return {
    schema: 3,
    app: "0.9.30",
    engine: 8,
    server,
    character,
    rows: Array.from({ length: n }, (_, i) => ({
      kind: "reincarnation",
      id: idPrefix + String(i).padStart(4, "0"),
      day: "2026-11-16",
      seq: i + 1,
      trio: "BER",
    })),
  };
}
