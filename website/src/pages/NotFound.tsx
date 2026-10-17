import { Glass, GlassStage } from "../glass/react";
import { useTheme } from "../theme";
import { Link } from "../router";
import type { Head } from "../router";
import { ArrowRightIcon } from "../components/Icons";

export default function NotFound() {
  const { theme, mounted } = useTheme();
  return (
    <div className="not-found">
      <GlassStage backdrop={mounted ? { kind: "canvas", pattern: "404" } : null} appearance={theme} className="not-found__stage" fallback={<p className="not-found__fallback" aria-hidden="true" data-backdrop-text>404</p>}>
        <div className="not-found__copy">
          <h1 className="not-found__title">This page slipped through the glass.</h1>
          <p className="lead">The address may be mistyped, or the page has moved.</p>
          <div className="not-found__actions">
            <Link href="/" className="button button--primary">
              Go home
            </Link>
            <Link href="/docs" className="button button--secondary">
              Documentation <ArrowRightIcon size={18} />
            </Link>
          </div>
        </div>
        <Glass className="not-found__lens" material="clear" radius="capsule" draggable interactive tabIndex={0} role="img" aria-label="Glass lens. Drag it over the numbers." />
      </GlassStage>
    </div>
  );
}

export const head = (): Head => ({
  title: "Page not found",
  description: "This page does not exist. Find Liquid Glass Engineering docs, downloads and the playground from the home page.",
  path: "/404",
  noindex: true,
});
