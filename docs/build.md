# Build Thermalright VibeWare

I use the .NET 10 SDK and Python with pip on the packaging PC.

Only `Reinstall.ps1` and `Publish.ps1` sit at the top of `scripts`. The
[script map](../scripts/README.md) lists the build, install, maintenance and tools folders.

Windows builds also need PowerShell 7, Visual Studio's C++ v143 tools for the target architecture, and the Windows SDK. No tool installer runs as part of the build.

`scripts/build/Build-PhotinoNative.ps1` builds the Windows native bridge with owned UTF strings. It checks the pinned Apache-2.0 source archive and native SDK package hashes, then caches its output under `artifacts/native/photino-memory/`. The first build downloads those sources and SDK packages. Later builds reuse the checked DLL when its patch and hash match.

From the repository folder:

For a local reinstall, use this command. It uninstalls registered Thermalright VibeWare
packages, clears `artifacts`, `bin`, `obj` and generated web files, increments
`installer/Version.txt`, builds a fresh Debug MSI and opens its normal installer UI.
After installation it compares every installed payload file with the new build
and removes build output, keeping only the final MSI. Settings and personal
themes stay in their user folders. Cancellation or any failed step stops it.

```powershell
pwsh -NoProfile -File ./scripts/Reinstall.ps1 -Architecture x64
```

Prepare new external imports before building: restore the NuGet packages into
`artifacts/nuget-stage`, then run `scripts/maintenance/Assert-DefenderClean.ps1 -ScanNewImport` on that
staging folder. Native and Python dependencies have separate `-PrepareImports`
steps. Each new archive and its extracted inputs need a completed targeted scan.
Unchanged hashes reuse evidence saved outside build output. Builds check this
evidence and never start scans. A detection or unavailable scanner stops the import.
The checked-in dependency scan manifest contains the same recorded hashes with
local paths removed, so clean CI runners can reuse the existing evidence.
No tests or USB commands run during reinstall.

The sensor library is compiled from the pinned MPL-2.0 source in
`third_party/LibreHardwareMonitor`. The portable ZIP and MSI include that exact
source under `sources/LibreHardwareMonitor`; no local checkout outside this
repository or previously built sensor DLL is needed.

CI packaging and an owner-approved exception to the local reinstall rule may call
the publishing script directly:

```powershell
./scripts/Publish.ps1 -Architecture x64
```

Do not use that command for an ordinary local compilation. Every local compilation
uses the Debug reinstall flow above unless the owner explicitly says otherwise.

The ZIP is written to `artifacts/packages/ThermalrightVibeWare-win-x64-preview.zip`. Each
build first uses a unique ignored staging folder, so packaging does not overwrite
or stop a portable copy that is currently running. Use `-Architecture ARM64` for
that architecture; my local package check covered x64.

Packaging downloads an isolated Python runtime and the pinned theme dependencies. It does not change the system Python installation. ZIP users do not need Python or .NET installed separately.

The CLI output contains its own `themes`, `theme-runtime` and `plugins` folders, in local builds too. The engine never searches parent folders for bundled code. In a ZIP these folders live under `engine/`.

Tests and hardware trials need the owner's approval before each run. Read [AGENTS.md](../AGENTS.md).
