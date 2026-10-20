import { useEffect, useLayoutEffect } from "react";
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

const useIsoLayoutEffect = typeof window === "undefined" ? useEffect : useLayoutEffect;

/**
 * Keeps `nav`'s data-tone on the tone under `bar`'s middle line (layout.css, base.css).
 * The attribute is set on the element directly, never through React state, so it changes
 * in the same task as whatever moved the stages and no render has to commit first: a
 * stage registers or leaves in a layout effect (GlassStage), and the tone is measured and
 * set right there, in the commit that shows or removes the stage, before that commit
 * paints; the next frame measures again before it paints, since the router restores the
 * scroll after the page's own layout effects. A scroll measures the registered stages
 * (none on most pages, three rectangles on the home page) in its scroll event, which
 * runs before the frame that shows the new position paints; an IntersectionObserver whose
 * root is shrunk to the one-pixel line catches a stage edge that crosses it without a
 * scroll (a layout change above it), in the next frame. The prerendered nav carries no
 * tone, so the theme's palette shows until the first measurement, which runs as the page
 * hydrates.
 */
export function useNavTone(nav: RefObject<HTMLElement | null>, bar: RefObject<HTMLElement | null>) {
  useIsoLayoutEffect(() => {
    const header = nav.current;
    if (!header || typeof IntersectionObserver === "undefined") return;
    const observed = new Set<Element>();
    let observer: IntersectionObserver | null = null;
    let line = 36;
    let frame = 0;
    // Any overlap with the line, edges included, as the observer counts it; the last
    // registered stage that covers it wins.
    const update = () => {
      let next: NavTone = "none";
      for (const [el, tone] of sources) {
        const r = el.getBoundingClientRect();
        if ((r.width || r.height) && r.top <= line + 1 && r.bottom >= line) next = tone;
      }
      if (header.dataset.tone !== next) header.dataset.tone = next;
    };
    const onFrame = () => {
      frame = 0;
      update();
    };
    const schedule = () => {
      if (!frame) frame = requestAnimationFrame(onFrame);
    };
    const sync = () => {
      for (const el of observed)
        if (!sources.has(el)) {
          observer?.unobserve(el);
          observed.delete(el);
        }
      for (const el of sources.keys())
        if (!observed.has(el)) {
          observed.add(el);
          observer?.observe(el);
        }
      update();
      schedule();
    };
    const build = () => {
      const rect = bar.current?.getBoundingClientRect();
      line = Math.round(rect && rect.height ? rect.top + rect.height / 2 : 36);
      const below = Math.max(0, window.innerHeight - line - 1);
      observer?.disconnect();
      observer = new IntersectionObserver(schedule, { rootMargin: `-${line}px 0px -${below}px 0px` });
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
    window.addEventListener("scroll", update, { passive: true });
    return () => {
      listeners.delete(sync);
      window.removeEventListener("resize", onResize);
      window.removeEventListener("scroll", update);
      observer?.disconnect();
      if (frame) cancelAnimationFrame(frame);
      if (resizeFrame) cancelAnimationFrame(resizeFrame);
    };
  }, [nav, bar]);
}
