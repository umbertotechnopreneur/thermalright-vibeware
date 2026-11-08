# Repository console

Run `pwsh -NoProfile -File ./scripts/vibeware.ps1` for the menu.

| Command | Action |
| --- | --- |
| `preflight-check` | Inspect prerequisites without changes. |
| `preflight` | Install missing prerequisites with apt or winget. |
| `up` | Build Debug and start; Windows uses the visible MSI completion choices. |
| `build` | Build Linux Debug without installation, or reinstall Windows Debug. |
| `launch` | Open the existing local studio. |
| `package` | Prepare a Linux DEB or Windows portable ZIP for CI. |
| `clean` | Remove generated repository output. |
| `help` | Show usage. |

On Windows, open PowerShell 7 as administrator and use
`./scripts/vibeware.ps1 build -Architecture x64 -PrepareImports`.
The flag explicitly prepares and scans new staged imports before compilation.
The result is held in memory for that command.

For repeated Windows builds, start the menu with `./scripts/vibeware.ps1 -PrepareImports`
and keep it open between builds. The menu reuses the staged dependencies within
that session and removes generated material when you exit.

The single console follows Armata's menu wording and command structure. Its
proprietary code is not copied. Internal modules and build/install helpers remain
separate so MSBuild and portable packages can call their specific operations.
The old top-level build scripts have been removed; there are no legacy aliases.

On Linux, `./scripts/vibeware preflight` also installs PowerShell if missing.
On Windows, `powershell -File ./scripts/vibeware.ps1 preflight` can prepare PowerShell 7.
PowerShell 7 runs the console. Ubuntu preparation uses its configured
apt repositories and may ask for sudo. Windows preparation can install App Installer (winget);
prerequisite installers may ask for elevation or a restart. Missing tools stop the
operation before uninstalling the app. New dependency imports still follow the
repository scan policy; preflight does not bypass it.

Linux builds are Debug and run without installation. CI uploads an amd64 DEB for
Ubuntu 24.04 and compatible systems. Linux supports local theme previews and
kernel sensor diagnostics. USB, audio and media features remain unavailable. The DEB installs no driver or
startup entry. Tests and physical-device trials still need owner approval.

Both local and CI packaging use `./scripts/vibeware package`. It calls
`build/Build-PhotinoLinux.ps1`, `build/Bundle-LinuxPython.ps1` and `build/Build-Deb.ps1`; there is no separate
CI packaging recipe. The final DEB stays under `artifacts/packages/`.
DEB staging uses the system temporary directory and is removed afterwards.
That directory must support Unix permissions; use `TMPDIR` to choose it when
building from NTFS. No package is installed by this command.

Linux builds compile the pinned Apache-2.0 Photino native bridge in Debug against
WebKitGTK 4.1. Preflight includes its C++ compiler, pkg-config and development
headers. Source archives and extracted inputs are checked before compilation.
The Linux bridge selects the window monitor; it does not require a primary monitor
from Wayland. Window placement remains subject to the desktop compositor.

The Linux studio uses the desktop's AppIndicator host for its status icon, with
Show studio and Close app actions. It sends one startup notification after the
UI loads. Indicator visibility and notification banners depend on desktop
support and preferences; the studio remains visible when no indicator host exists.

Linux `launch` uses a transient user service so the studio outlives its build
console. It requires an active systemd user manager. It installs no service file
or sign-in entry. The console prints the unit name for local journal diagnostics.
