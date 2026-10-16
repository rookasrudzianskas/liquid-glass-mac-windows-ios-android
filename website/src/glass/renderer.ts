// The WebGL2 pipeline behind every live glass stage on the site.
//
// Per backdrop change (image load, resize, cross-fade):
//   backdrop -> scene (device pixels)
//   scene -> half (CSS/2) -> Gaussian sigma 7.33 -> frost            (Regular)
//   frost -> quarter (CSS/4) -> Gaussian sqrt(sigma_fill^2 - 7.33^2) -> fill
// Per frame (only when something moved):
//   scene -> screen, then one quad per surface with premultiplied blending.
// Surfaces marked `nested` (the tab bar's selection lens) sample the
// composited result, so glass inside glass refracts the outer glass.

import { materials } from "@lg/reference";
import {
  BACKDROP_FS,
  BLUR_FS,
  CONTENT_FS,
  COPY_FS,
  DOWNSAMPLE_FS,
  FULLSCREEN_VS,
  GLASS_FS,
  GLASS_VS,
  MAX_BLUR_TAPS,
} from "./shaders";

export type Material = "clear" | "regular";

export interface SurfaceFrame {
  /** Center and size in CSS pixels, relative to the canvas, y down. */
  x: number;
  y: number;
  width: number;
  height: number;
  /** Corner radius in CSS pixels, or -1 for a capsule. */
  radius: number;
  refraction: number;
  material: Material;
  dark: boolean;
  nested: boolean;
  tint: readonly [number, number, number] | null;
  opacity: number;
  /** Glow center relative to the surface center, and its spring value. */
  glowX: number;
  glowY: number;
  glow: number;
  glowRadius: number;
  contrast: boolean;
}

/** A solid shape painted into the backdrop (CSS pixels, center-based). */
export interface ContentRect {
  x: number;
  y: number;
  width: number;
  height: number;
  /** Corner radius in CSS pixels, or -1 for a capsule. */
  radius: number;
  /** sRGB color with alpha, unpremultiplied. */
  color: readonly [number, number, number, number];
}

export interface BackdropImage {
  source: TexImageSource;
  width: number;
  height: number;
  /** Focal point in image space (0..1) kept visible when cover-fitting. */
  focusX?: number;
  focusY?: number;
}

interface Target {
  fbo: WebGLFramebuffer;
  tex: WebGLTexture;
  width: number;
  height: number;
}

type Program = { program: WebGLProgram; uniforms: Map<string, WebGLUniformLocation | null> };

const FROST_SIGMA = materials.regular.blur;

function gaussianTaps(sigma: number) {
  const radius = Math.max(1, Math.ceil(sigma * 3));
  const raw: number[] = [];
  for (let i = 0; i <= radius; i++) raw.push(Math.exp(-(i * i) / (2 * sigma * sigma)));
  const total = raw.reduce((sum, w, i) => sum + (i === 0 ? w : 2 * w), 0);
  const w = raw.map((v) => v / total);
  const weights: number[] = [];
  const offsets: number[] = [];
  for (let i = 1; i <= radius; i += 2) {
    const a = w[i] ?? 0;
    const b = w[i + 1] ?? 0;
    const sum = a + b;
    weights.push(sum);
    offsets.push(sum > 0 ? (i * a + (i + 1) * b) / sum : i);
  }
  if (weights.length > MAX_BLUR_TAPS) throw new Error("blur kernel too wide");
  return { center: w[0] ?? 1, weights, offsets };
}

