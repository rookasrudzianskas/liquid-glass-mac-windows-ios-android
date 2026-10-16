import type { Head } from "../router";

export default function NotFound() {
  return <div className="section"><h1>NotFound</h1></div>;
}

export const head = (): Head => ({ title: "NotFound", description: "NotFound", path: "/" });
