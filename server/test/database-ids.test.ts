// Deploys and migrations read their database from the environment (docs/specs/private-settings.md): the repository
// names none, and a deploy that wasn't told stops before it reaches Cloudflare.
import { describe, expect, it } from "vitest";
import { databaseId, workerTarget } from "../database-ids.ts";

describe("which database a deploy talks to", () => {
  const env = { EQLP_D1_PRODUCTION: "made-up-production", EQLP_D1_STAGING: "made-up-staging" };

  it("production and staging each read their own variable, and tests get no real database whatever is set", () => {
    expect(databaseId("production", env)).toBe("made-up-production");
    expect(databaseId("staging", env)).toBe("made-up-staging");
    expect(databaseId("staging-sleep", env)).toBe("made-up-staging");
    expect(databaseId("test", env)).toBeUndefined();
    expect(databaseId("test", {})).toBeUndefined();
  });

  it("a deploy whose variable is unset or blank stops and names it, and never falls back to the other database", () => {
    expect(() => databaseId("production", { EQLP_D1_STAGING: "made-up-staging" })).toThrow(/EQLP_D1_PRODUCTION/);
    expect(() => databaseId("staging", { EQLP_D1_PRODUCTION: "made-up-production", EQLP_D1_STAGING: " " })).toThrow(
      /EQLP_D1_STAGING/,
    );
    expect(() => databaseId("staging-sleep", { EQLP_D1_PRODUCTION: "made-up-production" })).toThrow(/EQLP_D1_STAGING/);
  });

  it("turns workers.dev off in production and staging-sleep, and keeps it on only during active staging", () => {
    expect(workerTarget("production")).toMatchObject({
      name: "api",
      staging: false,
      workersDev: false,
      domains: ["api.eqlpathfinder.com"],
    });
    expect(workerTarget(undefined)).toMatchObject({
      name: "api",
      staging: false,
      workersDev: false,
      domains: ["api.eqlpathfinder.com"],
    });
    expect(workerTarget("staging")).toMatchObject({
      name: "api-staging",
      staging: true,
      workersDev: true,
      domains: [],
    });
    expect(workerTarget("staging-sleep")).toMatchObject({
      name: "api-staging",
      staging: true,
      workersDev: false,
      domains: [],
    });
    expect(workerTarget("test")).toMatchObject({ name: "api", test: true, workersDev: false, domains: [] });
  });
});
