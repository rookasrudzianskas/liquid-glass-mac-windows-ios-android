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

/**
 * Code that wraps instead of scrolling sideways (install commands, whose package names
 * and versions sit at the ends of long lines): each line carries its indentation, so a
 * wrapped line continues two columns in from its own start (code.css), and a path or URL
 * may break after a slash. Only the markup changes; Copy copies the code as written.
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
      // Break opportunities after a slash between two other characters, in text only.
      const rest = line
        .slice(at + '<span class="line">'.length)
        .split(/(<[^>]*>)/)
        .map((part) => (part.startsWith("<") ? part : part.replace(/([^/\s])\/(?=[^/\s])/g, "$1/<wbr>")))
        .join("");
      return head + rest;
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

/** A one-line command with a copy button. */
export function Command({ children, text }: { children?: ReactNode; text: string }) {
  return (
    <div className="command">
      <code>{children ?? text}</code>
      <CopyButton text={text} />
    </div>
  );
}
