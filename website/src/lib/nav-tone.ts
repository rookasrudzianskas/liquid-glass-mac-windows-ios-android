import { useEffect, useState } from "react";
import type { RefObject } from "react";
import { flushSync } from "react-dom";

/**
 * What the fixed nav bar sits over. A glass stage registers itself with the tone of the
 * backdrop it shows (GlassStage's navTone); the nav takes that tone's palette while the
 * stage covers the line through its labels, and the page's palette otherwise ("none").
 */
export type Tone = "light" | "dark";
export type NavTone = Tone | "none";

const sources = new Map<Element, Tone>();
const listeners = new Set<() => void>();
const emit = () => listeners.forEach((listener) => listener());

/** Register `el` as showing `tone`; returns the unregister function. */
export function registerNavTone(el: Element, tone: Tone) {
  sources.set(el, tone);
  emit();
  return () => {
    if (sources.get(el) === tone) sources.delete(el);
    emit();
  };
}

/**
 * The tone under `bar`, or undefined before the first measurement (on the server and in
 * the first client render, so the prerendered HTML and hydration agree). The root of an
 * IntersectionObserver is shrunk to the one-pixel line through the bar's middle, so
 * scrolling costs nothing until a stage's edge crosses that line. When stages register
 * or leave (a load, a client-side navigation, a new backdrop), the next frame measures
 * them itself before it paints, where the observer's first entries would arrive only
 * after a frame had painted the previous tone; the new tone is committed in that frame
 * (flushSync), so a page is never painted with another page's palette.
 */
export function useNavTone(bar: RefObject<HTMLElement | null>): NavTone | undefined {
  const [tone, setTone] = useState<NavTone>();
  useEffect(() => {
    if (typeof IntersectionObserver === "undefined") return;
    const covering = new Set<Element>();
    const observed = new Set<Element>();
    let observer: IntersectionObserver | null = null;
    let line = 36;
    // Sources changed since the last frame: measure them all rather than wait for entries.
    let stale = true;
    let current: NavTone | undefined;
    let frame = 0;
    const apply = () => {
      frame = 0;
      if (stale) {
        stale = false;
        covering.clear();
        for (const el of sources.keys()) {
          const r = el.getBoundingClientRect();
          // Any overlap with the line, edges included, as the observer counts it.
          if ((r.width || r.height) && r.top <= line + 1 && r.bottom >= line) covering.add(el);
        }
      }
      let next: NavTone = "none";
      for (const [el, t] of sources) if (covering.has(el)) next = t;
      if (next === current) return;
      current = next;
      flushSync(() => setTone(next));
    };
    const schedule = () => {
      if (!frame) frame = requestAnimationFrame(apply);
    };
    const sync = () => {
      for (const el of observed)
        if (!sources.has(el)) {
          observer?.unobserve(el);
          observed.delete(el);
          covering.delete(el);
        }
      for (const el of sources.keys())
        if (!observed.has(el)) {
          observed.add(el);
          observer?.observe(el);
        }
      stale = true;
      schedule();
    };
    const build = () => {
      const rect = bar.current?.getBoundingClientRect();
      line = Math.round(rect && rect.height ? rect.top + rect.height / 2 : 36);
      const below = Math.max(0, window.innerHeight - line - 1);
      observer?.disconnect();
      observer = new IntersectionObserver(
        (entries) => {
          for (const entry of entries) {
            if (!sources.has(entry.target)) continue;
            if (entry.isIntersecting) covering.add(entry.target);
            else covering.delete(entry.target);
          }
          schedule();
        },
        { rootMargin: `-${line}px 0px -${below}px 0px` },
      );
      observed.clear();
      sync();
    };
    build();
    listeners.add(sync);
    // The line's distance from the bottom edge changes with the viewport's height.
    let height = window.innerHeight;
    let resizeFrame = 0;
    const onResize = () => {
      if (resizeFrame || window.innerHeight === height) return;
      resizeFrame = requestAnimationFrame(() => {
        resizeFrame = 0;
        height = window.innerHeight;
        build();
      });
    };
    window.addEventListener("resize", onResize, { passive: true });
    return () => {
      listeners.delete(sync);
      window.removeEventListener("resize", onResize);
      observer?.disconnect();
      if (frame) cancelAnimationFrame(frame);
      if (resizeFrame) cancelAnimationFrame(resizeFrame);
    };
  }, [bar]);
  return tone;
}
