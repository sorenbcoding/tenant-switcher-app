# Changelog

All notable changes to Tenant Switcher for UiPath are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed
- When connecting failed with an error instead of an exit code, for example because UiRobot couldn't be started, the switch skipped the rollback and left the Robot disconnected and Assistant closed. Such errors now roll back to the previous tenant like a failed connect. A missing client secret is now found before the Robot is disconnected. ([#16](https://github.com/sorenbcoding/tenant-switcher-app/issues/16))
- The credential check accepted any successful response, so a proxy or sign-in page could pass as valid credentials. It now requires an access token and doesn't follow redirects. Otherwise the card offers **Switch anyway**, as for an unreachable Identity Server. ([#17](https://github.com/sorenbcoding/tenant-switcher-app/issues/17))
- Switching while a UiPath process was running on the Robot failed with "Orchestrator already connected!" and wrongly blamed the client ID and secret. UiRobot doesn't disconnect while a process runs. The switch now stops there, leaves the Robot on its current tenant, and says to wait for the process or stop it in Assistant. ([#21](https://github.com/sorenbcoding/tenant-switcher-app/issues/21))

## [0.4.1] - 2026-11-06

### Fixed
- Windows Explorer could stop responding after a switch when UiPath Assistant's *Show in Windows Start Menu* is on, because Assistant then rewrites all its Start menu shortcuts. A switch now warns about the setting first and offers **Turn off and switch**, which turns it off while Assistant is closed. A new setting, **Warn about Assistant's Start menu shortcuts**, turns the warning off. ([#11](https://github.com/sorenbcoding/tenant-switcher-app/issues/11))

## [0.4.0] - 2026-10-14

### Changed
- Renamed from *Orchestrator Connector* to *Tenant Switcher for UiPath*.
  - The executable is now `Tenant Switcher.exe`, matching the name the Studio extension shows in the activity bar. The assembly ID is `SorenB.TenantSwitcher`.
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
- If the connect fails, the tenant the Robot was connected to before is reconnected.
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
- The **Connected** preset is read from the Robot's own `UiPath.settings` (`UiPathServerUrl`), so it is correct after a switch in the Studio extension, and it updates live.
- A note under **Add preset** when the Robot is connected to a tenant that matches no preset.
- Unit tests for URL handling, validation, the identity check, the stores and the locator.

### Fixed
- A failed `UiRobot connect` was reported as `Done!`. The exit code is now checked.
- Renaming a preset in the form created a second preset instead of renaming it.
- Delete removed a preset without confirmation.
- The Debug solution configuration built the Release project configuration.
