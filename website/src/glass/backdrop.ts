// What a stage refracts: a shared wallpaper (from assets/wallpapers, served
// as AVIF/WebP/JPEG) or a pattern drawn into a canvas (grid and type make the
// bend of the bevel easy to see).

import wallpapers from "../generated/wallpapers.json";
import type { BackdropImage } from "./renderer";
import type { Appearance } from "./scene";

export type WallpaperName = keyof typeof wallpapers.items;
export type Pattern = "grid" | "type" | "404";

export type BackdropSpec = { kind: "wallpaper"; name: WallpaperName } | { kind: "canvas"; pattern: Pattern };

export const wallpaperInfo = wallpapers.items;
export const wallpaperNames = Object.keys(wallpapers.items) as WallpaperName[];

let preferredFormat: "avif" | "webp" | "jpg" | null = null;

/** Remember which format the browser picked for the hero's <picture>. */
export function notePictureFormat(src: string) {
  const ext = /\.(avif|webp|jpg)(?:$|\?)/.exec(src)?.[1];
  if (ext === "avif" || ext === "webp" || ext === "jpg") preferredFormat = ext;
}

export function wallpaperUrl(name: WallpaperName, width: number, format: "avif" | "webp" | "jpg" = "webp") {
  const sizes = wallpapers.sizes;
  const size = sizes.find((s) => s >= width) ?? sizes[sizes.length - 1] ?? 2048;
  return `/wallpapers/${name}-${size}.${format}`;
}

export function wallpaperSrcSet(name: WallpaperName, format: "avif" | "webp" | "jpg") {
  return wallpapers.sizes.map((s) => `/wallpapers/${name}-${s}.${format} ${s}w`).join(", ");
}

/**
 * The wallpaper size for a stage: the same buckets as the CSS fallback in
 * glass.css (.wallpaper-fallback), so the WebGL texture reuses the cached file.
 */
export function wallpaperSize(maxDimension: number, dpr: number) {
  const big = maxDimension > 1024;
  if (dpr >= 1.5) return big ? 3072 : 2048;
  return big ? 2048 : 1024;
}

function neededWidth(canvas: HTMLCanvasElement | null) {
  const w = canvas?.clientWidth || window.innerWidth;
  const h = canvas?.clientHeight || window.innerHeight;
  // Wallpapers are square: cover-fitting needs the larger side.
  return wallpaperSize(Math.max(w, h, window.innerWidth, window.innerHeight), window.devicePixelRatio || 1);
}

