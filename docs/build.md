# Build Thermalright VibeWare

I use the .NET 10 SDK and Python with pip on the packaging PC.

Use `scripts/vibeware.ps1` as the console; Linux can use `scripts/vibeware` to prepare PowerShell too. The
[script map](../scripts/README.md) lists the build, install, maintenance and tools folders.

Windows builds also need PowerShell 7, Visual Studio's C++ v143 tools for the target architecture, and the Windows SDK. Run `scripts/vibeware.ps1 preflight` to install missing build prerequisites.

`scripts/build/Build-PhotinoNative.ps1` builds the Windows native bridge with owned UTF strings. Preparation checks the pinned Apache-2.0 source archive and SDK packages. The build patches a working copy of those sources and writes the DLL under `artifacts/native/photino-memory/` for MSBuild to copy.

From the repository folder:

For a local Windows reinstall, run this command from an elevated PowerShell 7
session. It clears generated output, prepares the imports and builds a fresh Debug
MSI. After the MSI is ready, it removes the previous version and opens the normal
installer. It keeps the final MSI after a successful install. Settings and personal
themes stay in their user folders.

```powershell
pwsh -NoProfile -File ./scripts/vibeware.ps1 build -Architecture x64 -PrepareImports
```

`-PrepareImports` explicitly stages NuGet, the native bridge inputs, Python and
WiX before the build begins. Defender must complete a targeted scan of the new
archives and extracted inputs without detections. The result stays in memory in
that PowerShell session. Later phases check the same content and use that result.
An unavailable scanner, detection or failed scan stops preparation.

For repeated local builds, open the console with `pwsh -NoProfile -File
./scripts/vibeware.ps1 -PrepareImports` and use **Build package** in that session.
The console reuses prepared dependencies until it closes. Local builds publish the
Debug app and MSI directly. CI checks formatting, builds the solution and runs the
offline checks before publishing the distribution packages.

The sensor library is compiled from the pinned MPL-2.0 source in
`third_party/LibreHardwareMonitor`. The portable ZIP and MSI include that exact
source under `sources/LibreHardwareMonitor`; no local checkout outside this
repository or previously built sensor DLL is needed.

CI packaging and an owner-approved exception to the local reinstall rule may call
the publishing script directly:

```powershell
./scripts/vibeware.ps1 package -Architecture x64 -PrepareImports
```

On Windows, do not use that command for an ordinary local compilation. Every local Windows compilation
uses the Debug reinstall flow above unless the owner explicitly says otherwise.

The ZIP is written to `artifacts/packages/ThermalrightVibeWare-win-x64-preview.zip`. Each
build first uses a unique ignored staging folder, so packaging does not overwrite
or stop a portable copy that is currently running. Use `-Architecture ARM64` for
that architecture; my local package check covered x64.

Packaging downloads an isolated Python runtime and the pinned theme dependencies. It does not change the system Python installation. ZIP users do not need Python or .NET installed separately.

The CLI output contains its own `themes`, `theme-runtime` and `plugins` folders, in local builds too. The engine never searches parent folders for bundled code. In a ZIP these folders live under `engine/`.

Tests and hardware trials need the owner's approval before each run. Read [AGENTS.md](../AGENTS.md).

## Linux sensor development

Core and CLI have a separate `net10.0` target for Linux sensors. Select it
explicitly; the Windows target remains `net10.0-windows10.0.19041.0`.
For a Linux-only restore, set `-p:TargetFramework=net10.0`; for a build, select
`--framework net10.0 --configuration Debug`. Keep new SDK and package imports
outside the repository until the required ClamAV scan is clean.

On Ubuntu, use `pwsh -File ./scripts/vibeware.ps1 up` for a Debug build and
launch without installation. `package` prepares the amd64 DEB used by CI.
Both the local shell and the Ubuntu 24.04 CI job use the same entry point:

```bash
./scripts/vibeware package
```

The final package is `artifacts/packages/thermalright-vibeware_<version>_amd64.deb`.
Packaging uses a temporary directory with Unix permissions, then removes it.
Set `TMPDIR` if the default temporary filesystem does not support these permissions.
The checkout itself can reside on NTFS. Packaging does not install the DEB.
Build on the oldest Ubuntu release you intend to support; a local build on a
newer system does not establish compatibility with older releases.

Linux uses its bundled `engine/trv` for theme catalogs, local previews and sensor
diagnostics. Packaging includes the pinned NumPy/Skia wheels for `/usr/bin/python3`
and declares the matching Python minor version in the DEB dependencies. User
Python packages and `PYTHONPATH` are ignored. USB, media sessions and audio remain
unavailable. Sensor diagnostics are described in [Linux sensors](linux-sensors.md).
Tests and device trials require separate approval.

For an approved Linux software check against an extracted package:

```bash
python3 tests/check_linux_preview.py /absolute/path/to/engine/trv
```

This checks the real local protocol and Python renderer without USB requests.
Add `--include-sensors` only for an approved local sensor trial; that also renders
System themes using kernel readings. The desktop icon requires an AppIndicator
host. Startup notification banners follow the desktop notification settings.

Linux `launch` uses a transient user service so the studio outlives its build
console. It requires an active systemd user manager. It installs no service file
or sign-in entry. The console prints the unit name for local journal diagnostics.
