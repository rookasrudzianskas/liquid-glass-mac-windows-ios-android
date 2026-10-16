import { useState } from "react";
import { GlassStage } from "../../glass/react";
import { GlassButton, GlassSlider, GlassSwitch } from "../../glass/controls";
import { wallpaperNames, wallpaperSrcSet } from "../../glass/backdrop";
import type { WallpaperName } from "../../glass/backdrop";
import { useTheme } from "../../theme";
import { ArrowRightIcon, DropIcon, SparkleIcon } from "../../components/Icons";

export function Controls() {
  const { theme, mounted } = useTheme();
  const [wifi, setWifi] = useState(true);
  const [focus, setFocus] = useState(false);
  const [value, setValue] = useState(64);
  const [presses, setPresses] = useState(0);
  const name = (wallpaperNames.includes("aurora" as WallpaperName) ? "aurora" : wallpaperNames[0]) as WallpaperName;

  return (
    <section className="section controls" aria-labelledby="controls-title">
      <div className="section__inner">
        <header className="section-head">
          <p className="eyebrow">Controls</p>
          <h2 id="controls-title" className="headline">
            Press it. It turns to glass.
          </h2>
          <p className="lead">
            Buttons grow and glow under the pointer. Slider and switch thumbs lift into Clear glass while pressed and refract the track beneath them. Try it.
          </p>
        </header>
        <GlassStage
          backdrop={mounted ? { kind: "wallpaper", name } : null}
          appearance={theme}
          className="controls__stage"
          fallback={
            <picture className="stage__fallback">
              <source type="image/avif" srcSet={wallpaperSrcSet(name, "avif")} sizes="min(1180px, 100vw)" />
              <source type="image/webp" srcSet={wallpaperSrcSet(name, "webp")} sizes="min(1180px, 100vw)" />
              <img src={`/wallpapers/${name}-1536.jpg`} alt="" width={3072} height={3072} loading="lazy" decoding="async" />
            </picture>
          }
        >
          <div className="controls__panel">
            <div className="controls__group">
              <p className="controls__label">Buttons</p>
              <div className="controls__row">
                <GlassButton size="large" onClick={() => setPresses((n) => n + 1)} enterDelay={0}>
                  Continue <ArrowRightIcon size={18} />
                </GlassButton>
                <GlassButton size="regular" tint="#3d8bff" onClick={() => setPresses((n) => n + 1)} enterDelay={60}>
                  Tinted
                </GlassButton>
                <GlassButton size="small" material="clear" onClick={() => setPresses((n) => n + 1)} enterDelay={120}>
                  Clear
                </GlassButton>
                <GlassButton size="regular" icon label="Add a drop" onClick={() => setPresses((n) => n + 1)} enterDelay={180}>
                  <DropIcon size={20} />
                </GlassButton>
                <GlassButton size="regular" icon tint="#ff4f9a" label="Sparkle" onClick={() => setPresses((n) => n + 1)} enterDelay={240}>
                  <SparkleIcon size={20} />
                </GlassButton>
              </div>
              <p className="controls__note" aria-live="polite">
                {presses ? `Pressed ${presses} ${presses === 1 ? "time" : "times"}` : "Small, regular and large, Clear or Regular, tinted or not."}
              </p>
            </div>
            <div className="controls__group">
              <p className="controls__label" id="controls-slider-label">
                Slider · {value}
              </p>
              <GlassSlider value={value} onChange={setValue} label="Brightness" />
            </div>
            <div className="controls__group controls__group--switches">
              <div className="controls__switch">
                <span aria-hidden="true">Wi-Fi</span>
                <GlassSwitch checked={wifi} onChange={setWifi} label="Wi-Fi" />
              </div>
              <div className="controls__switch">
                <span aria-hidden="true">Focus</span>
                <GlassSwitch checked={focus} onChange={setFocus} label="Focus" tint="#8a5cff" />
              </div>
            </div>
          </div>
        </GlassStage>
      </div>
    </section>
  );
}
