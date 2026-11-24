// What the Worker is given (cloudflare.config.ts).

export interface Env {
  DB: D1Database;
  /** Per-address counters in memory (code guesses, feedback, usage). Absent in a bare local run. */
  CODE_GUESSES?: RateLimit;
  /** Per-address counters in memory for sharing, registration and read endpoints (60/min). Absent in a bare local run. */
  API_LIMIT?: RateLimit;
  /** Makes the daily connection codes. A Worker secret: never in the database or the source. */
  IP_SECRET?: string;
  /** Screenshots attached to reports ("<day>/<feedback id>.<png|webp|jpg>"), kept 90 days. Absent in a bare local run. */
  SHOTS?: R2Bucket;
}

/** Who signed a request: the install, and its player once registered. */
export interface Who {
  id: string;
  player?: string;
  public_key?: string;
}
