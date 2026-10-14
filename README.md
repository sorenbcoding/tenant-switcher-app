<p align="center">
  <img src="branding/tenant-switcher-app-light-128.png#gh-light-mode-only" width="96" alt="Tenant Switcher for UiPath logo">
  <img src="branding/tenant-switcher-app-dark-128.png#gh-dark-mode-only" width="96" alt="Tenant Switcher for UiPath logo">
</p>

<h1 align="center">Tenant Switcher for UiPath</h1>
<p align="center">By Søren Schytz Birk</p>

Tenant Switcher for UiPath is a Windows app that switches the local UiPath Robot and Assistant between Orchestrator tenants in one step. It uses saved presets, each with an Orchestrator URL, a machine client ID and a client secret. It was formerly called *Orchestrator Connector*.

The Studio extension [Tenant Switcher for UiPath Studio](https://github.com/sorenbcoding/tenant-switcher-studio) does the same from inside UiPath Studio. The app and the extension share their presets.

## Install

- Windows 10 or 11 with UiPath Studio, Robot or Assistant installed. This includes the per-user *UiPath Platform* install.
- The [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
- Download `Tenant Switcher.exe` from [Releases](https://github.com/sorenbcoding/tenant-switcher-app/releases), or [build it from source](#building-and-publishing). It is a single file and needs no installer.
- You also need a machine in Orchestrator that uses **client ID / client secret** credentials, either a machine template or a standard machine.

**Upgrading from *Orchestrator Connector*:** your presets and secrets are kept.

## Quick example

1. Start `Tenant Switcher.exe` and select **Add preset**.
2. Fill in the form:
   - Name: `Acme production`
   - URL: `https://cloud.uipath.com/acme/Production/orchestrator_`
   - The machine's client ID and client secret.
3. Select **Test** to check the credentials, then **Save**.
4. Select **Connect** on the `Acme production` card, then **Switch**.
5. The banner shows each step, then `Connected to 'Acme production'.` The card shows **Connected**.

## Reference

### Window
Each preset has its own card in the window:

| Action | Where |
|---|---|
| Add a preset | **Add preset** opens a form with all fields |
| Connect | **Connect** on a preset card |
| Edit | the pencil icon on a card. Leave the client secret blank to keep the stored one. |
| Test credentials | **Test** in the form. It requests a token with the values entered, without touching the Robot. |
| Delete | the bin icon on a card. The stored secret is removed too. |
| Settings, About | the icons in the top-right corner |

**Confirmations** appear on the preset card. For example, *Do you want to switch tenant to Staging?* comes with **Switch** and **Cancel**.

**Switch results** appear as a banner at the top of the window, with **Show log** and **OK**. A result stays until you select **OK**, including across restarts.

**The connected preset** has an accent border and a green **Connected** label. Hover over a card to see its full URL and client ID.

The connected preset comes from the Robot itself, so it is correct whichever tool made the switch. It updates live when the Robot's connection changes, for example after a switch in the Studio extension. If the Robot is connected to a tenant without a preset, a note under **Add preset** says so.

**Theme:** the window follows the Windows light or dark app theme. You can override this in Settings.

### What a switch does
1. **Checks the credentials.** The app requests a `client_credentials` token from the tenant's Identity Server. If the credentials are rejected, nothing changes. If Identity Server cannot be reached, the card offers **Switch anyway**.
2. **Disconnects.** Closes UiPath Assistant and runs `UiRobot disconnect`.
3. **Connects.** Runs `UiRobot connect --url … --clientId … --clientSecret …` and checks the exit code. If the connect fails, the previously connected preset is reconnected.
4. **Restarts the `UiPath Robot` Windows service**, but only on service-mode installs that have one. Without administrator rights, Windows asks for elevation. Per-user installs skip this step.
5. **Starts UiPath Assistant again.**
6. **Signs in the uip CLI (optional, per preset).** Runs `uip login` with the same client credentials so the UiPath CLI targets the same tenant.

Each step is written to `switch.log`. **Show log** opens it.

### Settings
| Setting | Default | Description |
|---|---|---|
| Theme | Use the Windows setting | Light, dark, or follow Windows. |
| Ask before switching | on | Shows a confirmation on the card before the Robot is reconnected. |
| Check the credentials before switching | on | Tests the credentials against Identity Server before disconnecting. |
| Restart UiPath Assistant | on | Closes Assistant before the switch and starts it afterwards. |
| UiRobot.exe path | *(empty)* | Full path to `UiRobot.exe`. When empty, the app looks for the per-user UiPath Platform install first, then Program Files. |
| uip CLI path | `uip` | The `uip` command or its full path. |

### Storage
| Data | Location |
|---|---|
| Presets (name, URL, client ID) | `%AppData%\OrchestratorConnector\presets.json` |
| Client secrets | Windows Credential Manager, target `OrchestratorConnector:<preset id>` |
| Settings, connected preset, last result, uip sync option per preset | `%LocalAppData%\SorenB\TenantSwitcher\settings.json` and `state.json` |
| Switch log | `%LocalAppData%\SorenB\TenantSwitcher\switch.log` |
| Connected tenant (read only) | the Robot's `UiPath.settings`, key `UiPathServerUrl`: `%AppData%\UiPath` (user mode) or `%ProgramData%\UiPath` (service mode) |

The preset and secret locations keep the `OrchestratorConnector` name on purpose. They are shared with the Studio extension, so a preset created in one tool appears in the other. The list reloads when the file changes.

Early versions stored everything, including the secret, in a `presets.json` next to the executable. That file is migrated automatically on first start: presets move to `%AppData%` and secrets move to Credential Manager. The old file is deleted once the migration is verified.

### Security
- Client secrets are stored only in Windows Credential Manager.
- Secrets are masked as `********` in the log and in step details.
- The uip CLI receives the secret through an environment variable.
- **Exception:** `UiRobot.exe` accepts the client secret only as a command-line argument. While `UiRobot connect` runs, any local process that can read other processes' command lines can see it.

### Building and publishing
```powershell
dotnet build SorenB.TenantSwitcher.sln
dotnet test SorenB.TenantSwitcher.sln
dotnet publish SorenB.TenantSwitcher\SorenB.TenantSwitcher.csproj -c Release -o publish
```

`publish\Tenant Switcher.exe` is the only file to distribute; you can delete the `.pdb`. The publish step gives the exe this name; the assembly itself is `SorenB.TenantSwitcher`, so Debug builds in `bin\` keep that name.

The exe in `bin\Release\…` is not the same file. It is the build-time apphost and needs the `.dll` and `.json` files next to it.

For a build that runs without the .NET runtime installed (about 150 MB), add `--self-contained true -p:IncludeNativeLibrariesForSelfExtract=true` to the publish command.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## License

MIT. See [LICENSE](LICENSE).

---

By Søren Schytz Birk · UiPath Community MVP · [github.com/sorenbcoding](https://github.com/sorenbcoding)

Tenant Switcher for UiPath is an independent open-source project by Søren Schytz Birk (github.com/sorenbcoding) and is not affiliated with, endorsed by, or sponsored by UiPath. UiPath and related names are trademarks of UiPath Inc.
