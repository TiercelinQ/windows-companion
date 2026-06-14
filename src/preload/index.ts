import { contextBridge, ipcRenderer, type IpcRendererEvent } from "electron";
import { IPC } from "../shared/ipc-channels";
import type { UpdateEndResult, WindowApi } from "../shared/types";

const api: WindowApi = {
  getPreferences: () => ipcRenderer.invoke(IPC.PREF_GET),
  setPreference: (key, value) => ipcRenderer.invoke(IPC.PREF_SET, { key, value }),
  startUpdate: () => ipcRenderer.invoke(IPC.UPDATE_START),
  checkUpdates: () => ipcRenderer.invoke(IPC.UPDATE_CHECK),
  cancelUpdate: () => ipcRenderer.invoke(IPC.UPDATE_CANCEL),
  onUpdateData: (cb) => {
    const listener = (_e: IpcRendererEvent, line: string): void => cb(line);
    ipcRenderer.on(IPC.UPDATE_DATA, listener);
    return () => ipcRenderer.removeListener(IPC.UPDATE_DATA, listener);
  },
  onUpdateEnd: (cb) => {
    const listener = (_e: IpcRendererEvent, result: UpdateEndResult): void => cb(result);
    ipcRenderer.on(IPC.UPDATE_END, listener);
    return () => ipcRenderer.removeListener(IPC.UPDATE_END, listener);
  },
  scanHardware: () => ipcRenderer.invoke(IPC.HARDWARE_SCAN),
  exportHardware: (info) => ipcRenderer.invoke(IPC.HARDWARE_EXPORT, info),
  scanSystem: () => ipcRenderer.invoke(IPC.SYSTEM_SCAN),
  exportSystem: (info) => ipcRenderer.invoke(IPC.SYSTEM_EXPORT, info),
  scanNetwork: () => ipcRenderer.invoke(IPC.NETWORK_SCAN),
  exportNetwork: (info) => ipcRenderer.invoke(IPC.NETWORK_EXPORT, info),
};

contextBridge.exposeInMainWorld("api", api);
