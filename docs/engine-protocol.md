# Local engine protocol

The desktop window starts one hidden `trv --stdio` process. Requests and replies are UTF-8 JSON Lines, version 1. There is no server port. Stdout contains replies only.

## Choose a theme and display

```json
{"version":1,"id":"1","method":"themes"}
{"version":1,"id":"2","method":"devices"}
{"version":1,"id":"3","method":"state","parameters":{"themeId":"media.aurora","profileId":"wide-1280x480","refreshIntervalMilliseconds":250,"jpegQuality":"medium"}}
```

`themes` reads the local manifests. `devices` enumerates interfaces and returns opaque IDs. Neither sends a USB handshake or picture. `state` renders a local preview and never writes to USB.

Each theme includes `supportsSpectrum`, default false. It is true only when its manifest declares `settings.showSpectrum.type` as `boolean`; catalog discovery never executes theme code.

## Show the same picture on the panel

Use an ID returned by `devices`:

```json
{"version":1,"id":"4","method":"start","parameters":{"deviceId":"<device-id>","themeId":"media.aurora","profileId":"auto"}}
{"version":1,"id":"5","method":"present","parameters":{"themeId":"media.aurora","profileId":"wide-1280x480","refreshIntervalMilliseconds":250,"jpegQuality":"medium","fontFamily":"Cascadia Mono","fontScale":1,"light":false}}
{"version":1,"id":"6","method":"stop"}
```

`start` opens the selected panel and probes its size. Its reply includes `accepted` and the detected `profileId`. Pass that profile to `present`. An explicit profile can override detection for an unknown model.

`present` draws one frame, sends that JPEG to the panel, and returns it for the desktop preview. The CLI owns the renderer and USB session. It does not draw a second copy for the display. Changing theme, font, colors, interval or quality takes effect on the next request. Byte-identical frames are not sent again.

Only one request/frame is in flight. A slow update lowers the rate rather than creating a queue. Theme updates follow the GUI's requests; they stop when the window closes or the engine reaches EOF. `stop` closes the panel. Errors close the theme output; reconnecting requires another explicit start. An accepted submission does not prove visible pixels.

Every Windows GUI launch automatically starts the saved display and theme. With no saved display, a single connected device is selected; multiple devices require a selection. At the owner's request, Windows sign-in uses `--restore-last-session`: the studio issues one `start` for its saved device/theme, then the usual `present` requests with saved settings. The first launch shows the studio; after its first loaded page is saved, later launches stay in the tray. Missing saved choices never fall back to another device or theme. A late device is checked every ten seconds; Stop cancels that startup wait. A failed start is not replayed.

The studio requests `devices` every ten seconds, with one enumeration pending at most. Discovery reads Windows interface metadata and closes a missing theme session; it never sends a probe or a frame. Reconnection only updates the list, apart from the initial Windows-startup wait described above. Starting output still needs `start`. A browser preview failure leaves a successfully opened USB session active.

During explicit `present` requests, an identical JPEG is sent again after ten seconds. This detects a stale handle after an unplug/replug between enumerations. It uses the existing session and never reopens it. An I/O failure closes output and requires a new explicit start. Legacy `trv play` also stops on a disconnect rather than retrying.

`colorFiltersEnabled: false` skips the final color pass, including theme overrides. It keeps the slider values. The studio saves this switch and starts with it off; omitted engine values default to true for CLI color requests. Replies include `accentColor`, the theme's cached accent, for the slider guides.

## Rate and quality

`refreshIntervalMilliseconds` defaults to 1000; valid range 25–1000. The GUI offers 1000, 500, 250, 100, 50 and 25 ms. The 25 ms setting is aggressive. These are requested intervals, not guaranteed panel frame rates. Metadata and artwork are checked at most once a second; timeline and existing playback meters can update faster.

`jpegQuality` is `poor`, `low`, `medium` or `high`, default high. Encoder targets are 20, 50, 75 and 90. Poor uses very strong compression. Encoding falls back to lower quality if needed to stay under 450,000 bytes. Rotated JPEG profiles also honor the selected target. Other pixel formats require conversion.

`PanelState` adds `frameMilliseconds` (server-side data/render/send duration) and `submittedFrames`. These are not measurements of physical display latency. `displayRunning` and `status` report the session. `activePageId`, `activePageTitle`, `activePageIndex` and `activePageCount` describe the page actually rendered. `PreviewOptions.pageIndex` selects the first page for a rotation and `pageRevision` restarts its interval after an explicit studio navigation.

`nextFrameMilliseconds` is normally null. During a two-second page or track/cover crossfade it requests updates at up to 40 ms, or the user's faster interval. The studio returns to the selected interval when it becomes null. The worker blends before the single color pass and JPEG encoding, so preview and USB receive the same image. Slow rendering or USB can reduce the fade steps; no frames are queued.

## Other settings

`fontFamily` defaults to Cascadia Mono. `fontScale` is 0.65–1.6. `light` and `ambientDrift` default to false. Python keeps decoded artwork and static backgrounds cached. A failed preview retries after five seconds, except when settings change.

