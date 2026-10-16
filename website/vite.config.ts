import { fileURLToPath } from "node:url";
import { spawn } from "node:child_process";
import { writeFileSync } from "node:fs";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import type { Plugin } from "vite";

const repo = fileURLToPath(new URL("..", import.meta.url));

/**
 * In development, regenerate docs, skills and llms.txt whenever the repo's
 * Markdown changes (other agents keep writing docs/*.md while the site runs).
 */
function contentWatch(): Plugin {
  let running = false;
  let queued = false;
  const run = () => {
    if (running) {
      queued = true;
      return;
    }
    running = true;
    const child = spawn(process.execPath, [fileURLToPath(new URL("./scripts/content.mjs", import.meta.url)), "--quiet"], { stdio: "inherit" });
    child.on("exit", () => {
      running = false;
      if (queued) {
        queued = false;
        run();
      }
    });
  };
  return {
    name: "lg-content-watch",
    apply: "serve",
    configureServer(server) {
      const watched = [`${repo}docs`, `${repo}spec`, `${repo}plugins`, `${repo}assets`, `${repo}NOTICE.md`];
      server.watcher.add(watched);
      server.watcher.on("all", (_event, file) => {
        if (watched.some((dir) => file.startsWith(dir))) run();
      });
    },
  };
}

/** Development only: receives the PNG rendered by /og.html and writes public/og.png. */
function ogSave(): Plugin {
  return {
    name: "lg-og-save",
    apply: "serve",
    configureServer(server) {
      server.middlewares.use("/__og/save", (req, res) => {
        if (req.method !== "POST") {
          res.statusCode = 405;
          res.end();
          return;
        }
        const chunks: Buffer[] = [];
        req.on("data", (c: Buffer) => chunks.push(c));
        req.on("end", () => {
          const png = Buffer.concat(chunks);
          if (png.subarray(1, 4).toString() !== "PNG") {
            res.statusCode = 400;
            res.end("not a png");
            return;
          }
          writeFileSync(fileURLToPath(new URL("./public/og.png", import.meta.url)), png);
          res.end("ok");
        });
      });
    },
  };
}

export default defineConfig({
  plugins: [react(), contentWatch(), ogSave()],
  resolve: {
    alias: [
      { find: /^@lg\/reference$/, replacement: `${repo}spec/reference.mjs` },
      { find: /^@lg\/shader/, replacement: `${repo}spec/shaders/liquid-glass.glsl` },
    ],
  },
  server: {
    fs: { allow: [repo] },
  },
  build: {
    target: "es2022",
    manifest: true,
    sourcemap: false,
    reportCompressedSize: false,
  },
});
