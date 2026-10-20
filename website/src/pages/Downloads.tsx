import downloads from "../generated/downloads.json";
import code from "../generated/code.json";
import packages from "../generated/packages.json";
import { CodeBlock, CopyButton, InlineCode } from "../components/Code";
import type { Highlighted } from "../components/Code";
import { Link, REPO_URL, SITE_URL } from "../router";
import type { Head } from "../router";
import { docHref } from "../docs";
import { AndroidIcon, ArrowRightIcon, DownloadIcon, LaptopIcon, PackageIcon, ShieldIcon, TerminalIcon, WindowIcon } from "../components/Icons";
import { SKILL_SUMMARIES } from "../lib/skills";

interface Item {
  id: string;
  name: string;
  platform: string;
  kind: string;
  file: string | null;
  fileName: string | null;
  bytes: number | null;
  sha256: string | null;
  version: string;
  requirements: string;
  updatedAt: string | null;
  available: boolean;
  required?: boolean;
  coordinates?: string;
  packageId?: string;
}

const manifest = downloads as { version: string; generatedAt: string | null; items: Item[]; notes?: Record<string, string> };
const samples = code as unknown as Record<string, Highlighted>;
// The WPF package depends on the Core package: a local package source needs both.
const nugetCore = manifest.items.find((i) => i.packageId && /\.core$/i.test(i.packageId))?.packageId ?? "LiquidGlassEngineering.Core";

/** Decimal (SI) units, as Finder, Windows Explorer and most download pages show sizes: 1 MB = 1,000,000 bytes. */
export function formatBytes(bytes: number | null) {
  if (bytes === null || !Number.isFinite(bytes)) return "";
  if (bytes < 1000) return `${bytes} B`;
  const units = ["KB", "MB", "GB"];
  let v = bytes / 1000;
  let u = 0;
  while (v >= 999.5 && u < units.length - 1) {
    v /= 1000;
    u++;
  }
  return `${v >= 100 ? v.toFixed(0) : v.toFixed(1)} ${units[u]}`;
}

function DownloadRow({ item }: { item: Item }) {
  const size = formatBytes(item.bytes);
  const isRepo = item.kind === "repository";
  return (
    <li className="dl" data-available={item.available ? "" : undefined}>
      <div className="dl__main">
        <p className="dl__name">{item.name}</p>
        <p className="dl__meta">
          {item.fileName ? <span className="dl__file">{item.fileName}</span> : null}
          {item.coordinates ? <span className="dl__file">{item.coordinates}</span> : null}
          {size ? <span>{size}</span> : null}
          <span>v{item.version}</span>
        </p>
        {/* A skill row says what the skill is for; the requirement is stated once, for the section. */}
        <p className="dl__req">{(item.kind === "skill" && SKILL_SUMMARIES[item.id.replace(/^skill-/, "")]) || item.requirements}</p>
        {manifest.notes?.[item.id] && item.available ? <p className="dl__note">{manifest.notes[item.id]}</p> : null}
        {item.available && item.sha256 ? (
          <p className="dl__sha">
            <span className="dl__sha-label">SHA-256</span>
            <code title={item.sha256}>{item.sha256}</code>
            <CopyButton text={item.sha256} label="Copy" className="dl__sha-copy" />
          </p>
        ) : null}
      </div>
      <div className="dl__action">
        {item.available && item.file && !isRepo ? (
          <a className="button button--primary button--small" href={item.file} download={item.fileName ?? true} aria-label={`Download ${item.name}${size ? `, ${size}` : ""}`}>
            <DownloadIcon size={16} />
            Download
          </a>
        ) : item.available && isRepo ? (
          <span className="dl__status dl__status--ok">Live</span>
        ) : (
          <span className="dl__status">Not yet available</span>
        )}
      </div>
    </li>
  );
}

const PLATFORMS = [
  {
    id: "apple",
    title: "Apple",
    subtitle: "SwiftUI for iOS 17+ and macOS 14+, built with Xcode 16 or later",
    Icon: LaptopIcon,
    install: [{ key: "spm", title: "Package.swift" }],
    note: (
      <>
        In Xcode: <strong>File › Add Package Dependencies…</strong>, paste <InlineCode text={REPO_URL} /> and choose <strong>Up to Next Major</strong> from {packages.version}.
      </>
    ),
  },
  {
    id: "android",
    title: "Android",
    subtitle: "Jetpack Compose for Android 7+, glass on Android 13+",
    Icon: AndroidIcon,
    install: [
      { key: "gradleRepo", title: "settings.gradle.kts" },
      { key: "gradleDep", title: "app/build.gradle.kts" },
    ],
    note: (
      <>
        JitPack also serves the library, built from the {packages.version} release tag: add <InlineCode text={'maven("https://jitpack.io")'} /> and depend on{" "}
        <InlineCode text={`com.github.rookasrudzianskas:liquid-glass-mac-windows-ios-android:${packages.version}`} />. JitPack builds each version on its first request, which can take a few
        minutes.
      </>
    ),
  },
  {
    id: "windows",
    title: "Windows",
    subtitle: "WPF for .NET 8+ and .NET Framework 4.8 on Windows 10 and 11",
    Icon: WindowIcon,
    install: [
      { key: "nugetCli", title: ".NET CLI" },
      { key: "nugetConfig", title: "nuget.config" },
    ],
    note: (
      <>
        Or download both <code>.nupkg</code> files below (the WPF package depends on <InlineCode text={nugetCore} />) into one folder and add that folder as a local package source.
      </>
    ),
  },
] as const;

