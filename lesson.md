# Project choices

- TermalRight VibeWare is Umberto Giacobbi's independent hobby project. VibeWare is his initiative.
- Ship a Windows MSI and a portable ZIP built from current source. Keep settings and personal themes outside installed files.
- Use .NET 10, OpenSilver and Photino for the studio, and Python/Skia for themes. The engine owns USB communication; the UI uses its versioned local protocol.
- Restore the saved display and theme on Windows startup. Stop cancels a pending startup wait. Keep preview rendering independent of display output.
- Use local Windows media and available sensor readings. Do not invent missing values, record audio or integrate directly with Spotify.
- Use the source-built LibreHardwareMonitor fork and ship its source. Do not install a sensor driver or monitoring suite from the app.
- Keep themes customizable, and make clear that they run code. Use the shared accent-to-amber-to-red scale for progress indicators.
- Keep Music, Codex and System in the theme library. Use central Preview, Appearance, Display and Diagnostics tabs, with raw Sensor Debug separate and no duplicate page title. Omit routine library/running labels. Keep output options and screen choices side by side. Preserve choices when tabs change. Auto follows the system light/dark preference.
- Enable local diagnostic logging by default and preserve an explicit choice to turn it off. Keep the saved-display waiting status visible during preview.
- On Linux, show the product icon in the desktop indicator area and send a startup notification when the studio UI is ready. The icon menu shows or closes the studio.
- Select an installed stable .NET 10 SDK without rolling forward to .NET 11.
- Use the installer result after a local reinstall. Do not run post-install audits unless requested.
- Keep current limitations in [cave.md](cave.md), without accumulating a development history.

The repository console is `scripts/vibeware.ps1`. Linux local builds use Debug
without installation. The Linux studio negotiates engine capabilities and renders
local previews through Python/Skia. CI prepares an Ubuntu amd64 DEB with isolated
Python dependencies and an explicit interpreter version requirement. Windows local builds keep
the visible Debug MSI reinstall flow.

The studio and engine share bounded local initialization events in `engine.log`.
Events record the stage, process, platform and error type, without request bodies
or media data. The saved diagnostics switch still controls local file logging.

Linux NVIDIA readings use the already installed NVML library without setters or a
bundled driver. Keep driver detection separate from successful samples. Show
missing channels as unavailable and fan target percentages separately from RPM.
Sensor access offers detected sources and manual guidance, without loading
modules, scanning buses or changing permissions.

Keep studio text concise. The sidebar shows theme numbers and names only. Put the
description once in the main workspace. Omit the local-preview badge and repeated
theme titles. Put routine technical notices behind Diagnostics details, while
keeping errors and active display status visible. Sensor access uses two columns
without pagination. Keep studio labels and footer text readable.
