<!-- VBWR B
Project: Thermalright VibeWare
Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
Purpose: Documents the Linux sensor stage and its evidence limits.
Copyright (c) 2026 Umberto Giacobbi
SPDX-License-Identifier: MIT
VBWR E -->

# Linux sensors

The Linux studio supports local theme previews and read-only sensor access.
USB output, audio and media sessions remain unavailable. Readings depend on the
installed kernel and NVIDIA drivers, supported channels and user permissions.

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
  driver's hwmon and DRM interfaces.
- NVIDIA GPUs also use the installed driver's `libnvidia-ml.so.1` through NVML.
  The app does not bundle or install NVIDIA libraries. Explicit samples read GPU
  and memory-controller load, GPU temperature, board power, graphics/memory
  clocks and VRAM totals when supported. Unsupported or denied channels stay
  unavailable. Fan output is the driver's intended percentage, never measured RPM.
  NVML and hwmon keep separate identities in Sensor Debug. Theme GPUs are joined
  by PCI identity, preserving the source of each reading.

Sensor access lists detected kernel modules, exposed non-storage hwmon chips,
GPU driver bindings and NVML availability. It reads metadata without querying
sensor values, caches that metadata for five seconds and reports the latest
sample separately. The guidance button explains kernel and driver support,
lm-sensors, permissions, board conversions and the missing Linux DIMM/SPD reader.
No module loading, detection scan or permission change runs from this panel.

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

Sensor Debug shows the raw thermistor voltage in mV for VT1211 channels 3–7,
VT8231 channels 2–6 and PC87365/PC87366 channels 4–6. Those channels need a
board-specific conversion to produce a temperature. The external diode on
VT1211 and VT8231 channel 1 also needs calibration, so its temperature stays
unavailable. VT1211 channel 2 and the digital temperature channels on the
PC87365/PC87366 keep their kernel temperature values. See the
[VT1211 driver guide](https://docs.kernel.org/hwmon/vt1211.html#temperature-monitoring),
[VT8231 driver](https://github.com/torvalds/linux/blob/master/drivers/hwmon/vt8231.c)
and [lm-sensors chip configurations](https://github.com/lm-sensors/lm-sensors/blob/master/etc/sensors.conf.eg).

Construction and access-status requests take no sensor samples. Explicit reads
share a one-second hwmon/NVML cache with bounded input and output sizes. No timer, driver
installation, module loading, SMBus probe or sysfs write is added. Permission
failures remain visible; the backend does not request elevation.

Raw diagnostics allow at most 64 devices and 1,024 readings, including CPU and RAM
counters. Truncation is reported. Stable sensor IDs use a hash of the canonical
device path and chip identity, so changing a `hwmonN` index does not activate a
different fan. Ambiguous duplicate identities are skipped with a notice.
During a read, the Linux backend keeps the monitor and physical-device directories
open. The four thermistor chips above can expose their attributes on the physical
parent. GPU metadata is read from the pinned device with its sensor values.
If the device disappears, its class link changes or its GPU driver binding changes,
the backend drops the sample before updating fan history. This requires access to
the process's own directory descriptors under `/proc/self/fd`.

## Entry points and validation

The Linux CLI target is `net10.0`. Its sensor entry points are `trv sensors`,
`trv sensors --diagnose` and `trv sensors --all`. The JSON-lines engine exposes
`hello`, `themes`, `fonts`, `state`, `stop`, `codex.status`, `diagnostics.status`,
`diagnostics.set`, `sensors.debug` and `sensors.driver.status`. `hello` declares
these supported methods before the studio schedules work. Local previews use the
packaged Python/Skia dependencies. USB, media controls and audio requests fail
immediately. Sensor commands read hardware; they are not automated build checks.
The Linux help lists only the implemented commands.

The Windows CLI target remains `net10.0-windows10.0.19041.0`. Windows publishing
selects that framework explicitly.

This is pre-beta implementation work. Tests and runtime trials need explicit owner
approval. Local Linux compilation uses Debug without installation, as described
in [AGENTS.md](../AGENTS.md). Build success alone cannot prove sensor access.
