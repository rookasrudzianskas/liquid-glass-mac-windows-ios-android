import { useId, useRef, useState } from "react";
import type { ReactNode } from "react";
import snippets from "../../generated/snippets.json";
import code from "../../generated/code.json";
import { CodeBlock } from "../../components/Code";
import type { Highlighted } from "../../components/Code";
import { Link } from "../../router";
import { docHref } from "../../docs";
import { AndroidIcon, ArrowRightIcon, LaptopIcon, PhoneIcon, WindowIcon } from "../../components/Icons";

type PlatformId = "apple" | "android" | "windows";

const fromDocs = snippets as Partial<Record<PlatformId, Highlighted & { source: string | null }>>;
const samples = code as Record<string, Highlighted>;

const PLATFORMS: {
  id: PlatformId;
  tab: string;
  title: string;
  targets: string[];
  gpu: string;
  install: string;
  installTitle: string;
  fallback: string;
  file: string;
  summary: string;
  icon: ReactNode;
}[] = [
  {
    id: "apple",
    tab: "SwiftUI",
    title: "SwiftUI for iOS and macOS",
    targets: ["iOS 17+", "macOS 14+"],
    gpu: "Metal",
    install: "spm",
    installTitle: "Swift Package Manager",
    fallback: "fallbackApple",
    file: "ContentView.swift",
    summary: "Views and modifiers that feel like SwiftUI, rendered with Metal. It does not depend on iOS 26's glassEffect and coexists with it.",
    icon: (
      <span className="platform-icons" aria-hidden="true">
        <PhoneIcon size={18} />
        <LaptopIcon size={18} />
      </span>
    ),
  },
  {
    id: "android",
    tab: "Jetpack Compose",
    title: "Jetpack Compose for Android",
    targets: ["Android 7+", "Glass on Android 13+"],
    gpu: "AGSL",
    install: "gradleDep",
    installTitle: "Gradle",
    fallback: "fallbackAndroid",
    file: "Hero.kt",
    summary: "Composables and Modifier.liquidGlass() on RuntimeShader. Below Android 13 the same API draws the fallback material.",
    icon: (
      <span className="platform-icons" aria-hidden="true">
        <AndroidIcon size={18} />
      </span>
    ),
  },
  {
    id: "windows",
    tab: "WPF",
    title: "WPF for Windows",
    targets: [".NET 8+", "Windows 10+"],
    gpu: "HLSL",
    install: "nugetCli",
    installTitle: ".NET CLI",
    fallback: "fallbackWindows",
    file: "MainWindow.xaml",
    summary: "XAML controls in the lg: namespace, rendered by an HLSL pixel shader. Without pixel shader 3.0 they fall back gracefully.",
    icon: (
      <span className="platform-icons" aria-hidden="true">
        <WindowIcon size={18} />
      </span>
    ),
  },
];

export function Platforms() {
  const [active, setActive] = useState<PlatformId>("apple");
  const id = useId();
  const tabsRef = useRef<HTMLDivElement>(null);

  const focusTab = (index: number) => {
    const p = PLATFORMS[(index + PLATFORMS.length) % PLATFORMS.length];
    if (!p) return;
    setActive(p.id);
    tabsRef.current?.querySelector<HTMLElement>(`#${CSS.escape(`${id}-tab-${p.id}`)}`)?.focus();
  };

  return (
    <section className="section platforms" aria-labelledby="platforms-title">
      <div className="section__inner">
        <header className="section-head">
          <p className="eyebrow">Four platforms</p>
          <h2 id="platforms-title" className="headline">
            Write it the way your platform does.
          </h2>
          <p className="lead">The same concepts under the same names, in the idiom of each toolkit. Learn one port and you can read the others.</p>
        </header>

        <div ref={tabsRef} className="segmented" role="tablist" aria-label="Platform">
          {PLATFORMS.map((p, i) => (
            <button
              key={p.id}
              id={`${id}-tab-${p.id}`}
              type="button"
              role="tab"
              aria-selected={active === p.id}
              aria-controls={`${id}-panel-${p.id}`}
              tabIndex={active === p.id ? 0 : -1}
              className="segmented__item"
              onClick={() => setActive(p.id)}
              onKeyDown={(e) => {
                if (e.key === "ArrowRight") {
                  e.preventDefault();
                  focusTab(i + 1);
                } else if (e.key === "ArrowLeft") {
                  e.preventDefault();
                  focusTab(i - 1);
                }
              }}
            >
              {p.tab}
            </button>
          ))}
        </div>

        {PLATFORMS.map((p) => {
          const snippet = fromDocs[p.id] ?? samples[p.fallback];
          const install = samples[p.install];
          return (
            <div key={p.id} id={`${id}-panel-${p.id}`} role="tabpanel" aria-labelledby={`${id}-tab-${p.id}`} hidden={active !== p.id} className="platform">
              <div className="platform__info">
                {p.icon}
                <h3 className="platform__title">{p.title}</h3>
                <p className="platform__summary">{p.summary}</p>
                <ul className="chips" role="list">
                  {p.targets.map((t) => (
                    <li key={t} className="chip">
                      {t}
                    </li>
                  ))}
                  <li className="chip chip--accent">{p.gpu}</li>
                </ul>
                {install ? <CodeBlock sample={install} title={p.installTitle} className="platform__install" /> : null}
                <div className="platform__links">
                  <Link href={docHref(p.id)} className="button button--secondary button--small">
                    {p.tab} docs
                    <ArrowRightIcon size={16} />
                  </Link>
                  <Link href="/downloads" className="text-link">
                    Downloads
                  </Link>
                </div>
              </div>
              {snippet ? (
                <CodeBlock
                  sample={snippet}
                  title={p.file}
                  className="platform__code"
                  caption={fromDocs[p.id]?.source ? <>From {fromDocs[p.id]?.source}</> : undefined}
                />
              ) : null}
            </div>
          );
        })}
      </div>
    </section>
  );
}
