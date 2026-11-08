/* VBWR B
 * TermalRight VibeWare | Umberto Giacobbi | SPDX-License-Identifier: MIT
 * Joins explicit NVIDIA and kernel observations without duplicate theme GPUs.
 * VBWR E */

using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Core;

public sealed partial class LinuxMetricsService
{
    private IReadOnlyList<HardwareDeviceSensors> ComposeGpus(IEnumerable<KernelDevice> devices, IReadOnlyList<LinuxNvmlDevice> nvidia)
    {
        var result = new List<HardwareDeviceSensors>();
        var included = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in devices.Where(device => device.Kind == SensorKind.Gpu).GroupBy(device => device.DevicePath, StringComparer.Ordinal).Take(8))
        {
            var pci = LinuxNvmlSensors.NormalizePciBusId(Path.GetFileName(group.Key));
            var matching = pci is null ? null : nvidia.FirstOrDefault(device => device.PciBusId == pci);
            if (matching is not null) { included.Add(matching.Id); }
            result.Add(ComposeGpu(group.First().Name, ThemeReadings(group).ToArray(), matching));
        }
        foreach (var gpu in nvidia.Where(device => !included.Contains(device.Id)).Take(8 - result.Count))
        { result.Add(ComposeGpu(gpu.Name, [], gpu)); }
        return result;
    }

    internal static HardwareDeviceSensors ComposeGpu(string kernelName, IReadOnlyList<HardwareReading> kernelReadings, LinuxNvmlDevice? nvidia)
    {
        // Preserve source identity in every joined reading. No query result is invented.
        var native = nvidia?.Readings.Where(reading => reading.Value is not null)
            .Select(reading => new HardwareReading(reading.Id, "NVML · " + reading.Name, reading.Type, reading.Value, reading.Unit)).ToArray() ?? [];
        var readings = native.Concat(kernelReadings).Take(MaximumDeviceReadings).ToArray();
        var primary = SelectTemperature(readings);
        var clock = readings.FirstOrDefault(reading => reading.Type == "Clock");
        var load = readings.FirstOrDefault(reading => reading.Type == "Load" && reading.Name.Contains("GPU busy", StringComparison.OrdinalIgnoreCase));
        return new(nvidia?.Name ?? kernelName, load?.Value, clock?.Value, primary?.Value,
            ClockLabel: clock?.Name, TemperatureLabel: primary?.Name, Fans: ThemeFans(readings), Readings: readings);
    }

    private HardwareDriverStatus AccessWithSampleStatus(HardwareDriverStatus metadata)
    {
        var sources = metadata.Sources?.Select(source => source.Name switch
        {
            "Latest sensor sample" => new HardwareSensorSource(source.Name, !hasRead ? "Not sampled" : driverStatus.Available ? "Available" : "Unavailable",
                !hasRead ? source.Detail : driverStatus.Available ? "At least one hwmon or NVML measurement was readable. Other channels may remain unavailable."
                    : InitializationFailureDetails ?? "No hwmon or NVML measurement was confirmed."),
            "NVIDIA NVML" when nvidiaSample is not null => new HardwareSensorSource(source.Name,
                nvidiaSample.Devices.Any(device => device.Readings.Any(reading => reading.Value is not null)) ? "Readings available" : "Unavailable",
                nvidiaSample.Notice ?? "No NVIDIA NVML measurement was available in the latest sample."),
            _ => source
        }).ToArray();
        return metadata with { Available = driverStatus.Available, Sources = sources };
    }

    private static IReadOnlyList<HardwareDeviceDto> BoundDebugDevices(IReadOnlyList<HardwareDeviceDto> devices, List<string> notices)
    {
        var result = new List<HardwareDeviceDto>();
        var remaining = MaximumReadings;
        foreach (var device in devices.Take(MaximumDevices))
        {
            var readings = device.Sensors.Take(remaining).ToArray();
            if (readings.Length != device.Sensors.Count) { AddNotice(notices, "Sensor Debug reached its combined hwmon/NVML reading limit."); }
            result.Add(device with { Sensors = readings });
            remaining -= readings.Length;
        }
        if (devices.Count > MaximumDevices) { AddNotice(notices, "Sensor Debug reached its combined hwmon/NVML device limit."); }
        return result;
    }
}
