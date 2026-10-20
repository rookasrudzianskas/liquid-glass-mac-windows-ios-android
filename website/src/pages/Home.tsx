import { Suspense, useRef } from "react";
import screens from "../generated/screens.json";
import { Link, REPO_URL } from "../router";
import { Glass, GlassStage } from "../glass/react";
import { wallpaperNames, wallpaperTone } from "../glass/backdrop";
import type { WallpaperName } from "../glass/backdrop";
import { useTheme } from "../theme";
import { Static } from "../components/Static";
import { InlineCode } from "../components/Code";
import { docHref } from "../docs";
import type { Head } from "../router";
import { Hero, WallpaperFallback } from "./home/Hero";
import { Platforms } from "./home/Platforms";
import { Controls } from "./home/Controls";
import { BevelDiagram, CornerDiagram, RefractionDiagram, RimDiagram, SpringDiagram, ToneDiagram } from "./home/Diagrams";
import { ArrowRightIcon, DownloadIcon, GaugeIcon, GitHubIcon, LayersIcon, ShieldIcon } from "../components/Icons";
import { useScrollRegions } from "../lib/scroll-regions";

const PIPELINE = [
  { name: "Backdrop", detail: "scene content, once" },
  { name: "Frost", detail: "σ 7.33 + fill σ 23" },
  { name: "Refract", detail: "p + offset" },
  { name: "Tone", detail: "Clear · Regular" },
  { name: "Tint", detail: "luma × pigment" },
  { name: "Rim", detail: "specular + shade" },
];

const OPTICS = [
  {
    title: "Continuous corners",
    body: "Each corner is a superellipse quadrant that meets the edges with continuous curvature, and blends into a capsule as the radius grows.",
    Diagram: CornerDiagram,
  },
  {
    title: "A 20-unit bevel",
    body: "Light bends most at the outline and not at all 20 units inside. Small controls never reach full depth, so they refract across their whole face.",
    Diagram: BevelDiagram,
  },
  {
    title: "Refraction",
    body: "A glass pixel shows the backdrop from deeper inside the shape, like a thick convex lens. Unset, the travel is min(60, 0.75 × shorter side), measured at rest.",
    Diagram: RefractionDiagram,
  },
  {
    title: "Rim light",
    body: "A thin specular line runs along the top and bottom, a soft glow sits just inside it, and the sides carry a dark shade.",
    Diagram: RimDiagram,
  },
  {
    title: "Two materials",
    body: "Clear lifts shadows and adds a faint veil. Regular frosts the backdrop, then applies a calibrated tone curve for light and dark.",
    Diagram: ToneDiagram,
  },
  {
    title: "Springs, in closed form",
    body: "Press, release, glow, morph, layout and track springs are (duration, bounce) pairs, solved exactly every frame.",
    Diagram: SpringDiagram,
  },
];

const FEATURES = [
  { title: "Clear and Regular", body: "Clear keeps content sharp. Regular frosts it, brightened in light appearance and deepened in dark." },
  { title: "Tint", body: "Colored glass transmits luminance through the pigment. It colors the glass, never the content." },
  { title: "Elastic interaction", body: "Press grows, dragging stretches with volume preserved, and release overshoots like gel." },
  { title: "Tab bar lens", body: "A glass lens slides between tabs on the track spring and stretches with its speed. Arrow keys move it on desktop." },
  { title: "Slider and switch", body: "Native value controls whose thumbs lift into Clear glass while pressed, with haptics where the OS has them." },
  { title: "Menus", body: "Regular panels grow out of their trigger on the morph spring and collapse back into it. Escape dismisses." },
  { title: "Progressive blur", body: "Six blur levels blend to any edge of scrolling content, so toolbars float over a soft fade." },
  { title: "Toolbars", body: "Capsule toolbars with clusters and spacers, sized for touch on phones and for the pointer on desktops." },
  { title: "Fallbacks", body: "Where the GPU path is unavailable, the platform blur or a translucent fill plus the rim light. Never nothing." },
];

type Screen = { src: string; width: number; height: number; alt: string; name: string };
const SCREEN_LABELS: Record<string, string> = { apple: "macOS and iOS", android: "Android", windows: "Windows" };

