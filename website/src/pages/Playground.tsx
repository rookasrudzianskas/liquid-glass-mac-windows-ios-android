import type { Head } from "../router";

export default function Playground() {
  return <div className="section"><h1>Playground</h1></div>;
}

export const head = (): Head => ({ title: "Playground", description: "Playground", path: "/" });
