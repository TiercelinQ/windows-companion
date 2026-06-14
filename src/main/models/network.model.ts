import { COMMANDS, PS_NETWORK, SCAN_TIMEOUT_MS } from "../../shared/config";
import { asArray, runJson } from "./command-runner";
import type { NetworkAdapter, NetworkInfo } from "../../shared/types";

interface RawAdapter {
  name?: string;
  description?: string;
  status?: string;
  macAddress?: string;
  linkSpeed?: string;
  ipv4?: string | string[];
  ipv6?: string | string[];
  subnet?: string | string[];
  gateway?: string | string[];
  dns?: string | string[];
  dhcpEnabled?: boolean;
}
interface RawNetwork {
  hostName?: string;
  adapters?: RawAdapter | RawAdapter[];
}

const psArgs = [...COMMANDS.powershell.baseArgs, PS_NETWORK];

/** Scan network configuration via PowerShell (Get-NetIPConfiguration / Get-NetAdapter). */
export async function scan(): Promise<NetworkInfo> {
  const raw = await runJson<RawNetwork>(COMMANDS.powershell.file, psArgs, SCAN_TIMEOUT_MS);
  const adapters: NetworkAdapter[] = asArray(raw.adapters).map((a) => ({
    name: (a.name ?? "").trim(),
    description: (a.description ?? "").trim(),
    status: (a.status ?? "").trim(),
    macAddress: (a.macAddress ?? "").trim(),
    linkSpeed: (a.linkSpeed ?? "").trim(),
    ipv4: asArray(a.ipv4),
    ipv6: asArray(a.ipv6),
    subnet: asArray(a.subnet),
    gateway: asArray(a.gateway),
    dns: asArray(a.dns),
    dhcpEnabled: Boolean(a.dhcpEnabled),
  }));
  return { hostName: (raw.hostName ?? "").trim(), adapters };
}

function joinOrDash(values: string[]): string {
  return values.length ? values.join(", ") : "—";
}

/** Build a Markdown report from a network scan. */
export function toMarkdown(info: NetworkInfo): string {
  const lines: string[] = ["# Informations réseau", "", `Nom d'hôte : ${info.hostName}`, ""];
  info.adapters.forEach((a) => {
    lines.push(`## ${a.name}`, "", "| Champ | Valeur |", "| --- | --- |");
    lines.push(`| Description | ${a.description} |`);
    lines.push(`| État | ${a.status} |`);
    lines.push(`| Adresse MAC | ${a.macAddress} |`);
    lines.push(`| IPv4 | ${joinOrDash(a.ipv4)} |`);
    lines.push(`| IPv6 | ${joinOrDash(a.ipv6)} |`);
    lines.push(`| Masque (préfixe) | ${joinOrDash(a.subnet)} |`);
    lines.push(`| Passerelle | ${joinOrDash(a.gateway)} |`);
    lines.push(`| DNS | ${joinOrDash(a.dns)} |`);
    lines.push(`| DHCP | ${a.dhcpEnabled ? "Activé" : "Désactivé"} |`);
    lines.push(`| Vitesse de lien | ${a.linkSpeed || "—"} |`, "");
  });
  return lines.join("\n");
}
