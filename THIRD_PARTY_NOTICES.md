# Third-party notices

The desktop UI uses OpenSilver and OpenSilver.Photino (MIT, copyright Userware). The launcher uses Photino.NET and Photino.Native (Apache-2.0, TryPhotino); the license is included under `licenses/Photino-Apache-2.0.txt`. Windows App SDK/WinUI are no longer dependencies of this UI.

OpenSilver's bundled browser resources retain their upstream notices. These include Quill, html-to-image, and FileSaver. They are restored from NuGet, not hand-copied or relicensed as ThermalrightVibeWare source.

The Windows build patches Photino.Native's UTF string ownership to release converted web messages, and synchronizes WebView2 controller visibility with the host window on creation, show/hide and minimize/restore. The Apache-2.0 source is pinned to TryPhotino commit `d790e5be3127ec628d0a9f1f9fac68e14f339c2a`; its copyright and license notices remain in the build sources. See `scripts/build/Build-PhotinoNative.ps1`, [upstream issue 165](https://github.com/tryphotino/photino.Native/issues/165), and Microsoft's [controller visibility contract](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/icorewebview2controller#put_isvisible). The C ABI is unchanged. This does not complete a review of the whole native library.

The USB protocol facts for Thermalright `0416:5302` were researched from device behaviour, public protocol references, and comparison with `thermalright-trcc-linux`'s implementation. That project is GPL-3.0. ThermalrightVibeWare does not include copied upstream code or assets. Its Windows transport, renderer, and application services are independently authored. See `docs/protocol-notes.md` for the source references and evidence limits.

System.Drawing.Common is used under the MIT license. Windows GDI+ performs local still-image rendering and JPEG encoding.

Spectre.Console and NAudio.Wasapi (including NAudio.Core) are MIT-licensed dependencies. System.Management is part of the MIT-licensed .NET libraries. Their package license information remains available through NuGet.

Windows media-session metadata and artwork come from the current user's local player. No Spotify Web API or Spotify asset bundle is included.

CPU, GPU and motherboard measurements use a source build of [LibreHardwareMonitor fork commit e5fa436eae6cb9ad5e8693d9909cd81ddf473629](https://github.com/umbertotechnopreneur/LibreHardwareMonitor/tree/e5fa436eae6cb9ad5e8693d9909cd81ddf473629), based on upstream commit `e68a7ef7326fa875210e89e1402d93a45ffe4d3e`. The fork fixes NCT6792D temperature decoding and adds the ASRock B550M-ITX/ac source mapping in [upstream PR #2590](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/2590). The library remains MPL-2.0, with original notices preserved. Its source is under `third_party/LibreHardwareMonitor` in this repository and `sources/LibreHardwareMonitor` in the distribution; the license is also under `licenses/LibreHardwareMonitor-MPL-2.0.txt`.

The library project is adapted for .NET 10 and Thermalright VibeWare's build paths and packaging. Sensor code and embedded modules are unchanged from the pinned fork. Required runtime dependencies keep their own licenses and NuGet notices. The monitoring GUI and driver installers are excluded.

The unchanged embedded PawnIO modules come from [PawnIO.Modules 0.2.11](https://github.com/namazso/PawnIO.Modules/releases/tag/0.2.11) under LGPL-2.1. Their original `COPYING` and source reference are retained in `sources/LibreHardwareMonitor/Resources/PawnIo`.

One Windows adapter collects the library's readings, including the enabled RAM/SPD group through `RAMSPDToolkit-NDD` and existing PawnIO. Windows supplies physical RAM totals, module identities, memory type and configured data rate independently of SPD. No swap data is exposed. CPU and motherboard access requires a separate PawnIO installation. Thermalright VibeWare checks existing registration and never installs PawnIO, changes antivirus exclusions or falls back to WinRing0. Construction and driver-status queries do not open hardware; selected System themes request sensor reads lazily.

This source migration replaces the earlier Open Hardware Monitor dependency and embedded WinRing0 build. Legacy installer cleanup retains the old driver names solely to remove files/services owned by older Thermalright VibeWare installations. It does not load them. CPU and motherboard access on Windows ARM64 remains disabled pending implementation evidence. Sensor coverage and actual driver access require separate live validation. See [sensor setup](docs/sensors.md).

The Windows portable ZIP bundles CPython 3.13.16's official embeddable distribution (Python Software Foundation License 2.0). Its `LICENSE.txt` is copied into `licenses/Python-PSF-2.0.txt`. It also bundles pinned PyPI wheels for skia-python 144.0.post2 (BSD-style), NumPy 2.4.3 (BSD-3-Clause), and pybind11 3.0.2 (BSD-3-Clause). Their wheel metadata and included license files remain under `engine/python/Lib/site-packages/*.dist-info/`. The packaging script verifies the Python archive against the official release SHA-256 and each wheel against its PyPI release SHA-256 before extraction.
