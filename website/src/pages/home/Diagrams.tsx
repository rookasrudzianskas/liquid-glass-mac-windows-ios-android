// Diagrams of the optical model, computed from spec/reference.mjs itself:
// the curves on this page are the functions every port is tested against.

import { clearTone, edgeProfile, regularTone, rimOverlay, shapeField, shapeOutline, shapeParams, springStep, springs } from "@lg/reference";
import type { SpringOptions } from "@lg/reference";

const f = (n: number) => n.toFixed(2);
const pathFrom = (pts: [number, number][], close = false) => `M${pts.map(([x, y]) => `${f(x)} ${f(y)}`).join("L")}${close ? "Z" : ""}`;

/** Continuous corner (superellipse, n = 3.26, 1.528 r) against a circular arc of the same radius. */
export function CornerDiagram() {
  const r = 120;
  const size = 520;
  // The top-right corner of a 520 x 520 outline, mapped into the view.
  const corner = shapeOutline(size, size, r, 64).slice(0, 65);
  const view = (pts: [number, number][]) => pts.map(([x, y]) => [x - 210, y + 8] as [number, number]);
  const cont = view([[220, 0], ...corner, [520, 200]]);
  const arc: [number, number][] = [[220, 0]];
  for (let i = 0; i <= 64; i++) {
    const a = -Math.PI / 2 + (Math.PI / 2) * (i / 64);
    arc.push([size - r + r * Math.cos(a), r + r * Math.sin(a)]);
  }
  arc.push([520, 200]);
  const circle = view(arc);
  return (
    <svg viewBox="0 0 320 200" className="diagram" role="img" aria-label="A continuous corner meets the straight edges more gradually than a circular arc of the same radius">
      <defs>
        <linearGradient id="dg-spectrum" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#12b5cb" />
          <stop offset=".5" stopColor="#6a5cff" />
          <stop offset="1" stopColor="#ff4f9a" />
        </linearGradient>
      </defs>
      <g className="diagram__grid">
        {Array.from({ length: 13 }, (_, i) => (
          <line key={`v${i}`} x1={10 + i * 25} y1={8} x2={10 + i * 25} y2={200} />
        ))}
        {Array.from({ length: 8 }, (_, i) => (
          <line key={`h${i}`} x1={10} y1={8 + i * 25} x2={310} y2={8 + i * 25} />
        ))}
      </g>
      <path d={pathFrom(circle)} className="diagram__ghost" />
      <path d={pathFrom(cont)} className="diagram__line" stroke="url(#dg-spectrum)" />
      <circle cx={size - r - 210} cy={r + 8} r={2.5} className="diagram__dot" />
      <text x={22} y={186} className="diagram__label">
        n = 3.26 · extent 1.528 r
      </text>
    </svg>
  );
}

