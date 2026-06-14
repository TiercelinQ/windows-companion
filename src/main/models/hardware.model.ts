import { COMMANDS, PS_HARDWARE, SCAN_TIMEOUT_MS } from "../../shared/config";
import { asArray, runJson } from "./command-runner";
import type { HardwareInfo } from "../../shared/types";

interface RawCpu {
  Name?: string;
  NumberOfCores?: number;
  NumberOfLogicalProcessors?: number;
  MaxClockSpeed?: number;
}
interface RawMemory {
  Capacity?: number | string;
  Speed?: number | null;
  Manufacturer?: string;
}
interface RawGpu {
  Name?: string;
  AdapterRAM?: number | null;
}
interface RawDisk {
  Model?: string;
  MediaType?: string;
  Size?: number | string;
}
interface RawHardware {
  cpu?: RawCpu;
  memory?: RawMemory | RawMemory[];
  gpu?: RawGpu | RawGpu[];
  disks?: RawDisk | RawDisk[];
  motherboard?: { Manufacturer?: string; Product?: string };
  bios?: { SMBIOSBIOSVersion?: string; ReleaseDate?: string };
}

const psArgs = [...COMMANDS.powershell.baseArgs, PS_HARDWARE];

/** Scan hardware specifications via PowerShell / CIM. */
export async function scan(): Promise<HardwareInfo> {
  const raw = await runJson<RawHardware>(COMMANDS.powershell.file, psArgs, SCAN_TIMEOUT_MS);

  const memory = asArray(raw.memory).map((m) => ({
    capacityBytes: Number(m.Capacity ?? 0),
    speedMhz: m.Speed != null ? Number(m.Speed) : null,
    manufacturer: (m.Manufacturer ?? "").trim() || "Inconnu",
  }));

  return {
    cpu: {
      model: (raw.cpu?.Name ?? "").trim(),
      cores: Number(raw.cpu?.NumberOfCores ?? 0),
      logical: Number(raw.cpu?.NumberOfLogicalProcessors ?? 0),
      clockGhz: Number(raw.cpu?.MaxClockSpeed ?? 0) / 1000,
    },
    memory,
    totalMemoryBytes: memory.reduce((sum, m) => sum + m.capacityBytes, 0),
    gpu: asArray(raw.gpu).map((g) => ({
      model: (g.Name ?? "").trim(),
      vramBytes: g.AdapterRAM != null ? Number(g.AdapterRAM) : null,
    })),
    disks: asArray(raw.disks).map((d) => ({
      model: (d.Model ?? "").trim(),
      mediaType: (d.MediaType ?? "").trim() || "Inconnu",
      sizeBytes: Number(d.Size ?? 0),
    })),
    motherboard: {
      manufacturer: (raw.motherboard?.Manufacturer ?? "").trim(),
      product: (raw.motherboard?.Product ?? "").trim(),
    },
    bios: {
      version: (raw.bios?.SMBIOSBIOSVersion ?? "").trim(),
      releaseDate: (raw.bios?.ReleaseDate ?? "").trim(),
    },
  };
}

function formatBytes(bytes: number): string {
  if (!bytes || bytes <= 0) return "0 octet";
  const units = ["octets", "Ko", "Mo", "Go", "To"];
  const i = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  return `${(bytes / 1024 ** i).toFixed(i >= 3 ? 1 : 0)} ${units[i]}`;
}

/** Build a Markdown report from a hardware scan. */
export function toMarkdown(info: HardwareInfo): string {
  const lines: string[] = ["# Spécifications matérielles", ""];

  lines.push("## Processeur", "", "| Champ | Valeur |", "| --- | --- |");
  lines.push(`| Modèle | ${info.cpu.model} |`);
  lines.push(`| Cœurs physiques | ${info.cpu.cores} |`);
  lines.push(`| Processeurs logiques | ${info.cpu.logical} |`);
  lines.push(`| Fréquence | ${info.cpu.clockGhz.toFixed(2)} GHz |`, "");

  lines.push("## Mémoire vive", "", `Capacité totale : ${formatBytes(info.totalMemoryBytes)}`, "");
  lines.push("| Barrette | Capacité | Fréquence | Fabricant |", "| --- | --- | --- | --- |");
  info.memory.forEach((m, i) => {
    const speed = m.speedMhz != null ? `${m.speedMhz} MHz` : "Inconnue";
    lines.push(`| ${i + 1} | ${formatBytes(m.capacityBytes)} | ${speed} | ${m.manufacturer} |`);
  });
  lines.push("");

  lines.push("## Carte graphique", "", "| Modèle | VRAM |", "| --- | --- |");
  info.gpu.forEach((g) => {
    lines.push(`| ${g.model} | ${g.vramBytes != null ? formatBytes(g.vramBytes) : "Inconnue"} |`);
  });
  lines.push("");

  lines.push("## Disques", "", "| Modèle | Type | Capacité |", "| --- | --- | --- |");
  info.disks.forEach((d) => {
    lines.push(`| ${d.model} | ${d.mediaType} | ${formatBytes(d.sizeBytes)} |`);
  });
  lines.push("");

  lines.push("## Carte mère", "", "| Champ | Valeur |", "| --- | --- |");
  lines.push(`| Fabricant | ${info.motherboard.manufacturer} |`);
  lines.push(`| Modèle | ${info.motherboard.product} |`, "");

  lines.push("## BIOS", "", "| Champ | Valeur |", "| --- | --- |");
  lines.push(`| Version | ${info.bios.version} |`);
  lines.push(`| Date | ${info.bios.releaseDate || "Inconnue"} |`, "");

  return lines.join("\n");
}
