# Thermalright VibeWare - Quick Start

Music, artwork and PC readings on your USB display.

By Umberto Giacobbi

## 1. Get the app

Open [Downloads](https://github.com/umbertotechnopreneur/thermalright-vibeware/releases).
On Windows, choose the **x64** or **ARM64** MSI for your PC and follow the installer.
Using a portable ZIP? Extract the whole folder and keep its files together.

## 2. Open it

Connect your compatible USB screen. Open **Thermalright VibeWare** from Start,
or open `ThermalrightVibeWare.exe` in the extracted folder.
**Run as administrator on Windows.** Approve the Windows permission prompt.

## 3. Pick something to show

Choose **Music**, **System** or **Codex**, then pick a theme and look at the preview.
For Music, start playback in a player that shares a Windows media session.
No music login is needed in this app. Codex needs its optional plugin setup.

## 4. Send it to the screen

Choose your screen in **USB DISPLAY**, then press **Show on display**.
Press **Stop** to stop output. Changing the preview alone does not start output.
After disconnecting and reconnecting an active screen, press **Show on display** again.

![Real studio capture](screenshots/studio-music.png)

Real app capture supplied by the maintainer, taken before the rename. Not AI-generated.

## Make it yours

- **Appearance:** change the font. Enable color filters to adjust hue, saturation,
  brightness and contrast. With filters off, the sliders do not affect the picture.
- **Refresh:** start with **1 s**. You can also choose 500, 250, 100, 50 or 25 ms.
  This sets the requested update interval; your PC and screen determine actual speed.
- **Quality:** choose **Poor**, **Low**, **Medium** or **High**. Start with **High**;
  lower quality makes smaller, rougher pictures.
- **Pages:** use the page arrows. Edit page timings in the theme files.
  Auto-cycle switches between enabled screens every 20 seconds; with only one
  enabled screen, it cycles that screen's themes.

## Keep it running, or stop it

Your current setup is saved. **Minimize to tray** keeps the app running by the clock.
Use **Close app** or the tray menu's **Exit** to quit.

With Windows startup enabled, the app restores your saved screen and theme in the
tray at sign-in. If that screen is not ready yet, it waits for it. **Stop** cancels
that wait for the current run. The [usage guide](../PORTABLE.md) explains how to
turn off future startup.

## A couple of useful things to know

Missing sensor readings stay unavailable. Open **Sensor access** or the
[sensor guide](sensors.md) for setup details.

**Themes run Python code on your PC. For now, only download themes from the
[official repository](https://github.com/umbertotechnopreneur/thermalright-vibeware).**

## Useful links

- [Downloads](https://github.com/umbertotechnopreneur/thermalright-vibeware/releases)
- [Full usage guide](../PORTABLE.md)
- [Sensor setup](sensors.md)
- [Make a theme](theme-packs.md)

This is an independent, unofficial hobby project by Umberto Giacobbi, provided
as is without warranty. I do not work for Thermalright and am not affiliated
with or sponsored by Thermalright. All trademarks belong to their respective owners.
