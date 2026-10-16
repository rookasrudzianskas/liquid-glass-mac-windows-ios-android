import type { Head } from "../router";

export default function Skills() {
  return <div className="section"><h1>Skills</h1></div>;
}

export const head = (): Head => ({ title: "Skills", description: "Skills", path: "/" });
