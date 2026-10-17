import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { RouterContext, normalizePath, resolve } from "./router";
import type { Resolved } from "./router";
import { ThemeProvider } from "./theme";
import { Layout } from "./components/Layout";
import { applyHead } from "./head";

const useIsoLayoutEffect = typeof window === "undefined" ? useEffect : useLayoutEffect;

interface HistoryState {
  scroll?: number;
}

export function App({ initial }: { initial: Resolved }) {
  const [route, setRoute] = useState(initial);
  const [pending, setPending] = useState(false);
  const pendingScroll = useRef<{ hash: string; scroll: number | null } | null>(null);
  const token = useRef(0);
  const pathRef = useRef(initial.path);
  pathRef.current = route.path;

  const go = useCallback(async (href: string, mode: "push" | "replace" | "pop") => {
    const url = new URL(href, window.location.href);
    const id = ++token.current;
    const timer = window.setTimeout(() => setPending(true), 120);
    let next: Resolved;
    try {
      next = await resolve(url.pathname);
    } catch {
      // A chunk failed to load (for example after a deploy): load the page normally.
      window.location.assign(url.href);
      return;
    } finally {
      window.clearTimeout(timer);
    }
    if (id !== token.current) return;
    setPending(false);
    if (mode !== "pop") {
      const state: HistoryState = { scroll: window.scrollY };
      window.history.replaceState({ ...(window.history.state as object), ...state }, "");
      const target = url.pathname + url.search + url.hash;
      if (mode === "push") window.history.pushState({ scroll: 0 } satisfies HistoryState, "", target);
      else window.history.replaceState({ scroll: 0 } satisfies HistoryState, "", target);
      pendingScroll.current = { hash: url.hash, scroll: null };
    } else {
      const state = (window.history.state ?? {}) as HistoryState;
      pendingScroll.current = { hash: url.hash, scroll: typeof state.scroll === "number" ? state.scroll : 0 };
    }
    setRoute(next);
  }, []);

  const navigate = useCallback((href: string, options?: { replace?: boolean }) => void go(href, options?.replace ? "replace" : "push"), [go]);

  useEffect(() => {
    if ("scrollRestoration" in window.history) window.history.scrollRestoration = "manual";
    const onPop = () => {
      // Fragment navigation within the same page: the browser scrolls.
      if (normalizePath(window.location.pathname) === pathRef.current) return;
      void go(window.location.href, "pop");
    };
    window.addEventListener("popstate", onPop);
    // The current entry keeps its scroll position up to date, so Back and Forward
    // both return to where the visitor was. Debounced: Safari limits replaceState.
    let timer = 0;
    const save = () => {
      window.clearTimeout(timer);
      // Between a push (or a pop) and the render of the new page, the entry is not this page's.
      if (normalizePath(window.location.pathname) !== pathRef.current) return;
      const state = (window.history.state ?? {}) as HistoryState;
      if (state.scroll !== window.scrollY) window.history.replaceState({ ...state, scroll: window.scrollY } satisfies HistoryState, "");
    };
    const onScroll = () => {
      window.clearTimeout(timer);
      timer = window.setTimeout(save, 200);
    };
    window.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("pagehide", save);
    return () => {
      window.clearTimeout(timer);
      window.removeEventListener("popstate", onPop);
      window.removeEventListener("scroll", onScroll);
      window.removeEventListener("pagehide", save);
    };
  }, [go]);

  // After a navigation renders: scroll, update the head, move focus.
  const first = useRef(true);
  useIsoLayoutEffect(() => {
    if (first.current) {
      first.current = false;
      return;
    }
    const target = pendingScroll.current;
    pendingScroll.current = null;
    applyHead(route.head);
    if (target?.hash) {
      const el = document.getElementById(decodeURIComponent(target.hash.slice(1)));
      if (el) el.scrollIntoView();
      else window.scrollTo(0, 0);
    } else window.scrollTo(0, target?.scroll ?? 0);
    const main = document.getElementById("main");
    main?.focus({ preventScroll: true });
    const live = document.getElementById("route-announcer");
    if (live) live.textContent = route.head.title;
  }, [route]);

  const router = useMemo(() => ({ path: route.path, navigate, pending }), [route.path, navigate, pending]);
  const Page = route.Page;

  return (
    <RouterContext.Provider value={router}>
      <ThemeProvider>
        <Layout routeKey={route.key}>
          <Page data={route.data} />
        </Layout>
      </ThemeProvider>
    </RouterContext.Provider>
  );
}
