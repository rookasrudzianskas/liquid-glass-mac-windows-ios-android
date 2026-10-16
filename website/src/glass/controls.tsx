// Value controls whose thumbs lift into Clear glass while pressed
// (OPTICS.md section 6), plus a glass button. Tracks and fills are painted
// into the scene as solid shapes, so the lifted thumb refracts them.

import { useEffect, useLayoutEffect, useRef } from "react";
import type { CSSProperties, ReactNode, RefObject } from "react";
import { springStep, springs } from "@lg/reference";
import type { SpringOptions, SpringState } from "@lg/reference";
import { Glass, hexToRgb, useStage, useSurface } from "./react";
import type { Radius } from "./scene";

const useIso = typeof window === "undefined" ? useEffect : useLayoutEffect;

type RGBA = readonly [number, number, number, number];

/** Paint an element into the stage's backdrop (WebGL) with a color. */
function useContent(ref: RefObject<HTMLElement | null>, radius: Radius, color: RGBA) {
  const { scene } = useStage();
  const content = useRef<ReturnType<NonNullable<typeof scene>["addContent"]> | null>(null);
  useIso(() => {
    const el = ref.current;
    if (!scene || !el) return;
    const c = scene.addContent(el, radius, color);
    content.current = c;
    return () => {
      scene.removeContent(c);
      content.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scene]);
  return content;
}

const spring = (v = 0): SpringState => ({ value: v, velocity: 0, target: v });
const done = (...list: SpringState[]) => list.every((s) => s.value === s.target && s.velocity === 0);
const calm = (o: SpringOptions, reduced: boolean): SpringOptions => (reduced ? { duration: o.duration, bounce: 0 } : o);
const mixColor = (a: RGBA, b: RGBA, t: number): RGBA => [0, 1, 2, 3].map((i) => (a[i] ?? 0) + ((b[i] ?? 0) - (a[i] ?? 0)) * t) as unknown as RGBA;
const rgba = (hex: string, alpha = 1): RGBA => {
  const c = hexToRgb(hex) ?? [0, 0, 0];
  return [c[0], c[1], c[2], alpha];
};

/** The switch's measured spring: response 0.47, damping 0.7 (COMPONENTS.md). */
const SWITCH_SPRING: SpringOptions = { duration: 0.47, bounce: 0.3 };

// The spec's sizes, scaled up for a showcase on a large screen.
const K = 1.5;

export function GlassSwitch({ checked, onChange, label, tint = "#34c759" }: { checked: boolean; onChange: (v: boolean) => void; label: string; tint?: string }) {
  const trackRef = useRef<HTMLSpanElement>(null);
  const thumbRef = useRef<HTMLSpanElement>(null);
  const knobRef = useRef<HTMLSpanElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const { scene } = useStage();
  const off: RGBA = [0.47, 0.47, 0.5, 0.42];
  const on = rgba(tint, 1);
  const content = useContent(trackRef, "capsule", checked ? on : off);
  const glass = useSurface(thumbRef, { material: "clear", radius: "capsule", refraction: 7.75 * K });
  const checkedRef = useRef(checked);
  checkedRef.current = checked;
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  useIso(() => {
    const track = trackRef.current;
    const thumb = thumbRef.current;
    const knob = knobRef.current;
    const button = buttonRef.current;
    if (!scene || !track || !thumb || !knob || !button) return;
    const W = 54 * K;
    const H = 24 * K;
    const tw = 32 * K;
    const th = 20 * K;
    const inset = (H - th) / 2;
    const travel = W - tw - 2 * inset;
    let pos = spring(checkedRef.current ? 1 : 0);
    let lift = spring(0);
    let liftOptions = springs.press;
    let drag: { id: number; x0: number; p0: number; moved: boolean } | null = null;

    const onDown = (e: PointerEvent) => {
      if (e.button !== 0) return;
      drag = { id: e.pointerId, x0: e.clientX, p0: pos.target, moved: false };
      liftOptions = springs.press;
      lift.target = 1;
      try {
        button.setPointerCapture(e.pointerId);
      } catch {
        /* synthetic or already released pointer */
      }
      scene.invalidate();
    };
    const onMove = (e: PointerEvent) => {
      if (!drag || e.pointerId !== drag.id) return;
      const dx = e.clientX - drag.x0;
      if (Math.abs(dx) > 4) drag.moved = true;
      if (drag.moved) pos.target = Math.min(1, Math.max(0, drag.p0 + dx / travel));
      scene.invalidate();
    };
    const onUp = (e: PointerEvent) => {
      if (!drag || e.pointerId !== drag.id) return;
      const moved = drag.moved;
      drag = null;
      liftOptions = springs.release;
      lift.target = 0;
      if (moved) {
        const next = pos.target > 0.5;
        pos.target = next ? 1 : 0;
        if (next !== checkedRef.current) onChangeRef.current(next);
        suppressClick = true;
      }
      scene.invalidate();
    };
    let suppressClick = false;
    const onClick = (e: MouseEvent) => {
      if (suppressClick) {
        suppressClick = false;
        e.preventDefault();
        e.stopImmediatePropagation();
      }
    };
    button.addEventListener("pointerdown", onDown);
    button.addEventListener("pointermove", onMove);
    button.addEventListener("pointerup", onUp);
    button.addEventListener("pointercancel", onUp);
    button.addEventListener("click", onClick, true);

    let last = "";
    const tick = (dt: number) => {
      const reduced = scene.reducedMotion;
      if (!drag) pos.target = checkedRef.current ? 1 : 0;
      pos = springStep(pos, calm(SWITCH_SPRING, reduced), dt);
      lift = springStep(lift, calm(liftOptions, reduced), dt);
      const l = Math.max(0, lift.value);
      const growX = reduced ? 0 : 9 * K * l;
      const growY = reduced ? 0 : 5.5 * K * l;
      const x = inset + travel * pos.value;
      const transform = `translate3d(${x.toFixed(2)}px, 0, 0) scale(${((tw + 2 * growX) / tw).toFixed(4)}, ${((th + 2 * growY) / th).toFixed(4)})`;
      if (transform !== last) thumb.style.transform = last = transform;
      knob.style.opacity = (1 - Math.min(1, l)).toFixed(3);
      if (glass) glass.opacity = Math.min(1, l);
      if (content.current) content.current.color = mixColor(off, on, Math.min(1, Math.max(0, pos.value)));
      return !done(pos, lift) || drag !== null;
    };
    const remove = scene.addTicker(tick);
    return () => {
      remove();
      button.removeEventListener("pointerdown", onDown);
      button.removeEventListener("pointermove", onMove);
      button.removeEventListener("pointerup", onUp);
      button.removeEventListener("pointercancel", onUp);
      button.removeEventListener("click", onClick, true);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scene, glass]);

  useEffect(() => scene?.invalidate(), [checked, scene]);

  return (
    <button
      ref={buttonRef}
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      className="glass-switch"
      style={{ width: 54 * K, height: 24 * K, "--switch-on": tint } as CSSProperties}
      onClick={() => onChange(!checked)}
    >
      <span ref={trackRef} className="glass-switch__track glass-content" data-on={checked ? "" : undefined} />
      <span ref={thumbRef} className="glass-switch__thumb" style={{ width: 32 * K, height: 20 * K, top: (24 * K - 20 * K) / 2 }}>
        <span ref={knobRef} className="glass-switch__knob" />
      </span>
    </button>
  );
}

export function GlassSlider({ value, onChange, label, tint = "#0a84ff", min = 0, max = 100 }: { value: number; onChange: (v: number) => void; label: string; tint?: string; min?: number; max?: number }) {
  const rootRef = useRef<HTMLDivElement>(null);
  const trackRef = useRef<HTMLDivElement>(null);
  const fillRef = useRef<HTMLDivElement>(null);
  const thumbRef = useRef<HTMLDivElement>(null);
  const knobRef = useRef<HTMLSpanElement>(null);
  const { scene } = useStage();
  useContent(trackRef, "capsule", [0.47, 0.47, 0.5, 0.34]);
  useContent(fillRef, "capsule", rgba(tint, 1));
  const glass = useSurface(thumbRef, { material: "clear", radius: "capsule", refraction: 5 * K });
  const valueRef = useRef(value);
  valueRef.current = value;
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  useIso(() => {
    const root = rootRef.current;
    const thumb = thumbRef.current;
    const fill = fillRef.current;
    const knob = knobRef.current;
    if (!scene || !root || !thumb || !fill || !knob) return;
    const tw = 20 * K;
    const th = 16 * K;
    let pos = spring((valueRef.current - min) / (max - min));
    let lift = spring(0);
    let liftOptions = springs.press;
    let pointer: number | null = null;

    const setFromPointer = (clientX: number) => {
      const rect = root.getBoundingClientRect();
      const t = Math.min(1, Math.max(0, (clientX - rect.left - tw / 2) / Math.max(1, rect.width - tw)));
      const v = Math.round(min + t * (max - min));
      if (v !== valueRef.current) onChangeRef.current(v);
    };
    const onDown = (e: PointerEvent) => {
      if (e.button !== 0) return;
      pointer = e.pointerId;
      liftOptions = springs.press;
      lift.target = 1;
      setFromPointer(e.clientX);
      try {
        root.setPointerCapture(e.pointerId);
      } catch {
        /* synthetic or already released pointer */
      }
      scene.invalidate();
    };
    const onMove = (e: PointerEvent) => {
      if (e.pointerId !== pointer) return;
      setFromPointer(e.clientX);
    };
    const onUp = (e: PointerEvent) => {
      if (e.pointerId !== pointer) return;
      pointer = null;
      liftOptions = springs.release;
      lift.target = 0;
      scene.invalidate();
    };
    root.addEventListener("pointerdown", onDown);
    root.addEventListener("pointermove", onMove);
    root.addEventListener("pointerup", onUp);
    root.addEventListener("pointercancel", onUp);

    let last = "";
    const tick = (dt: number) => {
      const reduced = scene.reducedMotion;
      pos.target = (valueRef.current - min) / (max - min);
      pos = springStep(pos, calm(springs.track, reduced), dt);
      lift = springStep(lift, calm(liftOptions, reduced), dt);
      const l = Math.max(0, lift.value);
      const width = root.clientWidth;
      const x = (width - tw) * Math.min(1, Math.max(0, pos.value));
      const sx = reduced ? 1 : (tw + 2 * 2.5 * K * l) / tw;
      const sy = reduced ? 1 : (th + 2 * 2 * K * l) / th;
      const transform = `translate3d(${x.toFixed(2)}px, 0, 0) scale(${sx.toFixed(4)}, ${sy.toFixed(4)})`;
      if (transform !== last) thumb.style.transform = last = transform;
      fill.style.width = `${(x + tw / 2).toFixed(2)}px`;
      knob.style.opacity = (1 - Math.min(1, l)).toFixed(3);
      if (glass) glass.opacity = Math.min(1, l);
      return !done(pos, lift) || pointer !== null;
    };
    const remove = scene.addTicker(tick);
    return () => {
      remove();
      root.removeEventListener("pointerdown", onDown);
      root.removeEventListener("pointermove", onMove);
      root.removeEventListener("pointerup", onUp);
      root.removeEventListener("pointercancel", onUp);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scene, glass]);

  useEffect(() => scene?.invalidate(), [value, scene]);

  return (
    <div
      ref={rootRef}
      className="glass-slider"
      role="slider"
      tabIndex={0}
      aria-label={label}
      aria-valuemin={min}
      aria-valuemax={max}
      aria-valuenow={value}
      style={{ height: 16 * K + 12 }}
      onKeyDown={(e) => {
        const step = e.shiftKey ? 10 : 1;
        let next = value;
        if (e.key === "ArrowRight" || e.key === "ArrowUp") next = value + step;
        else if (e.key === "ArrowLeft" || e.key === "ArrowDown") next = value - step;
        else if (e.key === "Home") next = min;
        else if (e.key === "End") next = max;
        else if (e.key === "PageUp") next = value + 10;
        else if (e.key === "PageDown") next = value - 10;
        else return;
        e.preventDefault();
        onChange(Math.min(max, Math.max(min, next)));
      }}
    >
      <div ref={trackRef} className="glass-slider__track glass-content" />
      <div ref={fillRef} className="glass-slider__fill glass-content" style={{ background: tint }} />
      <div ref={thumbRef} className="glass-slider__thumb" style={{ width: 20 * K, height: 16 * K }}>
        <span ref={knobRef} className="glass-slider__knob" />
      </div>
    </div>
  );
}

/** A glass button: the surface presses, stretches and glows; the label sits on top. */
export function GlassButton({
  children,
  size = "regular",
  tint = null,
  material = "regular",
  icon = false,
  onClick,
  label,
  enterDelay = 0,
}: {
  children: ReactNode;
  size?: "small" | "regular" | "large";
  tint?: string | null;
  material?: "clear" | "regular";
  icon?: boolean;
  onClick?: () => void;
  label?: string;
  enterDelay?: number;
}) {
  const height = size === "small" ? 34 : size === "large" ? 52 : 44;
  return (
    <Glass
      className={`glass-button glass-button--${size}${icon ? " glass-button--icon" : ""}${tint ? " glass-button--tinted" : ""}`}
      material={material}
      radius="capsule"
      tint={tint}
      interactive
      stretch
      enterDelay={enterDelay}
      style={{ height, ...(icon ? { width: height } : {}) }}
    >
      <button type="button" className="glass-button__hit" onClick={onClick} aria-label={label}>
        {children}
      </button>
    </Glass>
  );
}
