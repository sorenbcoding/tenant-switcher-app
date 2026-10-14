# Tenant Switcher for UiPath

Brand: sorenb-brand
Brand category: app (slate accent, ID `SorenB.TenantSwitcher`)

WPF desktop app (.NET 8, `net8.0-windows`) for switching the local UiPath Robot and Assistant between Orchestrator tenants. Formerly *Orchestrator Connector*. Its sibling is the Studio extension *Tenant Switcher for UiPath Studio* (github.com/sorenbcoding/tenant-switcher-studio); use it as the reference for layout, flows and wording.

## Layout
- `SorenB.TenantSwitcher/`: the app. MVVM: `ViewModels/`, `Services/`, `Models/`, `Themes/`.
- `Themes/`: `SorenB.Theme.*.xaml` are copied unchanged from the sorenb-brand skill tokens; `Semantic.*.xaml` map them to `App.*` keys; `Controls.xaml` holds the styles. Reference `App.*` keys via `DynamicResource`, never raw hex.
- `tests/SorenB.TenantSwitcher.Tests/`: xUnit.
- `branding/`: product logo (generated with the sorenb-brand `make_logo.py`; glyph in `branding/glyphs/`).

## Constraints
- **Do not rename the shared store.** Keep `%AppData%\OrchestratorConnector\presets.json` (bare array, PascalCase keys), `preset-meta.json` beside it, and the Credential Manager target `OrchestratorConnector:<guid>`. They are shared with the Studio extension.
- App-only state (settings, last switch, log) lives in `%LocalAppData%\SorenB\TenantSwitcher\`.
- The connected tenant comes from the Robot's `UiPath.settings` (`UiPathServerUrl`; `%AppData%\UiPath`, or `%ProgramData%\UiPath` in service mode), watched live in `Services/RobotConnection.cs`. The extension reads the same file. Never write to it.
- Flat UI: no shadows, no gradients, no orange. Accent is `App.Accent` (slate) only.

## Commands
`dotnet build SorenB.TenantSwitcher.sln`, `dotnet test`, `dotnet publish SorenB.TenantSwitcher -c Release -o publish` (produces `publish\Tenant Switcher.exe`). Brand check: `python ~/.claude/skills/sorenb-brand/scripts/brand_check.py . --category app`.