function drawPattern(ctx: CanvasRenderingContext2D, pattern: Pattern, w: number, h: number, dark: boolean) {
  const bg = ctx.createLinearGradient(0, 0, w, h);
  if (dark) {
    bg.addColorStop(0, "#0b0d17");
    bg.addColorStop(1, "#141a2e");
  } else {
    bg.addColorStop(0, "#f7f6f2");
    bg.addColorStop(1, "#e9eef6");
  }
  ctx.fillStyle = bg;
  ctx.fillRect(0, 0, w, h);
  if (pattern === "404") {
    const glow = ctx.createRadialGradient(w * 0.5, h * 0.4, 0, w * 0.5, h * 0.4, Math.max(w, h) * 0.6);
    glow.addColorStop(0, dark ? "rgba(122,92,255,0.45)" : "rgba(122,92,255,0.22)");
    glow.addColorStop(1, "rgba(122,92,255,0)");
    ctx.fillStyle = glow;
    ctx.fillRect(0, 0, w, h);
    const size = Math.min(w * 0.36, h * 0.48);
    ctx.font = `800 ${size}px "Inter Variable", "Inter", system-ui, sans-serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    const g = ctx.createLinearGradient(w * 0.2, 0, w * 0.8, 0);
    g.addColorStop(0, "#12b5cb");
    g.addColorStop(0.5, "#6a5cff");
    g.addColorStop(1, "#ff4f9a");
    ctx.fillStyle = g;
    ctx.fillText("404", w / 2, h * 0.4);
    return;
  }
  if (pattern === "grid") {
    const step = 28;
    ctx.lineWidth = 1;
    ctx.strokeStyle = dark ? "rgba(255,255,255,0.16)" : "rgba(20,24,40,0.14)";
    ctx.beginPath();
    for (let x = (w / 2) % step; x < w; x += step) {
      ctx.moveTo(Math.round(x) + 0.5, 0);
      ctx.lineTo(Math.round(x) + 0.5, h);
    }
    for (let y = (h / 2) % step; y < h; y += step) {
      ctx.moveTo(0, Math.round(y) + 0.5);
      ctx.lineTo(w, Math.round(y) + 0.5);
    }
    ctx.stroke();
    const colors = ["#ff5f6d", "#ffb347", "#2bd2ff", "#7c5cff", "#2ee59d"];
    const count = Math.max(6, Math.round((w * h) / 52000));
    let seed = 7;
    const rand = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
    for (let i = 0; i < count; i++) {
      ctx.fillStyle = colors[i % colors.length] ?? "#7c5cff";
      const r = 10 + rand() * 26;
      ctx.beginPath();
      ctx.arc(rand() * w, rand() * h, r, 0, Math.PI * 2);
      ctx.fill();
    }
  } else {
    const words = ["Liquid", "Glass", "SwiftUI", "Compose", "WPF", "Refraction", "Clear", "Regular", "Metal", "AGSL", "HLSL"];
    const size = Math.max(44, Math.min(120, w / 9));
    ctx.textBaseline = "alphabetic";
    let y = size * 0.95;
    let i = 0;
    while (y < h + size) {
      let x = -((i * 0.37 * size) % (size * 2));
      while (x < w) {
        const word = words[i % words.length] ?? "Glass";
        const accent = i % 4 === 1;
        ctx.font = `${accent ? 800 : 700} ${size}px "Inter Variable", "Inter", system-ui, sans-serif`;
        ctx.fillStyle = accent ? (dark ? "#8f7cff" : "#4f3cf0") : dark ? "rgba(255,255,255,0.88)" : "rgba(17,17,19,0.9)";
        ctx.fillText(word, x, y);
        x += ctx.measureText(word).width + size * 0.4;
        i++;
      }
      y += size * 1.08;
    }
  }
}

export interface BackdropController {
  cancel(): void;
}

/** Load a backdrop and call `ready` with an uploadable image (again on redraws). */
export function loadBackdrop(
  spec: BackdropSpec,
  stageCanvas: HTMLCanvasElement | null,
  appearance: Appearance,
  ready: (image: BackdropImage) => void,
): BackdropController {
  if (spec.kind === "wallpaper") {
    const info = wallpapers.items[spec.name];
    let cancelled = false;
    const done = (img: HTMLImageElement) => {
      if (!cancelled) ready({ source: img, width: img.naturalWidth, height: img.naturalHeight, focusX: info.focus[0], focusY: info.focus[1] });
    };
    // Reuse the stage's own <picture> when it shows this wallpaper: no second download.
    const picture = stageCanvas?.parentElement?.querySelector<HTMLImageElement>(".stage__fallback img");
    const matches = (img: HTMLImageElement) => img.currentSrc.includes(`/wallpapers/${spec.name}-`);
    const width = neededWidth(stageCanvas);
    const load = (format: "webp" | "jpg") => {
      const img = new Image();
      img.decoding = "async";
      img.onload = () => {
        // decode() keeps the texture upload off the critical path; never wait on it for long.
        void Promise.race([img.decode().catch(() => undefined), new Promise((r) => setTimeout(r, 250))]).then(() => done(img));
      };
      img.onerror = () => {
        if (format === "webp") load("jpg");
      };
      img.src = wallpaperUrl(spec.name, width, format);
    };
    if (picture && picture.complete && picture.naturalWidth && matches(picture)) done(picture);
    else if (picture && !picture.complete) {
      picture.addEventListener(
        "load",
        () => (matches(picture) ? done(picture) : load(preferredFormat === "jpg" ? "jpg" : "webp")),
        { once: true },
      );
      picture.addEventListener("error", () => load("webp"), { once: true });
    } else load(preferredFormat === "jpg" ? "jpg" : "webp");
    return {
      cancel: () => {
        cancelled = true;
      },
    };
  }

  const off = document.createElement("canvas");
  const ctx = off.getContext("2d");
  let frame = 0;
  const draw = () => {
    frame = 0;
    if (!ctx || !stageCanvas) return;
    const cw = stageCanvas.clientWidth || 1;
    const ch = stageCanvas.clientHeight || 1;
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    off.width = Math.round(cw * dpr);
    off.height = Math.round(ch * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    drawPattern(ctx, spec.pattern, cw, ch, appearance === "dark");
    ready({ source: off, width: cw, height: ch });
  };
  const fontsReady = document.fonts?.ready ?? Promise.resolve();
  void fontsReady.then(draw);
  const observer = new ResizeObserver(() => {
    if (!frame) frame = requestAnimationFrame(draw);
  });
  if (stageCanvas) observer.observe(stageCanvas);
  return {
    cancel: () => {
      observer.disconnect();
      if (frame) cancelAnimationFrame(frame);
    },
  };
}
