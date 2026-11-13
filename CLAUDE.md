# Windows Companion

## Origin

Framework: dotnet-wpf v1.2.0

## Business context

Application de bureau Windows 11 de maintenance d'un PC :

- recherche, installation et annulation des mises à jour des applications (winget, Microsoft Store), avec file d'installation, journal winget, historique et liste d'exclusions ;
- fiche du matériel, du système et du réseau, à copier ou exporter (HTML, CSV de l'historique) ;
- actions explicites limitées : activer un adaptateur réseau, vider le cache DNS, relancer l'application en administrateur.

Stockage JSON sous `%APPDATA%\WindowsCompanion`, français seul, accent système Windows.

## Deviations from the framework

Déclarées et validées en phase 4 ; détail dans `docs/specs/04-architect.md` section 8 (privé, non versionné).

- Toasts : hôte propre (`ToastHost`) à la place de `ISnackbarService` - reason: actions (Réessayer, Annuler, Copier le détail), empilement et fusion que le snackbar WPF UI n'offre pas (D-01)
- Relance en administrateur : `UseShellExecute = true` avec `runas` sur `Environment.ProcessPath` - reason: seul moyen d'obtenir l'invite UAC (D-02)
- ViewModels de page en singleton - reason: la file d'installation et les instantanés survivent à la navigation (D-03)
- Dimensions et typographie du cahier des charges (barre de titre 40, volet 244, fenêtre minimale 800 x 600) - reason: arbitrage de la phase 1 (D-04)
- Pas de `Tokens.Light.xaml` / `Tokens.Dark.xaml` - reason: aucun brush ne diffère entre les thèmes hors Fluent (D-05)
- 14 fichiers de journal conservés au lieu de 7 (D-06)
- `ClipboardService` et `AppLifetimeService` utilisent des types WPF derrière une interface (D-07)
- Styles du rapport HTML dans `Resources/report.css` (D-08)
- Navigation depuis les ViewModels par `IPageNavigator` (D-09)
- Fichiers supplémentaires par entité : `Services/Winget/*`, `Services/Platform/*`, `UpdateItemViewModel`, `ToastViewModel` (D-10)
- Bouton de volet dans la barre de titre, thème réglé seulement dans Paramètres (D-11)
- Pipeline GitHub Actions : `.github/workflows/ci.yml` (restore, build, format, test, audit des paquets) et `release.yml` (push sur `main` : publish, exe et ZIP, release GitHub si le tag de la version n'existe pas) - reason: le contrat ne prévoyait aucun fichier d'intégration continue (D-12)

- Vérification de nouvelle version : appel sortant vers `api.github.com` (dernière release du dépôt), absent du contrat initial - reason: informer l'utilisateur d'une version plus récente ; aucun paquet ajouté, `User-Agent` générique, page des releases ouverte par une URL constante via `IProcessRunner` (D-13)

## Open points

Suivi dans `docs/errors.md` (privé) : E-02 (hors application) et la liste des points de recette non exercés. E-01 (progression d'installation) et E-03 (versions tronquées) sont corrigés le 2026-11-12.

## Maintenance

- Load the project first: `/wpf-load-project`
- Change it: `/wpf-add-feature` · `/wpf-fix-issue` · `/wpf-refactor-code` (each records the change under `[Unreleased]` in `docs/release/CHANGELOG.md`; the version does not move)
- Verify: `/wpf-run-tests`
- Publish a version: `/wpf-release` (turns the accumulated `[Unreleased]` changelog into a dated version and raises the version number)
