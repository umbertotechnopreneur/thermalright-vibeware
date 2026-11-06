// VBWR B
// TermalRight VibeWare | Umberto Giacobbi | SPDX-License-Identifier: MIT
// Pure GPU snapshot composition, with no native or kernel access.
// VBWR E

using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Core;

internal static class LinuxGpuCompositionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        HardwareReading[] kernel =
        [
            new("kernel:gpu/temp1", "GPU edge", "Temperature", 52.25, "°C"),
            new("kernel:gpu/clock1", "GPU clock", "Clock", 1200, "MHz"),
            new("kernel:gpu/fan1", "GPU fan", "Fan", 0, "RPM"),
            new("kernel:gpu/in1", "GPU voltage", "Voltage", 1.05, "V")
        ];
        SensorReadingDto[] native =
        [
            new("nvml:gpu/memory-load", "Memory controller busy", "Load", "%", 75, null, null),
            new("nvml:gpu/load", "GPU busy", "Load", "%", 0, null, null),
            new("nvml:gpu/temp", "GPU temperature", "Temperature", "°C", null, null, null, "Unsupported fixture reading"),
            new("nvml:gpu/clock", "GPU graphics clock", "Clock", "MHz", 1410, null, null),
            new("nvml:gpu/fan", "Fan target speed", "Control", "%", 42, null, null)
        ];
        var nvidia = new LinuxNvmlDevice("nvml:gpu", "NVIDIA fixture GPU", "0000:06:00.0", native, null);
        var gpu = LinuxMetricsService.ComposeGpu("Kernel fixture GPU", kernel, nvidia);
        var projected = gpu.Readings ?? throw new InvalidOperationException("Missing composed GPU readings.");
        check(gpu.Name == "NVIDIA fixture GPU", "A joined GPU retains its NVML model identity.");
        check(gpu.UsagePercent == 0, "GPU utilization comes from GPU busy, not memory-controller load.");
        check(gpu.TemperatureC == 52.25 && gpu.TemperatureLabel == "GPU edge",
            "An unavailable NVML temperature does not erase an available kernel temperature.");
        check(projected.Select(reading => reading.Id).ToHashSet(StringComparer.Ordinal)
            .SetEquals(kernel.Select(reading => reading.Id).Concat(native.Where(reading => reading.Value is not null).Select(reading => reading.Id))),
            "Joining GPU sources preserves every available source identifier without inventing unavailable values.");
        check(kernel.All(projected.Contains), "Kernel GPU reading labels, types, units and values are preserved.");
        check(gpu.Fans is { Count: 1 } && gpu.Fans[0].Name == "GPU fan" && gpu.Fans[0].Rpm == 0,
            "Only a supplied tachometer becomes a fan; a previously active kernel fan may retain a measured zero.");

        var nativeOnly = LinuxMetricsService.ComposeGpu("Unused kernel name", [], nvidia);
        check(nativeOnly.Fans is { Count: 0 } && nativeOnly.Readings!.Any(reading => reading.Id == "nvml:gpu/fan" && reading.Type == "Control"),
            "NVML fan percentage remains available without creating a fan RPM reading.");
        var unavailable = nvidia with { Readings = native.Select(reading => reading with { Value = null }).ToArray() };
        var empty = LinuxMetricsService.ComposeGpu("Unused kernel name", [], unavailable);
        check(empty.UsagePercent is null && empty.TemperatureC is null && empty.ClockMHz is null && empty.Fans is { Count: 0 },
            "An NVIDIA device with unavailable observations does not gain zero-valued replacement metrics.");
    }
}
