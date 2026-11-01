/* VBWR B
 * Project: Thermalright VibeWare
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * Purpose: Defines read-only sensor diagnostics independently of the operating system.
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 * VBWR E */

using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Core;

/// <summary>One platform's metrics and raw diagnostics. Construction must not open or sample hardware.</summary>
public interface ISensorProvider : IMetricsProvider
{
    /// <summary>Returns cached access metadata without sampling sensors.</summary>
    HardwareDriverStatus DriverStatus { get; }

    /// <summary>Provides local initialization failure details for an explicitly requested diagnostic command.</summary>
    string? InitializationFailureDetails { get; }

    /// <summary>Reads raw channels from the same bounded provider session used by themes.</summary>
    SensorDebugDto ReadSensorDebug();

    /// <summary>Checks platform access metadata without opening or sampling sensor hardware.</summary>
    HardwareDriverStatus ReadDriverStatus();
}
