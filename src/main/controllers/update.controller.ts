import { ipcMain } from "electron";
import { IPC } from "../../shared/ipc-channels";
import type { IpcResult } from "../../shared/types";
import { cancelUpgrade, checkUpgrades, isUpdateRunning, startUpgrade } from "../models/update.model";

/** Wire the software-update IPC handlers (winget). */
export function registerUpdateController(): void {
  ipcMain.handle(IPC.UPDATE_START, (e): IpcResult<void> => {
    if (isUpdateRunning()) {
      return { ok: false, error: { type: "warning", message: "Une mise à jour est déjà en cours." } };
    }
    try {
      const sender = e.sender;
      startUpgrade({
        onData: (line) => {
          if (!sender.isDestroyed()) sender.send(IPC.UPDATE_DATA, line);
        },
        onEnd: (result) => {
          if (!sender.isDestroyed()) sender.send(IPC.UPDATE_END, result);
        },
      });
      return { ok: true, data: undefined };
    } catch (err) {
      return {
        ok: false,
        error: {
          type: "danger",
          message: "Impossible de lancer la mise à jour.",
          description: (err as Error).message,
        },
      };
    }
  });

  ipcMain.handle(IPC.UPDATE_CHECK, (e): IpcResult<void> => {
    if (isUpdateRunning()) {
      return { ok: false, error: { type: "warning", message: "Une opération winget est déjà en cours." } };
    }
    try {
      const sender = e.sender;
      checkUpgrades({
        onData: (line) => {
          if (!sender.isDestroyed()) sender.send(IPC.UPDATE_DATA, line);
        },
        onEnd: (result) => {
          if (!sender.isDestroyed()) sender.send(IPC.UPDATE_END, result);
        },
      });
      return { ok: true, data: undefined };
    } catch (err) {
      return {
        ok: false,
        error: {
          type: "danger",
          message: "Impossible de vérifier les mises à jour.",
          description: (err as Error).message,
        },
      };
    }
  });

  ipcMain.handle(IPC.UPDATE_CANCEL, (): IpcResult<void> => {
    cancelUpgrade();
    return { ok: true, data: undefined };
  });
}
