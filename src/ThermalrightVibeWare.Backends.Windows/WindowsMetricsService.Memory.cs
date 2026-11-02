/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsMetricsService.Memory.cs
 * Purpose: Combines Windows physical RAM identities with unambiguous SPD observations.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard file headers.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */

using System.Globalization;
using System.Management;
using Lhm = LibreHardwareMonitor.Hardware;

namespace ThermalrightVibeWare.Core;

public sealed partial class WindowsMetricsService
{
    private string? memoryType, memoryMetadataNotice;
    private double? memoryConfiguredSpeed;
    private IReadOnlyList<HardwareMemoryModule> memoryModules = [];

    // Static SMBIOS metadata is read once when this reader opens, never every frame.
    /// <summary>Reads physical RAM identities from Windows without guessing slot mappings.</summary>
    private void ReadMemoryConfiguration()
    {
        try
        {
            var modules = new List<HardwareMemoryModule>();
            var types = new HashSet<string>();
            var speeds = new HashSet<double>();
            using var query = new ManagementObjectSearcher("SELECT Tag, Manufacturer, PartNumber, SerialNumber, DeviceLocator, BankLabel, Capacity, SMBIOSMemoryType, ConfiguredClockSpeed FROM Win32_PhysicalMemory");
            using var results = query.Get();
            foreach (ManagementObject module in results)
            {
                using (module)
                {
                    if (modules.Count >= 16) { break; }
                    var type = Convert.ToInt32(module["SMBIOSMemoryType"] ?? 0, CultureInfo.InvariantCulture) switch
                    {
                        20 => "DDR",
                        21 => "DDR2",
                        24 => "DDR3",
                        26 => "DDR4",
                        27 => "LPDDR",
                        28 => "LPDDR2",
                        29 => "LPDDR3",
                        30 => "LPDDR4",
                        34 => "DDR5",
                        35 => "LPDDR5",
                        _ => null
                    };
                    if (type is not null) { types.Add(type); }
                    var speed = Convert.ToDouble(module["ConfiguredClockSpeed"] ?? 0, CultureInfo.InvariantCulture);
                    if (speed > 0) { speeds.Add(speed); }
                    var bytes = Convert.ToDouble(module["Capacity"] ?? 0, CultureInfo.InvariantCulture);
                    var manufacturer = MemoryIdentityText(module["Manufacturer"]);
                    var partNumber = MemoryIdentityText(module["PartNumber"]);
                    var name = string.Join(" ", new[] { manufacturer, partNumber }.Where(value => value is not null));
                    var tag = MemoryIdentityText(module["Tag"]);
                    modules.Add(new(string.IsNullOrWhiteSpace(name) ? "Memory module" : Limit(name), bytes > 0 ? bytes / (1024d * 1024 * 1024) : null,
                        Id: tag is null ? null : "smbios:" + tag, Manufacturer: manufacturer, PartNumber: partNumber,
                        SerialNumber: MemoryIdentityText(module["SerialNumber"], serialNumber: true),
                        DeviceLocator: MemoryIdentityText(module["DeviceLocator"]), BankLabel: MemoryIdentityText(module["BankLabel"]),
                        Type: type, ConfiguredSpeedMTs: speed > 0 ? speed : null));
                }
            }
            memoryModules = modules;
            memoryType = types.Count == 1 ? types.Single() : types.Count > 1 ? "Mixed" : null;
            // Preserve the SMBIOS configured DDR data rate; do not infer it from SPD timing limits.
            memoryConfiguredSpeed = speeds.Count == 1 ? speeds.Single() : null;
        }
        catch (Exception error) when (IsSensorFailure(error))
        { memoryMetadataNotice = "RAM identity/type/configured speed metadata unavailable: " + error.GetType().Name; }
    }

