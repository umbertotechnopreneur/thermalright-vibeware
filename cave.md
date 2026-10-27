# Still to do

On 19 October, the old MSI and stale script registration were removed before a
clean Windows x64 Release build and visible installation of MSI 0.1.8. Installer
exit status was 0. All 1,624 installed payload files matched the new staging
files by SHA-256. One MSI registration remained, with no legacy script entry.
The installed sensor library is LibreHardwareMonitorLib 0.9.6.0; the old Open
Hardware Monitor DLL is absent. [Dependency scan evidence](docs/dependency-scan-2026-10-19.md).

The new reinstall command increments the MSI version, removes the old package
and generated output, builds and installs the new package, verifies its files,
then keeps only the final MSI. Initial final cleanup hit an OpenSilver DLL held
by an MSBuild server. Stopping that server allowed cleanup to finish; publishing
now disables build-server/node reuse. Only the final MSI remains in artifacts.

MSI upgrade ordering and the legacy cleanup helper were updated and compiled.
The package table places old-product removal before new file installation.
The clean installation above does not prove upgrade, repair, rollback, cancellation
or duplicate-registration regression scenarios. No tests, sensor diagnostic run,
driver installation or USB output was performed. Thermal RPM plumbing was inspected
in source; actual RPM availability on this hardware remains unverified.

I made this in a few days. It isn't as good as I want it yet.

My list:

- Check memory over longer runs.
- Check how fast it runs.
- Check security, especially themes.
- Try more displays and settings.
- Try the Codex plugin with real activity.
- Add Linux and Mac support.

