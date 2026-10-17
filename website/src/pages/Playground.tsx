import { useEffect, useId, useMemo, useRef, useState } from "react";
import type { CSSProperties, KeyboardEvent, ReactNode } from "react";
import { defaultRefraction } from "@lg/reference";
import { Glass, GlassStage, hexToRgb } from "../glass/react";
import { wallpaperInfo, wallpaperNames, wallpaperSrcSet } from "../glass/backdrop";
import type { BackdropSpec, WallpaperName } from "../glass/backdrop";
import type { Material } from "../glass/renderer";
import type { RendererKind } from "../glass/scene";
import { useTheme } from "../theme";
import type { Head } from "../router";
import site from "../generated/site.json";
import { highlight } from "../lib/highlight";
import type { Lang } from "../lib/highlight";
import { CopyButton } from "../components/Code";
import { ChevronDownIcon } from "../components/Icons";

type BackdropId = WallpaperName | "grid" | "type";
type AppearanceSetting = "automatic" | "light" | "dark";

interface Settings {
  backdrop: BackdropId;
  material: Material;
  appearance: AppearanceSetting;
  /** null is Auto: the glass sets none and follows its size (OPTICS.md section 2). */
  refraction: number | null;
  radius: number;
  capsule: boolean;
  width: number;
  height: number;
  tint: string | null;
  interactive: boolean;
}

const DEFAULTS: Settings = {
  backdrop: (wallpaperNames.includes("aurora" as WallpaperName) ? "aurora" : wallpaperNames[0]) as BackdropId,
  material: "regular",
  appearance: "automatic",
  refraction: null,
  radius: 32,
  capsule: false,
  width: 340,
  height: 220,
  tint: null,
  interactive: true,
};

const TINTS = [
  { name: "Blue", hex: "#3d8bff" },
  { name: "Violet", hex: "#8a5cff" },
  { name: "Pink", hex: "#ff4f9a" },
  { name: "Red", hex: "#ff5a4f" },
  { name: "Amber", hex: "#ffb02e" },
  { name: "Mint", hex: "#2fd4a3" },
];

