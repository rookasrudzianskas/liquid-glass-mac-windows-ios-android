import { useEffect, useId, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { useRouter } from "../router";
import { CloseIcon, SearchIcon } from "./Icons";

interface Entry {
  slug: string;
  title: string;
  anchor: string;
  heading: string;
  text: string;
  /** The section without its code blocks and tables, for snippets (absent when the same as text). */
  prose?: string;
}

interface Result extends Entry {
  score: number;
  snippet: string;
}

const norm = (s: string) => s.toLowerCase().normalize("NFKD").replace(/[̀-ͯ]/g, "");

function search(index: Entry[], query: string): Result[] {
  const terms = norm(query).split(/\s+/).filter(Boolean);
  if (!terms.length) return [];
  const out: Result[] = [];
  for (const e of index) {
    const title = norm(e.title);
    const heading = norm(e.heading);
    const text = norm(e.text);
    let score = 0;
    let ok = true;
    for (const t of terms) {
      const inHeading = heading.includes(t);
      const inTitle = title.includes(t);
      const at = text.indexOf(t);
      if (!inHeading && !inTitle && at < 0) {
        ok = false;
        break;
      }
      score += (inHeading ? 8 : 0) + (inTitle ? 4 : 0) + (at >= 0 ? 1 + 1 / (1 + at / 200) : 0);
      if (heading.startsWith(t)) score += 4;
    }
    if (!ok) continue;
    if (!e.anchor) score += 1;
    // The snippet comes from the prose when it holds a term (or the match is only in the heading
    // or title); a match only inside code shows the code around it.
    const prose = e.prose ? { raw: e.prose, norm: norm(e.prose) } : null;
    const useProse = prose !== null && (terms.some((t) => prose.norm.includes(t)) || !terms.some((t) => text.includes(t)));
    const source = useProse ? prose.raw : e.text;
    const haystack = useProse ? prose.norm : text;
    const first = terms.map((t) => haystack.indexOf(t)).filter((i) => i >= 0).sort((a, b) => a - b)[0] ?? 0;
    const start = Math.max(0, first - 50);
    const snippet = `${start > 0 ? "…" : ""}${source.slice(start, start + 160)}${start + 160 < source.length ? "…" : ""}`;
    out.push({ ...e, score, snippet });
  }
  return out.sort((a, b) => b.score - a.score).slice(0, 12);
}

function Mark({ text, query }: { text: string; query: string }): ReactNode {
  const terms = norm(query).split(/\s+/).filter((t) => t.length > 1);
  if (!terms.length) return text;
  const re = new RegExp(`(${terms.map((t) => t.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|")})`, "gi");
  return text.split(re).map((part, i) => (i % 2 ? <mark key={i}>{part}</mark> : part));
}

export default function Search({ onClose, initialQuery }: { onClose: () => void; initialQuery?: () => string }) {
  const [index, setIndex] = useState<Entry[] | null>(null);
  // Keys typed into the loading shell carry over.
  const [query, setQuery] = useState(() => initialQuery?.() ?? "");
  const [active, setActive] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const dialogRef = useRef<HTMLDivElement>(null);
  const listId = useId();
  const { navigate } = useRouter();

  useEffect(() => {
    void import("../generated/search.json").then((m) => setIndex(m.default as Entry[]));
    // Focus returns to where it was when the dialog closes (handled by Layout).
    const input = inputRef.current;
    input?.focus();
    input?.setSelectionRange(input.value.length, input.value.length);
    const overflow = document.documentElement.style.overflow;
    document.documentElement.style.overflow = "hidden";
    return () => {
      document.documentElement.style.overflow = overflow;
    };
  }, []);

  const results = useMemo(() => (index ? search(index, query) : []), [index, query]);
  useEffect(() => setActive(0), [query]);
  // Keep the active result in view while moving with the arrow keys.
  useEffect(() => {
    document.getElementById(`${listId}-${active}`)?.scrollIntoView({ block: "nearest" });
  }, [active, listId, results]);

  // Escape closes; Tab stays inside the dialog.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
      } else if (e.key === "Tab") {
        const focusables = [...(dialogRef.current?.querySelectorAll<HTMLElement>("input, button, a[href]") ?? [])];
        const first = focusables[0];
        const last = focusables[focusables.length - 1];
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last?.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first?.focus();
        }
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  const go = (r: Result) => {
    onClose();
    navigate(`/docs/${r.slug}${r.anchor ? `#${r.anchor}` : ""}`);
  };

  return (
    <div
      className="search-backdrop"
      onPointerDown={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div
        ref={dialogRef}
        className="search popover"
        role="dialog"
        aria-modal="true"
        aria-label="Search documentation"
      >
        <div className="search__field">
          <SearchIcon size={18} />
          <input
            ref={inputRef}
            type="search"
            placeholder="Search the docs"
            aria-label="Search the docs"
            role="combobox"
            aria-expanded={results.length > 0}
            aria-controls={listId}
            aria-activedescendant={results[active] ? `${listId}-${active}` : undefined}
            aria-autocomplete="list"
            autoComplete="off"
            spellCheck={false}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "ArrowDown") {
                e.preventDefault();
                setActive((a) => Math.min(results.length - 1, a + 1));
              } else if (e.key === "ArrowUp") {
                e.preventDefault();
                setActive((a) => Math.max(0, a - 1));
              } else if (e.key === "Enter") {
                const r = results[active];
                if (r) {
                  e.preventDefault();
                  go(r);
                }
              }
            }}
          />
          <button type="button" className="icon-button" onClick={onClose} aria-label="Close search">
            <CloseIcon size={18} />
          </button>
        </div>
        <div id={listId} role="listbox" className="search__results" aria-label="Results">
          {results.map((r, i) => (
            <div
              key={`${r.slug}#${r.anchor}`}
              id={`${listId}-${i}`}
              role="option"
              aria-selected={i === active}
              className="search__result"
              tabIndex={-1}
              onPointerMove={() => setActive(i)}
              onClick={() => go(r)}
              onKeyDown={(e) => e.key === "Enter" && go(r)}
            >
              <span className="search__path">
                {r.title}
                {r.heading ? <> › <Mark text={r.heading} query={query} /></> : null}
              </span>
              <span className="search__snippet">
                <Mark text={r.snippet} query={query} />
              </span>
            </div>
          ))}
        </div>
        {query && index && !results.length && <p className="search__empty">No results for “{query}”.</p>}
        {!query && <p className="search__empty">Search SwiftUI, Compose and WPF docs, the optics spec and the component contract.</p>}
        <p className="search__hint" aria-hidden="true">
          <kbd>↑</kbd> <kbd>↓</kbd> to move · <kbd>↵</kbd> to open · <kbd>esc</kbd> to close
        </p>
      </div>
    </div>
  );
}
