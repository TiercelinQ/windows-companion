import { COMMANDS } from "../../shared/config";
import { startStream } from "./command-runner";
import type { UpdateEndResult } from "../../shared/types";

/** Lifecycle handlers for a winget upgrade run. */
export interface UpdateHandlers {
  onData: (line: string) => void;
  onEnd: (result: UpdateEndResult) => void;
}

let cancelCurrent: (() => void) | null = null;
let canceled = false;

/** Whether a winget run (upgrade or check) is currently in progress. */
export function isUpdateRunning(): boolean {
  return cancelCurrent !== null;
}

/**
 * Detect whether the read-only `winget upgrade` listing contains at least one package.
 * Locale/version independent: anchors on the dashes separator line and checks for at
 * least one non-empty row before the next blank line.
 */
function hasUpgradeRows(lines: string[]): boolean {
  const isSeparator = (l: string): boolean => {
    const trimmed = l.trim();
    return trimmed.length >= 5 && /^[-─]+$/.test(trimmed);
  };
  let i = 0;
  while (i < lines.length && !isSeparator(lines[i])) i++;
  for (let j = i + 1; j < lines.length; j++) {
    if (lines[j].trim() === "") break;
    return true;
  }
  return false;
}

/**
 * Start the winget upgrade, streaming output line by line through `handlers`.
 * Guarded by {@link isUpdateRunning} on the controller side.
 */
export function startUpgrade(handlers: UpdateHandlers): void {
  canceled = false;
  cancelCurrent = startStream(COMMANDS.winget.file, [...COMMANDS.winget.args], {
    onData: handlers.onData,
    onClose: (code) => {
      const wasCanceled = canceled;
      cancelCurrent = null;
      canceled = false;
      handlers.onEnd({ kind: "update", exitCode: code, canceled: wasCanceled });
    },
  });
}

/**
 * Run the read-only `winget upgrade` listing, streaming its raw output line by line.
 * On completion, reports whether any upgrade is available (parsed from the output).
 */
export function checkUpgrades(handlers: UpdateHandlers): void {
  canceled = false;
  const lines: string[] = [];
  cancelCurrent = startStream(COMMANDS.wingetCheck.file, [...COMMANDS.wingetCheck.args], {
    onData: (line) => {
      lines.push(line);
      handlers.onData(line);
    },
    onClose: (code) => {
      cancelCurrent = null;
      canceled = false;
      handlers.onEnd({ kind: "check", exitCode: code, canceled: false, hasUpdates: hasUpgradeRows(lines) });
    },
  });
}

/** Cancel the running upgrade, if any. */
export function cancelUpgrade(): void {
  if (cancelCurrent) {
    canceled = true;
    cancelCurrent();
  }
}
