# window-companion

## Origine

Framework : electron v1.0.0

## Contexte métier

Utilitaire desktop Windows de maintenance et de diagnostic, mono-fenêtre, 4 onglets :

- **Mise à jour** : met à jour tous les logiciels via `winget upgrade --all …`, sortie affichée en direct, annulable.
- **Matériel** : scan des spécifications matérielles (CPU, RAM, GPU, disques, carte mère, BIOS).
- **Système** : scan des informations système (OS, version, build, machine, démarrage).
- **Réseau** : scan de la configuration réseau par adaptateur.

Les 3 scans s'affichent en tables et s'exportent en Markdown. Aucune base de données. Thème clair/sombre.

## Écarts par rapport au framework

- Onglets alignés à gauche après le logo — raison : choix utilisateur (Phase 3), vs onglets centrés par défaut (`layout.md §3`).
- Composant `.terminal-output` + token `--font-family-mono` — raison : affichage du flux winget en direct (style terminal), non prévu par `layout.md §8`.
- Durées des toasts à 10s (success/info/warning) — raison : choix utilisateur (Phase 3), vs 4/4/6s (`layout.md §5`). `danger` reste persistant.
- IPC événementiel `update:data` / `update:end` (`webContents.send`) + abonnements `onUpdateData` / `onUpdateEnd` au preload — raison : streaming temps réel de la sortie winget, vs « 1 méthode = 1 invoke ». Aucune surface `ipcRenderer` brute exposée.
- Fichier `src/main/report-export.ts` (helper export `.md` partagé par 3 contrôleurs) — raison : mutualisation `showSaveDialog` + écriture, le formatage Markdown restant dans chaque model.
- Fichier `src/main/models/command-runner.ts` (wrapper `child_process` partagé) — raison : exécution mutualisée des commandes système, Node pur sans API UI.
- `src/renderer/src/views/ErrorBoundary.tsx` en class component — raison : React n'offre pas d'Error Boundary fonctionnel.
- i18n désactivée mais `i18n/index.ts` + `fr.json` conservés avec un `t()` local léger (sans `i18next`) — raison : strings centralisées FR pour un toggle FR/EN futur à coût nul.
- Bouton « Vérifier les mises à jour » (onglet Mise à jour) : commande `winget` lecture seule (sans `--all --force`), sortie brute affichée dans `.terminal-output`, présence parsée par structure. Réutilise les événements `update:data` / `update:end` (`UpdateEndResult.kind` = `update` | `check`, `hasUpdates`) ; seul nouveau canal `update:check` (invoke) — raison : afficher la liste des logiciels à mettre à jour sans installer, sans dépendance supplémentaire.
- Rendu terminal de `UpdateView` : les frames transitoires de winget en mode non-TTY (spinner `- \ | /`, barre `█▒`) réécrivent une unique ligne active (`.terminal-line-active`) au lieu de s'empiler ; lignes vides ignorées, lignes persistantes journalisées — raison : winget émet chaque frame sur une ligne distincte (pas de `\r`), ce qui floodait l'affichage. Logique d'affichage pure côté vue.
