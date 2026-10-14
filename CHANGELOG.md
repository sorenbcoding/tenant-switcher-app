# Changelog

All notable changes to Tenant Switcher for UiPath are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.4.0] - 2026-10-14

### Changed
- Renamed from *Orchestrator Connector* to *Tenant Switcher for UiPath*.
  - The executable is now `SorenB.TenantSwitcher.exe`.
  - Presets and secrets are kept: the shared `%AppData%\OrchestratorConnector` folder and Credential Manager targets are unchanged.
- New window, with the same layout as the Studio extension:
  - A single column with an **Add preset** button and an inline form.
  - One card per preset, with **Connect**, edit and delete actions and a **Connected** label.
- Confirmations (switch, delete, unverified credentials) appear on the preset card.
- Switch progress and results appear as banners. Results have **Show log** and **OK** and stay until dismissed, also across restarts.
- New SorenB look:
  - slate accent and diamond logo
  - light and dark themes that follow Windows, with a dark title bar
  - no shadows
- `UiPathPlatform\<version>` folders are compared numerically, so `26.0.203-cloud.25204` is preferred over `26.0.203-cloud.9`.

### Added
- **Test** in the form requests a token from Identity Server without touching the Robot.
- The credentials are checked before a switch. Rejected credentials stop the switch before the Robot is disconnected.
- If the connect fails, the previously connected preset is reconnected.
- Form validation with a message per field. The app now rejects a missing secret, an incomplete Automation Cloud URL and a duplicate name.
- Optional uip CLI sign-in per preset (**Also sign in the uip CLI to this tenant**).
- **Settings:**
  - theme
  - ask before switching
  - check the credentials before switching
  - restart UiPath Assistant
  - UiRobot.exe path
  - uip CLI path
- **About** screen with version, repository link and disclaimer.
- Switch log in `%LocalAppData%\SorenB\TenantSwitcher\switch.log`, with secrets masked.
- The preset list reloads when `presets.json` changes, for example after an edit in the Studio extension.
- Unit tests for URL handling, validation, the identity check, the stores and the locator.

### Fixed
- A failed `UiRobot connect` was reported as `Done!`. The exit code is now checked.
- Renaming a preset in the form created a second preset instead of renaming it.
- Delete removed a preset without confirmation.
- The Debug solution configuration built the Release project configuration.
