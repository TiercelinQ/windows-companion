#!/usr/bin/env node
// scripts/ensure-electron.cjs
// Installation du binaire Electron. Electron >= 42 ne déclare plus de
// "postinstall" : ce script est ce qui télécharge le binaire. Si l'extraction a
// échoué silencieusement (antivirus, permission, téléchargement interrompu), on
// récupère depuis le cache local. Sans cela : "Error: Electron uninstall" au
// lancement de electron-vite.

const fs = require("node:fs");
const path = require("node:path");
const os = require("node:os");
const { execFileSync } = require("node:child_process");

const ELECTRON_DIR = path.join(__dirname, "..", "node_modules", "electron");
const DIST_DIR = path.join(ELECTRON_DIR, "dist");
const PATH_TXT = path.join(ELECTRON_DIR, "path.txt");

function relExecPath() {
  switch (process.platform) {
    case "win32":
      return "electron.exe";
    case "darwin":
      return "Electron.app/Contents/MacOS/Electron";
    default:
      return "electron";
  }
}

function cacheDir() {
  if (process.env.ELECTRON_CACHE) return process.env.ELECTRON_CACHE;
  switch (process.platform) {
    case "win32":
      return path.join(
        process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local"),
        "electron",
        "Cache"
      );
    case "darwin":
      return path.join(os.homedir(), "Library", "Caches", "electron");
    default:
      return path.join(
        process.env.XDG_CACHE_HOME || path.join(os.homedir(), ".cache"),
        "electron"
      );
  }
}

function isInstalled() {
  return (
    fs.existsSync(PATH_TXT) &&
    fs.existsSync(path.join(DIST_DIR, relExecPath()))
  );
}

// Electron >= 42 ne déclare plus de script "postinstall" : npm ne lance donc
// jamais install.js et une installation propre ne contient aucun binaire.
// L'installeur est appelé ici.
//
// Il est appelé avec un environnement nettoyé car, dans un shell lancé par une
// app Electron (VS Code, Cursor…), ELECTRON_RUN_AS_NODE=1 est hérité par les
// process node enfants, et sous ce flag install.js avale le téléchargement
// (sortie 0, aucun zip mis en cache).
function runOfficialInstaller() {
  const installer = path.join(ELECTRON_DIR, "install.js");
  if (!fs.existsSync(installer)) return;
  const env = { ...process.env };
  delete env.ELECTRON_RUN_AS_NODE;
  try {
    execFileSync(process.execPath, [installer], { stdio: "inherit", env });
  } catch {
    // install.js n'échoue pas en sortie non nulle : on vérifie via isInstalled().
  }
}

function findCachedZip(version) {
  const dir = cacheDir();
  if (!fs.existsSync(dir)) return null;
  const zipName = `electron-v${version}-${process.platform}-${process.arch}.zip`;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const candidate = path.join(dir, entry.name, zipName);
    if (fs.existsSync(candidate)) return candidate;
  }
  return null;
}

async function main() {
  if (isInstalled()) return;

  if (!fs.existsSync(ELECTRON_DIR)) {
    console.error(
      "[ensure-electron] Package 'electron' absent — lancer 'npm install' d'abord."
    );
    process.exit(1);
  }

  // 1) Lancer le téléchargement officiel.
  runOfficialInstaller();
  if (isInstalled()) {
    console.log("[ensure-electron] Binaire téléchargé par l'installeur officiel.");
    return;
  }

  const { version } = JSON.parse(
    fs.readFileSync(path.join(ELECTRON_DIR, "package.json"), "utf8")
  );

  // 2) Sinon, restaurer depuis le cache local (extraction échouée).
  const zipPath = findCachedZip(version);
  if (!zipPath) {
    console.error(
      "[ensure-electron] Binaire Electron absent et zip introuvable dans le cache."
    );
    console.error(`[ensure-electron] Cache attendu : ${cacheDir()}`);
    console.error(
      `[ensure-electron] Fichier recherché : electron-v${version}-${process.platform}-${process.arch}.zip`
    );
    console.error(
      "[ensure-electron] Cause probable : téléchargement bloqué (réseau, proxy, antivirus)."
    );
    console.error(
      "[ensure-electron] Action : vérifier la connexion, exclure node_modules et le cache de l'antivirus, puis réinstaller."
    );
    process.exit(1);
  }

  // Electron >= 42 a renommé l'extracteur embarqué (extract-zip ->
  // @electron-internal/extract-zip). On essaie les deux noms.
  let extract;
  for (const name of ["@electron-internal/extract-zip", "extract-zip"]) {
    try {
      const mod = require(name);
      extract = mod.default || mod;
      break;
    } catch {
      /* try next name */
    }
  }
  if (!extract) {
    console.error(
      "[ensure-electron] Module d'extraction introuvable (@electron-internal/extract-zip ou extract-zip) — dépendance transitive d'electron manquante."
    );
    process.exit(1);
  }

  fs.mkdirSync(DIST_DIR, { recursive: true });
  try {
    await extract(zipPath, { dir: DIST_DIR });
  } catch (err) {
    console.error(`[ensure-electron] Extraction échouée : ${err.message}`);
    console.error(
      "[ensure-electron] Cause probable : antivirus bloque l'écriture du binaire par node.exe."
    );
    console.error(
      "[ensure-electron] Action : exclure node_modules et le cache Electron de l'antivirus, puis relancer 'npm run postinstall'."
    );
    process.exit(1);
  }

  fs.writeFileSync(PATH_TXT, relExecPath(), { encoding: "ascii" });
  console.log(`[ensure-electron] Binaire restauré depuis : ${zipPath}`);
}

main().catch((err) => {
  console.error(`[ensure-electron] Erreur inattendue : ${err.message}`);
  process.exit(1);
});