`saturationPercent` defaults to 100 and accepts finite values from 0 to 200. `hueDegrees` defaults to 0 and accepts finite values from -180 to 180. `brightnessPercent` defaults to 0 and accepts -100 to 100; `contrastPercent` defaults to 100 and accepts 0 to 200. All four values must be finite numbers.

These theme options apply one native color filter to the completed image before JPEG encoding. Neutral values (100, 0, 0, 100) skip the extra image operation. The preview and panel use the same adjusted JPEG. They do not rewrite a theme or its cached artwork.

Each frame gives Python themes a mutable `context.colors` object with `saturation_percent`, `hue_degrees`, `brightness_percent` and `contrast_percent`. It starts with the current UI/CLI values. The theme can read or override them inside `render(canvas, context)`:

```python
context.colors.brightness_percent = 10
context.colors.contrast_percent = 115
```

After `render` and `finish`, the worker validates the resolved values and applies the filter once. An invalid override fails the frame instead of silently clamping a control. The override does not change the UI/CLI settings and starts fresh on the next frame; bundled themes currently leave all four values unchanged. No manifest setting is needed for this API.

`showSpectrum` defaults to true. False hides FFT bars in themes that declare support; it does not hide stereo VU/decibel values or stop their audio analyzer. The studio disables this checkbox for Codex and themes without the manifest setting. Color and spectrum changes invalidate the current preview immediately.

`fonts` returns `{ "families": ["…"], "notice": null }`: the installed monospace families the Python/Skia renderer can use, not a hardcoded list. Discovery shares the renderer's serialized worker and twelve-second deadline, but loads no theme and opens no audio source or USB device. The result is cached by that worker; restart the engine after installing fonts. The list is bounded to 512 names, with a notice for a partial or empty result. Missing Python dependencies return a normal RPC error, not invented font names.

```json
{"version":1,"id":"fonts-1","method":"fonts"}
{"version":1,"id":"colors-1","method":"state","parameters":{"themeId":"media.aurora","saturationPercent":120,"hueDegrees":-20,"brightnessPercent":10,"contrastPercent":115,"showSpectrum":false}}
```

Multimedia themes read generic Windows media/audio data. Their audio analysis uses Windows playback loopback, never the microphone. PCM stays in bounded memory; it is not saved or sent. `audio.analysis` contains real log-frequency FFT bands and stereo peak/RMS values in dBFS. The source is `system-output`: the full playback mix, not an isolated app. App volume and session meters remain separate. Codex themes stop this analyzer and skip media/audio sources.

The requested audio update target is 20 Hz. A 4096-sample FFT at 48 kHz spans about 85 ms; selecting 50 ms does not promise 50 ms audio latency or panel refresh. Missing or stale samples remain unavailable or silent, never made-up bars.

Codex themes read optional local hook activity. The latest twelve observed events are kept in memory; events between polls and real chat titles may be unavailable.

## Codex setup and local logs

`codex.status` reads the local plugin, personal marketplace and latest event. It does not prove hooks are enabled or trusted. `codex.prepare` stages the bundled plugin and lists it locally, only after an explicit UI click or CLI command. The user must finish installation and review the hooks inside Codex. No extra account login is used.

`diagnostics.status` returns `enabled` and `logPath`. `diagnostics.set` takes `{ "enabled": true }` or false. It starts off and remembers the choice. `diagnostics.open` opens the known local log on request. Logs are limited to 512 KiB plus one backup. They contain technical event metadata, not request payloads, song titles, prompts or audio.

The engine caches that preference. If a separate CLI changes it while the GUI is open, reopen the GUI to pick it up.

The engine resolves Python from `THERMALRIGHT_VIBEWARE_PYTHON`, bundled `python/python.exe`, and requires that file to exist. There is no PATH search. It uses a persistent worker with a twelve-second render deadline. Themes are executable Python, not sandboxed documents.

## CLI

```powershell
trv gui
trv themes
trv fonts
trv codex status
trv codex prepare
trv diagnostics on
trv diagnostics open
trv diagnostics off
trv theme media.aurora --export frame.jpg --quality high
trv theme media.aurora --export tinted-frame.jpg --saturation 120 --hue -20 --brightness 10 --contrast 115 --no-spectrum
trv theme media.aurora --device 1 --interval-ms 250 --quality medium --seconds 20
```

Export is local and never overwrites a file. Device playback stops on Ctrl+C, its optional duration or a failure. It does not reconnect automatically.

`--saturation`, `--hue`, `--brightness`, `--contrast` and `--no-spectrum` work with both `theme --export` and `theme --device`. Use decimal points for numeric arguments. `--no-spectrum` hides only supported FFT bars; VU meters remain.

`state`, `start` and `present` require a theme ID. The retired C# scenes and scene fields are removed. `levels` reads cached playback meters. `media` controls the current OS media session when explicitly requested; there is no volume-write method.

Request lines are limited to 16 KiB; replies to two million characters. Reply fields are `version`, matching `id`, `ok`, `result` and `error`. Native providers currently support Windows.
