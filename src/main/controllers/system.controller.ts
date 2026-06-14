import { ipcMain } from "electron";
import { EXPORT_DEFAULT_NAMES } from "../../shared/config";
import { IPC } from "../../shared/ipc-channels";
import type { ExportResult, IpcResult, SystemInfo } from "../../shared/types";
import { CommandError, CommandTimeoutError } from "../models/errors";
import { scan, toMarkdown } from "../models/system.model";
import { saveReport } from "../report-export";

function scanError(err: unknown): IpcResult<never> {
  if (err instanceof CommandTimeoutError) {
    return { ok: false, error: { type: "danger", message: "Le scan a dépassé le délai imparti." } };
  }
  if (err instanceof CommandError) {
    return { ok: false, error: { type: "danger", message: "Le scan a échoué.", description: err.detail } };
  }
  return { ok: false, error: { type: "danger", message: "Erreur inattendue.", description: (err as Error).message } };
}

/** Wire the system-scan IPC handlers. */
export function registerSystemController(): void {
  ipcMain.handle(IPC.SYSTEM_SCAN, async (): Promise<IpcResult<SystemInfo>> => {
    try {
      return { ok: true, data: await scan() };
    } catch (err) {
      return scanError(err);
    }
  });

  ipcMain.handle(
    IPC.SYSTEM_EXPORT,
    async (_e, payload: unknown): Promise<IpcResult<ExportResult>> => {
      if (!payload || typeof payload !== "object") {
        return { ok: false, error: { type: "danger", message: "Données d'export invalides." } };
      }
      try {
        const result = await saveReport(EXPORT_DEFAULT_NAMES.system, toMarkdown(payload as SystemInfo));
        return { ok: true, data: result };
      } catch (err) {
        return {
          ok: false,
          error: { type: "danger", message: "Export impossible.", description: (err as Error).message },
        };
      }
    },
  );
}
