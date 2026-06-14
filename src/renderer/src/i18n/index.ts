import fr from "./fr.json";

const messages = fr as Record<string, string>;

/**
 * Minimal translation lookup. i18n is disabled for this project (FR only),
 * but strings stay centralized in fr.json for a future toggle at zero cost.
 * Supports `{name}` interpolation.
 */
export function t(key: string, params?: Record<string, string | number>): string {
  let value = messages[key] ?? key;
  if (params) {
    for (const [name, replacement] of Object.entries(params)) {
      value = value.replace(new RegExp(`\\{${name}\\}`, "g"), String(replacement));
    }
  }
  return value;
}
