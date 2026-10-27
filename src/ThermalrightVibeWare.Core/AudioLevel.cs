/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: AudioLevel.cs
 * Purpose: Maps available linear audio peaks to display fractions and dBFS labels.
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

/// <summary>Maps the Windows peak meter to a -60..0 dBFS display. Volume gain is a different number.</summary>
public static class AudioLevel
{

    /// <summary>Maps an available linear audio peak onto the bounded display scale.</summary>
    /// <param name="peak">Optional linear audio peak; null means unavailable.</param>
    public static double Fraction(double? peak) => peak is > 0 && double.IsFinite(peak.Value)
        ? Math.Clamp((20 * Math.Log10(Math.Clamp(peak.Value, 0, 1)) + 60) / 60, 0, 1) : 0;

    /// <summary>Formats an audio peak as a dBFS label or unavailable.</summary>
    /// <param name="peak">Optional linear audio peak; null means unavailable.</param>
    public static string Label(double? peak) => peak switch
    {
        null => "— dBFS",
        <= 0 => "−∞ dBFS",
        < 0.001 => "< −60 dBFS",
        _ => $"{20 * Math.Log10(Math.Clamp(peak.Value, 0.000001, 1)):F1} dBFS"
    };
}
