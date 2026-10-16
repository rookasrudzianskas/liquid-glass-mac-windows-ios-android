import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";

export type Preference = "system" | "light" | "dark";
export type Theme = "light" | "dark";

interface ThemeValue {
  preference: Preference;
  theme: Theme;
  setPreference: (p: Preference) => void;
  /** False during prerender and hydration: render nothing theme-specific until true. */
  mounted: boolean;
}

const ThemeContext = createContext<ThemeValue>({ preference: "system", theme: "light", setPreference: () => undefined, mounted: false });
export const useTheme = () => useContext(ThemeContext);

const KEY = "lg-theme";

function readPreference(): Preference {
  try {
    const v = localStorage.getItem(KEY);
    return v === "light" || v === "dark" ? v : "system";
  } catch {
    return "system";
  }
}

const systemTheme = (): Theme => (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");

/** The theme the inline head script already applied, so hydration starts right. */
const domTheme = (): Theme => (typeof document !== "undefined" && document.documentElement.dataset.theme === "dark" ? "dark" : "light");

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPref] = useState<Preference>("system");
  const [theme, setTheme] = useState<Theme>(domTheme);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setPref(readPreference());
    setMounted(true);
  }, []);

  useEffect(() => {
    if (!mounted) return;
    const apply = () => {
      const next = preference === "system" ? systemTheme() : preference;
      setTheme(next);
      const root = document.documentElement;
      root.dataset.theme = next;
      root.style.colorScheme = next;
      // Browser chrome follows the chosen theme, not only the system one.
      for (const meta of document.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]')) meta.content = next === "dark" ? "#000000" : "#f5f5f7";
    };
    apply();
    if (preference !== "system") return;
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    mq.addEventListener("change", apply);
    return () => mq.removeEventListener("change", apply);
  }, [preference, mounted]);

  const setPreference = useCallback((p: Preference) => {
    setPref(p);
    try {
      if (p === "system") localStorage.removeItem(KEY);
      else localStorage.setItem(KEY, p);
    } catch {
      /* storage unavailable */
    }
  }, []);

  const value = useMemo(() => ({ preference, theme, setPreference, mounted }), [preference, theme, setPreference, mounted]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}
