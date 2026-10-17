import docsIndex from "./generated/docs-index.json";
import site from "./generated/site.json";
import examples from "./content/examples.json";

export interface DocMeta {
  slug: string;
  title: string;
  nav: string;
  group: string;
  order: number;
  description: string;
  source: string;
  updated: string | null;
}

export interface TocEntry {
  depth: number;
  id: string;
  text: string;
}

export interface DocPage extends DocMeta {
  sourceUrl: string;
  /** True when the description comes from front matter (not the first paragraph). */
  lede: boolean;
  toc: TocEntry[];
  html: string;
}

export const docs = docsIndex as DocMeta[];

export const hasDoc = (slug: string) => docs.some((d) => d.slug === slug);

/** Link to a doc page when it exists, otherwise to the docs index (never a 404). */
export const docHref = (slug: string) => (hasDoc(slug) ? `/docs/${slug}` : "/docs");

/** The Hello, Glass example apps, one per native toolkit, on GitHub (src/content/examples.json). */
export const EXAMPLES = examples.items.map((e) => ({ ...e, url: `${site.repoUrl}/tree/main/${e.path}` }));
export const EXAMPLES_SUMMARY = examples.summary;

export function groupedDocs() {
  const groups = new Map<string, DocMeta[]>();
  for (const d of docs) {
    const list = groups.get(d.group) ?? [];
    list.push(d);
    groups.set(d.group, list);
  }
  return [...groups.entries()];
}
