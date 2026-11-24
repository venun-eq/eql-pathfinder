// Which D1 database a deploy or a migration talks to. The IDs come from the environment (this PC's private settings,
// docs/specs/private-settings.md), never from the repository. cloudflare.config.ts and migrate.mjs both ask here, so
// neither can reach a database it wasn't told about. Outside src/, so it's never part of the Worker.

/** Mode "test" has no real database, whatever is set. "staging" and "staging-sleep" read EQLP_D1_STAGING; anything else
 *  is production and reads EQLP_D1_PRODUCTION. Unset or blank throws, naming the variable: never the other database instead. */
export function databaseId(mode: string, env: Record<string, string | undefined>): string | undefined {
  if (mode === "test") return undefined;
  const staging = mode === "staging" || mode === "staging-sleep";
  const name = staging ? "EQLP_D1_STAGING" : "EQLP_D1_PRODUCTION";
  const id = env[name]?.trim();
  if (!id) {
    throw new Error(
      `${name} isn't set, so there's no database to talk to. On the owner's PC run tools/private.ps1 first ` +
        `(in the same PowerShell); anywhere else set ${name} to the D1 database's ID.`,
    );
  }
  return id;
}

/** Routes and flags per cf mode: production serves only api.eqlpathfinder.com; staging's workers.dev sleeps when idle. */
export function workerTarget(mode: string | undefined): {
  name: string;
  staging: boolean;
  test: boolean;
  workersDev: boolean;
  domains: string[];
} {
  const staging = mode === "staging" || mode === "staging-sleep";
  const sleep = mode === "staging-sleep";
  const test = mode === "test";
  return {
    name: staging ? "api-staging" : "api",
    staging,
    test,
    workersDev: staging && !sleep,
    domains: staging || test ? [] : ["api.eqlpathfinder.com"],
  };
}
