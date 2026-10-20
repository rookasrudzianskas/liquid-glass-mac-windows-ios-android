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
// Stages that have unregistered: one that is unmounting is still in the document while
// its layout effects clean up, with its data-nav-tone.
const gone = new WeakSet<Element>();
const listeners = new Set<() => void>();
const emit = () => listeners.forEach((listener) => listener());

/** Register `el` as showing `tone`; returns the unregister function. */
export function registerNavTone(el: Element, tone: Tone) {
  sources.set(el, tone);
  gone.delete(el);
  emit();
  return () => {
    if (sources.get(el) === tone) {
      sources.delete(el);
      gone.add(el);
    }
    emit();
  };
}

/**
 * Before the app hydrates, keeps the nav bar's palette on the stage under it as
 * useNavTone does afterwards (a <script> in the prerendered head: head.ts). A stage
 * carries its tone in data-nav-tone (GlassStage) from the prerendered page on, and a
 * scroll, a layout change, the end of parsing and every frame while the page loads
 * (watchLoadFrames) measure them; useNavTone stops it (window.lgNavToneStop) when it
 * takes over. The attribute is only set while a stage is under the bar.
 */
export const NAV_TONE_BOOT = `(function(){function u(){var h=document.querySelector(".nav"),b=h&&h.querySelector(".nav__bar");if(!b)return;var q=b.getBoundingClientRect(),y=Math.round(q.height?q.top+q.height/2:36),t="",s=document.querySelectorAll("[data-nav-tone]");for(var i=0;i<s.length;i++){var r=s[i].getBoundingClientRect();if((r.width||r.height)&&r.top<=y+1&&r.bottom>=y)t=s[i].getAttribute("data-nav-tone")}if(t){if(h.getAttribute("data-tone")!==t)h.setAttribute("data-tone",t)}else if(h.hasAttribute("data-tone"))h.removeAttribute("data-tone")}var R=typeof ResizeObserver=="function"?ResizeObserver:null,o=R?new R(u):null,f=0,n=2;function w(){f=0;var r=new R(function(){r.disconnect();u()});r.observe(document.documentElement);if(document.readyState!="complete"||n--)f=requestAnimationFrame(w)}if(o){o.observe(document.documentElement);f=requestAnimationFrame(w)}addEventListener("scroll",u,{passive:true});document.addEventListener("DOMContentLoaded",u);window.lgNavToneStop=function(){if(o)o.disconnect();if(f)cancelAnimationFrame(f);removeEventListener("scroll",u);document.removeEventListener("DOMContentLoaded",u)}})();`;

/**
 * Runs `measure` after layout in every frame while the page loads, and in the two frames
 * after its load event: a ResizeObserver delivers its first observation after the frame's
 * layout and before it paints, so a fresh one each frame sees the scroll the browser
 * applies there without a scroll event (that comes a frame later). Until the load event
 * the browser scrolls a fragment's target back into view after every layout (a hard load
 * of /#controls-slider-label lands in the Controls stage several frames after the first
 * paint), so the frame that first shows the stage under the bar also shows the bar in
 * its palette. NAV_TONE_BOOT does the same in its own words. Returns the cancel function.
 */
function watchLoadFrames(measure: () => void) {
  if (typeof ResizeObserver === "undefined" || typeof requestAnimationFrame === "undefined") return () => {};
  let frame = 0;
  let after = 2;
  const watch = () => {
    frame = 0;
    const once = new ResizeObserver(() => {
      once.disconnect();
      measure();
    });
    once.observe(document.documentElement);
    if (document.readyState !== "complete" || after-- > 0) frame = requestAnimationFrame(watch);
  };
  frame = requestAnimationFrame(watch);
  return () => {
    if (frame) cancelAnimationFrame(frame);
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
 * scroll (a layout change above it), in the next frame; and while the page loads every
 * frame measures after its layout (watchLoadFrames), so the scroll the browser applies
 * to keep a fragment's target in view paints with the palette of what it brings under
 * the bar. Before this runs, as the page hydrates, NAV_TONE_BOOT does the same for the
 * prerendered page.
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
    // registered stage that covers it wins. A prerendered stage whose Suspense boundary
    // has not hydrated yet has not registered: its data-nav-tone stands in for it.
    const covers = (el: Element) => {
      const r = el.getBoundingClientRect();
      return (r.width || r.height) && r.top <= line + 1 && r.bottom >= line;
    };
    const update = () => {
      let next: NavTone = "none";
      for (const el of document.querySelectorAll<HTMLElement>("[data-nav-tone]")) {
        const tone = el.dataset.navTone;
        if (!sources.has(el) && !gone.has(el) && (tone === "light" || tone === "dark") && covers(el)) next = tone;
      }
      for (const [el, tone] of sources) if (covers(el)) next = tone;
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
    (window as Window & { lgNavToneStop?: () => void }).lgNavToneStop?.();
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
    const stopWatch = watchLoadFrames(update);
    return () => {
      stopWatch();
      listeners.delete(sync);
      window.removeEventListener("resize", onResize);
      window.removeEventListener("scroll", update);
      observer?.disconnect();
      if (frame) cancelAnimationFrame(frame);
      if (resizeFrame) cancelAnimationFrame(resizeFrame);
    };
  }, [nav, bar]);
}
