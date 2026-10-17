import { Link } from "../router";
import type { Head } from "../router";
import { groupedDocs, hasDoc } from "../docs";
import { DocsSidebar } from "./Doc";
import { ArrowRightIcon, BookIcon, LaptopIcon, AndroidIcon, WindowIcon, TerminalIcon } from "../components/Icons";

const PLATFORM_CARDS = [
  { slug: "apple", title: "SwiftUI", detail: "iOS 17+ and macOS 14+, Xcode 16+, Metal", Icon: LaptopIcon },
  { slug: "android", title: "Jetpack Compose", detail: "Android 7+, glass on 13+, AGSL", Icon: AndroidIcon },
  { slug: "windows", title: "WPF", detail: ".NET 8+ or .NET Framework 4.8, Windows 10+, HLSL", Icon: WindowIcon },
];

export default function DocsIndex() {
  // Platform guides already have a card above; list every other doc by group.
  const carded = new Set(PLATFORM_CARDS.filter((c) => hasDoc(c.slug)).map((c) => c.slug));
  const groups = groupedDocs()
    .map(([group, list]) => [group, list.filter((d) => !carded.has(d.slug))] as const)
    .filter(([, list]) => list.length > 0);
  return (
    <div className="docs">
      <aside className="docs__sidebar">
        <div className="docs__sidebar-inner">
          <DocsSidebar />
        </div>
      </aside>
      <article className="docs__article docs__article--wide" aria-labelledby="docs-title">
        <header className="docs__header">
          <p className="docs__group">Documentation</p>
          <h1 id="docs-title" className="docs__title">
            Build with Liquid Glass
          </h1>
          <p className="docs__description">
            Platform guides for SwiftUI, Jetpack Compose and WPF, the optical specification every port implements, and the component contract they share.
          </p>
        </header>

        <ul className="doc-cards" role="list">
          {PLATFORM_CARDS.map(({ slug, title, detail, Icon }) => {
            const ready = hasDoc(slug);
            return (
              <li key={slug}>
                {ready ? (
                  <Link href={`/docs/${slug}`} className="doc-card">
                    <Icon size={22} />
                    <span className="doc-card__title">{title}</span>
                    <span className="doc-card__detail">{detail}</span>
                    <ArrowRightIcon size={18} className="doc-card__arrow" />
                  </Link>
                ) : (
                  <div className="doc-card doc-card--soon" aria-disabled="true">
                    <Icon size={22} />
                    <span className="doc-card__title">{title}</span>
                    <span className="doc-card__detail">{detail}</span>
                    <span className="doc-card__badge">Guide in progress</span>
                  </div>
                )}
              </li>
            );
          })}
        </ul>

        {groups.map(([group, list]) => (
          <section key={group} className="doc-group" aria-labelledby={`group-${group}`}>
            <h2 id={`group-${group}`} className="doc-group__title">
              {group}
            </h2>
            <ul role="list" className="doc-list">
              {list.map((d) => (
                <li key={d.slug}>
                  <Link href={`/docs/${d.slug}`} className="doc-list__item">
                    <BookIcon size={18} />
                    <span>
                      <span className="doc-list__title">{d.title}</span>
                      {d.description ? <span className="doc-list__desc">{d.description}</span> : null}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ))}

        <section className="doc-group" aria-labelledby="group-agents">
          <h2 id="group-agents" className="doc-group__title">
            For coding agents
          </h2>
          <ul role="list" className="doc-list">
            <li>
              <Link href="/skills" className="doc-list__item">
                <TerminalIcon size={18} />
                <span>
                  <span className="doc-list__title">Agent Skills</span>
                  <span className="doc-list__desc">A Claude Code plugin with design and platform skills, also usable from Codex and other Agent Skills tools.</span>
                </span>
              </Link>
            </li>
            <li>
              <a href="/llms.txt" className="doc-list__item">
                <BookIcon size={18} />
                <span>
                  <span className="doc-list__title">llms.txt and llms-full.txt</span>
                  <span className="doc-list__desc">Every page of these docs as plain Markdown for language models.</span>
                </span>
              </a>
            </li>
          </ul>
        </section>
      </article>
    </div>
  );
}

export const head = (): Head => ({
  title: "Documentation",
  description: "Guides for SwiftUI, Jetpack Compose and WPF, the optical specification and the component contract of Liquid Glass Engineering.",
  path: "/docs",
});