I used AI to help write the code. I haven't had time to finish these checks. I'll work on them when I can, following my [manifesto](https://umbertogiacobbi.biz/vibeware/manifesto).

**Themes are programs. They run code on your PC. For now, only get them from the [official repository](https://github.com/umbertotechnopreneur/VibePanel). Security checks are still to do.**

## What I checked

An earlier picture worked on my Thermalright display.

On 16 October 2026, I built the Windows x64 app and ZIP. One approved check made all 40 theme pictures: 20 themes, light and dark. I looked at the overview images. They used made-up demo data.

On 17 October, I confirmed Aurora Glass worked on my display after a 20-second CLI trial. The new GUI controls, faster rates, other themes, live Codex activity and setup on a fresh PC still need checking.

The new playback spectrum, stereo VU and Codex setup controls are not checked in live use yet. A successful build does not check them.

The color controls, monospace font list, theme fades and 25 ms/Poor settings also need a live check.

Page, track and cover changes now blend in the renderer for two seconds. The approved offline checks from 17 October covered the earlier 600 ms duration, so the new timing still needs an owner-approved run. They used synthetic images and metadata, with no app, audio or USB. Real playback and USB output still need checking.

The new studio layout builds for Windows x64 with no errors or warnings. Its dropdowns, filter switch and color guides still need my visual check. CPU savings have not been measured. No tests, app or USB trial were run for this change.

The preview now has a flat, solid backing with no shadow or blur effect. The reported flicker still needs a live check.

The Windows 1 GB guard builds in Release x64. On 17 October, the old app hit about 1.16 GB and triggered the popup and stop. A frozen UI and guard failures still need checking.

I then fixed native message ownership, moved preview JPEGs outside the JavaScript eval bridge and limited NumPy's native threads. Local checks passed: 4,000 large UTF conversions, 1,317 preview frames with UI changes and error recovery, and 400 synthetic renders across all twenty themes. The UI process family peaked at 345 MB; the separate worker check peaked at 102 MB. No USB device was used. These short checks do not prove long-term memory use, speed or security.

The earlier memory check did not check the preview's on-screen size. I fixed its HTML container height, then checked WebView captures and visible canvas pixels in light/dark mode, after resizing, and with wide, square and portrait fixtures. The image is visible in those captures. Very small windows can still leave little room for it. This was an offline check, not a USB trial.

The 17 October resource audit limits PNG/JPEG/GIF/BMP wallpaper and legacy artwork to 16 megapixels. Other formats use the fallback. Legacy backgrounds stay in a 12-entry/8 MiB memory cache. Bundled scripts load only beside the engine.

USB discovery runs every ten seconds. Active output also resends an unchanged frame after ten seconds to catch stale handles. A lost session closes and never restarts itself, including in the legacy still-image player.

At my request, Windows startup now starts the saved device and theme in the tray, once that device is available. Stop cancels the wait. All 14 approved hidden-window checks passed: startup settings, the ten-second timer, a late saved device, Stop, errors and close/reopen preferences, including the last slider change. They used a fake client and no USB backend. A real Windows sign-in check is still pending.

The Windows x64 Release build has no errors or warnings. All 85 approved offline checks passed, including oversized image headers, 32 artwork changes and bundled paths. No USB handles were opened. Repeated physical unplug/replug and long-term memory use remain unchecked.

The ten new System themes and screen switches build in Release Windows x64. Missing values and sections are hidden; swap is excluded. Screens can be disabled independently of their preview. Closed Codex processes are skipped on the device; no eligible screen sends a black frame.

On 17 October, all 96 approved offline C# checks and all 18 Python tests passed. The Python run encoded and decoded 180 JPEGs across the ten System themes, light/dark, wide/square/portrait and complete/partial/missing synthetic data. No live sensor or USB check ran. The ImageGen proposals use demo values and are design references, not captures of the renderer.

The original Open Hardware Monitor 0.9.6 is the only Windows sensor source. The stock .NET Framework 4.5 binary failed the approved diagnostic read with a missing mutex API before any data was returned. I now rebuild its pinned source for .NET 10 with four API compatibility changes. Sensor algorithms and driver bytes stay unchanged. The approved Windows x64 read returned CPU usage, GPU usage/clock/temperature, and used/total RAM. CPU clock and temperature were unavailable. This does not prove other hardware works. Windows ARM64 sensors are unavailable. GPU support covers detected NVIDIA/AMD devices, not every GPU.

I approved the DLL's WinRing0 driver lifecycle on 17 October 2026. Reading a System screen may install/load the driver internally; closing the reader may remove it. The first approved diagnostic attempt found the binary incompatibility. A separately approved read of the rebuilt library returned live data. Linux and macOS sensor readers remain unimplemented.

All 99 approved offline C# checks passed, including the .NET 10 library target and the exact hashes of both embedded upstream drivers. All 18 Python tests passed again, including 180 synthetic System-theme images. These offline checks did not access sensors, audio or USB; the live sensor read was separate.

After approval, I installed the compatibility build and reopened the saved System/Ledger screen in local preview. The visible studio showed live CPU usage, GPU usage/clock/temperature, and used/total RAM. The installed sensor DLL matched the packaged DLL. Two diagnostic reads succeeded; no USB output or audio command was issued. CPU clock and temperature remained hidden because the library returned no values.

On 18 October, a separately approved diagnostic with verified administrator privileges also returned CPU clock and package temperature. Windows recorded WinRing0 installation during the read; its service was absent after the reader closed. The existing studio and display session were left running without elevation. This confirms that the missing CPU details on this machine were caused by insufficient privileges. It does not prove the same driver works on other Windows configurations.

The System administrator-restart button builds in Windows x64. It saves choices, waits for Windows to accept elevation, then closes the old studio. The replacement waits for that process to exit and starts in preview. The existing memory guard launches the replacement outside the old process-family job. No extra service is added, and memory protection stays enabled.

On 18 October, all 99 baseline checks and nine offline administrator-button checks passed. The separate elevated fixture survived the old guard's cleanup and started its own guard. The three restore-visibility checks passed again. After approval, I updated the installed app and clicked its real administrator-restart button. One new studio reopened with the same System theme and settings, in local preview, showing CPU usage/clock/package temperature, GPU details and RAM. A normal launch then restored that elevated instance and exited successfully. Cancellation and launch failure were checked with a fake host; no real Windows permission dialog was canceled. No USB output command was sent during these checks.

The white-window report was traced read-only to a running installed studio whose WebView child windows were hidden while the UI and engine kept working. The pinned Photino bridge now synchronizes WebView2 controller visibility on host show/hide, minimize/restore and controller creation. The approved isolated startup check passed all three restore paths. Native child-window visibility, DOM content and three captured WebView images showed the studio after hidden startup, hide/show and minimize/restore. This used a fake client and isolated settings, with no engine, sensor or USB backend. The installed copy was left untouched during that check.

After separate approval, I updated the installed package and opened it in preview without startup-output arguments. Read-only inspection confirmed one studio instance with visible host, browser and renderer windows. Its native bridge matched the built DLL. This was not a sensor or USB trial, or a real Windows sign-in check.

This is my hobby project. No warranty.

On 19 October, Windows Defender recorded `VulnerableDriver:WinNT/Winring0` and quarantined the old installed `OpenHardwareMonitorLib.sys`. The source now uses the official precompiled LibreHardwareMonitorLib 0.9.6 package. The old source project, preparation script and payload source archive are removed. Current builds contain the library and required runtime dependencies, without a monitoring GUI or driver installer. Installer cleanup still recognizes old Vibe Panel driver names solely for removal during upgrades/uninstall.

The Windows adapter reads named motherboard channels without assigning generic readings to CPU. Physical RAM uses `GlobalMemoryStatusEx`; LibreHardwareMonitor's memory/SPD group remains disabled to avoid its driver-loading path. PawnIO registration and its service path are read without installing or opening a driver. The System sidebar displays this status and can open the existing driver folder. Registration alone does not verify access. The two thermal layouts separate names, temperatures, load and RPM, with explicit unavailable labels and readable notices.

Windows x64 restore from official NuGet, format verification and Release compilation passed. With explicit approval, 110 offline checks, 35 fake-client studio checks and 24 Python theme checks passed. Theme checks included 180 synthetic JPEG combinations; 24 additional Thermal Atlas/Airflow fixtures were inspected at font scale 1.6 across wide, square, portrait, light/dark and complete/missing sensor inputs. This does not verify physical sensors, the installed app, USB output or a new installer. No driver, monitoring app or MSI was installed, and Defender settings were not changed.

After verification, 5,800 generated build/staging/package files (about 1.60 GB) and nine generated web files were deleted, including the old sensor build and MSI/ZIP payloads. Automatic approval rejected directory deletion, including the empty-directory cleanup; generated directory shells remain. Installed app files and settings were preserved.

On 18 October, I requested a real Windows x64 MSI. The first MSI installation returned Windows Installer status 0. I then requested a clean rebuild and removal of generated output, keeping only the final MSI. No tests or MSI validation were run for this packaging change. Upgrade, repair and uninstall behavior have not been tested.

Later on 18 October, Start opened an old studio from an uninstall backup. After the app closed, its WinRing0 driver was still running from the old install path. Normal process and read-access checks missed the loaded driver. A separate administrator stop and service deletion succeeded, and the service query then reported that it no longer existed.

MSI 0.1.2 adds visible setup/progress and scoped process/driver cleanup before files change, including when upgrading an older MSI. Cleanup checks service removal and generated driver-file deletion; a busy or pending deletion is an error. Installer execution, elevation/cancellation, rollback and physical-device behavior for this new path have not been tested.

Windows x64 restore, C# format verification, Release publishing and native setup-helper compilation passed for this change. I requested no test execution. No new installer or cleanup-helper execution, sensor read, USB trial or ARM64 build was performed.

PR #2 CI later failed ARM64 publishing because the Visual Studio 2026 runner lacked the forced v143 toolset. Photino now explicitly selects the installed default after importing C++ defaults, avoiding x64's legacy v100 fallback. Targeted native x64 and ARM64 builds passed locally with v145. This is compilation evidence only; no application, sensor or USB trial ran for this fix.

The owner reported frozen display updates when the studio was hidden or minimized. The installed engine log recorded successful presents but did not record window visibility or preview waits, so it could not establish the timing of that freeze. The presentation loop awaited the browser's JPEG acknowledgement before releasing its refresh gate. I separated that bounded preview task from engine refresh and skip hidden previews using native window visibility. Windows x64 restore, format verification and Release compilation passed. The isolated background-output fixture is prepared; execution needs separate owner approval. No installed-app or USB trial has run for this change.

The next refactor moves settings, saved startup, discovery, rotation and output into `VibePanel.Studio`, with no UI framework dependency. The view reads a single latest snapshot. Shutdown drains the worker before closing engine stdin; the single-instance lock remains held through engine cleanup. MSI setup uses a transaction-scoped launch gate, released on commit/rollback or custom-action-host exit. The five semantic palette roles support manifest defaults and saved per-theme light/dark overrides.

With owner approval, 17 model checks, 14 hidden-window startup checks, the blocked-dispatcher/background-window fixture and the native setup-gate fixture passed. The gate checks covered explicit release and owner-process exit, not an actual MSI rollback. Three palette checks passed, including 60 offscreen renders across all 30 themes and both appearances. Eighteen System checks passed, including 180 synthetic JPEG renders. The startup fixture was updated to allow a hidden preview to remain idle. No test opened sensors, audio or USB. These fixtures do not prove real Windows sign-in, physical display behavior or every installer failure path.

MSI 0.1.4 was installed through its visible wizard after removing 0.1.2. Both returned status 0. The old installer used its termination fallback for the running elevated app, then verified driver stop, service removal and runtime-file deletion. The 0.1.4 package was also removed and reinstalled successfully with the app closed. Installed model, UI, engine and palette files match the published payload; settings remain present. The app was left closed. The offline background-check Start shortcut is removed. One obsolete pre-MSI uninstall registration remains despite MSI's registry-removal action; its cleanup is unresolved. Real sign-in, in-place upgrade, repair, cancellation and rollback were not exercised.

Thermal Atlas and Airflow replace two System layouts while keeping their theme IDs. The Windows reader now asks the same Open Hardware Monitor instance for named motherboard temperatures and fan RPM from Super I/O, plus GPU fan RPM when exposed. Preview JPEGs use labeled synthetic data; they are not proof that this computer exposes those sensors. Windows x64 restore, format verification and Release build passed. Automated tests and physical sensor or USB trials were not run for this change. The installed 0.1.5 MSI predates these themes.

Both thermal layouts now use cached, blurred CPU, graphics-card and motherboard photos. These are decorative assets, not pictures of the installed hardware. Dark, light and compact preview JPEGs rendered with synthetic readings; no live sensor, USB or installer check was run for this visual update.

Photino's default toast registration created a second Start shortcut named after the window title. Toast registration is now disabled and the title is simply Vibe Panel. Windows x64 restore, format verification and Release build passed. No runtime or hardware tests were run for this naming change.

The next change adds transactional preferences/screens/palettes, versioned settings with backup recovery, typed ViewModel commands, and the portable `VibePanel.Data` provider boundary. Windows x64 restore, format verification and Release build passed. With owner approval, 35 offline model/persistence/provider checks, 14 hidden-window startup checks and the blocked-dispatcher/minimized/hidden-window fixture passed. No hardware provider ran. Linux CI now runs the same portable checks; local Windows results do not prove Linux or macOS runtime support. The engine host and physical providers remain Windows-only. No MSI was rebuilt or installed for this change.

## 19 October 2026: full sensor readout and System themes

An elevated read-only acquisition returned 43 devices and 448 sensors (433 finite readings). All 232 unique identifiers from the supplied report sensor tree were present; this is coverage evidence, not simultaneous value agreement. The supplied RTX 4060 report and acquired tree contain no GPU Power sensors. CPU Package power is present. All ten System layouts were rendered as local previews in light/dark and wide/compact sizes using acquired hardware readings. Preview inputs combined acquisitions from different times; they are not a synchronized hardware measurement. Restore, format verification and Release compilation completed. No automated tests or USB validation were run for these changes.

## 19 October 2026: paged themes and artist data

Windows x64 restore, format verification, complete Release build and self-contained publication passed. Owner-approved offline checks passed: 35 studio checks and 31 Python checks, including all 44 declared System/Music pages at two sizes and two font limits, plus 180 System render scenarios. Light/dark previews and the 16–256 px icon frames were inspected. Preview graphs use explicitly synthetic inputs. A live Coldplay lookup returned an attributed Wikipedia biography and Commons photo; no qualifying worldwide album-sales record was returned. Sales coverage is not established. The artist provider keeps missing fields absent. No physical display trial or installer run was performed for these changes at this checkpoint.

The eight C# scenes, their RPC/CLI fallback, old hardware background assets, unversioned-settings migration and implicit PATH interpreter lookup have been removed. The studio UI compiled; its on-screen layout has not been exercised in a running studio during this change.

## 19 October 2026: cooling instruments and animation

I replaced the long power/RPM bars in all six System cooling pages with watt readouts and named fan instruments. Outline fan blades turn only for positive measured RPM; zero stays visible and still. Visual rotation is slowed and capped. Codex work dots pulse only during live work. Both animations use the existing theme clock and chosen refresh; there is no extra timer or sensor polling.

At the owner's request, I rendered 108 local page previews covering all 30 themes in light and dark with synthetic data at font scale 1.5. I inspected the overview sheets, cooling pages, activity pages and artist pages, plus wide/compact Thermal Atlas and a 32-frame rotor storyboard. These are source-rendered design previews, using the existing Python/Skia runtime as a dependency. They do not validate real sensor data, sustained frame rate, an installed app or USB output. No automated tests, C# build, installer build, installation or physical-device trial ran for this change.

Automatic approval initially rejected removal of the generated `artifacts/theme-review` folder with a policy block. At that checkpoint it remained ignored by Git. Review images were also retained outside the repository; the existing MSI was left intact.

## 19 October 2026: 0.1.11 source build

At the owner's request, I restored the current projects from official NuGet, verified C# formatting and compiled/published Windows x64 Release from this repository, including Photino.Native and the updated cooling renderer. Missing build inputs came from their pinned official sources. Explicit targeted first-import scans completed without detections for their archives and extracted files. Hash evidence is saved outside repository output and reused during compilation; builds no longer launch scans. Generated app files and MSI files are not scanned as external imports.

No automated tests, app launch, installation, sensor acquisition or USB trial ran. This compilation does not establish physical display behavior or animation frame rate. Build logs and package checksums are retained outside the repository, which keeps only the final requested MSI after cleanup.

MSI 0.1.11 compiled successfully from that fresh payload. The guarded cleanup completed and removed the old MSI, ZIP, preview folder, staged dependencies, build tools, intermediates and generated web files. Only `artifacts/packages/VibePanel-win-x64-0.1.11.msi` remains. No installer execution or MSI validation ran.

## 19 October 2026: shared progress colors and completion choices

Circular and linear progress now share a theme-to-amber-to-red scale across System meters, playback and audio. Colors use the shown range, not guessed hardware alarm thresholds. The successful MSI completion page has two independent checked choices: start the app and open GitHub Releases with fixed installer UTM tags. Finish dispatches them after commit, with matching guards in the native actions. Silent setup, repair, removal, failure and cancellation do not dispatch them.

These changes have not been exercised through a real installer. No automated tests, app launch, browser launch, sensor read or USB trial is authorized by this implementation checkpoint. Compilation and local synthetic design previews are separate evidence, not installer or hardware validation.

Windows x64 Release source publication, both native setup targets and MSI 0.1.12 compilation completed. All unchanged dependency inputs reused saved first-import hashes without a scan. I rendered 108 page previews for all 30 themes in light/dark plus four compact frames at font scale 1.5, and inspected the System progress and Multimedia playback galleries. Compact units and the wide RAM readout now fit their reserved slots.

Read-only MSI table inspection confirmed version 0.1.12, both option properties set to 1, independent guarded Finish events, and one active success dialog. No installer or custom action was executed. This does not verify the displayed Windows dialog, default-browser behavior or actual app startup after installation.

Output cleanup completed. The repository keeps only the final 0.1.12 MSI; its SHA-256, build logs, MSI table snapshot and design previews are saved outside the repository. Installed files and user settings were left intact.

## 19 October 2026: administrator launch and progressive disclosure

Windows startup now requests elevation before creating the studio's child processes.
Canceling shows an explicit non-administrator warning; saved-session and tray flags
are forwarded on success. Setup explains the request and that themes share these rights.
Fan pagination keeps one measured running fan visible while other channels rotate.
This addresses the stopped-only page visible in the owner's screenshot; it does not
establish that every possible sensor initialization issue has been reproduced.

The studio now starts with setup, appearance, display options and diagnostics closed.
Short connection/access summaries remain visible. Sensor Debug preserves expansion
choices on refresh and initially leaves devices and groups collapsed.

No automated tests, UAC dialog, real studio launch, installation, sensor read or USB
trial has been run for these changes. Compilation is separate from runtime validation.

Windows x64 restore, source format verification, Release publication and native/MSI
compilation completed for 0.1.13. Unchanged dependencies reused their saved first-import
hashes. Read-only MSI metadata confirms the administrator notice on the completion
page and both completion choices still checked by default. This does not validate
the real UAC, studio layout or installer interaction. Build logs, hashes and metadata
are saved outside the repository. Cleanup left only the final 0.1.13 MSI in artifacts;
installed files and user settings were not changed.

## 19 October 2026: fan channels observed running

The Windows reader now filters theme fan channels by actual positive RPM history,
keyed by the sensor library's identifier. Never-active zero channels stay hidden;
previously active channels retain a later measured zero. Null or invalid readings
remain absent. Raw Sensor Debug stays unfiltered and its positive observations
also qualify a channel. The bounded history belongs to the engine session, survives
page/theme changes and renderer restarts, and is not saved across app restarts.

No tests, studio launch, sensor sampling, driver access, installation or USB trial
has been performed for this change. Compilation does not establish live behavior.

Windows x64 restore, format verification, Release compilation and source publication
completed, followed by native setup and MSI 0.1.14 compilation. The published core
matches the current Release assembly. Unchanged dependencies reused existing
first-import evidence. Build logs, source/package hashes and receipts are saved
outside the repository. Cleanup retained only the final 0.1.14 MSI under artifacts.

## 19 October 2026: grouped sensor details and device filtering

The shared System renderer now declares eight pages, omitting unavailable details.
It shows electrical readings, named temperature channels including GPU hotspot,
aggregate clocks, RAM model/serial/slot identities and separate SPD timing limits.
Board readings now reach themes. Individual core/thread rows are omitted from themes.
Storage collection is disabled. Network diagnostics match only physical Ethernet/Wi-Fi
through Windows adapter metadata; unknown identities remain hidden with a notice.
Child sensor chips are visited before later roots can fill the 64-device budget.

The owner's saved LibreHardwareMonitor report was inventoried privately, without a
fresh acquisition. Static source and supplied-file inspection is separate from live
sensor validation. No tests, Python rendering, application launch, installer execution,
driver access or USB trial has been performed for these changes.

Static inspection found an upstream NCT6792D temperature-decoding defect and missing
exact-board mapping. This package retains the official library and does not repair
that decoder. Generic board values remain raw, not confirmed physical temperatures.
See [sensor mapping](docs/sensor-mapping.md). Proprietary HWMonitor resources were
inspected only as data; no code or driver was imported or executed.

Windows x64 restore, format verification for Core/Data, Release compilation and
self-contained source publication completed with no build warnings or errors.
Native setup targets and MSI 0.1.15 compiled successfully. Published Core and Python
sources match the current repository build inputs. Unchanged dependencies reused
their existing first-import hashes. The guarded cleanup retained only the final MSI;
logs, checksums and receipts are saved outside the repository. No tests or runtime
validation were added by packaging. Installed files and settings remain unchanged.

## 19 October 2026: LibreHardwareMonitor contribution

The owner authorized a separate fork and upstream contribution. Fork commit
`e5fa436eae6cb9ad5e8693d9909cd81ddf473629` routes NCT6792D through the existing
masked-source decoder, which skips empty register entries and retains valid
fractional samples. The exact B550M-ITX/ac mapping also exposes SMBUSMASTER0.
Source names remain technical; physical CPU/mainboard/chipset aliases are unverified.
[Draft PR #2590](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/2590)
contains the saved-register evidence without the owner's full hardware report.

Restore and a full Release net10.0 x64 rebuild with SDK 10.0.402 completed, with no
rebuild warnings or errors. Whitespace verification reports only existing issues
on unchanged lines, also present in the unmodified upstream base. Targeted
first-import Defender scans completed without detections; unchanged dependencies
reused recorded hash evidence. No tests, application launch, live sensor reads,
driver operations or installation were performed. Vibe Panel 0.1.15 retains the
official library and does not yet include this fork. Upstream CI requires action
before it can run; the contributor account cannot assign upstream labels or users.

## 19 October 2026: fork integration and RAM details

Vibe Panel now builds the pinned LibreHardwareMonitor fork from source in this
repository. Its original C# files, embedded modules, license and editor settings
are preserved. The project file targets .NET 10 and repository-local build output.
The distribution includes the exact MPL-covered source. No upstream GUI or driver
installer is added. Previously scanned unchanged inputs reuse their hash evidence;
new NuGet inputs passed a separate targeted scan before compilation.

Each physical RAM module now carries its own reported type and configured rate.
System themes use a clear model/serial hierarchy, a small DIMM outline and rotating
compact summaries. Temperature details use the shared color scale and disclose
their 0–120°C display range. Raw SYSTIN remains a detail channel, without being
promoted to a confirmed motherboard temperature. No physical alias is guessed.

Restore, changed C# formatting and the Core/library Release build completed.
No tests, renderer execution or hardware validation was performed during these
changes. Installation and package evidence are recorded separately below.

Windows x64 source publication, native setup compilation and MSI 0.1.16 completed.
The old 0.1.12 MSI was uninstalled with exit 0; the new MSI installed with exit 0
through its visible UI. One 0.1.16 registration remains, and all 1,822 installed
payload files match the fresh build. The installed sensor library identifies as
`0.9.6-vibepanel.e5fa436`. The initial install invocation rejected a forward-slash
package path before installation; retrying the resolved Windows path succeeded.
Cleanup retained only the final MSI. Build/install logs and hashes are outside
repository output. No automated tests or agent-issued sensor/USB trials ran.

After installation, the owner reported checking the patch on this local hardware
through Vibe Panel and seeing values matching CPUID HWMonitor. This owner-observed
comparison is recorded in upstream PR #2590; it does not establish physical chip
source aliases or coverage of other boards.

## 19 October 2026: local board display profile and fewer System pages

Vibe Panel now applies owner-approved Mainboard, Southbridge and CPU labels only
to the ASRock B550M-ITX/ac NCT6792D instance 0 theme readings. Other temperature
inputs from that chip remain in Sensor Debug. Raw library sensors, measurements,
the pinned fork and its upstream PR are unchanged. These display labels follow
the owner's comparison; physical board wiring has not been independently verified.

System pack 2.1.2 has six pages instead of eight. Electrical readings and aggregate
clocks share one page; memory identities and distinct SPD sources share another.
Balanced detail banks avoid lone trailing rows. Static review covered independent
memory counters and access to all groups in wide and compact layouts.

Restore, changed C# formatting and the engine/CLI Release build completed with no
warnings or errors. The prepared C# checks also compile; no tests were executed.
Python source and JSON manifests passed syntax parsing without importing the app
or renderer. No rendering, live sensor trial, new MSI or installation was performed
for this change. The installed 0.1.16 and its existing MSI do not contain this update.

## 20 October 2026: cooling power card

The shared System cooling card now uses aggregate CPU power readings and available
CPU voltage and current, with their measured units. It excludes named limits and
individual cores. The installed build has not been updated for this change. No
renderer preview, automated tests, live sensor trial or USB output was run.

## 20 October 2026: System detail trends

Electrical and temperature rows use bounded, timestamp-deduplicated snapshot history
for their curves and window extrema. The curve preserves sampled values; no new
sensor polling is added. Missing or stale readings clear their histories. The
installed build does not contain this change. No renderer preview, automated tests,
live sensor trial, USB output, build or installation was performed for this change.

## 20 October 2026: local Debug 0.1.18

The Windows x64 Debug payload and MSI were built from source at revision 73dca8b.
The previous MSI was removed and 0.1.18 installed through Windows Installer; all
1,823 installed payload files matched the fresh build. The app started afterwards.
The MSI SHA-256 is 961645CC23E13F541FEE3EA4153A67FF3A6D41DAD7A77FE45330532CEEBF3D64.
No automated tests, renderer previews, or agent-issued sensor or USB trials ran.

## 20 October 2026: Thermal Atlas page timing

Thermal Atlas omits Power & cooling and declares fifteen seconds for Live activity
as its first page. Temperature channels is second for twenty seconds, followed by
eight seconds per page. The shared page clock supports individual durations and
retains the default for other themes.
Boundary and skipped-page checks were added but not executed. The Windows x64
Debug 0.1.21 MSI was built from current source and installed through its visible
Windows Installer UI. All 1,825 installed payload files matched the fresh build.
The MSI SHA-256 is C68D156DC0357E90F9AC8BB07C0800EFA3544C8C7746275F3A43E09EBEA9390F.
No renderer preview, automated test, live sensor trial or USB output was run.

## 20 October 2026: per-core heatmaps and longer fades

System pack 2.2.0 adds the same optional core page to all ten themes. It matches
named per-core power and load readings, averages multiple load threads for each
physical core, requires at least eight matched cores and displays at most 32.
Power colors are relative to the currently visible core range; load uses 0–100%.
The power grid outline uses the reported general CPU temperature when available.
Missing or unmatched readings keep the page out of rotation.

Page and Multimedia track/cover crossfades now last two seconds. Checks were
updated for the new contracts but not executed. No renderer preview, live sensor
trial, build, installation or USB output was performed for this change.

## 20 October 2026: studio page navigation

The preview area now has separate theme and page arrow groups. The engine returns
the title and position of the page it actually rendered, including after unavailable
pages are filtered out. A manual page choice restarts that page's interval and then
normal rotation continues. The selection is session-only; a theme change starts its
first available page.

Contract and offline checks were updated but not executed. Static Python parsing,
changed-file C# format verification and the full ARM64 Release build completed;
the build had no warnings or errors. Whole-solution format verification still
reports pre-existing whitespace/import differences in the pinned LibreHardwareMonitor
fork, which was left unchanged. No live studio visual check, sensor trial,
installation or USB output was performed for this change.

## 21 October 2026: mandatory local reinstall and Debug 0.1.22

The repository now treats every local compilation as a Debug reinstall unless the
owner explicitly asks for an exception. The standing rule authorizes removal of the
installed version and installation through the normal visible MSI UI while keeping
settings and user data. Tests and publication remain separately authorized work.

The previous 0.1.21 MSI was removed and Debug 0.1.22 was built from current source.
The visible installer completed with optional app launch and release-news actions
turned off. One 0.1.22 MSI registration remains, and all 1,825 installed payload
files match the fresh build. The app was left closed, so no sensor or USB trial ran.
Cleanup retained only `artifacts/packages/VibePanel-win-x64-0.1.22.msi`; its SHA-256
is `C1E9DAE16DFC2D3C55B235EA4E9BF0C9452AA999E221D68434BFDCB1A5F524BE`.

## 21 October 2026: page navigation and renderer checks

All 46 checks in `check_theme_pages`, `check_hardware_themes`,
`check_track_transition` and `check_palette` passed with offscreen rendering
enabled. They ran against repository source using the installed Python runtime
and synthetic fixtures. The checks cover page timing, heatmap availability,
JPEG dimensions and size limits, palette overrides and transition pixels.
The pack assertion now includes the core page and Thermal Atlas's cooling-page
exception; independent JPEG cases reset the page clock and transition state.
No live studio visual check, sensor trial or USB output was performed.

## 21 October 2026: Thermalright VibeWare rename

The product is now Thermalright VibeWare. Code projects, namespaces, binaries,
the CLI (`trv`), Codex plugin, theme footers, installer and current guides use
the new identity. The existing GitHub repository was renamed to
`umbertotechnopreneur/thermalright-vibeware`, with the owner accepting GitHub's
automatic redirect. There are no old-name aliases or settings migrations.
Historical entries above and dependency scan evidence keep their original names.

Windows x64 was built from current source in Debug. Restore and source format
verification completed successfully. Unchanged dependency inputs reused their
recorded first-import scan evidence. The previous MSI was removed through its
visible UI, and the new 0.1.23 MSI completed visible installation with exit 0.
All 1,834 installed payload files match the fresh build by SHA-256. There is one
new MSI registration and no old MSI registration. Start and sign-in shortcuts
point to the new executable; sign-in retains `--start-minimized
--restore-last-session`. Existing user data remains outside the new settings
directory. The old cleanup log records completed scoped cleanup, and the
OpenHardwareMonitor service registration is absent.

The final package is `artifacts/packages/ThermalrightVibeWare-win-x64-0.1.23.msi`.
Its SHA-256 is
`3C6774E07FA107FABB18D44BF951D9789EB9C389D5DF352AA63F357A256F5011`.
The installer source checkpoint is `fe49056`. Build output is cleaned while
retaining this MSI. No tests, CI runs or USB output commands were executed for
this change.

Nine owner-supplied app captures use 1280 × 480 display crops and 1280 × 864
studio crops, with no added matte. They keep their
original UI text and readings, with conventional crop, resize and light
sharpening. The product icon is a first-party display and sensor trace; the
VibeWare initiative mark is unchanged.

The personal site's source now includes the project, images and independent
brand disclaimer in English, Italian and Vietnamese. Its Node 24 static build
and changed-file lint completed successfully; generated canonical URLs and
language alternates match the new pages. New npm archive and extracted-package
inputs completed targeted Defender scans without detections. The site's
pre-existing edited `dist` files were preserved. The site has not been deployed
as part of this change. UlanziDock keeps its existing name and repository;
its README and site page gained the requested independent-brand disclaimer.

### Delivery follow-up — 21 October 2026

App PR #6 was merged after Windows x64, Windows ARM64 and Linux checks passed
in run `39630640187`. The main-branch run `39632641215` also passed. The earlier
Windows checkout path failure was fixed by shortening the extracted native
source path; it was not overridden. README and screenshot corrections were
delivered separately with `[skip ci]` under the documentation-only rule.

Personal-site PRs #5 and #6 were merged. Production deploy `39633297405`
completed successfully. HTTP reads confirmed the new English, Italian and
Vietnamese copy and three pairs of screenshots. All six displayed image files
match the local owner-supplied crops by SHA-256. Ulanzi's disclaimer was pushed
without changing its project name. No physical-device test was performed.

### Script layout and Quick Start - 25 October 2026

The scripts root now contains two entry commands, with helpers grouped under
build, install, maintenance and tools. CI, MSBuild, packaging and the Python
memory-check import use the new paths. Package install/uninstall filenames
are unchanged. The Quick Start PDF and its Markdown text are under docs and
linked from the README and portable guide; publishing already includes docs.

Static parsing found no syntax errors in the ten PowerShell scripts, the two
Python tools and their changed test importer, or the two changed project XML
files. No build, installer, automated test, antivirus scan or physical-device
command was run for this change. The PDF was rendered and visually inspected.

### Source maintenance and first launch - 27 October 2026

First-party headers now describe file purpose and actual AI provenance. Studio,
CLI, capture and Python drawing responsibilities are split into smaller files.
Static inspection parsed 515 C# methods/constructors with matching XML parameter
entries, 195 documented Python functions and ten PowerShell scripts. Python
function bodies/signatures match their prior AST after excluding docstrings.
Native installer code tokens match the prior source. Project/XAML XML and the
App's explicit Compile entries were inspected. JavaScript syntax and the new
first-launch check sources also parsed. These are source inspections, not test
execution or runtime observations.

The first-launch flag defaults to true. The loaded main page requests durable
completion; only a successful settings save changes it to false. Later Windows
launches start in the tray, with the existing reopen and missing-tray fallback.
Offline checks were added for defaults, persistence, repeat completion and
unwritable/damaged settings. They have not been executed. First-launch, tray,
sign-in, administrator restart and installed behavior have not been observed.

Restore and changed-file format verification could not start: global.json pins
SDK 10.0.402 with roll-forward disabled, while the installed 10.0 patch is
10.0.403. The pin was preserved. No Debug build, uninstall/reinstall, CI,
publication, imported dependency, driver action or hardware trial was performed.
This work does not establish Microsoft Store readiness.
