# Project choices

- TermalRight VibeWare is Umberto Giacobbi's independent hobby project. VibeWare is his initiative.
- Ship a Windows MSI and a portable ZIP built from current source. Keep settings and personal themes outside installed files.
- Use .NET 10, OpenSilver and Photino for the studio, and Python/Skia for themes. The engine owns USB communication; the UI uses its versioned local protocol.
- Restore the saved display and theme on Windows startup. Stop cancels a pending startup wait. Keep preview rendering independent of display output.
- Use local Windows media and available sensor readings. Do not invent missing values, record audio or integrate directly with Spotify.
- Use the source-built LibreHardwareMonitor fork and ship its source. Do not install a sensor driver or monitoring suite from the app.
- Keep themes customizable, and make clear that they run code. Use the shared accent-to-amber-to-red scale for progress indicators.
- Keep Music, Codex and System in the theme library. Use central Preview, Appearance, Display and Diagnostics tabs, with raw Sensor Debug separate. Preserve choices when tabs change. Auto follows the system light/dark preference.
- Enable local diagnostic logging by default and preserve an explicit choice to turn it off. Keep the saved-display waiting status visible during preview.
- Select an installed stable .NET 10 SDK without rolling forward to .NET 11.
- Use the installer result after a local reinstall. Do not run post-install audits unless requested.
- Keep current limitations in [cave.md](cave.md), without accumulating a development history.
