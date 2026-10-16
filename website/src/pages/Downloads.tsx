import type { Head } from "../router";

export default function Downloads() {
  return <div className="section"><h1>Downloads</h1></div>;
}

export const head = (): Head => ({ title: "Downloads", description: "Downloads", path: "/" });
