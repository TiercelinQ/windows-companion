# Window Companion

Utilitaire desktop Windows de maintenance et de diagnostic. Une fenêtre, quatre onglets : mise à jour des logiciels via winget (avec vérification préalable des mises à jour disponibles), scan matériel, scan système, scan réseau. Les scans s'affichent en tables et s'exportent en Markdown.

## Stack

- Electron (≥ 42) + electron-vite
- React 19 (composants fonctionnels + hooks), TypeScript strict
- Architecture MVC : `main` = models, `renderer` = views, IPC = controllers
- CSS centralisé (`tokens.css` + `styles.css`), thème clair/sombre, flat design
- Font Awesome Free (local, zéro CDN)
- Commandes système : `winget`, PowerShell, via `node:child_process`
- Sans base de données, sans i18next (strings FR centralisées dans `i18n/fr.json`)

## Arborescence

```
src/
├── shared/            config (commandes figées), types, ipc-channels
├── main/
│   ├── index.ts       entrée, BrowserWindow sécurisée, single-instance lock
│   ├── report-export.ts   export .md (showSaveDialog + écriture)
│   ├── models/        command-runner, preferences, update, hardware, system, network
│   └── controllers/   un controller par entité + index
├── preload/           contextBridge "api"
└── renderer/
    ├── index.html     CSP stricte
    └── src/
        ├── App.tsx, main.tsx
        ├── views/     UpdateView, HardwareView, SystemView, NetworkView, layout/, ToastManager, ErrorBoundary
        ├── hooks/     useTheme, useToast
        ├── utils/     helpers (formatage pur)
        ├── i18n/      index.ts (t local), fr.json
        └── styles/    tokens.css, styles.css
```

## Canaux IPC

| Canal | Sens | Rôle |
| --- | --- | --- |
| `pref:get` / `pref:set` | invoke | Préférences (thème, fenêtre) |
| `update:start` / `update:cancel` | invoke | Lancer / annuler la mise à jour winget |
| `update:check` | invoke | Vérifier les mises à jour disponibles (winget lecture seule) |
| `update:data` / `update:end` | événement main → renderer | Flux de sortie en direct + fin (mise à jour et vérification) |
| `hardware:scan` / `hardware:export` | invoke | Scan matériel + export `.md` |
| `system:scan` / `system:export` | invoke | Scan système + export `.md` |
| `network:scan` / `network:export` | invoke | Scan réseau + export `.md` |

## Conventions

- Zéro valeur visuelle en dur dans le TS/TSX : tout dans `tokens.css` / `styles.css`.
- Commandes système figées dans `src/shared/config.ts`, exécutées côté main uniquement, jamais construites depuis le renderer.
- Erreurs métier remontées en `IpcResult<T>` et affichées en toasts (jamais `alert`/`confirm`).
- Sécurité Electron verrouillée : `contextIsolation`, `sandbox`, `nodeIntegration: false`, CSP stricte.

## Installation

```bash
npm install
npm run dev        # développement
npm run typecheck  # vérification TypeScript
npm run lint       # ESLint
npm run build      # build sans packaging
```

> Si `npm run dev` signale `Error: Electron uninstall`, lancer `npm run postinstall` (le script `scripts/ensure-electron.cjs` restaure le binaire depuis le cache).

## Packaging Windows (.exe)

```bash
npm run dist
```

L'icône `app_icon/app_icon.ico` est déjà intégrée (référencée par `electron-builder.yml` et l'option `icon` de la fenêtre). `npm run dist` produit dans `release\` l'installeur NSIS (`Window Companion Setup x.y.z.exe`, installation par utilisateur, sans droits admin) et la version portable (`Window Companion x.y.z.exe`, sans installation).

Les binaires ne sont pas signés : au premier lancement chez un autre utilisateur, SmartScreen affiche un avertissement (« Informations complémentaires » → « Exécuter quand même »). Pour le supprimer, configurer un certificat de signature de code dans `electron-builder.yml` (ou via `CSC_LINK` / `CSC_KEY_PASSWORD`).

## Données

`preferences.json` (thème, taille/position de la fenêtre) est stocké dans `app.getPath("userData")`, jamais dans le dossier d'installation.

## Notes

- `.claude/settings.json` ajoute un hook `Stop` qui lance `npm run lint` à chaque fin de tour en session de maintenance. À ajuster ou retirer selon le besoin.
- IP locale uniquement (sortie `Get-NetIPConfiguration`), aucun appel réseau externe.
