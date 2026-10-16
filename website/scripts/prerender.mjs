#!/usr/bin/env node
// Prerender every route into static HTML (dist/<route>.html) so each page
// loads with its content, title and description, and works on hard refresh.
// Also writes sitemap.xml. Run after `vite build` and the SSR build.

import { readFile, rm, writeFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const dist = path.join(site, "dist");
const SITE_URL = (process.env.LG_SITE_URL || "https://liquid-glass-engineering.vercel.app").replace(/\/$/, "");

let template = await readFile(path.join(dist, "index.html"), "utf8");
// Inline the stylesheet: one render-blocking request fewer on every page.
for (const m of template.matchAll(/<link rel="stylesheet"[^>]*href="\/(assets\/[^"]+\.css)"[^>]*>/g)) {
  const css = await readFile(path.join(dist, m[1]), "utf8");
  template = template.replace(m[0], () => `<style>${css}</style>`);
}
const manifest = JSON.parse(await readFile(path.join(dist, ".vite/manifest.json"), "utf8"));
const { render, paths } = await import(pathToFileURL(path.join(site, "dist-ssr/entry-server.js")).href);

function preloads(modules) {
  const seen = new Set();
  const files = [];
  const visit = (key) => {
    const chunk = manifest[key];
    if (!chunk || seen.has(key)) return;
    seen.add(key);
    if (!chunk.isEntry) files.push(chunk.file);
    for (const imp of chunk.imports ?? []) visit(imp);
  };
  for (const m of modules) visit(m);
  return files.map((f) => `<link rel="modulepreload" crossorigin href="/${f}" />`).join("\n    ");
}

const outFile = (p) => (p === "/" ? "index.html" : `${p.slice(1)}.html`);
const pages = paths();
const sitemap = [];
for (const { path: p, updated } of pages) {
  const r = await render(p);
  let html = template.replace("<!--app-head-->", `${r.head}\n    ${preloads(r.modules)}`).replace("<!--app-html-->", r.html);
  if (html.includes("<!--app-html-->") || html.includes("<!--app-head-->")) throw new Error(`placeholders left in ${p}`);
  const file = path.join(dist, outFile(p));
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, html);
  if (p !== "/404" && !r.noindex) sitemap.push({ loc: `${SITE_URL}${p === "/" ? "/" : p}`, updated });
  console.log(`[prerender] ${p} -> ${outFile(p)} (${(html.length / 1024).toFixed(1)} kB)`);
}

const xml = [
  '<?xml version="1.0" encoding="UTF-8"?>',
  '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">',
  ...sitemap.map((u) => `  <url><loc>${u.loc}</loc>${u.updated ? `<lastmod>${u.updated.slice(0, 10)}</lastmod>` : ""}</url>`),
  "</urlset>",
  "",
].join("\n");
await writeFile(path.join(dist, "sitemap.xml"), xml);
await rm(path.join(site, "dist-ssr"), { recursive: true, force: true });
console.log(`[prerender] ${pages.length} pages, sitemap with ${sitemap.length} urls`);
