// A glass stage: one canvas, the DOM elements that are glass, and one frame
// loop that runs only while something changes. The DOM is the source of
// truth for geometry: every frame reads each surface's on-screen rectangle
// (including the press/stretch transforms the motion controllers write), so
// labels on the glass and the refracting pixels never drift apart.

import { glow as glowParams } from "@lg/reference";
import { GlassRenderer } from "./renderer";
import type { BackdropImage, ContentRect, Material, SurfaceFrame } from "./renderer";

export type Radius = number | "capsule";
export type Appearance = "light" | "dark";
export type RendererKind = "pending" | "webgl" | "css";

export interface SurfaceOptions {
  material: Material;
  radius: Radius;
  refraction: number;
  tint: readonly [number, number, number] | null;
  nested: boolean;
  /** Overrides the stage appearance for this surface. */
  appearance?: Appearance;
}

export interface Surface {
  el: HTMLElement;
  options: SurfaceOptions;
  /** 0..1, multiplied into the glass (fade in and out). */
  opacity: number;
  /** Pointer glow in client coordinates and its spring value (0..1). */
  glowClientX: number;
  glowClientY: number;
  glow: number;
  order: number;
}

/** A DOM element painted into the backdrop as a solid shape, so glass refracts it. */
export interface Content {
  el: HTMLElement;
  radius: Radius;
  color: readonly [number, number, number, number];
}

/** Called once per frame before rendering; return true to keep animating. */
export type Ticker = (dt: number, now: number) => boolean;

export interface SceneOptions {
  appearance: Appearance;
  onRenderer?: (kind: RendererKind, reason?: string) => void;
  maxDpr?: number;
}

let order = 0;

export class GlassScene {
  readonly root: HTMLElement;
  readonly canvas: HTMLCanvasElement;
  appearance: Appearance;
  reducedMotion = false;
  reducedTransparency = false;
  increasedContrast = false;
  renderer: GlassRenderer | null = null;
  kind: RendererKind = "pending";

  private surfaces = new Set<Surface>();
  private contents = new Set<Content>();
  private tickers = new Set<Ticker>();
  private frame = 0;
  private last = 0;
  private visible = true;
  private dirty = true;
  private observers: { disconnect(): void }[] = [];
  private cleanups: (() => void)[] = [];
  private onRenderer: SceneOptions["onRenderer"];
  private maxDpr: number;
  private backdrop: BackdropImage | null = null;
  private fade: { from: number; start: number; duration: number } | null = null;
  private destroyed = false;

