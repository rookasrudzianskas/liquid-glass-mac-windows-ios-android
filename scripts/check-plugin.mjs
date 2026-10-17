#!/usr/bin/env node
// Validates the plugin marketplace and the agent skills (used in CI):
//   * .claude-plugin/marketplace.json and every listed plugin's .claude-plugin/plugin.json parse, and each
//     plugin source folder exists; plugin.json's version is the release version of scripts/build-downloads.mjs.
//   * Every skills/<name>/ folder has a SKILL.md whose frontmatter name equals the folder name
//     (lowercase letters, digits and hyphens, at most 64 characters) and whose description is 1 to 1024
//     characters without markup.
//   * Every skill a SKILL.md names (as `liquid-glass…` in backticks) and every EXPECTED_SKILLS entry of
//     scripts/build-downloads.mjs exists.
//   * Every relative link in the plugin's Markdown files resolves to a file.
// Usage: node scripts/check-plugin.mjs [--root <dir>]
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { basename, dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const args = process.argv.slice(2);
const rootArg = args.indexOf("--root");
const root = resolve(rootArg >= 0 ? args[rootArg + 1] : join(dirname(fileURLToPath(import.meta.url)), ".."));
const errors = [];
const error = (message) => errors.push(message);
const rel = (abs) => relative(root, abs) || ".";

function readJson(abs) {
  try {
    return JSON.parse(readFileSync(abs, "utf8"));
  } catch (err) {
    error(`${rel(abs)}: ${err.code === "ENOENT" ? "missing" : `invalid JSON (${err.message})`}`);
    return null;
  }
}

// The frontmatter fields this check needs: plain, quoted and folded (`>`, `|`) scalars.
function frontmatter(text) {
  const match = /^---\r?\n([\s\S]*?)\r?\n---\r?\n/.exec(text);
  if (!match) return null;
  const lines = match[1].split(/\r?\n/);
  const fields = {};
  for (let i = 0; i < lines.length; i++) {
    const field = /^([A-Za-z0-9_-]+):[ \t]*(.*)$/.exec(lines[i]);
    if (!field) continue;
    let value = field[2].trim();
    if (/^[>|][+-]?$/.test(value)) {
      const block = [];
      while (i + 1 < lines.length && (/^\s/.test(lines[i + 1]) || lines[i + 1] === "")) block.push(lines[++i].trim());
      value = block.join(value.startsWith(">") ? " " : "\n").trim();
    } else if (/^"(.*)"$/.test(value)) {
      value = JSON.parse(value);
    } else if (/^'(.*)'$/.test(value)) {
      value = value.slice(1, -1).replace(/''/g, "'");
    }
    fields[field[1]] = value;
  }
  return fields;
}

function constantFrom(source, name) {
  const match = new RegExp(`const ${name} = ([^;]+);`).exec(source);
  return match ? match[1] : null;
}

// Marketplace and plugins
const marketplaceFile = join(root, ".claude-plugin/marketplace.json");
const marketplace = readJson(marketplaceFile);
const downloadsScript = readFileSync(join(root, "scripts/build-downloads.mjs"), "utf8");
const releaseVersion = JSON.parse(constantFrom(downloadsScript, "DEFAULT_VERSION") ?? "null");
const expectedSkills = [...(constantFrom(downloadsScript, "EXPECTED_SKILLS") ?? "").matchAll(/"([^"]+)"/g)].map((m) => m[1]);
if (!releaseVersion) error("scripts/build-downloads.mjs: DEFAULT_VERSION not found");
if (!expectedSkills.length) error("scripts/build-downloads.mjs: EXPECTED_SKILLS not found");

const plugins = [];
if (marketplace) {
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(marketplace.name ?? "")) error(`${rel(marketplaceFile)}: name must be kebab-case`);
  if (!marketplace.owner?.name) error(`${rel(marketplaceFile)}: owner.name is required`);
  if (!Array.isArray(marketplace.plugins) || !marketplace.plugins.length) error(`${rel(marketplaceFile)}: no plugins`);
  for (const entry of marketplace.plugins ?? []) {
    const dir = join(root, entry.source ?? "");
    if (typeof entry.source !== "string" || !existsSync(dir) || !statSync(dir).isDirectory()) {
      error(`${rel(marketplaceFile)}: plugin "${entry.name}" source ${entry.source} is not a folder`);
      continue;
    }
    const manifestFile = join(dir, ".claude-plugin/plugin.json");
    const manifest = readJson(manifestFile);
    if (!manifest) continue;
    if (manifest.name !== entry.name) error(`${rel(manifestFile)}: name "${manifest.name}" differs from the marketplace entry "${entry.name}"`);
    if (manifest.version !== releaseVersion) error(`${rel(manifestFile)}: version ${manifest.version} is not the release version ${releaseVersion}`);
    plugins.push({ dir, manifest });
  }
}

