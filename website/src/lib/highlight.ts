// A tiny highlighter for the playground's generated code. It emits the same
// CSS variables as the build-time Shiki output, so both share one palette.

export type Lang = "swift" | "kotlin" | "xml";

const esc = (s: string) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
const span = (token: string, text: string) => `<span style="color:var(--shiki-token-${token})">${esc(text)}</span>`;

const KEYWORDS: Record<Exclude<Lang, "xml">, Set<string>> = {
  swift: new Set(["import", "struct", "var", "let", "some", "true", "false", "func", "return", "private", "public", "in"]),
  kotlin: new Set(["import", "fun", "val", "var", "true", "false", "return", "private", "object"]),
};

function code(source: string, lang: Exclude<Lang, "xml">) {
  const re = /(\/\/[^\n]*)|("(?:[^"\\]|\\.)*")|(\b0x[0-9A-Fa-f]+\b|\b\d+(?:\.\d+)?\b)|(@\w+)|(\.[a-z]\w*)|([A-Za-z_]\w*)(\s*:(?!:))?|(\s+)|([^\sA-Za-z_\d"/]+|\/)/g;
  let out = "";
  let m: RegExpExecArray | null;
  while ((m = re.exec(source))) {
    const [all, comment, str, num, attr, member, ident, colon, ws] = m;
    if (comment) out += span("comment", comment);
    else if (str) out += span("string", str);
    else if (num) out += span("constant", num);
    else if (attr) out += span("keyword", attr);
    else if (member) out += `${esc(".")}${span("constant", member.slice(1))}`;
    else if (ident) {
      if (KEYWORDS[lang].has(ident)) out += span("keyword", ident);
      else if (colon && lang === "swift") out += `${span("parameter", ident)}${esc(colon)}`;
      else if (/^[A-Z]/.test(ident)) out += span("function", ident) + (colon ? esc(colon) : "");
      else out += esc(ident) + (colon ? esc(colon) : "");
    } else if (ws) out += ws;
    else out += span("punctuation", all);
  }
  return out;
}

function xml(source: string) {
  // A comment ends at the first --> or --!>, as an HTML parser ends it.
  const re = /(<!--[\s\S]*?--!?>)|(<\/?)([\w:.]+)|([\w:.]+)(=)("[^"]*")|(\/?>)|(\s+)|([^<\s]+)/g;
  let out = "";
  let m: RegExpExecArray | null;
  while ((m = re.exec(source))) {
    const [all, comment, open, tag, name, eq, value, close, ws] = m;
    if (comment) out += span("comment", comment);
    else if (open && tag) out += span("punctuation", open) + span("keyword", tag);
    else if (name && eq && value) out += span("parameter", name) + span("punctuation", eq) + span("string", value);
    else if (close) out += span("punctuation", close);
    else if (ws) out += ws;
    else out += esc(all);
  }
  return out;
}

/** Highlighted HTML in the same shape as Shiki's output. */
export function highlight(source: string, lang: Lang) {
  const body = lang === "xml" ? xml(source) : code(source, lang);
  const lines = body.split("\n").map((l) => `<span class="line">${l}</span>`).join("\n");
  return `<pre class="shiki css-variables" tabindex="0" aria-label="Code sample"><code>${lines}</code></pre>`;
}
