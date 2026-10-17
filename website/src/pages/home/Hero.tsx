import { useRef, useState } from "react";
import type { CSSProperties } from "react";
import { Glass, GlassStage, GlassTabBar, useDefaultRefraction } from "../../glass/react";
import { wallpaperInfo, wallpaperNames } from "../../glass/backdrop";
import type { WallpaperName } from "../../glass/backdrop";
import type { Material } from "../../glass/renderer";
import { useTheme } from "../../theme";
import { Link } from "../../router";
import { ArrowRightIcon, DownloadIcon, DropIcon, LogoMark, PaletteIcon, SparkleIcon } from "../../components/Icons";

const TINTS: { name: string; hex: string | null }[] = [
  { name: "None", hex: null },
  { name: "Blue", hex: "#3d8bff" },
  { name: "Pink", hex: "#ff4f9a" },
  { name: "Mint", hex: "#2fd4a3" },
  { name: "Amber", hex: "#ffb02e" },
];

const LABELS: Record<string, string> = { dunes: "Dunes", aurora: "Aurora", ocean: "Ocean", midnight: "Midnight" };
const SWATCH: Record<string, string> = {
  dunes: "linear-gradient(160deg,#f7d9a8,#ef8a6c 45%,#b04aa8 75%,#4b2c7a)",
  aurora: "linear-gradient(160deg,#1c5f6b,#3ad9e6 35%,#7a5cff 65%,#ff5fa2)",
  ocean: "linear-gradient(160deg,#dbeaf6,#5fb4e6 40%,#2f6fd0 70%,#1d2d78)",
  midnight: "linear-gradient(160deg,#0c1024,#2ab4f0 40%,#e04fb7 65%,#20223f)",
};

const defaultWallpaper = (theme: "light" | "dark"): WallpaperName =>
  (theme === "dark" ? (wallpaperNames.includes("midnight" as WallpaperName) ? "midnight" : wallpaperNames[0]) : wallpaperNames[0]) as WallpaperName;

/**
 * The static wallpaper under the canvas, as CSS backgrounds so the one that
 * matches <html data-theme> (set before first paint) is the only download.
 */
/** A stage's wallpaper before WebGL draws, chosen by CSS from the theme on <html> (no flash before mounting). */
export function WallpaperFallback({ light, dark }: { light: WallpaperName; dark: WallpaperName }) {
  const vars = (name: WallpaperName, prefix: string) => ({
    [`--${prefix}-s1`]: `url("/wallpapers/${name}-1024.webp")`,
    [`--${prefix}-s2`]: `url("/wallpapers/${name}-2048.webp")`,
    [`--${prefix}-l1`]: `url("/wallpapers/${name}-2048.webp")`,
    [`--${prefix}-l2`]: `url("/wallpapers/${name}-3072.webp")`,
    [`--${prefix}-color`]: wallpaperInfo[name].color,
  });
  return <div className="stage__fallback wallpaper-fallback" aria-hidden="true" style={{ ...vars(light, "wl"), ...vars(dark, "wd") } as CSSProperties} />;
}

