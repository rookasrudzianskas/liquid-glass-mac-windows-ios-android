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

interface Piece {
  /** The wrappers the piece sits in, outermost first: "flag:3", "name:5", "part:5.1". */
  path: string[];
  /** The Shiki token (its opening tag) the piece belongs to. */
  open: string;
  raw: string;
}

const WRAPPER: Record<string, string> = { flag: "code__flag", name: "code__name", part: "code__part" };

/**
 * One line of wrapping code (the token spans of a Shiki line), with its break rules as
 * markup only. The line splits into segments at its spaces and after its ordinary break
 * opportunities, which get a <wbr>: after a slash between two other characters (a path
 * or URL), after a URL's "://" and after a colon between letters or digits
 * (group:artifact:version). A flag (--ignore-missing, -c) is a span that does not wrap.
 * A segment that has breaks of its own (a hyphen between letters or digits, an "@", or a
 * "." before a letter) is a name (.code__name, an inline block no wider than the line,
 * code.css): the line breaks before it rather than inside it, so it moves to the next
 * line whole, and only a name wider than the line wraps inside, first between its parts
 * (each part up to an "@" or before such a "." is an inline block too, .code__part) and
 * then at the hyphens of a part still wider than the line. Token spans are closed and
 * reopened around the wrappers, so the text is the code as written.
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
  const breakAfter = (i: number) => (c[i] === "/" && breakable(c[i + 1]) && (breakable(c[i - 1]) || (c[i - 1] === "/" && c[i - 2] === ":"))) || (c[i] === ":" && word(c[i - 1]) && word(c[i + 1]));
  const pieces: (Piece | "wbr")[] = [];
  const add = (path: string[], i: number) => pieces.push({ path, open: units[i]?.open ?? "", raw: units[i]?.raw ?? "" });
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
    let hyphen = false;
    for (let k = i + 1; k < end; k++) {
      if (c[k] === "." && word(c[k - 1]) && /[A-Za-z]/.test(c[k + 1] ?? "")) starts.push(k);
      if (c[k - 1] === "@" && k - 1 > i && c[k] !== "@") starts.push(k);
      if (c[k] === "-" && word(c[k - 1]) && word(c[k + 1])) hyphen = true;
    }
    const parts = new Set(starts);
    const flagged = [...Array(end - i).keys()].some((k) => flagAt.has(i + k));
    if (flagged || (parts.size === 1 && !hyphen)) {
      for (let k = i; k < end; k++) add(flagAt.has(k) ? [`flag:${flagAt.get(k)}`] : [], k);
    } else {
      const name = `name:${++names}`;
      let part = 0;
      for (let k = i; k < end; k++) {
        if (parts.has(k)) part++;
        add(parts.size > 1 ? [name, `part:${names}.${part}`] : [name], k);
      }
    }
    if (end < c.length && !space(c[end]) && breakAfter(end - 1)) pieces.push("wbr");
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
    for (const id of path.slice(same)) out += `<span class="${WRAPPER[id.split(":")[0] ?? ""]}">`;
    open = path;
  };
  for (const piece of pieces) {
    if (piece === "wbr") {
      enter([]);
      flush();
      out += "<wbr>";
      continue;
    }
    enter(piece.path);
    if (run && run.open === piece.open) run.raw += piece.raw;
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
 * inside only when it is wider than the line (wrapLine). It is all markup and CSS, the
 * same in the prerendered page and after hydration, and only the markup changes, never
 * the text, so the text is the code as written and Copy copies exactly that.
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
      const text = inner.replace(/<[^>]*>/g, "");
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
 * Where inline code in prose (a URL, a path, a call, a coordinate) may break besides its
 * spaces and hyphens, as indices of the characters a break follows: after a slash between
 * two other characters (a URL's "://" included), after a colon or a dot between a name
 * and a letter or digit, and after an opening parenthesis with something in it. So
 * overflow-wrap: anywhere cuts only a segment wider than the column. A short XML prefix
 * (<lg:) keeps its name.
 */