export class GlassRenderer {
  readonly canvas: HTMLCanvasElement;
  private gl: WebGL2RenderingContext;
  private programs!: {
    backdrop: Program;
    down: Program;
    blur: Program;
    copy: Program;
    glass: Program;
    content: Program;
  };
  private triangle!: WebGLVertexArrayObject;
  private quad!: WebGLVertexArrayObject;
  private scene: Target | null = null;
  private composite: Target | null = null;
  private halfA: Target | null = null;
  private halfB: Target | null = null;
  private quarterA: Target | null = null;
  private quarterB: Target | null = null;
  private floatFormat: { internal: number; type: number } | null = null;
  private images: { a: WebGLTexture | null; b: WebGLTexture | null } = { a: null, b: null };
  private imageInfo: { a: BackdropImage | null; b: BackdropImage | null } = { a: null, b: null };
  private crossfade = 0;
  private cssWidth = 1;
  private cssHeight = 1;
  private backdropDirty = true;
  private placeholder!: WebGLTexture;
  lost = false;

  constructor(canvas: HTMLCanvasElement) {
    this.canvas = canvas;
    const gl = canvas.getContext("webgl2", {
      alpha: false,
      antialias: false,
      depth: false,
      stencil: false,
      premultipliedAlpha: true,
      preserveDrawingBuffer: false,
      powerPreference: "default",
      // Software rasterizers (no GPU) get the CSS fallback instead: smoother than slow WebGL.
      failIfMajorPerformanceCaveat: true,
    });
    if (!gl) throw new Error("WebGL2 unavailable");
    const info = gl.getExtension("WEBGL_debug_renderer_info");
    const name = String(gl.getParameter(info ? info.UNMASKED_RENDERER_WEBGL : gl.RENDERER) ?? "");
    if (/swiftshader|llvmpipe|softpipe|software|basic render/i.test(name)) {
      gl.getExtension("WEBGL_lose_context")?.loseContext();
      throw new Error(`software renderer: ${name}`);
    }
    this.gl = gl;
    this.init();
  }

  /** (Re)create every GPU resource. Called at start and after context restore. */
  init() {
    const gl = this.gl;
    const colorFloat = gl.getExtension("EXT_color_buffer_float");
    const colorHalf = colorFloat ? null : gl.getExtension("EXT_color_buffer_half_float");
    this.floatFormat = colorFloat || colorHalf ? { internal: gl.RGBA16F, type: gl.HALF_FLOAT } : null;

    this.programs = {
      backdrop: this.program(FULLSCREEN_VS, BACKDROP_FS),
      down: this.program(FULLSCREEN_VS, DOWNSAMPLE_FS),
      blur: this.program(FULLSCREEN_VS, BLUR_FS),
      copy: this.program(FULLSCREEN_VS, COPY_FS),
      glass: this.program(GLASS_VS, GLASS_FS),
      content: this.program(GLASS_VS, CONTENT_FS),
    };
    this.triangle = this.vao(new Float32Array([-1, -1, 3, -1, -1, 3]));
    this.quad = this.vao(new Float32Array([0, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 1]));

    this.placeholder = this.texture();
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array([40, 44, 60, 255]));

