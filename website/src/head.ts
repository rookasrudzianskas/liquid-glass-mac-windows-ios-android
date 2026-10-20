import { SITE_URL } from "./router";
import type { Head } from "./router";
import { NAV_TONE_BOOT } from "./lib/nav-tone";

export const SITE_NAME = "Liquid Glass Engineering";
export const OG_IMAGE = `${SITE_URL}/og.png`;

export function fullTitle(head: Head) {
  return head.title === SITE_NAME ? head.title : `${head.title} · ${SITE_NAME}`;
}

const escape = (s: string) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

/** Head tags for a prerendered page, with the nav bar's palette until the app hydrates (NAV_TONE_BOOT). */
export function headTags(head: Head) {
  const url = `${SITE_URL}${head.path === "/" ? "/" : head.path}`;
  const title = fullTitle(head);
  return [
    `<title>${escape(title)}</title>`,
    `<meta name="description" content="${escape(head.description)}" />`,
    `<link rel="canonical" href="${escape(url)}" />`,
    head.noindex ? `<meta name="robots" content="noindex" />` : "",
    `<meta property="og:type" content="website" />`,
    `<meta property="og:site_name" content="${SITE_NAME}" />`,
    `<meta property="og:title" content="${escape(title)}" />`,
    `<meta property="og:description" content="${escape(head.description)}" />`,
    `<meta property="og:url" content="${escape(url)}" />`,
    `<meta property="og:image" content="${OG_IMAGE}" />`,
    `<meta property="og:image:width" content="1200" />`,
    `<meta property="og:image:height" content="630" />`,
    `<meta property="og:image:alt" content="Liquid Glass Engineering: glass cards refracting a colorful wave wallpaper" />`,
    `<meta name="twitter:card" content="summary_large_image" />`,
    `<meta name="twitter:title" content="${escape(title)}" />`,
    `<meta name="twitter:description" content="${escape(head.description)}" />`,
    `<meta name="twitter:image" content="${OG_IMAGE}" />`,
    `<script>${NAV_TONE_BOOT}</script>`,
  ]
    .filter(Boolean)
    .join("\n    ");
}

function setMeta(selector: string, attr: string, value: string) {
  const el = document.head.querySelector<HTMLMetaElement | HTMLLinkElement>(selector);
  if (el) el.setAttribute(attr, value);
}

/** Update the document head after a client-side navigation. */
export function applyHead(head: Head) {
  const title = fullTitle(head);
  document.title = title;
  const url = `${SITE_URL}${head.path}`;
  setMeta('meta[name="description"]', "content", head.description);
  setMeta('link[rel="canonical"]', "href", url);
  setMeta('meta[property="og:title"]', "content", title);
  setMeta('meta[property="og:description"]', "content", head.description);
  setMeta('meta[property="og:url"]', "content", url);
  setMeta('meta[name="twitter:title"]', "content", title);
  setMeta('meta[name="twitter:description"]', "content", head.description);
}
