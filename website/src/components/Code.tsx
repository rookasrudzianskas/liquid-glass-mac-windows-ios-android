import { useEffect, useLayoutEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { CheckIcon, CopyIcon } from "./Icons";

export async function copyText(text: string) {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    // Fallback for insecure contexts and older browsers.
    const area = document.createElement("textarea");
    area.value = text;
    area.setAttribute("readonly", "");
    area.style.position = "fixed";
    area.style.opacity = "0";
    document.body.appendChild(area);
    area.select();
    const ok = document.execCommand("copy");
    area.remove();
    return ok;
  }
}

export function CopyButton({ text, label = "Copy", className }: { text: string; label?: string; className?: string }) {
  const [copied, setCopied] = useState(false);
  const timer = useRef(0);
  useEffect(() => () => window.clearTimeout(timer.current), []);
  return (
    <button
      type="button"
      className={`copy-button${className ? ` ${className}` : ""}`}
      data-copied={copied ? "" : undefined}
      aria-label={copied ? "Copied" : `${label} to clipboard`}
      onClick={async () => {
        if (await copyText(text)) {
          setCopied(true);
          window.clearTimeout(timer.current);
          timer.current = window.setTimeout(() => setCopied(false), 1600);
        }
      }}
    >
      {copied ? <CheckIcon size={15} /> : <CopyIcon size={15} />}
      <span>{copied ? "Copied" : label}</span>
    </button>
  );
}

export interface Highlighted {
  code: string;
  html: string;
  label?: string;
  lang?: string;
}

// A command-line flag: one or two dashes and its name, after a space or at the start of a line.
const FLAG = /(^|\s)(--?[A-Za-z0-9][\w-]*)/g;

const WORD = /[A-Za-z0-9]/;
const word = (ch: string | undefined) => !!ch && WORD.test(ch);
const breakable = (ch: string | undefined) => !!ch && ch !== "/" && !/\s/.test(ch);
const SOFT = '<wbr class="code__soft">';

/**
 * Break opportunities in a line's text (HTML-escaped, between Shiki's tags; no entity
 * holds any of the characters looked at). Always: after a slash between two other
 * characters (a path or URL), after a URL's "://", and after a colon between letters or
 * digits (group:artifact:version). Held back until freeLongNames frees them: a hyphen
 * inside a name (.code__hyphen, a span around the hyphen and the character after it that
 * does not wrap, since the nearest element holding both characters decides), and a
 * <wbr> after "@" or before a "." that follows a letter or digit (wbr.code__soft, which
 * takes no part in layout until freed: a <wbr> breaks even inside a span that does not wrap).
 */
function breaks(text: string) {
  let out = "";
  for (let i = 0; i < text.length; i++) {
    const ch = text[i] ?? "";
    const prev = text[i - 1];
    const next = text[i + 1];
    if (ch === "-" && word(prev) && word(next)) {
      out += `<span class="code__hyphen">-${next}</span>`;
      i++;
      continue;
    }
    if (ch === "." && word(prev) && /[A-Za-z]/.test(next ?? "")) out += SOFT;
    out += ch;
    if (ch === "/" && breakable(next) && (breakable(prev) || (prev === "/" && text[i - 2] === ":"))) out += "<wbr>";
    else if (ch === ":" && word(prev) && word(next)) out += "<wbr>";
    else if (ch === "@" && prev && next && !/[\s@]/.test(prev) && !/[\s@]/.test(next)) out += SOFT;
  }
  return out;
}

/**
 * Code that wraps instead of scrolling sideways (install commands, whose package names
 * and versions sit at the ends of long lines): each line carries its indentation, so a
 * wrapped line continues two columns in from its own start (code.css), a flag
 * (--ignore-missing, -c) never breaks, inside it or after its dashes (.code__flag), and a
 * name (a package, host or plugin ID) breaks at a slash, a URL's "://" or a coordinate's
 * colon, and at its own "@", "." or hyphens only when it is wider than the line
 * (breaks, freeLongNames). Only the markup changes, in text and never inside a tag, so
 * the text is the code as written and Copy copies exactly that.
 */
function wrappable(html: string) {
  return html
    .replace(' tabindex="0"', "")
    .split("\n")
    .map((line) => {
      const at = line.indexOf('<span class="line">');
      if (at < 0) return line;
      const text = line.slice(at).replace(/<[^>]*>/g, "");
      const indent = /^ */.exec(text)?.[0].length ?? 0;
      const head = line.slice(0, at) + `<span class="line" style="--indent:${indent}">`;
      // Whether the text so far ends in a space (or nothing), which a flag must follow.
      let spaced = true;
      const rest = line
        .slice(at + '<span class="line">'.length)
        .split(/(<[^>]*>)/)
        .map((part) => {
          if (part.startsWith("<") || !part) return part;
          let out = "";
          let from = 0;
          for (const { index = 0, 1: lead = "", 2: flag = "" } of part.matchAll(FLAG)) {
            if (index === 0 && !lead && !spaced) continue;
            const start = index + lead.length;
            out += breaks(part.slice(from, start)) + `<span class="code__flag">${flag}</span>`;
            from = start + flag.length;
          }
          spaced = /\s$/.test(part);
          return out + breaks(part.slice(from));
        })
        .join("");
      return head + rest;
    })
    .join("\n");
}

