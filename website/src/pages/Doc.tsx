import { useEffect, useMemo, useState } from "react";
import type { MouseEvent } from "react";
import { Link, handleLinkClick, useRouter } from "../router";
import type { Head, PageProps } from "../router";
import { docs, groupedDocs } from "../docs";
import type { DocPage } from "../docs";
import { copyText } from "../components/Code";
import { ArrowRightIcon, ChevronDownIcon, GitHubIcon } from "../components/Icons";

const loaders = import.meta.glob<{ default: DocPage }>("../generated/docs/*.json");

interface Data {
  doc: DocPage;
  modules: string[];
}

export async function load(params: Record<string, string>): Promise<Data | null> {
  const loader = loaders[`../generated/docs/${params.slug}.json`];
  if (!loader) return null;
  const doc = (await loader()).default;
  return { doc, modules: [`src/generated/docs/${params.slug}.json`] };
}

export const head = (data: Data): Head => ({
  title: data.doc.title,
  description: data.doc.description || `${data.doc.title}: Liquid Glass Engineering documentation.`,
  path: `/docs/${data.doc.slug}`,
});

export function DocsSidebar({ current }: { current?: string }) {
  return (
    <nav className="docs-nav" aria-label="Documentation">
      <Link href="/docs" className="docs-nav__home" data-active={current === undefined ? "" : undefined}>
        Overview
      </Link>
      {groupedDocs().map(([group, list]) => (
        <div key={group} className="docs-nav__group">
          <h2 className="docs-nav__title">{group}</h2>
          <ul role="list">
            {list.map((d) => (
              <li key={d.slug}>
                <Link href={`/docs/${d.slug}`} className="docs-nav__link" data-active={current === d.slug ? "" : undefined}>
                  {d.nav}
                </Link>
              </li>
            ))}
          </ul>
        </div>
      ))}
      <div className="docs-nav__group">
        <h2 className="docs-nav__title">For agents</h2>
        <ul role="list">
          <li>
            <Link href="/skills" className="docs-nav__link">
              Agent Skills
            </Link>
          </li>
          <li>
            <a href="/llms.txt" className="docs-nav__link">
              llms.txt
            </a>
          </li>
          <li>
            <a href="/llms-full.txt" className="docs-nav__link">
              llms-full.txt
            </a>
          </li>
        </ul>
      </div>
    </nav>
  );
}

function useActiveHeading(ids: string[]) {
  const [active, setActive] = useState<string | null>(null);
  useEffect(() => {
    if (!ids.length) return;
    const els = ids.map((id) => document.getElementById(id)).filter((el): el is HTMLElement => Boolean(el));
    const visible = new Map<string, boolean>();
    const observer = new IntersectionObserver(
      (entries) => {
        for (const e of entries) visible.set(e.target.id, e.isIntersecting);
        const first = els.find((el) => visible.get(el.id));
        if (first) setActive(first.id);
        else {
          // Above the first visible heading: pick the last one scrolled past.
          const passed = els.filter((el) => el.getBoundingClientRect().top < 120);
          setActive(passed[passed.length - 1]?.id ?? null);
        }
      },
      { rootMargin: "-80px 0px -60% 0px" },
    );
    for (const el of els) observer.observe(el);
    return () => observer.disconnect();
  }, [ids]);
  return active;
}

const formatDate = (iso: string | null) => {
  if (!iso) return null;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? null : d.toLocaleDateString("en", { year: "numeric", month: "long", day: "numeric", timeZone: "UTC" });
};

export default function Doc({ data }: PageProps<Data>) {
  const { doc } = data;
  const { navigate } = useRouter();
  const ids = useMemo(() => doc.toc.map((t) => t.id), [doc]);
  const active = useActiveHeading(ids);
  const index = docs.findIndex((d) => d.slug === doc.slug);
  const prev = index > 0 ? docs[index - 1] : undefined;
  const next = index >= 0 && index < docs.length - 1 ? docs[index + 1] : undefined;
  const updated = formatDate(doc.updated);

  const onProseClick = async (e: MouseEvent<HTMLDivElement>) => {
    const target = e.target as HTMLElement;
    const copy = target.closest<HTMLButtonElement>("[data-copy]");
    if (copy) {
      const pre = copy.closest(".code")?.querySelector("pre");
      if (pre && (await copyText(pre.textContent ?? ""))) {
        copy.dataset.copied = "";
        copy.textContent = "Copied";
        window.setTimeout(() => {
          delete copy.dataset.copied;
          copy.textContent = "Copy";
        }, 1600);
      }
      return;
    }
    const anchor = target.closest<HTMLAnchorElement>("a[href]");
    if (anchor) handleLinkClick(e, anchor, navigate);
  };

  return (
    <div className="docs">
      <aside className="docs__sidebar">
        <details className="docs__menu">
          <summary>
            <span>{doc.nav}</span>
            <ChevronDownIcon size={18} />
          </summary>
          <DocsSidebar current={doc.slug} />
        </details>
        <div className="docs__sidebar-inner">
          <DocsSidebar current={doc.slug} />
        </div>
      </aside>

      <article className="docs__article" aria-labelledby="doc-title">
        <header className="docs__header">
          <p className="docs__group">{doc.group}</p>
          <h1 id="doc-title" className="docs__title">
            {doc.title}
          </h1>
          {doc.description ? <p className="docs__description">{doc.description}</p> : null}
          <p className="docs__meta">
            <a href={doc.sourceUrl} rel="noopener" target="_blank">
              <GitHubIcon size={14} /> {doc.source}
            </a>
            {updated ? <span>Updated {updated}</span> : null}
            <a href={`/docs/${doc.slug}.md`}>Markdown</a>
          </p>
        </header>
        {/* Rendered at build time from the repository's Markdown. */}
        {/* eslint-disable-next-line jsx-a11y/click-events-have-key-events, jsx-a11y/no-static-element-interactions */}
        <div className="prose" onClick={onProseClick} dangerouslySetInnerHTML={{ __html: doc.html }} />
        <nav className="docs__pager" aria-label="Previous and next">
          {prev ? (
            <Link href={`/docs/${prev.slug}`} className="docs__pager-link">
              <span>Previous</span>
              {prev.nav}
            </Link>
          ) : (
            <span />
          )}
          {next ? (
            <Link href={`/docs/${next.slug}`} className="docs__pager-link docs__pager-link--next">
              <span>Next</span>
              {next.nav} <ArrowRightIcon size={16} />
            </Link>
          ) : null}
        </nav>
      </article>

      {doc.toc.length > 1 ? (
        <nav className="docs__toc" aria-label="On this page">
          <p className="docs__toc-title">On this page</p>
          <ul role="list">
            {doc.toc.map((t) => (
              <li key={t.id} data-depth={t.depth}>
                <a href={`#${t.id}`} aria-current={active === t.id ? "location" : undefined}>
                  {t.text}
                </a>
              </li>
            ))}
          </ul>
        </nav>
      ) : null}
    </div>
  );
}
