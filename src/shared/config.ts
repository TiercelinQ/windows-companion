/**
 * Application-wide constants shared by all layers.
 * No absolute path here — userData paths are resolved in the main process only.
 * No color here — colors live in tokens.css.
 */

export const APP_NAME = "Window Companion";
export const APP_VERSION = "1.0.0";

export const PREFERENCES_FILENAME = "preferences.json";

export const WINDOW_MIN_WIDTH = 1024;
export const WINDOW_MIN_HEIGHT = 768;
export const WINDOW_DEFAULT_WIDTH = 1280;
export const WINDOW_DEFAULT_HEIGHT = 800;

/** Auto-dismiss delay (ms) for success/info/warning toasts. `danger` toasts are persistent. */
export const TOAST_DURATION_MS = 10000;

/** Timeout (ms) for one-shot scan commands. winget has no timeout (long-running, cancelable). */
export const SCAN_TIMEOUT_MS = 60000;

/** Default file names proposed in the export save dialog. */
export const EXPORT_DEFAULT_NAMES = {
  hardware: "window-companion-materiel.md",
  system: "window-companion-systeme.md",
  network: "window-companion-reseau.md",
} as const;

/**
 * Frozen system commands. Executed in the main process only, via execFile/spawn with an
 * argument array (never `shell: true`), never built from renderer input. No injection possible.
 */
export const COMMANDS = {
  winget: {
    file: "winget",
    args: [
      "upgrade",
      "--all",
      "--accept-package-agreements",
      "--include-unknown",
      "--disable-interactivity",
      "--accept-source-agreements",
      "--force",
    ],
  },
  wingetCheck: {
    file: "winget",
    args: ["upgrade", "--include-unknown", "--disable-interactivity", "--accept-source-agreements"],
  },
  powershell: {
    file: "powershell.exe",
    baseArgs: ["-NoProfile", "-NonInteractive", "-Command"],
  },
} as const;

/** PowerShell script — hardware specs as a single JSON object. */
export const PS_HARDWARE = `
$ErrorActionPreference='Stop'
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1 Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed
$mem = @(Get-CimInstance Win32_PhysicalMemory | Select-Object Capacity,Speed,Manufacturer)
$gpuMem = @{}
Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}' -ErrorAction SilentlyContinue | ForEach-Object {
  $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
  if ($p.DriverDesc -and $p.'HardwareInformation.qwMemorySize') { $gpuMem[$p.DriverDesc] = [int64]$p.'HardwareInformation.qwMemorySize' }
}
$gpu = @(Get-CimInstance Win32_VideoController | ForEach-Object {
  $vram = if ($gpuMem.ContainsKey($_.Name)) { $gpuMem[$_.Name] } else { $_.AdapterRAM }
  [pscustomobject]@{ Name=$_.Name; AdapterRAM=$vram }
})
$disk = @(Get-CimInstance Win32_DiskDrive | Select-Object Model,MediaType,Size)
$board = Get-CimInstance Win32_BaseBoard | Select-Object -First 1 Manufacturer,Product
$bios = Get-CimInstance Win32_BIOS | Select-Object -First 1 SMBIOSBIOSVersion,@{n='ReleaseDate';e={ if($_.ReleaseDate){$_.ReleaseDate.ToString('yyyy-MM-dd')}else{''} }}
[pscustomobject]@{ cpu=$cpu; memory=$mem; gpu=$gpu; disks=$disk; motherboard=$board; bios=$bios } | ConvertTo-Json -Depth 4 -Compress
`.trim();

/** PowerShell script — detailed system info as a single JSON object. */
export const PS_SYSTEM = `
$ErrorActionPreference='Stop'
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$cv = Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion'
$up = (Get-Date) - $os.LastBootUpTime
[pscustomobject]@{
  osName=$os.Caption
  edition=$cv.EditionID
  version=$cv.DisplayVersion
  build=$os.BuildNumber
  arch=$os.OSArchitecture
  machineName=$cs.Name
  user=$cs.UserName
  installDate=$os.InstallDate.ToString('yyyy-MM-dd HH:mm')
  lastBoot=$os.LastBootUpTime.ToString('yyyy-MM-dd HH:mm')
  uptime=('{0}j {1}h {2}m' -f $up.Days,$up.Hours,$up.Minutes)
} | ConvertTo-Json -Compress
`.trim();

/** PowerShell script — network configuration per adapter as a single JSON object. */
export const PS_NETWORK = `
$ErrorActionPreference='Stop'
$adapters = @(Get-NetAdapter | ForEach-Object {
  $a = $_
  $cfg = Get-NetIPConfiguration -InterfaceIndex $a.ifIndex -ErrorAction SilentlyContinue
  $dhcp = (Get-NetIPInterface -InterfaceIndex $a.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).Dhcp
  [pscustomobject]@{
    name=$a.Name
    description=$a.InterfaceDescription
    status=[string]$a.Status
    macAddress=$a.MacAddress
    linkSpeed=$a.LinkSpeed
    ipv4=@($cfg.IPv4Address.IPAddress)
    ipv6=@($cfg.IPv6Address.IPAddress)
    subnet=@($cfg.IPv4Address.PrefixLength | ForEach-Object { [string]$_ })
    gateway=@($cfg.IPv4DefaultGateway.NextHop)
    dns=@($cfg.DNSServer | Where-Object { $_.AddressFamily -eq 2 } | ForEach-Object { $_.ServerAddresses } | Where-Object { $_ })
    dhcpEnabled=($dhcp -eq 'Enabled')
  }
})
[pscustomobject]@{ hostName=$env:COMPUTERNAME; adapters=$adapters } | ConvertTo-Json -Depth 5 -Compress
`.trim();
