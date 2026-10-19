#!/usr/bin/env node
// Sets the release version, and the apps' build number, everywhere they are written in machine-readable form, or
// checks that those places agree (CI runs the check).
//
//   node scripts/set-version.mjs 1.0.2             # the version, and the build number raised by one
//   node scripts/set-version.mjs 1.0.2 --build 7   # the version and an explicit build number
//   node scripts/set-version.mjs --check           # exit 1 when the places disagree
//   (each with an optional --root <dir>, the repository root)
//
// The places are listed in PLACES below, grouped by platform; each pattern must match exactly as often as its
// `count` says, so a file whose layout changed fails loudly instead of being skipped. The build number is Android's
// versionCode and Apple's CURRENT_PROJECT_VERSION: one number for every app, raised by one per release.
//
// Prose is not rewritten. Setting a version lists the Markdown lines that still name the previous version (install
// snippets in the README, the guides, the Hello Glass READMEs and the skills) and the release notes in
// platforms/windows/Directory.Build.props, to edit by hand. --check also fails while an install coordinate in a
// tracked Markdown file names another version, or the release notes name another version. CHANGELOG.md,
// CONTRIBUTING.md and SECURITY.md keep their history and examples, so they are not read.
//
// Only Node built-ins are used (Node 20.19+, developed on Node 22).

import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

// A release version: x.y.z with an optional pre-release (1.1.0-rc.1), never ending in a dot.
const VERSION = String.raw`\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?`;
const VERSION_ONLY = new RegExp(`^${VERSION}$`);