interface Name {
  chars: { node: Text; at: number; ch: string; hyphen: Element | null; soft: Element | null }[];
  held: Set<Element>;
}

/**
 * In wrapped code, frees the held-back breaks (breaks) of each name that would otherwise
 * be cut at an arbitrary character (overflow-wrap: anywhere): a name is the text between
 * two spaces or ordinary break opportunities. Its "@" and "." breaks go first, and if a
 * part between those is still cut, that part's hyphens; a name that fits keeps them all.
 */
function freeLongNames(body: HTMLElement) {
  for (const el of body.querySelectorAll("[data-free]")) el.removeAttribute("data-free");
  const names: Name[] = [];
  let name: Name | null = null;
  let soft: Element | null = null;
  const walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    if (node instanceof Element) {
      if (node.tagName !== "WBR") continue;
      soft = node.classList.contains("code__soft") ? node : null;
      if (soft) name?.held.add(soft);
      else name = null;
      continue;
    }
    const text = node as Text;
    const parent = text.parentElement;
    if (parent?.closest(".code__flag")) {
      name = null;
      continue;
    }
    const hyphen = parent?.closest(".code__hyphen") ?? null;
    for (let at = 0; at < text.data.length; at++) {
      const ch = text.data[at] ?? "";
      if (/\s/.test(ch)) {
        name = null;
        soft = null;
        continue;
      }
      if (!name) names.push((name = { chars: [], held: new Set() }));
      name.chars.push({ node: text, at, ch, hyphen, soft });
      if (hyphen) name.held.add(hyphen);
      soft = null;
    }
  }
  const range = document.createRange();
  const top = (c: Name["chars"][number]) => {
    range.setStart(c.node, c.at);
    range.setEnd(c.node, c.at + 1);
    const rects = range.getClientRects();
    return rects[rects.length - 1]?.top ?? 0;
  };
  const held = names.filter((n) => n.held.size);
  // Each round frees what the last one showed was needed; freeing can move later names.
  for (let round = 0; round < 8; round++) {
    let changed = false;
    for (const n of held) {
      // The part of the name each character is in: parts are separated by its soft breaks.
      let part = 0;
      const parts = n.chars.map((c) => (c.soft ? ++part : part));
      const cut = new Set<number>();
      let last: number | null = null;
      n.chars.forEach((c, i) => {
        const t = top(c);
        const prev = n.chars[i - 1];
        if (prev && last !== null && t > last + 1) {
          const free = c.soft?.hasAttribute("data-free") || (prev.ch === "-" && prev.hyphen?.hasAttribute("data-free"));
          if (!free) cut.add(parts[i] ?? 0);
        }
        last = t;
      });
      if (!cut.size) continue;
      const softs = [...n.held].filter((el) => el.classList.contains("code__soft") && !el.hasAttribute("data-free"));
      const targets = softs.length ? softs : n.chars.filter((c, i) => c.hyphen && cut.has(parts[i] ?? 0) && !c.hyphen.hasAttribute("data-free")).map((c) => c.hyphen as Element);
      for (const el of targets) el.setAttribute("data-free", "");
      if (targets.length) changed = true;
    }
    if (!changed) break;
  }
}

const useIsoLayoutEffect = typeof window === "undefined" ? useEffect : useLayoutEffect;

/** A highlighted code block (HTML produced at build time by Shiki). */
export function CodeBlock({ sample, title, caption, className, wrap = false }: { sample: Highlighted; title?: ReactNode; caption?: ReactNode; className?: string; wrap?: boolean }) {
  const bodyRef = useRef<HTMLDivElement>(null);
  // Wrapped code frees the breaks of its long names before it paints, and again when its width changes.
  useIsoLayoutEffect(() => {
    const body = bodyRef.current;
    if (!wrap || !body?.querySelector(".code__hyphen, .code__soft")) return;
    let width = -1;
    const fit = () => {
      if (body.clientWidth === width) return;
      width = body.clientWidth;
      freeLongNames(body);
    };
    fit();
    if (typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver(fit);
    observer.observe(body);
    return () => observer.disconnect();
  }, [wrap, sample.html]);
  return (
    <figure className={`code${wrap ? " code--wrap" : ""}${className ? ` ${className}` : ""}`}>
      <div className="code__bar">
        <span className="code__lang">{title ?? sample.label ?? sample.lang}</span>
        <CopyButton text={sample.code} />
      </div>
      <div ref={bodyRef} className="code__body" dangerouslySetInnerHTML={{ __html: wrap ? wrappable(sample.html) : sample.html }} />
      {caption ? <figcaption className="code__caption">{caption}</figcaption> : null}
    </figure>
  );
}

/** A one-line command with a copy button. */
export function Command({ children, text }: { children?: ReactNode; text: string }) {
  return (
    <div className="command">
      <code>{children ?? text}</code>
      <CopyButton text={text} />
    </div>
  );
}
