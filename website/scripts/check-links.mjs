#!/usr/bin/env node
// Checks every internal link and asset in the prerendered site (dist/) the way
// Vercel serves it (cleanUrls): /docs/apple -> docs/apple.html. Prints broken
// links; with --strict it exits non-zero when there are any.

import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const dist = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../dist");
const strict = process.argv.includes("--strict");

function walk(dir) {
  const out = [];
  for (const name of readdirSync(dir)) {
    const p = path.join(dir, name);
    if (statSync(p).isDirectory()) out.push(...walk(p));
    else if (name.endsWith(".html")) out.push(p);
  }
  return out;
}

function resolves(urlPath) {
  const clean = decodeURIComponent(urlPath.split(/[?#]/)[0] ?? "");
  if (clean === "/" || clean === "") return existsSync(path.join(dist, "index.html"));
  const rel = clean.replace(/^\//, "");
  const candidates = [rel, `${rel}.html`, path.join(rel, "index.html")];
  return candidates.some((c) => existsSync(path.join(dist, c)) && statSync(path.join(dist, c)).isFile());
}

const pages = walk(dist);
const broken = new Map();
const anchors = new Map();
for (const file of pages) {
  const html = readFileSync(file, "utf8");
  const page = `/${path.relative(dist, file).replace(/\.html$/, "").replace(/^index$/, "")}`;
  anchors.set(page, new Set([...html.matchAll(/\sid="([^"]+)"/g)].map((m) => m[1])));
  for (const m of html.matchAll(/\s(?:href|src)="([^"]+)"/g)) {
    const url = m[1].replace(/&amp;/g, "&");
    if (!url.startsWith("/") || url.startsWith("//")) continue;
    if (!resolves(url)) broken.set(`${page} -> ${url}`, true);
  }
  for (const m of html.matchAll(/\ssrcset="([^"]+)"/g)) {
    for (const part of m[1].split(",")) {
      const url = part.trim().split(/\s+/)[0];
      if (url?.startsWith("/") && !resolves(url)) broken.set(`${page} -> ${url}`, true);
    }
  }
  for (const m of html.matchAll(/url\(&quot;(\/[^&]+)&quot;\)|url\("(\/[^"]+)"\)/g)) {
    const url = m[1] ?? m[2];
    if (url && !resolves(url)) broken.set(`${page} -> ${url}`, true);
  }
}
// Same-site anchors (/docs/x#id and #id).
for (const file of pages) {
  const html = readFileSync(file, "utf8");
  const page = `/${path.relative(dist, file).replace(/\.html$/, "").replace(/^index$/, "")}`;
  for (const m of html.matchAll(/\shref="((?:\/[^"#]*)?)#([^"]+)"/g)) {
    const target = m[1] || page;
    const ids = anchors.get(target === "/" ? "/" : target.replace(/\/$/, ""));
    if (ids && !ids.has(decodeURIComponent(m[2]))) broken.set(`${page} -> ${target}#${m[2]} (missing anchor)`, true);
  }
}

if (broken.size) {
  console.warn(`[links] ${broken.size} broken internal link(s):`);
  for (const k of broken.keys()) console.warn(`  ${k}`);
  if (strict) process.exit(1);
} else console.log(`[links] ${pages.length} pages, all internal links resolve`);
