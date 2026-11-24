// Every request except the public ones is signed by the install's own ECDSA P-256 key:
//   X-EQLP-Install:   first 32 hex of SHA-256(public key, SPKI)
//   X-EQLP-Time:      unix seconds (within 5 minutes of the server's clock)
//   X-EQLP-Signature: base64 IEEE P1363 signature (r||s) over
//                     "EQLP1\n{METHOD}\n{path}\n{time}\n{install}\n{hex SHA-256 of the body bytes}"
// A signature proves which install sent a row, not that the row is true: the nightly job checks plausibility.
import type { Env, Who } from "./env.ts";
import { bad, HttpError } from "./http.ts";
import { b64d, codeHash, hex, nowS, sha256 } from "./util.ts";

const MAX_SKEW_S = 300;

/** The install that signed this request (its row once registered), or 401. `registeringKey`: the key a new install sends. */
export async function authenticate(
  request: Request,
  env: Env,
  method: string,
  path: string,
  body: Uint8Array,
  registeringKey?: string,
): Promise<Who> {
  const install = request.headers.get("x-eqlp-install") || "";
  const rawTime = request.headers.get("x-eqlp-time") || "";
  const time = /^\d+$/.test(rawTime) ? Number(rawTime) : NaN;
  const signature = request.headers.get("x-eqlp-signature") || "";
  if (!/^[0-9a-f]{32}$/.test(install) || !Number.isSafeInteger(time) || !signature)
    throw new HttpError(401, "unsigned", "This request isn't signed.");
  if (Math.abs(nowS() - time) > MAX_SKEW_S)
    throw new HttpError(
      401,
      "clock",
      "Your PC's clock is more than 5 minutes off. Set it to update automatically and try again.",
    );

  let row: Who | null = null;
  let spki = registeringKey;
  if (!spki) {
    row = await env.DB.prepare("SELECT id, player, public_key FROM installs WHERE id = ?").bind(install).first<Who>();
    if (!row) throw new HttpError(401, "unknown_install", "This PC isn't registered (or its data was deleted).");
    spki = row.public_key!;
  }
  let keyBytes: Uint8Array;
  try {
    keyBytes = b64d(spki);
  } catch {
    throw bad("publicKey");
  }
  if (hex(await sha256(keyBytes)).slice(0, 32) !== install)
    throw new HttpError(401, "bad_install", "The install ID doesn't match its key.");
  let key: CryptoKey;
  try {
    key = await crypto.subtle.importKey("spki", keyBytes, { name: "ECDSA", namedCurve: "P-256" }, false, ["verify"]);
  } catch {
    throw bad("publicKey");
  }
  const canonical = ["EQLP1", method, path, time, install, hex(await sha256(body))].join("\n");
  let sig: Uint8Array;
  try {
    sig = b64d(signature);
  } catch {
    throw new HttpError(401, "bad_signature", "The signature doesn't match.");
  }
  const ok = await crypto.subtle.verify(
    { name: "ECDSA", hash: "SHA-256" },
    key,
    sig,
    new TextEncoder().encode(canonical),
  );
  if (!ok) throw new HttpError(401, "bad_signature", "The signature doesn't match.");
  return row || { id: install };
}

/** Per-address limit in memory, never stored. `prefix` keeps each use's counter apart. */
export async function limitPerAddress(
  request: Request,
  env: Env,
  prefix: string,
  message: string,
  limiter = env.CODE_GUESSES,
): Promise<void> {
  if (!limiter) return;
  const ip = request.headers.get("cf-connecting-ip") || "unknown";
  const { success } = await limiter.limit({ key: prefix + ip });
  if (!success) throw new HttpError(429, "slow_down", message, { retryAfter: 60 });
}

/** Codes are 8 of 32 characters (about 1.1 trillion); guesses are also limited per address. */
export const limitCodeGuesses = (request: Request, env: Env) =>
  limitPerAddress(request, env, "", "Too many code attempts. Wait a minute and try again.");

/** General per-address ceiling (60/min) for sharing, registration and read routes. */
export const limitApi = (request: Request, env: Env, prefix: string) =>
  limitPerAddress(request, env, prefix, "Too many requests at once. Wait a minute and try again.", env.API_LIMIT);

export interface CodeRow {
  player: string;
  install: string;
  payload: string | null;
}

/** Uses up one use of a code of this kind, or 403. A link code is deleted once used. */
export async function takeCode(env: Env, kind: "link" | "enroll", raw: unknown): Promise<CodeRow> {
  const code = String(raw || "")
    .toUpperCase()
    .replace(/[^0-9A-Z]/g, "");
  if (code.length !== 8) throw new HttpError(403, "bad_code", "That code isn't right, or it has expired.");
  const row = await env.DB.prepare(
    "UPDATE codes SET uses_left = uses_left - 1 WHERE hash = ? AND kind = ? AND expires > ? AND uses_left > 0 RETURNING player, install, payload",
  )
    .bind(await codeHash(code), kind, nowS())
    .first<CodeRow>();
  if (!row) throw new HttpError(403, "bad_code", "That code isn't right, or it has expired.");
  if (kind === "link")
    await env.DB.prepare("DELETE FROM codes WHERE hash = ?")
      .bind(await codeHash(code))
      .run();
  return row;
}