// Skills
const reserved = new Set([marketplace?.name, ...plugins.map((p) => basename(String(p.manifest.repository ?? "")))]);
const skills = new Set();
const named = [];
const markdown = [];
for (const { dir } of plugins) {
  const skillsDir = join(dir, "skills");
  for (const name of existsSync(skillsDir) ? readdirSync(skillsDir).sort() : []) {
    const skillDir = join(skillsDir, name);
    if (!statSync(skillDir).isDirectory()) continue;
    const skillFile = join(skillDir, "SKILL.md");
    if (!existsSync(skillFile)) {
      error(`${rel(skillDir)}: no SKILL.md`);
      continue;
    }
    skills.add(name);
    const text = readFileSync(skillFile, "utf8");
    const front = frontmatter(text);
    if (!front) {
      error(`${rel(skillFile)}: no frontmatter`);
      continue;
    }
    if (front.name !== name) error(`${rel(skillFile)}: name "${front.name}" differs from the folder name "${name}"`);
    if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(name) || name.length > 64) error(`${rel(skillDir)}: invalid skill name`);
    const description = front.description ?? "";
    if (!description) error(`${rel(skillFile)}: no description`);
    if (description.length > 1024) error(`${rel(skillFile)}: description is ${description.length} characters (at most 1024)`);
    if (/<\/?[A-Za-z][^>]*>/.test(description)) error(`${rel(skillFile)}: description contains markup`);
    for (const m of text.matchAll(/`(liquid-glass(?:-[a-z0-9]+)*)`/g)) {
      if (!reserved.has(m[1])) named.push([m[1], rel(skillFile)]);
    }
  }
  const walk = (abs) => {
    for (const entry of readdirSync(abs, { withFileTypes: true })) {
      const child = join(abs, entry.name);
      if (entry.isDirectory()) walk(child);
      else if (entry.name.endsWith(".md")) markdown.push(child);
    }
  };
  walk(dir);
}
for (const [name, where] of named) if (!skills.has(name)) error(`${where}: names the skill ${name}, which does not exist`);
for (const name of expectedSkills) if (!skills.has(name)) error(`scripts/build-downloads.mjs: expected skill ${name} does not exist`);

// Relative links (outside code)
for (const file of markdown) {
  const prose = readFileSync(file, "utf8")
    .replace(/^(```|~~~)[^\n]*\n[\s\S]*?^\1[ \t]*$/gm, "")
    .replace(/`[^`\n]*`/g, "");
  for (const m of prose.matchAll(/\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g)) {
    const target = m[1];
    if (/^([a-z][a-z0-9+.-]*:|#)/i.test(target)) continue;
    const path = decodeURIComponent(target.split("#")[0]);
    if (path.startsWith("/") || !existsSync(join(dirname(file), path))) error(`${rel(file)}: broken link ${target}`);
  }
}

if (errors.length) {
  for (const message of errors) console.error(`error: ${message}`);
  process.exit(1);
}
console.log(`plugin ok: ${plugins.length} plugin(s), ${skills.size} skills (${[...skills].join(", ")}), ${markdown.length} Markdown files`);
