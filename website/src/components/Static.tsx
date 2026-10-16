import { useState } from "react";
import type { ReactNode } from "react";

/**
 * A static island: content that never changes after render (diagrams).
 * When hydrating a prerendered page it adopts the server HTML as-is, so React
 * never walks its (large) subtree; after a client-side navigation it renders
 * the children normally.
 */
export function Static({ id, className, children }: { id: string; className?: string; children: ReactNode }) {
  const [html] = useState(() => (typeof document === "undefined" ? null : (document.getElementById(id)?.innerHTML ?? null)));
  if (html !== null) return <div id={id} className={className} dangerouslySetInnerHTML={{ __html: html }} suppressHydrationWarning />;
  return (
    <div id={id} className={className}>
      {children}
    </div>
  );
}
