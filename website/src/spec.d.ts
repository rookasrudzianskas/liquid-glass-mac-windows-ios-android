// Types for the canonical optical reference in ../spec, imported through the
// `@lg/*` aliases in vite.config.ts. The site uses the spec files directly so
// the website renders exactly the model every platform port is tested against.

declare module "@lg/reference" {
  export type Radius = number | "capsule" | "circle";
  export interface SpringState {
    value: number;
    velocity: number;
    target: number;
  }
  export interface SpringOptions {
    duration: number;
    bounce: number;
  }

  export const BEVEL: number;
  export const DEFAULT_REFRACTION: number;
  export const CONTINUOUS_EXTENT: number;
  export const CONTINUOUS_EXPONENT: number;
  export const PROGRESSIVE_LEVELS: number;

  export function clamp(x: number, lo: number, hi: number): number;
  export function sat(x: number): number;
  export function mix(a: number, b: number, t: number): number;
  export function smoothstep(e0: number, e1: number, x: number): number;

  export const materials: {
    clear: {
      blur: number;
      light: { shadowLift: number; highlightRolloff: number };
      dark: { shadowLift: number; highlightRolloff: number };
      tintBody: number;
    };
    regular: {
      blur: number;
      light: { fillSigma: number; fillOpacity: number; tone: [number, number, number]; chroma: number; toneWeight: number };
      dark: { fillSigma: number; fillOpacity: number; tone: [number, number, number]; chroma: number; toneWeight: number };
      tintBody: number;
    };
  };

  export function shapeParams(width: number, height: number, radius: Radius): { extent: number; exponent: number };
  export function shapeField(
    x: number,
    y: number,
    width: number,
    height: number,
    extent: number,
    exponent: number,
  ): { distance: number; nx: number; ny: number };
  export function shapeOutline(width: number, height: number, radius: Radius, segments?: number): [number, number][];
  export function edgeProfile(distance: number, bevel?: number): number;
  export function refractionOffset(distance: number, nx: number, ny: number, refraction?: number): { dx: number; dy: number };
  export function coverage(distance: number, pixel: number): number;
  export function luma(r: number, g: number, b: number): number;
  export function saturate(rgb: [number, number, number], s: number): [number, number, number];
  export function clearTone(v: number, dark: boolean, nested?: boolean): number;
  export function regularTone(v: number, dark: boolean): number;
  export function materialColor(rgb: [number, number, number], material: "clear" | "regular", dark: boolean, nested?: boolean): [number, number, number];
  export function rimOverlay(
    distance: number,
    nx: number,
    ny: number,
    dark: boolean,
    pixel: number,
    tint?: [number, number, number] | null,
  ): [number, number, number, number];

  export const springs: {
    press: SpringOptions;
    release: SpringOptions;
    glow: SpringOptions;
    morph: SpringOptions;
    layout: SpringOptions;
    track: SpringOptions;
  };
  export function springCoefficients(options: SpringOptions): { stiffness: number; damping: number };
  export function springStep(state: SpringState, options: SpringOptions, dt: number): SpringState;
  export function rubberBand(offset: number, limit: number, coefficient?: number): number;
  export function stretchLimit(width: number, height: number): number;
  export function pressScale(
    width: number,
    height: number,
    press: number,
    tx?: number,
    ty?: number,
    vx?: number,
    vy?: number,
    reduced?: boolean,
  ): { sx: number; sy: number };
  export function glow(width: number, height: number, dark: boolean): { radius: number; alpha: number; hover: number };
  export function progressiveLevelBlur(level: number, blur: number): number;
  export function progressiveWeight(level: number, u: number): number;
}

declare module "@lg/shader?raw" {
  const source: string;
  export default source;
}
