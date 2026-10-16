import type { Head } from "../router";

export default function DocsIndex() {
  return <div className="section"><h1>DocsIndex</h1></div>;
}

export const head = (): Head => ({ title: "DocsIndex", description: "DocsIndex", path: "/" });
