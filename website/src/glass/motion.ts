// Press, release, glow and drag for interactive glass (OPTICS.md section 6),
// driven by the closed-form springs from spec/reference.mjs.

import { glow as glowParams, pressScale, rubberBand, springStep, springs, stretchLimit } from "@lg/reference";
import type { SpringOptions, SpringState } from "@lg/reference";
import type { GlassScene, Surface } from "./scene";

export interface MotionOptions {
  /** Press grow and pointer glow. */
  interactive: boolean;
  /** Follows the pointer and stays where it is dropped. */
  draggable: boolean;
  /** Non-draggable surfaces stretch toward a finger that drags while pressed. */
  stretch: boolean;
  /** Entrance delay in ms (the surface materializes once the stage is ready). */
  enterDelay: number;
  /** Arrow-key step for draggable surfaces. */
  keyboardStep: number;
}

const spring = (value = 0): SpringState => ({ value, velocity: 0, target: value });
const settled = (s: SpringState) => s.value === s.target && s.velocity === 0;
const HOVER = glowParams(1, 1, false).hover;

/** Critically damped variant used under reduced motion. */
const calm = (o: SpringOptions): SpringOptions => ({ duration: o.duration, bounce: 0 });

export class SurfaceMotion {
  private scene: GlassScene;
  private surface: Surface;
  private el: HTMLElement;
  options: MotionOptions;

  private press = spring();
  private glow = spring();
  private x = spring();
  private y = spring();
  private presence = spring(0);
  private pressing = false;
  private hovering = false;
  private pressOptions: SpringOptions = springs.press;
  private offsetOptions: SpringOptions = springs.track;
  private start = { cx: 0, cy: 0, x: 0, y: 0, t: 0 };
  private pointerId: number | null = null;
  private enterAt = -1;
  private visible = false;
  private removeTicker: () => void;
  private cleanups: (() => void)[] = [];
  private lastTransform = "";
  private lastOpacity = "0";
  /** Set when the last press moved far enough to count as a drag, not a tap. */
  dragged = false;

  constructor(scene: GlassScene, surface: Surface, options: MotionOptions) {
    this.scene = scene;
    this.surface = surface;
    this.el = surface.el;
    this.options = options;
    surface.opacity = 0;
    this.el.style.opacity = "0";
    this.removeTicker = scene.addTicker(this.tick);
    this.bind();
  }

  private listen<K extends keyof HTMLElementEventMap>(target: HTMLElement, type: K, fn: (e: HTMLElementEventMap[K]) => void, opts?: AddEventListenerOptions) {
    target.addEventListener(type, fn, opts);
    this.cleanups.push(() => target.removeEventListener(type, fn, opts));
  }

  private bind() {
    const el = this.el;
    this.listen(el, "pointerdown", (e) => {
      if (!this.options.interactive && !this.options.draggable) return;
      if (e.button !== 0 || this.pointerId !== null) return;
      this.pointerId = e.pointerId;
      this.pressing = true;
      this.dragged = false;
      this.start = { cx: e.clientX, cy: e.clientY, x: this.x.target, y: this.y.target, t: performance.now() };
      this.setGlowPoint(e.clientX, e.clientY);
      if (this.options.draggable) {
        e.preventDefault();
        try {
          el.setPointerCapture(e.pointerId);
        } catch {
          /* pointer already released */
        }
        const size = Math.max(el.offsetWidth, el.offsetHeight);
        this.offsetOptions = { duration: springs.track.duration + 0.0005 * size, bounce: 0 };
      } else {
        this.offsetOptions = springs.track;
      }
      this.pressOptions = springs.press;
      this.press.target = 1;
      this.glow.target = 1;
      window.addEventListener("pointermove", this.onMove);
      window.addEventListener("pointerup", this.onUp);
      window.addEventListener("pointercancel", this.onUp);
      this.scene.invalidate();
    });
    this.listen(el, "pointerenter", (e) => {
      if (e.pointerType !== "mouse" || !this.options.interactive) return;
      this.hovering = true;
      if (!this.pressing) this.glow.target = HOVER;
      this.setGlowPoint(e.clientX, e.clientY);
      this.scene.invalidate();
    });
    this.listen(el, "pointermove", (e) => {
      if (e.pointerType !== "mouse" || this.pressing || !this.options.interactive) return;
      this.setGlowPoint(e.clientX, e.clientY);
      this.scene.invalidate();
    });
    this.listen(el, "pointerleave", (e) => {
      if (e.pointerType !== "mouse") return;
      this.hovering = false;
      if (!this.pressing) this.glow.target = 0;
      this.scene.invalidate();
    });
    this.listen(el, "keydown", (e) => {
      if (!this.options.draggable || e.target !== el) return;
      const step = this.options.keyboardStep * (e.shiftKey ? 3 : 1);
      const moves: Record<string, [number, number]> = {
        ArrowLeft: [-step, 0],
        ArrowRight: [step, 0],
        ArrowUp: [0, -step],
        ArrowDown: [0, step],
      };
      const move = moves[e.key];
      if (!move) return;
      e.preventDefault();
      this.offsetOptions = springs.track;
      const [x, y] = this.clamp(this.x.target + move[0], this.y.target + move[1], false);
      this.x.target = x;
      this.y.target = y;
      this.scene.invalidate();
    });
  }

  private setGlowPoint(cx: number, cy: number) {
    this.surface.glowClientX = cx;
    this.surface.glowClientY = cy;
  }

