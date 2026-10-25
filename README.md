# Thermalright VibeWare

[![CI status](https://github.com/umbertotechnopreneur/thermalright-vibeware/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/umbertotechnopreneur/thermalright-vibeware/actions/workflows/ci.yml)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D4)
[![License: MIT](https://img.shields.io/badge/license-MIT-2EA44F)](LICENSE)

I made this free, open-source app for my Thermalright Trofeo Vision LCD. It puts music, artwork and PC sensor readings on the screen. I'm [Umberto Giacobbi](https://umbertogiacobbi.biz).

**Start here: [Quick Start (PDF)](docs/quick-start.pdf)** · [Read it online](docs/quick-start.md).

*Real app captures, not AI-generated images. Captured before the rename.*

## Music from your apps

Spotify is shown here. Other players that share a Windows media session work too. See the song, artwork, playback progress, real output spectrum and stereo VU meters.

| Ribbon | Halo |
| --- | --- |
| ![Spotify playback in the Ribbon theme](docs/screenshots/ribbon-music.png) | ![Spotify playback in the Halo theme](docs/screenshots/halo-music.png) |

## Your PC on the display

System themes cycle through pages for CPU, GPU, RAM, temperatures, power and fans. I can choose a page and change the font and colors.

Each System theme has six rotating pages. Theme files let me change what they show and how long each page stays on screen.

| Temperatures · Quiet | Power and cooling · Ledger |
| --- | --- |
| ![Real temperature readings and history in Quiet](docs/screenshots/quiet-temperatures.png) | ![Real CPU, GPU and fan readings in Ledger](docs/screenshots/ledger-cooling.png) |

## Make it your own

Choose from 30 Music, System and Codex themes. Pick a theme and page in the studio, adjust its appearance and send it to the display. An optional Codex plugin shows local activity too.

For any theme, adjust hue, saturation, brightness and contrast. Choose a refresh interval of 25, 50, 100, 250, 500 or 1,000 ms, and Poor, Low, Medium or High image quality. Change page timings in the theme files, or let the studio switch themes every 20 seconds.

| Music in the studio | System pages in the studio |
| --- | --- |
| ![Studio with the Orbit music theme](docs/screenshots/studio-music.png) | ![Studio with the Ledger system theme](docs/screenshots/studio-system.png) |

<details>
<summary>Two more real screens</summary>

| Orbit · music | Matrix · hardware |
| --- | --- |
| ![Orbit music](docs/screenshots/orbit-music.png) | ![Matrix hardware readings](docs/screenshots/matrix-readings.png) |

</details>

## A small app that runs on your PC

Every theme is a Python script. Python is a widely used programming language that is simple to read and learn. A theme is **a small app that runs on your PC**: it can read the data supplied by the engine, draw charts, arrange information and change pages. You can change its colors, layout and behavior. The [theme guide](docs/theme-packs.md) shows where to start.

## Less weight on your PC

I have tried to test as much as possible for memory use, disk space and security, and to make this app extremely light compared with the alternatives. I want your PC's resources left for the things you care about. This is a work in progress: I keep working to make it lighter.

I built this for my Thermalright Trofeo Vision LCD, a 6.86-inch USB screen (1280 × 480). Thermalright sells it in [black](https://www.thermalright.com/product/trofeo-vision-lcd-black/) and [white](https://www.thermalright.com/product/trofeo-vision-lcd-white/). [Hardware details](docs/protocol-notes.md).

This is an independent, unofficial project. I do not work for Thermalright, and I am not affiliated with or sponsored by Thermalright. Thermalright does not endorse this project. All trademarks and registered trademarks belong to their respective owners.

## Get it

[Downloads](https://github.com/umbertotechnopreneur/thermalright-vibeware/releases). On Windows, choose the **x64** or **ARM64** MSI for your PC and follow the steps. For a portable ZIP, extract the whole folder before opening the app.

**On Windows, run Thermalright VibeWare as administrator.**

**Linux and macOS guinea pigs wanted! 🐹** Want to help try it on your computer? [Share what you find](https://github.com/umbertotechnopreneur/thermalright-vibeware/issues).

[How to use Thermalright VibeWare](PORTABLE.md) · [Sensor setup](docs/sensors.md).

**Themes run Python code on your PC. For now, only download themes from [this official repository](https://github.com/umbertotechnopreneur/thermalright-vibeware).**

I made this hobby project in a few days on holiday because I wanted lighter display software that I could change to suit me. I learned from other people's public work and used AI to help write it. [Credits](docs/protocol-notes.md#sources-studied).

The software is open source under the [MIT license](LICENSE); the hardware is sold separately. Some included components have their own [licenses](THIRD_PARTY_NOTICES.md). This hobby project is provided as is, without any warranty.

You are welcome to try it with other screens, use the code and share what you learn.

## More tools

[VoiceDucker](https://github.com/umbertotechnopreneur/VoiceDucker) · [Ulanzi Dock](https://github.com/umbertotechnopreneur/ulanzi-dock-vibeware)

## A note on security

I care about what goes into this project. Before I add a third-party library or prebuilt binary, including files supplied without source code, I scan the downloaded package, its extracted files and any dependencies with Microsoft Windows Defender. I keep new imports aside until the scan finishes without detections.

I keep a record of where each new import came from and the scan result. If a scan cannot finish or flags something, I leave it out.

---

Part of my VibeWare initiative. [My manifesto](https://umbertogiacobbi.biz/vibeware/manifesto).

[Terms](https://umbertogiacobbi.biz/terms/) · [Privacy](https://umbertogiacobbi.biz/privacy/)

<p align="right">
  <a href="https://umbertogiacobbi.biz/vibeware/manifesto"><img src="src/ThermalrightVibeWare.App/Assets/vibeware-logo.png" alt="VibeWare floppy logo" width="200"></a><br>
  <em>Human Intent, AI Execution, Plenty of Token</em>
</p>
