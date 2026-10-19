#!/usr/bin/env node
// Builds the website's download area from the platform build outputs.
//
// Other jobs drop their release builds into artifacts/<platform>/ (gitignored). This script
// collects them into the website's public folder (also gitignored) and writes the manifest the
// website renders:
//
//   website/public/downloads/              Studio apps, libraries, source zip, agent skills
//   website/public/downloads/manifest.json what the website renders (schema 1), including the commit it was
//                                          built from (`commit`, and `modified` when the working tree had changes)
//   website/public/downloads/SHA256SUMS    `sha256sum -c` compatible list of the downloads
//   website/public/maven/                  static Maven repository for the Compose library
//   website/public/nuget/                  static NuGet v3 feed for the WPF packages
//
// The feeds serve every version found in artifacts/ (index.json, registrations and maven-metadata.xml list them
// all, sorted, with the newest as latest); downloads/ carries only the newest version of each package. A deployed
// feed must keep every released version, but artifacts/ starts empty in a fresh clone or worktree, so the release
// path (--restore-released, which website/scripts/deploy.sh passes, and --strict) first restores the packages of
// every published GitHub release other than this version that are missing from artifacts/: the .nupkg assets of
// the release, checked against its SHA256SUMS (and GitHub's asset digest), into artifacts/windows/nuget, and the
// Maven files of the Compose library from the production site's /maven, checked against their .sha256 and .sha512
// files and the Gradle module's file hashes, with the AAR checked against the release's SHA256SUMS, into
// artifacts/android/maven. Restored files are only added, never overwritten, and get the release's publication
// time as their modification time, so the feeds stay reproducible.
//
// Usage:
//   node scripts/build-downloads.mjs [--strict] [--restore-released] [--base-url <url>] [--root <dir>] [--out <dir>] [--quiet]
//
// Guarantees:
//   * Idempotent. Unchanged inputs give byte-identical outputs: zips come from a deterministic
//     writer, files are rewritten only when their bytes change, and the manifest keeps an
//     item's updatedAt (and the top-level generatedAt) while nothing changed.
//   * Tolerant. A missing input becomes a manifest item with `available: false` and no link.
//     --strict (the production deploy) exits non-zero when a *required* item is missing or a
//     release check fails: the tree is committed and pushed, the release tag is on origin (the
//     Swift package and JitPack resolve versions from tags), every skill is committed, each
//     NuGet package targets the frameworks its .csproj declares, and the feeds serve every
//     published release and no other version than this one.
//   * Fetches the GitHub releases only with --restore-released or --strict, never in the Website workflow or
//     npm run build, whose feeds come from local files only. A release that cannot be listed, fetched or verified
//     then fails the run.
//   * Owns exactly three folders below the output folder (downloads/, maven/, nuget/) and
//     removes files there that this run does not produce. Nothing else is ever deleted.
//   * Never follows a symlink out of the input folder it was found in.
//
// Environment:
//   LG_VERSION         release version (default 1.0.1); artifacts may report their own
//   LG_SITE_URL        default for --base-url, and the site --restore-released fetches earlier Maven files
//                      from (default https://liquid-glass-engineering.vercel.app)
//   LG_NUGET_IDS       optional comma-separated NuGet package ids that must all be present
//   SOURCE_DATE_EPOCH  optional clock override (seconds) for generatedAt
//
// Only Node built-ins are used (Node 20.19+, developed on Node 22).

import crypto from "node:crypto";
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import zlib from "node:zlib";

// -------------------------------------------------------------------------------------------
// Constants
// -------------------------------------------------------------------------------------------

const REPOSITORY_URL = "https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android";
const RELEASES_API_URL = "https://api.github.com/repos/rookasrudzianskas/liquid-glass-mac-windows-ios-android/releases";
const DEFAULT_SITE_URL = "https://liquid-glass-engineering.vercel.app";
const DEFAULT_VERSION = "1.0.1";

// Maven coordinates of the Compose library (groupId as a path, artifactId).
const MAVEN_GROUP_PATH = "io/github/rookasrudzianskas";
const MAVEN_ARTIFACT = "liquid-glass-compose";

// Placeholder NuGet id shown when no package has been built yet and LG_NUGET_IDS is unset.
const DEFAULT_NUGET_ID = "LiquidGlassEngineering.Wpf";

// Skills the website expects; any other skill folder found is appended after these.
const EXPECTED_SKILLS = [
  "liquid-glass",
  "liquid-glass-swiftui",
  "liquid-glass-compose",
  "liquid-glass-wpf",
  "liquid-glass-port",
];

// The only folders (below --out) this script writes to and cleans.
const OWNED_DIRS = ["downloads", "maven", "nuget"];
const MANIFEST_REL = "downloads/manifest.json";
const SUMS_REL = "downloads/SHA256SUMS";

// Never published, wherever they appear.
const SKIPPED_NAMES = new Set([".DS_Store"]);

// Checksums a Maven repository carries next to every file.
const MAVEN_ALGOS = ["md5", "sha1", "sha256", "sha512"];
const MAVEN_CHECKSUM_FILE = /\.(md5|sha1|sha256|sha512)$/i;
// Bookkeeping files a copy of ~/.m2 (mavenLocal) brings along; not part of a remote repository.
const MAVEN_LOCAL_JUNK =
  /(^|\/)(_remote\.repositories|maven-metadata-local[^/]*|resolver-status\.properties|[^/]*\.lastUpdated)$/;

const REQUIREMENTS = {
  macos: "macOS 14 Sonoma or later",
  iosSimulator: "iOS 17 Simulator (Xcode 16 or later)",
  xcframework: "iOS 17+ / macOS 14+; Xcode 16 or later",
  androidApp: "Android 7.0 (API 24) or later; glass rendering on Android 13 (API 33)+",
  androidLibrary: "Jetpack Compose, minSdk 24; glass rendering on Android 13 (API 33)+",
  source: "Git snapshot of the repository; see the README for each platform's toolchain",
  skills: "Claude Code or any agent that supports Agent Skills (SKILL.md)",
};

const USAGE = `Usage: node scripts/build-downloads.mjs [options]

Collects artifacts/<platform>/ into <out>/downloads, <out>/maven and <out>/nuget and writes
<out>/downloads/manifest.json and SHA256SUMS.

Options:
  --strict           exit 1 (after writing) when a required item is missing or a release
                     check fails (clean, pushed tree; release tag on origin; committed
                     skills; NuGet target frameworks matching the .csproj; feeds that
                     serve every published release); implies --restore-released
  --restore-released first add the packages of every published GitHub release (other
                     than this version) that artifacts/ lacks, verified, so the feeds
                     keep them; exit 1 when one cannot be fetched or verified
  --base-url <url>   absolute site URL used by the NuGet feed
                     (default: $LG_SITE_URL or ${DEFAULT_SITE_URL})
  --root <dir>       repository root (default: the folder above scripts/)
  --out <dir>        public folder (default: <root>/website/public)
  --quiet            print errors only
  -h, --help         show this help

Environment: LG_VERSION (default ${DEFAULT_VERSION}), LG_SITE_URL, LG_NUGET_IDS, SOURCE_DATE_EPOCH`;

// -------------------------------------------------------------------------------------------
// Logging
// -------------------------------------------------------------------------------------------

let QUIET = false;

function info(message) {
  if (!QUIET) process.stdout.write(`${message}\n`);
}

function warn(message) {
  if (!QUIET) process.stderr.write(`warning: ${message}\n`);
}

function fail(message, code = 2) {
  process.stderr.write(`build-downloads: ${message}\n`);
  process.exit(code);
}

// -------------------------------------------------------------------------------------------
// Command line
// -------------------------------------------------------------------------------------------

function parseArgs(argv) {
  const opts = {
    strict: false,
    restoreReleased: false,
    quiet: false,
    help: false,
    baseUrl: process.env.LG_SITE_URL || DEFAULT_SITE_URL,
    root: null,
    out: null,
  };
  for (let i = 0; i < argv.length; i++) {
    const raw = argv[i];
    const eq = raw.startsWith("--") ? raw.indexOf("=") : -1;
    const flag = eq > 0 ? raw.slice(0, eq) : raw;
    const inline = eq > 0 ? raw.slice(eq + 1) : null;
    const value = () => {
      if (inline !== null) return inline;
      const next = argv[i + 1];
      if (next === undefined || next.startsWith("--")) fail(`${flag} needs a value\n\n${USAGE}`);
      i++;
      return next;
    };
    const toggle = () => {
      if (inline !== null) fail(`${flag} does not take a value`);
      return true;
    };
    switch (flag) {
      case "--strict":
        opts.strict = toggle();
        break;
      case "--restore-released":
        opts.restoreReleased = toggle();
        break;
      case "--quiet":
      case "-q":
        opts.quiet = toggle();
        break;
      case "--help":
      case "-h":
        opts.help = toggle();
        break;
      case "--base-url":
        opts.baseUrl = value();
        break;
      case "--root":
        opts.root = value();
        break;
      case "--out":
        opts.out = value();
        break;
      default:
        fail(`unknown argument: ${raw}\n\n${USAGE}`);
    }
  }
  return opts;
}

// "https://site.app/" -> "https://site.app" (a path prefix such as /docs is kept).
function normalizeBaseUrl(raw) {
  let url;
  try {
    url = new URL(raw);
  } catch {
    fail(`--base-url is not a valid URL: ${raw}`);
  }
  if (url.protocol !== "http:" && url.protocol !== "https:") fail(`--base-url must be http(s): ${raw}`);
  if (url.search || url.hash) fail(`--base-url must not have a query or fragment: ${raw}`);
  return url.href.replace(/\/+$/, "");
}

// -------------------------------------------------------------------------------------------
// Small helpers
// -------------------------------------------------------------------------------------------

const toPosix = (p) => p.split(path.sep).join("/");

// Byte-wise UTF-8 ordering, the same on every machine and locale.
function compareBytes(a, b) {
  return Buffer.compare(Buffer.from(a, "utf8"), Buffer.from(b, "utf8"));
}

function isInside(child, parent) {
  const rel = path.relative(parent, child);
  return rel === "" || (rel !== ".." && !rel.startsWith(`..${path.sep}`) && !path.isAbsolute(rel));
}

function lstatOrNull(p) {
  try {
    return fs.lstatSync(p);
  } catch (err) {
    if (err.code === "ENOENT" || err.code === "ENOTDIR") return null;
    throw err;
  }
}

const isExecutable = (stat) => (stat.mode & 0o111) !== 0;
const iso = (ms) => new Date(ms).toISOString();

// Decimal units, like the website (and Finder): 1 MB = 1,000,000 bytes.
function formatBytes(n) {
  if (n == null) return "-";
  if (n < 1000) return `${n} B`;
  const units = ["KB", "MB", "GB", "TB"];
  let v = n / 1000;
  let u = 0;
  while (v >= 999.95 && u < units.length - 1) {
    v /= 1000;
    u++;
  }
  return `${v.toFixed(1)} ${units[u]}`;
}

// Site-absolute URL of an output, e.g. "downloads/a b.zip" -> "/downloads/a%20b.zip".
const siteUrl = (rel) => `/${rel.split("/").map(encodeURIComponent).join("/")}`;

function display(ctx, abs) {
  const rel = path.relative(ctx.rootReal, abs);
  return rel && rel !== ".." && !rel.startsWith(`..${path.sep}`) && !path.isAbsolute(rel) ? toPosix(rel) : abs;
}

function json(value) {
  return `${JSON.stringify(value, null, 2)}\n`;
}

