// How `cf build` / `cf deploy` build the site (they read cloudflare.config.ts). Every page is an entry; build/site-plugin.ts
// fills in the shared partials, What's new and the responsive screenshots, then Vite names the stylesheet, font and
// images by their content (cached for good, public/_headers). No scripts are produced.

import { cloudflare } from "@cloudflare/vite-plugin";
import { defineConfig } from "vite";
import { sitePlugin } from "./build/site-plugin.ts";

export default defineConfig({
  plugins: [sitePlugin(), cloudflare()],
  build: {
    // Nothing inlined as data: URLs, which the Content-Security-Policy in public/_headers doesn't allow.
    assetsInlineLimit: 0,
    rollupOptions: {
      input: {
        index: "index.html",
        privacy: "privacy.html",
        changelog: "changelog.html",
        notFound: "404.html",
      },
    },
  },
});
