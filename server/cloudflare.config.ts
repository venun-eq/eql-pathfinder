// EQL Pathfinder community API: the Worker's whole configuration (deploys with `cf deploy`, tests with Vitest).
//
//   cf deploy                    production: Worker "api" on the live database (api.eqlpathfinder.com only)
//   cf deploy --mode staging     staging: Worker "api-staging" on its own database (npm run staging:sleep turns its workers.dev off after)
//   vitest (mode "test")         a local database only: tests never reach a real one
//
// Database changes are migrations in ./migrations, applied with `npm run migrate:staging` and `npm run migrate`.
// Each database's ID comes from the environment (EQLP_D1_PRODUCTION, EQLP_D1_STAGING: database-ids.ts); none is here.
import { bindings, defineConfig, triggers } from "cf/config";
import { databaseId, workerTarget } from "./database-ids.ts";
import * as entrypoint from "./src/index.ts" with { type: "cf-worker" };

// This file runs in Node (the cf CLI and Vitest evaluate it), and the Worker's own types have no `process`.
declare const process: { env: Record<string, string | undefined> };

export default defineConfig(({ mode }) => {
  const { name, staging, test, workersDev, domains } = workerTarget(mode);
  return {
    worker: {
      name,
      compatibilityDate: "2026-11-01",
      entrypoint,
      // Production answers only on api.eqlpathfinder.com (behind the zone's edge rate limit). Staging uses its
      // workers.dev address during a smoke run, and `npm run staging:sleep` disables it again when idle.
      workersDev,
      domains,
      // No per-version preview addresses: they would be extra public doors to the same database.
      previewUrls: false,
      // Only the Worker's own console output (errors) is logged. Cloudflare's per-request logs carry request metadata that
      // can include the caller's address, and the server promises to keep no addresses; query strings are redacted too.
      observability: {
        enabled: true,
        headSamplingRate: 1,
        redactQueryString: true,
        logs: { enabled: true, invocationLogs: false },
      },
      env: {
        // Tests get a local database with no id, so nothing can point them at a real one.
        DB: test
          ? bindings.d1({ name: "eqlpathfinder-test" })
          : bindings.d1({
              id: databaseId(mode ?? "production", process.env),
              name: staging ? "eqlpathfinder-staging" : "eqlpathfinder",
            }),
        // Code guesses, feedback and usage per address, in memory only (each environment has its own counters).
        CODE_GUESSES: bindings.rateLimit({ namespace: staging ? "1002" : "1001", simple: { limit: 10, period: 60 } }),
        // Sharing, registration and read endpoints per address (60/min lets a first-time 30-batch history sync finish at once).
        API_LIMIT: bindings.rateLimit({ namespace: staging ? "1004" : "1003", simple: { limit: 60, period: 60 } }),
        // Makes the daily connection codes (one character per server, new players per connection); never stored anywhere.
        IP_SECRET: bindings.secret(),
        // Screenshots players attach to a report (Report a problem), deleted after 90 days by the daily cleanup. Private:
        // no public address. Each environment has its own bucket; tests get a local one.
        SHOTS: bindings.r2({
          name: test
            ? "eqlpathfinder-feedback-test"
            : staging
              ? "eqlpathfinder-feedback-staging"
              : "eqlpathfinder-feedback",
        }),
      },
      // Daily pruning: raw rows after a year, codes when expired, counters and connection codes after days.
      triggers: [triggers.scheduled({ schedule: "17 9 * * *" })],
    },
  };
});
