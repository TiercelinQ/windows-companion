# Changelog

All notable changes to this project are documented in this file.
The format is based on Keep a Changelog, and this project adheres to Semantic Versioning.

## [Unreleased]

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
