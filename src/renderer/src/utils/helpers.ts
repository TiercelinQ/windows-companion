/** Pure formatting helpers for the renderer. No business logic, no Node/Electron/React imports. */

const BYTE_UNITS = ["octets", "Ko", "Mo", "Go", "To"];

/** Human-readable byte size, e.g. 17179869184 → "16 Go". */
export function formatBytes(bytes: number): string {
  if (!bytes || bytes <= 0) return "0 octet";
  const i = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), BYTE_UNITS.length - 1);
  return `${(bytes / 1024 ** i).toFixed(i >= 3 ? 1 : 0)} ${BYTE_UNITS[i]}`;
}

/** Clock speed in GHz, e.g. 3.4 → "3.40 GHz". */
export function formatGhz(ghz: number): string {
  return `${ghz.toFixed(2)} GHz`;
}

/** Memory frequency, null-safe. */
export function formatMhz(mhz: number | null): string {
  return mhz != null ? `${mhz} MHz` : "—";
}

/** Join a list with commas, or "—" when empty. */
export function joinList(values: string[]): string {
  return values.length ? values.join(", ") : "—";
}
