import { lazy, Suspense, useCallback, useEffect, useId, useLayoutEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Link, REPO_URL, useRouter } from "../router";
import { useTheme } from "../theme";
import type { Preference } from "../theme";
import site from "../generated/site.json";
import { docHref } from "../docs";
import { useNavTone } from "../lib/nav-tone";
import { CloseIcon, GitHubIcon, LogoMark, MenuIcon, MoonIcon, SearchIcon, SunIcon, SystemIcon } from "./Icons";

const Search = lazy(() => import("./Search"));
const loadSearch = () => void import("./Search");

/**
 * The search field while the dialog's code loads (the first ⌘K): the same
 * dialog shell with a live input, so keys typed right away are kept and handed
 * to the real dialog when it mounts.
 */
function SearchShell({ initial, onType, onClose }: { initial: string; onType: (query: string) => void; onClose: () => void }) {
  const [value, setValue] = useState(initial);
  const inputRef = useRef<HTMLInputElement>(null);
  useLayoutEffect(() => inputRef.current?.focus(), []);
  return (
    <div className="search-backdrop" onPointerDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="search popover" role="dialog" aria-modal="true" aria-label="Search documentation">
        <div className="search__field">
          <SearchIcon size={18} />
          <input
            ref={inputRef}
            type="search"
            placeholder="Search the docs"
            aria-label="Search the docs"
            autoComplete="off"
            spellCheck={false}
            value={value}
            onChange={(e) => {
              onType(e.target.value);
              setValue(e.target.value);
            }}
            onKeyDown={(e) => e.key === "Escape" && onClose()}
          />
        </div>
      </div>
    </div>
  );
}

const NAV = [
  { href: "/playground", label: "Playground" },
  { href: "/docs", label: "Docs" },
  { href: "/downloads", label: "Downloads" },
  { href: "/skills", label: "Agent Skills" },
];

function isActive(path: string, href: string) {
  return path === href || path.startsWith(`${href}/`);
}

const THEMES: { value: Preference; label: string; Icon: typeof SunIcon }[] = [
  { value: "light", label: "Light", Icon: SunIcon },
  { value: "dark", label: "Dark", Icon: MoonIcon },
  { value: "system", label: "System", Icon: SystemIcon },
];

