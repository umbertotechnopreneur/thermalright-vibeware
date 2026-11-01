# System sensors

On Windows, Thermalright VibeWare builds its CPU, GPU and motherboard reader from
[LibreHardwareMonitor fork e5fa436](https://github.com/umbertotechnopreneur/LibreHardwareMonitor/tree/e5fa436eae6cb9ad5e8693d9909cd81ddf473629).
This includes the NCT6792D decoder repair and ASRock B550M-ITX/ac source mapping.
The MPL-2.0 library source is in `third_party/LibreHardwareMonitor`, and distributions
include it under `sources/LibreHardwareMonitor`. Its project is adapted for .NET 10;
sensor code and embedded modules match the pinned fork. The GUI and driver installers
are excluded.

CPU and motherboard temperatures and fan RPM need PawnIO. Its official signed edition
is available from [pawnio.eu](https://pawnio.eu/). Thermalright VibeWare does not install it.
The System sidebar shows a brief access status. Expand **Sensor access** for
registration details and the driver path when available.
**Open driver folder** reveals that location; it does not execute or allow the driver.
Registration is not proof that every sensor can be read.

GPU readings and physical RAM can remain available without PawnIO. RAM is read with
Windows' [GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex);
physical totals remain independent of SPD access. Following owner approval, the
library's RAM/SPD group is enabled. `RAMSPDToolkit-NDD` contains no default driver;
LibreHardwareMonitor supplies a PawnIO adapter that opens its embedded SMBus
modules through the existing PawnIO installation. Thermalright VibeWare installs no driver.
No values are inferred from load or ACPI thermal zones. Zero RPM is a measured stop;
an unavailable RPM is a missing reading. Technical motherboard source names remain
visible when their physical location is unknown.

Thermalright VibeWare requests administrator access on every Windows launch, including sign-in.
Windows asks for approval. If canceled, a popup warns that the app is running without
administrator rights and some readings may be unavailable. **Sensor access** and
**Sensor Debug** offer **Reopen as administrator**. Themes run with the app's rights.
The request keeps the saved display session and tray choice. Elevation does not install a missing
driver or bypass a Windows security block. Windows ARM64 CPU/motherboard access remains
disabled pending implementation evidence. A separate [Linux sensor backend](linux-sensors.md)
is available in source for CLI and stdio diagnostics. It has not been validated on
hardware. Linux USB, audio and the complete studio are not implemented. macOS has
no sensor provider.

## Sensor Debug

Open **Sensor Debug** beside **Themes**, then press **Refresh readings**. The tree
shows collapsed devices and sensor groups. Expand what you need to see subdevices
and all reported sensor types, including RAM DIMMs and
timings, voltages, power, temperatures, fan RPM, control percentages,
physical Ethernet/Wi-Fi, batteries and supported controllers. Current, minimum and maximum values
come directly from the library. Control percentages are displayed, never changed.
Sensor rows also expand to show available calibration parameters and defaults,
with no editing controls. At most 512 calibration parameters are returned.
Unknown names and unavailable values are kept. A refresh is manual, cached for one
second, and limited to 64 devices and 2,048 sensors with an explicit truncation notice.

Storage collection is disabled. Virtual, VPN, Bluetooth, debugger and filter network
interfaces are omitted before update and device limits. Physical network identity is
matched by Windows adapter GUID, not a translated display name. Disconnected physical
adapters remain eligible. If Windows metadata is unavailable, uncertain entries stay
hidden with a notice. Adapter metadata is cached until the engine restarts.

`trv sensors --all` reads the same raw schema once without audio or USB. The
`sensors.debug` JSON-lines request uses the engine's existing sensor reader. No
network listener is added. Some devices need administrator rights; the header
shows the privilege level and sample time. The existing System action can reopen
the studio as administrator. Min/max values describe this reader's lifetime.

The old Open Hardware Monitor/WinRing0 integration has been removed from the source and
new payload. Microsoft documents the
[WinRing0 vulnerable-driver alert](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/microsoft-defender-antivirus-alert-vulnerabledriver-winnt-winring0).
Do not treat that alert as proof that an exception is safe. Older installed versions
still use their own files until separately updated; source changes do not modify them.

## System theme layout

All ten System themes use one renderer. Names, primary values, clock lines and details have separate regions in wide and compact layouts. CPU/GPU power readings use watts; GPU channels retain their names and rotate in bounded pairs. Missing fields are omitted. RAM configured data rate (MT/s) and measured memory clock (MHz) stay distinct. Generic board channels retain their reported names. Sensor Debug retains the complete bounded tree, including missing readings.

Six pages progressively expose overview, activity, cooling, hardware readings,
temperature channels and memory. Power, voltage, current and aggregate clocks share
one hardware page. RAM identities and SPD timing limits share one memory page. Pages
without relevant readings are skipped. Detail cards show at most four readings at a
time; groups rotate using the existing theme clock. Individual CPU cores and threads
are omitted from themes. Voltage and current remain numeric readings, with no guessed
safe range; separate power readings are never summed.

RAM cards put the model code first, with an available serial below in smaller type.
Each physical module can also show its RAM type, capacity and configured data rate in
MT/s. Windows supplies these details and physical slot and bank labels. The configured
rate is distinct from a measured clock in MHz. SPD supplies module identity and timing
limits; these are not live memory-controller timings. Identical DIMMs cannot be matched
to physical slots by list order. Their SPD details remain separate, under the library's
original device names. A missing serial is omitted.

The NCT6792D decoder and Sensor Debug retain technical chip source names. The exact
ASRock B550M-ITX/ac + NCT6792D instance 0 theme profile uses owner-approved Mainboard,
Southbridge and CPU labels, retaining other temperature inputs in debug only. It
changes no measurements and does not apply to other chips or controllers. See the
[decoder correction and display profile](sensor-mapping.md) for evidence limits.

Themes hide a fan channel until its first positive RPM reading in the current
engine session. Activity is remembered by sensor identifier across page and theme
changes, including renderer restarts. A later measured zero remains visible as a
stopped fan. Missing readings stay absent. No last-known RPM is substituted.
Sensor Debug keeps every raw channel, including those that have always been zero;
positive readings observed there also count for the same session.

Fan pages keep one running channel visible while the other eligible channels rotate.
Stopped fans retain their names and zero RPM, with still rotors. A page containing
only stopped fans can appear only after those channels have actually been active.
