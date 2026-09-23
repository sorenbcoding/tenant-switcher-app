# Orchestrator Connector

A small Windows utility for quickly switching which UiPath Orchestrator tenant your local UiPath Robot/Assistant is connected to. Save a preset per tenant (name, Orchestrator URL, Machine Client ID, Client Secret) and switch with one click, instead of reconfiguring the Robot by hand every time.

## Features

- **Tenant presets** — save any number of Orchestrator connections, each with a custom name, Orchestrator URL, and Machine Client ID/Secret.
- **One-click switch** — Connect disconnects the current tenant, connects to the selected preset, restarts the UiPath Robot service, and relaunches UiPath Assistant.
- **Secrets stay out of plaintext files** — Client Secrets are stored in Windows Credential Manager, not in the presets file.
- **No install footprint beyond .NET** — presets live under `%AppData%\OrchestratorConnector`; no database, no extra services.

## Requirements

- Windows with UiPath Studio or Robot installed (the app shells out to `UiRobot.exe` and restarts the `UiPath Robot` Windows service).
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) if running the framework-dependent build, or none if running a self-contained/single-file publish.
- Permission to restart the `UiPath Robot` Windows service (may require running as administrator, depending on your machine's configuration).

## Usage

1. Fill in **Preset Name**, **Orchestrator URL**, **Client / Machine ID**, and **Client Secret**, then click **Add / Save**.
   - Saving with a name that matches an existing preset overwrites that preset instead of creating a duplicate.
2. Select a preset from the list, click **Edit** to load it back into the form (the Client Secret field is left blank — leave it blank to keep the stored secret, or type a new one to replace it).
3. Select a preset and click **Delete** to remove it (this also removes its stored secret from Credential Manager).
4. Select a preset and click **Connect** to switch: the app closes Assistant, disconnects the current tenant, connects to the selected one, restarts the Robot service, and reopens Assistant. Progress is shown in the status bar at the bottom.

## Where data is stored

- Preset metadata (name, URL, Client ID) — `%AppData%\OrchestratorConnector\presets.json`
- Client Secrets — Windows Credential Manager, one entry per preset (`OrchestratorConnector:{preset-id}`)

If you're upgrading from a pre-1.0 version that stored everything (including the secret) in a single `presets.json` next to the executable, that file is migrated automatically the first time you run the new version: presets move to `%AppData%`, secrets move into Credential Manager, and the old plaintext file is deleted once the migration is verified.

## Building from source

```
dotnet build OrchestratorConnector.sln
```

## Publishing a single-file executable

```
dotnet publish OrchestratorConnector\OrchestratorConnector.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## Project structure

```
OrchestratorConnector/
  App.xaml(.cs)              Composition root / entry point
  MainWindow.xaml(.cs)       Main window
  Assets/                    App icon
  Themes/                    UI styling
  Models/                    Preset data model
  Services/                  Persistence, Credential Manager access, UiPath process orchestration, migration
  ViewModels/                MVVM view models and commands
  Behaviors/                 PasswordBox data-binding support
```

## License

Copyright © Søren Schytz Birk.