// The first two sentences of NOTICE.md's statement: an independent port, not affiliated with Apple.
const NOTICE = site.notice.split(/(?<=\.)\s+(?=[A-Z"])/).slice(0, 2).join(" ");

const BACKDROPS: BackdropId[] = [...wallpaperNames, "grid", "type"];
const BACKDROP_LABELS: Record<string, string> = { dunes: "Dunes", aurora: "Aurora", ocean: "Ocean", midnight: "Midnight", grid: "Grid", type: "Type" };

const clampInt = (v: unknown, lo: number, hi: number, fallback: number) => {
  const n = Math.round(Number(v));
  return Number.isFinite(n) ? Math.min(hi, Math.max(lo, n)) : fallback;
};

function parse(search: string): Settings {
  const q = new URLSearchParams(search);
  const backdrops: string[] = [...wallpaperNames, "grid", "type"];
  const b = q.get("backdrop");
  const tint = q.get("tint");
  const refraction = q.get("refraction");
  return {
    backdrop: (b && backdrops.includes(b) ? b : DEFAULTS.backdrop) as BackdropId,
    material: q.get("material") === "clear" ? "clear" : q.get("material") === "regular" ? "regular" : DEFAULTS.material,
    appearance: (["automatic", "light", "dark"].includes(q.get("appearance") ?? "") ? q.get("appearance") : DEFAULTS.appearance) as AppearanceSetting,
    refraction: refraction === null || refraction === "auto" ? null : clampInt(refraction, 0, 120, 60),
    radius: clampInt(q.get("radius") ?? DEFAULTS.radius, 0, 120, DEFAULTS.radius),
    capsule: q.get("capsule") === "1",
    width: clampInt(q.get("width") ?? DEFAULTS.width, 60, 560, DEFAULTS.width),
    height: clampInt(q.get("height") ?? DEFAULTS.height, 44, 420, DEFAULTS.height),
    tint: tint && /^[0-9a-f]{6}$/i.test(tint) ? `#${tint.toLowerCase()}` : null,
    interactive: q.get("interactive") !== "0",
  };
}

function serialize(s: Settings) {
  const q = new URLSearchParams();
  (Object.keys(DEFAULTS) as (keyof Settings)[]).forEach((k) => {
    if (s[k] === DEFAULTS[k]) return;
    const v = s[k];
    if (k === "tint") q.set("tint", (v as string).slice(1));
    else if (typeof v === "boolean") q.set(k, v ? "1" : "0");
    else q.set(k, String(v));
  });
  const str = q.toString();
  return str ? `?${str}` : "";
}

// ---------------------------------------------------------------------------
// Code for each platform, reflecting the current settings. Auto refraction is
// left out: every port then follows the surface's size, as the glass here does.

const cap = (s: string) => s[0]?.toUpperCase() + s.slice(1);

function swiftColor(hex: string) {
  const [r, g, b] = hexToRgb(hex) ?? [0, 0, 0];
  return `Color(red: ${r.toFixed(3)}, green: ${g.toFixed(3)}, blue: ${b.toFixed(3)})`;
}

function swiftCode(s: Settings) {
  const args = [`material: .${s.material}`];
  if (s.appearance !== "automatic") args.push(`appearance: .${s.appearance}`);
  args.push(`radius: ${s.capsule ? ".capsule" : s.radius}`);
  if (s.refraction !== null) args.push(`refraction: ${s.refraction}`);
  if (s.tint) args.push(`tint: ${swiftColor(s.tint)}`);
  if (s.interactive) args.push("interactive: true");
  // GlassSurface takes the size of its content, so the frame goes on the content.
  return `GlassSurface(
    ${args.join(",\n    ")}
) {
    Text("Liquid Glass")
        .font(.headline)
        .frame(width: ${s.width}, height: ${s.height})
}`;
}

function composeCode(s: Settings) {
  const args = [`modifier = Modifier.size(${s.width}.dp, ${s.height}.dp)`, `shape = ${s.capsule ? "GlassShape.Capsule" : `GlassShape(${s.radius}.dp)`}`, `material = GlassMaterial.${cap(s.material)}`];
  if (s.appearance !== "automatic") args.push(`appearance = GlassAppearance.${cap(s.appearance)}`);
  if (s.tint) args.push(`tint = Color(0xFF${s.tint.slice(1).toUpperCase()})`);
  if (s.refraction !== null) args.push(`refraction = ${s.refraction}.dp`);
  if (s.interactive) args.push("interactive = true");
  args.push("contentAlignment = Alignment.Center");
  // GlassText takes the glass content color (Material's Text would not follow the appearance).
  return `GlassSurface(
    ${args.join(",\n    ")},
) {
    GlassText("Liquid Glass")
}`;
}

function wpfCode(s: Settings) {
  const attrs = [`Width="${s.width}"`, `Height="${s.height}"`, `Material="${cap(s.material)}"`];
  if (s.appearance !== "automatic") attrs.push(`Appearance="${cap(s.appearance)}"`);
  attrs.push(`Radius="${s.capsule ? "capsule" : s.radius}"`);
  if (s.refraction !== null) attrs.push(`Refraction="${s.refraction}"`);
  if (s.tint) attrs.push(`Tint="${s.tint.toUpperCase()}"`);
  if (s.interactive) attrs.push(`IsInteractive="True"`);
  return `<lg:GlassSurface ${attrs.join("\n                 ")}>
    <TextBlock Text="Liquid Glass" FontSize="17" FontWeight="SemiBold"
               HorizontalAlignment="Center" VerticalAlignment="Center" />
</lg:GlassSurface>`;
}

const CODE_TABS: { id: string; label: string; file: string; lang: Lang; make: (s: Settings) => string }[] = [
  { id: "swiftui", label: "SwiftUI", file: "ContentView.swift", lang: "swift", make: swiftCode },
  { id: "compose", label: "Compose", file: "Glass.kt", lang: "kotlin", make: composeCode },
  { id: "wpf", label: "WPF", file: "MainWindow.xaml", lang: "xml", make: wpfCode },
];

// ---------------------------------------------------------------------------
// Controls

/**
 * Radio groups use a roving tab stop: Tab reaches the checked option, and the
 * arrow keys, Home and End move the selection (and focus) inside the group.
 */
function onRadioKey<T>(e: KeyboardEvent<HTMLElement>, values: readonly T[], index: number, select: (v: T) => void) {
  const n = values.length;
  const to =
    e.key === "ArrowRight" || e.key === "ArrowDown"
      ? (index + 1) % n
      : e.key === "ArrowLeft" || e.key === "ArrowUp"
        ? (index - 1 + n) % n
        : e.key === "Home"
          ? 0
          : e.key === "End"
            ? n - 1
            : -1;
  if (to < 0) return;
  e.preventDefault();
  select(values[to] as T);
  e.currentTarget.closest('[role="radiogroup"]')?.querySelectorAll<HTMLElement>('[role="radio"]')[to]?.focus();
}

function Segmented<T extends string>({ label, value, options, onChange }: { label: string; value: T; options: { value: T; label: ReactNode }[]; onChange: (v: T) => void }) {
  const id = useId();
  return (
    <div className="control" role="radiogroup" aria-labelledby={id}>
      <span id={id} className="control__label">
        {label}
      </span>
      <div className="pg-segmented">
        {options.map((o, i) => (
          <button
            key={o.value}
            type="button"
            role="radio"
            aria-checked={value === o.value}
            tabIndex={value === o.value ? 0 : -1}
            className="pg-segmented__item"
            onClick={() => onChange(o.value)}
            onKeyDown={(e) =>
              onRadioKey(
                e,
                options.map((opt) => opt.value),
                i,
                onChange,
              )
            }
          >
            {o.label}
          </button>
        ))}
      </div>
    </div>
  );
}

function Slider({
  label,
  value,
  min,
  max,
  step = 1,
  unit = "",
  disabled,
  accessory,
  valueText,
  hint,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  step?: number;
  unit?: string;
  disabled?: boolean;
  /** Shown between the label and the value (the refraction's Auto toggle). */
  accessory?: ReactNode;
  valueText?: string;
  hint?: ReactNode;
  onChange: (v: number) => void;
}) {
  const id = useId();
  const pct = ((value - min) / (max - min)) * 100;
  return (
    <div className="control" data-disabled={disabled ? "" : undefined}>
      <div className="control__row">
        <label htmlFor={id} className="control__label">
          {label}
        </label>
        {accessory}
        <output htmlFor={id} className="control__value">
          {value}
          {unit}
        </output>
      </div>
      <input
        id={id}
        type="range"
        className="pg-range"
        min={min}
        max={max}
        step={step}
        value={value}
        disabled={disabled}
        aria-valuetext={valueText}
        aria-describedby={hint ? `${id}-hint` : undefined}
        style={{ "--pct": `${pct}%` } as CSSProperties}
        onChange={(e) => onChange(Number(e.target.value))}
      />
      {hint ? (
        <p id={`${id}-hint`} className="control__hint">
          {hint}
        </p>
      ) : null}
    </div>
  );
}

/**
 * Refraction is Auto until the slider moves: the glass then sets none and follows its
 * size, min(60, 0.75 × shorter side), and the slider shows that value. Moving the
 * slider sets an explicit value, used as given; Auto returns to the size rule.
 */
function RefractionControl({ value, auto, onChange }: { value: number | null; auto: number; onChange: (v: number | null) => void }) {
  const isAuto = value === null;
  const shown = value ?? auto;
  return (
    <Slider
      label="Refraction"
      value={shown}
      min={0}
      max={120}
      valueText={isAuto ? `${shown}, automatic` : undefined}
      accessory={
        <button type="button" className="pg-auto" aria-pressed={isAuto} aria-label="Auto refraction" onClick={() => onChange(isAuto ? Math.round(auto) : null)}>
          Auto
        </button>
      }
      hint={isAuto ? "Follows the size: min(60, 0.75 × shorter side)." : "Used as given. Auto follows the size."}
      onChange={onChange}
    />
  );
}

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  const id = useId();
  return (
    <div className="control control--inline">
      <span id={`${id}-label`} className="control__label">
        {label}
      </span>
      <button id={id} type="button" role="switch" aria-checked={checked} aria-labelledby={`${id}-label`} className="pg-switch" onClick={() => onChange(!checked)}>
        <span className="pg-switch__thumb" />
      </button>
    </div>
  );
}

function TintPicker({ value, onChange }: { value: string | null; onChange: (v: string | null) => void }) {
  const id = useId();
  const custom = value && !TINTS.some((t) => t.hex === value) ? value : null;
  const values: (string | null)[] = [null, ...TINTS.map((t) => t.hex)];
  // With a custom color nothing in the group is checked: the first option keeps the tab stop.
  const stop = Math.max(0, values.indexOf(value));
  const radio = (i: number) => ({
    type: "button" as const,
    role: "radio",
    "aria-checked": values[i] === value,
    tabIndex: i === stop ? 0 : -1,
    onClick: () => onChange(values[i] ?? null),
    onKeyDown: (e: KeyboardEvent<HTMLElement>) => onRadioKey(e, values, i, onChange),
  });
  return (
    <div className="control" role="radiogroup" aria-labelledby={id}>
      <span id={id} className="control__label">
        Tint
      </span>
      <div className="tints">
        <button {...radio(0)} className="tint tint--none" aria-label="No tint" title="No tint" />
        {TINTS.map((t, i) => (
          <button key={t.hex} {...radio(i + 1)} className="tint" style={{ background: t.hex }} aria-label={t.name} title={t.name} />
        ))}
        <label className="tint tint--custom" data-checked={custom ? "" : undefined} title="Custom color" style={custom ? { background: custom } : undefined}>
          <span className="sr-only">Custom tint color</span>
          <input type="color" value={value ?? "#3d8bff"} onChange={(e) => onChange(e.target.value.toLowerCase())} />
        </label>
      </div>
    </div>
  );
}

/**
 * What the stage shows before WebGL draws (and the starting point without it):
 * the chosen wallpaper as a <picture> the renderer reuses as its texture, or a
 * plain field in the colors of the drawn grid and type backdrops.
 */
function StageFallback({ backdrop }: { backdrop: BackdropId }) {
  if (backdrop === "grid" || backdrop === "type") return <div className="stage__fallback pg__pattern" aria-hidden="true" />;
  const info = wallpaperInfo[backdrop];
  return (
    <picture key={backdrop} className="stage__fallback" style={{ backgroundColor: info.color }}>
      <source type="image/avif" srcSet={wallpaperSrcSet(backdrop, "avif")} sizes="100vw" />
      <source type="image/webp" srcSet={wallpaperSrcSet(backdrop, "webp")} sizes="100vw" />
      <img src={`/wallpapers/${backdrop}-1536.jpg`} alt="" width={info.width} height={info.height} decoding="async" style={{ objectPosition: `${(info.focus[0] ?? 0.5) * 100}% ${(info.focus[1] ?? 0.5) * 100}%` }} />
    </picture>
  );
}

// ---------------------------------------------------------------------------

export default function Playground() {
  const { theme, mounted } = useTheme();
  const [s, setS] = useState<Settings>(DEFAULTS);
  const [ready, setReady] = useState(false);
  const [tab, setTab] = useState(CODE_TABS[0]?.id ?? "swiftui");
  const [codeOpen, setCodeOpen] = useState(true);
  const [panelOpen, setPanelOpen] = useState(true);
  const [renderer, setRenderer] = useState<{ kind: RendererKind; reason?: string }>({ kind: "pending" });
  // Reset remounts the glass, which drops where it was dragged.
  const [resets, setResets] = useState(0);
  const set = <K extends keyof Settings>(k: K, v: Settings[K]) => setS((prev) => ({ ...prev, [k]: v }));

  // Settings live in the URL so a configuration can be shared.
  useEffect(() => {
    setS(parse(window.location.search));
    setReady(true);
  }, []);
  useEffect(() => {
    if (!ready) return;
    const t = window.setTimeout(() => {
      const next = `${window.location.pathname}${serialize(s)}`;
      if (next !== `${window.location.pathname}${window.location.search}`) window.history.replaceState(window.history.state, "", next);
    }, 250);
    return () => window.clearTimeout(t);
  }, [s, ready]);

  const appearance = s.appearance === "automatic" ? theme : s.appearance;
  const backdrop: BackdropSpec | null = !mounted || !ready ? null : s.backdrop === "grid" || s.backdrop === "type" ? { kind: "canvas", pattern: s.backdrop } : { kind: "wallpaper", name: s.backdrop };
  const current = CODE_TABS.find((t) => t.id === tab) ?? CODE_TABS[0];
  const source = useMemo(() => (current ? current.make(s) : ""), [current, s]);
  const html = useMemo(() => (current ? highlight(source, current.lang) : ""), [source, current]);
  const tabsId = useId();
  const tabsRef = useRef<HTMLDivElement>(null);
  const rootRef = useRef<HTMLDivElement>(null);
  const codeRef = useRef<HTMLElement>(null);
  // The glass can be smaller than asked for (it never outgrows the free area): say the size it has.
  const glassRef = useRef<HTMLDivElement>(null);
  const [shown, setShown] = useState<{ w: number; h: number } | null>(null);
  useEffect(() => {
    const el = glassRef.current;
    if (!el || typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver(() => setShown({ w: Math.round(el.offsetWidth), h: Math.round(el.offsetHeight) }));
    observer.observe(el);
    return () => observer.disconnect();
  }, [resets]);
  // Auto refraction is measured on the glass as drawn, which is the size the renderer uses.
  const autoRefraction = defaultRefraction(shown?.w ?? s.width, shown?.h ?? s.height);
  const size = shown && (shown.w < s.width - 1 || shown.h < s.height - 1) ? `${shown.w} by ${shown.h} (fitted to the screen; the code keeps ${s.width} by ${s.height})` : `${s.width} by ${s.height}`;
  // The glass centres in the space the code panel leaves free.
  useEffect(() => {
    const root = rootRef.current;
    const code = codeRef.current;
    if (!root || !code) return;
    const observer = new ResizeObserver(() => root.style.setProperty("--pg-code-h", `${code.offsetHeight}px`));
    observer.observe(code);
    return () => observer.disconnect();
  }, []);
  const focusTab = (index: number) => {
    const next = CODE_TABS[(index + CODE_TABS.length) % CODE_TABS.length];
    if (!next) return;
    setTab(next.id);
    setCodeOpen(true);
    tabsRef.current?.querySelector<HTMLElement>(`#${CSS.escape(`${tabsId}-${next.id}`)}`)?.focus();
  };

  return (
    // The appearance is known only after mounting; until then CSS follows <html data-theme>.
    <div ref={rootRef} className="pg" data-appearance={mounted ? appearance : undefined} data-pattern={ready && (s.backdrop === "grid" || s.backdrop === "type") ? s.backdrop : undefined}>
      <h1 className="sr-only">Liquid Glass materials lab</h1>
      <GlassStage
        backdrop={backdrop}
        appearance={appearance}
        className="pg__stage"
        transition={450}
        fallback={ready ? <StageFallback backdrop={s.backdrop} /> : null}
        onRenderer={(kind, reason) => setRenderer({ kind, reason })}
      >
        <div className="pg__center">
          <Glass
            key={resets}
            ref={glassRef}
            className="pg__glass"
            material={s.material}
            radius={s.capsule ? "capsule" : s.radius}
            refraction={s.refraction ?? undefined}
            tint={s.tint}
            appearance={appearance}
            draggable
            bounds="parent"
            interactive={s.interactive}
            tabIndex={0}
            role="img"
            aria-label={`${s.material === "regular" ? "Regular" : "Clear"} glass, ${size}. Drag it with the pointer, or focus it and use the arrow keys.`}
            style={{ width: s.width, height: s.height }}
          >
            <span className="pg__glass-label" aria-hidden="true">
              {s.width >= 150 && s.height >= 70 ? "Liquid Glass" : ""}
            </span>
          </Glass>
        </div>
      </GlassStage>

      <aside className="pg-panel" data-open={panelOpen ? "" : undefined} aria-label="Glass settings">
        <button type="button" className="pg-panel__toggle" aria-expanded={panelOpen} onClick={() => setPanelOpen((o) => !o)}>
          <span>Settings</span>
          <ChevronDownIcon size={18} />
        </button>
        <div className="pg-panel__body">
          <div className="control" role="radiogroup" aria-label="Backdrop">
            <span className="control__label" aria-hidden="true">
              Backdrop
            </span>
            <div className="backdrops">
              {BACKDROPS.map((b, i) => (
                <button
                  key={b}
                  type="button"
                  role="radio"
                  aria-checked={s.backdrop === b}
                  tabIndex={s.backdrop === b ? 0 : -1}
                  onKeyDown={(e) => onRadioKey(e, BACKDROPS, i, (v) => set("backdrop", v))}
                  className="backdrop-thumb"
                  data-kind={b === "grid" || b === "type" ? b : "wallpaper"}
                  style={b === "grid" || b === "type" ? undefined : { backgroundImage: `url(/wallpapers/${b}-640.webp)`, backgroundColor: wallpaperInfo[b as WallpaperName].color }}
                  onClick={() => set("backdrop", b)}
                >
                  <span>{BACKDROP_LABELS[b] ?? b}</span>
                </button>
              ))}
            </div>
          </div>
          <Segmented
            label="Material"
            value={s.material}
            options={[
              { value: "clear", label: "Clear" },
              { value: "regular", label: "Regular" },
            ]}
            onChange={(v) => set("material", v)}
          />
          <Segmented
            label="Appearance"
            value={s.appearance}
            options={[
              { value: "automatic", label: "Auto" },
              { value: "light", label: "Light" },
              { value: "dark", label: "Dark" },
            ]}
            onChange={(v) => set("appearance", v)}
          />
          <RefractionControl value={s.refraction} auto={autoRefraction} onChange={(v) => set("refraction", v)} />
          <Slider label="Corner radius" value={s.radius} min={0} max={120} disabled={s.capsule} onChange={(v) => set("radius", v)} />
          <div className="control-pair">
            <Slider label="Width" value={s.width} min={60} max={560} onChange={(v) => set("width", v)} />
            <Slider label="Height" value={s.height} min={44} max={420} onChange={(v) => set("height", v)} />
          </div>
          <div className="control-pair">
            <Toggle label="Capsule" checked={s.capsule} onChange={(v) => set("capsule", v)} />
            <Toggle label="Interactive" checked={s.interactive} onChange={(v) => set("interactive", v)} />
          </div>
          <TintPicker value={s.tint} onChange={(v) => set("tint", v)} />
          {renderer.kind === "css" ? (
            <p className="pg-panel__note" role="status">
              {renderer.reason === "reduced transparency"
                ? "Reduce transparency is on, so the glass shows its opaque fallback material."
                : "Live refraction needs WebGL 2. Showing the fallback material."}
            </p>
          ) : null}
        </div>
        <div className="pg-panel__foot">
          <div className="pg-panel__actions">
            <button
              type="button"
              className="button button--secondary button--small"
              onClick={() => {
                setS(DEFAULTS);
                setResets((n) => n + 1);
              }}
            >
              Reset
            </button>
            <CopyButton text={typeof window === "undefined" ? "" : `${window.location.origin}/playground${serialize(s)}`} label="Copy link" />
          </div>
          <p className="pg-panel__credit">
            Optical model ported from{" "}
            <a href="https://github.com/Glass-HQ/liquid-glass" rel="noopener" target="_blank">
              Glass HQ liquid-glass
            </a>{" "}
            (MIT). {NOTICE}
          </p>
        </div>
      </aside>

      <section ref={codeRef} className="pg-code" data-open={codeOpen ? "" : undefined} aria-label="Code for the current settings">
        <div className="pg-code__bar">
          <div ref={tabsRef} className="pg-code__tabs" role="tablist" aria-label="Platform">
            {CODE_TABS.map((t, i) => (
              <button
                key={t.id}
                id={`${tabsId}-${t.id}`}
                type="button"
                role="tab"
                aria-selected={tab === t.id}
                aria-controls={`${tabsId}-panel`}
                tabIndex={tab === t.id ? 0 : -1}
                className="pg-code__tab"
                onClick={() => {
                  setTab(t.id);
                  setCodeOpen(true);
                }}
                onKeyDown={(e) => {
                  const to = e.key === "ArrowRight" ? i + 1 : e.key === "ArrowLeft" ? i - 1 : e.key === "Home" ? 0 : e.key === "End" ? CODE_TABS.length - 1 : null;
                  if (to === null) return;
                  e.preventDefault();
                  focusTab(to);
                }}
              >
                {t.label}
              </button>
            ))}
          </div>
          <CopyButton text={source} />
          <button type="button" className="icon-button pg-code__collapse" aria-expanded={codeOpen} aria-label={codeOpen ? "Hide code" : "Show code"} onClick={() => setCodeOpen((o) => !o)}>
            <ChevronDownIcon size={18} />
          </button>
        </div>
        <div id={`${tabsId}-panel`} role="tabpanel" aria-labelledby={`${tabsId}-${tab}`} className="pg-code__body code" hidden={!codeOpen} dangerouslySetInnerHTML={{ __html: html }} />
      </section>
    </div>
  );
}

export const head = (): Head => ({
  title: "Playground",
  description: "A live Liquid Glass materials lab in WebGL2: switch Clear and Regular, appearance, refraction, corner radius, tint and size, and copy matching SwiftUI, Compose and WPF code.",
  path: "/playground",
});