  constructor(root: HTMLElement, canvas: HTMLCanvasElement, options: SceneOptions) {
    this.root = root;
    this.canvas = canvas;
    this.appearance = options.appearance;
    this.onRenderer = options.onRenderer;
    this.maxDpr = options.maxDpr ?? 2;

    const media = (query: string, apply: (matches: boolean) => void) => {
      const mq = window.matchMedia(query);
      apply(mq.matches);
      const listener = (e: MediaQueryListEvent) => {
        apply(e.matches);
        this.invalidate();
      };
      mq.addEventListener("change", listener);
      this.cleanups.push(() => mq.removeEventListener("change", listener));
    };
    media("(prefers-reduced-motion: reduce)", (m) => (this.reducedMotion = m));
    media("(prefers-reduced-transparency: reduce)", (m) => (this.reducedTransparency = m));
    media("(prefers-contrast: more)", (m) => (this.increasedContrast = m));

    try {
      this.renderer = new GlassRenderer(canvas);
    } catch (error) {
      this.fail(error instanceof Error ? error.message : String(error));
    }
    if (this.reducedTransparency && this.renderer) {
      // OPTICS.md section 8: reduce transparency draws the opaque fallback.
      this.renderer.destroy();
      this.renderer = null;
      this.fail("reduced transparency");
    }

    const onLost = (e: Event) => {
      e.preventDefault();
      if (this.renderer) this.renderer.lost = true;
      this.setKind("css", "context lost");
    };
    const onRestored = () => {
      if (!this.renderer) return;
      try {
        this.renderer.init();
        if (this.backdrop) this.renderer.setImage(this.backdrop, "a");
        this.measure();
        this.pendingReady = true;
        this.invalidate();
      } catch (error) {
        this.fail(error instanceof Error ? error.message : String(error));
      }
    };
    canvas.addEventListener("webglcontextlost", onLost);
    canvas.addEventListener("webglcontextrestored", onRestored);
    this.cleanups.push(() => {
      canvas.removeEventListener("webglcontextlost", onLost);
      canvas.removeEventListener("webglcontextrestored", onRestored);
    });

    const resize = new ResizeObserver((entries) => {
      for (const entry of entries) {
        if (entry.target === canvas) {
          const box = entry.devicePixelContentBoxSize?.[0];
          this.measure(box ? { width: box.inlineSize, height: box.blockSize } : undefined);
        }
      }
      this.invalidate();
    });
    try {
      resize.observe(canvas, { box: "device-pixel-content-box" });
    } catch {
      resize.observe(canvas);
    }
    this.observers.push(resize);
    this.surfaceResize = resize;

    const intersection = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) this.visible = entry.isIntersecting;
        if (this.visible) this.invalidate();
      },
      { rootMargin: "64px" },
    );
    intersection.observe(root);
    this.observers.push(intersection);

    const onVisibility = () => {
      if (!document.hidden) {
        this.last = 0;
        this.invalidate();
      }
    };
    document.addEventListener("visibilitychange", onVisibility);
    this.cleanups.push(() => document.removeEventListener("visibilitychange", onVisibility));

    // DPR changes when a window moves between displays.
    const watchDpr = () => {
      const mq = window.matchMedia(`(resolution: ${window.devicePixelRatio}dppx)`);
      const listener = () => {
        this.measure();
        this.invalidate();
        watchDpr();
      };
      mq.addEventListener("change", listener, { once: true });
      this.cleanups.push(() => mq.removeEventListener("change", listener));
    };
    watchDpr();

    this.measure();
    if (this.renderer) this.pendingReady = true;
    this.invalidate();
  }

  private surfaceResize: ResizeObserver;
  private pendingReady = false;

  private fail(reason: string) {
    this.renderer = null;
    this.setKind("css", reason);
  }

  private setKind(kind: RendererKind, reason?: string) {
    if (this.kind === kind) return;
    this.kind = kind;
    this.root.dataset.renderer = kind;
    this.onRenderer?.(kind, reason);
  }

  private cssSize = { width: 1, height: 1 };

  private measure(device?: { width: number; height: number }) {
    const width = this.canvas.clientWidth;
    const height = this.canvas.clientHeight;
    if (!width || !height) return;
    this.cssSize = { width, height };
    const dpr = Math.min(window.devicePixelRatio || 1, this.maxDpr);
    const real = window.devicePixelRatio || 1;
    let dw = width * dpr;
    let dh = height * dpr;
    if (device && dpr === real) {
      dw = device.width;
      dh = device.height;
    }
    this.renderer?.resize(width, height, dw, dh);
  }

  setAppearance(appearance: Appearance) {
    if (appearance !== this.appearance) {
      this.appearance = appearance;
      this.invalidate();
    }
  }

  /** Show an image as the backdrop, optionally cross-fading from the current one. */
  setBackdrop(image: BackdropImage, transition = 0) {
    const r = this.renderer;
    this.backdrop = image;
    if (!r) return;
    if (transition > 0 && !this.reducedMotion && this.hasBackdrop) {
      r.setImage(image, "b");
      this.fade = { from: 0, start: -1, duration: transition };
    } else {
      r.setImage(image, "a");
      r.setCrossfade(0);
      this.fade = null;
    }
    this.hasBackdrop = true;
    this.invalidate();
  }

  /** Re-upload a canvas backdrop after drawing into it. */
  refreshBackdrop(image: BackdropImage) {
    this.backdrop = image;
    this.renderer?.setImage(image, "a");
    this.hasBackdrop = true;
    this.invalidate();
  }

  private hasBackdrop = false;

  add(el: HTMLElement, options: SurfaceOptions): Surface {
    const surface: Surface = { el, options, opacity: 1, glowClientX: 0, glowClientY: 0, glow: 0, order: order++ };
    this.surfaces.add(surface);
    this.surfaceResize.observe(el);
    this.invalidate();
    return surface;
  }

  remove(surface: Surface) {
    this.surfaces.delete(surface);
    this.surfaceResize.unobserve(surface.el);
    this.invalidate();
  }

  addContent(el: HTMLElement, radius: Radius, color: readonly [number, number, number, number]): Content {
    const c: Content = { el, radius, color };
    this.contents.add(c);
    this.surfaceResize.observe(el);
    this.invalidate();
    return c;
  }

  removeContent(c: Content) {
    this.contents.delete(c);
    this.surfaceResize.unobserve(c.el);
    this.invalidate();
  }

  surfaceFor(el: HTMLElement): Surface | null {
    for (const s of this.surfaces) if (s.el === el) return s;
    return null;
  }

  addTicker(ticker: Ticker) {
    this.tickers.add(ticker);
    this.invalidate();
    return () => {
      this.tickers.delete(ticker);
    };
  }

  /** Schedule a frame. Cheap to call many times. */
  invalidate() {
    this.dirty = true;
    if (!this.frame && !this.destroyed) this.frame = requestAnimationFrame(this.tick);
  }

  private tick = (now: number) => {
    this.frame = 0;
    if (this.destroyed) return;
    const dt = this.last ? Math.min(1 / 30, Math.max(0, (now - this.last) / 1000)) : 1 / 60;
    this.last = now;
    let animating = false;
    for (const ticker of this.tickers) if (ticker(dt, now)) animating = true;

    const r = this.renderer;
    if (this.fade && r) {
      if (this.fade.start < 0) this.fade.start = now;
      const t = Math.min(1, (now - this.fade.start) / this.fade.duration);
      const eased = t * t * (3 - 2 * t);
      r.setCrossfade(eased);
      if (t >= 1) {
        r.commitCrossfade();
        this.fade = null;
      } else animating = true;
    }

    if (r && !r.lost && (this.dirty || animating) && this.visible && !document.hidden) {
      r.render(this.frames(), this.appearance === "dark");
      if (this.pendingReady && this.hasBackdrop) {
        this.pendingReady = false;
        this.setKind("webgl");
      }
    }
    this.dirty = false;
    if (animating) this.invalidate();
    else this.last = 0;
  };

  private frames(): SurfaceFrame[] {
    const canvasRect = this.canvas.getBoundingClientRect();
    const sx = canvasRect.width ? this.cssSize.width / canvasRect.width : 1;
    const sy = canvasRect.height ? this.cssSize.height / canvasRect.height : 1;
    if (this.renderer) {
      const rects: ContentRect[] = [];
      for (const c of this.contents) {
        const rect = c.el.getBoundingClientRect();
        if (!rect.width || !rect.height) continue;
        rects.push({
          x: (rect.left - canvasRect.left + rect.width / 2) * sx,
          y: (rect.top - canvasRect.top + rect.height / 2) * sy,
          width: rect.width * sx,
          height: rect.height * sy,
          radius: c.radius === "capsule" ? -1 : c.radius,
          color: c.color,
        });
      }
      this.renderer.setContent(rects);
    }
    const list = [...this.surfaces].sort((a, b) => Number(a.options.nested) - Number(b.options.nested) || a.order - b.order);
    const out: SurfaceFrame[] = [];
    for (const s of list) {
      const rect = s.el.getBoundingClientRect();
      if (!rect.width || !rect.height) continue;
      const width = rect.width * sx;
      const height = rect.height * sy;
      const x = (rect.left - canvasRect.left + rect.width / 2) * sx;
      const y = (rect.top - canvasRect.top + rect.height / 2) * sy;
      const dark = (s.options.appearance ?? this.appearance) === "dark";
      const g = glowParams(width, height, dark);
      out.push({
        x,
        y,
        width,
        height,
        radius: s.options.radius === "capsule" ? -1 : s.options.radius,
        refraction: s.options.refraction,
        material: this.increasedContrast ? "regular" : s.options.material,
        dark,
        nested: s.options.nested,
        tint: s.options.tint,
        opacity: s.opacity,
        glowX: (s.glowClientX - rect.left - rect.width / 2) * sx,
        glowY: (s.glowClientY - rect.top - rect.height / 2) * sy,
        glow: s.glow,
        glowRadius: g.radius,
        contrast: this.increasedContrast,
      });
    }
    return out;
  }

  destroy() {
    this.destroyed = true;
    if (this.frame) cancelAnimationFrame(this.frame);
    for (const o of this.observers) o.disconnect();
    for (const c of this.cleanups) c();
    this.renderer?.destroy();
    this.renderer = null;
    this.surfaces.clear();
    this.contents.clear();
    this.tickers.clear();
  }
}
