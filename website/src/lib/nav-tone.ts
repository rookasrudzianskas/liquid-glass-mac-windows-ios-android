import { useEffect, useState } from "react";
import type { RefObject } from "react";

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
 * scrolling costs nothing until a stage's edge crosses that line; changes are applied
 * once per frame.
 */
export function useNavTone(bar: RefObject<HTMLElement | null>): NavTone | undefined {
  const [tone, setTone] = useState<NavTone>();
  useEffect(() => {
    if (typeof IntersectionObserver === "undefined") return;
    const covering = new Set<Element>();
    const observed = new Set<Element>();
    // Observed, but their first entry has not arrived: until it does, the tone stays as it
    // is, so a new observer (or the next page's stage) never flashes the page's palette.
    const waiting = new Set<Element>();
    let observer: IntersectionObserver | null = null;
    let frame = 0;
    const apply = () => {
      frame = 0;
      if (waiting.size) return;
      let next: NavTone = "none";
      for (const [el, t] of sources) if (covering.has(el)) next = t;
      setTone(next);
    };
    const schedule = () => {
      if (!frame) frame = requestAnimationFrame(apply);
    };
    const observe = (el: Element) => {
      observed.add(el);
      waiting.add(el);
      observer?.observe(el);
    };
    const sync = () => {
      for (const el of observed)
        if (!sources.has(el)) {
          observer?.unobserve(el);
          observed.delete(el);
          waiting.delete(el);
          covering.delete(el);
        }
      for (const el of sources.keys()) if (!observed.has(el)) observe(el);
      schedule();
    };
    const build = () => {
      const rect = bar.current?.getBoundingClientRect();
      const line = Math.round(rect && rect.height ? rect.top + rect.height / 2 : 36);
      const below = Math.max(0, window.innerHeight - line - 1);
      observer?.disconnect();
      observer = new IntersectionObserver(
        (entries) => {
          for (const entry of entries) {
            waiting.delete(entry.target);
            if (entry.isIntersecting) covering.add(entry.target);
            else covering.delete(entry.target);
          }
          schedule();
        },
        { rootMargin: `-${line}px 0px -${below}px 0px` },
      );
      observed.clear();
      waiting.clear();
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
