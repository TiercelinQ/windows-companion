import { BrowserWindow, dialog } from "electron";
import { writeFile } from "node:fs/promises";
import type { ExportResult } from "../shared/types";

/**
 * Prompt for a destination and write a Markdown report there.
 * The path comes from the OS save dialog only — never from the renderer.
 * @throws on a filesystem write failure (mapped to a toast by the calling controller).
 */
export async function saveReport(defaultName: string, markdown: string): Promise<ExportResult> {
  const win = BrowserWindow.getFocusedWindow() ?? BrowserWindow.getAllWindows()[0];
  const options = {
    defaultPath: defaultName,
    filters: [{ name: "Markdown", extensions: ["md"] }],
  };
  const result = win
    ? await dialog.showSaveDialog(win, options)
    : await dialog.showSaveDialog(options);
  if (result.canceled || !result.filePath) return { canceled: true };
  await writeFile(result.filePath, markdown, "utf8");
  return { canceled: false, path: result.filePath };
}
