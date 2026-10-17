// Copies the optical spec, the license and the notices into the agent skills so installed skills are
// self-contained and keep their attribution (the plugin folder and every skill folder carry LICENSE and NOTICE.md).
// Relative Markdown links in the copies are rewritten: to the bundled file when the skill carries the target,
// otherwise to the file on GitHub, so no installed skill has a broken link.
// Run after changing anything in spec/, LICENSE or NOTICE.md: node scripts/sync-skill-references.mjs
// With --check it fails instead of writing when a copy is stale (used in CI).
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, posix } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const plugin = "plugins/liquid-glass";
const repositoryBlob = "https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/";

// [source in the repository, copy relative to the plugin folder]
const copies = [
  ["spec/OPTICS.md", "skills/liquid-glass/references/optics.md"],
  ["spec/OPTICS.md", "skills/liquid-glass-port/references/OPTICS.md"],
  ["spec/COMPONENTS.md", "skills/liquid-glass-port/references/COMPONENTS.md"],
  ["spec/reference.mjs", "skills/liquid-glass-port/references/reference.mjs"],
  ["spec/shaders/liquid-glass.glsl", "skills/liquid-glass-port/references/liquid-glass.glsl"],
  ["spec/vectors.json", "skills/liquid-glass-port/references/vectors.json"],
  ["LICENSE", "LICENSE"],
  ["NOTICE.md", "NOTICE.md"],
];
const skillsDir = join(root, plugin, "skills");
for (const name of readdirSync(skillsDir).sort()) {
  if (!existsSync(join(skillsDir, name, "SKILL.md"))) continue;
  copies.push(["LICENSE", `skills/${name}/LICENSE`], ["NOTICE.md", `skills/${name}/NOTICE.md`]);
}

// The folder a copy belongs to: its skill folder, or the plugin folder for top-level copies.
const owner = (to) => (to.startsWith("skills/") ? to.split("/").slice(0, 2).join("/") : "");

// Rewrites relative links of a Markdown copy so they resolve where the copy lives.
function relink(text, from, to) {
  return text.replace(/\]\(([^)\s]+)\)/g, (link, target) => {
    if (/^([a-z][a-z0-9+.-]*:|#|\/)/i.test(target)) return link;
    const hash = target.indexOf("#");
    const [file, anchor] = hash < 0 ? [target, ""] : [target.slice(0, hash), target.slice(hash)];
    const resolved = posix.normalize(posix.join(posix.dirname(from), file));
    const bundled = copies.find(([source, copy]) => source === resolved && owner(copy) === owner(to));
    const href = bundled ? posix.relative(posix.dirname(to), bundled[1]) : repositoryBlob + resolved;
    return `](${href}${anchor})`;
  });
}

function expected(from, to) {
  const bytes = readFileSync(join(root, from));
  return from.endsWith(".md") ? Buffer.from(relink(bytes.toString("utf8"), from, to), "utf8") : bytes;
}

const check = process.argv.includes("--check");
let stale = 0;
for (const [from, to] of copies) {
  const target = join(root, plugin, to);
  const want = expected(from, to);
  if (existsSync(target) && readFileSync(target).equals(want)) continue;
  if (check) {
    console.error(`stale: ${plugin}/${to} (from ${from})`);
    stale++;
    continue;
  }
  mkdirSync(dirname(target), { recursive: true });
  writeFileSync(target, want);
  console.log(`copied ${from} -> ${plugin}/${to}`);
}
if (stale) {
  console.error("Run: node scripts/sync-skill-references.mjs");
  process.exit(1);
}
