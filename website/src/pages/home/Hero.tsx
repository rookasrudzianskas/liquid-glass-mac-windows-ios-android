import { useState } from "react";
import { Glass, GlassStage, GlassTabBar } from "../../glass/react";
import { notePictureFormat, wallpaperInfo, wallpaperNames, wallpaperSrcSet } from "../../glass/backdrop";
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

function HeroPicture() {
  const light = wallpaperNames[0];
  const dark = (wallpaperNames.includes("midnight" as WallpaperName) ? "midnight" : light) as WallpaperName;
  if (!light) return null;
  const sizes = "(max-aspect-ratio: 1/1) 100vh, 100vw";
  return (
    <picture className="stage__fallback">
      <source media="(prefers-color-scheme: dark)" type="image/avif" srcSet={wallpaperSrcSet(dark, "avif")} sizes={sizes} />
      <source media="(prefers-color-scheme: dark)" type="image/webp" srcSet={wallpaperSrcSet(dark, "webp")} sizes={sizes} />
      <source type="image/avif" srcSet={wallpaperSrcSet(light, "avif")} sizes={sizes} />
      <source type="image/webp" srcSet={wallpaperSrcSet(light, "webp")} sizes={sizes} />
      <img
        src={`/wallpapers/${light}-1536.jpg`}
        srcSet={wallpaperSrcSet(light, "jpg")}
        sizes={sizes}
        alt=""
        width={3072}
        height={3072}
        fetchPriority="high"
        decoding="async"
        onLoad={(e) => notePictureFormat(e.currentTarget.currentSrc)}
      />
    </picture>
  );
}

export function Hero() {
  const { theme, mounted } = useTheme();
  const [picked, setPicked] = useState<WallpaperName | null>(null);
  const [material, setMaterial] = useState<Material>("regular");
  const [tint, setTint] = useState(0);
  const [refraction, setRefraction] = useState(true);
  // Until mounted the theme is unknown to React (the head script set it on
  // <html>), so markup stays theme-neutral and CSS picks the right tone.
  const current = mounted ? (picked ?? defaultWallpaper(theme)) : null;
  const wallpaper = current ?? defaultWallpaper("light");
  const tone = current ? wallpaperInfo[current].tone : undefined;
  const lightTone = wallpaperInfo[defaultWallpaper("light")].tone;
  const darkTone = wallpaperInfo[defaultWallpaper("dark")].tone;
  const tintHex = TINTS[tint]?.hex ?? null;
  const items = wallpaperNames.map((name) => ({
    id: name,
    label: LABELS[name] ?? name,
    icon: <span className="swatch" style={{ background: SWATCH[name] ?? wallpaperInfo[name].color }} aria-hidden="true" />,
  }));

  return (
    <section className="hero" data-tone={tone} data-tone-light={lightTone} data-tone-dark={darkTone} aria-labelledby="hero-title">
      <GlassStage backdrop={current ? { kind: "wallpaper", name: current } : null} appearance={theme} className="hero__stage" fallback={<HeroPicture />}>
        <div className="hero__copy">
          <p className="hero__eyebrow">iOS · macOS · Android · Windows</p>
          <h1 id="hero-title" className="hero__title">
            Liquid Glass.
            <br />
            <span className="hero__title-2">Native on every platform.</span>
          </h1>
          <p className="hero__lead">
            Refraction, Clear and Regular materials, rim light and elastic motion for SwiftUI, Jetpack Compose and WPF. One optical model, ported pixel for pixel.
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
            className="hero-card"
            material={material}
            radius={30}
            refraction={refraction ? 60 : 0}
            tint={tintHex}
            draggable
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
                  {material === "regular" ? "Regular" : "Clear"} · refraction {refraction ? 60 : 0}
                  {tintHex ? ` · ${TINTS[tint]?.name.toLowerCase()} tint` : ""}
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
            refraction={refraction ? 60 : 0}
            draggable
            interactive
            enterDelay={140}
            tabIndex={0}
            role="img"
            aria-label="Clear glass lens. Drag it with the pointer, or focus it and use the arrow keys."
          />

          <Glass className="hero-toolbar" material={material} radius="capsule" tint={tintHex} interactive stretch enterDelay={220} role="toolbar" aria-label="Glass options">
            <button type="button" className="toolbar-button" aria-pressed={material === "regular"} onClick={() => setMaterial((m) => (m === "regular" ? "clear" : "regular"))}>
              <DropIcon size={18} />
              <span>{material === "regular" ? "Regular" : "Clear"}</span>
            </button>
            <button type="button" className="toolbar-button" onClick={() => setTint((t) => (t + 1) % TINTS.length)} aria-label={`Tint: ${TINTS[tint]?.name}. Change tint`}>
              <PaletteIcon size={18} />
              <span>{TINTS[tint]?.name === "None" ? "Tint" : TINTS[tint]?.name}</span>
            </button>
            <button type="button" className="toolbar-button" aria-pressed={refraction} onClick={() => setRefraction((r) => !r)}>
              <SparkleIcon size={18} />
              <span>Refraction</span>
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
