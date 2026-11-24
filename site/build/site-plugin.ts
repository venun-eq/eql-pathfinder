// The website's build step, as one Vite plugin. It only rewrites HTML at build time; nothing here reaches the browser.
//
//  <!-- head -->, <!-- header -->, <!-- footer -->   become partials/*.html. The header link named by the page's
//                                                   <body data-page="..."> gets aria-current="page".
//  <!-- changelog -->                               becomes What's new, from ../docs/releases/*.md (stable releases).
//  <img src="img/name.png" sizes="...">             becomes responsive WebP: the full width lossless (smallest for
//                                                   screenshots), narrower widths at quality 90, made with sharp into
//                                                   gen/ and reused while the PNG is unchanged.
import { existsSync, mkdirSync, readdirSync, readFileSync, statSync } from "node:fs";
import { join, resolve } from "node:path";
import sharp from "sharp";
import type { Plugin } from "vite";

/** Narrower copies offered to phones and small windows; a width close to the original isn't worth a file. */
const WIDTHS = [400, 640, 1024, 1400];

export function sitePlugin(): Plugin {
  let root = "";
  return {
    name: "eqlpathfinder-site",
    configResolved(config) {
      root = config.root;
    },
    transformIndexHtml: {
      // Before Vite's own pass, so the stylesheet and images added here are bundled and hashed like any other.
      order: "pre",
      async handler(html) {
        html = html
          .replace("<!-- head -->", partial(root, "head"))
          .replace("<!-- header -->", header(root, html))
          .replace("<!-- footer -->", partial(root, "footer"));
        if (html.includes("<!-- changelog -->"))
          html = html.replace("<!-- changelog -->", changelog(resolve(root, "../docs/releases")));
        return await responsiveImages(root, html);
      },
    },
  };
}

function partial(root: string, name: string): string {
  return readFileSync(join(root, "partials", `${name}.html`), "utf8").trim();
}

function header(root: string, html: string): string {
  const page = /<body[^>]*\bdata-page="([\w-]+)"/.exec(html)?.[1];
  return partial(root, "header").replace(/ data-nav="([\w-]+)"/g, (_, nav: string) =>
    nav === page ? ` aria-current="page"` : "",
  );
}

// ---------------------------------------------------------------------------------------------- What's new

interface Release {
  version: string;
  date: string;
  channel: string;
  body: string;
}

const escapeHtml = (s: string) =>
  s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");

/** A release note's front matter (version, date, channel) and its text. */
export function parseRelease(text: string, file: string): Release {
  const m = /^---\r?\n([\s\S]*?)\r?\n---\r?\n([\s\S]*)$/.exec(text);
  if (!m) throw new Error(`${file}: no front matter`);
  const meta = Object.fromEntries(
    m[1].split(/\r?\n/).map(line => {
      const i = line.indexOf(":");
      return [line.slice(0, i).trim(), line.slice(i + 1).trim()];
    }),
  );
  if (!meta.version || !/^\d{4}-\d{2}-\d{2}$/.test(meta.date ?? ""))
    throw new Error(`${file}: needs version and date (yyyy-mm-dd)`);
  return { version: meta.version, date: meta.date, channel: meta.channel ?? "stable", body: m[2].trim() };
}

/** The notes' small Markdown: paragraphs, and lists of "- " lines. */
export function noteHtml(md: string): string {
  const out: string[] = [];
  let para: string[] = [];
  let list: string[] = [];
  const flush = () => {
    if (para.length) out.push(`<p>${escapeHtml(para.join(" "))}</p>`);
    if (list.length) out.push(`<ul>${list.map(x => `<li>${escapeHtml(x)}</li>`).join("")}</ul>`);
    para = [];
    list = [];
  };
  for (const raw of md.split(/\r?\n/)) {
    const line = raw.trim();
    const item = /^- (.+)/.exec(line);
    if (item) {
      if (para.length) flush();
      list.push(item[1]);
    } else if (!line) flush();
    else {
      if (list.length) flush();
      para.push(line);
    }
  }
  flush();
  return out.join("");
}

const compareVersions = (a: string, b: string) => {
  const pa = a.split(".").map(Number),
    pb = b.split(".").map(Number);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++)
    if ((pa[i] ?? 0) !== (pb[i] ?? 0)) return (pa[i] ?? 0) - (pb[i] ?? 0);
  return 0;
};

function changelog(dir: string): string {
  const releases = readdirSync(dir)
    .filter(f => f.endsWith(".md"))
    .map(f => parseRelease(readFileSync(join(dir, f), "utf8"), f))
    .filter(r => r.channel === "stable")
    .sort((a, b) => compareVersions(b.version, a.version));
  if (!releases.length) throw new Error(`No stable release notes in ${dir}`);
  const day = new Intl.DateTimeFormat("en-US", { month: "long", day: "numeric", year: "numeric", timeZone: "UTC" });
  const byDay = new Map<string, Release[]>();
  for (const r of releases) byDay.set(r.date, [...(byDay.get(r.date) ?? []), r]);
  const days = [...byDay].map(
    ([date, rs]) =>
      `<section class="day"><h2>${day.format(new Date(`${date}T00:00:00Z`))}</h2>\n` +
      rs
        .map(
          r =>
            `<article class="release" id="v${escapeHtml(r.version)}"><h3>${escapeHtml(r.version)}</h3>${noteHtml(r.body)}</article>`,
        )
        .join("\n") +
      "\n</section>",
  );
  return (
    `<p class="updated">Latest version: ${escapeHtml(releases[0].version)}. Pathfinder updates itself; this is what each version brought.</p>\n` +
    days.join("\n") +
    `\n<p class="earlier">Before ${escapeHtml(releases.at(-1)!.version)}, Pathfinder was a private test.</p>`
  );
}

// ---------------------------------------------------------------------------------------------- Screenshots

async function responsiveImages(root: string, html: string): Promise<string> {
  const tags = [...html.matchAll(/<img\b[^>]*\bsrc="img\/([\w-]+)\.png"[^>]*>/g)];
  for (const [tag, name] of tags) {
    const source = join(root, "img", `${name}.png`);
    const { width } = await sharp(source).metadata();
    if (!width) throw new Error(`${source}: no width`);
    const full = await webp(root, source, name, width, true);
    const fullBytes = statSync(join(root, full)).size;
    // Lossless at full width can beat lossy at a narrower one (flat UI colours): offer a narrower copy only when it saves.
    const narrower = await Promise.all(
      WIDTHS.filter(w => w < width * 0.85).map(async w => {
        const file = await webp(root, source, name, w, false);
        return statSync(join(root, file)).size < fullBytes * 0.8 ? `${file} ${w}w` : null;
      }),
    );
    const srcset = [...narrower.filter(x => x !== null), `${full} ${width}w`].join(", ");
    html = html.replace(tag, tag.replace(`src="img/${name}.png"`, `src="${full}" srcset="${srcset}"`));
  }
  return html;
}

/** gen/name-width.webp, made again only when the PNG is newer. */
async function webp(root: string, source: string, name: string, width: number, full: boolean): Promise<string> {
  const rel = `gen/${name}-${width}.webp`;
  const out = join(root, rel);
  if (!existsSync(out) || statSync(out).mtimeMs < statSync(source).mtimeMs) {
    mkdirSync(join(root, "gen"), { recursive: true });
    const image = full
      ? sharp(source).webp({ lossless: true, effort: 6 })
      : sharp(source).resize({ width }).webp({ quality: 90, effort: 6 });
    await image.toFile(out);
  }
  return rel;
}
