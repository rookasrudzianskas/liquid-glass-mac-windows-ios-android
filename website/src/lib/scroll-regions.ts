import { useEffect } from "react";
import type { RefObject } from "react";

/**
 * Elements under `root` matching `selector` that overflow sideways become
 * keyboard-scrollable regions: a tab stop, role="region" and a name, so Safari
 * (which does not focus scroll containers) and screen reader users can reach
 * them. Ones that fit get none of it, so they add no extra tab stops. One that
 * overflows by a single pixel (the edge of a table's last cell padding) counts
 * as fitting: it is clipped instead of scrolling, so nothing scrolls that is not
 * also a named region.
 */
export function useScrollRegions(root: RefObject<HTMLElement | null>, selector: string, label: string | ((el: HTMLElement) => string), deps: unknown[] = []) {
  useEffect(() => {
    const host = root.current;
    if (!host || typeof ResizeObserver === "undefined") return;
    const els = [...host.querySelectorAll<HTMLElement>(selector)];
    if (!els.length) return;
    const update = (el: HTMLElement) => {
      const over = el.scrollWidth - el.clientWidth;
      const scrolls = over > 1;
      el.style.overflowX = over === 1 ? "clip" : "";
      if (scrolls && !el.hasAttribute("tabindex")) {
        el.tabIndex = 0;
        el.dataset.scrollRegion = "";
        if (!el.getAttribute("role")) el.setAttribute("role", "region");
        if (!el.hasAttribute("aria-label")) el.setAttribute("aria-label", typeof label === "function" ? label(el) : label);
      } else if (!scrolls && el.dataset.scrollRegion !== undefined) {
        el.removeAttribute("tabindex");
        el.removeAttribute("aria-label");
        if (el.getAttribute("role") === "region") el.removeAttribute("role");
        delete el.dataset.scrollRegion;
      }
    };
    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) update(entry.target as HTMLElement);
    });
    for (const el of els) {
      update(el);
      observer.observe(el);
    }
    return () => observer.disconnect();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [root, selector, ...deps]);
}
