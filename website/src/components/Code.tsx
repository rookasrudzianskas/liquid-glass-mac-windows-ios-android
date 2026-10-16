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

/** A highlighted code block (HTML produced at build time by Shiki). */
export function CodeBlock({ sample, title, caption, className }: { sample: Highlighted; title?: ReactNode; caption?: ReactNode; className?: string }) {
  return (
    <figure className={`code${className ? ` ${className}` : ""}`}>
      <div className="code__bar">
        <span className="code__lang">{title ?? sample.label ?? sample.lang}</span>
        <CopyButton text={sample.code} />
      </div>
      <div className="code__body" dangerouslySetInnerHTML={{ __html: sample.html }} />
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
