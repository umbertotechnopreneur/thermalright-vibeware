/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: HardwareSensors.cs
 * Purpose: Defines available hardware readings, physical RAM identities and driver notices.
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

namespace ThermalrightVibeWare.Core;

/// <summary>One bounded sensor sample. Missing measurements stay null; zero is a valid measurement.</summary>
public sealed record HardwareSensorSnapshot(HardwareDeviceSensors? Cpu, IReadOnlyList<HardwareDeviceSensors> Gpus,
    HardwareMemorySensors? Memory, DateTimeOffset? UpdatedAtUtc, string? Notice = null,
    HardwareMotherboardSensors? Motherboard = null, HardwareDriverStatus? Driver = null)
{
    public static HardwareSensorSnapshot Empty { get; } = new(null, [], null, null);
}

/// <summary>Measurements supplied by the selected hardware library, in percent, MHz, and degrees Celsius.</summary>
public sealed record HardwareDeviceSensors(string Name, double? UsagePercent, double? ClockMHz, double? TemperatureC,
    string? ClockLabel = null, string? TemperatureLabel = null, IReadOnlyList<HardwareFanSensors>? Fans = null,
    IReadOnlyList<HardwareReading>? Readings = null);

/// <summary>A named library value retained for richer themes without changing its physical meaning.</summary>
public sealed record HardwareReading(string Id, string Name, string Type, double? Value, string Unit);
/// <summary>A physical module identity. Part numbers, serial numbers and firmware slot labels remain distinct.</summary>
public sealed record HardwareMemoryModule(string Name, double? CapacityGiB, IReadOnlyList<HardwareReading>? Readings = null,
    string? Id = null, string? Manufacturer = null, string? PartNumber = null, string? SerialNumber = null,
    string? DeviceLocator = null, string? BankLabel = null, string? Type = null, double? ConfiguredSpeedMTs = null);

/// <summary>A named tachometer reading from the selected sensor library. RPM zero is a measured stop.</summary>
public sealed record HardwareFanSensors(string Name, double Rpm);

/// <summary>Board identity and readings from its Super I/O chips, when exposed by the library.</summary>
public sealed record HardwareMotherboardSensors(string Name, double? TemperatureC, string? TemperatureLabel,
    IReadOnlyList<HardwareFanSensors> Fans, IReadOnlyList<HardwareTemperatureSensors>? Temperatures = null,
    IReadOnlyList<HardwareReading>? Readings = null);

/// <summary>A reported temperature name is kept even when the library cannot identify its physical location.</summary>
public sealed record HardwareTemperatureSensors(string Name, double TemperatureC);

/// <summary>The existing PawnIO installation; registration alone does not prove usable sensor access.</summary>
public sealed record HardwareDriverStatus(bool Available, string? Path, string? Notice = null);

/// <summary>Physical memory in GiB (2^30 bytes); excludes swap and committed virtual memory.</summary>
public sealed record HardwareMemorySensors(double? TotalGiB, double? UsedGiB, double? UsagePercent,
    string? Type = null, double? ConfiguredSpeedMTs = null, double? ClockMHz = null,
    IReadOnlyList<HardwareMemoryModule>? Modules = null, string? MetadataNotice = null,
    IReadOnlyList<HardwareMemoryModule>? SpdModules = null);