/** The bevel's displacement weight across the 20-unit band inside the outline. */
export function BevelDiagram() {
  const w = 320;
  const h = 200;
  const pad = { l: 34, r: 14, t: 18, b: 34 };
  const iw = w - pad.l - pad.r;
  const ih = h - pad.t - pad.b;
  const maxD = 24;
  const pts: [number, number][] = [];
  for (let i = 0; i <= 120; i++) {
    const d = (i / 120) * maxD;
    pts.push([pad.l + (d / maxD) * iw, pad.t + (1 - edgeProfile(-d)) * ih]);
  }
  const area = `${pathFrom(pts)}L${f(pad.l + iw)} ${f(pad.t + ih)}L${f(pad.l)} ${f(pad.t + ih)}Z`;
  const bevelX = pad.l + (20 / maxD) * iw;
  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="diagram" role="img" aria-label="Displacement weight falls from 1 at the outline to 0 twenty units inside">
      <defs>
        <linearGradient id="dg-bevel" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#6a5cff" stopOpacity=".35" />
          <stop offset="1" stopColor="#6a5cff" stopOpacity="0" />
        </linearGradient>
      </defs>
      <line x1={pad.l} y1={pad.t + ih} x2={pad.l + iw} y2={pad.t + ih} className="diagram__axis" />
      <line x1={pad.l} y1={pad.t} x2={pad.l} y2={pad.t + ih} className="diagram__axis" />
      <line x1={bevelX} y1={pad.t} x2={bevelX} y2={pad.t + ih} className="diagram__ghost" />
      <path d={area} fill="url(#dg-bevel)" />
      <path d={pathFrom(pts)} className="diagram__line diagram__line--accent" />
      <text x={pad.l - 8} y={pad.t + 4} textAnchor="end" className="diagram__tick">1</text>
      <text x={pad.l - 8} y={pad.t + ih + 4} textAnchor="end" className="diagram__tick">0</text>
      <text x={pad.l} y={h - 12} className="diagram__tick">outline</text>
      <text x={bevelX} y={h - 12} textAnchor="middle" className="diagram__tick">20</text>
      <text x={pad.l + iw} y={h - 12} textAnchor="end" className="diagram__tick">depth</text>
      <text x={pad.l + 14} y={pad.t + 12} className="diagram__label">1 − √((2 − t) t)</text>
    </svg>
  );
}

/** Where each glass pixel samples its backdrop: arrows point into the glass. */
export function RefractionDiagram() {
  const w = 320;
  const h = 200;
  const sw = 280;
  const sh = 150;
  const { extent, exponent } = shapeParams(sw, sh, 44);
  const outline = shapeOutline(sw, sh, 44, 32).map(([x, y]) => [x + (w - sw) / 2, y + (h - sh) / 2] as [number, number]);
  const arrows: { x: number; y: number; dx: number; dy: number; e: number }[] = [];
  const step = 14;
  for (let y = -sh / 2 + 4; y <= sh / 2 - 4; y += step) {
    for (let x = -sw / 2 + 4; x <= sw / 2 - 4; x += step) {
      const fld = shapeField(x, y, sw, sh, extent, exponent);
      if (fld.distance > -1 || fld.distance < -20) continue;
      const e = edgeProfile(fld.distance);
      if (e < 0.04) continue;
      arrows.push({ x: x + w / 2, y: y + h / 2, dx: -fld.nx * e * 60 * 0.18, dy: -fld.ny * e * 60 * 0.18, e });
    }
  }
  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="diagram" role="img" aria-label="Near the rim, glass shows the backdrop from deeper inside the shape">
      <defs>
        <marker id="dg-arrow" viewBox="0 0 6 6" refX="5" refY="3" markerWidth="4" markerHeight="4" orient="auto-start-reverse">
          <path d="M0 0L6 3L0 6Z" className="diagram__arrowhead" />
        </marker>
      </defs>
      <path d={pathFrom(outline, true)} className="diagram__shape" />
      {arrows.map((a, i) => (
        <line
          key={i}
          x1={f(a.x)}
          y1={f(a.y)}
          x2={f(a.x + a.dx)}
          y2={f(a.y + a.dy)}
          className="diagram__vector"
          style={{ opacity: 0.35 + 0.65 * a.e }}
          markerEnd="url(#dg-arrow)"
        />
      ))}
      <text x={w / 2} y={h / 2 + 4} textAnchor="middle" className="diagram__label">
        offset = −n · edge · 60
      </text>
    </svg>
  );
}