function ThemeMenu() {
  const { preference, setPreference } = useTheme();
  const [open, setOpen] = useState(false);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const id = useId();

  useEffect(() => {
    if (!open) return;
    const items = () => [...(menuRef.current?.querySelectorAll<HTMLElement>('[role="menuitemradio"]') ?? [])];
    const current = items().find((el) => el.getAttribute("aria-checked") === "true") ?? items()[0];
    current?.focus();
    const onDown = (e: PointerEvent) => {
      if (!menuRef.current?.contains(e.target as Node) && !buttonRef.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setOpen(false);
        buttonRef.current?.focus();
      } else if (e.key === "ArrowDown" || e.key === "ArrowUp") {
        e.preventDefault();
        const list = items();
        const i = list.indexOf(document.activeElement as HTMLElement);
        list[(i + (e.key === "ArrowDown" ? 1 : -1) + list.length) % list.length]?.focus();
      } else if (e.key === "Tab") setOpen(false);
    };
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div className="theme-menu">
      <button
        ref={buttonRef}
        type="button"
        className="icon-button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? id : undefined}
        aria-label="Appearance"
        title="Appearance"
        onClick={() => setOpen((v) => !v)}
      >
        {/* Both icons render; CSS shows the one matching <html data-theme>, so there is no flash. */}
        <SunIcon size={18} className="theme-icon theme-icon--light" />
        <MoonIcon size={18} className="theme-icon theme-icon--dark" />
      </button>
      {open && (
        <div ref={menuRef} id={id} role="menu" aria-label="Appearance" className="popover theme-menu__list">
          {THEMES.map(({ value, label, Icon }) => (
            <button
              key={value}
              type="button"
              role="menuitemradio"
              aria-checked={preference === value}
              className="popover__item"
              onClick={() => {
                setPreference(value);
                setOpen(false);
                buttonRef.current?.focus();
              }}
            >
              <Icon size={16} />
              <span>{label}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

function Nav({ onSearch, overlay }: { onSearch: () => void; overlay: boolean }) {
  const { path } = useRouter();
  const [modKey, setModKey] = useState("⌘");
  useEffect(() => {
    if (!/Mac|iPhone|iPad/i.test(navigator.platform || navigator.userAgent)) setModKey("Ctrl ");
  }, []);
  const [open, setOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => setOpen(false), [path]);
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);
  const sheetRef = useRef<HTMLDivElement>(null);
  const toggleRef = useRef<HTMLButtonElement>(null);
  // Over a glass stage the bar takes the palette of the stage's backdrop (data-tone, set by useNavTone; layout.css).
  const navRef = useRef<HTMLElement>(null);
  const barRef = useRef<HTMLElement>(null);
  useNavTone(navRef, barRef);
  useEffect(() => {
    if (!open) return;
    // Escape closes; focus inside the sheet goes back to the toggle instead of falling to <body>.
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      const inside = sheetRef.current?.contains(document.activeElement) ?? false;
      setOpen(false);
      if (inside || document.activeElement === toggleRef.current) toggleRef.current?.focus();
    };
    // A tap outside the sheet (and its toggle), or scrolling the page, closes it.
    const onDown = (e: PointerEvent) => {
      const target = e.target as Node;
      if (!sheetRef.current?.contains(target) && !toggleRef.current?.contains(target)) setOpen(false);
    };
    const startY = window.scrollY;
    const onScroll = () => Math.abs(window.scrollY - startY) > 24 && setOpen(false);
    document.addEventListener("keydown", onKey);
    document.addEventListener("pointerdown", onDown);
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("pointerdown", onDown);
      window.removeEventListener("scroll", onScroll);
    };
  }, [open]);

  return (
    <header ref={navRef} className="nav" data-overlay={overlay ? "" : undefined} data-scrolled={scrolled ? "" : undefined} data-open={open ? "" : undefined}>
      <nav ref={barRef} className="nav__bar" aria-label="Primary">
        <Link href="/" className="nav__brand" aria-label="Liquid Glass Engineering, home">
          <LogoMark size={26} />
          <span className="nav__name">
            Liquid Glass <span className="nav__name-sub">Engineering</span>
          </span>
        </Link>
        <ul className="nav__links" role="list">
          {NAV.map((item) => (
            <li key={item.href}>
              <Link href={item.href} className="nav__link" data-label={item.label} data-active={isActive(path, item.href) ? "" : undefined}>
                {item.label}
              </Link>
            </li>
          ))}
        </ul>
        <div className="nav__actions">
          <button type="button" className="nav__search" onClick={onSearch} onMouseEnter={loadSearch} onFocus={loadSearch} aria-keyshortcuts="Meta+K Control+K">
            <SearchIcon size={16} />
            <span className="nav__search-label">Search</span>
            <kbd className="nav__kbd" aria-hidden="true">
              {modKey}K
            </kbd>
          </button>
          <ThemeMenu />
          <a className="icon-button nav__github" href={REPO_URL} rel="noopener" target="_blank" aria-label="GitHub repository (opens in a new tab)" title="GitHub">
            <GitHubIcon size={18} />
          </a>
          <button ref={toggleRef} type="button" className="icon-button nav__toggle" aria-expanded={open} aria-controls="mobile-menu" aria-label={open ? "Close menu" : "Open menu"} onClick={() => setOpen((v) => !v)}>
            {open ? <CloseIcon size={20} /> : <MenuIcon size={20} />}
          </button>
        </div>
      </nav>
      <div ref={sheetRef} id="mobile-menu" className="nav__sheet" hidden={!open}>
        <ul role="list">
          {NAV.map((item) => (
            <li key={item.href}>
              <Link href={item.href} className="nav__sheet-link" data-active={isActive(path, item.href) ? "" : undefined}>
                {item.label}
              </Link>
            </li>
          ))}
          <li>
            <a className="nav__sheet-link" href={REPO_URL} rel="noopener" target="_blank">
              GitHub
            </a>
          </li>
        </ul>
      </div>
    </header>
  );
}

function Footer() {
  const year = 2026;
  return (
    <footer className="footer">
      <div className="footer__inner">
        <div className="footer__brand">
          <Link href="/" className="footer__logo" aria-label="Liquid Glass Engineering, home">
            <LogoMark size={30} />
            <span>Liquid Glass Engineering</span>
          </Link>
          <p className="footer__tagline">Native Liquid Glass for iOS, macOS, Android and Windows. One optical model, three GPU languages.</p>
        </div>
        <nav className="footer__cols" aria-label="Footer">
          <div>
            <h2 className="footer__title">Product</h2>
            <ul role="list">
              <li><Link href="/playground">Playground</Link></li>
              <li><Link href="/downloads">Downloads</Link></li>
              <li><Link href="/skills">Agent Skills</Link></li>
            </ul>
          </div>
          <div>
            <h2 className="footer__title">Docs</h2>
            <ul role="list">
              <li><Link href={docHref("apple")}>SwiftUI</Link></li>
              <li><Link href={docHref("android")}>Jetpack Compose</Link></li>
              <li><Link href={docHref("windows")}>WPF</Link></li>
              <li><Link href={docHref("optics")}>Optics</Link></li>
            </ul>
          </div>
          <div>
            <h2 className="footer__title">Project</h2>
            <ul role="list">
              <li><a href={REPO_URL} rel="noopener" target="_blank">GitHub</a></li>
              <li><a href={`${REPO_URL}/issues`} rel="noopener" target="_blank">Issues</a></li>
              <li><a href={`${REPO_URL}/blob/main/LICENSE`} rel="noopener" target="_blank">MIT License</a></li>
              <li><a href="/llms.txt">llms.txt</a></li>
            </ul>
          </div>
        </nav>
      </div>
      <div className="footer__legal">
        <p>
          Optical model ported from{" "}
          <a href="https://github.com/Glass-HQ/liquid-glass" rel="noopener" target="_blank">
            Glass HQ liquid-glass
          </a>
          , MIT. © {year} Rokas Rudzianskas. Released under the MIT License.
        </p>
        <p className="footer__notice">{site.notice}</p>
      </div>
    </footer>
  );
}

export function Layout({ children, routeKey }: { children: ReactNode; routeKey: string }) {
  const [searchOpen, setSearchOpen] = useState(false);
  // Typed before the dialog's code arrived; handed to the dialog when it mounts.
  const pendingQuery = useRef("");
  // Where focus goes back when the dialog closes.
  const returnFocus = useRef<HTMLElement | null>(null);
  const openRef = useRef(false);
  const setSearch = useCallback((next: boolean) => {
    if (next === openRef.current) return;
    openRef.current = next;
    if (next) {
      loadSearch();
      pendingQuery.current = "";
      const active = document.activeElement;
      returnFocus.current = active instanceof HTMLElement && active !== document.body ? active : null;
    }
    setSearchOpen(next);
  }, []);
  const openSearch = useCallback(() => setSearch(true), [setSearch]);
  const closeSearch = useCallback(() => setSearch(false), [setSearch]);
  const rememberQuery = useCallback((query: string) => {
    pendingQuery.current = query;
  }, []);
  const typedQuery = useCallback(() => pendingQuery.current, []);

  useEffect(() => {
    if (searchOpen) return;
    const el = returnFocus.current;
    returnFocus.current = null;
    if (el?.isConnected) el.focus({ preventScroll: true });
  }, [searchOpen]);

  useEffect(() => {
    // Fetch the dialog's code while the browser is idle, so ⌘K opens it at once.
    const idle = "requestIdleCallback" in window;
    const handle = idle ? window.requestIdleCallback(loadSearch, { timeout: 4000 }) : window.setTimeout(loadSearch, 2500);
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null;
      const typing = target && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName));
      if ((e.key === "k" || e.key === "K") && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        setSearch(!openRef.current);
      } else if (e.key === "/" && !typing && !e.metaKey && !e.ctrlKey && !e.altKey) {
        e.preventDefault();
        setSearch(true);
      }
    };
    document.addEventListener("keydown", onKey);
    return () => {
      if (idle) window.cancelIdleCallback(handle);
      else window.clearTimeout(handle);
      document.removeEventListener("keydown", onKey);
    };
  }, [setSearch]);

  const overlay = routeKey === "home" || routeKey === "playground";
  return (
    <div className="app" data-route={routeKey}>
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <Nav onSearch={openSearch} overlay={overlay} />
      <main id="main" tabIndex={-1} className="main">
        {children}
      </main>
      {routeKey !== "playground" && <Footer />}
      <div id="route-announcer" className="sr-only" aria-live="polite" aria-atomic="true" />
      {searchOpen && (
        <Suspense fallback={<SearchShell initial="" onType={rememberQuery} onClose={closeSearch} />}>
          <Search onClose={closeSearch} initialQuery={typedQuery} />
        </Suspense>
      )}
    </div>
  );
}