  private onMove = (e: PointerEvent) => {
    if (e.pointerId !== this.pointerId) return;
    const dx = e.clientX - this.start.cx;
    const dy = e.clientY - this.start.cy;
    if (Math.hypot(dx, dy) > 6) this.dragged = true;
    this.setGlowPoint(e.clientX, e.clientY);
    if (this.options.draggable) {
      const [x, y] = this.clamp(this.start.x + dx, this.start.y + dy, true);
      this.x.target = x;
      this.y.target = y;
    } else if (this.options.stretch && !this.scene.reducedMotion) {
      const limit = stretchLimit(this.el.offsetWidth, this.el.offsetHeight);
      this.x.target = rubberBand(dx, limit);
      this.y.target = rubberBand(dy, limit);
    }
    this.scene.invalidate();
  };

  private onUp = (e: PointerEvent) => {
    if (e.pointerId !== this.pointerId) return;
    this.pointerId = null;
    this.pressing = false;
    window.removeEventListener("pointermove", this.onMove);
    window.removeEventListener("pointerup", this.onUp);
    window.removeEventListener("pointercancel", this.onUp);
    this.pressOptions = springs.release;
    this.press.target = 0;
    this.glow.target = this.hovering && e.pointerType === "mouse" ? HOVER : 0;
    if (this.options.draggable) {
      const [x, y] = this.clamp(this.x.target, this.y.target, false);
      this.x.target = x;
      this.y.target = y;
    } else {
      this.offsetOptions = springs.release;
      this.x.target = 0;
      this.y.target = 0;
    }
    this.scene.invalidate();
  };

  /** Keep a draggable surface inside its stage; rubber-band past the edge while dragging. */
  private clamp(x: number, y: number, elastic: boolean): [number, number] {
    const el = this.el;
    const parent = this.scene.root;
    const w = el.offsetWidth;
    const h = el.offsetHeight;
    const pw = parent.clientWidth;
    const ph = parent.clientHeight;
    // Layout position of the element inside the stage, before our transform.
    let left = 0;
    let top = 0;
    let node: HTMLElement | null = el;
    while (node && node !== parent) {
      left += node.offsetLeft;
      top += node.offsetTop;
      node = node.offsetParent as HTMLElement | null;
    }
    const margin = 8;
    const axis = (v: number, base: number, size: number, room: number) => {
      const lo = margin - base;
      const hi = room - margin - size - base;
      if (lo > hi) return (lo + hi) / 2;
      if (v < lo) return elastic ? lo - rubberBand(lo - v, size * 0.25) : lo;
      if (v > hi) return elastic ? hi + rubberBand(v - hi, size * 0.25) : hi;
      return v;
    };
    return [axis(x, left, w, pw), axis(y, top, h, ph)];
  }

  /** Re-apply bounds after the stage resizes. */
  reclamp() {
    if (!this.options.draggable || this.pressing) return;
    const [x, y] = this.clamp(this.x.target, this.y.target, false);
    if (x !== this.x.target || y !== this.y.target) {
      this.x.target = x;
      this.y.target = y;
      this.scene.invalidate();
    }
  }

  /** Called by the stage once glass is ready to appear. */
  show(now = performance.now()) {
    if (this.visible) return;
    this.visible = true;
    this.enterAt = now + this.options.enterDelay;
    this.scene.invalidate();
  }

  private tick = (dt: number, now: number) => {
    const reduced = this.scene.reducedMotion;
    if (this.visible && this.enterAt >= 0 && now >= this.enterAt) {
      this.presence.target = 1;
      this.enterAt = -1;
    }
    const pick = (o: SpringOptions) => (reduced ? calm(o) : o);
    this.press = springStep(this.press, pick(this.pressOptions), dt);
    this.glow = springStep(this.glow, springs.glow, dt);
    this.x = springStep(this.x, pick(this.offsetOptions), dt);
    this.y = springStep(this.y, pick(this.offsetOptions), dt);
    this.presence = springStep(this.presence, pick(springs.morph), dt);

    const el = this.el;
    const w = el.offsetWidth || 1;
    const h = el.offsetHeight || 1;
    // Stretch from the drag spring: its lag behind the finger and its speed.
    const lagX = this.options.draggable ? this.x.target - this.x.value : this.x.value;
    const lagY = this.options.draggable ? this.y.target - this.y.value : this.y.value;
    const interactive = this.options.interactive;
    const { sx, sy } = pressScale(w, h, interactive ? this.press.value : 0, lagX, lagY, this.x.velocity, this.y.velocity, reduced);
    const p = Math.max(0, Math.min(1, this.presence.value));
    const enter = reduced ? 1 : 0.86 + 0.14 * this.presence.value;
    const transform = `translate3d(${this.x.value.toFixed(2)}px, ${this.y.value.toFixed(2)}px, 0) scale(${(sx * enter).toFixed(4)}, ${(sy * enter).toFixed(4)})`;
    const opacity = p >= 0.999 ? "" : p.toFixed(3);
    if (transform !== this.lastTransform) el.style.transform = this.lastTransform = transform;
    if (opacity !== this.lastOpacity) el.style.opacity = this.lastOpacity = opacity;
    this.surface.opacity = p;
    this.surface.glow = interactive ? Math.max(0, this.glow.value) : 0;

    return !(settled(this.press) && settled(this.glow) && settled(this.x) && settled(this.y) && settled(this.presence)) || this.enterAt >= 0;
  };

  destroy() {
    this.removeTicker();
    for (const c of this.cleanups) c();
    window.removeEventListener("pointermove", this.onMove);
    window.removeEventListener("pointerup", this.onUp);
    window.removeEventListener("pointercancel", this.onUp);
  }
}