function Screens() {
  const order = ["apple", "android", "windows"];
  // A strip that scrolls sideways gets a tab stop (its list keeps its name).
  const ref = useRef<HTMLElement>(null);
  useScrollRegions(ref, ".screens__list", (el) => el.getAttribute("aria-label") ?? "Screenshots");
  const groups = Object.entries(screens as Record<string, Screen[]>)
    .filter(([, list]) => list.length)
    .sort(([a], [b]) => (order.indexOf(a) + 1 || 99) - (order.indexOf(b) + 1 || 99));
  if (!groups.length) return null;
  return (
    <section ref={ref} className="section screens" aria-labelledby="screens-title">
      <div className="section__inner">
        <header className="section-head">
          <p className="eyebrow">Liquid Glass Studio</p>
          <h2 id="screens-title" className="headline">
            The same demo, on every platform.
          </h2>
          <p className="lead">Each port ships the same Studio app, so screenshots line up across platforms.</p>
        </header>
        {groups.map(([platform, list]) => (
          <div key={platform} className="screens__group">
            <h3 className="screens__title">{SCREEN_LABELS[platform] ?? platform}</h3>
            <ul className="screens__list" role="list" aria-label={`${SCREEN_LABELS[platform] ?? platform} screenshots`}>
              {list.map((s) => (
                <li key={s.src} className="screens__item" style={{ aspectRatio: `${s.width} / ${s.height}` }}>
                  <img src={s.src} width={s.width} height={s.height} alt={s.alt} loading="lazy" decoding="async" />
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </section>
  );
}

const pick = (name: string) => (wallpaperNames.includes(name as WallpaperName) ? name : wallpaperNames[0]) as WallpaperName;
// Light glass over Ocean in light appearance; in dark appearance the dark glass sits on
// Midnight, since over Ocean's bright waves white text falls below 4.5:1 (home.css deepens it too).
const CTA_WALLPAPER = { light: pick("ocean"), dark: pick("midnight") };

function CallToAction() {
  const { theme, mounted } = useTheme();
  const name = CTA_WALLPAPER[theme];
  return (
    <section className="section cta" aria-labelledby="cta-title">
      <GlassStage
        backdrop={mounted ? { kind: "wallpaper", name } : null}
        appearance={theme}
        className="cta__stage"
        navTone={mounted ? wallpaperTone(name) : undefined}
        fallback={<WallpaperFallback light={CTA_WALLPAPER.light} dark={CTA_WALLPAPER.dark} />}
      >
        <Glass className="cta__card" material="regular" radius={40} interactive enterDelay={80}>
          <h2 id="cta-title" className="cta__title">
            Start building with glass.
          </h2>
          <p className="cta__lead">Free and open source under the MIT License. Packages, demo apps and agent skills are one click away.</p>
          <div className="cta__buttons">
            <Link href="/downloads" className="button button--primary">
              <DownloadIcon size={18} />
              Downloads
            </Link>
            <Link href="/docs" className="button button--onglass">
              Documentation
              <ArrowRightIcon size={18} />
            </Link>
            <a href={REPO_URL} className="button button--onglass" rel="noopener" target="_blank">
              <GitHubIcon size={18} />
              GitHub
            </a>
          </div>
        </Glass>
      </GlassStage>
    </section>
  );
}

export default function Home() {
  return (
    <>
      <Hero />
      <Suspense fallback={null}>

      <section className="section intro" aria-labelledby="intro-title">
        <div className="section__inner intro__inner">
          <p className="eyebrow eyebrow--spectrum">Not a blur. A lens.</p>
          <h2 id="intro-title" className="statement">
            Most glass effects blur what is behind them. <span className="statement__em">Liquid Glass bends it.</span> A bevel at the rim refracts the content underneath, a calibrated material frosts or clears it, and a rim of light traces the edge.
          </h2>
          <ul className="pillars" role="list">
            <li>
              <span className="pillars__num">4</span>
              <span className="pillars__label">platforms: iOS, macOS, Android, Windows</span>
            </li>
            <li>
              <span className="pillars__num">3</span>
              <span className="pillars__label">GPU ports: Metal, AGSL and HLSL</span>
            </li>
            <li>
              <span className="pillars__num">1</span>
              <span className="pillars__label">optical model, with golden test vectors</span>
            </li>
          </ul>
        </div>
      </section>

      </Suspense>
      <Suspense fallback={null}>
        <Controls />
      </Suspense>
      <Suspense fallback={null}>
        <Platforms />
      </Suspense>
      <Suspense fallback={null}>

      <section className="section optics" aria-labelledby="optics-title">
        <div className="section__inner">
          <header className="section-head">
            <p className="eyebrow">One optical model</p>
            <h2 id="optics-title" className="headline">
              Every pixel, accounted for.
            </h2>
            <p className="lead">
              A single specification defines the shape, refraction, materials, rim light and motion. These diagrams are drawn by the reference implementation itself.
            </p>
          </header>

          <ol className="pipeline" aria-label="Rendering pipeline">
            {PIPELINE.map((step, i) => (
              <li key={step.name} className="pipeline__step">
                <span className="pipeline__index">{String(i + 1).padStart(2, "0")}</span>
                <span className="pipeline__name">{step.name}</span>
                <span className="pipeline__detail">{step.detail}</span>
              </li>
            ))}
          </ol>

          <div className="optics__grid">
            {OPTICS.map(({ title, body, Diagram }, i) => (
              <article key={title} className="optic-card">
                <Static id={`optics-figure-${i}`} className="optic-card__figure">
                  <Diagram />
                </Static>
                <h3 className="optic-card__title">{title}</h3>
                <p className="optic-card__body">{body}</p>
              </article>
            ))}
          </div>

          <div className="vectors">
            <div>
              <h3 className="vectors__title">Tested against golden vectors</h3>
              <p className="vectors__body">
                Every port reproduces <InlineCode text="spec/vectors.json" /> in its unit tests: within 1e-4 on the CPU and 2e-3 for values read back from the GPU. The live glass on this page runs the canonical GLSL library unchanged.
              </p>
            </div>
            <div className="vectors__links">
              <Link href={docHref("optics")} className="button button--secondary button--small">
                Read the optics spec
                <ArrowRightIcon size={16} />
              </Link>
              <Link href="/playground" className="text-link">
                Open the materials lab
              </Link>
            </div>
          </div>
        </div>
      </section>

      </Suspense>
      <Suspense fallback={null}>
      <section className="section features" aria-labelledby="features-title">
        <div className="section__inner">
          <header className="section-head">
            <p className="eyebrow">Components</p>
            <h2 id="features-title" className="headline">
              Everything glass needs to feel alive.
            </h2>
          </header>
          <ul className="feature-grid" role="list">
            {FEATURES.map((f) => (
              <li key={f.title} className="feature">
                <h3 className="feature__title">{f.title}</h3>
                <p className="feature__body">{f.body}</p>
              </li>
            ))}
          </ul>
        </div>
      </section>

      </Suspense>
      <Suspense fallback={null}>
        <Screens />
      </Suspense>
      <Suspense fallback={null}>

      <section className="section quality" aria-labelledby="quality-title">
        <div className="section__inner">
          <header className="section-head">
            <p className="eyebrow">Built right</p>
            <h2 id="quality-title" className="headline">
              Fast, and for everyone.
            </h2>
          </header>
          <div className="quality__grid">
            <article className="quality__card">
              <GaugeIcon size={26} />
              <h3>Performance</h3>
              <ul role="list">
                <li>Refraction, frost and rim light run per pixel in a GPU shader: Metal, AGSL or HLSL.</li>
                <li>Idle glass costs nothing per frame: springs animate only while something moves.</li>
                <li>Clear is the lightest material; Regular adds GPU blurs of the backdrop.</li>
                <li>On this page, the glass draws only when something moves, and pauses offscreen.</li>
              </ul>
            </article>
            <article className="quality__card">
              <ShieldIcon size={26} />
              <h3>Accessibility</h3>
              <ul role="list">
                <li>Reduce transparency swaps in an opaque fill; increase contrast adds an inner border.</li>
                <li>Reduced motion removes grow, stretch and overshoot. Controls keep their platform roles, labels and focus.</li>
                <li>Glass never carries meaning by itself, and foreground colors follow the appearance.</li>
              </ul>
            </article>
            <article className="quality__card">
              <LayersIcon size={26} />
              <h3>Graceful fallbacks</h3>
              <ul role="list">
                <li>Android below 13, WPF without pixel shader 3.0 and reduced transparency draw the fallback material.</li>
                <li>The platform blur where available, otherwise a translucent fill, always with the rim light.</li>
                <li>Never a crash, never an empty surface.</li>
              </ul>
            </article>
          </div>
        </div>
      </section>

      </Suspense>
      <Suspense fallback={null}>
        <CallToAction />
      </Suspense>
    </>
  );
}

export const head = (): Head => ({
  title: "Liquid Glass Engineering",
  description:
    "Native Liquid Glass for iOS, macOS, Android and Windows: refraction, Clear and Regular materials, rim light and elastic motion for SwiftUI, Jetpack Compose and WPF, from one optical model.",
  path: "/",
});
