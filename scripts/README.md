# Repository scripts

Start with these two commands, from the repository folder:

| Script | Purpose |
| --- | --- |
| `Reinstall.ps1` | Build the current source in Debug and reinstall it with the visible MSI. |
| `Publish.ps1` | Create the portable package for CI or an explicitly approved packaging run. |

For a local build:

```powershell
pwsh -NoProfile -File ./scripts/Reinstall.ps1 -Architecture x64
```

The supporting scripts are grouped by job:

| Folder | Contents |
| --- | --- |
| `build/` | MSI, setup actions, native Photino bridge and bundled Python runtime. |
| `install/` | Install and uninstall scripts copied into portable packages. |
| `maintenance/` | Generated-output cleanup and first-import scan evidence. |
| `tools/` | Product icon generation and theme review images. |

The portable package still contains `Install-ThermalrightVibeWare.ps1` and
`Uninstall-ThermalrightVibeWare.ps1` beside the app. Run its install script from
that extracted folder, not from this source tree.

Builds, tests and hardware commands follow the owner's approval rules in
[AGENTS.md](../AGENTS.md). See the [build guide](../docs/build.md) for dependencies
and first-import preparation. Merely opening this folder runs nothing.
