import { createContext, useContext, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import type { CSSProperties, HTMLAttributes, ReactNode, RefObject } from "react";
import { pressScale, springStep, springs } from "@lg/reference";
import type { SpringState } from "@lg/reference";
import { GlassScene } from "./scene";
import type { Appearance, Radius, RendererKind, Surface, SurfaceOptions } from "./scene";
import { SurfaceMotion } from "./motion";
import type { BackdropImage, Material } from "./renderer";
import { loadBackdrop } from "./backdrop";
import type { BackdropSpec } from "./backdrop";

const useIsoLayoutEffect = typeof window === "undefined" ? useEffect : useLayoutEffect;

interface StageValue {
  scene: GlassScene | null;
  kind: RendererKind;
  appearance: Appearance;
}

const StageContext = createContext<StageValue>({ scene: null, kind: "pending", appearance: "light" });

export const useStage = () => useContext(StageContext);

export function hexToRgb(hex: string | null | undefined): [number, number, number] | null {
  if (!hex) return null;
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m || !m[1]) return null;
  const n = parseInt(m[1], 16);
  return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
}

export interface GlassStageProps {
  /** null defers loading (for example until the theme is known). */
  backdrop: BackdropSpec | null;
  appearance: Appearance;
  className?: string;
  style?: CSSProperties;
  children?: ReactNode;
  /** Rendered under the canvas: the static image shown before and without WebGL. */
  fallback?: ReactNode;
  /** Cross-fade duration when the backdrop changes. */
  transition?: number;
  onRenderer?: (kind: RendererKind, reason?: string) => void;
}

/** Cover-fit a backdrop into a 2D canvas, keeping its focal point in view (as the WebGL renderer does). */
function paintBackdrop(canvas: HTMLCanvasElement, image: BackdropImage) {
  const w = canvas.clientWidth;
  const h = canvas.clientHeight;
  const ctx = w && h ? canvas.getContext("2d") : null;
  if (!ctx) return;
  const dpr = Math.min(window.devicePixelRatio || 1, 2);
  canvas.width = Math.round(w * dpr);
  canvas.height = Math.round(h * dpr);
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const scale = Math.max(w / image.width, h / image.height);
  const dw = image.width * scale;
  const dh = image.height * scale;
  const x = Math.min(0, Math.max(w - dw, w / 2 - (image.focusX ?? 0.5) * dw));
  const y = Math.min(0, Math.max(h - dh, h / 2 - (image.focusY ?? 0.5) * dh));
  ctx.imageSmoothingQuality = "high";
  ctx.drawImage(image.source as CanvasImageSource, x, y, dw, dh);
}

