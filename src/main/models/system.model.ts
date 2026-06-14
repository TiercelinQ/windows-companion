import { COMMANDS, PS_SYSTEM, SCAN_TIMEOUT_MS } from "../../shared/config";
import { runJson } from "./command-runner";
import type { SystemInfo } from "../../shared/types";

interface RawSystem {
  osName?: string;
  edition?: string;
  version?: string;
  build?: string;
  arch?: string;
  machineName?: string;
  user?: string;
  installDate?: string;
  lastBoot?: string;
  uptime?: string;
}

const psArgs = [...COMMANDS.powershell.baseArgs, PS_SYSTEM];

/** Scan detailed system information via PowerShell / CIM. */
export async function scan(): Promise<SystemInfo> {
  const raw = await runJson<RawSystem>(COMMANDS.powershell.file, psArgs, SCAN_TIMEOUT_MS);
  return {
    osName: (raw.osName ?? "").trim(),
    edition: (raw.edition ?? "").trim(),
    version: (raw.version ?? "").trim(),
    build: (raw.build ?? "").trim(),
    arch: (raw.arch ?? "").trim(),
    machineName: (raw.machineName ?? "").trim(),
    user: (raw.user ?? "").trim(),
    installDate: (raw.installDate ?? "").trim(),
    lastBoot: (raw.lastBoot ?? "").trim(),
    uptime: (raw.uptime ?? "").trim(),
  };
}

/** Build a Markdown report from a system scan. */
export function toMarkdown(info: SystemInfo): string {
  return [
    "# Informations système",
    "",
    "| Champ | Valeur |",
    "| --- | --- |",
    `| Système | ${info.osName} |`,
    `| Édition | ${info.edition} |`,
    `| Version | ${info.version} |`,
    `| Build | ${info.build} |`,
    `| Architecture | ${info.arch} |`,
    `| Nom de machine | ${info.machineName} |`,
    `| Utilisateur | ${info.user} |`,
    `| Date d'installation | ${info.installDate} |`,
    `| Dernier démarrage | ${info.lastBoot} |`,
    `| Temps de fonctionnement | ${info.uptime} |`,
    "",
  ].join("\n");
}
