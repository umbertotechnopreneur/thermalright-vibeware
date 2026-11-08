/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: PlatformDashboardProviders.cs
 * Purpose: Selects sensor and dashboard adapters without sampling hardware.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Created with AI: OpenAI Codex; explicit platform backend selection; 2026-11-02.
 * Human guidance: Umberto Giacobbi; separate Linux sensors and fail fast.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */

namespace ThermalrightVibeWare.Core;

/// <summary>Selects host adapters. Construction does not sample sensors or open USB/audio devices.</summary>
public static class PlatformDashboardProviders
{
    public static string PlatformName => OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : "unsupported";

    /// <summary>Creates the sensor adapter compiled for this host; no sample is taken here.</summary>
    public static ISensorProvider CreateSensors()
    {
#if WINDOWS
        if (OperatingSystem.IsWindows()) { return new WindowsMetricsService(allowHardwareAccess: true); }
        throw new PlatformNotSupportedException("This engine targets Windows. Use the net10.0 Linux sensor engine on Linux.");
#else
        if (OperatingSystem.IsLinux()) { return new LinuxMetricsService(allowHardwareAccess: true); }
        throw new PlatformNotSupportedException("Use the Windows-targeted engine on Windows. This engine supports Linux local previews and sensor diagnostics.");
#endif
    }

    /// <summary>Creates the host dashboard with explicit availability for unsupported sources.</summary>
    /// <param name="metrics">Optional sensor provider owned by the resulting dashboard service.</param>
    public static DashboardProviders Create(ISensorProvider? metrics = null)
    {
#if WINDOWS
        if (OperatingSystem.IsWindows()) { return WindowsDashboardProviders.Create(metrics); }
        throw new PlatformNotSupportedException("This engine targets Windows. Use the net10.0 Linux sensor engine on Linux.");
#else
        if (!OperatingSystem.IsLinux())
        { throw new PlatformNotSupportedException("Use the Windows-targeted engine on Windows. This engine supports Linux local previews and sensor diagnostics."); }
        return LinuxDashboardProviders.Create(metrics);
#endif
    }
}
