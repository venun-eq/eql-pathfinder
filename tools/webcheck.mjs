// The window's page scripts (src/EqlPathfinder.App/web/js), loaded in order with a stand-in for the browser, then sent
// a data message and then each page's own answer, the way the window sends them. Fails on any name the scripts use that
// nothing declares (a ReferenceError): 0.9.37 shipped core.js setting `planLocal` after plan.js was gone, so every data
// message threw before the page drew.
// Usage: node tools/webcheck.mjs [web folder]   (the folder is for tools/check.ps1's samples: a copy with a slip in it)
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const web = process.argv[2] ?? join(dirname(fileURLToPath(import.meta.url)), "..", "src", "EqlPathfinder.App", "web");
const html = readFileSync(join(web, "index.html"), "utf8");
const scripts = [...html.matchAll(/<script src="(js\/[^"]+)"><\/script>/g)].map(m => m[1]);
if (!scripts.length) throw new Error("no page scripts found in index.html");

// Anything the DOM gives back: every property and call returns the stand-in again, and it iterates as empty.
// biome-ignore lint/complexity/useArrowFunction: Proxy construct trap requires a constructible function target
const stub = new Proxy(function () {}, {
  get: (_, k) =>
    k === Symbol.iterator
      ? function* () {}
      : k === Symbol.toPrimitive
        ? () => ""
        : k === "length"
          ? 0
          : k === "then"
            ? undefined
            : stub,
  apply: () => stub,
  construct: () => stub,
  set: () => true,
});
const listeners = [];
const store = new Map();
const sandbox = {
  console,
  document: stub,
  location: stub,
  navigator: stub,
  history: stub,
  performance: stub,
  fetch: () => stub,
  localStorage: {
    getItem: k => store.get(k) ?? null,
    setItem: (k, v) => store.set(k, String(v)),
    removeItem: k => store.delete(k),
  },
  setTimeout: () => 0,
  clearTimeout: () => {},
  setInterval: () => 0,
  clearInterval: () => {},
  requestAnimationFrame: () => 0,
  addEventListener: () => {},
  removeEventListener: () => {},
  matchMedia: () => stub,
  getComputedStyle: () => stub,
  CSS: { escape: s => String(s) },
  innerWidth: 1280,
  innerHeight: 800,
  devicePixelRatio: 1,
  ResizeObserver: stub,
  MutationObserver: stub,
  IntersectionObserver: stub,
  Image: stub,
  Audio: stub,
  AudioContext: stub,
  Event: stub,
  CustomEvent: stub,
  chrome: {
    webview: {
      addEventListener: (type, fn) => {
        if (type === "message") listeners.push(fn);
      },
      postMessage: () => {},
    },
  },
};
sandbox.window = sandbox;
const ctx = vm.createContext(sandbox);

const failures = [];
const isUndeclared = e => e?.name === "ReferenceError";
const where = e => (e.stack ?? "").split("\n").slice(1, 3).join("\n");
// Every answer the scripts test for (`e.data?.kind === "loot"`), read from their text: a list kept here by hand let a
// new answer through unsent. tools/check.ps1's sample fails if this stops finding core.js's.
const kinds = new Set();
for (const s of scripts) {
  const text = readFileSync(join(web, s), "utf8");
  for (const m of text.matchAll(/\bdata\??\.kind\s*[!=]==?\s*(["'])([^"']+)\1/g)) kinds.add(m[2]);
  try {
    vm.runInContext(text, ctx, { filename: s });
  } catch (e) {
    if (isUndeclared(e)) failures.push(`${s} while loading: ${e.message}`);
    // A script that doesn't parse never runs in the app, and nothing else here notices unless another file uses its names.
    else if (e?.name === "SyntaxError") failures.push(`${s} doesn't parse: ${e.message}`);
  }
}
if (!listeners.length) failures.push("no script listens for the window's messages");
// A data message with nothing in it: the handlers may trip over missing fields (fine here), never over a missing name.
for (const fn of listeners) {
  try {
    fn({ data: {} });
  } catch (e) {
    if (isUndeclared(e)) failures.push(`data message: ${e.message}\n${where(e)}`);
  }
}
// Each page's own answer (it has a kind), sent empty after the data, then one no script knows. For six of them core.js's
// handler writes names that other files declare (docs/code-map.md): rename one and miss core.js, and only that answer
// throws. The page that asked may trip over the empty answer (fine here), never over a missing name.
// And no answer may replace the page data: from 0.9.9 to 0.9.37 core.js took any message whose kind it didn't know for
// the page data, Help's `roadmap` and `feedbackSent` among them, so opening Help could leave other pages without theirs.
const pageData = vm.runInContext("data", ctx);
answers: for (const kind of [...kinds, "some-future-answer"])
  for (const fn of listeners) {
    try {
      fn({ data: { kind } });
    } catch (e) {
      if (isUndeclared(e)) failures.push(`a "${kind}" answer: ${e.message}\n${where(e)}`);
    }
    if (vm.runInContext("data", ctx) !== pageData) {
      failures.push(`a "${kind}" answer replaced the page data`);
      break answers;
    }
  }
if (failures.length) {
  console.error("webcheck failed:\n" + failures.join("\n"));
  process.exit(1);
}
console.log(
  `webcheck: ${scripts.length} scripts load, take a data message and ${kinds.size} kinds of answer, and keep the page data through them`,
);
