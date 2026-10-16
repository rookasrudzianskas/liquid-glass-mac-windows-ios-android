import { renderToString } from "react-dom/server";
import { App } from "./App";
import { headTags } from "./head";
import { resolve } from "./router";
import docsIndex from "./generated/docs-index.json";

export interface Rendered {
  html: string;
  /** Identifies what was prerendered, so the client only hydrates matching HTML. */
  stamp: string;
  head: string;
  modules: string[];
  status: number;
  noindex: boolean;
}

export async function render(url: string): Promise<Rendered> {
  const route = await resolve(url);
  const html = renderToString(<App initial={route} />);
  return { html, stamp: route.status === 404 ? "404" : route.path, head: headTags(route.head), modules: route.modules, status: route.status, noindex: Boolean(route.head.noindex) };
}

/** Every page to prerender. */
export function paths(): { path: string; updated: string | null }[] {
  return [
    { path: "/", updated: null },
    { path: "/playground", updated: null },
    { path: "/downloads", updated: null },
    { path: "/skills", updated: null },
    { path: "/docs", updated: null },
    ...docsIndex.map((d) => ({ path: `/docs/${d.slug}`, updated: d.updated })),
    { path: "/404", updated: null },
  ];
}
