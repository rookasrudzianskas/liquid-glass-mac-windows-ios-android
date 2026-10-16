// Development-only renderer for public/og.png: the real WebGL glass from the
// site's renderer, with the title set in Inter, saved through the dev server
// (see the og-save middleware in vite.config.ts). Run `npm run dev`, open
// /og.html and the PNG is written to website/public/og.png.

import "./styles/base.css";
import { GlassRenderer } from "./glass/renderer";
import type { SurfaceFrame } from "./glass/renderer";
import { shapeOutline } from "@lg/reference";

const W = 1200;
const H = 630;
const S = 2;

const status = document.getElementById("status");
const say = (t: string) => {
  if (status) status.textContent = t;
};

function surface(x: number, y: number, width: number, height: number, radius: number, material: "clear" | "regular", dark: boolean, refraction = 60): SurfaceFrame {
  return { x, y, width, height, radius, refraction, material, dark, nested: false, tint: null, opacity: 1, glowX: 0, glowY: 0, glow: 0, glowRadius: 1, contrast: false };
}

async function main() {
  await document.fonts.load('700 64px "Inter Variable"');
  await document.fonts.load('500 28px "Inter Variable"');
  const img = new Image();
  img.src = "/wallpapers/aurora-3072.jpg";
  await img.decode();

  const gl = document.createElement("canvas");
  gl.width = W * S;
  gl.height = H * S;
  const renderer = new GlassRenderer(gl);
  renderer.resize(W, H, W * S, H * S);
  renderer.setImage({ source: img, width: img.naturalWidth, height: img.naturalHeight, focusX: 0.5, focusY: 0.48 });

  const card = { x: 560, y: 330, w: 860, h: 300, r: 46 };
  const lens = { x: 1010, y: 168, d: 210 };
  renderer.render(
    [
      surface(card.x, card.y, card.w, card.h, card.r, "regular", true),
      surface(lens.x, lens.y, lens.d, lens.d, -1, "clear", true),
      surface(560, 548, 330, 58, -1, "regular", true),
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

  // Card content.
  const left = card.x - card.w / 2 + 56;
  ctx.fillStyle = "#ffffff";
  ctx.textBaseline = "alphabetic";
  ctx.font = '600 22px "Inter Variable", sans-serif';
  ctx.globalAlpha = 0.72;
  ctx.fillText("iOS · macOS · Android · Windows", left, card.y - 78);
  ctx.globalAlpha = 1;
  ctx.font = '750 66px "Inter Variable", sans-serif';
  ctx.letterSpacing = "-2.4px";
  ctx.fillText("Liquid Glass", left, card.y - 6);
  ctx.globalAlpha = 0.86;
  ctx.font = '650 66px "Inter Variable", sans-serif';
  ctx.fillText("Engineering", left, card.y + 62);
  ctx.globalAlpha = 0.7;
  ctx.letterSpacing = "0px";
  ctx.font = '500 21px "Inter Variable", sans-serif';
  ctx.fillText("SwiftUI · Jetpack Compose · WPF — one optical model", left, card.y + 112);
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

  // Logo mark (top left), using the spec's continuous corner outline.
  const mark = new Path2D();
  shapeOutline(56, 56, 15, 12).forEach(([x, y], i) => (i ? mark.lineTo(68 + x, 64 + y) : mark.moveTo(68 + x, 64 + y)));
  mark.closePath();
  const g = ctx.createLinearGradient(68, 64, 124, 120);
  g.addColorStop(0, "#3ad9e6");
  g.addColorStop(0.5, "#7a5cff");
  g.addColorStop(1, "#ff5fa2");
  ctx.fillStyle = g;
  // oxlint-disable-next-line unicorn/no-array-fill-with-reference-type -- Path2D, not an array
  ctx.fill(mark);
  ctx.beginPath();
  ctx.arc(96, 91, 14, 0, Math.PI * 2);
  ctx.fillStyle = "rgba(255,255,255,0.2)";
  ctx.fill();
  ctx.strokeStyle = "rgba(255,255,255,0.9)";
  ctx.lineWidth = 2.4;
  ctx.stroke();
  ctx.textAlign = "left";
  ctx.fillStyle = "#ffffff";
  ctx.font = '650 24px "Inter Variable", sans-serif';
  ctx.fillText("liquid-glass-engineering.vercel.app", 142, 100);

  // Downscale to the final 1200 x 630 PNG.
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