function xmlEscape(s) {
  return String(s).replace(/[<>&"']/g, (c) => ({ "<": "&lt;", ">": "&gt;", "&": "&amp;", '"': "&quot;", "'": "&apos;" })[c]);
}

// Version number embedded in an artifact file name ("studio-1.2.0-rc.1-macos.zip" -> "1.2.0-rc.1").
// Only common pre-release tags count, so "app-1.0.0-release.apk" yields "1.0.0".
function versionFromName(name) {
  const stem = name.replace(/(\.(xcframework|app))?\.(zip|apk|aar|nupkg|snupkg)$/i, "").replace(/\.(app|xcframework)$/i, "");
  const m =
    /(?:^|[^0-9A-Za-z.])v?(\d+\.\d+(?:\.\d+){0,2}(?:-(?:alpha|beta|rc|preview|pre|dev)(?:\.?\d+)*)?)(?=$|[^0-9A-Za-z])/i.exec(
      stem,
    );
  return m ? m[1] : null;
}

// Lower is better: release builds beat unnamed ones, which beat debug builds; unsigned last.
function buildRank(name) {
  const lower = name.toLowerCase();
  let rank = lower.includes("release") ? 0 : lower.includes("debug") ? 2 : 1;
  if (lower.includes("unsigned")) rank += 3;
  return rank;
}

// Case-insensitive "ios" that is not just the tail of a word ("studios"), or a literal "iOS".
const mentionsIos = (name) => /(^|[^a-z])ios/i.test(name) || name.includes("iOS");

// -------------------------------------------------------------------------------------------
// Hashing
// -------------------------------------------------------------------------------------------

const hashCache = new Map();

// Streams a file through the requested digests. Cached per (path, inode, size, mtime).
function hashFile(file, algos = ["sha256"]) {
  const st = fs.statSync(file);
  const key = `${file}\0${st.ino}\0${st.size}\0${st.mtimeMs}\0${algos.join(",")}`;
  const cached = hashCache.get(key);
  if (cached) return cached;
  const hashes = algos.map((a) => crypto.createHash(a));
  const buf = Buffer.allocUnsafe(1 << 20);
  const fd = fs.openSync(file, "r");
  let bytes = 0;
  try {
    for (;;) {
      const n = fs.readSync(fd, buf, 0, buf.length, null);
      if (n === 0) break;
      bytes += n;
      for (const h of hashes) h.update(buf.subarray(0, n));
    }
  } finally {
    fs.closeSync(fd);
  }
  const result = { bytes };
  algos.forEach((a, i) => (result[a] = hashes[i].digest("hex")));
  hashCache.set(key, result);
  return result;
}

function hashBuffer(data, algos = ["sha256"]) {
  const result = { bytes: data.length };
  for (const a of algos) result[a] = crypto.createHash(a).update(data).digest("hex");
  return result;
}

// -------------------------------------------------------------------------------------------
// Walking input folders without escaping them
// -------------------------------------------------------------------------------------------

// Direct children of an input folder (hidden files skipped). A symlink is followed only when
// its real target stays inside the folder's real path.
function listInputDir(ctx, dir) {
  let boundary;
  try {
    boundary = fs.realpathSync(dir);
  } catch {
    return [];
  }
  if (!fs.statSync(boundary).isDirectory()) return [];
  const entries = [];
  for (const name of fs.readdirSync(boundary).sort(compareBytes)) {
    if (SKIPPED_NAMES.has(name) || name.startsWith(".")) continue;
    const abs = path.join(boundary, name);
    const lst = fs.lstatSync(abs);
    let real = abs;
    let stat = lst;
    if (lst.isSymbolicLink()) {
      try {
        real = fs.realpathSync(abs);
      } catch {
        warn(`ignoring dangling symlink ${display(ctx, abs)}`);
        continue;
      }
      if (real === boundary || !isInside(real, boundary)) {
        warn(`ignoring ${display(ctx, abs)}: the symlink points outside ${display(ctx, boundary)}`);
        continue;
      }
      stat = fs.statSync(real);
    }
    if (!stat.isFile() && !stat.isDirectory()) continue;
    entries.push({ name, abs: real, stat, display: display(ctx, abs) });
  }
  return entries;
}

// Every entry below `top`, sorted by relative path. Symlinks whose target leaves `top` are
// skipped. mode "zip" keeps in-tree symlinks as links (app bundles and frameworks rely on
// them); mode "copy" dereferences links to files and skips links to folders.
function walkTree(ctx, top, mode) {
  const boundary = fs.realpathSync(top);
  const out = [];
  const visit = (dirAbs, relDir) => {
    for (const name of fs.readdirSync(dirAbs)) {
      if (SKIPPED_NAMES.has(name)) continue;
      const abs = path.join(dirAbs, name);
      const rel = relDir ? `${relDir}/${name}` : name;
      const lst = fs.lstatSync(abs);
      if (lst.isSymbolicLink()) {
        let real = null;
        try {
          real = fs.realpathSync(abs);
        } catch {
          // dangling link
        }
        if (!real || !isInside(real, boundary)) {
          warn(`skipping symlink ${display(ctx, abs)}: it does not resolve inside ${display(ctx, boundary)}`);
          continue;
        }
        const st = fs.statSync(real);
        if (mode === "zip") {
          let target = fs.readlinkSync(abs);
          if (path.isAbsolute(target)) target = path.relative(dirAbs, real) || ".";
          out.push({ rel, type: "symlink", target: toPosix(target), mtimeMs: lst.mtimeMs });
        } else if (st.isFile()) {
          out.push({ rel, type: "file", abs: real, size: st.size, mtimeMs: st.mtimeMs, executable: isExecutable(st) });
        } else {
          warn(`skipping symlinked folder ${display(ctx, abs)}`);
        }
        continue;
      }
      if (lst.isDirectory()) {
        out.push({ rel, type: "dir", mtimeMs: lst.mtimeMs });
        visit(abs, rel);
      } else if (lst.isFile()) {
        out.push({ rel, type: "file", abs, size: lst.size, mtimeMs: lst.mtimeMs, executable: isExecutable(lst) });
      }
      // sockets, FIFOs and devices are ignored
    }
  };
  visit(boundary, "");
  return out.sort((a, b) => compareBytes(a.rel, b.rel));
}

// Zip entries for a folder placed under `prefix/` (the prefix folder itself included).
function treeToZipEntries(ctx, dirAbs, prefix) {
  const entries = [{ name: `${prefix}/`, type: "dir" }];
  let newestMs = 0;
  for (const e of walkTree(ctx, dirAbs, "zip")) {
    if (e.type !== "dir") newestMs = Math.max(newestMs, e.mtimeMs);
    entries.push({
      name: `${prefix}/${e.rel}${e.type === "dir" ? "/" : ""}`,
      type: e.type,
      abs: e.abs,
      target: e.target,
      executable: e.executable,
    });
  }
  return { entries, newestMs: newestMs || fs.statSync(dirAbs).mtimeMs };
}

// -------------------------------------------------------------------------------------------
// Deterministic zip writer
// -------------------------------------------------------------------------------------------
// Entries sorted by path, fixed DOS timestamp 1980-01-01 00:00, no extra fields, UTF-8 names,
// Unix permissions in the external attributes (0644 files, 0755 folders and executables,
// symlinks stored as links), deflate level 9 unless storing is smaller. No zip64: inputs over
// 4 GiB or 65535 entries are rejected.

const DOS_TIME = 0; // 00:00:00
const DOS_DATE = (0 << 9) | (1 << 5) | 1; // 1980-01-01
const ZIP_UTF8 = 0x0800;
const ZIP_VERSION_NEEDED = 20; // 2.0: deflate and folders
const ZIP_VERSION_MADE_BY = (3 << 8) | 30; // host 3 = Unix, spec 3.0

let crcTable = null;
function crc32(data) {
  if (typeof zlib.crc32 === "function") return zlib.crc32(data) >>> 0;
  if (!crcTable) {
    crcTable = new Uint32Array(256);
    for (let n = 0; n < 256; n++) {
      let c = n;
      for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
      crcTable[n] = c >>> 0;
    }
  }
  let crc = 0xffffffff;
  for (let i = 0; i < data.length; i++) crc = crcTable[(crc ^ data[i]) & 0xff] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}

function writeAll(fd, buf) {
  let off = 0;
  while (off < buf.length) off += fs.writeSync(fd, buf, off, buf.length - off);
}

// entries: [{ name, type: "file"|"dir"|"symlink", abs?|data?, target?, executable? }]
function writeZip(ctx, dest, entries) {
  const sorted = [...entries].sort((a, b) => compareBytes(a.name, b.name));
  if (sorted.length > 0xffff) throw new Error(`too many zip entries (${sorted.length}); zip64 is not supported`);
  const tmp = tempPath(dest);
  const fd = fs.openSync(tmp, "w");
  const central = [];
  let offset = 0;
  const emit = (buf) => {
    writeAll(fd, buf);
    offset += buf.length;
  };
  try {
    for (const e of sorted) {
      const nameBuf = Buffer.from(e.name, "utf8");
      let data;
      let mode;
      if (e.type === "dir") {
        data = Buffer.alloc(0);
        mode = 0o040755;
      } else if (e.type === "symlink") {
        data = Buffer.from(e.target, "utf8");
        mode = 0o120755;
      } else {
        data = e.data ?? fs.readFileSync(e.abs);
        mode = e.executable ? 0o100755 : 0o100644;
      }
      if (data.length > 0xfffffffe) throw new Error(`${e.name} is larger than 4 GiB; zip64 is not supported`);
      const crc = crc32(data);
      let payload = data;
      let method = 0;
      if (e.type === "file" && data.length > 0) {
        const deflated = zlib.deflateRawSync(data, { level: 9 });
        if (deflated.length < data.length) {
          payload = deflated;
          method = 8;
        }
      }
      if (offset > 0xfffffffe) throw new Error("zip larger than 4 GiB; zip64 is not supported");
      const local = Buffer.alloc(30);
      local.writeUInt32LE(0x04034b50, 0);
      local.writeUInt16LE(ZIP_VERSION_NEEDED, 4);
      local.writeUInt16LE(ZIP_UTF8, 6);
      local.writeUInt16LE(method, 8);
      local.writeUInt16LE(DOS_TIME, 10);
      local.writeUInt16LE(DOS_DATE, 12);
      local.writeUInt32LE(crc, 14);
      local.writeUInt32LE(payload.length, 18);
      local.writeUInt32LE(data.length, 22);
      local.writeUInt16LE(nameBuf.length, 26);
      local.writeUInt16LE(0, 28);
      central.push({ nameBuf, method, crc, csize: payload.length, usize: data.length, mode, isDir: e.type === "dir", offset });
      emit(local);
      emit(nameBuf);
      emit(payload);
    }
    const cdStart = offset;
    for (const c of central) {
      const h = Buffer.alloc(46);
      h.writeUInt32LE(0x02014b50, 0);
      h.writeUInt16LE(ZIP_VERSION_MADE_BY, 4);
      h.writeUInt16LE(ZIP_VERSION_NEEDED, 6);
      h.writeUInt16LE(ZIP_UTF8, 8);
      h.writeUInt16LE(c.method, 10);
      h.writeUInt16LE(DOS_TIME, 12);
      h.writeUInt16LE(DOS_DATE, 14);
      h.writeUInt32LE(c.crc, 16);
      h.writeUInt32LE(c.csize, 20);
      h.writeUInt32LE(c.usize, 24);
      h.writeUInt16LE(c.nameBuf.length, 28);
      h.writeUInt16LE(0, 30); // extra field length
      h.writeUInt16LE(0, 32); // comment length
      h.writeUInt16LE(0, 34); // disk number
      h.writeUInt16LE(0, 36); // internal attributes
      h.writeUInt32LE(((c.mode << 16) | (c.isDir ? 0x10 : 0)) >>> 0, 38); // Unix mode + MS-DOS folder bit
      h.writeUInt32LE(c.offset, 42);
      emit(h);
      emit(c.nameBuf);
    }
    const cdSize = offset - cdStart;
    if (offset > 0xfffffffe) throw new Error("zip larger than 4 GiB; zip64 is not supported");
    const end = Buffer.alloc(22);
    end.writeUInt32LE(0x06054b50, 0);
    end.writeUInt16LE(0, 4);
    end.writeUInt16LE(0, 6);
    end.writeUInt16LE(central.length, 8);
    end.writeUInt16LE(central.length, 10);
    end.writeUInt32LE(cdSize, 12);
    end.writeUInt32LE(cdStart, 16);
    end.writeUInt16LE(0, 20);
    emit(end);
  } catch (err) {
    fs.closeSync(fd);
    fs.rmSync(tmp, { force: true });
    throw err;
  }
  fs.closeSync(fd);
  return commitTemp(ctx, tmp, dest);
}

// -------------------------------------------------------------------------------------------
// Minimal zip reader (used to pull the .nuspec out of a .nupkg)
// -------------------------------------------------------------------------------------------

function openZip(file) {
  const buf = fs.readFileSync(file);
  let eocd = -1;
  for (let i = buf.length - 22; i >= Math.max(0, buf.length - 22 - 0xffff); i--) {
    if (buf.readUInt32LE(i) === 0x06054b50) {
      eocd = i;
      break;
    }
  }
  if (eocd < 0) throw new Error("not a zip file (no end of central directory record)");
  let count = buf.readUInt16LE(eocd + 10);
  let cdOffset = buf.readUInt32LE(eocd + 16);
  if (count === 0xffff || cdOffset === 0xffffffff) {
    // zip64: the locator sits right before the classic end record
    const loc = eocd - 20;
    if (loc < 0 || buf.readUInt32LE(loc) !== 0x07064b50) throw new Error("broken zip64 end record");
    const z64 = Number(buf.readBigUInt64LE(loc + 8));
    if (buf.readUInt32LE(z64) !== 0x06064b50) throw new Error("broken zip64 end record");
    count = Number(buf.readBigUInt64LE(z64 + 32));
    cdOffset = Number(buf.readBigUInt64LE(z64 + 48));
  }
  const entries = [];
  let p = cdOffset;
  for (let i = 0; i < count; i++) {
    if (p + 46 > buf.length || buf.readUInt32LE(p) !== 0x02014b50) throw new Error("corrupt central directory");
    const flags = buf.readUInt16LE(p + 8);
    const method = buf.readUInt16LE(p + 10);
    const crc = buf.readUInt32LE(p + 16);
    let csize = buf.readUInt32LE(p + 20);
    let usize = buf.readUInt32LE(p + 24);
    const nameLen = buf.readUInt16LE(p + 28);
    const extraLen = buf.readUInt16LE(p + 30);
    const commentLen = buf.readUInt16LE(p + 32);
    let localOffset = buf.readUInt32LE(p + 42);
    const name = buf.toString(flags & ZIP_UTF8 ? "utf8" : "latin1", p + 46, p + 46 + nameLen);
    // zip64 extended information (0x0001) carries the real sizes and offset
    let x = p + 46 + nameLen;
    const xEnd = x + extraLen;
    while (x + 4 <= xEnd) {
      const id = buf.readUInt16LE(x);
      const size = buf.readUInt16LE(x + 2);
      if (id === 0x0001) {
        let q = x + 4;
        if (usize === 0xffffffff) (usize = Number(buf.readBigUInt64LE(q))), (q += 8);
        if (csize === 0xffffffff) (csize = Number(buf.readBigUInt64LE(q))), (q += 8);
        if (localOffset === 0xffffffff) localOffset = Number(buf.readBigUInt64LE(q));
      }
      x += 4 + size;
    }
    entries.push({ name, flags, method, crc, csize, usize, localOffset });
    p = xEnd + commentLen;
  }
  const read = (entry) => {
    if (entry.flags & 1) throw new Error(`${entry.name} is encrypted`);
    const lh = entry.localOffset;
    if (buf.readUInt32LE(lh) !== 0x04034b50) throw new Error(`bad local header for ${entry.name}`);
    const start = lh + 30 + buf.readUInt16LE(lh + 26) + buf.readUInt16LE(lh + 28);
    const raw = buf.subarray(start, start + entry.csize);
    let data;
    if (entry.method === 0) data = Buffer.from(raw);
    else if (entry.method === 8) data = zlib.inflateRawSync(raw);
    else throw new Error(`${entry.name} uses unsupported compression method ${entry.method}`);
    if (crc32(data) !== entry.crc) throw new Error(`CRC mismatch in ${entry.name}`);
    return data;
  };
  return { entries, read };
}

// -------------------------------------------------------------------------------------------
// Writing outputs (only when bytes change, always via a temp file + rename)
// -------------------------------------------------------------------------------------------

const tempPath = (dest) => `${dest}.tmp-${process.pid}`;

function sameFile(dest, digest) {
  const st = lstatOrNull(dest);
  return Boolean(st && st.isFile() && st.size === digest.bytes && hashFile(dest).sha256 === digest.sha256);
}

// Moves a finished temp file into place unless dest already holds the same bytes.
function commitTemp(ctx, tmp, dest) {
  const digest = hashFile(tmp);
  if (sameFile(dest, digest)) {
    fs.rmSync(tmp, { force: true });
    ctx.stats.unchanged++;
  } else {
    fs.renameSync(tmp, dest);
    ctx.stats.written++;
  }
  return { bytes: digest.bytes, sha256: digest.sha256 };
}

function writeBufferIfChanged(ctx, dest, data) {
  const digest = hashBuffer(data);
  const st = lstatOrNull(dest);
  if (st && st.isFile() && st.size === data.length && fs.readFileSync(dest).equals(data)) {
    ctx.stats.unchanged++;
  } else {
    const tmp = tempPath(dest);
    fs.writeFileSync(tmp, data);
    fs.renameSync(tmp, dest);
    ctx.stats.written++;
  }
  return { bytes: digest.bytes, sha256: digest.sha256 };
}

function copyIfChanged(ctx, src, dest) {
  const digest = hashFile(src);
  if (sameFile(dest, digest)) {
    ctx.stats.unchanged++;
    return { bytes: digest.bytes, sha256: digest.sha256 };
  }
  const tmp = tempPath(dest);
  try {
    fs.copyFileSync(src, tmp);
    fs.chmodSync(tmp, 0o644);
  } catch (err) {
    fs.rmSync(tmp, { force: true });
    throw err;
  }
  return commitTemp(ctx, tmp, dest); // hashes the copy, so the manifest matches what is served
}

function gitArchive(ctx, dest, job) {
  const tmp = tempPath(dest);
  try {
    execFileSync("git", ["-C", ctx.rootReal, "archive", "--format=zip", `--prefix=${job.prefix}`, `--output=${tmp}`, job.commit], {
      stdio: ["ignore", "ignore", "pipe"],
    });
  } catch (err) {
    fs.rmSync(tmp, { force: true });
    throw new Error(`git archive failed: ${String(err.stderr || err.message).trim()}`);
  }
  return commitTemp(ctx, tmp, dest);
}

// -------------------------------------------------------------------------------------------
// Planning: every output is registered before anything is cleaned or written
// -------------------------------------------------------------------------------------------

// job kinds: copy { src } | buffer { data } | zip { entries } | git-archive { prefix, commit }
// | final (manifest and SHA256SUMS, written last)
function planOutput(ctx, rel, job) {
  if (ctx.plan.has(rel)) {
    warn(`two inputs map to ${rel}; keeping the first`);
    return false;
  }
  ctx.plan.set(rel, job);
  return true;
}

const outPath = (ctx, rel) => path.join(ctx.out, ...rel.split("/"));

// Adds a manifest item. `outRel` is the planned output (null when the input is missing).
function addItem(ctx, spec) {
  ctx.items.push({ extra: {}, outRel: null, sourceTimeMs: null, ...spec });
}

// Picks one file when several match a slot: files beat folders, then `rank`, then release
// beats debug, then the newest, then the name. The others are reported.
/** A download file name safe for URLs: spaces and unusual characters become dashes. */
function safeName(name) {
  return name.replace(/[^A-Za-z0-9._+-]+/g, "-");
}

function pickBest(list, label, rank = () => 0) {
  if (list.length === 0) return null;
  const ranked = [...list].sort(
    (a, b) =>
      (a.stat.isFile() ? 0 : 1) - (b.stat.isFile() ? 0 : 1) ||
      rank(a) - rank(b) ||
      buildRank(a.name) - buildRank(b.name) ||
      b.stat.mtimeMs - a.stat.mtimeMs ||
      compareBytes(a.name, b.name),
  );
  for (const other of ranked.slice(1)) warn(`${label}: using ${ranked[0].display}, ignoring ${other.display}`);
  return ranked[0];
}

// Plans one downloadable file: a copy for files, a deterministic zip for folders (.app, .xcframework).
function planDownload(ctx, entry, outRel) {
  if (entry.stat.isFile()) {
    if (!planOutput(ctx, outRel, { kind: "copy", src: entry.abs })) return null;
    return { outRel, sourceTimeMs: entry.stat.mtimeMs, input: entry.display };
  }
  const { entries, newestMs } = treeToZipEntries(ctx, entry.abs, entry.name);
  if (!planOutput(ctx, outRel, { kind: "zip", entries })) return null;
  return { outRel, sourceTimeMs: newestMs, input: `${entry.display}/ (zipped)` };
}

// -------------------------------------------------------------------------------------------
// Apple: artifacts/apple/
// -------------------------------------------------------------------------------------------

// AppleDouble files ("._name", extended attributes and resource forks) and a __MACOSX folder in a
// zip. Archive Utility and ditto fold them back into attributes; unzip and other tools write them
// out as real files, inside an app bundle too, where they break the code signature seal.
function appleDoubleEntries(zipFile) {
  try {
    return openZip(zipFile)
      .entries.map((e) => e.name)
      .filter((name) => /(^|\/)\._[^/]+$/.test(name) || /^__MACOSX(\/|$)/.test(name));
  } catch (err) {
    warn(`could not read ${zipFile}: ${err.message}`);
    return [];
  }
}

// iOS app bundles are flat (Info.plist at the top); macOS bundles have Contents/.
function isIosAppBundle(entry) {
  if (mentionsIos(entry.name)) return true;
  return fs.existsSync(path.join(entry.abs, "Info.plist")) && !fs.existsSync(path.join(entry.abs, "Contents"));
}

function collectApple(ctx) {
  const found = { mac: [], ios: [], xcf: [] };
  for (const entry of listInputDir(ctx, path.join(ctx.artifacts, "apple"))) {
    const lower = entry.name.toLowerCase();
    const zip = entry.stat.isFile() && lower.endsWith(".zip");
    const dir = entry.stat.isDirectory();
    if (zip && lower.endsWith(".xcframework.zip")) found.xcf.push(entry);
    else if (zip && mentionsIos(entry.name) && (lower.includes("simulator") || lower.includes("studio"))) found.ios.push(entry);
    else if (zip && !mentionsIos(entry.name) && (lower.includes("studio") || lower.endsWith(".app.zip"))) found.mac.push(entry);
    else if (dir && lower.endsWith(".xcframework")) found.xcf.push(entry);
    else if (dir && lower.endsWith(".app")) (isIosAppBundle(entry) ? found.ios : found.mac).push(entry);
    // platforms/apple/scripts/package-macos.sh writes README.txt next to the macOS zip: the first-launch
    // steps, which website/scripts/content.mjs turns into the download's note. It is not published itself.
    else if (entry.stat.isFile() && lower === "readme.txt") continue;
    else warn(`ignoring unrecognised Apple artifact ${entry.display}`);
  }

  const slots = [
    {
      id: "macos-studio",
      name: "Liquid Glass Studio for macOS",
      fileName: "liquid-glass-studio-macos.zip",
      required: true,
      requirements: REQUIREMENTS.macos,
      kind: "app",
      candidates: found.mac,
      hint: "artifacts/apple/*studio*macos*.zip, *studio*.zip or a .app folder",
    },
    {
      id: "ios-simulator-studio",
      name: "Liquid Glass Studio for iOS Simulator",
      fileName: "liquid-glass-studio-ios-simulator.zip",
      required: false,
      requirements: REQUIREMENTS.iosSimulator,
      kind: "app",
      candidates: found.ios,
      hint: "artifacts/apple/*ios*simulator*.zip or *studio*ios*.zip",
    },
    {
      id: "apple-xcframework",
      name: "LiquidGlass XCFramework (iOS and macOS)",
      fileName: "liquid-glass-apple-xcframework.zip",
      required: false,
      requirements: REQUIREMENTS.xcframework,
      kind: "library",
      candidates: found.xcf,
      hint: "artifacts/apple/*.xcframework.zip",
    },
  ];
  for (const slot of slots) {
    const entry = pickBest(slot.candidates, slot.id);
    if (entry?.stat.isFile() && slot.kind === "app") {
      const junk = appleDoubleEntries(entry.abs);
      if (junk.length) {
        const problem = `${entry.display} holds ${junk.length} AppleDouble entries (${junk[0]}${junk.length > 1 ? ", ..." : ""}): unzip writes them into the app bundle and breaks its signature. Zip it with ditto -c -k --norsrc --noextattr --noqtn --keepParent`;
        warn(problem);
        ctx.zipProblems.push(problem);
      }
    }
    const planned = entry ? planDownload(ctx, entry, `downloads/${slot.fileName}`) : null;
    addItem(ctx, {
      id: slot.id,
      name: slot.name,
      platform: "apple",
      kind: slot.kind,
      fileName: slot.fileName,
      version: (entry && versionFromName(entry.name)) || ctx.version,
      requirements: slot.requirements,
      required: slot.required,
      hint: slot.hint,
      ...planned,
    });
  }
}

// -------------------------------------------------------------------------------------------
// Android: artifacts/android/ (APK, AAR, Maven repository)
// -------------------------------------------------------------------------------------------

function collectAndroid(ctx) {
  const apks = [];
  const aars = [];
  let mavenDir = null;
  for (const entry of listInputDir(ctx, path.join(ctx.artifacts, "android"))) {
    const lower = entry.name.toLowerCase();
    if (entry.stat.isFile() && lower.endsWith(".apk")) apks.push(entry);
    else if (entry.stat.isFile() && lower.endsWith(".aar")) aars.push(entry);
    else if (entry.stat.isDirectory() && entry.name === "maven") mavenDir = entry;
    else warn(`ignoring unrecognised Android artifact ${entry.display}`);
  }

  // Studio APK
  const apk = pickBest(apks, "android-studio");
  if (apk && apk.name.toLowerCase().includes("unsigned")) warn(`${apk.display} looks unsigned; it will not install`);
  addItem(ctx, {
    id: "android-studio",
    name: "Liquid Glass Studio for Android",
    platform: "android",
    kind: "app",
    fileName: "liquid-glass-studio-android.apk",
    version: (apk && versionFromName(apk.name)) || ctx.version,
    requirements: REQUIREMENTS.androidApp,
    required: true,
    hint: "artifacts/android/*.apk",
    ...(apk ? planDownload(ctx, apk, "downloads/liquid-glass-studio-android.apk") : null),
  });

  // Maven repository is planned first: it can supply the AAR's version (and the AAR itself).
  const maven = collectMaven(ctx, mavenDir);

  // The newest version wins: an earlier release's AAR may still lie next to this one.
  const byVersion = [...aars].sort((a, b) => compareVersionStrings(versionFromName(b.name) ?? "", versionFromName(a.name) ?? ""));
  let aar = pickBest(aars, "android-aar", (e) => byVersion.indexOf(e));
  if (!aar && maven.aar) aar = maven.aar;
  const aarVersion = (aar && versionFromName(aar.name)) || maven.latest || ctx.version;
  const aarFile = `liquid-glass-compose-${aarVersion}.aar`;
  addItem(ctx, {
    id: "android-aar",
    name: "Liquid Glass for Jetpack Compose (AAR)",
    platform: "android",
    kind: "library",
    fileName: aarFile,
    version: aarVersion,
    requirements: REQUIREMENTS.androidLibrary,
    required: true,
    hint: "artifacts/android/*.aar",
    ...(aar ? planDownload(ctx, aar, `downloads/${aarFile}`) : null),
  });

  const mavenVersion = maven.latest || ctx.version;
  addItem(ctx, {
    id: "android-maven",
    name: "Liquid Glass for Jetpack Compose (Maven repository)",
    platform: "android",
    kind: "repository",
    fileName: null,
    version: mavenVersion,
    requirements: REQUIREMENTS.androidLibrary,
    required: true,
    hint: `artifacts/android/maven/${MAVEN_GROUP_PATH}/${MAVEN_ARTIFACT}/<version>/${MAVEN_ARTIFACT}-<version>.pom`,
    repository: maven.latest ? { rels: maven.rels, url: "/maven/" } : null,
    sourceTimeMs: maven.newestMs || null,
    input: maven.latest ? `${maven.display}/ (${maven.rels.length} files)` : undefined,
    extra: { coordinates: `${MAVEN_GROUP_PATH.replace(/\//g, ".")}:${MAVEN_ARTIFACT}:${mavenVersion}` },
  });
}

function formatMavenTimestamp(ms) {
  return iso(ms).replace(/[-:T]/g, "").slice(0, 14); // yyyyMMddHHmmss, UTC
}

function compareVersionStrings(a, b) {
  const pa = parseNuGetVersion(a);
  const pb = parseNuGetVersion(b);
  if (pa && pb) return compareNuGetVersions(pa, pb) || compareBytes(a, b);
  if (pa || pb) return pa ? 1 : -1;
  return compareBytes(a, b);
}

function mavenMetadataXml(groupId, artifactId, versions, lastUpdated) {
  const sorted = [...versions].sort(compareVersionStrings);
  const releases = sorted.filter((v) => !/-SNAPSHOT$/i.test(v));
  const lines = [
    `<?xml version="1.0" encoding="UTF-8"?>`,
    `<metadata>`,
    `  <groupId>${xmlEscape(groupId)}</groupId>`,
    `  <artifactId>${xmlEscape(artifactId)}</artifactId>`,
    `  <versioning>`,
    `    <latest>${xmlEscape(sorted.at(-1))}</latest>`,
  ];
  if (releases.length) lines.push(`    <release>${xmlEscape(releases.at(-1))}</release>`);
  lines.push(`    <versions>`);
  for (const v of sorted) lines.push(`      <version>${xmlEscape(v)}</version>`);
  lines.push(`    </versions>`, `    <lastUpdated>${lastUpdated}</lastUpdated>`, `  </versioning>`, `</metadata>`, ``);
  return lines.join("\n");
}

// Plans maven/** : verbatim copies, plus maven-metadata.xml and checksum files where missing.
function collectMaven(ctx, dirEntry) {
  const result = { rels: [], latest: null, aar: null, newestMs: 0, display: dirEntry?.display };
  if (!dirEntry) return result;

  const inputs = new Map(); // repository-relative path -> walk entry
  for (const f of walkTree(ctx, dirEntry.abs, "copy")) {
    if (f.type === "file" && !MAVEN_LOCAL_JUNK.test(f.rel)) inputs.set(f.rel, f);
  }

  // Artifact folders: <group path>/<artifactId>/<version>/<artifactId>-<version>[...].pom
  const artifacts = new Map();
  for (const [rel, f] of inputs) {
    if (!rel.endsWith(".pom")) continue;
    const parts = rel.split("/");
    if (parts.length < 4) continue;
    const [artifactId, version, file] = parts.slice(-3);
    if (!file.startsWith(`${artifactId}-`)) continue;
    const key = parts.slice(0, -2).join("/");
    const artifact = artifacts.get(key) ?? { groupId: parts.slice(0, -3).join("."), artifactId, poms: new Map() };
    artifact.poms.set(version, Math.max(artifact.poms.get(version) ?? 0, f.mtimeMs));
    artifacts.set(key, artifact);
  }

  // Each artifact's maven-metadata.xml is written from the versions in the tree, never copied: the one Gradle
  // writes lists only the versions it found in its repository folder (in a fresh clone, just the one it built),
  // and versions restored next to it would be missing from it. A version an input file lists without a pom here
  // is reported, since the feed would drop it.
  const generated = new Map(); // rel -> Buffer
  for (const [key, artifact] of artifacts) {
    const rel = `${key}/maven-metadata.xml`;
    const input = inputs.get(rel);
    if (input) {
      const text = fs.readFileSync(input.abs, "utf8");
      const listed = [...(/<versions>([\s\S]*?)<\/versions>/.exec(text)?.[1] ?? "").matchAll(/<version>\s*([^<\s]+)\s*<\/version>/g)].map((m) => m[1]);
      for (const v of listed) {
        if (!artifact.poms.has(v)) warn(`${dirEntry.display}/${rel} lists ${v}, which has no pom in the tree: the feed leaves it out (--restore-released restores released versions)`);
      }
    }
    for (const r of [rel, ...MAVEN_ALGOS.map((algo) => `${rel}.${algo}`)]) inputs.delete(r);
    const newestPomMs = Math.max(...artifact.poms.values());
    const xml = mavenMetadataXml(artifact.groupId, artifact.artifactId, [...artifact.poms.keys()], formatMavenTimestamp(newestPomMs));
    generated.set(rel, Buffer.from(xml, "utf8"));
  }

  // Every non-checksum file gets .md5/.sha1/.sha256/.sha512 unless the tree already has them.
  const subjects = [...inputs.keys(), ...generated.keys()].filter((rel) => !MAVEN_CHECKSUM_FILE.test(rel)).sort(compareBytes);
  for (const rel of subjects) {
    let digests = null;
    const digest = () => (digests ??= generated.has(rel) ? hashBuffer(generated.get(rel), MAVEN_ALGOS) : hashFile(inputs.get(rel).abs, MAVEN_ALGOS));
    for (const algo of MAVEN_ALGOS) {
      const sumRel = `${rel}.${algo}`;
      const existing = inputs.get(sumRel);
      if (existing) {
        const declared = fs.readFileSync(existing.abs, "utf8").trim().split(/\s+/)[0].toLowerCase();
        if (declared !== digest()[algo]) warn(`checksum mismatch in the Maven tree: ${dirEntry.display}/${sumRel}`);
        continue;
      }
      generated.set(sumRel, Buffer.from(digest()[algo], "utf8"));
    }
  }

  for (const [rel, f] of [...inputs].sort((a, b) => compareBytes(a[0], b[0]))) {
    if (planOutput(ctx, `maven/${rel}`, { kind: "copy", src: f.abs })) result.rels.push(`maven/${rel}`);
    result.newestMs = Math.max(result.newestMs, f.mtimeMs);
  }
  for (const [rel, data] of [...generated].sort((a, b) => compareBytes(a[0], b[0]))) {
    if (planOutput(ctx, `maven/${rel}`, { kind: "buffer", data })) result.rels.push(`maven/${rel}`);
  }

  // The Compose library must be there with a pom for the item to count as available.
  const key = `${MAVEN_GROUP_PATH}/${MAVEN_ARTIFACT}`;
  const versions = [...(artifacts.get(key)?.poms.keys() ?? [])]
    .filter((v) => inputs.has(`${key}/${v}/${MAVEN_ARTIFACT}-${v}.pom`))
    .sort(compareVersionStrings);
  ctx.mavenVersions = versions;
  result.latest = versions.at(-1) ?? null;
  if (result.latest) {
    const aar = inputs.get(`${key}/${result.latest}/${MAVEN_ARTIFACT}-${result.latest}.aar`);
    if (aar) {
      const stat = fs.statSync(aar.abs);
      result.aar = { name: path.basename(aar.rel), abs: aar.abs, stat, display: `${dirEntry.display}/${aar.rel}` };
    }
  } else if (inputs.size) {
    warn(`${dirEntry.display} has no ${key}/<version>/${MAVEN_ARTIFACT}-<version>.pom`);
  }
  return result;
}

// -------------------------------------------------------------------------------------------
// NuGet: versions and nuspec parsing
// -------------------------------------------------------------------------------------------

// NuGet version parse + normalisation: leading zeros dropped, at least three parts, a zero
// fourth part dropped, build metadata kept separately.
function parseNuGetVersion(raw) {
  const m =
    /^\s*(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\s*$/.exec(
      String(raw ?? ""),
    );
  if (!m) return null;
  const parts = [m[1], m[2], m[3], m[4]].map((p) => (p === undefined ? 0 : Number(p)));
  const release = parts.slice(0, 3).join(".") + (parts[3] ? `.${parts[3]}` : "");
  const pre = m[5] ?? "";
  const normalized = release + (pre ? `-${pre}` : "");
  return { parts, pre, normalized, full: normalized + (m[6] ? `+${m[6]}` : "") };
}

// SemVer 2 precedence with NuGet's case-insensitive pre-release labels.
function compareNuGetVersions(a, b) {
  for (let i = 0; i < 4; i++) if (a.parts[i] !== b.parts[i]) return a.parts[i] - b.parts[i];
  if (!a.pre && !b.pre) return 0;
  if (!a.pre) return 1;
  if (!b.pre) return -1;
  const as = a.pre.split(".");
  const bs = b.pre.split(".");
  for (let i = 0; i < Math.max(as.length, bs.length); i++) {
    if (i >= as.length) return -1;
    if (i >= bs.length) return 1;
    const x = as[i];
    const y = bs[i];
    const xn = /^\d+$/.test(x);
    const yn = /^\d+$/.test(y);
    if (xn && yn) {
      if (Number(x) !== Number(y)) return Number(x) - Number(y);
      continue;
    }
    if (xn !== yn) return xn ? -1 : 1;
    const xl = x.toLowerCase();
    const yl = y.toLowerCase();
    if (xl !== yl) return xl < yl ? -1 : 1;
  }
  return 0;
}

// "1.0.0" in a nuspec means ">= 1.0.0"; feeds publish it as the range "[1.0.0, )".
function normalizeRange(raw) {
  const s = String(raw ?? "").trim();
  if (!s) return "(, )";
  if (/^[[(]/.test(s)) return s;
  const v = parseNuGetVersion(s);
  return v ? `[${v.normalized}, )` : s;
}

function decodeXml(s) {
  return String(s).replace(/<!\[CDATA\[([\s\S]*?)\]\]>|&(#x[0-9a-f]+|#\d+|lt|gt|amp|quot|apos);/gi, (match, cdata, ent) => {
    if (cdata !== undefined) return cdata;
    const e = ent.toLowerCase();
    if (e.startsWith("#x")) return String.fromCodePoint(parseInt(e.slice(2), 16));
    if (e.startsWith("#")) return String.fromCodePoint(parseInt(e.slice(1), 10));
    return { lt: "<", gt: ">", amp: "&", quot: '"', apos: "'" }[e];
  });
}

function parseXmlAttributes(s) {
  const attrs = {};
  for (const m of String(s ?? "").matchAll(/([\w:.-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g)) {
    attrs[m[1].replace(/^[\w.-]+:/, "")] = decodeXml(m[2] ?? m[3]);
  }
  return attrs;
}

const NS = "(?:[\\w.-]+:)?"; // optional XML namespace prefix

function parseDependencies(xml) {
  const deps = [];
  for (const m of xml.matchAll(new RegExp(`<${NS}dependency\\b([^>]*?)\\/?>`, "g"))) {
    const a = parseXmlAttributes(m[1]);
    if (a.id) deps.push({ id: a.id, range: normalizeRange(a.version) });
  }
  return deps;
}

// Reads the metadata the feed needs from a .nuspec with careful regexes (no XML parser).
function parseNuspec(xmlText) {
  const xml = xmlText.replace(/^\uFEFF/, "").replace(/<!--[\s\S]*?-->/g, "");
  const meta = new RegExp(`<${NS}metadata\\b([^>]*)>([\\s\\S]*?)<\\/${NS}metadata\\s*>`).exec(xml);
  if (!meta) throw new Error("the .nuspec has no <metadata> element");
  const metaAttrs = parseXmlAttributes(meta[1]);
  const body = meta[2];
  // Simple fields are looked up with the nested blocks removed, so <dependency id> etc. never match.
  const simple = body.replace(
    new RegExp(
      `<(${NS}(?:dependencies|frameworkAssemblies|frameworkReferences|references|contentFiles|packageTypes|files|repository))\\b[^>]*?(?:\\/>|>[\\s\\S]*?<\\/\\1\\s*>)`,
      "g",
    ),
    "",
  );
  const field = (name) => {
    const m = new RegExp(`<${NS}${name}(\\s[^>]*?)?(?:\\/>|>([\\s\\S]*?)<\\/${NS}${name}\\s*>)`).exec(simple);
    return m ? { attrs: parseXmlAttributes(m[1]), text: decodeXml(m[2] ?? "").trim() } : null;
  };
  const text = (name) => field(name)?.text ?? "";

  const dependencyGroups = [];
  const deps = new RegExp(`<${NS}dependencies\\b[^>]*?(?:\\/>|>([\\s\\S]*?)<\\/${NS}dependencies\\s*>)`).exec(body);
  if (deps?.[1]) {
    let grouped = false;
    for (const g of deps[1].matchAll(new RegExp(`<${NS}group\\b([^>]*?)(?:\\/>|>([\\s\\S]*?)<\\/${NS}group\\s*>)`, "g"))) {
      grouped = true;
      dependencyGroups.push({ targetFramework: parseXmlAttributes(g[1]).targetFramework ?? "", dependencies: parseDependencies(g[2] ?? "") });
    }
    if (!grouped) {
      const flat = parseDependencies(deps[1]);
      if (flat.length) dependencyGroups.push({ targetFramework: "", dependencies: flat });
    }
  }
  const packageTypes = [...body.matchAll(new RegExp(`<${NS}packageType\\b([^>]*?)\\/?>`, "g"))]
    .map((m) => parseXmlAttributes(m[1]).name)
    .filter(Boolean);
  const license = field("license");

  return {
    id: text("id"),
    version: text("version"),
    title: text("title"),
    authors: text("authors"),
    owners: text("owners"),
    description: text("description"),
    summary: text("summary"),
    tags: text("tags").split(/[\s,;]+/).filter(Boolean),
    projectUrl: text("projectUrl"),
    iconUrl: text("iconUrl"),
    licenseUrl: text("licenseUrl"),
    licenseExpression: license && license.attrs.type === "expression" ? license.text : "",
    language: text("language"),
    requireLicenseAcceptance: text("requireLicenseAcceptance").toLowerCase() === "true",
    minClientVersion: metaAttrs.minClientVersion ?? "",
    dependencyGroups,
    packageTypes,
  };
}

// -------------------------------------------------------------------------------------------
// Windows: artifacts/windows/ (Studio zip, NuGet packages -> downloads + static v3 feed)
// -------------------------------------------------------------------------------------------

function collectWindows(ctx) {
  const zips = [];
  let nugetDir = null;
  for (const entry of listInputDir(ctx, path.join(ctx.artifacts, "windows"))) {
    if (entry.stat.isFile() && entry.name.toLowerCase().endsWith(".zip")) zips.push(entry);
    else if (entry.stat.isDirectory() && entry.name === "nuget") nugetDir = entry;
    else warn(`ignoring unrecognised Windows artifact ${entry.display}`);
  }
  // Studio builds: x64 (framework-dependent, required), x64 self-contained and
  // ARM64. Original file names are kept, since docs/windows.md refers to them.
  const variants = [
    {
      id: "windows-studio",
      name: "Liquid Glass Studio for Windows (x64)",
      match: (n) => !/arm64/.test(n) && !/self-?contained/.test(n),
      fallbackName: "LiquidGlassStudio-win-x64.zip",
      requirements: "Windows 10 1809 or later, x64; .NET 10 Desktop Runtime",
      required: true,
    },
    {
      id: "windows-studio-self-contained",
      name: "Liquid Glass Studio for Windows (x64, self-contained)",
      match: (n) => !/arm64/.test(n) && /self-?contained/.test(n),
      fallbackName: "LiquidGlassStudio-win-x64-self-contained.zip",
      requirements: "Windows 10 1809 or later, x64; no .NET install needed",
      required: false,
    },
    {
      id: "windows-studio-arm64",
      name: "Liquid Glass Studio for Windows (ARM64)",
      match: (n) => /arm64/.test(n),
      fallbackName: "LiquidGlassStudio-win-arm64.zip",
      requirements: "Windows 11 on ARM64; .NET 10 Desktop Runtime",
      required: false,
    },
  ];
  for (const v of variants) {
    const candidates = zips.filter((e) => v.match(e.name.toLowerCase()));
    const studio = pickBest(candidates, v.id, (e) => (e.name.toLowerCase().includes("studio") ? 0 : 1));
    const fileName = studio ? safeName(studio.name) : v.fallbackName;
    // Whether the app carries an Authenticode signature: the website tells users about
    // SmartScreen when it does not.
    const signed = studio?.stat.isFile() ? zippedExeSigned(studio.abs) : null;
    addItem(ctx, {
      id: v.id,
      name: v.name,
      platform: "windows",
      kind: "app",
      fileName,
      version: (studio && versionFromName(studio.name)) || ctx.version,
      requirements: v.requirements,
      required: v.required,
      hint: `artifacts/windows/${v.fallbackName}`,
      ...(studio ? planDownload(ctx, studio, `downloads/${fileName}`) : null),
      extra: signed === null ? {} : { signed },
    });
  }
  collectNuGet(ctx, nugetDir);
}

// True when the PE image has a certificate table (an Authenticode signature), false when it
// has none, null when the bytes are not a PE image.
function peSigned(data) {
  if (data.length < 0x40 || data.readUInt16LE(0) !== 0x5a4d) return null; // "MZ"
  const pe = data.readUInt32LE(0x3c);
  if (pe + 26 > data.length || data.readUInt32LE(pe) !== 0x00004550) return null; // "PE\0\0"
  const optional = pe + 24;
  const magic = data.readUInt16LE(optional);
  const dirs = magic === 0x20b ? optional + 112 : magic === 0x10b ? optional + 96 : -1; // PE32+ / PE32
  if (dirs < 0 || dirs > data.length) return null;
  const count = data.readUInt32LE(dirs - 4); // NumberOfRvaAndSizes
  const security = dirs + 4 * 8; // IMAGE_DIRECTORY_ENTRY_SECURITY
  if (count <= 4 || security + 8 > data.length) return false;
  return data.readUInt32LE(security + 4) > 0;
}

// The signature state of the main executable in a Windows app zip (the .exe nearest the top,
// preferring one named like the app), or null when it cannot be read.
function zippedExeSigned(zipFile) {
  try {
    const zip = openZip(zipFile);
    const exes = zip.entries
      .filter((e) => /\.exe$/i.test(e.name) && !e.name.endsWith("/"))
      .sort((a, b) => a.name.split("/").length - b.name.split("/").length || (/studio/i.test(b.name) ? 1 : 0) - (/studio/i.test(a.name) ? 1 : 0));
    return exes[0] ? peSigned(zip.read(exes[0])) : null;
  } catch (err) {
    warn(`could not read the executable in ${zipFile}: ${err.message}`);
    return null;
  }
}

function readPackage(ctx, entry) {
  const zip = openZip(entry.abs);
  const nuspecEntry = zip.entries.find((e) => /^[^/\\]+\.nuspec$/i.test(e.name));
  if (!nuspecEntry) throw new Error("no .nuspec at the package root");
  const nuspec = zip.read(nuspecEntry);
  const meta = parseNuspec(nuspec.toString("utf8"));
  if (!/^[A-Za-z0-9_][A-Za-z0-9_.-]*$/.test(meta.id)) throw new Error(`invalid package id "${meta.id}"`);
  const version = parseNuGetVersion(meta.version);
  if (!version) throw new Error(`invalid package version "${meta.version}"`);
  // Target frameworks from lib/<tfm>/ and ref/<tfm>/, else from the dependency groups.
  const tfms = new Set();
  for (const e of zip.entries) {
    const m = /^(?:lib|ref)\/([^/]+)\//i.exec(e.name);
    if (m) tfms.add(decodeURIComponent(m[1]));
  }
  if (tfms.size === 0) for (const g of meta.dependencyGroups) if (g.targetFramework) tfms.add(g.targetFramework);
  return {
    entry,
    meta,
    version,
    nuspec,
    idLower: meta.id.toLowerCase(),
    verLower: version.normalized.toLowerCase(),
    tfms: [...tfms].sort(compareBytes),
    published: iso(entry.stat.mtimeMs),
  };
}

// A NuGet target framework as people name it: "net48" -> ".NET Framework 4.8",
// "net8.0-windows7.0" -> ".NET 8" (the -windowsN platform suffix is NuGet's normalization, not a
// Windows version), "netstandard2.0" -> ".NET Standard 2.0". Unknown forms are kept as written.
function friendlyFramework(tfm) {
  const t = tfm.toLowerCase();
  let m;
  const version = (v) => v.replace(/(?:\.0)+$/, "") || "0";
  if ((m = /^net(\d)(\d)(\d?)$/.exec(t))) return { rank: 0, v: [+m[1], +m[2], +(m[3] || 0)], name: `.NET Framework ${m[1]}.${m[2]}${m[3] ? `.${m[3]}` : ""}` };
  if ((m = /^\.?netframework,?(?:version=)?v?(\d+(?:\.\d+)*)$/.exec(t))) return { rank: 0, v: m[1].split(".").map(Number), name: `.NET Framework ${m[1]}` };
  if ((m = /^\.?netstandard,?(?:version=)?v?(\d+(?:\.\d+)*)$/.exec(t))) return { rank: 1, v: m[1].split(".").map(Number), name: `.NET Standard ${m[1]}` };
  if ((m = /^\.?netcoreapp,?(?:version=)?v?(\d+(?:\.\d+)*)$/.exec(t))) return { rank: 2, v: m[1].split(".").map(Number), name: `.NET Core ${m[1]}` };
  if ((m = /^net(\d+(?:\.\d+)*)(?:-[a-z]+[\d.]*)?$/.exec(t))) return { rank: 3, v: m[1].split(".").map(Number), name: `.NET ${version(m[1])}` };
  return { rank: 4, v: [], name: tfm };
}

function nugetRequirements(tfms) {
  if (tfms.length === 0) return "WPF on Windows 10 1809 or later; .NET Framework 4.8, .NET 8 or .NET 10";
  const names = [];
  for (const f of tfms.map(friendlyFramework).sort((a, b) => a.rank - b.rank || compareVersionArrays(a.v, b.v))) {
    if (!names.includes(f.name)) names.push(f.name);
  }
  const windows = tfms.some((t) => /windows/i.test(t) || /^net[1-4]\d*$/i.test(t));
  return windows ? `WPF on Windows 10 1809 or later; ${names.join(", ")}` : `Targets ${names.join(", ")}`;
}

function compareVersionArrays(a, b) {
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    const d = (a[i] ?? 0) - (b[i] ?? 0);
    if (d) return d;
  }
  return 0;
}

function collectNuGet(ctx, dirEntry) {
  const byKey = new Map(); // "<id>/<version>" lowercase -> package
  const symbols = [];
  for (const entry of dirEntry ? listInputDir(ctx, dirEntry.abs) : []) {
    const lower = entry.name.toLowerCase();
    if (!entry.stat.isFile()) continue;
    if (lower.endsWith(".snupkg")) {
      symbols.push(entry);
      continue;
    }
    if (!lower.endsWith(".nupkg")) {
      warn(`ignoring ${entry.display}: not a .nupkg`);
      continue;
    }
    let pkg;
    try {
      pkg = readPackage(ctx, entry);
    } catch (err) {
      warn(`ignoring ${entry.display}: ${err.message}`);
      continue;
    }
    const key = `${pkg.idLower}/${pkg.verLower}`;
    if (byKey.has(key)) {
      warn(`${entry.display} duplicates ${byKey.get(key).entry.display} (${pkg.meta.id} ${pkg.version.normalized}); ignoring it`);
      continue;
    }
    byKey.set(key, pkg);
  }

  // Group versions per package id, oldest first.
  const groups = new Map();
  for (const pkg of byKey.values()) {
    if (!groups.has(pkg.idLower)) groups.set(pkg.idLower, []);
    groups.get(pkg.idLower).push(pkg);
  }
  for (const list of groups.values()) list.sort((a, b) => compareNuGetVersions(a.version, b.version));
  for (const pkg of byKey.values()) ctx.nugetFeed.set(`${pkg.idLower}/${pkg.verLower}`, pkg.entry.display);

  // The feed serves every version; downloads/ (and SHA256SUMS) only the one each manifest item links
  // (latest stable, else latest pre-release), with its symbol package, under the original file names.
  const latestOf = (list) => {
    const stable = list.filter((p) => !p.version.pre);
    return (stable.length ? stable : list).at(-1);
  };
  const downloaded = new Set();
  for (const list of groups.values()) {
    const latest = latestOf(list);
    planOutput(ctx, `downloads/${latest.entry.name}`, { kind: "copy", src: latest.entry.abs });
    downloaded.add(`${latest.meta.id}.${latest.version.normalized}`.toLowerCase());
  }
  for (const sym of symbols) {
    if (downloaded.has(sym.name.replace(/\.snupkg$/i, "").toLowerCase())) planOutput(ctx, `downloads/${sym.name}`, { kind: "copy", src: sym.abs });
  }

  if (groups.size) planNuGetFeed(ctx, groups);

  // Manifest items: one per package id.
  const expected = (process.env.LG_NUGET_IDS ?? "").split(",").map((s) => s.trim()).filter(Boolean);
  const items = [];
  for (const [idLower, list] of groups) {
    const latest = latestOf(list);
    items.push({
      id: `nuget-${idLower}`,
      name: `${latest.meta.id} (NuGet package)`,
      platform: "windows",
      kind: "library",
      fileName: latest.entry.name,
      version: latest.version.full,
      requirements: nugetRequirements(latest.tfms),
      required: true,
      hint: `artifacts/windows/nuget/${latest.meta.id}.<version>.nupkg`,
      outRel: `downloads/${latest.entry.name}`,
      sourceTimeMs: latest.entry.stat.mtimeMs,
      input: latest.entry.display,
      extra: { packageId: latest.meta.id },
    });
    ctx.nugetPackages.push({ id: latest.meta.id, version: latest.version.full, tfms: latest.tfms, display: latest.entry.display });
  }
  const missing = expected.filter((id) => !groups.has(id.toLowerCase()));
  if (groups.size === 0 && missing.length === 0) missing.push(DEFAULT_NUGET_ID);
  for (const id of missing) {
    items.push({
      id: `nuget-${id.toLowerCase()}`,
      name: `${id} (NuGet package)`,
      platform: "windows",
      kind: "library",
      fileName: `${id}.${ctx.version}.nupkg`,
      version: ctx.version,
      requirements: nugetRequirements([]),
      required: true,
      hint: `artifacts/windows/nuget/${id}.<version>.nupkg`,
      extra: { packageId: id },
    });
  }
  items.sort((a, b) => compareBytes(a.id, b.id));
  for (const item of items) addItem(ctx, item);
}

// Static NuGet v3 feed: service index, flat container, registrations and a search document.
// Shapes follow https://learn.microsoft.com/nuget/api/overview (PackageBaseAddress,
// RegistrationsBaseUrl, SearchQueryService).
function planNuGetFeed(ctx, groups) {
  const base = `${ctx.base}/nuget`;
  const flatBase = `${base}/v3-flatcontainer/`;
  const regBase = `${base}/registration/`;
  const queryUrl = `${base}/query`;
  const buffer = (value) => ({ kind: "buffer", data: Buffer.from(json(value), "utf8") });

  const registrationContext = {
    "@vocab": "http://schema.nuget.org/schema#",
    catalog: "http://schema.nuget.org/catalog#",
    xsd: "http://www.w3.org/2001/XMLSchema#",
    items: { "@id": "catalog:item", "@container": "@set" },
    count: { "@id": "catalog:count" },
    parent: { "@id": "catalog:parent", "@type": "@id" },
    tags: { "@id": "tag", "@container": "@set" },
    dependencyGroups: { "@id": "dependencyGroup", "@container": "@set" },
    dependencies: { "@id": "dependency", "@container": "@set" },
    packageContent: { "@type": "@id" },
    published: { "@type": "xsd:dateTime" },
    registration: { "@type": "@id" },
  };

  planOutput(
    ctx,
    "nuget/index.json",
    buffer({
      version: "3.0.0",
      resources: [
        { "@id": flatBase, "@type": "PackageBaseAddress/3.0.0", comment: "Package content (static flat container)" },
        ...["RegistrationsBaseUrl", "RegistrationsBaseUrl/3.0.0-rc", "RegistrationsBaseUrl/3.0.0-beta", "RegistrationsBaseUrl/3.4.0", "RegistrationsBaseUrl/3.6.0"].map(
          (type) => ({ "@id": regBase, "@type": type, comment: "Package metadata (static registrations, SemVer 2.0.0 included)" }),
        ),
        ...["SearchQueryService", "SearchQueryService/3.0.0-rc", "SearchQueryService/3.0.0-beta", "SearchQueryService/3.5.0"].map((type) => ({
          "@id": queryUrl,
          "@type": type,
          comment: "Static search document listing every package; query parameters are ignored",
        })),
      ],
      "@context": { "@vocab": "http://schema.nuget.org/services#", comment: "http://www.w3.org/2000/01/rdf-schema#comment" },
    }),
  );

  const searchData = [];
  for (const [idLower, list] of [...groups].sort((a, b) => compareBytes(a[0], b[0]))) {
    const regIndexUrl = `${regBase}${idLower}/index.json`;
    const leaves = list.map((pkg) => {
      const { meta, verLower } = pkg;
      const leafUrl = `${regBase}${idLower}/${verLower}.json`;
      const contentUrl = `${flatBase}${idLower}/${verLower}/${idLower}.${verLower}.nupkg`;
      const dependencyGroups = meta.dependencyGroups.map((g) => {
        const groupId = `${leafUrl}#dependencygroup${g.targetFramework ? `/${g.targetFramework.toLowerCase()}` : ""}`;
        const group = { "@id": groupId, "@type": "PackageDependencyGroup" };
        if (g.targetFramework) group.targetFramework = g.targetFramework;
        if (g.dependencies.length) {
          group.dependencies = g.dependencies.map((d) => {
            const dep = { "@id": `${groupId}/${d.id.toLowerCase()}`, "@type": "PackageDependency", id: d.id, range: d.range };
            if (groups.has(d.id.toLowerCase())) dep.registration = `${regBase}${d.id.toLowerCase()}/index.json`;
            return dep;
          });
        }
        return group;
      });
      const catalogEntry = {
        "@id": leafUrl,
        "@type": "PackageDetails",
        authors: meta.authors,
        dependencyGroups,
        description: meta.description,
        iconUrl: meta.iconUrl,
        id: meta.id,
        language: meta.language,
        licenseExpression: meta.licenseExpression,
        licenseUrl: meta.licenseUrl,
        listed: true,
        minClientVersion: meta.minClientVersion,
        packageContent: contentUrl,
        projectUrl: meta.projectUrl,
        published: pkg.published,
        requireLicenseAcceptance: meta.requireLicenseAcceptance,
        summary: meta.summary,
        tags: meta.tags,
        title: meta.title,
        version: pkg.version.full,
      };
      // Flat container files and the standalone registration leaf.
      planOutput(ctx, `nuget/v3-flatcontainer/${idLower}/${verLower}/${idLower}.${verLower}.nupkg`, { kind: "copy", src: pkg.entry.abs });
      planOutput(ctx, `nuget/v3-flatcontainer/${idLower}/${verLower}/${idLower}.nuspec`, { kind: "buffer", data: pkg.nuspec });
      planOutput(
        ctx,
        `nuget/registration/${idLower}/${verLower}.json`,
        buffer({
          "@id": leafUrl,
          "@type": ["Package", "http://schema.nuget.org/catalog#Permalink"],
          listed: true,
          packageContent: contentUrl,
          published: pkg.published,
          registration: regIndexUrl,
          "@context": registrationContext,
        }),
      );
      return { pkg, leafUrl, contentUrl, catalogEntry };
    });

    planOutput(ctx, `nuget/v3-flatcontainer/${idLower}/index.json`, buffer({ versions: list.map((p) => p.verLower) }));

    const lower = list[0].version.normalized;
    const upper = list.at(-1).version.normalized;
    planOutput(
      ctx,
      `nuget/registration/${idLower}/index.json`,
      buffer({
        "@id": regIndexUrl,
        "@type": ["catalog:CatalogRoot", "PackageRegistration", "catalog:Permalink"],
        count: 1,
        items: [
          {
            "@id": `${regIndexUrl}#page/${lower}/${upper}`,
            "@type": "catalog:CatalogPage",
            count: leaves.length,
            items: leaves.map((l) => ({
              "@id": l.leafUrl,
              "@type": "Package",
              catalogEntry: l.catalogEntry,
              packageContent: l.contentUrl,
              registration: regIndexUrl,
            })),
            lower,
            parent: regIndexUrl,
            upper,
          },
        ],
        "@context": registrationContext,
      }),
    );

    // A static document cannot honour `prerelease=`; the API default is false, so report the
    // latest stable version (pre-releases stay listed under `versions`).
    const stable = list.filter((p) => !p.version.pre);
    const latest = (stable.length ? stable : list).at(-1);
    const m = latest.meta;
    searchData.push({
      "@id": regIndexUrl,
      "@type": "Package",
      registration: regIndexUrl,
      id: m.id,
      version: latest.version.full,
      description: m.description,
      summary: m.summary,
      title: m.title,
      iconUrl: m.iconUrl,
      licenseUrl: m.licenseUrl,
      projectUrl: m.projectUrl,
      tags: m.tags,
      authors: m.authors.split(",").map((s) => s.trim()).filter(Boolean),
      owners: m.owners.split(",").map((s) => s.trim()).filter(Boolean),
      totalDownloads: 0,
      verified: false,
      packageTypes: (m.packageTypes.length ? m.packageTypes : ["Dependency"]).map((name) => ({ name })),
      versions: leaves.map((l) => ({ version: l.pkg.version.full, downloads: 0, "@id": l.leafUrl })),
    });
  }

  planOutput(
    ctx,
    "nuget/query",
    buffer({ "@context": { "@vocab": "http://schema.nuget.org/schema#", "@base": regBase }, totalHits: searchData.length, data: searchData }),
  );
}

// -------------------------------------------------------------------------------------------
// Source zip: git archive of the release tag (refs/tags/<version>), or of HEAD without one
// -------------------------------------------------------------------------------------------

function git(ctx, args) {
  return execFileSync("git", ["-C", ctx.rootReal, ...args], { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
}

// The commit the release tag refs/tags/<version> points to, fetching that tag from origin when it is
// not in the checkout yet (a clone without tags); null when there is no such tag.
function releaseTagCommit(ctx) {
  const ref = `refs/tags/${ctx.version}`;
  const resolve = () => {
    try {
      return git(ctx, ["rev-parse", "--verify", "--quiet", `${ref}^{commit}`]);
    } catch {
      return null;
    }
  };
  const local = resolve();
  if (local) return local;
  try {
    execFileSync("git", ["-C", ctx.rootReal, "fetch", "--quiet", "--no-tags", "origin", `+${ref}:${ref}`], {
      stdio: ["ignore", "ignore", "pipe"],
      env: { ...process.env, GIT_TERMINAL_PROMPT: "0" },
      timeout: 30000,
    });
  } catch {
    return null; // offline, no origin, or the tag is not published yet
  }
  return resolve();
}

function collectSource(ctx) {
  const fileName = `liquid-glass-engineering-${ctx.version}-source.zip`;
  const spec = {
    id: "source",
    name: "Liquid Glass Engineering source code",
    platform: "all",
    kind: "source",
    fileName,
    version: ctx.version,
    requirements: REQUIREMENTS.source,
    required: true,
    hint: "a git checkout with at least one commit at --root",
  };
  try {
    // Only archive when --root is the top of its own repository (not a folder inside another one).
    const top = fs.realpathSync(git(ctx, ["rev-parse", "--show-toplevel"]));
    if (top !== ctx.rootReal) throw new Error(`${ctx.rootReal} is not the top of a git repository`);
    // The release's source is the tagged commit, even when main has moved on (docs, website); a
    // checkout without the tag (before the release is tagged) archives HEAD.
    const tagged = releaseTagCommit(ctx);
    const commit = tagged ?? git(ctx, ["rev-parse", "--verify", "HEAD^{commit}"]);
    if (!tagged) warn(`no release tag ${ctx.version} (refs/tags/${ctx.version}): the source zip is built from HEAD ${commit.slice(0, 12)}`);
    const committedMs = Number(git(ctx, ["log", "-1", "--format=%ct", commit])) * 1000;
    const outRel = `downloads/${fileName}`;
    planOutput(ctx, outRel, { kind: "git-archive", prefix: `liquid-glass-engineering-${ctx.version}/`, commit });
    const input = tagged ? `git tag ${ctx.version} ${commit.slice(0, 12)}` : `git HEAD ${commit.slice(0, 12)}`;
    addItem(ctx, { ...spec, outRel, sourceTimeMs: committedMs, input, extra: { commit } });
  } catch (err) {
    spec.hint += ` (${String(err.stderr || err.message).trim().split("\n")[0]})`;
    addItem(ctx, spec);
  }
}

// -------------------------------------------------------------------------------------------
// Agent skills: plugins/liquid-glass/skills/<name>/SKILL.md
// -------------------------------------------------------------------------------------------

// Minimal YAML front matter reader: top-level `key: value` pairs with plain, single-quoted and
// double-quoted scalars (folded over several lines) and `|` / `>` block scalars.
function parseFrontmatter(markdown) {
  const src = markdown.replace(/^\uFEFF/, "").replace(/\r\n?/g, "\n");
  const m = /^---[ \t]*\n([\s\S]*?)\n(?:---|\.\.\.)[ \t]*(?:\n|$)/.exec(src);
  if (!m) return {};
  const lines = m[1].split("\n");
  const result = {};
  for (let i = 0; i < lines.length; i++) {
    const key = /^([A-Za-z_][\w-]*)[ \t]*:(?:[ \t]+(.*))?$/.exec(lines[i]);
    if (!key) continue;
    const block = [];
    while (i + 1 < lines.length && (/^[ \t]/.test(lines[i + 1]) || lines[i + 1].trim() === "")) block.push(lines[++i]);
    result[key[1]] = yamlScalar((key[2] ?? "").trim(), block);
  }
  return result;
}

// Joins flow-scalar lines: single breaks become spaces, each blank line becomes a newline.
function foldLines(parts) {
  let out = "";
  let breaks = 0;
  for (const p of parts) {
    if (p === "") {
      breaks++;
      continue;
    }
    if (out) out += breaks ? "\n".repeat(breaks) : " ";
    out += p;
    breaks = 0;
  }
  return out;
}

function yamlScalar(raw, block) {
  const header = /^([|>])([+-]?)([1-9]?)([+-]?)[ \t]*(?:#.*)?$/.exec(raw);
  if (header) {
    const chomp = header[2] || header[4];
    const first = block.find((l) => l.trim() !== "");
    const indent = header[3] ? Number(header[3]) : first ? first.length - first.trimStart().length : 0;
    const lines = block.map((l) => (l.trim() === "" ? "" : l.slice(indent)));
    let trailing = 0;
    while (lines.length && lines.at(-1) === "") lines.pop(), trailing++;
    let text = lines.join("\n");
    if (header[1] === ">") text = text.replace(/([^\n])\n(?=[^\n])/g, "$1 ").replace(/\n(\n+)/g, "$1");
    if (!text) return "";
    return chomp === "-" ? text : chomp === "+" ? text + "\n".repeat(trailing + 1) : `${text}\n`;
  }
  const rest = block.map((l) => l.trim());
  if (raw.startsWith('"')) {
    const s = foldLines([raw, ...rest]);
    let out = "";
    for (let i = 1; i < s.length; i++) {
      const c = s[i];
      if (c === '"') return out;
      if (c !== "\\") {
        out += c;
        continue;
      }
      const n = s[++i];
      const simpleEscapes = { n: "\n", t: "\t", r: "\r", 0: "\0", '"': '"', "\\": "\\", "/": "/", " ": " ", e: "\x1b" };
      if (n in simpleEscapes) out += simpleEscapes[n];
      else if (n === "x" || n === "u" || n === "U") {
        const len = { x: 2, u: 4, U: 8 }[n];
        out += String.fromCodePoint(parseInt(s.slice(i + 1, i + 1 + len), 16));
        i += len;
      } else out += n ?? "";
    }
    return out;
  }
  if (raw.startsWith("'")) {
    const s = foldLines([raw, ...rest]);
    let out = "";
    for (let i = 1; i < s.length; i++) {
      if (s[i] === "'") {
        if (s[i + 1] === "'") {
          out += "'";
          i++;
          continue;
        }
        return out;
      }
      out += s[i];
    }
    return out;
  }
  // A nested mapping or sequence (e.g. `metadata:`) is not a scalar.
  if (raw === "" && block.some((l) => /^\s+(-\s|[\w-]+\s*:(\s|$))/.test(l))) return null;
  const stripComment = (l) => l.replace(/(^|[ \t])#.*$/, "").trim();
  return foldLines([stripComment(raw), ...rest.map(stripComment)]);
}

// Skill folders whose SKILL.md is committed at HEAD, or null when --root is not a git checkout.
// The website links every skill to GitHub, so only committed skills are published.
function committedSkills(ctx) {
  try {
    if (fs.realpathSync(git(ctx, ["rev-parse", "--show-toplevel"])) !== ctx.rootReal) return null;
  } catch {
    return null;
  }
  try {
    const files = git(ctx, ["ls-tree", "-r", "--name-only", "HEAD", "--", "plugins/liquid-glass/skills"]).split("\n");
    return new Set(files.map((f) => /^plugins\/liquid-glass\/skills\/([^/]+)\/SKILL\.md$/.exec(f)?.[1]).filter(Boolean));
  } catch {
    return new Set(); // no commits yet
  }
}

function collectSkills(ctx) {
  const skillsDir = path.join(ctx.root, "plugins", "liquid-glass", "skills");
  const found = new Map();
  const committed = committedSkills(ctx);
  for (const entry of listInputDir(ctx, skillsDir)) {
    if (!entry.stat.isDirectory()) continue;
    const skillMd = path.join(entry.abs, "SKILL.md");
    const st = lstatOrNull(skillMd);
    if (!st || !st.isFile()) {
      warn(`ignoring ${entry.display}: no SKILL.md`);
      continue;
    }
    if (committed && !committed.has(entry.name)) {
      warn(`ignoring ${entry.display}: its SKILL.md is not committed, so the website's GitHub links to it would 404`);
      ctx.uncommittedSkills.push(entry.name);
      continue;
    }
    const front = parseFrontmatter(fs.readFileSync(skillMd, "utf8"));
    const skillName = typeof front.name === "string" && front.name.trim() ? front.name.trim() : entry.name;
    if (skillName !== entry.name) warn(`${entry.display}/SKILL.md declares name "${skillName}"`);
    const description = typeof front.description === "string" ? front.description.trim() : "";
    if (!description) warn(`${entry.display}/SKILL.md has no description`);
    const zip = treeToZipEntries(ctx, entry.abs, entry.name);
    found.set(entry.name, { entry, skillName, description, ...zip });
  }

  // All skills in one zip, each folder at the zip root.
  const all = [...found.values()];
  const allRel = "downloads/liquid-glass-skills.zip";
  const allPlanned = all.length > 0 && planOutput(ctx, allRel, { kind: "zip", entries: all.flatMap((s) => s.entries) });
  addItem(ctx, {
    id: "skills",
    name: "Liquid Glass agent skills",
    platform: "all",
    kind: "skills",
    fileName: "liquid-glass-skills.zip",
    version: ctx.version,
    requirements: REQUIREMENTS.skills,
    required: true,
    hint: "plugins/liquid-glass/skills/<name>/SKILL.md",
    ...(allPlanned
      ? { outRel: allRel, sourceTimeMs: Math.max(...all.map((s) => s.newestMs)), input: `${display(ctx, skillsDir)}/ (${all.length} skills)` }
      : null),
  });

  // One zip per skill: expected names first (even when missing), then any other skill found.
  const names = [...EXPECTED_SKILLS, ...[...found.keys()].filter((n) => !EXPECTED_SKILLS.includes(n)).sort(compareBytes)];
  for (const name of names) {
    const skill = found.get(name);
    // Flat, with a distinct name: SHA256SUMS lists the same name the browser saves the file as.
    const rel = `downloads/skill-${name}.zip`;
    const planned = skill && planOutput(ctx, rel, { kind: "zip", entries: skill.entries });
    addItem(ctx, {
      id: `skill-${name}`,
      name: `${name} agent skill`,
      platform: "all",
      kind: "skill",
      fileName: `skill-${name}.zip`,
      version: ctx.version,
      requirements: REQUIREMENTS.skills,
      required: false,
      hint: `plugins/liquid-glass/skills/${name}/SKILL.md`,
      ...(planned ? { outRel: rel, sourceTimeMs: skill.newestMs, input: `${skill.entry.display}/` } : null),
      extra: { skillName: skill?.skillName ?? name, description: skill?.description ?? null },
    });
  }
}

// -------------------------------------------------------------------------------------------
// Earlier releases (--restore-released, --strict): the feeds keep every published version
// -------------------------------------------------------------------------------------------

const versionKey = (v) => (parseNuGetVersion(v)?.normalized ?? String(v)).toLowerCase();
const releaseAssetUrl = (tag, name) => `${REPOSITORY_URL}/releases/download/${encodeURIComponent(tag)}/${encodeURIComponent(name)}`;

// GET with a timeout, retried twice after a network error or a 5xx answer; any other failure throws at once.
async function download(url, { accept, timeoutMs = 60000 } = {}) {
  let last = null;
  for (let attempt = 1; attempt <= 3; attempt++) {
    try {
      const res = await fetch(url, {
        headers: { "User-Agent": "liquid-glass-build-downloads", ...(accept ? { Accept: accept } : {}) },
        redirect: "follow",
        signal: AbortSignal.timeout(timeoutMs),
      });
      if (res.ok) return Buffer.from(await res.arrayBuffer());
      last = `HTTP ${res.status}`;
      if (res.status < 500) break;
    } catch (err) {
      last = err.cause?.message ? `${err.message} (${err.cause.message})` : err.message;
    }
    if (attempt < 3) await new Promise((resolve) => setTimeout(resolve, 2000 * attempt));
  }
  throw new Error(`${url}: ${last}`);
}

// Published (not draft) GitHub releases whose tag is a version.
async function publishedReleases() {
  const releases = [];
  for (let page = 1; page <= 10; page++) {
    const batch = JSON.parse((await download(`${RELEASES_API_URL}?per_page=100&page=${page}`, { accept: "application/vnd.github+json" })).toString("utf8"));
    if (!Array.isArray(batch)) throw new Error(`${RELEASES_API_URL} did not answer with a list of releases`);
    releases.push(...batch);
    if (batch.length < 100) break;
  }
  return releases.filter((r) => r && !r.draft && typeof r.tag_name === "string" && parseNuGetVersion(r.tag_name));
}

// sha256sum lines ("<hex>  <name>" or "<hex> *<name>") -> Map name -> hex.
function parseSums(text) {
  const sums = new Map();
  for (const line of text.split(/\r?\n/)) {
    const m = /^\\?([0-9a-f]{64}) [ *](.+)$/i.exec(line);
    if (m) sums.set(m[2], m[1].toLowerCase());
  }
  return sums;
}

// The SHA-256 a release asset must have: from the release's SHA256SUMS and GitHub's digest of the asset, which
// must agree when both exist. Throws when neither exists.
function expectedSha256(release, asset, sums) {
  const listed = sums?.get(asset.name) ?? null;
  const digest = /^sha256:([0-9a-f]{64})$/i.exec(String(asset.digest ?? ""))?.[1].toLowerCase() ?? null;
  if (listed && digest && listed !== digest) throw new Error(`SHA256SUMS (${listed}) and GitHub (${digest}) disagree on ${asset.name}`);
  if (!listed && !digest) throw new Error(`${asset.name} has no checksum (not in SHA256SUMS, no GitHub digest), so it cannot be verified`);
  return listed ?? digest;
}

// Adds a verified file to artifacts/ (never over an existing one), dated at the release's publication time so
// the feed entries it produces (registration `published`, maven-metadata lastUpdated) are reproducible.
function addRestoredFile(ctx, dest, data, timeMs) {
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  const tmp = tempPath(dest);
  try {
    fs.writeFileSync(tmp, data, { mode: 0o644 });
    fs.utimesSync(tmp, new Date(timeMs), new Date(timeMs));
    fs.renameSync(tmp, dest);
  } catch (err) {
    fs.rmSync(tmp, { force: true });
    throw err;
  }
  ctx.restored.push(display(ctx, dest));
}

// The release's .nupkg assets, into artifacts/windows/nuget. A package already there must be the released file.
async function restoreNuGet(ctx, release, sums, record, timeMs) {
  const tag = release.tag_name;
  const dir = path.join(ctx.artifacts, "windows", "nuget");
  for (const asset of release.assets.filter((a) => /\.nupkg$/i.test(String(a.name)))) {
    if (!/^[A-Za-z0-9_][A-Za-z0-9_.-]*\.nupkg$/.test(asset.name)) throw new Error(`unexpected package name ${asset.name}`);
    const want = expectedSha256(release, asset, sums);
    const dest = path.join(dir, asset.name);
    const existing = lstatOrNull(dest);
    if (existing) {
      if (!existing.isFile() || hashFile(dest).sha256 !== want) {
        throw new Error(`${display(ctx, dest)} is not the ${asset.name} of the release (sha256 ${want}): replace it with the release's file`);
      }
    } else {
      const data = await download(releaseAssetUrl(tag, asset.name));
      const got = hashBuffer(data).sha256;
      if (got !== want || (Number.isInteger(asset.size) && data.length !== asset.size)) throw new Error(`${asset.name} does not match its checksum (got sha256 ${got})`);
      addRestoredFile(ctx, dest, data, timeMs);
    }
    const pkg = readPackage(ctx, { abs: dest, stat: fs.statSync(dest), display: display(ctx, dest) });
    if (versionKey(pkg.version.normalized) !== versionKey(tag)) throw new Error(`${asset.name} holds version ${pkg.version.full}, not ${tag}`);
    record.nuget.push({ id: pkg.meta.id, key: `${pkg.idLower}/${pkg.verLower}` });
  }
}

// The Compose library's Maven files for a release that ships its AAR, into artifacts/android/maven. The release
// holds the AAR only, so the files come from the production site's repository: the Gradle module and the pom
// checked against their .sha256 and .sha512 files, every file the module names against the module's sizes and
// hashes, and the module's AAR against the release's checksum.
async function restoreMaven(ctx, release, sums, record, timeMs) {
  const tag = release.tag_name;
  const aarName = `${MAVEN_ARTIFACT}-${tag}.aar`;
  const aarAsset = release.assets.find((a) => a.name === aarName);
  if (!aarAsset) return;
  record.maven = true;
  const aarSha = expectedSha256(release, aarAsset, sums);
  const rel = `${MAVEN_GROUP_PATH}/${MAVEN_ARTIFACT}/${tag}`;
  const dir = path.join(ctx.artifacts, "android", "maven", ...rel.split("/"));
  const pomName = `${MAVEN_ARTIFACT}-${tag}.pom`;
  const moduleName = `${MAVEN_ARTIFACT}-${tag}.module`;
  if (lstatOrNull(path.join(dir, pomName))) {
    // Built or restored here before: it must be the released build.
    const localAar = path.join(dir, aarName);
    if (!lstatOrNull(localAar)?.isFile() || hashFile(localAar).sha256 !== aarSha) {
      throw new Error(`${display(ctx, localAar)} is missing or is not the AAR of the release (sha256 ${aarSha})`);
    }
    return;
  }
  const base = `${ctx.releasedSite}/maven/${rel}/`;
  const first = (buf) => buf.toString("utf8").trim().split(/\s+/)[0].toLowerCase();
  const fetchChecked = async (name) => {
    const [data, sha256, sha512] = await Promise.all([download(base + name), download(`${base}${name}.sha256`), download(`${base}${name}.sha512`)]);
    const h = hashBuffer(data, ["sha256", "sha512"]);
    if (first(sha256) !== h.sha256 || first(sha512) !== h.sha512) throw new Error(`${base}${name} does not match its .sha256 and .sha512 files`);
    return data;
  };

  const moduleData = await fetchChecked(moduleName);
  const module = JSON.parse(moduleData.toString("utf8"));
  const component = module.component ?? {};
  if (component.group !== MAVEN_GROUP_PATH.replace(/\//g, ".") || component.module !== MAVEN_ARTIFACT || component.version !== tag) {
    throw new Error(`${base}${moduleName} describes ${component.group}:${component.module}:${component.version}`);
  }
  const files = new Map();
  for (const variant of module.variants ?? []) {
    for (const f of variant.files ?? []) {
      if (typeof f.name !== "string" || !/^[A-Za-z0-9][A-Za-z0-9._-]*$/.test(f.name) || f.url !== f.name) throw new Error(`${moduleName} names an unexpected file ${f.name}`);
      files.set(f.name, f);
    }
  }
  if (String(files.get(aarName)?.sha256 ?? "").toLowerCase() !== aarSha) throw new Error(`${base}${moduleName} does not describe the AAR of the release (sha256 ${aarSha})`);

  const out = new Map(); // name -> verified bytes; the pom goes last, since a version counts once its pom is there
  for (const [name, f] of files) {
    const data = await download(base + name);
    const h = hashBuffer(data, ["sha256", "sha512"]);
    if (data.length !== f.size || h.sha256 !== String(f.sha256).toLowerCase() || h.sha512 !== String(f.sha512).toLowerCase()) {
      throw new Error(`${base}${name} does not match ${moduleName}`);
    }
    out.set(name, data);
  }
  const pom = await fetchChecked(pomName);
  const pomText = pom.toString("utf8");
  if (!pomText.includes(`<artifactId>${MAVEN_ARTIFACT}</artifactId>`) || !pomText.includes(`<version>${tag}</version>`)) throw new Error(`${base}${pomName} is not ${MAVEN_ARTIFACT} ${tag}`);
  out.set(moduleName, moduleData);
  out.set(pomName, pom);

  for (const [name, data] of out) {
    const dest = path.join(dir, name);
    const st = lstatOrNull(dest);
    if (st) {
      if (st.isFile() && hashFile(dest).sha256 === hashBuffer(data).sha256) continue;
      throw new Error(`${display(ctx, dest)} exists with other content: remove the incomplete ${tag} folder and run again`);
    }
    addRestoredFile(ctx, dest, data, timeMs);
  }
}

// Fills ctx.released (one record per published release other than this version) and adds what artifacts/ lacks.
// Returns the problems; any problem fails the run.
async function restoreReleased(ctx) {
  let releases;
  try {
    releases = await publishedReleases();
  } catch (err) {
    return [`could not list the published releases: ${err.message}`];
  }
  const problems = [];
  ctx.released = [];
  for (const release of releases) {
    const tag = release.tag_name;
    if (versionKey(tag) === versionKey(ctx.version)) continue;
    const record = { tag, nuget: [], maven: false };
    ctx.released.push(record);
    release.assets = Array.isArray(release.assets) ? release.assets : [];
    const timeMs = Date.parse(release.published_at ?? release.created_at ?? "") || ctx.nowMs;
    try {
      let sums = null;
      const sumsAsset = release.assets.find((a) => a.name === "SHA256SUMS");
      if (sumsAsset) {
        const data = await download(releaseAssetUrl(tag, "SHA256SUMS"));
        const digest = /^sha256:([0-9a-f]{64})$/i.exec(String(sumsAsset.digest ?? ""))?.[1].toLowerCase();
        if (digest && hashBuffer(data).sha256 !== digest) throw new Error("SHA256SUMS does not match GitHub's digest of it");
        sums = parseSums(data.toString("utf8"));
      }
      await restoreNuGet(ctx, release, sums, record, timeMs);
      await restoreMaven(ctx, release, sums, record, timeMs);
    } catch (err) {
      problems.push(`release ${tag}: ${err.message}`);
    }
  }
  const tags = ctx.released.map((r) => r.tag).join(", ") || "none";
  if (ctx.restored.length) {
    info(`Earlier releases (${tags}): restored ${ctx.restored.length} file(s) into artifacts/:`);
    for (const file of ctx.restored) info(`  ${file}`);
  } else if (!problems.length) info(`Earlier releases (${tags}): artifacts/ already has their packages`);
  if (problems.length) info(`Earlier releases (${tags}): ${problems.length} could not be restored (listed at the end)`);
  return problems;
}

// -------------------------------------------------------------------------------------------
// Cleaning, producing, manifest
// -------------------------------------------------------------------------------------------

// Removes every file below the owned folders that the plan does not produce (symlinks are
// removed, never followed), then prunes folders that became empty.
function cleanStale(ctx) {
  const removed = [];
  const sweep = (abs, rel) => {
    let empty = true;
    for (const name of fs.readdirSync(abs)) {
      const childAbs = path.join(abs, name);
      const childRel = `${rel}/${name}`;
      const lst = fs.lstatSync(childAbs);
      if (lst.isDirectory()) {
        if (sweep(childAbs, childRel)) fs.rmdirSync(childAbs);
        else empty = false;
      } else if (ctx.plan.has(childRel)) {
        empty = false;
      } else {
        fs.unlinkSync(childAbs);
        removed.push(childRel);
      }
    }
    return empty;
  };
  for (const dir of OWNED_DIRS) {
    const top = path.join(ctx.out, dir);
    const st = lstatOrNull(top);
    if (!st) continue;
    if (st.isSymbolicLink() || !st.isDirectory()) fail(`${top} is not a plain folder; refusing to clean it`);
    sweep(top, dir);
  }
  return removed;
}

function produceAll(ctx) {
  for (const [rel, job] of ctx.plan) {
    if (job.kind === "final") continue;
    const dest = outPath(ctx, rel);
    try {
      fs.mkdirSync(path.dirname(dest), { recursive: true });
      let result;
      if (job.kind === "copy") result = copyIfChanged(ctx, job.src, dest);
      else if (job.kind === "buffer") result = writeBufferIfChanged(ctx, dest, job.data);
      else if (job.kind === "zip") result = writeZip(ctx, dest, job.entries);
      else if (job.kind === "git-archive") result = gitArchive(ctx, dest, job);
      else throw new Error(`unknown job kind ${job.kind}`);
      ctx.results.set(rel, result);
    } catch (err) {
      ctx.failed.add(rel);
      warn(`could not produce ${rel}: ${err.message}`);
      fs.rmSync(dest, { force: true }); // never leave a stale or partial file behind a link
    }
  }
}

function readPreviousManifest(ctx) {
  try {
    const prev = JSON.parse(fs.readFileSync(outPath(ctx, MANIFEST_REL), "utf8"));
    return prev && typeof prev === "object" && Array.isArray(prev.items) ? prev : null;
  } catch {
    return null;
  }
}

// The commit the downloads are built from, and whether the working tree differs from it (tracked changes or
// untracked files, which the skill zips would include). Both null when --root is not the top of a git checkout.
function sourceState(ctx) {
  try {
    if (fs.realpathSync(git(ctx, ["rev-parse", "--show-toplevel"])) !== ctx.rootReal) return { commit: null, modified: null };
    return { commit: git(ctx, ["rev-parse", "--verify", "HEAD^{commit}"]), modified: git(ctx, ["status", "--porcelain"]) !== "" };
  } catch {
    return { commit: null, modified: null };
  }
}

function buildManifest(ctx, previous) {
  const { commit, modified } = sourceState(ctx);
  const prevById = new Map((previous?.items ?? []).map((i) => [i.id, i]));
  const items = ctx.items.map((spec) => {
    let available = false;
    let bytes = null;
    let sha256 = null;
    let file = null;
    let fingerprint = null;
    const extra = { ...spec.extra };
    if (spec.kind === "repository") {
      const rels = spec.repository?.rels ?? [];
      if (spec.repository && rels.every((r) => ctx.results.has(r))) {
        available = true;
        file = spec.repository.url;
        bytes = rels.reduce((sum, r) => sum + ctx.results.get(r).bytes, 0);
        // A digest of the whole tree keeps updatedAt stable while nothing in it changed.
        const listing = [...rels].sort(compareBytes).map((r) => `${ctx.results.get(r).sha256}  ${r}\n`).join("");
        fingerprint = crypto.createHash("sha256").update(listing).digest("hex");
        extra.treeSha256 = fingerprint;
      }
    } else if (spec.outRel && ctx.results.has(spec.outRel)) {
      const r = ctx.results.get(spec.outRel);
      available = true;
      file = siteUrl(spec.outRel);
      bytes = r.bytes;
      sha256 = r.sha256;
      fingerprint = r.sha256;
    }
    // updatedAt: kept while the content is unchanged; otherwise the input's own time (file
    // mtime, commit time), so a rebuild from scratch reproduces it, or now when that time is not
    // newer than what the previous manifest said (e.g. a file was removed from a bundle).
    let updatedAt = null;
    if (available) {
      const prev = prevById.get(spec.id);
      const prevFingerprint = prev ? (prev.kind === "repository" ? prev.treeSha256 : prev.sha256) : null;
      const prevMs = prev && typeof prev.updatedAt === "string" ? Date.parse(prev.updatedAt) : NaN;
      if (prev && prev.available && prevFingerprint === fingerprint && Number.isFinite(prevMs)) {
        updatedAt = prev.updatedAt;
      } else {
        let ms = spec.sourceTimeMs ?? ctx.nowMs;
        if (Number.isFinite(prevMs) && prevMs >= ms) ms = Math.max(ctx.nowMs, prevMs + 1);
        updatedAt = iso(ms);
      }
    }
    if (spec.kind === "repository" && !available) delete extra.treeSha256;
    return {
      id: spec.id,
      name: spec.name,
      platform: spec.platform,
      kind: spec.kind,
      file,
      fileName: spec.fileName,
      bytes,
      sha256,
      version: spec.version,
      requirements: spec.requirements,
      updatedAt,
      available,
      required: spec.required,
      ...extra,
    };
  });
  const unchanged =
    previous &&
    previous.schema === 1 &&
    previous.version === ctx.version &&
    previous.repository === REPOSITORY_URL &&
    previous.commit === commit &&
    previous.modified === modified &&
    typeof previous.generatedAt === "string" &&
    JSON.stringify(previous.items) === JSON.stringify(items);
  return {
    schema: 1,
    version: ctx.version,
    generatedAt: unchanged ? previous.generatedAt : iso(ctx.nowMs),
    repository: REPOSITORY_URL,
    commit,
    modified,
    items,
  };
}

// sha256sum format: "<hex>  <name>", names relative to downloads/, sorted byte-wise.
function buildSums(ctx) {
  const lines = [];
  for (const [rel, result] of ctx.results) {
    if (!rel.startsWith("downloads/") || rel === MANIFEST_REL || rel === SUMS_REL) continue;
    lines.push([rel.slice("downloads/".length), result.sha256]);
  }
  lines.sort((a, b) => compareBytes(a[0], b[0]));
  return lines
    .map(([name, sha]) =>
      /[\\\n]/.test(name) ? `\\${sha}  ${name.replace(/\\/g, "\\\\").replace(/\n/g, "\\n")}\n` : `${sha}  ${name}\n`,
    )
    .join("");
}

function printSummary(ctx, manifest, removed) {
  const rows = [["ITEM", "AVAILABLE", "REQUIRED", "SIZE", "VERSION", "INPUT"]];
  manifest.items.forEach((item, i) => {
    const spec = ctx.items[i];
    rows.push([
      item.id,
      item.available ? "yes" : "no",
      item.required ? "yes" : "no",
      formatBytes(item.bytes),
      item.version ?? "",
      item.available ? (spec.input ?? "") : `missing: ${spec.hint}`,
    ]);
  });
  const widths = rows[0].map((_, c) => Math.max(...rows.map((r) => r[c].length)));
  info("");
  for (const r of rows) info(r.map((cell, c) => (c === r.length - 1 ? cell : cell.padEnd(widths[c]))).join("  ").trimEnd());
  const available = manifest.items.filter((i) => i.available).length;
  info("");
  info(
    `${available}/${manifest.items.length} items available. Files: ${ctx.stats.written} written, ${ctx.stats.unchanged} unchanged, ${removed.length} stale removed.`,
  );
}

// -------------------------------------------------------------------------------------------
// Main
// -------------------------------------------------------------------------------------------

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  if (opts.help) {
    process.stdout.write(`${USAGE}\n`);
    return 0;
  }
  QUIET = opts.quiet;

  const scriptRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
  const root = path.resolve(opts.root ?? scriptRoot);
  if (!fs.existsSync(root) || !fs.statSync(root).isDirectory()) fail(`--root is not a folder: ${root}`);
  const rootReal = fs.realpathSync(root);
  const out = path.resolve(opts.out ?? path.join(root, "website", "public"));
  const version = (process.env.LG_VERSION ?? "").trim() || DEFAULT_VERSION;
  if (!/^[0-9A-Za-z][0-9A-Za-z.+-]*$/.test(version)) fail(`LG_VERSION is not a usable version: ${version}`);
  const epoch = Number(process.env.SOURCE_DATE_EPOCH);

  const ctx = {
    root,
    rootReal,
    out,
    base: normalizeBaseUrl(opts.baseUrl),
    version,
    artifacts: path.join(root, "artifacts"),
    nowMs: process.env.SOURCE_DATE_EPOCH && Number.isFinite(epoch) ? epoch * 1000 : Date.now(),
    plan: new Map(),
    results: new Map(),
    failed: new Set(),
    items: [],
    stats: { written: 0, unchanged: 0 },
    nugetPackages: [], // { id, version, tfms, display } per published package id, for the release checks
    nugetFeed: new Map(), // "<id>/<version>" (lowercase) -> input, every package the feed serves
    mavenVersions: [], // the Compose library versions the Maven repository serves, oldest first
    released: null, // with --restore-released: { tag, nuget: [{ id, key }], maven } per other published release
    restored: [], // files --restore-released added to artifacts/
    releasedSite: normalizeBaseUrl(process.env.LG_SITE_URL || DEFAULT_SITE_URL),
    uncommittedSkills: [],
    zipProblems: [], // app zips that would not extract cleanly with unzip, for the release checks
  };

  // The owned output folders must not overlap the inputs.
  fs.mkdirSync(out, { recursive: true });
  const outReal = fs.realpathSync(out);
  const artifactsReal = fs.existsSync(ctx.artifacts) ? fs.realpathSync(ctx.artifacts) : path.join(rootReal, "artifacts");
  for (const dir of OWNED_DIRS) {
    const owned = path.join(outReal, dir);
    if (isInside(owned, artifactsReal) || isInside(artifactsReal, owned) || isInside(rootReal, owned)) {
      fail(`refusing to use ${owned}: it overlaps the inputs`);
    }
  }

  info(`Liquid Glass downloads ${version}: ${rootReal} -> ${out} (feed base ${ctx.base})`);

  // 0. Earlier releases' packages into artifacts/ (the only network step besides the tag check), so the feeds keep them.
  const restoreProblems = opts.restoreReleased || opts.strict ? await restoreReleased(ctx) : [];

  // 1. Plan every output (reads inputs only).
  collectApple(ctx);
  collectAndroid(ctx);
  collectWindows(ctx);
  collectSource(ctx);
  collectSkills(ctx);
  planOutput(ctx, MANIFEST_REL, { kind: "final" });
  planOutput(ctx, SUMS_REL, { kind: "final" });

  // 2. Remove what this run will not produce, 3. write the rest, 4. manifest and checksums.
  const previous = readPreviousManifest(ctx);
  const removed = cleanStale(ctx);
  produceAll(ctx);
  const manifest = buildManifest(ctx, previous);
  ctx.results.set(MANIFEST_REL, writeBufferIfChanged(ctx, outPath(ctx, MANIFEST_REL), Buffer.from(json(manifest), "utf8")));
  ctx.results.set(SUMS_REL, writeBufferIfChanged(ctx, outPath(ctx, SUMS_REL), Buffer.from(buildSums(ctx), "utf8")));

  printSummary(ctx, manifest, removed);

  let status = 0;
  if (restoreProblems.length) {
    const list = restoreProblems.map((p) => `  - ${p}`).join("\n");
    process.stderr.write(`build-downloads: earlier releases could not be restored, so the feeds would drop them:\n${list}\n`);
    status = 1;
  }
  const missing = manifest.items.filter((i) => i.required && !i.available);
  if (missing.length) {
    const list = missing.map((i) => `  - ${i.id} (${i.name}): expected ${ctx.items.find((s) => s.id === i.id).hint}`).join("\n");
    if (opts.strict) {
      process.stderr.write(`build-downloads: ${missing.length} required item(s) missing:\n${list}\n`);
      status = 1;
    } else info(`${missing.length} required item(s) missing (shown as unavailable; --strict fails on this):\n${list}`);
  }
  // Network checks (the release tag on origin) run only for a release build.
  const problems = releaseProblems(ctx, manifest, { network: opts.strict });
  if (problems.length) {
    const list = problems.map((p) => `  - ${p}`).join("\n");
    if (opts.strict) {
      process.stderr.write(`build-downloads: ${problems.length} release check(s) failed:\n${list}\n`);
      status = 1;
    } else info(`${problems.length} release check(s) would fail with --strict:\n${list}`);
  }
  return status;
}

// -------------------------------------------------------------------------------------------
// Release checks (--strict): what the website promises must hold for the published files
// -------------------------------------------------------------------------------------------

// A .csproj target framework in NuGet's normalized folder form: "net8.0-windows" ->
// "net8.0-windows7.0" (the default Windows platform version for .NET 5+), others unchanged.
function nugetFolderName(tfm) {
  const t = tfm.trim().toLowerCase();
  return /^net\d+\.\d+-windows$/.test(t) ? `${t}7.0` : t;
}

// PackageId (or project name) -> declared target frameworks, for every .csproj below platforms/.
function declaredFrameworks(ctx) {
  const out = new Map();
  const visit = (dir, depth) => {
    let names;
    try {
      names = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }
    for (const d of names) {
      if (d.isDirectory() && depth < 4 && !/^(bin|obj|node_modules|\..*)$/.test(d.name)) visit(path.join(dir, d.name), depth + 1);
      else if (d.isFile() && d.name.endsWith(".csproj")) {
        const xml = fs.readFileSync(path.join(dir, d.name), "utf8");
        const id = /<PackageId>\s*([^<\s]+)\s*<\/PackageId>/.exec(xml)?.[1] ?? d.name.replace(/\.csproj$/, "");
        const list = /<TargetFrameworks?>\s*([^<]+?)\s*<\/TargetFrameworks?>/.exec(xml)?.[1];
        if (list) out.set(id.toLowerCase(), { file: display(ctx, path.join(dir, d.name)), tfms: list.split(";").map((t) => t.trim()).filter(Boolean) });
      }
    }
  };
  visit(path.join(ctx.root, "platforms"), 0);
  return out;
}

function releaseProblems(ctx, manifest, { network }) {
  const problems = [];

  // Every NuGet package ships the frameworks its project declares (a net48 build left out of a
  // package makes the site's ".NET Framework 4.8" claim false).
  const declared = declaredFrameworks(ctx);
  for (const pkg of ctx.nugetPackages) {
    const project = declared.get(pkg.id.toLowerCase());
    if (!project) continue;
    const want = [...new Set(project.tfms.map(nugetFolderName))].sort(compareBytes);
    const have = [...new Set(pkg.tfms.map((t) => t.toLowerCase()))].sort(compareBytes);
    if (want.join(";") !== have.join(";")) {
      problems.push(`${pkg.display} targets ${have.join(", ") || "nothing"}, but ${project.file} declares ${want.join(", ")}: rebuild the package`);
    }
  }

  // App zips extract cleanly with any unzip tool.
  problems.push(...ctx.zipProblems);

  // The feeds keep every published release, and serve no version that is neither one nor this release (package.sh
  // keeps earlier versions in artifacts/windows/nuget, so an unreleased build of an earlier version would linger).
  if (ctx.released) {
    const current = versionKey(ctx.version);
    const released = new Set(ctx.released.map((r) => versionKey(r.tag)));
    for (const r of ctx.released) {
      for (const p of r.nuget) {
        if (!ctx.nugetFeed.has(p.key)) problems.push(`the NuGet feed would drop ${p.id} ${r.tag} (release ${r.tag}): it is not in artifacts/windows/nuget`);
      }
      if (r.maven && !ctx.mavenVersions.some((v) => versionKey(v) === versionKey(r.tag))) {
        problems.push(`the Maven repository would drop ${MAVEN_ARTIFACT} ${r.tag} (release ${r.tag}): it is not in artifacts/android/maven`);
      }
    }
    for (const [key, input] of ctx.nugetFeed) {
      const v = key.slice(key.indexOf("/") + 1);
      if (v !== current && !released.has(v)) problems.push(`${input} is version ${v}, neither ${ctx.version} nor a published release: delete it, or the feed serves it`);
    }
    for (const v of ctx.mavenVersions) {
      if (versionKey(v) !== current && !released.has(versionKey(v))) {
        problems.push(`artifacts/android/maven has ${MAVEN_ARTIFACT} ${v}, neither ${ctx.version} nor a published release: delete its folder, or the repository serves it`);
      }
    }
  }

  // Every skill the website lists is committed, and the expected ones exist.
  for (const name of ctx.uncommittedSkills) problems.push(`skill ${name} is not committed (plugins/liquid-glass/skills/${name}/SKILL.md)`);
  for (const name of EXPECTED_SKILLS) {
    if (!manifest.items.some((i) => i.id === `skill-${name}` && i.available)) problems.push(`skill ${name} is missing from plugins/liquid-glass/skills`);
  }

  // The downloads (source zip, skill zips) are built from a commit that is on GitHub.
  if (manifest.commit === null) problems.push("--root is not the top of a git checkout: the source and skills cannot be tied to a commit");
  else {
    if (manifest.modified) problems.push("the working tree has uncommitted or untracked changes: commit them so the zips match the repository");
    try {
      execFileSync("git", ["-C", ctx.rootReal, "merge-base", "--is-ancestor", manifest.commit, "refs/remotes/origin/main"], { stdio: "ignore" });
    } catch {
      problems.push(`HEAD ${manifest.commit.slice(0, 12)} is not on origin/main: push it so the website's GitHub links resolve`);
    }
    // Swift Package Manager and JitPack find versions only from tags.
    if (network) {
      try {
        const remote = execFileSync("git", ["-C", ctx.rootReal, "ls-remote", "--tags", "origin", `refs/tags/${ctx.version}`], {
          encoding: "utf8",
          stdio: ["ignore", "pipe", "pipe"],
          env: { ...process.env, GIT_TERMINAL_PROMPT: "0" },
          timeout: 30000,
        }).trim();
        if (!remote) problems.push(`the release tag ${ctx.version} is not on origin: create and push it (git tag -a ${ctx.version} -m "..." && git push origin ${ctx.version})`);
      } catch (err) {
        problems.push(`could not check the release tag ${ctx.version} on origin: ${String(err.stderr || err.message).trim().split("\n")[0]}`);
      }
    }
  }
  return problems;
}

process.exitCode = await main();
