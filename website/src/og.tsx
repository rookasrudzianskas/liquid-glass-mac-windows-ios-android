// Development-only renderer for public/og.png: the real WebGL glass from the
// site's renderer, with the title set in Inter, saved through the dev server
// (see the og-save middleware in vite.config.ts). Run `npm run dev`, open
// /og.html and the PNG is written to website/public/og.png.

import "./styles/base.css";
import { GlassRenderer } from "./glass/renderer";
import type { SurfaceFrame } from "./glass/renderer";
import { defaultRefraction } from "@lg/reference";

const W = 1200;
const H = 630;
const S = 2;

const status = document.getElementById("status");
const say = (t: string) => {
  if (status) status.textContent = t;
};

// No surface sets a refraction, so each follows its size (OPTICS.md section 2).
function surface(x: number, y: number, width: number, height: number, radius: number, material: "clear" | "regular", dark: boolean, nested = false): SurfaceFrame {
  return { x, y, width, height, radius, refraction: defaultRefraction(width, height), material, dark, nested, tint: null, opacity: 1, glowX: 0, glowY: 0, glow: 0, glowRadius: 1, contrast: false };
}

async function main() {
  await document.fonts.load('700 64px "Inter Variable"');
  await document.fonts.load('500 28px "Inter Variable"');
  const img = new Image();
  img.src = "/wallpapers/aurora-3072.jpg";
  await img.decode();
  // The site's mark (waves under a lens), as in the favicon and the app icons.
  const logo = new Image();
  logo.src = "/favicon.svg";
  await logo.decode();

  const gl = document.createElement("canvas");
  gl.width = W * S;
  gl.height = H * S;
  const renderer = new GlassRenderer(gl);
  renderer.resize(W, H, W * S, H * S);
  renderer.setImage({ source: img, width: img.naturalWidth, height: img.naturalHeight, focusX: 0.5, focusY: 0.48 });

  const card = { x: 560, y: 330, w: 860, h: 300, r: 46 };
  // Clear of the card's corner, so it floats beside the card rather than covering it.
  const lens = { x: 1075, y: 125, d: 210 };
  const bar = { x: 560, y: 548, w: 330, h: 58 };
  renderer.render(
    [
      surface(card.x, card.y, card.w, card.h, card.r, "regular", true),
      surface(lens.x, lens.y, lens.d, lens.d, -1, "clear", true),
      surface(bar.x, bar.y, bar.w, bar.h, -1, "regular", true),
      // The selection lens on "Regular", as in every tab bar of the libraries.
      surface(bar.x, bar.y, bar.w / 3 - 6, bar.h - 10, -1, "clear", true, true),
    ],
    true,
  );

  const out = document.createElement("canvas");
  out.width = W * S;
  out.height = H * S;
  const ctx = out.getContext("2d");
  if (!ctx) throw new Error("2d context");
  ctx.drawImage(gl, 0, 0);
  ctx.scale(S, S);

  // White text over light wallpaper or light glass gets a dimmed backdrop so it reads at feed size: at least
  // WCAG 4.5:1 against the pixels behind it, measured on the PNG. Canvas shadows and filters ignore the transform,
  // so their blur is given in output pixels.
  const ink = "4, 14, 34";
  // A soft halo: first only the text's dark blurred shadow (the text is drawn off the canvas, its shadow offset
  // back under the real line), then the text itself.
  const halo = (draw: () => void, alpha: number, blur: number) => {
    ctx.save();
    ctx.translate(-2 * W, 0);
    ctx.shadowOffsetX = 2 * W * S;
    ctx.shadowColor = `rgba(${ink}, ${alpha})`;
    ctx.shadowBlur = blur * S;
    draw();
    ctx.restore();
    draw();
  };
  // A feathered band of the same dimming under a line of text on the card, clipped to the card.
  const band = (x: number, y: number, w: number, h: number, alpha: number) => {
    ctx.save();
    ctx.beginPath();
    ctx.roundRect(card.x - card.w / 2, card.y - card.h / 2, card.w, card.h, card.r);
    ctx.clip();
    ctx.filter = `blur(${14 * S}px)`;
    ctx.fillStyle = `rgba(${ink}, ${alpha})`;
    ctx.beginPath();
    ctx.roundRect(x, y, w, h, 24);
    ctx.fill();
    ctx.restore();
  };

  // The mark and domain sit on the light aqua top band: a feathered scrim (an ellipse that fades out above the
  // card and well clear of the round lens) dims the band behind them.
  ctx.save();
  ctx.translate(310, 88);
  ctx.scale(420, 90);
  const scrim = ctx.createRadialGradient(0, 0, 0, 0, 0, 1);
  const falloff: [number, number][] = [[0, 0.58], [0.45, 0.56], [0.6, 0.5], [0.72, 0.38], [0.82, 0.24], [0.9, 0.12], [0.96, 0.04], [1, 0]];
  for (const [at, alpha] of falloff) scrim.addColorStop(at, `rgba(${ink}, ${alpha})`);
  ctx.fillStyle = scrim;
  ctx.fillRect(-1, -1, 2, 2);
  ctx.restore();

  // Card content. The kicker sits where the card's glass is lightest (the teal behind its top-left corner) and
  // the subtitle over the pink at its bottom right, so both are at 0.9 (from 0.7), haloed, over a faint band.
  const left = card.x - card.w / 2 + 56;
  ctx.fillStyle = "#ffffff";
  ctx.textBaseline = "alphabetic";
  band(left - 24, card.y - 117, 389, 62, 0.32);
  ctx.font = '600 22px "Inter Variable", sans-serif';
  ctx.globalAlpha = 0.9;
  halo(() => ctx.fillText("iOS · macOS · Android · Windows", left, card.y - 78), 0.4, 8);
  ctx.globalAlpha = 1;
  ctx.font = '750 66px "Inter Variable", sans-serif';
  ctx.letterSpacing = "-2.4px";
  ctx.fillText("Liquid Glass", left, card.y - 6);
  ctx.globalAlpha = 0.86;
  ctx.font = '650 66px "Inter Variable", sans-serif';
  ctx.fillText("Engineering", left, card.y + 62);
  ctx.globalAlpha = 1;
  ctx.letterSpacing = "0px";
  band(left - 24, card.y + 76, 581, 64, 0.32);
  ctx.globalAlpha = 0.9;
  ctx.font = '500 21px "Inter Variable", sans-serif';
  halo(() => ctx.fillText("SwiftUI · Jetpack Compose · WPF — one optical model", left, card.y + 112), 0.4, 8);
  ctx.globalAlpha = 1;

  // Tab bar labels.
  ctx.font = '600 17px "Inter Variable", sans-serif';
  ctx.textAlign = "center";
  const labels = ["Clear", "Regular", "Tint"];
  labels.forEach((l, i) => {
    ctx.globalAlpha = i === 1 ? 1 : 0.8;
    ctx.fillText(l, 560 - 105 + i * 105, 554);
  });
  ctx.globalAlpha = 1;

  // Logo mark (top left): the favicon's waves under a lens.
  ctx.drawImage(logo, 68, 64, 56, 56);
  ctx.textAlign = "left";
  ctx.fillStyle = "#ffffff";
  ctx.font = '650 24px "Inter Variable", sans-serif';
  halo(() => ctx.fillText("liquid-glass-engineering.vercel.app", 142, 100), 0.35, 6);

  // Downscale to the final 1200 x 630 PNG. It is saved as the canvas encodes it (full color). A lossless re-encode
  // (RGB, maximum compression) halves the file; do not quantize it to a palette, which speckles the glass with dithering.
  const final = document.createElement("canvas");
  final.width = W;
  final.height = H;
  const fctx = final.getContext("2d");
  if (!fctx) throw new Error("2d context");
  fctx.imageSmoothingQuality = "high";
  fctx.drawImage(out, 0, 0, W, H);
  document.body.appendChild(final);

  const blob = await new Promise<Blob | null>((r) => final.toBlob(r, "image/png"));
  if (!blob) throw new Error("toBlob failed");
  const res = await fetch("/__og/save", { method: "POST", body: blob });
  say(res.ok ? `Saved public/og.png (${blob.size} bytes)` : `Save failed: ${res.status}`);
}

main().catch((e: unknown) => say(`Failed: ${e instanceof Error ? e.message : String(e)}`));