    /// <summary>Normalizes a RAM identity field while preserving unavailable values.</summary>
    /// <param name="value">Raw Windows memory identity field; null or placeholder text is unavailable.</param>
    /// <param name="serialNumber">Whether placeholder serial-number values must be treated as unavailable.</param>
    private static string? MemoryIdentityText(object? value, bool serialNumber = false)
    {
        var text = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Undefined", StringComparison.OrdinalIgnoreCase) || text.Equals("Not Specified", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Not Available", StringComparison.OrdinalIgnoreCase) || text.Equals("Default string", StringComparison.OrdinalIgnoreCase)
            || text.Equals("To Be Filled By O.E.M.", StringComparison.OrdinalIgnoreCase)
            || text.Equals("None", StringComparison.OrdinalIgnoreCase)) { return null; }
        if (serialNumber && text.All(character => character == '0' || char.IsWhiteSpace(character))) { return null; }
        return Limit(text);
    }

    /// <summary>Projects one SPD device and its available readings into a memory module.</summary>
    /// <param name="hardware">Raw hardware device from the configured LibreHardwareMonitor provider.</param>
    /// <param name="sensors">Raw sensor channels supplied by the selected hardware device.</param>
    private HardwareMemoryModule ReadSpdModule(Lhm.IHardware hardware, Lhm.ISensor[] sensors)
    {
        // LHM 0.9.6 names SPD devices as "manufacturer - part number (#SMBus index)".
        // That index is not a physical slot or a memory channel.
        var name = hardware.Name;
        var identity = name;
        var suffix = identity.LastIndexOf(" (#", StringComparison.Ordinal);
        if (suffix >= 0 && identity.EndsWith(')')
            && int.TryParse(identity.AsSpan(suffix + 3, identity.Length - suffix - 4), NumberStyles.None, CultureInfo.InvariantCulture, out _))
        { identity = identity[..suffix]; }
        var separator = identity.IndexOf(" - ", StringComparison.Ordinal);
        var manufacturer = separator > 0 ? MemoryIdentityText(identity[..separator]) : null;
        var partNumber = separator > 0 ? MemoryIdentityText(identity[(separator + 3)..]) : null;
        return new(Limit(name), FindValue(sensors, Lhm.SensorType.Data, "Capacity", nonnegative: true), ReadThemeValues(sensors, 64),
            Limit(hardware.Identifier.ToString()), manufacturer, partNumber);
    }

    /// <summary>Combines physical RAM identities and SPD data only when model matching is unambiguous.</summary>
    /// <param name="configured">Physical RAM identities reported by Windows.</param>
    /// <param name="spd">Independently reported SPD modules, without inferred physical slot assignments.</param>
    internal static IReadOnlyList<HardwareMemoryModule> MergeMemoryModules(IReadOnlyList<HardwareMemoryModule> configured,
        IReadOnlyList<HardwareMemoryModule> spd)
    {
        if (configured.Count == 0) { return spd; }
        return configured.Select(module =>
        {
            var candidates = spd.Where(candidate => SameMemoryModel(module, candidate)).ToArray();
            var manufacturers = candidates.Select(candidate => candidate.Manufacturer).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var manufacturer = module.Manufacturer ?? (manufacturers.Length == 1 ? manufacturers[0] : null);
            // Identical modules cannot be assigned to SMBIOS slots by enumeration order.
            var readings = candidates.Length == 1 && configured.Count(candidate => SameMemoryModel(candidate, candidates[0])) == 1
                ? candidates[0].Readings : module.Readings;
            var name = manufacturer is not null && module.PartNumber is not null ? Limit(manufacturer + " " + module.PartNumber) : module.Name;
            return module with { Name = name, Manufacturer = manufacturer, Readings = readings };
        }).ToArray();
    }

    /// <summary>Compares normalized RAM model identities without inferring physical slots.</summary>
    /// <param name="left">First RAM identity being compared.</param>
    /// <param name="right">Second RAM identity being compared.</param>
    private static bool SameMemoryModel(HardwareMemoryModule left, HardwareMemoryModule right) =>
        left.PartNumber is not null && right.PartNumber is not null
        && left.PartNumber.Equals(right.PartNumber, StringComparison.OrdinalIgnoreCase)
        && left.CapacityGiB is double leftCapacity && right.CapacityGiB is double rightCapacity
        && Math.Abs(leftCapacity - rightCapacity) < 0.01;

    /// <summary>Projects bounded sensor readings while applying session fan visibility rules.</summary>
    /// <param name="sensors">Raw sensor channels supplied by the selected hardware device.</param>
    /// <param name="maximum">Maximum number of projected readings returned for this device.</param>
    private HardwareReading[] ReadThemeValues(IEnumerable<Lhm.ISensor> sensors, int maximum = 256) => sensors
        .Where(sensor => sensor.SensorType != Lhm.SensorType.Fan
            || Value(sensor, nonnegative: true) is double rpm && rpm is >= 0 and <= 20000 && ObserveFan(sensor.Identifier.ToString(), rpm))
        .Take(maximum)
        .Select(sensor => new HardwareReading(Limit(sensor.Identifier.ToString()), Limit(sensor.Name), sensor.SensorType.ToString(),
            Finite(sensor.Value), SensorUnit(sensor.SensorType))).ToArray();
}
