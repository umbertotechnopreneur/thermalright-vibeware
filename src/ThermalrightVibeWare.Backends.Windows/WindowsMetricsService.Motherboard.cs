/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsMetricsService.Motherboard.cs
 * Purpose: Applies the approved exact motherboard/chip display profile.
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

using Lhm = LibreHardwareMonitor.Hardware;

namespace ThermalrightVibeWare.Core;

public sealed partial class WindowsMetricsService
{

    /// <summary>Applies only the approved board/chip temperature labels and channel selection.</summary>
    /// <param name="manufacturer">Detected motherboard manufacturer used for exact profile matching.</param>
    /// <param name="model">Detected motherboard model used for exact profile matching.</param>
    /// <param name="hardwareId">Exact sensor hardware identifier used for board/chip profile matching.</param>
    /// <param name="readings">Raw or projected hardware readings, according to the declared type.</param>
    internal static IReadOnlyList<HardwareReading> ProjectMotherboardReadings(
        Lhm.Motherboard.Manufacturer manufacturer, Lhm.Motherboard.Model model,
        string hardwareId, IReadOnlyList<HardwareReading> readings)
    {
        if (manufacturer != Lhm.Motherboard.Manufacturer.ASRock || model != Lhm.Motherboard.Model.B550M_ITX_AC
            || hardwareId != "/lpc/nct6792d/0")
        { return readings; }

        // Owner-approved display labels for this board and chip instance only.
        // Raw library sensors remain intact for Sensor Debug; no calibration is applied.
        var projected = new List<HardwareReading>(readings.Count);
        foreach (var reading in readings)
        {
            if (reading.Type != "Temperature")
            {
                projected.Add(reading);
                continue;
            }
            var label = reading.Id switch
            {
                "/lpc/nct6792d/0/temperature/1" => "Mainboard",
                "/lpc/nct6792d/0/temperature/3" => "Southbridge",
                "/lpc/nct6792d/0/temperature/17" => "CPU",
                _ => null
            };
            // Unidentified inputs stay in diagnostics, regardless of their numeric value.
            if (label is not null) { projected.Add(reading with { Name = label }); }
        }
        return projected.ToArray();
    }
}
