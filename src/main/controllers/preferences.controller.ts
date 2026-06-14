import { ipcMain } from "electron";
import { IPC } from "../../shared/ipc-channels";
import type { IpcResult, PreferenceValue, Preferences } from "../../shared/types";
import { getPreferences, setPreference } from "../models/preferences.model";

const NUMBER_KEYS: (keyof Preferences)[] = ["windowWidth", "windowHeight", "windowX", "windowY"];

function validate(payload: unknown): { key: keyof Preferences; value: PreferenceValue } | null {
  if (!payload || typeof payload !== "object") return null;
  const { key, value } = payload as { key?: unknown; value?: unknown };
  if (key === "theme") {
    return value === "light" || value === "dark" ? { key, value } : null;
  }
  if (typeof key === "string" && (NUMBER_KEYS as string[]).includes(key)) {
    return typeof value === "number" && Number.isFinite(value)
      ? { key: key as keyof Preferences, value }
      : null;
  }
  return null;
}

/** Wire the preferences IPC handlers. */
export function registerPreferencesController(): void {
  ipcMain.handle(IPC.PREF_GET, (): IpcResult<Preferences> => ({ ok: true, data: getPreferences() }));

  ipcMain.handle(IPC.PREF_SET, (_e, payload: unknown): IpcResult<void> => {
    const valid = validate(payload);
    if (!valid) {
      return { ok: false, error: { type: "danger", message: "Préférence invalide." } };
    }
    setPreference(valid.key, valid.value);
    return { ok: true, data: undefined };
  });
}