export default function Downloads() {
  // Optional items appear once they exist; required ones show as pending until then.
  const shown = manifest.items.filter((i) => i.available || i.required !== false);
  const byPlatform = (p: string) => shown.filter((i) => i.platform === p);
  const general = shown.filter((i) => i.platform === "all" && i.kind !== "skill");
  const skills = shown.filter((i) => i.kind === "skill");
  const anyAvailable = manifest.items.some((i) => i.available);

  return (
    <div className="page downloads">
      <header className="page-hero">
        <p className="eyebrow eyebrow--spectrum">Version {manifest.version}</p>
        <h1 className="page-hero__title">Downloads</h1>
        <p className="lead">
          Add the library with your platform&apos;s package manager, or grab the packages, the Liquid Glass Studio demo apps, the source and the agent skills directly. Every file lists its size and SHA-256.
        </p>
      </header>

      <div className="page__inner">
        {PLATFORMS.map((p) => (
          <section key={p.id} className="dl-platform" aria-labelledby={`dl-${p.id}`}>
            <div className="dl-platform__head">
              <span className="dl-platform__icon" aria-hidden="true">
                <p.Icon size={22} />
              </span>
              <div>
                <h2 id={`dl-${p.id}`} className="dl-platform__title">
                  {p.title}
                </h2>
                <p className="dl-platform__subtitle">{p.subtitle}</p>
              </div>
              <Link href={docHref(p.id)} className="text-link dl-platform__docs">
                Docs <ArrowRightIcon size={14} />
              </Link>
            </div>
            <div className="dl-platform__body">
              <div className="dl-platform__install">
                <h3 className="dl-subtitle">
                  <TerminalIcon size={16} /> Install
                </h3>
                {p.install.map(({ key, title }) => {
                  const sample = samples[key];
                  return sample ? <CodeBlock key={key} sample={sample} title={title} wrap /> : null;
                })}
                <p className="dl-note">{p.note}</p>
              </div>
              <div className="dl-platform__files">
                <h3 className="dl-subtitle">
                  <PackageIcon size={16} /> Direct downloads
                </h3>
                <ul role="list" className="dl-list">
                  {byPlatform(p.id).map((item) => (
                    <DownloadRow key={item.id} item={item} />
                  ))}
                </ul>
              </div>
            </div>
          </section>
        ))}

        <section className="dl-platform" aria-labelledby="dl-all">
          <div className="dl-platform__head">
            <span className="dl-platform__icon" aria-hidden="true">
              <PackageIcon size={22} />
            </span>
            <div>
              <h2 id="dl-all" className="dl-platform__title">
                Source and agent skills
              </h2>
              <p className="dl-platform__subtitle">The whole repository, and the agent skills as zips for Claude Code or any tool that reads Agent Skills</p>
            </div>
            <Link href="/skills" className="text-link dl-platform__docs">
              About skills <ArrowRightIcon size={14} />
            </Link>
          </div>
          <ul role="list" className="dl-list">
            {general.map((item) => (
              <DownloadRow key={item.id} item={item} />
            ))}
            {skills.map((item) => (
              <DownloadRow key={item.id} item={item} />
            ))}
          </ul>
        </section>

        <section className="dl-verify" aria-labelledby="dl-verify">
          <div>
            <h2 id="dl-verify" className="dl-subtitle">
              <ShieldIcon size={16} /> Verify a download
            </h2>
            <p className="dl-note">
              Compare the checksum with the one listed above, or with{" "}
              <a href="/downloads/SHA256SUMS">SHA256SUMS</a>. The full list is also published as{" "}
              <a href="/downloads/manifest.json">manifest.json</a>.
            </p>
          </div>
          {samples.verify ? <CodeBlock sample={samples.verify} title="Terminal" wrap /> : null}
        </section>

        {!anyAvailable ? <p className="dl-note">Builds are published here as each platform is released.</p> : null}
        <p className="dl-feeds">
          Package feeds: Maven <InlineCode text={`${SITE_URL}/maven`} /> · NuGet <InlineCode text={`${SITE_URL}/nuget/index.json`} />
        </p>
      </div>
    </div>
  );
}

export const head = (): Head => ({
  title: "Downloads",
  description: "Install Liquid Glass with Swift Package Manager, Gradle or NuGet, or download the packages, the Liquid Glass Studio demo apps, the source and the agent skills, with SHA-256 checksums.",
  path: "/downloads",
});
