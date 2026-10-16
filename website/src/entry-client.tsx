import "./styles/index.css";
import { createRoot, hydrateRoot } from "react-dom/client";
import { App } from "./App";
import { applyHead } from "./head";
import { resolve } from "./router";

const root = document.getElementById("root");
if (root) {
  const initial = await resolve(window.location.pathname);
  const stamp = initial.status === 404 ? "404" : initial.path;
  if (root.firstElementChild && root.dataset.route === stamp) {
    hydrateRoot(root, <App initial={initial} />);
  } else {
    // Development server, or HTML prerendered for another route (a host's
    // fallback page): render from scratch instead of hydrating.
    applyHead(initial.head);
    root.textContent = "";
    createRoot(root).render(<App initial={initial} />);
  }
}