/** The rim overlay sampled around a capsule: specular top and bottom, shaded sides. */
export function RimDiagram() {
  const w = 320;
  const h = 200;
  const sw = 260;
  const sh = 120;
  const { extent, exponent } = shapeParams(sw, sh, "capsule");
  const pts = shapeOutline(sw, sh, "capsule", 48);
  const segs: { x1: number; y1: number; x2: number; y2: number; light: number; shade: number }[] = [];
  for (let i = 0; i < pts.length; i++) {
    const a = pts[i];
    const b = pts[(i + 1) % pts.length];
    if (!a || !b) continue;
    const mx = (a[0] + b[0]) / 2 - sw / 2;
    const my = (a[1] + b[1]) / 2 - sh / 2;
    const fld = shapeField(mx, my, sw, sh, extent, exponent);
    const lightRim = rimOverlay(-0.4, fld.nx, fld.ny, false, 1 / 2);
    const shadeRim = rimOverlay(0.5, fld.nx, fld.ny, false, 1 / 2);
    segs.push({
      x1: a[0] + (w - sw) / 2,
      y1: a[1] + (h - sh) / 2,
      x2: b[0] + (w - sw) / 2,
      y2: b[1] + (h - sh) / 2,
      light: lightRim[0],
      shade: Math.max(0, shadeRim[3] - shadeRim[0]),
    });
  }
  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="diagram diagram--rim" role="img" aria-label="A bright specular line along the top and bottom of the outline, darker shading on the left and right">
      <defs>
        <linearGradient id="dg-rim-bg" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#3a3f8f" />
          <stop offset=".55" stopColor="#7a4fd0" />
          <stop offset="1" stopColor="#d6589f" />
        </linearGradient>
        <filter id="dg-glow" x="-10%" y="-10%" width="120%" height="120%">
          <feGaussianBlur stdDeviation="2.2" />
        </filter>
      </defs>
      <rect x="0" y="0" width={w} height={h} rx="18" fill="url(#dg-rim-bg)" />
      <rect x={(w - sw) / 2} y={(h - sh) / 2} width={sw} height={sh} rx={sh / 2} fill="rgba(255,255,255,0.14)" />
      {segs.map((s, i) => (
        <line key={`s${i}`} x1={f(s.x1)} y1={f(s.y1)} x2={f(s.x2)} y2={f(s.y2)} stroke="#000" strokeOpacity={f(s.shade * 0.9)} strokeWidth="5" strokeLinecap="round" filter="url(#dg-glow)" />
      ))}
      {segs.map((s, i) => (
        <line key={`l${i}`} x1={f(s.x1)} y1={f(s.y1)} x2={f(s.x2)} y2={f(s.y2)} stroke="#fff" strokeOpacity={f(Math.min(1, s.light * 1.1))} strokeWidth="2.2" strokeLinecap="round" />
      ))}
    </svg>
  );
}

/** Material tone curves: output channel for each input channel value. */
export function ToneDiagram() {
  const w = 320;
  const h = 200;
  const pad = { l: 30, r: 12, t: 14, b: 28 };
  const iw = w - pad.l - pad.r;
  const ih = h - pad.t - pad.b;
  const curve = (fn: (v: number) => number) => {
    const pts: [number, number][] = [];
    for (let i = 0; i <= 64; i++) {
      const v = i / 64;
      pts.push([pad.l + v * iw, pad.t + (1 - fn(v)) * ih]);
    }
    return pathFrom(pts);
  };
  const lines = [
    { key: "cl", d: curve((v) => clearTone(v, false)), cls: "diagram__line--c1", label: "Clear · light" },
    { key: "cd", d: curve((v) => clearTone(v, true)), cls: "diagram__line--c2", label: "Clear · dark" },
    { key: "rl", d: curve((v) => regularTone(v, false)), cls: "diagram__line--c3", label: "Regular · light" },
    { key: "rd", d: curve((v) => regularTone(v, true)), cls: "diagram__line--c4", label: "Regular · dark" },
  ];
  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="diagram" role="img" aria-label="Tone curves of the Clear and Regular materials in light and dark appearance">
      <line x1={pad.l} y1={pad.t + ih} x2={pad.l + iw} y2={pad.t} className="diagram__ghost" />
      <line x1={pad.l} y1={pad.t + ih} x2={pad.l + iw} y2={pad.t + ih} className="diagram__axis" />
      <line x1={pad.l} y1={pad.t} x2={pad.l} y2={pad.t + ih} className="diagram__axis" />
      {lines.map((l) => (
        <path key={l.key} d={l.d} className={`diagram__line ${l.cls}`} />
      ))}
      {lines.map((l, i) => (
        <g key={`k${l.key}`} transform={`translate(${pad.l + iw - 104}, ${pad.t + ih - 70 + i * 16})`}>
          <line x1="0" y1="-4" x2="14" y2="-4" className={`diagram__line ${l.cls}`} />
          <text x="20" y="0" className="diagram__tick">
            {l.label}
          </text>
        </g>
      ))}
      <text x={pad.l - 8} y={pad.t + 4} textAnchor="end" className="diagram__tick">1</text>
      <text x={pad.l - 8} y={pad.t + ih + 4} textAnchor="end" className="diagram__tick">0</text>
      <text x={pad.l + iw} y={h - 8} textAnchor="end" className="diagram__tick">backdrop →</text>
    </svg>
  );
}

