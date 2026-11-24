// Every test starts from the database ./migrations builds (the same files production and staging are migrated with),
// with the operator's switches at their defaults and no rows: tests can't leak into each other.
import { applyD1Migrations } from "cloudflare:test";
import { env } from "cloudflare:workers";
import { beforeEach } from "vitest";

declare global {
  namespace Cloudflare {
    interface Env {
      TEST_MIGRATIONS: import("cloudflare:test").D1Migration[];
    }
  }
}

await applyD1Migrations(env.DB, env.TEST_MIGRATIONS);

const ROW_TABLES = [
  "players",
  "installs",
  "codes",
  "units",
  "usage",
  "app_usage",
  "register_ips",
  "upload_ips",
  "shot_ips",
  "feedback_ips",
  "feedback",
  "feedback_extra",
  "votes",
];

beforeEach(async () => {
  await env.DB.batch([
    ...ROW_TABLES.map(t => env.DB.prepare("DELETE FROM " + t)),
    env.DB.prepare("DELETE FROM config WHERE key NOT IN ('require_enrollment', 'min_app', 'community_url')"),
    env.DB.prepare("UPDATE config SET value = '0' WHERE key = 'require_enrollment'"),
    env.DB.prepare("UPDATE config SET value = '0.8.0' WHERE key = 'min_app'"),
  ]);
});
