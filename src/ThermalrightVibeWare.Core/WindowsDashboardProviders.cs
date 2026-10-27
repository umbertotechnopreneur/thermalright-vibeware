/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsDashboardProviders.cs
 * Purpose: Supplies Windows implementations for the platform-independent dashboard.
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

/// <summary>Windows composition root. Construction does not sample devices.</summary>
public static class WindowsDashboardProviders
{

    /// <summary>Creates the Windows dashboard provider set.</summary>
    /// <param name="metrics">Optional metrics provider; null creates the default Windows provider.</param>
    public static DashboardProviders Create(IMetricsProvider? metrics = null) => new(new WindowsMediaService(), new WindowsVolumeService(),
        metrics ?? new WindowsMetricsService(allowHardwareAccess: true), new WindowsPlaybackAnalyzer(), new NetworkTrafficService(), new OnlineArtistProvider());
}
