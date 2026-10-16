// A small client router with prerendering in mind: a route resolves to its
// page module and data before it renders, on the server (prerender) and in the
// browser (hydration and navigation), so the HTML never differs between them.

import { createContext, useContext } from "react";
import type { AnchorHTMLAttributes, ComponentType, MouseEvent } from "react";
import site from "./generated/site.json";

export interface Head {
  title: string;
  description: string;
  path: string;
  noindex?: boolean;
}

export interface PageProps<D> {
  data: D;
}

export interface PageModule<D = unknown> {
  default: ComponentType<PageProps<D>>;
  load?: (params: Record<string, string>) => Promise<D | null>;
  head: (data: D) => Head;
}

export interface Resolved {
  key: string;
  path: string;
  Page: ComponentType<PageProps<unknown>>;
  data: unknown;
  head: Head;
  /** Source modules of the page and its data, for modulepreload hints. */
  modules: string[];
  status: 200 | 404;
}

interface RouteDef {
  key: string;
  pattern: RegExp;
  params?: string[];
  id: string;
  module: () => Promise<unknown>;
}

const routes: RouteDef[] = [
  { key: "home", pattern: /^\/$/, id: "src/pages/Home.tsx", module: () => import("./pages/Home") },
  { key: "playground", pattern: /^\/playground$/, id: "src/pages/Playground.tsx", module: () => import("./pages/Playground") },
  { key: "downloads", pattern: /^\/downloads$/, id: "src/pages/Downloads.tsx", module: () => import("./pages/Downloads") },
  { key: "skills", pattern: /^\/skills$/, id: "src/pages/Skills.tsx", module: () => import("./pages/Skills") },
  { key: "docs", pattern: /^\/docs$/, id: "src/pages/DocsIndex.tsx", module: () => import("./pages/DocsIndex") },
  { key: "doc", pattern: /^\/docs\/([a-z0-9][a-z0-9-]*)$/, params: ["slug"], id: "src/pages/Doc.tsx", module: () => import("./pages/Doc") },
];

const notFound: RouteDef = { key: "404", pattern: /^$/, id: "src/pages/NotFound.tsx", module: () => import("./pages/NotFound") };

export function normalizePath(pathname: string) {
  let p = pathname.replace(/\/index\.html$/, "/").replace(/\.html$/, "");
  if (p.length > 1) p = p.replace(/\/+$/, "");
  return p || "/";
}

export const SITE_URL = site.siteUrl;
export const REPO_URL = site.repoUrl;

async function load(def: RouteDef, path: string, params: Record<string, string>, status: 200 | 404): Promise<Resolved | null> {
  const mod = (await def.module()) as unknown as PageModule<unknown>;
  const data = mod.load ? await mod.load(params) : null;
  if (mod.load && data === null) return null;
  const extra = (data as { modules?: string[] } | null)?.modules ?? [];
  return { key: def.key, path, Page: mod.default, data, head: mod.head(data), modules: [def.id, ...extra], status };
}

export async function resolve(pathname: string): Promise<Resolved> {
  const path = normalizePath(pathname);
  for (const def of routes) {
    const m = def.pattern.exec(path);
    if (!m) continue;
    const params: Record<string, string> = {};
    def.params?.forEach((name, i) => (params[name] = m[i + 1] ?? ""));
    const r = await load(def, path, params, 200);
    if (r) return r;
    break;
  }
  const r = await load(notFound, path, {}, 404);
  if (!r) throw new Error("404 page failed to load");
  return r;
}

/** Warm a route's code before the user clicks. */
export function prefetch(pathname: string) {
  const path = normalizePath(pathname);
  const def = routes.find((r) => r.pattern.test(path));
  void def?.module().catch(() => undefined);
}

export interface Router {
  path: string;
  navigate: (href: string, options?: { replace?: boolean }) => void;
  pending: boolean;
}

export const RouterContext = createContext<Router>({ path: "/", navigate: () => undefined, pending: false });
export const useRouter = () => useContext(RouterContext);

/** True for same-origin page links the router should handle itself. */
export function isRoutable(url: URL) {
  if (typeof window === "undefined") return false;
  if (url.origin !== window.location.origin) return false;
  const last = url.pathname.split("/").pop() ?? "";
  // Files (downloads, llms.txt, raw markdown, feeds) load normally.
  if (/\.[a-z0-9]+$/i.test(last) && !last.endsWith(".html")) return false;
  if (/^\/(maven|nuget|downloads|media|wallpapers|fonts|icons)\//.test(url.pathname)) return false;
  return true;
}

export function handleLinkClick(e: MouseEvent<HTMLAnchorElement> | globalThis.MouseEvent, anchor: HTMLAnchorElement, navigate: Router["navigate"]) {
  if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
  if (anchor.target && anchor.target !== "_self") return;
  if (anchor.hasAttribute("download")) return;
  const url = new URL(anchor.href, window.location.href);
  if (!isRoutable(url)) return;
  // Same page, different hash: let the browser scroll.
  if (url.pathname === window.location.pathname && url.search === window.location.search && url.hash) return;
  e.preventDefault();
  navigate(url.pathname + url.search + url.hash);
}

export function Link({ href, onClick, onMouseEnter, onFocus, ...rest }: AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) {
  const { navigate, path } = useRouter();
  const current = normalizePath(href.split(/[?#]/)[0] ?? "") === path;
  return (
    <a
      href={href}
      aria-current={current ? "page" : undefined}
      onClick={(e) => {
        onClick?.(e);
        handleLinkClick(e, e.currentTarget, navigate);
      }}
      onMouseEnter={(e) => {
        onMouseEnter?.(e);
        prefetch(href);
      }}
      onFocus={(e) => {
        onFocus?.(e);
        prefetch(href);
      }}
      {...rest}
    />
  );
}
