# Changelog

All notable changes to this project are documented in this file.
The format is based on Keep a Changelog, and this project adheres to Semantic Versioning.

## [Unreleased]

## [1.1.1] - 2026-11-13
### Changed
- Settings page: the blocks are laid out in two columns instead of one.
- Pinned to the Start menu or the taskbar, the application now shows as "Windows Companion" (the executable file description was "WindowsCompanion"); the file itself is still `WindowsCompanion.exe`.
- GitHub release: the executable is attached as `WindowsCompanion.exe` (no version in its name); the ZIP keeps the `WindowsCompanion-<version>-win-x64.zip` name and holds that executable.

## [1.1.0] - 2026-11-13
### Added
- New-version check: at startup (once a day at most) and from a "Rechercher maintenant" button in Settings, the application compares its version with the latest GitHub release and, when a newer one exists, shows a toast with a "Télécharger" action that opens the releases page. A Settings switch turns the automatic check off.
- The title bar shows the application version (for example "v1.1.0") to the right of the application name.

## [1.0.2] - 2026-11-12
### Changed
- Added GitHub Actions workflows: a CI that restores, builds, checks the format, runs the tests and audits the packages on every pull request and push to `main`, and a release workflow that, after each merge into `main`, builds the executable and the ZIP and publishes them as a new GitHub release when the version has no tag yet.

## [1.0.1] - 2026-11-12
### Changed
- Updates page: the winget log now sits left of the table (under it on a narrow page) and both scroll inside their block.
- Updates table: the always-empty Size column is removed and the row buttons move to a new Action column, where the cancel button reads "Annuler" instead of a cross. Column names, State and Action always stay readable; application names and versions are truncated first.
- Minimum window width raised from 760 to 800.

### Fixed
- Installation progress no longer sticks at "Download 0 %": a yellow "En cours" badge is shown until the application is installed, and the batch bar advances one step per finished application.

## [1.0.0] - 2026-11-08
### Added
- Initial release.
