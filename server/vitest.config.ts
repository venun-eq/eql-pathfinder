// Tests run inside workerd (Cloudflare's runtime) against a local database built from ./migrations: never a real one
// (cloudflare.config.ts gives mode "test" a database with no id, and remote bindings are off).

import { cloudflareTest, readD1Migrations } from "@cloudflare/vitest-plugin";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [
    cloudflareTest(async () => ({
      config: true,
      remoteBindings: false,
      miniflare: {
        bindings: {
          TEST_MIGRATIONS: await readD1Migrations("./migrations"),
          IP_SECRET: "test-secret-not-a-real-one",
        },
      },
    })),
  ],
  test: {
    setupFiles: ["./test/apply-migrations.ts"],
  },
});
