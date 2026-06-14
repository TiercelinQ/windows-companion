import { app } from "electron";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import * as config from "../../shared/config";
import type { Preferences, PreferenceValue } from "../../shared/types";

const DEFAULTS: Preferences = {
  theme: "light",
  windowWidth: config.WINDOW_DEFAULT_WIDTH,
  windowHeight: config.WINDOW_DEFAULT_HEIGHT,
};

let cache: Preferences | null = null;

function filePath(): string {
  return join(app.getPath("userData"), config.PREFERENCES_FILENAME);
}

/** Whether a preferences file already exists on disk (first run detection). */
export function preferencesFileExists(): boolean {
  return existsSync(filePath());
}

/** Current preferences, merged with defaults. Cached after first read. */
export function getPreferences(): Preferences {
  if (cache) return cache;
  if (!preferencesFileExists()) {
    cache = { ...DEFAULTS };
    return cache;
  }
  try {
    const parsed = JSON.parse(readFileSync(filePath(), "utf8")) as Partial<Preferences>;
    cache = { ...DEFAULTS, ...parsed };
  } catch {
    cache = { ...DEFAULTS };
  }
  return cache;
}

/** Persist a single preference. */
export function setPreference(key: keyof Preferences, value: PreferenceValue): void {
  const prefs = getPreferences();
  Object.assign(prefs, { [key]: value });
  writeFileSync(filePath(), JSON.stringify(prefs, null, 2), "utf8");
}
