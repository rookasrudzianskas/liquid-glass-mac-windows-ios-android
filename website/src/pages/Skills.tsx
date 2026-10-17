import skillsData from "../generated/skills.json";
import downloads from "../generated/downloads.json";
import code from "../generated/code.json";
import { CodeBlock } from "../components/Code";
import type { Highlighted } from "../components/Code";
import { REPO_URL } from "../router";
import type { Head } from "../router";
import { formatBytes } from "./Downloads";
import { BookIcon, DownloadIcon, GitHubIcon, SparkleIcon, TerminalIcon } from "../components/Icons";

interface Skill {
  name: string;
  folder: string;
  title: string;
  description: string;
  files: number;
  source: string;
}

interface Item {
  id: string;
  file: string | null;
  fileName: string | null;
  bytes: number | null;
  available: boolean;
}

const data = skillsData as { plugin: { name: string; version: string; description: string } | null; marketplace: { name: string } | null; hasAgentsMd: boolean; skills: Skill[] };
const items = (downloads as { items: Item[] }).items;
const samples = code as unknown as Record<string, Highlighted>;

const ROLES: Record<string, string> = {
  "liquid-glass": "Design and platform choice",
  "liquid-glass-swiftui": "SwiftUI on iOS and macOS",
  "liquid-glass-compose": "Jetpack Compose on Android",
  "liquid-glass-wpf": "WPF on Windows",
  "liquid-glass-port": "Port the optical model",
};

export default function Skills() {
  const all = items.find((i) => i.id === "skills");

  return (
    <div className="page skills">
      <header className="page-hero">
        <p className="eyebrow eyebrow--spectrum">Agent Skills</p>
        <h1 className="page-hero__title">Teach your agent Liquid Glass.</h1>
        <p className="lead">
          A Claude Code plugin with a design skill, one skill per platform and a porting skill. They follow the open Agent Skills format (a folder with a <code>SKILL.md</code>), so they also work in Codex and other tools that read skills.
        </p>
        <div className="page-hero__actions">
          {all?.available && all.file ? (
            <a className="button button--primary" href={all.file} download>
              <DownloadIcon size={18} />
              All skills (.zip{all.bytes ? `, ${formatBytes(all.bytes)}` : ""})
            </a>
          ) : null}
          <a className="button button--secondary" href={`${REPO_URL}/tree/main/plugins/liquid-glass`} rel="noopener" target="_blank">
            <GitHubIcon size={18} />
            View on GitHub
          </a>
        </div>
      </header>

      <div className="page__inner">
        <section className="skills-install" aria-labelledby="install-title">
          <h2 id="install-title" className="page-section__title">
            Install
          </h2>
          <div className="install-grid">
            <article className="install-card">
              <h3 className="install-card__title">
                <TerminalIcon size={18} /> Claude Code
              </h3>
              <p className="install-card__body">Add the marketplace and install the plugin from inside Claude Code:</p>
              {samples.pluginInstall ? <CodeBlock sample={samples.pluginInstall} title="Claude Code" /> : null}
              <p className="install-card__body">Or from your shell:</p>
              {samples.pluginShell ? <CodeBlock sample={samples.pluginShell} title="Terminal" /> : null}
            </article>
            <article className="install-card">
              <h3 className="install-card__title">
                <BookIcon size={18} /> Copy the folders
              </h3>
              <p className="install-card__body">
                Unzip the skills into <code>~/.claude/skills/</code> for Claude Code, or <code>~/.agents/skills/</code> for OpenAI Codex and other Agent Skills tools. Project-level folders work too.
              </p>
              {samples.skillsManual ? <CodeBlock sample={samples.skillsManual} title="Terminal" /> : null}
            </article>
            <article className="install-card">
              <h3 className="install-card__title">
                <SparkleIcon size={18} /> Claude apps
              </h3>
              <p className="install-card__body">
                In claude.ai, upload a skill as a zip whose root is the skill folder: one zip per skill, from the list below.
              </p>
            </article>
          </div>
        </section>

        <section aria-labelledby="skills-title">
          <h2 id="skills-title" className="page-section__title">
            The skills
          </h2>
          <ul role="list" className="skill-list">
            {data.skills.map((s) => {
              const dl = items.find((i) => i.id === `skill-${s.name}`);
              return (
                <li key={s.name} className="skill">
                  <div className="skill__head">
                    <code className="skill__name">{s.name}</code>
                    <span className="skill__role">{ROLES[s.name] ?? s.title}</span>
                  </div>
                  <p className="skill__desc">{s.description}</p>
                  <div className="skill__foot">
                    <span className="skill__files">
                      {s.files} {s.files === 1 ? "file" : "files"}
                    </span>
                    <a href={`${REPO_URL}/blob/main/${s.source}`} rel="noopener" target="_blank">
                      SKILL.md
                    </a>
                    {dl?.available && dl.file ? (
                      <a href={dl.file} download>
                        Download .zip{dl.bytes ? ` (${formatBytes(dl.bytes)})` : ""}
                      </a>
                    ) : null}
                  </div>
                </li>
              );
            })}
          </ul>
        </section>

        <section className="llms" aria-labelledby="llms-title">
          <h2 id="llms-title" className="page-section__title">
            Docs for language models
          </h2>
          <p className="install-card__body">
            The documentation is also published as plain text for agents that read it directly: <a href="/llms.txt">/llms.txt</a> is an index, <a href="/llms-full.txt">/llms-full.txt</a> holds every page, and each doc has a Markdown twin at <code>/docs/&lt;page&gt;.md</code>.
            {data.hasAgentsMd ? (
              <>
                {" "}
                The repository&apos;s <a href={`${REPO_URL}/blob/main/AGENTS.md`}>AGENTS.md</a> describes how agents should work inside it.
              </>
            ) : null}
          </p>
        </section>
      </div>
    </div>
  );
}

export const head = (): Head => ({
  title: "Agent Skills",
  description: "A Claude Code plugin with Liquid Glass design, SwiftUI, Jetpack Compose, WPF and porting skills, in the Agent Skills format for Codex and other agents. Plus llms.txt.",
  path: "/skills",
});