export function GlassStage({ backdrop, appearance, className, style, children, fallback, transition = 650, onRenderer }: GlassStageProps) {
  const rootRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  // Without WebGL the chosen backdrop is painted into a plain 2D canvas instead,
  // so changing it still shows (the fallback material blurs it like glass).
  const flatRef = useRef<HTMLCanvasElement>(null);
  const shown = useRef<BackdropImage | null>(null);
  const [scene, setScene] = useState<GlassScene | null>(null);
  const [kind, setKind] = useState<RendererKind>("pending");
  const kindRef = useRef(kind);
  kindRef.current = kind;
  const paintFlat = () => {
    if (kindRef.current === "css" && flatRef.current && shown.current) paintBackdrop(flatRef.current, shown.current);
  };
  const callback = useRef(onRenderer);
  callback.current = onRenderer;
  const appearanceRef = useRef(appearance);
  appearanceRef.current = appearance;

  useEffect(() => {
    const root = rootRef.current;
    const canvas = canvasRef.current;
    if (!root || !canvas) return;
    let s: GlassScene | null = null;
    // Start the GPU work once the page has painted and hydrated: glass is
    // decoration, the content comes first.
    const start = () => {
      s = new GlassScene(root, canvas, {
        appearance: appearanceRef.current,
        onRenderer: (k, reason) => {
          setKind(k);
          callback.current?.(k, reason);
        },
      });
      setScene(s);
      if (s.kind === "css") setKind("css");
      if (import.meta.env.DEV) ((window as unknown as { __lgScenes?: GlassScene[] }).__lgScenes ??= []).push(s);
    };
    // Safari has no requestIdleCallback.
    const idle = "requestIdleCallback" in window;
    const handle = idle ? window.requestIdleCallback(start, { timeout: 900 }) : window.setTimeout(start, 120);
    return () => {
      if (idle) window.cancelIdleCallback(handle);
      else window.clearTimeout(handle);
      s?.destroy();
      setScene(null);
    };
    // The scene lives as long as the stage; appearance updates below.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    scene?.setAppearance(appearance);
  }, [scene, appearance]);

  // Load and show the backdrop; changes cross-fade.
  const first = useRef(true);
  const backdropKey = JSON.stringify(backdrop);
  useEffect(() => {
    if (!scene || !backdrop) return;
    let cancelled = false;
    const canvas = canvasRef.current;
    const controller = loadBackdrop(backdrop, canvas, appearance, (image) => {
      if (cancelled) return;
      if (backdrop.kind === "canvas" && !first.current) scene.refreshBackdrop(image);
      else scene.setBackdrop(image, first.current ? 0 : transition);
      first.current = false;
      shown.current = image;
      paintFlat();
    });
    return () => {
      cancelled = true;
      controller.cancel();
    };
    // backdropKey captures the spec; appearance matters for drawn backdrops.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scene, backdropKey, backdrop?.kind === "canvas" ? appearance : null]);

  // The flat backdrop follows the stage's size while the CSS renderer is in use.
  useEffect(() => {
    const flat = flatRef.current;
    if (kind !== "css" || !flat) return;
    paintFlat();
    let frame = 0;
    const observer = new ResizeObserver(() => {
      if (!frame) frame = requestAnimationFrame(() => ((frame = 0), paintFlat()));
    });
    observer.observe(flat);
    return () => {
      observer.disconnect();
      if (frame) cancelAnimationFrame(frame);
    };
    // paintFlat reads refs only.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kind]);

  const value = useMemo(() => ({ scene, kind, appearance }), [scene, kind, appearance]);

  return (
    <div ref={rootRef} className={`stage${className ? ` ${className}` : ""}`} style={style} data-renderer={kind}>
      {fallback}
      <canvas ref={flatRef} className="stage__flat" aria-hidden="true" hidden={kind !== "css"} />
      <canvas ref={canvasRef} className="stage__canvas" aria-hidden="true" />
      <StageContext.Provider value={value}>{children}</StageContext.Provider>
    </div>
  );
}

export interface SurfaceProps {
  material?: Material;
  radius?: Radius;
  refraction?: number;
  tint?: string | null;
  nested?: boolean;
  appearance?: Appearance;
}

/** Register an element as a glass surface of the enclosing stage. */
export function useSurface(ref: RefObject<HTMLElement | null>, props: SurfaceProps): Surface | null {
  const { scene } = useStage();
  const [surface, setSurface] = useState<Surface | null>(null);
  const options: SurfaceOptions = {
    material: props.material ?? "clear",
    radius: props.radius ?? 16,
    refraction: props.refraction ?? 60,
    tint: hexToRgb(props.tint),
    nested: props.nested ?? false,
    ...(props.appearance ? { appearance: props.appearance } : {}),
  };
  const key = JSON.stringify(options);

  useIsoLayoutEffect(() => {
    const el = ref.current;
    if (!scene || !el) return;
    const s = scene.add(el, options);
    setSurface(s);
    return () => {
      scene.remove(s);
      setSurface(null);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scene]);

  useIsoLayoutEffect(() => {
    if (surface && scene) {
      surface.options = options;
      scene.invalidate();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [surface, scene, key]);

  return surface;
}

export function radiusStyle(radius: Radius): CSSProperties {
  return radius === "capsule"
    ? ({ "--glass-r": "9999px", "--glass-rc": "9999px" } as CSSProperties)
    : ({ "--glass-r": `${radius}px`, "--glass-rc": `${(radius * 1.528).toFixed(2)}px` } as CSSProperties);
}

export interface GlassProps extends SurfaceProps, Omit<HTMLAttributes<HTMLDivElement>, "draggable"> {
  interactive?: boolean;
  draggable?: boolean;
  stretch?: boolean;
  enterDelay?: number;
  keyboardStep?: number;
  bounds?: "stage" | "parent";
  ref?: RefObject<HTMLDivElement | null>;
  onMotion?: (motion: SurfaceMotion | null) => void;
}

/** A DOM element that is glass: its children draw above the refracting surface. */
export function Glass({
  material = "clear",
  radius = 16,
  refraction = 60,
  tint = null,
  nested = false,
  appearance,
  interactive = false,
  draggable = false,
  stretch = false,
  enterDelay = 0,
  keyboardStep = 24,
  bounds = "stage",
  className,
  style,
  children,
  ref,
  onMotion,
  ...rest
}: GlassProps) {
  const own = useRef<HTMLDivElement>(null);
  const elRef = ref ?? own;
  const { scene, kind } = useStage();
  const surface = useSurface(elRef, { material, radius, refraction, tint, nested, ...(appearance ? { appearance } : {}) });
  const motionRef = useRef<SurfaceMotion | null>(null);
  const motionCallback = useRef(onMotion);
  motionCallback.current = onMotion;

  useIsoLayoutEffect(() => {
    if (!scene || !surface) return;
    const m = new SurfaceMotion(scene, surface, { interactive, draggable, stretch, enterDelay, keyboardStep, bounds });
    motionRef.current = m;
    motionCallback.current?.(m);
    return () => {
      m.destroy();
      motionRef.current = null;
      motionCallback.current?.(null);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scene, surface]);

  useEffect(() => {
    const m = motionRef.current;
    if (m) m.options = { interactive, draggable, stretch, enterDelay, keyboardStep, bounds };
  }, [interactive, draggable, stretch, enterDelay, keyboardStep, bounds]);

  useEffect(() => {
    if (kind !== "pending") motionRef.current?.show();
  }, [kind, surface]);

  const tintStyle = tint ? ({ "--glass-tint": tint } as CSSProperties) : undefined;
  return (
    <div
      ref={elRef}
      className={`glass${className ? ` ${className}` : ""}`}
      data-material={material}
      data-capsule={radius === "capsule" ? "" : undefined}
      data-tinted={tint ? "" : undefined}
      data-draggable={draggable ? "" : undefined}
      style={{ ...radiusStyle(radius), ...tintStyle, ...style }}
      {...rest}
    >
      {children}
    </div>
  );
}

export interface TabItem {
  id: string;
  label: string;
  icon?: ReactNode;
}

export interface GlassTabBarProps {
  items: readonly TabItem[];
  value: string;
  onChange: (id: string) => void;
  label: string;
  material?: Material;
  tint?: string | null;
  className?: string;
  enterDelay?: number;
  lensRefraction?: number;
}

/**
 * Tabs with a moving glass lens (COMPONENTS.md behavior 4): the lens slides
 * on the track spring, stretches with its speed and can be dragged.
 */
export function GlassTabBar({ items, value, onChange, label, material = "regular", tint = null, className, enterDelay = 0, lensRefraction = 24 }: GlassTabBarProps) {
  const barRef = useRef<HTMLDivElement>(null);
  const lensRef = useRef<HTMLDivElement>(null);
  const { scene } = useStage();
  const [barMotion, setBarMotion] = useState<SurfaceMotion | null>(null);
  const barSurface = useRef<Surface | null>(null);
  const lens = useSurface(lensRef, { material: "clear", radius: "capsule", refraction: lensRefraction, nested: true });
  const valueRef = useRef(value);
  valueRef.current = value;
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;
  const retargetRef = useRef<(() => void) | null>(null);

  useIsoLayoutEffect(() => {
    const bar = barRef.current;
    const lensEl = lensRef.current;
    if (!scene || !lens || !bar || !lensEl) return;
    let x: SpringState = { value: 0, velocity: 0, target: 0 };
    let w: SpringState = { value: 0, velocity: 0, target: 0 };
    let press: SpringState = { value: 0, velocity: 0, target: 0 };
    let pressOptions = springs.press;
    let initialized = false;
    let dragging: { id: number; startX: number; moved: boolean } | null = null;

    const slot = (id: string) => {
      const button = bar.querySelector<HTMLElement>(`[data-tab="${CSS.escape(id)}"]`);
      return button ? { left: button.offsetLeft, width: button.offsetWidth } : null;
    };
    const retarget = () => {
      const s = slot(valueRef.current);
      if (!s) return;
      if (!dragging?.moved) x.target = s.left;
      w.target = s.width;
      if (!initialized) {
        x = { value: s.left, velocity: 0, target: s.left };
        w = { value: s.width, velocity: 0, target: s.width };
        initialized = true;
      }
      scene.invalidate();
    };
    retarget();
    const resize = new ResizeObserver(retarget);
    resize.observe(bar);

    const onDown = (e: PointerEvent) => {
      if (e.button !== 0 || dragging) return;
      dragging = { id: e.pointerId, startX: e.clientX, moved: false };
      pressOptions = springs.press;
      press.target = 1;
      window.addEventListener("pointermove", onMove);
      window.addEventListener("pointerup", onUp);
      window.addEventListener("pointercancel", onUp);
      scene.invalidate();
    };
    const onMove = (e: PointerEvent) => {
      if (!dragging || e.pointerId !== dragging.id) return;
      if (Math.abs(e.clientX - dragging.startX) > 6) dragging.moved = true;
      if (!dragging.moved) return;
      const rect = bar.getBoundingClientRect();
      const scale = rect.width / bar.offsetWidth || 1;
      const inset = 4;
      const px = (e.clientX - rect.left) / scale;
      x.target = Math.max(inset, Math.min(bar.offsetWidth - inset - w.target, px - w.target / 2));
      scene.invalidate();
    };
    const onUp = (e: PointerEvent) => {
      if (!dragging || e.pointerId !== dragging.id) return;
      const moved = dragging.moved;
      dragging = null;
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
      window.removeEventListener("pointercancel", onUp);
      pressOptions = springs.release;
      press.target = 0;
      if (moved) {
        // Snap to the nearest item.
        const center = x.target + w.target / 2;
        let best = valueRef.current;
        let bestDistance = Infinity;
        for (const button of bar.querySelectorAll<HTMLElement>("[data-tab]")) {
          const d = Math.abs(button.offsetLeft + button.offsetWidth / 2 - center);
          if (d < bestDistance) {
            bestDistance = d;
            best = button.dataset.tab ?? best;
          }
        }
        if (best !== valueRef.current) onChangeRef.current(best);
        const s = slot(best);
        if (s) {
          x.target = s.left;
          w.target = s.width;
        }
      }
      scene.invalidate();
    };
    bar.addEventListener("pointerdown", onDown);

    let lastTransform = "";
    const tick = (dt: number) => {
      const reduced = scene.reducedMotion;
      const track = reduced ? { ...springs.track, bounce: 0 } : springs.track;
      const pressSpring = reduced ? { ...pressOptions, bounce: 0 } : pressOptions;
      x = springStep(x, track, dt);
      w = springStep(w, track, dt);
      press = springStep(press, pressSpring, dt);
      const width = Math.max(1, w.value);
      const height = lensEl.offsetHeight || 1;
      const { sx, sy } = pressScale(width, height, press.value, 0, 0, x.velocity, 0, reduced);
      lensEl.style.width = `${width.toFixed(2)}px`;
      const transform = `translate3d(${x.value.toFixed(2)}px, 0, 0) scale(${sx.toFixed(4)}, ${sy.toFixed(4)})`;
      if (transform !== lastTransform) lensEl.style.transform = lastTransform = transform;
      lens.opacity = barSurface.current?.opacity ?? 1;
      lensEl.style.opacity = lens.opacity >= 0.999 ? "" : lens.opacity.toFixed(3);
      const done = [x, w, press].every((s) => s.value === s.target && s.velocity === 0);
      return !done;
    };
    const removeTicker = scene.addTicker(tick);
    retargetRef.current = retarget;
    return () => {
      removeTicker();
      resize.disconnect();
      bar.removeEventListener("pointerdown", onDown);
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
      window.removeEventListener("pointercancel", onUp);
      retargetRef.current = null;
    };
  }, [scene, lens]);

  useEffect(() => {
    retargetRef.current?.();
  }, [value]);

  const focusIndex = (index: number) => {
    const item = items[(index + items.length) % items.length];
    if (!item) return;
    onChange(item.id);
    barRef.current?.querySelector<HTMLElement>(`[data-tab="${CSS.escape(item.id)}"]`)?.focus();
  };

  return (
    <Glass
      ref={barRef}
      className={`tabbar${className ? ` ${className}` : ""}`}
      material={material}
      radius="capsule"
      tint={tint}
      interactive
      enterDelay={enterDelay}
      role="radiogroup"
      aria-label={label}
      onMotion={(m) => {
        setBarMotion(m);
        barSurface.current = m ? findSurface(scene, barRef.current) : null;
      }}
      data-motion={barMotion ? "" : undefined}
    >
      <div ref={lensRef} className="tabbar__lens" aria-hidden="true" />
      {items.map((item, index) => {
        const selected = item.id === value;
        return (
          <button
            key={item.id}
            type="button"
            role="radio"
            aria-checked={selected}
            tabIndex={selected ? 0 : -1}
            data-tab={item.id}
            className="tabbar__item"
            onClick={() => onChange(item.id)}
            onKeyDown={(e) => {
              if (e.key === "ArrowRight" || e.key === "ArrowDown") {
                e.preventDefault();
                focusIndex(index + 1);
              } else if (e.key === "ArrowLeft" || e.key === "ArrowUp") {
                e.preventDefault();
                focusIndex(index - 1);
              } else if (e.key === "Home") {
                e.preventDefault();
                focusIndex(0);
              } else if (e.key === "End") {
                e.preventDefault();
                focusIndex(items.length - 1);
              }
            }}
          >
            {item.icon}
            <span>{item.label}</span>
          </button>
        );
      })}
    </Glass>
  );
}

function findSurface(scene: GlassScene | null, el: HTMLElement | null): Surface | null {
  if (!scene || !el) return null;
  return scene.surfaceFor(el);
}
