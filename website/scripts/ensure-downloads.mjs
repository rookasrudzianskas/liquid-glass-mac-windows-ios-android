#!/usr/bin/env node
// Collects the downloads (scripts/build-downloads.mjs, tolerant of missing platform artifacts) when
// public/downloads has no manifest yet, so `npm run build` works on a fresh clone: the Downloads page
// links SHA256SUMS and manifest.json, which only that script writes. A manifest already there (from
// CI or website/scripts/deploy.sh, possibly built with --strict or another --base-url) is left alone.

import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const site = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repo = path.resolve(site, "..");

if (!existsSync(path.join(site, "public/downloads/manifest.json"))) {
  console.log("[downloads] no public/downloads/manifest.json yet: running scripts/build-downloads.mjs");
  execFileSync(process.execPath, [path.join(repo, "scripts/build-downloads.mjs"), "--quiet"], { stdio: "inherit" });
}
