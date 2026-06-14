import { useCallback, useEffect, useState } from "react";

type Theme = "light" | "dark";

function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme;
}

/** Theme state synced with persisted preferences; toggles light/dark. */
export function useTheme(): { theme: Theme; toggle: () => void } {
  const [theme, setTheme] = useState<Theme>("light");

  useEffect(() => {
    void (async () => {
      const res = await window.api.getPreferences();
      if (res.ok) {
        applyTheme(res.data.theme);
        setTheme(res.data.theme);
      }
    })();
  }, []);

  const toggle = useCallback(() => {
    setTheme((prev) => {
      const next: Theme = prev === "dark" ? "light" : "dark";
      applyTheme(next);
      void window.api.setPreference("theme", next);
      return next;
    });
  }, []);

  return { theme, toggle };
}