    this.images = { a: null, b: null };
    const { a, b } = this.imageInfo;
    this.imageInfo = { a: null, b: null };
    if (a) this.setImage(a, "a");
    if (b) this.setImage(b, "b");
    this.scene = this.composite = this.halfA = this.halfB = this.quarterA = this.quarterB = null;
    this.backdropDirty = true;
    this.lost = false;
  }

  private compile(type: number, source: string) {
    const gl = this.gl;
    const shader = gl.createShader(type);
    if (!shader) throw new Error("createShader failed");
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS) && !gl.isContextLost()) {
      const log = gl.getShaderInfoLog(shader);
      gl.deleteShader(shader);
      throw new Error(`shader compile failed: ${log ?? ""}`);
    }
    return shader;
  }

  private program(vs: string, fs: string): Program {
    const gl = this.gl;
    const program = gl.createProgram();
    if (!program) throw new Error("createProgram failed");
    gl.attachShader(program, this.compile(gl.VERTEX_SHADER, vs));
    gl.attachShader(program, this.compile(gl.FRAGMENT_SHADER, fs));
    gl.bindAttribLocation(program, 0, "aPos");
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS) && !gl.isContextLost()) {
      throw new Error(`program link failed: ${gl.getProgramInfoLog(program) ?? ""}`);
    }
    return { program, uniforms: new Map() };
  }

  private vao(data: Float32Array) {
    const gl = this.gl;
    const vao = gl.createVertexArray();
    if (!vao) throw new Error("createVertexArray failed");
    gl.bindVertexArray(vao);
    const buffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, data, gl.STATIC_DRAW);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    gl.bindVertexArray(null);
    return vao;
  }

  private texture() {
    const gl = this.gl;
    const tex = gl.createTexture();
    if (!tex) throw new Error("createTexture failed");
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return tex;
  }

  private target(width: number, height: number, float: boolean, existing: Target | null): Target {
    const gl = this.gl;
    if (existing && existing.width === width && existing.height === height) return existing;
    if (existing) {
      gl.deleteFramebuffer(existing.fbo);
      gl.deleteTexture(existing.tex);
    }
    const tex = this.texture();
    const fmt = float ? this.floatFormat : null;
    if (fmt) gl.texImage2D(gl.TEXTURE_2D, 0, fmt.internal, width, height, 0, gl.RGBA, fmt.type, null);
    else gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
    const fbo = gl.createFramebuffer();
    if (!fbo) throw new Error("createFramebuffer failed");
    gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0);
    if (fmt && gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) {
      // Some drivers advertise the extension but refuse the attachment.
      this.floatFormat = null;
      gl.bindFramebuffer(gl.FRAMEBUFFER, null);
      gl.deleteFramebuffer(fbo);
      gl.deleteTexture(tex);
      return this.target(width, height, false, null);
    }
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    return { fbo, tex, width, height };
  }

  private uniform(p: Program, name: string) {
    let loc = p.uniforms.get(name);
    if (loc === undefined) {
      loc = this.gl.getUniformLocation(p.program, name);
      p.uniforms.set(name, loc);
    }
    return loc;
  }

  /** Resize to the canvas's CSS size and device pixel ratio. */
  resize(cssWidth: number, cssHeight: number, deviceWidth: number, deviceHeight: number) {
    this.cssWidth = Math.max(1, cssWidth);
    this.cssHeight = Math.max(1, cssHeight);
    const w = Math.max(1, Math.round(deviceWidth));
    const h = Math.max(1, Math.round(deviceHeight));
    if (this.canvas.width !== w) this.canvas.width = w;
    if (this.canvas.height !== h) this.canvas.height = h;
    this.backdropDirty = true;
  }

  get pixel() {
    return this.cssWidth / Math.max(1, this.canvas.width);
  }

  /** Upload a backdrop image into slot a (current) or b (cross-fade target). */
  setImage(image: BackdropImage, slot: "a" | "b" = "a") {
    const gl = this.gl;
    const tex = this.images[slot] ?? this.texture();
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, image.source);
    const isCanvas = typeof HTMLCanvasElement !== "undefined" && image.source instanceof HTMLCanvasElement;
    if (!isCanvas) {
      gl.generateMipmap(gl.TEXTURE_2D);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
    } else {
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    }
    this.images[slot] = tex;
    this.imageInfo[slot] = image;
    this.backdropDirty = true;
  }

  /** Cross-fade amount from image a (0) to image b (1). */
  setCrossfade(t: number) {
    if (t !== this.crossfade) {
      this.crossfade = t;
      this.backdropDirty = true;
    }
  }

  /** Make b the current image (after a cross-fade completes). */
  commitCrossfade() {
    const { a, b } = this.images;
    this.images = { a: b, b: a };
    this.imageInfo = { a: this.imageInfo.b, b: this.imageInfo.a };
    this.crossfade = 0;
    this.backdropDirty = true;
  }

  invalidateBackdrop() {
    this.backdropDirty = true;
  }

  private content: ContentRect[] = [];

  /** Shapes painted over the backdrop image; changing them re-blurs the frost. */
  setContent(rects: ContentRect[]) {
    const a = this.content;
    const same =
      a.length === rects.length &&
      a.every((r, i) => {
        const b = rects[i];
        return (
          b &&
          Math.abs(r.x - b.x) < 0.01 &&
          Math.abs(r.y - b.y) < 0.01 &&
          Math.abs(r.width - b.width) < 0.01 &&
          Math.abs(r.height - b.height) < 0.01 &&
          r.radius === b.radius &&
          r.color.every((c, j) => Math.abs(c - (b.color[j] ?? 0)) < 0.002)
        );
      });
    if (!same) {
      this.content = rects;
      this.backdropDirty = true;
    }
  }

  private drawContent() {
    if (!this.content.length) return;
    const gl = this.gl;
    const p = this.programs.content;
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
    gl.useProgram(p.program);
    gl.uniform2f(this.uniform(p, "uViewport"), this.cssWidth, this.cssHeight);
    gl.uniform1f(this.uniform(p, "uPixel"), this.pixel);
    gl.bindVertexArray(this.quad);
    for (const r of this.content) {
      const pad = 2;
      gl.uniform4f(this.uniform(p, "uRect"), r.x - r.width / 2 - pad, r.y - r.height / 2 - pad, r.width + 2 * pad, r.height + 2 * pad);
      gl.uniform2f(this.uniform(p, "uCenter"), r.x, r.y);
      gl.uniform2f(this.uniform(p, "uSize"), r.width, r.height);
      gl.uniform1f(this.uniform(p, "uRadius"), r.radius);
      gl.uniform4f(this.uniform(p, "uColor"), r.color[0], r.color[1], r.color[2], r.color[3]);
      gl.drawArrays(gl.TRIANGLES, 0, 6);
    }
    gl.disable(gl.BLEND);
  }

  private fit(info: BackdropImage | null) {
    if (!info) return [0, 0, this.cssWidth, this.cssHeight] as const;
    const scale = Math.max(this.cssWidth / info.width, this.cssHeight / info.height);
    const w = info.width * scale;
    const h = info.height * scale;
    const fx = info.focusX ?? 0.5;
    const fy = info.focusY ?? 0.5;
    const x = Math.min(0, Math.max(this.cssWidth - w, this.cssWidth / 2 - fx * w));
    const y = Math.min(0, Math.max(this.cssHeight - h, this.cssHeight / 2 - fy * h));
    return [x, y, w, h] as const;
  }

  private bindTarget(target: Target | null) {
    const gl = this.gl;
    gl.bindFramebuffer(gl.FRAMEBUFFER, target ? target.fbo : null);
    gl.viewport(0, 0, target ? target.width : this.canvas.width, target ? target.height : this.canvas.height);
  }

  private fullscreen(p: Program) {
    const gl = this.gl;
    gl.uniform2f(this.uniform(p, "uViewport"), this.cssWidth, this.cssHeight);
    gl.bindVertexArray(this.triangle);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
  }

  private bindTexture(unit: number, tex: WebGLTexture | null) {
    const gl = this.gl;
    gl.activeTexture(gl.TEXTURE0 + unit);
    gl.bindTexture(gl.TEXTURE_2D, tex);
  }

  private downsample(src: Target, dst: Target) {
    const gl = this.gl;
    const p = this.programs.down;
    this.bindTarget(dst);
    gl.useProgram(p.program);
    this.bindTexture(0, src.tex);
    gl.uniform1i(this.uniform(p, "uSrc"), 0);
    // Spread so the four bilinear taps cover the destination texel's footprint.
    const fx = src.width / dst.width;
    const fy = src.height / dst.height;
    gl.uniform2f(this.uniform(p, "uOffset"), fx / 4 / src.width, fy / 4 / src.height);
    this.fullscreen(p);
  }

  private blur(src: Target, tmp: Target, sigmaTexels: number) {
    const gl = this.gl;
    const p = this.programs.blur;
    const taps = gaussianTaps(sigmaTexels);
    gl.useProgram(p.program);
    gl.uniform1f(this.uniform(p, "uCenter"), taps.center);
    gl.uniform1fv(this.uniform(p, "uWeights"), new Float32Array(MAX_BLUR_TAPS).fill(0).map((_, i) => taps.weights[i] ?? 0));
    gl.uniform1fv(this.uniform(p, "uOffsets"), new Float32Array(MAX_BLUR_TAPS).fill(0).map((_, i) => taps.offsets[i] ?? 0));
    gl.uniform1i(this.uniform(p, "uCount"), taps.weights.length);
    gl.uniform1i(this.uniform(p, "uSrc"), 0);
    // Horizontal into tmp, vertical back into src.
    this.bindTarget(tmp);
    this.bindTexture(0, src.tex);
    gl.uniform2f(this.uniform(p, "uStep"), 1 / src.width, 0);
    this.fullscreen(p);
    this.bindTarget(src);
    this.bindTexture(0, tmp.tex);
    gl.uniform2f(this.uniform(p, "uStep"), 0, 1 / tmp.height);
    this.fullscreen(p);
  }

  private renderBackdrop(dark: boolean) {
    const gl = this.gl;
    const dw = this.canvas.width;
    const dh = this.canvas.height;
    this.scene = this.target(dw, dh, false, this.scene);
    const hw = Math.max(1, Math.round(this.cssWidth / 2));
    const hh = Math.max(1, Math.round(this.cssHeight / 2));
    const qw = Math.max(1, Math.round(this.cssWidth / 4));
    const qh = Math.max(1, Math.round(this.cssHeight / 4));
    this.halfA = this.target(hw, hh, true, this.halfA);
    this.halfB = this.target(hw, hh, true, this.halfB);
    this.quarterA = this.target(qw, qh, true, this.quarterA);
    this.quarterB = this.target(qw, qh, true, this.quarterB);

    const p = this.programs.backdrop;
    this.bindTarget(this.scene);
    gl.disable(gl.BLEND);
    gl.useProgram(p.program);
    this.bindTexture(0, this.images.a ?? this.placeholder);
    this.bindTexture(1, this.images.b ?? this.placeholder);
    gl.uniform1i(this.uniform(p, "uImageA"), 0);
    gl.uniform1i(this.uniform(p, "uImageB"), 1);
    gl.uniform4f(this.uniform(p, "uFitA"), ...this.fit(this.imageInfo.a));
    gl.uniform4f(this.uniform(p, "uFitB"), ...this.fit(this.imageInfo.b));
    gl.uniform1f(this.uniform(p, "uMix"), this.images.b ? this.crossfade : 0);
    this.fullscreen(p);
    this.drawContent();

    // Regular frost: sigma 7.33 at half CSS resolution...
    this.downsample(this.scene, this.halfA);
    this.blur(this.halfA, this.halfB, FROST_SIGMA * (hw / this.cssWidth));
    // ...and the wide fill blur, continued from the frost (Gaussians add in quadrature).
    const fillSigma = dark ? materials.regular.dark.fillSigma : materials.regular.light.fillSigma;
    this.downsample(this.halfA, this.quarterA);
    const extra = Math.sqrt(Math.max(0, fillSigma * fillSigma - FROST_SIGMA * FROST_SIGMA));
    this.blur(this.quarterA, this.quarterB, extra * (qw / this.cssWidth));
    this.backdropDirty = false;
    this.lastDark = dark;
  }

  private lastDark: boolean | null = null;

  private drawSurface(s: SurfaceFrame, sceneTex: WebGLTexture) {
    const gl = this.gl;
    const p = this.programs.glass;
    const pad = 3;
    gl.uniform4f(this.uniform(p, "uRect"), s.x - s.width / 2 - pad, s.y - s.height / 2 - pad, s.width + 2 * pad, s.height + 2 * pad);
    gl.uniform2f(this.uniform(p, "uCenter"), s.x, s.y);
    gl.uniform2f(this.uniform(p, "uSize"), s.width, s.height);
    gl.uniform1f(this.uniform(p, "uRadius"), s.radius);
    gl.uniform1f(this.uniform(p, "uRefraction"), s.refraction);
    gl.uniform1f(this.uniform(p, "uMaterial"), s.material === "regular" ? 1 : 0);
    gl.uniform1f(this.uniform(p, "uDark"), s.dark ? 1 : 0);
    gl.uniform1f(this.uniform(p, "uNested"), s.nested ? 1 : 0);
    gl.uniform3f(this.uniform(p, "uTint"), s.tint?.[0] ?? 0, s.tint?.[1] ?? 0, s.tint?.[2] ?? 0);
    gl.uniform1f(this.uniform(p, "uTintOn"), s.tint ? 1 : 0);
    gl.uniform1f(this.uniform(p, "uOpacity"), s.opacity);
    gl.uniform3f(this.uniform(p, "uGlow"), s.glowX, s.glowY, s.glow);
    gl.uniform1f(this.uniform(p, "uGlowRadius"), s.glowRadius);
    gl.uniform1f(this.uniform(p, "uContrast"), s.contrast ? 1 : 0);
    this.bindTexture(0, sceneTex);
    gl.drawArrays(gl.TRIANGLES, 0, 6);
  }

  /** Draw one frame. `dark` selects the appearance the fill blur is computed for. */
  render(surfaces: readonly SurfaceFrame[], dark: boolean) {
    const gl = this.gl;
    if (this.lost || gl.isContextLost()) return;
    if (this.backdropDirty || this.lastDark !== dark || !this.scene) this.renderBackdrop(dark);
    const scene = this.scene;
    const frost = this.halfA;
    const fill = this.quarterA;
    if (!scene || !frost || !fill) return;

    const visible = surfaces.filter(
      (s) =>
        s.opacity > 0.001 &&
        s.width > 0.5 &&
        s.height > 0.5 &&
        s.x + s.width / 2 > -4 &&
        s.y + s.height / 2 > -4 &&
        s.x - s.width / 2 < this.cssWidth + 4 &&
        s.y - s.height / 2 < this.cssHeight + 4,
    );
    const outer = visible.filter((s) => !s.nested);
    const inner = visible.filter((s) => s.nested);
    if (inner.length) this.composite = this.target(this.canvas.width, this.canvas.height, false, this.composite);

    const copy = (src: WebGLTexture, dst: Target | null) => {
      const p = this.programs.copy;
      this.bindTarget(dst);
      gl.disable(gl.BLEND);
      gl.useProgram(p.program);
      this.bindTexture(0, src);
      gl.uniform1i(this.uniform(p, "uSrc"), 0);
      this.fullscreen(p);
    };

    const pass = (list: SurfaceFrame[], sceneTex: WebGLTexture, dst: Target | null) => {
      if (!list.length) return;
      const p = this.programs.glass;
      this.bindTarget(dst);
      gl.enable(gl.BLEND);
      gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
      gl.useProgram(p.program);
      gl.uniform2f(this.uniform(p, "uViewport"), this.cssWidth, this.cssHeight);
      gl.uniform1f(this.uniform(p, "uPixel"), this.pixel);
      gl.uniform1i(this.uniform(p, "uScene"), 0);
      gl.uniform1i(this.uniform(p, "uFrost"), 1);
      gl.uniform1i(this.uniform(p, "uFill"), 2);
      this.bindTexture(1, frost.tex);
      this.bindTexture(2, fill.tex);
      gl.bindVertexArray(this.quad);
      for (const s of list) this.drawSurface(s, sceneTex);
      gl.disable(gl.BLEND);
    };

    if (inner.length && this.composite) {
      copy(scene.tex, this.composite);
      pass(outer, scene.tex, this.composite);
      copy(this.composite.tex, null);
      pass(inner, this.composite.tex, null);
    } else {
      copy(scene.tex, null);
      pass(outer, scene.tex, null);
    }
  }

  destroy() {
    const ext = this.gl.getExtension("WEBGL_lose_context");
    ext?.loseContext();
  }
}
