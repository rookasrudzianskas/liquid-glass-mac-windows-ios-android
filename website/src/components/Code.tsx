import { useEffect, useRef, useState } from "react";
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

const word = (ch: string | undefined) => !!ch && /[A-Za-z0-9]/.test(ch);
const space = (ch: string | undefined) => !!ch && /\s/.test(ch);
const breakable = (ch: string | undefined) => !!ch && ch !== "/" && !space(ch);

// A unit of HTML-escaped text: an entity or one character.
const UNITS = /&(?:#x[0-9a-fA-F]+|#\d+|[a-zA-Z]+);|[\s\S]/gu;
const NAMED: Record<string, string> = { amp: "&", lt: "<", gt: ">", quot: '"', apos: "'", "#39": "'" };
/** The character an HTML-escaped unit of text (a character or an entity) stands for. */
function decode(unit: string) {
  if (unit.length === 1 || !unit.startsWith("&")) return unit;
  const body = unit.slice(1, -1);
  if (body.startsWith("#x")) return String.fromCodePoint(parseInt(body.slice(2), 16));
  if (body.startsWith("#") && body !== "#39") return String.fromCodePoint(parseInt(body.slice(1), 10));
  return NAMED[body] ?? unit;
}

/**
 * The text of HTML, still escaped: its tags (a "<" up to the next ">") removed until none is
 * left, and every "<" or ">" left over, which opens or closes no tag (a "<" with no ">" after
 * it, a lone ">"), escaped, so nothing in it is markup and each of its units stays one unit.
 */
function htmlText(html: string) {
  let text = html;
  let previous: string;
  do {
    previous = text;
    text = text.replace(/<[^>]*>/g, "");
  } while (text !== previous);
  return text.replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

interface Piece {
  /** The wrappers the piece sits in, outermost first: "lead", "flag:3", "name:5", "part:5.1". */
  path: string[];
  /** The Shiki token (its opening tag) the piece belongs to; none for a break. */
  open: string;
  raw: string;
  /** A break opportunity (<wbr>) rather than text. */
  wbr?: boolean;
}

/**
 * One line of wrapping code (the token spans of a Shiki line), with its break rules as
 * markup only, every wrapper an inline span so the line's hanging indent (code.css) sets
 * each of its rows and find in page matches across them. The line splits into segments
 * at its spaces and after its ordinary break opportunities, which get a <wbr>: after a
 * slash between two other characters (a path or URL), after a URL's "://", after a
 * colon between letters or digits (group:artifact:version) and after a call's opening
 * parenthesis before its argument (implementation( before "androidx…", which is
 * otherwise cut inside the word in a snippet narrower than the two). A flag
 * (--ignore-missing, -c) is a span that does not wrap. A segment that has breaks of its
 * own (a hyphen between letters or digits, an "@", or a "." before a letter) is a name
 * (.code__name): its parts, up to an "@" or before such a ".", are separated by a <wbr>,
 * and data-k is the columns it takes on a wrapped row of its own (its length, the line's
 * indentation and the 2 columns a wrapped row is indented by). While the snippet is that
 * wide, the name does not wrap and its <wbr>s are off (code.css, a container query), so
 * the line breaks before it and it moves to the next row whole; in a narrower snippet it
 * breaks between its parts, each part with a hyphen kept whole the same way while it fits
 * (.code__part), and then at the hyphens of a part that does not. The indentation and
 * the first character after it do not wrap (.code__lead), so no row holds only the
 * indentation. Token spans are closed and reopened around the wrappers, so the text is
 * the code as written.
 */
function wrapLine(inner: string): string | null {
  const tokens = [...inner.matchAll(/<span([^>]*)>([^<]*)<\/span>/g)];
  if (tokens.map((m) => m[0]).join("") !== inner) return null;
  const units: { raw: string; ch: string; open: string }[] = [];
  for (const m of tokens)
    for (const raw of (m[2] ?? "").match(UNITS) ?? []) units.push({ raw, ch: decode(raw), open: `<span${m[1] ?? ""}>` });
  const c = units.map((u) => u.ch);
  const text = c.join("");
  // Flags, by character index (FLAG matches on the decoded text; entities are one unit).
  const flagAt = new Map<number, number>();
  const offsets: number[] = [];
  let offset = 0;
  for (const ch of c) offsets.push((offset += ch.length) - ch.length);
  const byOffset = new Map(offsets.map((o, i) => [o, i]));
  let flags = 0;
  for (const m of text.matchAll(FLAG)) {
    const start = byOffset.get((m.index ?? 0) + (m[1] ?? "").length);
    const end = byOffset.get((m.index ?? 0) + m[0].length) ?? c.length;
    if (start === undefined) continue;
    flags++;
    for (let i = start; i < end; i++) flagAt.set(i, flags);
  }
  let indent = 0;
  while (c[indent] === " ") indent++;
  const breakAfter = (i: number) =>
    (c[i] === "/" && breakable(c[i + 1]) && (breakable(c[i - 1]) || (c[i - 1] === "/" && c[i - 2] === ":"))) ||
    (c[i] === ":" && word(c[i - 1]) && word(c[i + 1])) ||
    (c[i] === "(" && word(c[i - 1]) && (c[i + 1] === '"' || word(c[i + 1])));
  const hyphenAt = (k: number) => c[k] === "-" && word(c[k - 1]) && word(c[k + 1]);
  const tags = new Map<string, string>([["lead", '<span class="code__lead">']]);
  // The indentation and the character after it (the whole flag, if a flag starts the line).
  let lead = indent ? indent + 1 : 0;
  while (lead && lead < c.length && flagAt.has(lead) && flagAt.get(lead) === flagAt.get(lead - 1)) lead++;
  const pieces: Piece[] = [];
  const add = (path: string[], i: number) => pieces.push({ path: i < lead ? ["lead"] : path, open: units[i]?.open ?? "", raw: units[i]?.raw ?? "" });
  // The columns `length` characters take on a wrapped row of their own.
  const columns = (length: number) => length + indent + 2;
  let names = 0;
  for (let i = 0; i < c.length; ) {
    if (space(c[i])) {
      add([], i++);
      continue;
    }
    let end = i;
    while (end < c.length && !space(c[end])) if (breakAfter(end++)) break;
    // Parts start before a "." between a letter or digit and a letter, and after an "@" inside the segment.
    const starts = [i];
    for (let k = i + 1; k < end; k++)
      if ((c[k] === "." && word(c[k - 1]) && /[A-Za-z]/.test(c[k + 1] ?? "")) || (c[k - 1] === "@" && k - 1 > i && c[k] !== "@")) starts.push(k);
    const hyphen = (from: number, to: number) => [...Array(to - from).keys()].some((k) => hyphenAt(from + k));
    const flagged = [...Array(end - i).keys()].some((k) => flagAt.has(i + k));
    if (flagged || (starts.length === 1 && !hyphen(i, end))) {
      for (let k = i; k < end; k++) {
        const flag = flagAt.get(k);
        if (flag) tags.set(`flag:${flag}`, '<span class="code__flag">');
        add(flag ? [`flag:${flag}`] : [], k);
      }
    } else {
      const name = `name:${++names}`;
      tags.set(name, `<span class="code__name" data-k="${columns(end - i)}">`);
      starts.forEach((from, p) => {
        const to = starts[p + 1] ?? end;
        if (p) pieces.push({ path: [name], open: "", raw: "", wbr: true });
        const part = `part:${names}.${p + 1}`;
        const own = starts.length > 1 && hyphen(from, to);
        if (own) tags.set(part, `<span class="code__part" data-k="${columns(to - from)}">`);
        for (let k = from; k < to; k++) add(own ? [name, part] : [name], k);
      });
    }
    if (end < c.length && !space(c[end]) && breakAfter(end - 1)) pieces.push({ path: [], open: "", raw: "", wbr: true });
    i = end;
  }
  // Emit: each run of pieces in the same wrappers and token as one token span.
  let out = "";
  let open: string[] = [];
  let run: Piece | null = null;
  const flush = () => {
    if (run) out += `${run.open}${run.raw}${run.open ? "</span>" : ""}`;
    run = null;
  };
  const enter = (path: string[]) => {
    let same = 0;
    while (same < open.length && same < path.length && open[same] === path[same]) same++;
    if (same === open.length && same === path.length) return;
    flush();
    out += "</span>".repeat(open.length - same);
    for (const id of path.slice(same)) out += tags.get(id) ?? "<span>";
    open = path;
  };
  for (const piece of pieces) {
    enter(piece.path);
    if (piece.wbr) {
      flush();
      out += "<wbr>";
    } else if (run && run.open === piece.open) run.raw += piece.raw;
    else {
      flush();
      run = { ...piece };
    }
  }
  enter([]);
  flush();
  return out;
}

/**
 * Code that wraps instead of scrolling sideways (install commands, whose package names
 * and versions sit at the ends of long lines): each line carries its indentation, so a
 * wrapped line continues two columns in from its own start (code.css), a flag never
 * breaks, inside it or after its dashes, and a name (a package, host or plugin ID) breaks
 * inside only when it is wider than a row (wrapLine). It is all markup and CSS, the same
 * in the prerendered page and after hydration, and only the markup changes, never the
 * text, so the text is the code as written and Copy, a selection and find in page see
 * exactly that.
 */
function wrappable(html: string) {
  return html
    .replace(' tabindex="0"', "")
    .split("\n")
    .map((line) => {
      const at = line.indexOf('<span class="line">');
      if (at < 0) return line;
      const body = line.slice(at + '<span class="line">'.length);
      const close = body.lastIndexOf("</span>", body.endsWith("</code></pre>") ? body.length - "</code></pre>".length - 1 : body.length - 1);
      const inner = body.slice(0, close);
      const tail = body.slice(close);
      const text = htmlText(inner);
      const indent = /^ */.exec(text)?.[0].length ?? 0;
      return `${line.slice(0, at)}<span class="line" style="--indent:${indent}">${wrapLine(inner) ?? inner}${tail}`;
    })
    .join("\n");
}

/** A highlighted code block (HTML produced at build time by Shiki). */
export function CodeBlock({ sample, title, caption, className, wrap = false }: { sample: Highlighted; title?: ReactNode; caption?: ReactNode; className?: string; wrap?: boolean }) {
  return (
    <figure className={`code${wrap ? " code--wrap" : ""}${className ? ` ${className}` : ""}`}>
      <div className="code__bar">
        <span className="code__lang">{title ?? sample.label ?? sample.lang}</span>
        <CopyButton text={sample.code} />
      </div>
      <div className="code__body" dangerouslySetInnerHTML={{ __html: wrap ? wrappable(sample.html) : sample.html }} />
      {caption ? <figcaption className="code__caption">{caption}</figcaption> : null}
    </figure>
  );
}

/**
 * Where inline code (a URL, a path, a call, a command line) may break besides its spaces,
 * as indices of the characters a break follows: after a slash between two other
 * characters (a URL's "://" included), after a colon or a dot between a name and a letter
 * or digit, and after an opening parenthesis with something in it.
 */
function inlineBreaks(c: string[]) {
  const at = new Set<number>();
  const name = (ch: string | undefined) => word(ch) || ch === "_";
  for (let i = 1; i < c.length - 1; i++) {
    const [prev, ch, next] = [c[i - 1], c[i], c[i + 1]];
    if (space(prev) || space(next)) continue;
    if (ch === "/" ? next !== "/" : ch === "(" ? name(prev) && next !== ")" : ch === "." ? (name(prev) || prev === ")") && /[A-Za-z_]/.test(next ?? "") : ch === ":" && (name(prev) || prev === ")") && name(next)) at.add(i);
  }
  return at;
}

/**
 * Inline code this long or shorter, with what is glued to it, fits a line of its own in
 * every column the site sets for prose at 320 px (the narrowest, the home page's 222 px
 * paragraph, holds 24 such characters and a period), so it never breaks.
 */
const ALWAYS_FITS = 24;
/** The widest advance of the code font (Menlo and DejaVu Sans Mono 0.6021em, SF Mono 0.6001) at 0.86em, in em of the text around it (code.css). */
const CODE_CHAR = 0.6021 * 0.86;
/** Inter's advances, in em, of the characters text glues to code (no line break between them); anything else counts as 1em. */
const GLUE: Record<string, number> = { ".": 0.277, ",": 0.277, ":": 0.277, ";": 0.289, "!": 0.276, "?": 0.513, ")": 0.354, "(": 0.354, "]": 0.354, "[": 0.354, "}": 0.419, "{": 0.419, "/": 0.355, "-": 0.455, "'": 0.292, "\u2019": 0.249, "\u201c": 0.424, "\u201d": 0.424, '"': 0.455 };
/** The width of text glued to code, in characters of the code font. */
const glueChars = (text: string) => [...text].reduce((sum, ch) => sum + (GLUE[ch] ?? 1), 0) / CODE_CHAR;
/** A piece this short alone on a line reads as debris ("aar", "-"). */
const SHORT = 4;

const escapeChar = (ch: string) => (ch === "&" ? "&amp;" : ch === "<" ? "&lt;" : ch === ">" ? "&gt;" : ch === '"' ? "&quot;" : ch);

/**
 * Inline code's markup, all inline spans and <wbr>s, so its text, a selection's copy and
 * find in page are the code as written. Code that is ALWAYS_FITS characters or fewer,
 * counting the text glued to it (`before` and `after`: an opening bracket, a closing
 * comma or bracket, which no line break separates from it) as the characters of code it
 * is as wide as, is whole (class "whole": it does not wrap). Longer code carries
 * data-fit, that width in characters to the nearest quarter ("31", "31.75"), and does
 * not wrap while the line is that wide (a container query on its paragraph, list or
 * heading, in em: code.css), so it moves to the next line whole whenever it fits a line
 * of its own. Only in a narrower column does it break, and then each of its pieces keeps
 * whole the same way while it fits: a word (a word of four characters or fewer belongs to
 * its neighbour, so "YES" stays with its flag and "{}" with its call), and a segment of a
 * word between its breaks (inlineBreaks); a part of four characters or fewer joins its
 * neighbour while the two are ALWAYS_FITS or shorter or can break at a hyphen
 * (spec/OPTICS.md never leaves "md" alone), and otherwise keeps its break, so the rest is
 * not cut inside a word ("app:" before surface_type="texture_view"). So it breaks at a
 * space or after a slash, colon, dot or opening parenthesis, at a hyphen only inside a
 * segment wider than the column, never after a flag's dashes, and inside a word only
 * where the word is wider than the column, never within its last five characters.
 */
function chipMarkup(units: string[], c: string[], before = "", after = ""): { fit?: string; html: string } {
  const n = c.length;
  const [glueBefore, glueAfter] = [glueChars(before), glueChars(after)];
  if (n + glueBefore + glueAfter <= ALWAYS_FITS) return { html: units.join("") };
  const at = inlineBreaks(c);
  const words: [number, number][] = [];
  for (let i = 0; i < n; ) {
    if (space(c[i])) {
      i++;
      continue;
    }
    let j = i;
    while (j < n && !space(c[j])) j++;
    words.push([i, j]);
    i = j;
  }
  // A word of four characters or fewer goes with the word before it (the first, with the next).
  for (let w = 0; w < words.length && words.length > 1; ) {
    const [s, e] = words[w] ?? [0, 0];
    if (e - s > SHORT) {
      w++;
      continue;
    }
    const lo = w > 0 ? w - 1 : w;
    words.splice(lo, 2, [words[lo]?.[0] ?? 0, words[lo + 1]?.[1] ?? 0]);
    w = 0;
  }
  // A flag (--name, -c) at the start of a word: no break inside it, and its dashes and
  // first character stay together even when it has to be cut.
  const head = new Map<number, number>();
  for (const [s, e] of words) {
    const flag = /^(-+)[A-Za-z0-9][\w-]*/.exec(c.slice(s, e).join(""));
    if (!flag) continue;
    for (let k = s; k < s + flag[0].length; k++) at.delete(k);
    head.set(s, s + (flag[1]?.length ?? 1) + 1);
  }
  const hyphenated = (from: number, to: number) => c.slice(from + 1, to - 1).some((ch, k) => ch === "-" && word(c[from + k]) && word(c[from + k + 2]));
  const segments = words.map(([s, e]) => {
    const parts: [number, number][] = [];
    let from = s;
    for (let k = s; k < e - 1; k++)
      if (at.has(k)) {
        parts.push([from, k + 1]);
        from = k + 1;
      }
    parts.push([from, e]);
    for (let p = 0; p < parts.length && parts.length > 1; ) {
      const [a, b] = parts[p] ?? [0, 0];
      const joined =
        b - a <= SHORT &&
        [p + 1, p - 1].some((q) => {
          const lo = Math.min(p, q);
          const [x, y] = [parts[lo], parts[lo + 1]];
          if (q < 0 || q >= parts.length || !x || !y || (y[1] - x[0] > ALWAYS_FITS && !hyphenated(x[0], y[1]))) return false;
          parts.splice(lo, 2, [x[0], y[1]]);
          return true;
        });
      p = joined ? 0 : p + 1;
    }
    return parts;
  });
  const kept = new Set(segments.flatMap((parts) => parts.slice(0, -1).map(([, b]) => b - 1)));
  // The wrappers of each character, outermost first, and their tags.
  const path: string[][] = c.map(() => []);
  const tags = new Map<string, string>();
  const fit = (from: number, to: number) => {
    const width = to - from + (from === 0 ? glueBefore : 0) + (to === n ? glueAfter : 0);
    return width <= ALWAYS_FITS ? null : `${Math.max(to - from, Math.round(width * 4) / 4)}`;
  };
  const breaksInside = (from: number, to: number) => c.slice(from, to).some((ch) => !/[A-Za-z0-9_]/.test(ch));
  const wrap = (id: string, from: number, to: number, whole = false) => {
    const k = whole ? null : fit(from, to);
    tags.set(id, k ? `<span data-fit="${k}">` : '<span class="whole">');
    for (let i = from; i < to; i++) path[i]?.push(id);
  };
  words.forEach(([s, e], wi) => {
    if ((s !== 0 || e !== n) && breaksInside(s, e)) wrap(`w${wi}`, s, e);
    const parts = segments[wi] ?? [];
    parts.forEach(([a, b], pi) => {
      if (parts.length > 1 && breaksInside(a, b)) wrap(`s${wi}.${pi}`, a, b);
      // A run of the segment without spaces that may be wider than a column: its last
      // five characters, and a flag's dashes with its first character, stay together.
      for (let r = a; r < b; ) {
        let z = r;
        while (z < b && !space(c[z])) z++;
        if (z - r > ALWAYS_FITS) {
          const h = head.get(r);
          if (h) wrap(`h${r}`, r, h, true);
          wrap(`t${r}`, z - 5, z, true);
        }
        r = z + 1;
      }
    });
  });
  let html = "";
  let open: string[] = [];
  const enter = (to: string[]) => {
    let same = 0;
    while (same < open.length && same < to.length && open[same] === to[same]) same++;
    html += "</span>".repeat(open.length - same);
    for (const id of to.slice(same)) html += tags.get(id) ?? "<span>";
    open = to;
  };
  for (let i = 0; i < n; i++) {
    const here = path[i] ?? [];
    enter(here);
    html += units[i] ?? "";
    if (!kept.has(i)) continue;
    const next = path[i + 1] ?? [];
    let same = 0;
    while (same < here.length && same < next.length && here[same] === next[same]) same++;
    enter(here.slice(0, same));
    html += "<wbr>";
  }
  enter([]);
  return { fit: fit(0, n) ?? undefined, html };
}

/** The <code> element of HTML-escaped inline code, with its markup (chipMarkup). */
function inlineCodeElement(escaped: string, before: string, after: string) {
  const units = escaped.match(UNITS) ?? [];
  const chip = chipMarkup(units, units.map(decode), before, after);
  return `<code${chip.fit ? ` data-fit="${chip.fit}"` : ' class="whole"'}>${chip.html}</code>`;
}

/** The length in characters of HTML-escaped text without its tags, spaces trimmed. */
const textLength = (html: string) => (htmlText(html).trim().match(UNITS) ?? []).length;

/**
 * A table's code chip without the breaks content.mjs marks that would leave a prefix of
 * four characters or fewer alone on a line ("<lg:" before GlassToolbarSpacer, "min"
 * before its argument list); a break before a short last part ("Wpf" after
 * LiquidGlassEngineering.) is a wbr.chip-tail, which only a column under 768 px uses
 * (docs.css). It is what lets three tables fit at a few widths, where they would scroll
 * without it (/docs/windows' Install table from 463 to 500 px and its GlassButton table
 * from 413 to 454, /docs/android's options table from 541 to 548); elsewhere it only
 * puts the short part on a line of its own when the column is narrower than the chip.
 */
function chipBreaks(inner: string) {
  const parts = inner.split("<wbr>");
  return parts.reduce((out, part, i) => {
    if (i === 0) return part;
    if (textLength(out) <= 4) return out + part;
    return `${out}${textLength(parts.slice(i).join("")) <= 4 ? '<wbr class="chip-tail">' : "<wbr>"}${part}`;
  }, "");
}

/** The text glued to inline code on one side, no line break between them (a closing comma or bracket, an opening bracket), from the HTML beside it. */
function gluedText(html: string, after: boolean) {
  const run = after ? /^[^\s<]*/.exec(html.replace(/^(<[^>]*>)+/, ""))?.[0] : /[^\s>]*$/.exec(html.replace(/(<[^>]*>)+$/, ""))?.[0];
  return (run?.match(UNITS) ?? []).map(decode).join("");
}

/**
 * Prose HTML with its inline code marked up (chipMarkup) outside code blocks and tables,
 * and a break after the slash between two chips (UIViewRepresentable/NSViewRepresentable),
 * and in tables (whose code content.mjs marks) without a break that leaves a short
 * fragment alone on a line (chipBreaks). Markup only: what a selection copies is unchanged.
 */
export function proseCodeBreaks(html: string) {
  return html
    .split(/(<table[\s\S]*?<\/table>|<pre[\s\S]*?<\/pre>)/)
    .map((part, i) =>
      i % 2
        ? part.startsWith("<table")
          ? part.replace(/<code>((?:[^<]|<wbr>)*)<\/code>/g, (_m, inner: string) => `<code>${chipBreaks(inner)}</code>`)
          : part
        : part
            .replace(/<code>([^<]*)<\/code>/g, (m, text: string, offset: number, whole: string) => {
              const before = gluedText(whole.slice(Math.max(0, offset - 40), offset), false);
              return inlineCodeElement(text, before, gluedText(whole.slice(offset + m.length, offset + m.length + 40), true));
            })
            .replace(/<\/code>\/<code/g, "</code>/<wbr><code"),
    )
    .join("");
}

/**
 * Inline code in a page (chipMarkup); `glued` is the text right after it with no break
 * between (a closing period or bracket).
 */
export function InlineCode({ text, glued = "" }: { text: string; glued?: string }) {
  const chars = [...text];
  const chip = chipMarkup(chars.map(escapeChar), chars, "", glued);
  return chip.fit ? <code data-fit={chip.fit} dangerouslySetInnerHTML={{ __html: chip.html }} /> : <code className="whole">{text}</code>;
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
