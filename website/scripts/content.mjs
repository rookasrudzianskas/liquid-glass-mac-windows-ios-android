#!/usr/bin/env node
// Build-time content for the website. Everything the pages render from the
// repository is generated here, so missing or half-written inputs never break
// the build: docs (Markdown -> highlighted HTML, table of contents, search
// index, llms.txt), agent skills, platform code snippets, screenshots,
// wallpapers (AVIF/WebP/JPEG in responsive sizes), fonts, icons and the
// downloads manifest. Outputs are gitignored and cached by content hash.

import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { existsSync, realpathSync, statSync } from "node:fs";
import { copyFile, mkdir, readFile, readdir, rm, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Marked } from "marked";
import { createCssVariablesTheme, createHighlighter } from "shiki";
import YAML from "yaml";

const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repo = path.resolve(site, "..");
const quiet = process.argv.includes("--quiet");
const log = (...args) => !quiet && console.log("[content]", ...args);

export const SITE_URL = (process.env.LG_SITE_URL || "https://liquid-glass-engineering.vercel.app").replace(/\/$/, "");
export const REPO_URL = "https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android";

const gen = path.join(site, "src/generated");
const pub = path.join(site, "public");
const cacheDir = path.join(site, "node_modules/.cache/lg-content");

const sha = (data) => createHash("sha256").update(data).digest("hex");
const exists = (p) => existsSync(p);
async function writeIfChanged(file, data) {
  await mkdir(path.dirname(file), { recursive: true });
  const buf = typeof data === "string" ? Buffer.from(data) : data;
  if (exists(file)) {
    const old = await readFile(file);
    if (old.equals(buf)) return false;
  }
  await writeFile(file, buf);
  return true;
}
const json = (v) => `${JSON.stringify(v, null, 2)}\n`;
const toPosix = (p) => p.split(path.sep).join("/");
const escapeHtml = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
const stripTags = (html) =>
  html
    .replace(/<[^>]+>/g, "")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    // Numeric references (the highlighter writes `<` as &#x3C;); before &amp; so "&amp;#60;" stays literal.
    .replace(/&#x([0-9a-f]+);/gi, (_m, hex) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/&#(\d+);/g, (_m, dec) => String.fromCodePoint(Number(dec)))
    .replace(/&amp;/g, "&");

const git = (args) => execFileSync("git", args, { cwd: repo, stdio: ["ignore", "pipe", "ignore"] }).toString();

/** True when the repository folder is the top of its own git checkout (not a copy inside another one). */
const inGitCheckout = (() => {
  let value;
  return () => {
    if (value === undefined) {
      try {
        value = realpathSync(git(["rev-parse", "--show-toplevel"]).trim()) === realpathSync(repo);
      } catch {
        value = false;
      }
    }
    return value;
  };
})();

/** Last commit date of a file, as a UTC ISO timestamp (the pages and the sitemap both show the UTC day). */
function gitDate(file) {
  if (!inGitCheckout()) return null;
  try {
    const out = git(["log", "-1", "--format=%cI", "--", file]).trim();
    const date = out ? new Date(out) : null;
    return date && !Number.isNaN(date.getTime()) ? date.toISOString() : null;
  } catch {
    return null;
  }
}

// ---------------------------------------------------------------------------
// Syntax highlighting (shared by docs and home page snippets)

const LANGS = ["swift", "kotlin", "xml", "csharp", "shellscript", "json", "javascript", "typescript", "glsl", "hlsl", "groovy", "toml", "yaml", "diff", "powershell", "ini", "markdown", "wgsl", "cpp", "html", "css", "jsonc", "properties"];
const ALIASES = {
  sh: "shellscript", bash: "shellscript", zsh: "shellscript", shell: "shellscript", console: "shellscript", terminal: "shellscript",
  kts: "kotlin", kt: "kotlin", gradle: "groovy", xaml: "xml", cs: "csharp", "c#": "csharp", ps1: "powershell", pwsh: "powershell",
  js: "javascript", mjs: "javascript", ts: "typescript", metal: "cpp", agsl: "glsl", frag: "glsl", vert: "glsl", yml: "yaml", md: "markdown",
  plist: "xml", svg: "xml", csproj: "xml", props: "xml", swiftui: "swift",
};
const LANG_LABELS = { shellscript: "Shell", swift: "Swift", kotlin: "Kotlin", xml: "XML", csharp: "C#", groovy: "Gradle", glsl: "GLSL", hlsl: "HLSL", cpp: "C++", json: "JSON", powershell: "PowerShell", javascript: "JavaScript", typescript: "TypeScript", wgsl: "WGSL", toml: "TOML", yaml: "YAML", diff: "Diff", ini: "INI", markdown: "Markdown", html: "HTML", css: "CSS", jsonc: "JSON", properties: "Properties" };

let highlighterPromise;
function highlighter() {
  highlighterPromise ??= createHighlighter({
    themes: [createCssVariablesTheme({ name: "css-variables", variablePrefix: "--shiki-", variableDefaults: {}, fontStyle: true })],
    langs: LANGS,
  });
  return highlighterPromise;
}

function resolveLang(raw) {
  const lang = (raw || "").trim().toLowerCase().split(/[\s{]/)[0] ?? "";
  if (!lang) return null;
  if (LANGS.includes(lang)) return lang;
  return ALIASES[lang] ?? null;
}

export async function highlight(code, rawLang) {
  const hl = await highlighter();
  const lang = resolveLang(rawLang);
  const label = rawLang === "xaml" ? "XAML" : rawLang === "kts" ? "Kotlin" : rawLang === "gradle" ? "Gradle" : lang ? LANG_LABELS[lang] ?? lang : (rawLang || "Text");
  const html = lang
    ? hl.codeToHtml(code, { lang, theme: "css-variables" })
    : `<pre class="shiki css-variables" tabindex="0"><code>${code
        .split("\n")
        .map((l) => `<span class="line">${escapeHtml(l)}</span>`)
        .join("\n")}</code></pre>`;
  return { html, label, lang: lang ?? "text" };
}

function codeBlock({ html, label, lang }, title) {
  return `<div class="code" data-lang="${escapeHtml(lang)}"><div class="code__bar"><span class="code__lang">${escapeHtml(title || label)}</span><button type="button" class="code__copy" data-copy aria-label="Copy code">Copy</button></div>${html.replace(' tabindex="0"', ' tabindex="0" aria-label="Code sample"')}</div>`;
}

// ---------------------------------------------------------------------------
// Docs

const GROUPS = ["Get started", "Platforms", "Reference", "Guides"];
/** Alt text of every repository image the docs show, by repo-relative path. */
const imageAlts = new Map();
// `summary` is the page's meta description and docs-index excerpt (a complete
// sentence of at most 160 characters). It lives here rather than in front
// matter because GitHub renders front matter as a table at the top of the file.
// A `description:` in a doc's front matter still wins, and is also shown as the lede.
const KNOWN = {
  index: { group: "Get started", order: 0, title: "Overview" },
  readme: { group: "Get started", order: 0, title: "Overview", slug: "overview" },
  overview: { group: "Get started", order: 0 },
  "getting-started": { group: "Get started", order: 1 },
  apple: {
    group: "Platforms",
    order: 10,
    nav: "Apple (SwiftUI)",
    summary: "Native Liquid Glass for SwiftUI on iOS 17+ and macOS 14+: refracting glass surfaces and controls, rendered with Metal from the shared optical model.",
  },
  android: {
    group: "Platforms",
    order: 11,
    nav: "Android (Compose)",
    summary: "Native Liquid Glass for Jetpack Compose: refraction, Clear and Regular materials, rim light and elastic interaction, drawn with AGSL on Android 13+.",
  },
  windows: {
    group: "Platforms",
    order: 12,
    nav: "Windows (WPF)",
    summary: "Native Liquid Glass for WPF on Windows 10 and 11, rendered by an HLSL pixel shader, for .NET Framework 4.8, .NET 8 and .NET 10.",
  },
  optics: {
    group: "Reference",
    order: 20,
    nav: "Optics",
    summary: "The single source of truth for how every port renders glass: shape, refraction, Clear and Regular materials, rim light, interaction and accessibility.",
  },
  components: {
    group: "Reference",
    order: 21,
    nav: "Components",
    summary: "The components every port ships under the same names, from scene and surface to tab bar, menu and progressive blur, with shared options and behavior.",
  },
};

/** Shorten text to at most `max` characters, at the end of a sentence when one fits, else at a word. */
function summarize(text, max = 160) {
  if (text.length <= max) return text;
  const head = text.slice(0, max + 1);
  const sentence = /^[\s\S]*[.!?](?=\s|$)/.exec(head)?.[0];
  if (sentence && sentence.length >= max * 0.5) return sentence.trim();
  return `${text.slice(0, max - 1).replace(/[\s,;:([]+\S*$/, "").replace(/[\s,;:]+$/, "")}…`;
}

function slugify(text) {
  return (
    text
      .toLowerCase()
      .trim()
      .replace(/<[^>]+>/g, "")
      .replace(/[^\p{L}\p{N}\s_-]/gu, "")
      .replace(/\s+/g, "-")
      .replace(/-+/g, "-")
      .replace(/^-|-$/g, "") || "section"
  );
}

async function discoverDocs() {
  const docs = [];
  const docsDir = path.join(repo, "docs");
  if (exists(docsDir)) {
    for (const name of (await readdir(docsDir)).sort()) {
      if (!name.toLowerCase().endsWith(".md")) continue;
      const base = name.slice(0, -3).toLowerCase();
      const known = KNOWN[base] ?? {};
      docs.push({ file: `docs/${name}`, slug: known.slug ?? slugify(base), ...known });
    }
  }
  for (const [file, slug] of [["spec/OPTICS.md", "optics"], ["spec/COMPONENTS.md", "components"]]) {
    if (exists(path.join(repo, file)) && !docs.some((d) => d.slug === slug)) docs.push({ file, slug, ...KNOWN[slug] });
  }
  return docs;
}

function parseFrontmatter(src) {
  const m = /^---\r?\n([\s\S]*?)\r?\n---\r?\n?/.exec(src);
  if (!m) return { data: {}, body: src };
  let data = {};
  try {
    data = YAML.parse(m[1] ?? "") ?? {};
  } catch {
    data = {};
  }
  return { data: typeof data === "object" && data ? data : {}, body: src.slice(m[0].length) };
}

async function copyMedia(rel) {
  // rel is repo-relative. Raster images become WebP (max 1600 px wide) in
  // public/media; anything else is copied as is.
  const from = path.join(repo, rel);
  if (!exists(from)) return null;
  if (/\.(png|jpe?g)$/i.test(rel)) {
    const outRel = rel.replace(/\.(png|jpe?g)$/i, ".webp");
    const to = path.join(pub, "media", outRel);
    const input = await readFile(from);
    const info = await cached(`media-${outRel.replace(/[^\w.-]+/g, "_")}`, sha(input), [to], async () => {
      const s = await sharp();
      const meta = await s(input).metadata();
      await mkdir(path.dirname(to), { recursive: true });
      const r = await s(input).resize({ width: Math.min(meta.width ?? 1600, 1600), withoutEnlargement: true }).webp({ quality: 86 }).toFile(to);
      return { width: r.width, height: r.height };
    });
    return { src: `/media/${toPosix(outRel)}`, width: info.width, height: info.height };
  }
  const to = path.join(pub, "media", rel);
  await mkdir(path.dirname(to), { recursive: true });
  const [a, b] = await Promise.all([stat(from), exists(to) ? stat(to) : null]);
  if (!b || b.size !== a.size || b.mtimeMs < a.mtimeMs) await copyFile(from, to);
  return { src: `/media/${toPosix(rel)}` };
}

async function renderDoc(entry, slugByFile) {
  const raw = await readFile(path.join(repo, entry.file), "utf8");
  const { data, body } = parseFrontmatter(raw);
  const docDir = path.posix.dirname(entry.file);
  const headings = [];
  const ids = new Map();
  const sections = [];
  let title = typeof data.title === "string" ? data.title : null;
  let firstH1 = true;
  const media = [];

  const unique = (base) => {
    const n = ids.get(base) ?? 0;
    ids.set(base, n + 1);
    return n ? `${base}-${n}` : base;
  };

  const resolveHref = (href) => {
    const link = classifyLink(href, docDir, slugByFile);
    if (link.kind === "as-is") return href;
    if (link.kind === "doc") return `/docs/${link.slug}${link.hash}`;
    return repoFileUrl(link);
  };

  const marked = new Marked({ gfm: true });
  const codeQueue = [];
  marked.use({
    renderer: {
      heading({ tokens, depth }) {
        const inner = this.parser.parseInline(tokens);
        const text = stripTags(inner).trim();
        if (depth === 1 && firstH1) {
          firstH1 = false;
          title ??= text;
          return "";
        }
        const id = unique(slugify(text));
        if (depth === 2 || depth === 3) headings.push({ depth, id, text });
        sections.push({ id, heading: text, depth });
        return `<h${depth} id="${id}"><a class="anchor" href="#${id}" aria-label="Link to ${escapeHtml(text)}">#</a>${inner}</h${depth}>\n`;
      },
      code({ text, lang }) {
        const token = `@@CODE${codeQueue.length}@@`;
        const titleMatch = /title="([^"]+)"/.exec(lang || "");
        codeQueue.push({ text, lang: lang || "", title: titleMatch?.[1] });
        return token;
      },
      link({ href, title: t, tokens }) {
        const inner = this.parser.parseInline(tokens);
        const resolved = resolveHref(href);
        const external = /^https?:/i.test(resolved) && !resolved.startsWith(SITE_URL);
        return `<a href="${escapeHtml(resolved)}"${t ? ` title="${escapeHtml(t)}"` : ""}${external ? ' rel="noopener" target="_blank"' : ""}>${inner}</a>`;
      },
      image({ href, title: t, text }) {
        const token = `@@IMG${media.length}@@`;
        media.push({ href, title: t, alt: text });
        return token;
      },
    },
  });

  let html = await marked.parse(body);

  // Code blocks (async highlighting) and images (copied into public/media).
  for (let i = 0; i < codeQueue.length; i++) {
    const c = codeQueue[i];
    const block = codeBlock(await highlight(c.text, c.lang), c.title);
    html = html.replace(`<p>@@CODE${i}@@</p>`, block).replace(`@@CODE${i}@@`, block);
  }
  for (let i = 0; i < media.length; i++) {
    const m = media[i];
    let src = m.href;
    let dims = "";
    if (src && !/^(https?:|data:|\/)/i.test(src)) {
      const rel = path.posix.normalize(path.posix.join(docDir, decodeURIComponent(src)));
      if (m.alt) imageAlts.set(rel, m.alt);
      const copied = await copyMedia(rel);
      if (!copied) {
        if (!quiet) console.warn(`[content] ${entry.file}: missing image ${rel}`);
        html = html.replace(`<p>@@IMG${i}@@</p>`, "").replace(`@@IMG${i}@@`, "");
        continue;
      }
      src = copied.src;
      mediaUrls.set(rel, copied.src);
      // The size variables let the CSS reserve the image's box before it loads (docs.css).
      if (copied.width && copied.height) dims = ` width="${copied.width}" height="${copied.height}" style="--w: ${copied.width}; --h: ${copied.height}"`;
    }
    const img = `<img src="${escapeHtml(src)}" alt="${escapeHtml(m.alt || "")}"${dims} loading="lazy" decoding="async"${m.title ? ` title="${escapeHtml(m.title)}"` : ""}>`;
    html = html.replace(`@@IMG${i}@@`, img);
  }

  // Raw HTML images in the Markdown (for example a centered gallery) get the
  // same treatment as Markdown images: copied, converted and lazy-loaded.
  const rawImages = [...html.matchAll(/<img\b[^>]*>/g)].map((m) => m[0]);
  for (const tag of new Set(rawImages)) {
    const src = /\bsrc="([^"]+)"/.exec(tag)?.[1];
    if (!src || /^(https?:|data:|\/)/i.test(src)) continue;
    const rel = path.posix.normalize(path.posix.join(docDir, decodeURIComponent(src)));
    const alt = /\balt="([^"]*)"/.exec(tag)?.[1];
    if (alt) imageAlts.set(rel, alt);
    const copied = await copyMedia(rel);
    if (!copied) {
      // Not in the repository yet: leave it out rather than show a broken image.
      if (!quiet) console.warn(`[content] ${entry.file}: missing image ${rel}`);
      html = html.split(tag).join("");
      continue;
    }
    mediaUrls.set(rel, copied.src);
    let next = tag.replace(/\bsrc="[^"]+"/, `src="${escapeHtml(copied.src)}"`);
    if (copied.width && copied.height && !/\b(width|height)=/.test(next)) next = next.replace("<img", `<img width="${copied.width}" height="${copied.height}"`);
    else if (copied.width && copied.height && !/\bstyle=/.test(next)) next = next.replace("<img", `<img style="aspect-ratio: ${copied.width} / ${copied.height}"`);
    if (!/\bloading=/.test(next)) next = next.replace("<img", '<img loading="lazy" decoding="async"');
    html = html.split(tag).join(next);
  }

  // Tables that hold nothing but images (a screenshot gallery written as a
  // Markdown table) become a centered gallery, like the raw-HTML ones.
  html = html.replace(/<table>([\s\S]*?)<\/table>/g, (whole, inner) => imageTableToGallery(inner) ?? whole);

  // Key/value tables written with an empty header row (`| | |`): drop the
  // empty header and make the first column row headers, so every cell has one
  // (unless that column holds only images, which are not headers).
  html = html.replace(/<table>\s*<thead>\s*<tr>((?:\s*<th[^>]*>\s*<\/th>)+)\s*<\/tr>\s*<\/thead>([\s\S]*?)<\/table>/g, (_m, _empty, rest) =>
    firstColumnIsHeader(rest) ? `<table class="table--keys">${toRowHeaders(rest)}</table>` : `<table>${rest}</table>`,
  );

  // Tables with some empty header cells: an empty corner makes the first column
  // row headers; any other empty header gets a screen-reader label.
  html = html.replace(/<table>\s*<thead>\s*<tr>([\s\S]*?)<\/tr>\s*<\/thead>([\s\S]*?)<\/table>/g, (whole, head, rest) => {
    const cells = [...head.matchAll(/<th([^>]*)>([\s\S]*?)<\/th>/g)];
    if (!cells.some((c) => !stripTags(c[2] ?? "").trim())) return whole;
    const cornerEmpty = !stripTags(cells[0]?.[2] ?? "").trim();
    const headHtml = cells
      .map((c, i) => (stripTags(c[2] ?? "").trim() || (i === 0 && cornerEmpty) ? `<th${c[1]}>${c[2]}</th>` : `<th${c[1]}><span class="sr-only">Details</span></th>`))
      .join("");
    const rows = cornerEmpty && firstColumnIsHeader(rest) ? toRowHeaders(rest) : rest;
    return `<table><thead><tr>${headHtml}</tr></thead>${rows}</table>`;
  });

  html = html
    .replace(/<table( class="[^"]*")?>/g, '<div class="table-wrap"><table$1>')
    .replace(/<\/table>/g, "</table></div>")
    // GitHub alerts: > [!NOTE] ...
    .replace(/<blockquote>\s*<p>\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*/g, (_m, kind) => `<blockquote class="callout callout--${kind.toLowerCase()}"><p><strong class="callout__label">${kind[0] + kind.slice(1).toLowerCase()}</strong> `);

  // Search sections: split the plain text at headings.
  const plain = [];
  const parts = html.split(/(?=<h[2-4] id=")/);
  for (const part of parts) {
    const m = /^<h[2-4] id="([^"]+)">/.exec(part);
    const text = stripTags(
      part
        .replace(/<a class="anchor"[^>]*>#<\/a>/g, "")
        // A code block's bar (language label and copy button) is chrome, not content.
        .replace(/<div class="code__bar">[\s\S]*?<\/div>/g, " ")
        .replace(/<button[^>]*>[\s\S]*?<\/button>/g, "")
        // Block boundaries separate words, so neighbouring blocks never fuse.
        .replace(/<\/(?:p|pre|li|dt|dd|td|th|tr|div|h[1-6]|blockquote|figcaption)>|<br\s*\/?>/g, " "),
    )
      .replace(/\s+/g, " ")
      .trim();
    if (!text) continue;
    const heading = m ? sections.find((s) => s.id === m[1])?.heading ?? "" : "";
    plain.push({ anchor: m?.[1] ?? "", heading, text: heading && text.startsWith(heading) ? text.slice(heading.length).trim() : text });
  }

  const firstPara = /<p>([\s\S]*?)<\/p>/.exec(html);
  const explicit = typeof data.description === "string";
  const description = summarize(
    explicit ? data.description.trim() : entry.summary ?? (firstPara ? stripTags(firstPara[1] ?? "").replace(/\s+/g, " ").trim() : ""),
  );
  title ??= entry.title ?? entry.slug;

  return {
    slug: entry.slug,
    title,
    nav: entry.nav ?? title,
    group: GROUPS.includes(data.group) ? data.group : entry.group ?? "Guides",
    order: typeof data.order === "number" ? data.order : entry.order ?? 100,
    description,
    lede: explicit,
    source: entry.file,
    sourceUrl: `${REPO_URL}/blob/main/${entry.file}`,
    updated: gitDate(entry.file),
    toc: headings,
    html,
    sections: plain,
    markdown: raw,
  };
}

function isDirectory(p) {
  try {
    return statSync(p).isDirectory();
  } catch {
    return false;
  }
}

/** Site URL of every repository image the docs show, by repo-relative path (filled while rendering). */
const mediaUrls = new Map();

/**
 * Where a link written in a doc points: another doc (by slug), a file or
 * folder in the repository, or somewhere the site leaves alone (absolute URLs,
 * fragments, site paths, and paths outside the repository).
 */
function classifyLink(href, docDir, slugByFile) {
  if (!href || /^(?:[a-z][a-z0-9+.-]*:|#|\/)/i.test(href)) return { kind: "as-is" };
  const cut = href.indexOf("#");
  const file = cut < 0 ? href : href.slice(0, cut);
  const hash = cut < 0 ? "" : href.slice(cut);
  let decoded = file;
  try {
    decoded = decodeURIComponent(file);
  } catch {
    /* keep it as written */
  }
  const target = path.posix.normalize(path.posix.join(docDir, decoded));
  const slug = slugByFile.get(target.toLowerCase());
  if (slug) return { kind: "doc", slug, hash };
  if (target === ".." || target.startsWith("../")) return { kind: "as-is" };
  return { kind: isDirectory(path.join(repo, target)) ? "tree" : "blob", target, hash };
}

const repoFileUrl = (link) => `${REPO_URL}/${link.kind}/main/${encodeURI(link.target)}${link.hash}`;

/** Apply `fn` to a Markdown document outside fenced code blocks and inline code spans. */
function outsideCode(markdown, fn) {
  const out = [];
  let prose = [];
  let fence = null;
  const flush = () => {
    if (!prose.length) return;
    // Inline code is masked (a link label may hold some), then put back unchanged.
    const code = [];
    const masked = prose.join("\n").replace(/``[\s\S]*?``|`[^`\n]*`/g, (span) => `\uE000${code.push(span) - 1}\uE001`);
    out.push(fn(masked).replace(/\uE000(\d+)\uE001/g, (_m, i) => code[Number(i)]));
    prose = [];
  };
  for (const line of markdown.split("\n")) {
    if (fence) {
      out.push(line);
      if (new RegExp(`^ {0,3}${fence[0]}{${fence.length},}\\s*$`).test(line)) fence = null;
      continue;
    }
    const open = /^ {0,3}(`{3,}|~{3,})/.exec(line);
    if (open?.[1]) {
      flush();
      fence = open[1];
      out.push(line);
    } else prose.push(line);
  }
  flush();
  return out.join("\n");
}

/**
 * A doc's Markdown with every relative link and image made absolute, for the
 * Markdown twins (/docs/<slug>.md) and llms-full.txt, which are read away from
 * the repository: docs point at their twins, images at the site's copies, and
 * other repository files at GitHub.
 */
function absoluteMarkdown(markdown, docFile, slugByFile) {
  const docDir = path.posix.dirname(docFile);
  const resolve = (href, image) => {
    const link = classifyLink(href, docDir, slugByFile);
    if (link.kind === "as-is") return /^\/(?!\/)/.test(href) ? `${SITE_URL}${href}` : href;
    if (link.kind === "doc") return `${SITE_URL}/docs/${link.slug}.md${link.hash}`;
    const media = mediaUrls.get(link.target);
    if (media) return `${SITE_URL}${media}`;
    return image && link.kind === "blob" ? `${REPO_URL}/raw/main/${encodeURI(link.target)}` : repoFileUrl(link);
  };
  const target = (raw, image) => (raw.startsWith("<") ? `<${resolve(raw.slice(1, -1), image)}>` : resolve(raw, image));
  const rewrite = (text) =>
    text
      // Inline links and images, [label](target "title"); a label may hold an image.
      .replace(
        /(!?)\[((?:[^[\]\\]|\\.|\[(?:[^[\]\\]|\\.)*\])*)\]\(\s*(<[^>\n]*>|[^\s()]+(?:\([^\s()]*\)[^\s()]*)*)((?:\s+(?:"[^"\n]*"|'[^'\n]*'|\([^)\n]*\)))?\s*)\)/g,
        (_m, bang, label, href, rest) => `${bang}[${bang ? label : rewrite(label)}](${target(href, Boolean(bang))}${rest})`,
      )
      // Reference definitions, [id]: target (not footnotes, [^1]: text)
      .replace(/^( {0,3}\[(?!\^)[^\]\n]+\]:[ \t]*)(<[^>\n]*>|\S+)/gm, (_m, lead, href) => `${lead}${target(href, false)}`)
      // Raw HTML: <img src="…">, <a href="…">
      .replace(/(<(img|source)\b[^>]*?\ssrc=")([^"]*)(")/gi, (_m, a, _tag, href, b) => `${a}${resolve(href, true)}${b}`)
      .replace(/(<a\b[^>]*?\shref=")([^"]*)(")/gi, (_m, a, href, b) => `${a}${resolve(href, false)}${b}`);
  return outsideCode(markdown, rewrite);
}

const ONLY_IMAGES = /^(?:<img\b[^>]*>\s*)+$/;

/** The table's first-column cells, when it has body rows. */
const firstCells = (rows) => [...rows.matchAll(/<tr>\s*<td[^>]*>([\s\S]*?)<\/td>/g)].map((m) => (m[1] ?? "").trim());

/** A first column is a row header unless it is empty or holds only images. */
function firstColumnIsHeader(rows) {
  const cells = firstCells(rows);
  return cells.length > 0 && !cells.every((c) => !c || ONLY_IMAGES.test(c));
}

const toRowHeaders = (rows) => rows.replace(/<tr>\s*<td([^>]*)>([\s\S]*?)<\/td>/g, '<tr><th scope="row"$1>$2</th>');

/** A table of images only, as a centered gallery (`<p align="center">`, styled in docs.css); null otherwise. */
function imageTableToGallery(inner) {
  const cells = [...inner.matchAll(/<t([hd])\b[^>]*>([\s\S]*?)<\/t\1>/g)].map((m) => (m[2] ?? "").trim()).filter(Boolean);
  if (!cells.length || !cells.every((c) => ONLY_IMAGES.test(c))) return null;
  const perRow = Math.max(1, ...[...inner.matchAll(/<tr>([\s\S]*?)<\/tr>/g)].map((r) => [...(r[1] ?? "").matchAll(/<img\b/g)].length));
  const images = cells.flatMap((c) => c.match(/<img\b[^>]*>/g) ?? []);
  const tall = images.some((img) => Number(/\bheight="(\d+)"/.exec(img)?.[1]) > Number(/\bwidth="(\d+)"/.exec(img)?.[1]));
  // Portrait screenshots go at most three to a row, so a pair is not a screen tall.
  const share = `${Math.floor(100 / (tall ? Math.max(perRow, 3) : perRow)) - 1}%`;
  const sized = images.map((img) => {
    const w = /\bwidth="(\d+)"/.exec(img)?.[1];
    const h = /\bheight="(\d+)"/.exec(img)?.[1];
    const plain = img.replace(/\s(?:width|height|style)="[^"]*"/g, "");
    return plain.replace("<img", `<img width="${share}"${w && h ? ` style="aspect-ratio: ${w} / ${h}"` : ""}`);
  });
  return `<p align="center">\n${sized.join("\n")}\n</p>`;
}

async function buildDocs() {
  const entries = await discoverDocs();
  const slugByFile = new Map(entries.map((e) => [e.file.toLowerCase(), e.slug]));
  const docs = [];
  for (const entry of entries) {
    try {
      docs.push(await renderDoc(entry, slugByFile));
    } catch (error) {
      console.warn(`[content] skipping ${entry.file}: ${error instanceof Error ? error.message : error}`);
    }
  }
  docs.sort((a, b) => GROUPS.indexOf(a.group) - GROUPS.indexOf(b.group) || a.order - b.order || a.title.localeCompare(b.title));

  // Warn about links to headings that do not exist (they would land at the top).
  const idsBySlug = new Map(docs.map((d) => [d.slug, new Set([...d.html.matchAll(/\sid="([^"]+)"/g)].map((m) => m[1]))]));
  for (const d of docs) {
    for (const m of d.html.matchAll(/href="(?:\/docs\/([a-z0-9-]+))?#([^"]+)"/g)) {
      const slug = m[1] ?? d.slug;
      const id = decodeURIComponent(m[2] ?? "");
      if (!idsBySlug.get(slug)?.has(id)) console.warn(`[content] ${d.source}: link to missing anchor ${slug}#${id}`);
    }
  }

  await rm(path.join(gen, "docs"), { recursive: true, force: true });
  for (const d of docs) {
    // The Markdown twin is read away from the repository: its links and images are made absolute.
    d.twin = absoluteMarkdown(d.markdown, d.source, slugByFile);
    const { sections: _s, markdown: _m, twin: _t, ...page } = d;
    await writeIfChanged(path.join(gen, "docs", `${d.slug}.json`), json(page));
    await writeIfChanged(path.join(pub, "docs", `${d.slug}.md`), d.twin);
  }
  // Remove raw markdown of docs that no longer exist.
  if (exists(path.join(pub, "docs"))) {
    for (const f of await readdir(path.join(pub, "docs"))) {
      if (f.endsWith(".md") && !docs.some((d) => `${d.slug}.md` === f)) await rm(path.join(pub, "docs", f));
    }
  }
  await writeIfChanged(
    path.join(gen, "docs-index.json"),
    json(docs.map(({ slug, title, nav, group, order, description, source, updated }) => ({ slug, title, nav, group, order, description, source, updated }))),
  );
  const search = [];
  for (const d of docs) for (const s of d.sections) search.push({ slug: d.slug, title: d.title, anchor: s.anchor, heading: s.heading, text: s.text.slice(0, 900) });
  await writeIfChanged(path.join(gen, "search.json"), json(search));
  log(`docs: ${docs.map((d) => d.slug).join(", ") || "(none)"}`);
  return docs;
}

// ---------------------------------------------------------------------------
// Agent skills (Claude Code plugin at plugins/liquid-glass)

async function readJson(file, fallback) {
  try {
    return JSON.parse(await readFile(file, "utf8"));
  } catch {
    return fallback;
  }
}

async function countFiles(dir) {
  let n = 0;
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    if (entry.name === ".DS_Store") continue;
    n += entry.isDirectory() ? await countFiles(path.join(dir, entry.name)) : 1;
  }
  return n;
}

// Main skill first, then the platform skills in platform order.
const SKILL_ORDER = ["liquid-glass", "liquid-glass-swiftui", "liquid-glass-compose", "liquid-glass-wpf", "liquid-glass-port"];

/**
 * Skill folders whose SKILL.md is committed (at HEAD), or null outside a git
 * checkout. The pages link every skill to GitHub, so a folder that exists only
 * in the working tree would publish links that 404.
 */
function committedSkills() {
  if (!inGitCheckout()) return null;
  try {
    const files = git(["ls-tree", "-r", "--name-only", "HEAD", "--", "plugins/liquid-glass/skills"]).split("\n");
    return new Set(files.map((f) => /^plugins\/liquid-glass\/skills\/([^/]+)\/SKILL\.md$/.exec(f)?.[1]).filter(Boolean));
  } catch {
    return new Set(); // a checkout without commits: nothing is published yet
  }
}

async function buildSkills() {
  const pluginDir = path.join(repo, "plugins/liquid-glass");
  const plugin = await readJson(path.join(pluginDir, ".claude-plugin/plugin.json"), null);
  const marketplace = await readJson(path.join(repo, ".claude-plugin/marketplace.json"), null);
  const skills = [];
  const skillsDir = path.join(pluginDir, "skills");
  const committed = committedSkills();
  if (exists(skillsDir)) {
    for (const name of (await readdir(skillsDir)).sort()) {
      const file = path.join(skillsDir, name, "SKILL.md");
      if (!exists(file)) continue;
      if (committed && !committed.has(name)) {
        console.warn(`[content] skipping skill ${name}: plugins/liquid-glass/skills/${name}/SKILL.md is not committed, so its GitHub links would 404`);
        continue;
      }
      const { data, body } = parseFrontmatter(await readFile(file, "utf8"));
      const heading = /^#\s+(.+)$/m.exec(body)?.[1]?.trim();
      skills.push({
        name: typeof data.name === "string" ? data.name : name,
        folder: name,
        title: heading ?? name,
        description: typeof data.description === "string" ? data.description.trim() : "",
        files: await countFiles(path.join(skillsDir, name)),
        source: `plugins/liquid-glass/skills/${name}/SKILL.md`,
      });
    }
  }
  skills.sort((a, b) => (SKILL_ORDER.indexOf(a.name) + 1 || 99) - (SKILL_ORDER.indexOf(b.name) + 1 || 99) || a.name.localeCompare(b.name));
  const missing = SKILL_ORDER.filter((name) => !skills.some((s) => s.name === name));
  if (missing.length) console.warn(`[content] skills missing from the plugin: ${missing.join(", ")}`);
  const out = {
    plugin: plugin ? { name: plugin.name, version: plugin.version, description: plugin.description } : null,
    marketplace: marketplace ? { name: marketplace.name, description: marketplace.description } : null,
    hasAgentsMd: exists(path.join(repo, "AGENTS.md")),
    skills,
  };
  await writeIfChanged(path.join(gen, "skills.json"), json(out));
  log(`skills: ${skills.map((s) => s.name).join(", ") || "(none)"}`);
  return out;
}

// ---------------------------------------------------------------------------
// Platform snippets for the home page (taken from the real docs when present)

const SNIPPET_LANGS = { apple: ["swift"], android: ["kotlin", "kts"], windows: ["xml", "xaml"] };

function pickSnippet(markdown, langs) {
  if (!markdown) return null;
  const blocks = [];
  const re = /^(```|~~~)\s*([\w#+-]*)[^\n]*\n([\s\S]*?)^\1\s*$/gm;
  let m;
  while ((m = re.exec(markdown))) {
    const before = markdown.slice(0, m.index);
    const heading = [...before.matchAll(/^#{2,3}\s+(.+)$/gm)].pop()?.[1] ?? "";
    blocks.push({ lang: (m[2] || "").toLowerCase(), code: (m[3] ?? "").replace(/\s+$/, ""), heading });
  }
  const candidates = blocks.filter((b) => langs.includes(b.lang) && /Glass/.test(b.code) && b.code.split("\n").length >= 5 && b.code.split("\n").length <= 40);
  const score = (b) => (/(quick ?start|usage|getting started|example|first)/i.test(b.heading) ? 10 : 0) + (/GlassScene|GlassSurface|liquidGlass/.test(b.code) ? 5 : 0) - b.code.split("\n").length / 40;
  candidates.sort((a, b) => score(b) - score(a));
  return candidates[0] ?? null;
}

async function buildSnippets(docs) {
  const out = {};
  for (const [platform, langs] of Object.entries(SNIPPET_LANGS)) {
    const doc = docs.find((d) => d.slug === platform);
    const pick = pickSnippet(doc?.markdown, langs);
    if (pick) {
      const h = await highlight(pick.code, pick.lang);
      out[platform] = { code: pick.code, html: h.html, lang: h.lang, source: doc?.source ?? null };
    }
  }
  await writeIfChanged(path.join(gen, "snippets.json"), json(out));
  log(`snippets: ${Object.keys(out).join(", ") || "(fallbacks)"}`);
}

/** Highlight the site's built-in code samples (install commands and fallbacks). */
async function buildStaticCode() {
  const file = path.join(site, "src/content/code.json");
  const samples = await readJson(file, {});
  // Package ids and versions come from the downloads manifest when it exists.
  const manifest = await readJson(path.join(pub, "downloads/manifest.json"), null);
  const nuget = manifest?.items?.filter((i) => i.id?.startsWith("nuget-") && i.packageId) ?? [];
  const nugetId = (nuget.find((i) => /wpf/i.test(i.packageId)) ?? nuget[0])?.packageId ?? "LiquidGlassEngineering.Wpf";
  const vars = { NUGET_ID: nugetId, VERSION: manifest?.version ?? "1.0.0", SITE_URL };
  const fill = (s) => s.replace(/\{\{(\w+)\}\}/g, (_m, k) => vars[k] ?? _m);
  await writeIfChanged(path.join(gen, "packages.json"), json({ nugetId, version: vars.VERSION }));
  const out = {};
  for (const [key, { code: raw, lang }] of Object.entries(samples)) {
    const code = fill(raw);
    out[key] = { code, ...(await highlight(code, lang)) };
  }
  await writeIfChanged(path.join(gen, "code.json"), json(out));
  return { nugetId, version: vars.VERSION };
}

// ---------------------------------------------------------------------------
// Images (sharp is loaded lazily; it is only needed when inputs change)

let sharpModule;
async function sharp() {
  sharpModule ??= (await import("sharp")).default;
  return sharpModule;
}

async function cached(key, inputs, outputs, work) {
  const stampFile = path.join(cacheDir, `${key}.json`);
  const stamp = await readJson(stampFile, null);
  if (stamp && stamp.inputs === inputs && outputs.every(exists)) return stamp.result;
  const result = await work();
  await mkdir(cacheDir, { recursive: true });
  await writeFile(stampFile, JSON.stringify({ inputs, result }));
  return result;
}

const WALLPAPER_SIZES = [640, 1024, 1536, 2048, 3072];
const WALLPAPER_FOCUS = { aurora: [0.5, 0.42], dunes: [0.5, 0.4], ocean: [0.5, 0.4], midnight: [0.5, 0.42] };

async function buildWallpapers() {
  const dir = path.join(repo, "assets/wallpapers");
  const names = new Map();
  if (exists(dir)) {
    for (const f of await readdir(dir)) {
      const m = /^([a-z0-9-]+)-(\d+)\.jpe?g$/i.exec(f);
      if (!m || !m[1] || !m[2]) continue;
      const size = Number(m[2]);
      const prev = names.get(m[1]);
      if (!prev || size > prev.size) names.set(m[1], { file: path.join(dir, f), size });
    }
  }
  const order = ["dunes", "aurora", "ocean", "midnight"];
  const items = {};
  const outDir = path.join(pub, "wallpapers");
  await mkdir(outDir, { recursive: true });
  const sorted = [...names.keys()].sort((a, b) => (order.indexOf(a) + 1 || 99) - (order.indexOf(b) + 1 || 99) || a.localeCompare(b));
  for (const name of sorted) {
    const src = names.get(name);
    if (!src) continue;
    const input = await readFile(src.file);
    const sizes = WALLPAPER_SIZES.filter((s) => s <= src.size);
    const outputs = sizes.flatMap((s) => ["avif", "webp", "jpg"].map((ext) => path.join(outDir, `${name}-${s}.${ext}`)));
    items[name] = await cached(`wallpaper-${name}`, sha(input) + sizes.join(","), outputs, async () => {
      const s = await sharp();
      const meta = await s(input).metadata();
      for (const size of sizes) {
        const base = s(input).resize(size, Math.round((size * (meta.height ?? size)) / (meta.width ?? size)), { kernel: "lanczos3" });
        await base.clone().avif({ quality: 62, effort: 6, chromaSubsampling: "4:2:0" }).toFile(path.join(outDir, `${name}-${size}.avif`));
        await base.clone().webp({ quality: 82, effort: 5 }).toFile(path.join(outDir, `${name}-${size}.webp`));
        await base.clone().jpeg({ quality: 84, mozjpeg: true, progressive: true }).toFile(path.join(outDir, `${name}-${size}.jpg`));
      }
      // Average color and the brightness of the top third (where hero copy sits).
      const { data } = await s(input).resize(48, 48, { fit: "fill" }).removeAlpha().raw().toBuffer({ resolveWithObject: true });
      let r = 0, g = 0, b = 0, top = 0;
      for (let i = 0; i < 48 * 48; i++) {
        const R = data[i * 3] ?? 0, G = data[i * 3 + 1] ?? 0, B = data[i * 3 + 2] ?? 0;
        r += R; g += G; b += B;
        if (i < 48 * 16) top += 0.2126 * R + 0.7152 * G + 0.0722 * B;
      }
      const n = 48 * 48;
      const hex = (v) => Math.round(v / n).toString(16).padStart(2, "0");
      const topLuma = top / (48 * 16) / 255;
      return {
        width: meta.width ?? src.size,
        height: meta.height ?? src.size,
        color: `#${hex(r)}${hex(g)}${hex(b)}`,
        tone: topLuma > 0.55 ? "light" : "dark",
        focus: WALLPAPER_FOCUS[name] ?? [0.5, 0.5],
      };
    });
  }
  const sizes = WALLPAPER_SIZES.filter((s) => Object.values(items).every((it) => it.width >= s));
  await writeIfChanged(path.join(gen, "wallpapers.json"), json({ sizes: sizes.length ? sizes : [1024], formats: ["avif", "webp", "jpg"], items }));
  log(`wallpapers: ${Object.keys(items).join(", ") || "(none)"}`);
}

const NAME_TOKENS = { ios: "iOS", ipados: "iPadOS", macos: "macOS", ipad: "iPad", iphone: "iPhone", wpf: "WPF", ui: "UI" };

/** Alt text for a screenshot no doc describes, from its file name ("ios-blur" -> "iOS blur"). */
function altFromName(base) {
  const words = base.split(/[-_\s]+/).filter(Boolean).map((w) => NAME_TOKENS[w.toLowerCase()] ?? w.toLowerCase());
  const text = words.join(" ");
  return /^[a-z]/.test(text) ? text[0].toUpperCase() + text.slice(1) : text;
}

async function buildScreens() {
  const root = path.join(repo, "docs/images");
  const out = {};
  if (exists(root)) {
    for (const platform of (await readdir(root)).sort()) {
      const dir = path.join(root, platform);
      if (!(await stat(dir)).isDirectory()) continue;
      const list = [];
      for (const f of (await readdir(dir)).sort()) {
        if (!/\.(png|jpe?g|webp)$/i.test(f)) continue;
        const input = await readFile(path.join(dir, f));
        const base = f.replace(/\.[^.]+$/, "");
        const target = path.join(pub, "media/screens", platform, `${base}.webp`);
        const info = await cached(`screen-${platform}-${base}`, sha(input), [target], async () => {
          const s = await sharp();
          const meta = await s(input).metadata();
          await mkdir(path.dirname(target), { recursive: true });
          const r = await s(input).resize({ width: Math.min(meta.width ?? 1600, 1600), withoutEnlargement: true }).webp({ quality: 84 }).toFile(target);
          return { width: r.width, height: r.height };
        });
        list.push({
          src: `/media/screens/${platform}/${base}.webp`,
          width: info.width,
          height: info.height,
          name: base,
          alt: imageAlts.get(`docs/images/${platform}/${f}`) ?? altFromName(base),
        });
      }
      if (list.length) out[platform] = list;
    }
  }
  await writeIfChanged(path.join(gen, "screens.json"), json(out));
  log(`screenshots: ${Object.entries(out).map(([p, l]) => `${p} ${l.length}`).join(", ") || "(none)"}`);
}

async function buildFonts() {
  const from = path.join(site, "node_modules/@fontsource-variable/inter/files");
  for (const sub of ["latin", "latin-ext"]) {
    const src = path.join(from, `inter-${sub}-opsz-normal.woff2`);
    if (exists(src)) await writeIfChanged(path.join(pub, "fonts", `inter-${sub}-opsz.woff2`), await readFile(src));
  }
}

async function buildIcons() {
  const svgFile = path.join(site, "src/content/app-icon.svg");
  if (!exists(svgFile)) return;
  const svg = await readFile(svgFile);
  const targets = [
    ["apple-touch-icon.png", 180],
    ["icon-192.png", 192],
    ["icon-512.png", 512],
  ];
  await cached("icons", sha(svg), targets.map(([f]) => path.join(pub, "icons", f)), async () => {
    const s = await sharp();
    await mkdir(path.join(pub, "icons"), { recursive: true });
    for (const [file, size] of targets) await s(svg, { density: 300 }).resize(size, size).png({ compressionLevel: 9 }).toFile(path.join(pub, "icons", file));
    return true;
  });
}

// ---------------------------------------------------------------------------
// Downloads manifest (written by scripts/build-downloads.mjs)

async function buildDownloads() {
  const file = path.join(pub, "downloads/manifest.json");
  const manifest = await readJson(file, null);
  // First-launch notes for apps that are not notarized or not code-signed.
  const notes = {};
  try {
    const readme = await readFile(path.join(repo, "artifacts/apple/README.txt"), "utf8");
    // Since macOS 15, Control-click › Open no longer overrides Gatekeeper for an app that is not notarized.
    if (/not notarized/i.test(readme)) {
      notes["macos-studio"] =
        'Not notarized, so macOS blocks the first launch: open System Settings › Privacy & Security and click Open Anyway (on macOS 14, Control-click › Open also works), or run xattr -dr com.apple.quarantine "/Applications/Liquid Glass Studio.app".';
    }
  } catch {
    /* no notes */
  }
  // build-downloads records whether each Windows Studio executable carries an Authenticode signature.
  for (const item of manifest?.items ?? []) {
    if (item.platform === "windows" && item.kind === "app" && item.signed === false) {
      notes[item.id] = "Not code-signed, so Windows SmartScreen may stop the first launch: choose More info › Run anyway.";
    }
  }
  await writeIfChanged(path.join(gen, "downloads.json"), json({ ...(manifest ?? { schema: 1, version: "1.0.0", generatedAt: null, items: [] }), notes }));
  log(`downloads: ${manifest ? `${manifest.items.filter((i) => i.available).length}/${manifest.items.length} available` : "no manifest yet"}`);
}

// ---------------------------------------------------------------------------
// llms.txt

async function buildLlms(docs, skills, packages) {
  const { nugetId, version } = packages;
  const repoSlug = new URL(REPO_URL).pathname.slice(1);
  const lines = [
    "# Liquid Glass Engineering",
    "",
    "> Native Liquid Glass for iOS, macOS, Android and Windows: refraction, Clear and Regular materials, rim light and elastic interaction, with one optical model shared by SwiftUI (Metal), Jetpack Compose (AGSL) and WPF (HLSL). Open source, MIT.",
    "",
    `- Website: ${SITE_URL}`,
    `- Source: ${REPO_URL}`,
    "",
    "## Install",
    "",
    `- Swift Package Manager (iOS 17+, macOS 14+): \`.package(url: "${REPO_URL}", from: "${version}")\`, product \`LiquidGlass\``,
    `- Gradle (Jetpack Compose, Android 7+, glass on 13+): repository \`maven("${SITE_URL}/maven")\` in settings.gradle.kts, then \`implementation("io.github.rookasrudzianskas:liquid-glass-compose:${version}")\``,
    `- Gradle from JitPack instead: repository \`maven("https://jitpack.io")\`, then \`implementation("com.github.${repoSlug.replace("/", ":")}:${version}")\``,
    `- NuGet (WPF on .NET Framework 4.8, .NET 8 and .NET 10): the package ${nugetId} ${version} is on the project feed, not nuget.org: \`dotnet nuget add source ${SITE_URL}/nuget/index.json -n liquid-glass\`, then \`dotnet add package ${nugetId} --version ${version}\``,
    "",
    "## Docs",
    "",
    ...docs.map((d) => `- [${d.title}](${SITE_URL}/docs/${d.slug}.md): ${d.description || d.nav}`),
    "",
  ];
  if (skills.skills.length) {
    const plugin = `${skills.plugin?.name ?? "liquid-glass"}@${skills.marketplace?.name ?? "liquid-glass-engineering"}`;
    lines.push("## Agent skills", "", `Install in Claude Code: \`/plugin marketplace add ${repoSlug}\` then \`/plugin install ${plugin}\`. Or unzip ${SITE_URL}/downloads/liquid-glass-skills.zip into ~/.claude/skills (Claude Code) or ~/.agents/skills (other Agent Skills tools).`, "");
    for (const s of skills.skills) lines.push(`- [${s.name}](${REPO_URL}/blob/main/${s.source}): ${s.description}`);
    lines.push("");
  }
  lines.push("## Optional", "", `- [Downloads](${SITE_URL}/downloads): packages, demo apps, source and skills with SHA-256 checksums`, `- [Playground](${SITE_URL}/playground): live WebGL2 materials lab`, "");
  await writeIfChanged(path.join(pub, "llms.txt"), lines.join("\n"));

  const full = [`# Liquid Glass Engineering: full documentation`, "", `> Generated from ${REPO_URL} for language models. Each section is one documentation page.`, ""];
  for (const d of docs) {
    full.push(`---`, "", `<!-- ${SITE_URL}/docs/${d.slug} (source: ${d.source}) -->`, "", parseFrontmatter(d.twin ?? d.markdown).body.trim(), "");
  }
  await writeIfChanged(path.join(pub, "llms-full.txt"), full.join("\n"));
}

async function buildSite() {
  let notice = "";
  try {
    const src = await readFile(path.join(repo, "NOTICE.md"), "utf8");
    notice = /^Liquid Glass Engineering is[^\n]+/m.exec(src)?.[0] ?? "";
  } catch {
    /* optional */
  }
  await writeIfChanged(path.join(gen, "site.json"), json({ siteUrl: SITE_URL, repoUrl: REPO_URL, notice }));
}

// ---------------------------------------------------------------------------

async function main() {
  const started = Date.now();
  await mkdir(gen, { recursive: true });
  await buildSite();
  const docs = await buildDocs();
  const skills = await buildSkills();
  await buildSnippets(docs);
  const packages = await buildStaticCode();
  await buildLlms(docs, skills, packages);
  await buildDownloads();
  await buildFonts();
  await buildIcons();
  await buildWallpapers();
  await buildScreens();
  log(`done in ${Date.now() - started} ms`);
}

await main();