function springCurve(options: SpringOptions, from: number, to: number, seconds: number) {
  const out: [number, number][] = [];
  let s = { value: from, velocity: 0, target: to };
  const dt = 1 / 240;
  for (let t = 0; t <= seconds + 1e-9; t += dt) {
    out.push([t, s.value]);
    s = springStep(s, options, dt);
  }
  return out;
}

/** Press, release and glow springs, solved in closed form. */
export function SpringDiagram() {
  const w = 320;
  const h = 200;
  const pad = { l: 30, r: 12, t: 22, b: 28 };
  const iw = w - pad.l - pad.r;
  const ih = h - pad.t - pad.b;
  const T = 0.9;
  const lo = -0.25;
  const hi = 1.3;
  const map = (pts: [number, number][]) => pathFrom(pts.map(([t, v]) => [pad.l + (t / T) * iw, pad.t + ((hi - v) / (hi - lo)) * ih]));
  const zero = pad.t + ((hi - 0) / (hi - lo)) * ih;
  const one = pad.t + ((hi - 1) / (hi - lo)) * ih;
  const series = [
    { key: "press", d: map(springCurve(springs.press, 0, 1, T)), cls: "diagram__line--c1", label: "press 0.24 s · 0.20" },
    { key: "release", d: map(springCurve(springs.release, 1, 0, T)), cls: "diagram__line--c4", label: "release 0.50 s · 0.42" },
    { key: "glow", d: map(springCurve(springs.glow, 0, 1, T)), cls: "diagram__line--c3", label: "glow 0.42 s · 0" },
  ];
  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="diagram" role="img" aria-label="Spring responses for press, release and glow over 0.9 seconds">
      <line x1={pad.l} y1={zero} x2={pad.l + iw} y2={zero} className="diagram__axis" />
      <line x1={pad.l} y1={one} x2={pad.l + iw} y2={one} className="diagram__ghost" />
      <line x1={pad.l} y1={pad.t} x2={pad.l} y2={pad.t + ih} className="diagram__axis" />
      {series.map((s) => (
        <path key={s.key} d={s.d} className={`diagram__line ${s.cls}`} />
      ))}
      {series.map((s, i) => (
        <g key={`k${s.key}`} transform={`translate(${pad.l + iw - 128}, ${pad.t + 26 + i * 16})`}>
          <line x1="0" y1="-4" x2="14" y2="-4" className={`diagram__line ${s.cls}`} />
          <text x="20" y="0" className="diagram__tick">
            {s.label}
          </text>
        </g>
      ))}
      <text x={pad.l - 8} y={one + 4} textAnchor="end" className="diagram__tick">1</text>
      <text x={pad.l - 8} y={zero + 4} textAnchor="end" className="diagram__tick">0</text>
      <text x={pad.l + iw} y={h - 8} textAnchor="end" className="diagram__tick">{T} s</text>
    </svg>
  );
}