function inlineBreaks(c: string[]) {
  const at = new Set<number>();
  const name = (ch: string | undefined) => word(ch) || ch === "_";
  for (let i = 1; i < c.length - 1; i++) {
    const [prev, ch, next] = [c[i - 1], c[i], c[i + 1]];
    if (space(prev) || space(next)) continue;
    if (ch === "/" ? next !== "/" : ch === "(" ? name(prev) && next !== ")" : ch === "." ? (name(prev) || prev === ")") && /[A-Za-z_]/.test(next ?? "") : ch === ":" && (name(prev) || prev === ")") && name(next)) {
      // <lg:Name and x:Name: the prefix stays with the name.
      if (ch === ":" && /(^|[\s<{])[A-Za-z]{1,3}$/.test(c.slice(Math.max(0, i - 5), i).join(""))) continue;
      at.add(i);
    }
  }
  return at;
}

/** HTML-escaped inline code text with a <wbr> at each of its breaks (inlineBreaks). */
export function inlineCodeHtml(escaped: string) {
  const units = escaped.match(UNITS) ?? [];
  const at = inlineBreaks(units.map(decode));
  return units.map((u, i) => (at.has(i) ? `${u}<wbr>` : u)).join("");
}

/** The length in characters of HTML-escaped text without its tags, spaces trimmed. */
const textLength = (html: string) => (html.replace(/<[^>]*>/g, "").trim().match(UNITS) ?? []).length;

/**
 * A table's code chip without the breaks content.mjs marks that would leave a prefix of
 * four characters or fewer alone on a line ("<lg:" before GlassToolbarSpacer, "min"
 * before its argument list); a break before a short last part ("Wpf" after
 * LiquidGlassEngineering.) is a wbr.chip-tail, which only a phone-width column uses
 * (docs.css), where without it a table would no longer fit.
 */
function chipBreaks(inner: string) {
  const parts = inner.split("<wbr>");
  return parts.reduce((out, part, i) => {
    if (i === 0) return part;
    if (textLength(out) <= 4) return out + part;
    return `${out}${textLength(parts.slice(i).join("")) <= 4 ? '<wbr class="chip-tail">' : "<wbr>"}${part}`;
  }, "");
}

/**
 * Prose HTML with breaks in its inline code (inlineCodeHtml) outside code blocks and
 * tables, and after the slash between two chips (UIViewRepresentable/NSViewRepresentable),
 * and in tables (whose code content.mjs marks) without a break that leaves a short
 * fragment alone on a line (chipBreaks). Text-only: what a selection copies is unchanged.
 */
export function proseCodeBreaks(html: string) {
  return html
    .split(/(<table[\s\S]*?<\/table>|<pre[\s\S]*?<\/pre>)/)
    .map((part, i) =>
      i % 2
        ? part.startsWith("<table")
          ? part.replace(/<code>((?:[^<]|<wbr>)*)<\/code>/g, (_m, inner: string) => `<code>${chipBreaks(inner)}</code>`)
          : part
        : part.replace(/<code>([^<]*)<\/code>/g, (_m, text: string) => `<code>${inlineCodeHtml(text)}</code>`).replaceAll("</code>/<code>", "</code>/<wbr><code>"),
    )
    .join("");
}

/** Inline code with a break opportunity (<wbr>) at each of its breaks (inlineBreaks). */
export function InlineCode({ text }: { text: string }) {
  const chars = [...text];
  const at = inlineBreaks(chars);
  const parts: ReactNode[] = [];
  let from = 0;
  for (const i of [...at].sort((a, b) => a - b)) {
    parts.push(chars.slice(from, i + 1).join(""), <wbr key={i} />);
    from = i + 1;
  }
  parts.push(chars.slice(from).join(""));
  return <code>{parts}</code>;
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