export function Hero() {
  const { theme, mounted } = useTheme();
  const [picked, setPicked] = useState<WallpaperName | null>(null);
  const [material, setMaterial] = useState<Material>("regular");
  const [tint, setTint] = useState(0);
  const [refraction, setRefraction] = useState(true);
  // With refraction on, the glass sets none and follows its size (OPTICS.md section 2).
  const cardRef = useRef<HTMLDivElement>(null);
  const cardRefraction = useDefaultRefraction(cardRef);
  // Until mounted the theme is unknown to React (the head script set it on
  // <html>), so markup stays theme-neutral and CSS picks the right tone.
  const current = mounted ? (picked ?? defaultWallpaper(theme)) : null;
  const wallpaper = current ?? defaultWallpaper("light");
  const tone = current ? wallpaperInfo[current].tone : undefined;
  const lightTone = wallpaperInfo[defaultWallpaper("light")].tone;
  const darkTone = wallpaperInfo[defaultWallpaper("dark")].tone;
  // The wash under the copy that keeps it legible on this wallpaper (home.css).
  // Phones get their own strength (the copy spans the whole width there).
  const lightInfo = wallpaperInfo[defaultWallpaper("light")];
  const darkInfo = wallpaperInfo[defaultWallpaper("dark")];
  const scrimStyle = {
    "--hero-scrim-light": lightInfo.scrim,
    "--hero-scrim-light-narrow": lightInfo.scrimNarrow,
    "--hero-scrim-dark": darkInfo.scrim,
    "--hero-scrim-dark-narrow": darkInfo.scrimNarrow,
    ...(current ? { "--hero-scrim-wide": wallpaperInfo[current].scrim, "--hero-scrim-narrow": wallpaperInfo[current].scrimNarrow } : null),
  } as CSSProperties;
  const tintHex = TINTS[tint]?.hex ?? null;
  const tintName = TINTS[tint]?.name ?? "None";
  const materialName = material === "regular" ? "Regular" : "Clear";
  const items = wallpaperNames.map((name) => ({
    id: name,
    label: LABELS[name] ?? name,
    icon: <span className="swatch" style={{ background: SWATCH[name] ?? wallpaperInfo[name].color }} aria-hidden="true" />,
  }));

  return (
    <section className="hero" data-tone={tone} data-tone-light={lightTone} data-tone-dark={darkTone} style={scrimStyle} aria-labelledby="hero-title">
      <GlassStage backdrop={current ? { kind: "wallpaper", name: current } : null} appearance={theme} className="hero__stage" fallback={<WallpaperFallback light={defaultWallpaper("light")} dark={defaultWallpaper("dark")} />}>
        <div className="hero__copy">
          <p className="hero__eyebrow">iOS · macOS · Android · Windows</p>
          <h1 id="hero-title" className="hero__title">
            Liquid Glass.
            <br />
            <span className="hero__title-2">Native on every platform.</span>
          </h1>
          <p className="hero__lead">
            Refraction, Clear and Regular materials, rim light and elastic motion for SwiftUI, Jetpack Compose and WPF. One optical model, tested against the same golden vectors.
          </p>
          <div className="hero__cta">
            <Link href="/downloads" className="button button--primary">
              <DownloadIcon size={18} />
              Download
            </Link>
            <Link href="/docs" className="button button--glass">
              Get started
              <ArrowRightIcon size={18} />
            </Link>
          </div>
        </div>

        <div className="hero__play">
          <Glass
            ref={cardRef}
            className="hero-card"
            material={material}
            radius={30}
            refraction={refraction ? undefined : 0}
            tint={tintHex}
            draggable
            bounds="parent"
            interactive
            enterDelay={60}
            tabIndex={0}
            role="group"
            aria-label="Glass card. Drag it with the pointer, or focus it and use the arrow keys."
          >
            <div className="hero-card__top">
              <LogoMark size={40} />
              <div>
                <p className="hero-card__title">Liquid Glass</p>
                <p className="hero-card__meta">
                  {materialName} · refraction {refraction ? (cardRefraction ?? "auto") : 0}
                  {tintHex ? ` · ${tintName.toLowerCase()} tint` : ""}
                </p>
              </div>
            </div>
            <ul className="hero-card__chips" aria-label="Platforms">
              <li>SwiftUI</li>
              <li>Compose</li>
              <li>WPF</li>
            </ul>
            <p className="hero-card__hint" aria-hidden="true">
              Drag me
            </p>
          </Glass>

          <Glass
            className="hero-lens"
            material="clear"
            radius="capsule"
            refraction={refraction ? undefined : 0}
            draggable
            bounds="parent"
            interactive
            enterDelay={140}
            tabIndex={0}
            role="img"
            aria-label="Clear glass lens. Drag it with the pointer, or focus it and use the arrow keys."
          />

          {/* Each button shows its state in the icon too (a frosted or clear drop, the tint's
              color, a filled sparkle), because narrow toolbars hide the labels. */}
          <Glass className="hero-toolbar" material={material} radius="capsule" tint={tintHex} interactive stretch enterDelay={220} role="toolbar" aria-label="Glass options">
            <button
              type="button"
              className="toolbar-button"
              title={`Material: ${materialName}`}
              aria-label={`Material: ${materialName}. Switch to ${material === "regular" ? "Clear" : "Regular"}`}
              onClick={() => setMaterial((m) => (m === "regular" ? "clear" : "regular"))}
            >
              <DropIcon size={18} fill={material === "regular" ? "currentColor" : "none"} fillOpacity={0.4} />
              <span className="toolbar-button__label">{materialName}</span>
            </button>
            <button type="button" className="toolbar-button" title={`Tint: ${tintName}`} onClick={() => setTint((t) => (t + 1) % TINTS.length)} aria-label={`Tint: ${tintName}. Change tint`}>
              <span className="toolbar-button__icon">
                <PaletteIcon size={18} />
                {tintHex ? <span className="toolbar-button__dot" style={{ background: tintHex }} /> : null}
              </span>
              <span className="toolbar-button__label">{tintHex ? tintName : "Tint"}</span>
            </button>
            <button type="button" className="toolbar-button" title={`Refraction: ${refraction ? "on" : "off"}`} aria-pressed={refraction} onClick={() => setRefraction((r) => !r)}>
              <SparkleIcon size={18} fill={refraction ? "currentColor" : "none"} />
              <span className="toolbar-button__label">Refraction</span>
            </button>
          </Glass>

          <div className="hero-tabbar-slot">
            <GlassTabBar className="hero-tabbar" items={items} value={wallpaper} onChange={(id) => setPicked(id as WallpaperName)} label="Wallpaper" material={material} tint={tintHex} enterDelay={300} />
          </div>
        </div>
      </GlassStage>
    </section>
  );
}
