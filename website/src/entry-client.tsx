import "./styles/index.css";
import { createRoot, hydrateRoot } from "react-dom/client";
import { App } from "./App";
import { applyHead } from "./head";
import { resolve } from "./router";

const root = document.getElementById("root");
if (root) {
  const initial = await resolve(window.location.pathname);
  if (root.firstElementChild) {
    hydrateRoot(root, <App initial={initial} />);
  } else {
    // Development server: nothing was prerendered.
    applyHead(initial.head);
    createRoot(root).render(<App initial={initial} />);
  }
}
