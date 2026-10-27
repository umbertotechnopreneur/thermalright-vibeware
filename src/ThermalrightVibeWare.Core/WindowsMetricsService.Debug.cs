/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsMetricsService.Debug.cs
 * Purpose: Exposes raw sensor channels and access notices for diagnostics.
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

using System.Diagnostics;
using System.Security.Principal;
using ThermalrightVibeWare.Contracts;
using Lhm = LibreHardwareMonitor.Hardware;

namespace ThermalrightVibeWare.Core;

public sealed partial class WindowsMetricsService
{
    private SensorDebugDto? sensorDebug;
    private long lastDebugRead;
    private const int MaximumDebugSensors = 2048;

    /// <summary>One bounded, read-only tree from the same library instance used by themes. No USB or audio.</summary>
    public SensorDebugDto ReadSensorDebug()
    {
        lock (sync)
        {
            var now = Stopwatch.GetTimestamp();
            if (sensorDebug is not null && lastDebugRead != 0 && Stopwatch.GetElapsedTime(lastDebugRead, now) < TimeSpan.FromSeconds(1))
            { return sensorDebug; }
            var devices = new List<HardwareDeviceDto>();
            var notices = new List<string>();
            var count = 0;
            var parameterCount = 0;
            if (disposed) { notices.Add("Sensor reader is closed."); }
            else if (!EnsureComputer()) { notices.Add(snapshot.Notice ?? "Sensor reader could not start."); }
            else
            {
                foreach (var hardware in EnumerateHardware(computer!.Hardware))
                {
                    var readings = new List<SensorReadingDto>();
                    string? notice = null;
                    try
                    {
                        hardware.Update();
                        var sensors = hardware.Sensors;
                        if (sensors.Length > MaximumSensorsPerDevice) { notice = "Device sensor list is truncated at 1,024 readings."; }
                        foreach (var sensor in sensors.Take(MaximumSensorsPerDevice))
                        {
                            if (count >= MaximumDebugSensors) { notice = "Sensor list reached the debug limit."; break; }
                            count++;
                            try
                            {
                                var value = Finite(sensor.Value);
                                if (sensor.SensorType == Lhm.SensorType.Fan && value is >= 0 and <= 20000)
                                { _ = ObserveFan(sensor.Identifier.ToString(), value.Value); }
                                var parameters = sensor.Parameters.Take(Math.Min(16, 512 - parameterCount)).Select(parameter =>
                                    new SensorParameterDto(Limit(parameter.Identifier.ToString()), Limit(parameter.Name), Limit(parameter.Description),
                                        Finite(parameter.Value), Finite(parameter.DefaultValue))).ToArray();
                                parameterCount += parameters.Length;
                                readings.Add(new(Limit(sensor.Identifier.ToString()), Limit(sensor.Name), sensor.SensorType.ToString(),
                                    SensorUnit(sensor.SensorType), value, Finite(sensor.Min), Finite(sensor.Max),
                                    sensor.Parameters.Count > parameters.Length ? "Calibration parameter list is truncated." : null, parameters));
                            }
                            catch (Exception error) when (IsSensorFailure(error))
                            { readings.Add(new("unavailable-" + count, "Unreadable sensor", "Unknown", "", null, null, null, error.GetType().Name)); }
                        }
                    }
                    catch (Exception error) when (IsSensorFailure(error)) { notice = "Read failed: " + error.GetType().Name; }
                    devices.Add(new(Limit(hardware.Identifier.ToString()), hardware.Parent is null ? null : Limit(hardware.Parent.Identifier.ToString()),
                        Limit(hardware.Name), hardware.HardwareType.ToString(), readings, notice));
                }
                if (devices.Count >= MaximumHardware) { notices.Add("Hardware list reached the debug limit."); }
                if (count >= MaximumDebugSensors) { notices.Add("Sensor list is truncated at 2,048 readings."); }
                if (parameterCount >= 512) { notices.Add("Calibration parameter list reached the debug limit of 512."); }
                if (networkMetadataNotice is not null) { notices.Add(networkMetadataNotice); }
                if (devices.Count == 0) { notices.Add("The library returned no hardware devices."); }
                if (!devices.Any(device => device.Id.StartsWith("/memory/dimm/", StringComparison.Ordinal)))
                { notices.Add("RAM/SPD was requested, but no DIMM devices were returned. Physical RAM totals remain separate."); }
                if (!devices.Any(device => device.Type is "SuperIO" or "EmbeddedController"))
                { notices.Add("No motherboard sensor chip was returned. Board fan RPM and temperatures may be unavailable."); }
                notices.Add(driverStatus.Notice ?? "Driver access has not been verified.");
            }
            var administrator = false;
            if (System.OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                administrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            if (!administrator) { notices.Add("Running without administrator rights. Some sensors may be unavailable."); }
            sensorDebug = new(typeof(Lhm.Computer).Assembly.GetName().Version?.ToString() ?? "Unknown", DateTimeOffset.UtcNow,
                administrator, devices, notices);
            lastDebugRead = Stopwatch.GetTimestamp();
            return sensorDebug;
        }
    }

    /// <summary>Returns a finite sensor value or unavailable.</summary>
    /// <param name="value">Optional raw floating-point sensor reading; non-finite values become unavailable.</param>
    private static double? Finite(float? value) => value is float number && float.IsFinite(number) ? number : null;

    /// <summary>Bounds diagnostic text to the display limit.</summary>
    /// <param name="text">Text to format, normalize or display in this operation.</param>
    private static string Limit(string text) => text.Length <= 160 ? text : text[..160];

    /// <summary>Maps a sensor type to its displayed unit.</summary>
    /// <param name="type">Raw sensor type used to select readings or their display unit.</param>
    private static string SensorUnit(Lhm.SensorType type) => type.ToString() switch
    {
        "Voltage" => "V",
        "Current" => "A",
        "Power" => "W",
        "Clock" => "MHz",
        "Temperature" => "°C",
        "Load" or "Control" or "Level" => "%",
        "Frequency" => "Hz",
        "Fan" => "RPM",
        "Flow" => "L/h",
        "Data" => "GiB",
        "SmallData" => "MiB",
        "Throughput" => "B/s",
        "TimeSpan" => "s",
        "Timing" => "ns",
        "Energy" => "mWh",
        "Noise" => "dBA",
        "Conductivity" => "µS/cm",
        "Humidity" => "%",
        _ => ""
    };
}
