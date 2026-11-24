// Answers, errors and the checks every field goes through. Nothing a request sends reaches SQL except through .bind().

export class HttpError extends Error {
  constructor(
    public status: number,
    public code: string,
    message?: string,
    public extra: Record<string, unknown> = {},
  ) {
    super(message || code);
  }
}

export function json(obj: unknown, status = 200, extraHeaders?: Record<string, string>): Response {
  return new Response(JSON.stringify(obj), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
      "x-content-type-options": "nosniff",
      "referrer-policy": "no-referrer",
      ...extraHeaders,
    },
  });
}

/** A JSON object from the request body, or 400. */
export function parseJson(body: Uint8Array): Record<string, unknown> {
  try {
    const v: unknown = JSON.parse(new TextDecoder().decode(body));
    if (v && typeof v === "object" && !Array.isArray(v)) return v as Record<string, unknown>;
  } catch {
    /* below */
  }
  throw new HttpError(400, "bad_json", "The request body isn't a JSON object.");
}

export function bad(field: string): HttpError {
  return new HttpError(400, "bad_field", "The field " + field + " isn't valid.", { field });
}

/** A required string of 1..max characters, or 400. */
export function str(v: unknown, field: string, max: number): string {
  if (typeof v !== "string" || v.length === 0 || v.length > max) throw bad(field);
  return v;
}

/** An optional string, cut to max characters; null when missing or empty. */
export function optStr(v: unknown, max: number): string | null {
  return typeof v === "string" && v.length > 0 ? v.slice(0, max) : null;
}

/** A required whole number in [min, max], or 400. */
export function int(v: unknown, field: string, min: number, max: number): number {
  if (!Number.isInteger(v) || (v as number) < min || (v as number) > max) throw bad(field);
  return v as number;
}

/** An optional whole number in [min, max]; null otherwise. */
export function optInt(v: unknown, min: number, max: number): number | null {
  return Number.isInteger(v) && (v as number) >= min && (v as number) <= max ? (v as number) : null;
}

/** A 32-hex anonymous id (roadmap voter, usage id). */
export const HEX32 = /^[0-9a-f]{32}$/;
