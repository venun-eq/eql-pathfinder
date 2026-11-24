// Time, codes and hashing.

export const nowS = (): number => Math.floor(Date.now() / 1000);

/** Today's UTC day, yyyy-mm-dd. */
export const today = (): string => new Date().toISOString().slice(0, 10);

/** True if `day` is a real yyyy-mm-dd calendar date (rejects e.g. "2026-02-31" or "9999-99-99"). */
export function isValidDay(day: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) return false;
  const d = new Date(day + "T00:00:00Z");
  return Number.isFinite(d.getTime()) && d.toISOString().slice(0, 10) === day;
}

export function secondsToUtcMidnight(): number {
  const d = new Date();
  return Math.max(
    60,
    Math.ceil((Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate() + 1) - d.getTime()) / 1000),
  );
}

/** a < b as app versions ("0.9.10" < "0.9.28"; a pre-release tag doesn't count). */
export function older(a: string, b: string): boolean {
  const pa = String(a)
    .split(/[.-]/)
    .map(n => parseInt(n, 10) || 0);
  const pb = String(b)
    .split(/[.-]/)
    .map(n => parseInt(n, 10) || 0);
  for (let i = 0; i < 3; i++) if ((pa[i] || 0) !== (pb[i] || 0)) return (pa[i] || 0) < (pb[i] || 0);
  return false;
}

const CODE_CHARS = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // 32: no 0/O/1/I

/** An 8-character one-time code. */
export function newCode(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(8));
  return Array.from(bytes, b => CODE_CHARS[b & 31]).join("");
}

export const pretty = (code: string): string => code.slice(0, 4) + "-" + code.slice(4);

/** Only a hash of a code is kept. */
export async function codeHash(code: string): Promise<string> {
  return hex(await sha256(new TextEncoder().encode("eqlp-code:" + code)));
}

export const randomHex = (n: number): string => hex(crypto.getRandomValues(new Uint8Array(n)));

export async function sha256(bytes: BufferSource): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.digest("SHA-256", bytes));
}

export const hex = (bytes: Uint8Array): string => Array.from(bytes, b => b.toString(16).padStart(2, "0")).join("");

export function b64d(s: string): Uint8Array {
  const bin = atob(s);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}