// kind: "version" (the release version), "assembly" (its Windows form, 1.0.2.0) or "build" (the build number).
// pattern: three groups, (before)(value)(after); count: the number of matches the file must have.
const PLACES = [
  // Android
  { file: "platforms/android/liquidglass/build.gradle.kts", kind: "version", pattern: /(val libraryVersion = ")([^"]*)(")/g, count: 1 },
  { file: "platforms/android/studio/build.gradle.kts", kind: "version", pattern: /(versionName = ")([^"]*)(")/g, count: 1 },
  { file: "platforms/android/studio/build.gradle.kts", kind: "build", pattern: /(versionCode = )(\d+)()/g, count: 1 },
  { file: "examples/compose/HelloGlass/app/build.gradle.kts", kind: "version", pattern: /(versionName = ")([^"]*)(")/g, count: 1 },
  { file: "examples/compose/HelloGlass/app/build.gradle.kts", kind: "build", pattern: /(versionCode = )(\d+)()/g, count: 1 },
  {
    file: "examples/compose/HelloGlass/app/build.gradle.kts",
    kind: "version",
    pattern: new RegExp(`(io\\.github\\.rookasrudzianskas:liquid-glass-compose:)(${VERSION})()`, "g"),
    count: 1,
  },
  { file: "examples/compose/HelloGlass/gradle/libs.versions.toml", kind: "version", pattern: /(liquidGlass = ")([^"]*)(")/g, count: 1 },
  {
    file: "examples/compose/HelloGlass/settings.gradle.kts",
    kind: "version",
    pattern: new RegExp(`(io\\.github\\.rookasrudzianskas:liquid-glass-compose:)(${VERSION})()`, "g"),
    count: 1,
  },
  // Windows
  { file: "platforms/windows/Directory.Build.props", kind: "version", pattern: /(<Version>)([^<]*)(<\/Version>)/g, count: 1 },
  { file: "platforms/windows/src/LiquidGlass.Studio/app.manifest", kind: "assembly", pattern: /(<assemblyIdentity version=")([^"]*)(")/g, count: 1 },
  { file: "examples/wpf/HelloGlass/app.manifest", kind: "assembly", pattern: /(<assemblyIdentity version=")([^"]*)(")/g, count: 1 },
  { file: "examples/wpf/HelloGlass/HelloGlass.csproj", kind: "version", pattern: /(<Version>)([^<]*)(<\/Version>)/g, count: 1 },
  {
    file: "examples/wpf/HelloGlass/HelloGlass.csproj",
    kind: "version",
    pattern: /(<PackageReference Include="LiquidGlassEngineering\.Wpf" Version=")([^"]*)(")/g,
    count: 1,
  },
  // Apple (the Studio's project file is generated from project.yml; both are written)
  { file: "platforms/apple/Studio/project.yml", kind: "version", pattern: /(MARKETING_VERSION: ")([^"]*)(")/g, count: 1 },
  { file: "platforms/apple/Studio/project.yml", kind: "build", pattern: /(CURRENT_PROJECT_VERSION: ")(\d+)(")/g, count: 1 },
  { file: "platforms/apple/Studio/LiquidGlassStudio.xcodeproj/project.pbxproj", kind: "version", pattern: /(MARKETING_VERSION = )([^;]*)(;)/g, count: 2 },
  { file: "platforms/apple/Studio/LiquidGlassStudio.xcodeproj/project.pbxproj", kind: "build", pattern: /(CURRENT_PROJECT_VERSION = )(\d+)(;)/g, count: 2 },
  { file: "examples/swiftui/HelloGlass/HelloGlass.xcodeproj/project.pbxproj", kind: "version", pattern: /(MARKETING_VERSION = )([^;]*)(;)/g, count: 2 },
  { file: "examples/swiftui/HelloGlass/HelloGlass.xcodeproj/project.pbxproj", kind: "build", pattern: /(CURRENT_PROJECT_VERSION = )(\d+)(;)/g, count: 2 },
  // Agent skills plugin, website and scripts
  { file: "plugins/liquid-glass/.claude-plugin/plugin.json", kind: "version", pattern: /^(  "version": ")([^"]*)(")/gm, count: 1 },
  { file: "website/package.json", kind: "version", pattern: /^(  "version": ")([^"]*)(")/gm, count: 1 },
  { file: "website/package-lock.json", kind: "version", pattern: /("name": "liquid-glass-engineering-site",\s*"version": ")([^"]*)(")/g, count: 2 },
  { file: "scripts/build-downloads.mjs", kind: "version", pattern: /(const DEFAULT_VERSION = ")([^"]*)(")/g, count: 1 },
  { file: "scripts/build-downloads.mjs", kind: "version", pattern: /(LG_VERSION +release version \(default )([^)]*)(\))/g, count: 1 },
  { file: "website/scripts/content.mjs", kind: "version", pattern: /(manifest\?\.version \?\? ")([^"]*)(")/g, count: 1 },
  { file: "website/scripts/content.mjs", kind: "version", pattern: /(schema: 1, version: ")([^"]*)(")/g, count: 1 },
  // Repository
  { file: "README.md", kind: "version", pattern: new RegExp(`(img\\.shields\\.io/badge/version-)(${VERSION})(-blue)`, "g"), count: 1 },
  { file: "README.md", kind: "version", pattern: new RegExp(`(alt="Version )(${VERSION})(")`, "g"), count: 1 },
  { file: ".github/ISSUE_TEMPLATE/bug_report.yml", kind: "version", pattern: /(label: Library version\n(?:.*\n)*?\s*placeholder: ")([^"]*)(")/g, count: 1 },
];

// Install coordinates in Markdown: each must name the release version (checked, never rewritten).
const SNIPPETS = [
  new RegExp(`io\\.github\\.rookasrudzianskas:liquid-glass-compose:(${VERSION})`, "g"),
  new RegExp(`com\\.github\\.rookasrudzianskas:liquid-glass-mac-windows-ios-android:(${VERSION})`, "g"),
  new RegExp(`liquid-glass-compose-(${VERSION})\\.aar`, "g"),
  new RegExp(`from: "(${VERSION})"`, "g"),
  new RegExp(`Include="LiquidGlassEngineering\\.Wpf" Version="(${VERSION})"`, "g"),
  new RegExp(`LiquidGlassEngineering\\.Wpf --version (${VERSION})`, "g"),
  new RegExp(`liquidGlass = "(${VERSION})"`, "g"),
  new RegExp(`img\\.shields\\.io/badge/version-(${VERSION})-blue`, "g"),
];
// History and examples of earlier versions.
const MARKDOWN_SKIPPED = new Set(["CHANGELOG.md", "CONTRIBUTING.md", "SECURITY.md"]);
const RELEASE_NOTES = { file: "platforms/windows/Directory.Build.props", pattern: /<PackageReleaseNotes>([\s\S]*?)<\/PackageReleaseNotes>/ };

const USAGE = `Usage:
  node scripts/set-version.mjs <version> [--build <n>] [--root <dir>]
  node scripts/set-version.mjs --check [--root <dir>]`;

function usage(message) {
  if (message) console.error(`set-version: ${message}`);
  console.error(USAGE);
  process.exit(2);
}

const opts = { check: false, version: null, build: null, root: resolve(dirname(fileURLToPath(import.meta.url)), "..") };
const argv = process.argv.slice(2);
for (let i = 0; i < argv.length; i++) {
  const arg = argv[i];
  if (arg === "--check") opts.check = true;
  else if (arg === "--build") opts.build = argv[++i] ?? usage("--build needs a number");
  else if (arg === "--root") opts.root = resolve(argv[++i] ?? usage("--root needs a folder"));
  else if (arg === "-h" || arg === "--help") {
    console.log(USAGE);
    process.exit(0);
  } else if (!arg.startsWith("-") && opts.version === null) opts.version = arg;
  else usage(`unknown argument: ${arg}`);
}
if (opts.check === (opts.version !== null)) usage(opts.check ? "--check takes no version" : "give a version or --check");
if (opts.check && opts.build !== null) usage("--check takes no --build");
if (opts.version !== null && !VERSION_ONLY.test(opts.version)) usage(`not a release version (x.y.z or x.y.z-pre.n): ${opts.version}`);
if (opts.build !== null && !/^[1-9]\d*$/.test(opts.build)) usage(`not a build number: ${opts.build}`);

const texts = new Map(); // file -> contents
const read = (file) => {
  if (!texts.has(file)) {
    const abs = join(opts.root, file);
    if (!existsSync(abs)) {
      console.error(`set-version: ${file} is missing (update PLACES in scripts/set-version.mjs if it moved)`);
      process.exit(2);
    }
    texts.set(file, readFileSync(abs, "utf8"));
  }
  return texts.get(file);
};
const lineAt = (text, index) => text.slice(0, index).split("\n").length;
const assemblyOf = (version) => `${version.split("-")[0]}.0`;
// The release version a place holds (an assembly version 1.0.2.0 stands for 1.0.2).
const asVersion = (place, value) => (place.kind === "assembly" ? value.replace(/\.0$/, "") : value);

// Every match of every place: { place, value, line }.
const found = [];
const layout = [];
for (const place of PLACES) {
  const text = read(place.file);
  const matches = [...text.matchAll(place.pattern)];
  if (matches.length !== place.count) layout.push(`${place.file}: expected ${place.count} match(es) of ${place.pattern}, found ${matches.length}`);
  for (const m of matches) found.push({ place, value: m[2], line: lineAt(text, m.index + m[1].length) });
}
if (layout.length) {
  for (const message of layout) console.error(`error: ${message}`);
  console.error("set-version: a file no longer has the expected layout: update PLACES in scripts/set-version.mjs");
  process.exit(2);
}

const versions = found.filter((f) => f.place.kind !== "build");
const builds = found.filter((f) => f.place.kind === "build");
const tally = (list, value) => {
  const counts = new Map();
  for (const f of list) counts.set(value(f), (counts.get(value(f)) ?? 0) + 1);
  return [...counts].sort((a, b) => b[1] - a[1]);
};
const versionTally = tally(versions, (f) => asVersion(f.place, f.value));
const buildTally = tally(builds, (f) => f.value);
const current = versionTally[0][0];
const where = (f) => `${f.place.file}:${f.line}`;

// Tracked Markdown files (or, outside a git checkout, the ones below the usual folders) other than the history.
function markdownFiles() {
  try {
    return execFileSync("git", ["-C", opts.root, "ls-files", "-z", "--", "*.md"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] })
      .split("\0")
      .filter((f) => f && !MARKDOWN_SKIPPED.has(f) && existsSync(join(opts.root, f)));
  } catch {
    const out = [];
    const walk = (dir) => {
      for (const entry of readdirSync(join(opts.root, dir), { withFileTypes: true })) {
        const rel = dir ? `${dir}/${entry.name}` : entry.name;
        if (entry.isDirectory() && !/^(\.|node_modules$|build$|dist$|bin$|obj$|artifacts$|public$)/.test(entry.name)) walk(rel);
        else if (entry.isFile() && entry.name.endsWith(".md") && !MARKDOWN_SKIPPED.has(rel)) out.push(rel);
      }
    };
    walk("");
    return out.sort();
  }
}

// Install coordinates in Markdown that name another version than `version`.
function snippetProblems(version) {
  const problems = [];
  for (const file of markdownFiles()) {
    const text = readFileSync(join(opts.root, file), "utf8");
    for (const pattern of SNIPPETS) {
      for (const m of text.matchAll(pattern)) {
        if (m[1] !== version) problems.push(`${file}:${lineAt(text, m.index)}: ${m[0]}`);
      }
    }
  }
  return problems;
}

// Versions the Windows release notes name other than `version`.
function releaseNotesProblems(version) {
  const notes = RELEASE_NOTES.pattern.exec(read(RELEASE_NOTES.file))?.[1] ?? "";
  return [...notes.matchAll(new RegExp(`(?<![\\d.])${VERSION}(?![\\d.]*\\d)`, "g"))]
    .map((m) => m[0])
    .filter((v) => v !== version)
    .map((v) => `${RELEASE_NOTES.file}: the PackageReleaseNotes name ${v}`);
}

if (opts.check) {
  const errors = [];
  if (versionTally.length > 1) {
    errors.push(`the release version differs between places (${versionTally.map(([v, n]) => `${v} in ${n}`).join(", ")}):`);
    for (const f of versions) if (asVersion(f.place, f.value) !== current) errors.push(`  ${where(f)}: ${f.value} (most places say ${current})`);
  }
  for (const f of versions) {
    if (f.place.kind === "assembly" ? f.value !== assemblyOf(asVersion(f.place, f.value)) || !VERSION_ONLY.test(asVersion(f.place, f.value)) : !VERSION_ONLY.test(f.value)) {
      errors.push(`${where(f)}: ${f.value} is not a release version${f.place.kind === "assembly" ? " in its four-part form (x.y.z.0)" : ""}`);
    }
  }
  if (buildTally.length > 1) {
    errors.push(`the build number differs between places (${buildTally.map(([v, n]) => `${v} in ${n}`).join(", ")}):`);
    for (const f of builds) if (f.value !== buildTally[0][0]) errors.push(`  ${where(f)}: ${f.value} (most places say ${buildTally[0][0]})`);
  }
  const snippets = snippetProblems(current);
  if (snippets.length) errors.push(`install snippets in Markdown name another version than ${current}:`, ...snippets.map((s) => `  ${s}`));
  errors.push(...releaseNotesProblems(current));
  if (errors.length) {
    for (const message of errors) console.error(message.startsWith("  ") ? message : `error: ${message}`);
    console.error(`set-version: run node scripts/set-version.mjs <version> to set every place (CONTRIBUTING.md, Releasing, step 0)`);
    process.exit(1);
  }
  const files = new Set(found.map((f) => f.place.file));
  console.log(`versions ok: ${current} in ${versions.length} places, build ${buildTally[0][0]} in ${builds.length}, across ${files.size} files; Markdown install snippets agree`);
  process.exit(0);
}

// Set mode.
if (buildTally.length > 1 && opts.build === null) {
  console.error(`set-version: the build numbers disagree (${buildTally.map(([v, n]) => `${v} in ${n}`).join(", ")}): pass --build <n>`);
  process.exit(1);
}
const build = opts.build ?? String(Number(buildTally[0][0]) + (opts.version === current && versionTally.length === 1 ? 0 : 1));
const changed = [];
for (const place of PLACES) {
  const value = place.kind === "build" ? build : place.kind === "assembly" ? assemblyOf(opts.version) : opts.version;
  const before = read(place.file);
  const after = before.replace(place.pattern, (_m, head, _old, tail) => `${head}${value}${tail}`);
  if (after !== before) {
    texts.set(place.file, after);
    if (!changed.includes(place.file)) changed.push(place.file);
  }
}
for (const file of changed) writeFileSync(join(opts.root, file), texts.get(file));
console.log(changed.length ? `version ${opts.version}, build ${build}: wrote ${changed.length} file(s)` : `version ${opts.version}, build ${build}: every place already says so`);
for (const file of changed) console.log(`  ${file}`);

// What is left to edit by hand.
const previous = versionTally.map(([v]) => v).filter((v) => v !== opts.version);
const mentions = [];
if (previous.length) {
  const named = new RegExp(`(?<![\\d.])(?:${previous.map((v) => v.replace(/\./g, "\\.")).join("|")})(?![\\d]|\\.\\d)`);
  for (const file of markdownFiles()) {
    readFileSync(join(opts.root, file), "utf8")
      .split("\n")
      .forEach((line, i) => named.test(line) && mentions.push(`  ${file}:${i + 1}: ${line.trim().slice(0, 160)}`));
  }
}
console.log("");
console.log("Edit by hand:");
console.log(`  CHANGELOG.md: a ## [${opts.version}] section with the release date, and its link at the end`);
console.log(`  ${RELEASE_NOTES.file}: PackageReleaseNotes for ${opts.version}`);
if (mentions.length) {
  console.log(`Markdown lines that name ${previous.join(" or ")} (install snippets and the versions the guides and skills describe):`);
  for (const line of mentions) console.log(line);
}
console.log("");
console.log("Then run node scripts/set-version.mjs --check.");
