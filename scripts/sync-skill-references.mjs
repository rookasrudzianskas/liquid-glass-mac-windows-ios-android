// Copies the optical spec into the agent skills so installed skills are self-contained.
// Run after changing anything in spec/: node scripts/sync-skill-references.mjs
// With --check it fails instead of writing when a copy is stale (used in CI).
import { copyFileSync, existsSync, mkdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const skills = join(root, "plugins/liquid-glass/skills");
const copies = [
  ["spec/OPTICS.md", "liquid-glass/references/optics.md"],
  ["spec/OPTICS.md", "liquid-glass-port/references/OPTICS.md"],
  ["spec/COMPONENTS.md", "liquid-glass-port/references/COMPONENTS.md"],
  ["spec/reference.mjs", "liquid-glass-port/references/reference.mjs"],
  ["spec/shaders/liquid-glass.glsl", "liquid-glass-port/references/liquid-glass.glsl"],
  ["spec/vectors.json", "liquid-glass-port/references/vectors.json"],
];
const check = process.argv.includes("--check");
let stale = 0;
for (const [from, to] of copies) {
  const source = join(root, from);
  const target = join(skills, to);
  const same = existsSync(target) && readFileSync(source).equals(readFileSync(target));
  if (same) continue;
  if (check) {
    console.error(`stale: ${to} (from ${from})`);
    stale++;
    continue;
  }
  mkdirSync(dirname(target), { recursive: true });
  copyFileSync(source, target);
  console.log(`copied ${from} -> plugins/liquid-glass/skills/${to}`);
}
if (stale) {
  console.error("Run: node scripts/sync-skill-references.mjs");
  process.exit(1);
}
