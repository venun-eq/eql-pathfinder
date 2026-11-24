// eqlpathfinder.com: the website, as a Worker with static assets only (no code runs). Built by Vite (vite.config.ts) and
// deployed with `cf deploy`. www.eqlpathfinder.com redirects here (infra/cloudflare, a redirect rule that runs first).
import { defineConfig } from "cf/config";

export default defineConfig({
  worker: {
    name: "site",
    compatibilityDate: "2026-11-01",
    // /privacy serves privacy.html; an unknown address gets 404.html with a 404 status.
    assets: { htmlHandling: "auto-trailing-slash", notFoundHandling: "404-page" },
    domains: ["eqlpathfinder.com", "www.eqlpathfinder.com"],
    // Only the real address: no workers.dev copy and no per-version preview addresses.
    workersDev: false,
    previewUrls: false,
  },
});
