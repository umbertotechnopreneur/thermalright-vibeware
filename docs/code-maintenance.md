<!-- VBWR B
Project: Thermalright VibeWare
Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
File: code-maintenance.md
Purpose: Maps first-party source responsibilities and maintenance conventions.
VibeWare: Human intent, AI execution, and plenty of tokens
Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
Created with AI: OpenAI Codex; source maintenance guide; 2026-10-27.
Human guidance: Umberto Giacobbi; readable methods, parameter comments and smaller files.
Copyright (c) 2026 Umberto Giacobbi
SPDX-License-Identifier: MIT
VBWR E -->

# Code maintenance

Keep each file focused on one job. Describe inputs where they are used, and keep missing data and resource ownership clear.

## Source map

| Area | Files and responsibility |
| --- | --- |
| Studio page | `MainPage.xaml.cs` owns control state and page lifetime. Its partial files group layout, appearance, library/navigation, preview, display commands, window actions, integrations, typography and filter controls. |
| Studio state | `StudioModel` owns transactions and persistence. `StudioSession` owns the worker and frame scheduling. `StudioViewModel` owns selection commands. |
| CLI | `Program.cs` dispatches commands and parses shared options. `Commands.Dashboard`, `Commands.Display`, `Commands.Integrations` and `Commands.Theme` keep each command group together. |
| Engine | `EngineServer.cs` dispatches stdio requests and owns the session. `EngineServer.Preview.cs` validates preview options and builds observations without sending USB frames. |
| Output analysis | `WindowsPlaybackAnalyzer.cs` manages endpoint/session lifetime. Its Capture partial owns sample decoding, FFT windows and measured stereo levels. |
| Theme API | `canvas.py` remains the entry point for `Canvas`, `ThemeContext` and the shared helpers used by themes. It retains the theme clock and measured display smoothing. |
| Drawing | `drawing_support.py` owns value/color conversions and the bounded layer cache. `theme_styles.py` holds fixed style data. `canvas_audio`, `canvas_media` and `canvas_composition` provide compositions through the Canvas primitive API, without importing the Canvas facade. |

OpenSilver disables default source discovery. Add every new App partial file to its project's explicit `Compile` list. The engine's existing content glob and Python bundle copy include the complete theme-runtime directory.

## Comments and headers

- Give each handwritten C# method and constructor a useful XML summary, including private methods. Describe every parameter: its meaning, unit, null/default policy, and ownership where relevant. Describe a meaningful return value or failure when it is not clear from the signature.
- Document Python functions with a docstring and an `Args` entry for each input except `self` and `cls`. Keep native installer parameter notes beside their functions and PowerShell help beside each helper.
- In longer methods, separate validation, data collection, conversion and publication with blank lines and short comments. Name intermediate decisions rather than repeating a long expression. Explain an invariant or reason; avoid narrating every assignment.
- Use the VibeWare source-header format. Keep `VBWR B` and `VBWR E`, the file's purpose, author, repository and license. Record only AI work that actually happened. Omit unknown creation history and unsupported review claims.
- Keep third-party source and its license headers intact. This pass covers first-party production code, tools and theme entry points; it does not rewrite third-party code or the existing test fixtures.

## Validation boundary

Syntax parsing, matching parameter names and comparing source bodies help review a refactor. They do not establish runtime, native-window, sensor, USB, performance or Microsoft Store behavior.

Test execution needs owner approval. The normal C# workflow requires restore, changed-file format verification, a source-built Debug package and visible reinstall. Use the SDK pinned by `global.json`; a different installed SDK does not satisfy that pin. Keep the current installation intact when those build prerequisites are unavailable.
