/** Toast severity levels. */
export type ToastType = "success" | "info" | "warning" | "danger";

/** Result returned across the IPC. Business errors are mapped here, never thrown raw. */
export type IpcResult<T> =
  | { ok: true; data: T }
  | { ok: false; error: { type: ToastType; message: string; description?: string } };

/** --- Hardware --- */
export interface CpuInfo {
  model: string;
  cores: number;
  logical: number;
  clockGhz: number;
}
export interface MemoryStick {
  capacityBytes: number;
  speedMhz: number | null;
  manufacturer: string;
}
export interface GpuInfo {
  model: string;
  vramBytes: number | null;
}
export interface DiskInfo {
  model: string;
  mediaType: string;
  sizeBytes: number;
}
export interface HardwareInfo {
  cpu: CpuInfo;
  memory: MemoryStick[];
  totalMemoryBytes: number;
  gpu: GpuInfo[];
  disks: DiskInfo[];
  motherboard: { manufacturer: string; product: string };
  bios: { version: string; releaseDate: string };
}

/** --- System --- */
export interface SystemInfo {
  osName: string;
  edition: string;
  version: string;
  build: string;
  arch: string;
  machineName: string;
  user: string;
  installDate: string;
  lastBoot: string;
  uptime: string;
}

/** --- Network --- */
export interface NetworkAdapter {
  name: string;
  description: string;
  status: string;
  macAddress: string;
  ipv4: string[];
  ipv6: string[];
  subnet: string[];
  gateway: string[];
  dns: string[];
  dhcpEnabled: boolean;
  linkSpeed: string;
}
export interface NetworkInfo {
  hostName: string;
  adapters: NetworkAdapter[];
}

/** --- Update (winget) --- */
export interface UpdateEndResult {
  /** Which winget run just ended: a full upgrade or a read-only availability check. */
  kind: "update" | "check";
  exitCode: number | null;
  canceled: boolean;
  /** Only set when `kind === "check"`: whether at least one upgrade is available. */
  hasUpdates?: boolean;
}

/** --- Preferences --- */
export interface Preferences {
  theme: "light" | "dark";
  windowWidth: number;
  windowHeight: number;
  windowX?: number;
  windowY?: number;
}
export type PreferenceValue = string | number | boolean;

/** Outcome of an export save dialog. */
export interface ExportResult {
  canceled: boolean;
  path?: string;
}

/** Surface exposed to the renderer via the preload contextBridge (`window.api`). */
export interface WindowApi {
  getPreferences(): Promise<IpcResult<Preferences>>;
  setPreference(key: keyof Preferences, value: PreferenceValue): Promise<IpcResult<void>>;
  startUpdate(): Promise<IpcResult<void>>;
  checkUpdates(): Promise<IpcResult<void>>;
  cancelUpdate(): Promise<IpcResult<void>>;
  onUpdateData(cb: (line: string) => void): () => void;
  onUpdateEnd(cb: (result: UpdateEndResult) => void): () => void;
  scanHardware(): Promise<IpcResult<HardwareInfo>>;
  exportHardware(info: HardwareInfo): Promise<IpcResult<ExportResult>>;
  scanSystem(): Promise<IpcResult<SystemInfo>>;
  exportSystem(info: SystemInfo): Promise<IpcResult<ExportResult>>;
  scanNetwork(): Promise<IpcResult<NetworkInfo>>;
  exportNetwork(info: NetworkInfo): Promise<IpcResult<ExportResult>>;
}

declare global {
  interface Window {
    api: WindowApi;
  }
}
