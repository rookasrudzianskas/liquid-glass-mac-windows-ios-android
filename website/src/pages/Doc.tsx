import type { Head } from "../router";

export default function Doc() {
  return <div className="section"><h1>Doc</h1></div>;
}

export async function load() {
  return {};
}

export const head = (): Head => ({ title: "Doc", description: "Doc", path: "/" });
