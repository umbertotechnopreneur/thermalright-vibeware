<!-- VBWR B
Project: Thermalright VibeWare
Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
Purpose: Documents the Linux sensor stage and its evidence limits.
Copyright (c) 2026 Umberto Giacobbi
SPDX-License-Identifier: MIT
VBWR E -->

# Linux sensors

This stage adds a read-only Linux sensor backend in source. It does not add Linux
USB output, audio, media sessions or a working Linux studio. Available readings
depend on the installed kernel drivers and the current user's permissions.

Windows keeps its existing LibreHardwareMonitor fork, PawnIO integration and RAM
reader in `ThermalrightVibeWare.Backends.Windows`. Linux uses a separate
`ThermalrightVibeWare.Backends.Linux` project. Both implement `ISensorProvider` and
return the same theme snapshots and raw diagnostic schema.

## Readings

- CPU usage comes from consecutive aggregate `/proc/stat` samples. The first
  sample has no usage percentage. Guest time is not counted twice.
- Physical RAM totals and available memory come from `/proc/meminfo`. Swap is not
  counted as physical RAM. DIMM model, serial, physical slot and SPD details stay
  unavailable without a supported source.
- Temperatures, RPM, voltage, current, power and frequencies use the kernel's
  hwmon interfaces when exposed and readable. GPU coverage depends on the
  driver's hwmon and DRM interfaces; no vendor library is installed.

The kernel documents [procfs](https://docs.kernel.org/filesystems/proc.html),
[hwmon units and labels](https://docs.kernel.org/hwmon/sysfs-interface.html) and
[AMDGPU monitoring interfaces](https://docs.kernel.org/gpu/amdgpu/thermal.html).

Missing or unreadable channels remain unavailable. Raw debug keeps missing values
and source labels. Themes hide per-core rows and a fan until that exact channel
first reports positive RPM in the current provider session. A later measured zero
remains a stopped fan. Storage sensors are excluded. Network collection is not
enabled in this stage.

The Windows ASRock/NCT6792D display profile does not apply to Linux channel numbers.
Source labels remain visible. Voltage readings are the kernel's input values:
this backend does not apply board-specific resistor scaling from `sensors.conf`
or infer a named power rail. Generic ACPI temperatures are not presented as CPU
package temperatures.

Construction and access-status requests take no sensor samples. Explicit reads
share a one-second cache with bounded input and output sizes. No timer, driver
installation, module loading, SMBus probe or sysfs write is added. Permission
failures remain visible; the backend does not request elevation.

Raw diagnostics allow at most 64 devices and 1,024 readings, including CPU and RAM
counters. Truncation is reported. Stable sensor IDs use a hash of the canonical
device path and chip identity, so changing a `hwmonN` index does not activate a
different fan. Ambiguous duplicate identities are skipped with a notice.

## Entry points and validation

The Linux CLI target is `net10.0`. Its sensor entry points are `trv sensors`,
`trv sensors --diagnose` and `trv sensors --all`. The JSON-lines engine exposes
`hello`, `sensors.debug`, `sensors.driver.status` and `stop`. Other operations fail
immediately. Sensor commands read hardware; they are not automated build checks.
The Linux help lists only the implemented commands.

The Windows CLI target remains `net10.0-windows10.0.19041.0`. Windows publishing
selects that framework explicitly.

This is pre-beta implementation work. Tests and runtime trials need explicit owner
approval. Local compilation also follows the
Debug/reinstall rule in [AGENTS.md](../AGENTS.md), unless the owner approves an
exception for this Linux stage. Build success alone cannot prove sensor access.
