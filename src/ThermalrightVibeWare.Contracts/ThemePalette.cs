/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: ThemePalette.cs
 * Purpose: Defines and validates optional semantic theme color overrides.
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

namespace ThermalrightVibeWare.Contracts;

/// <summary>Optional semantic overrides. Null inherits the theme's light/dark value.</summary>
public sealed record ThemePalette(string? TextPrimary = null, string? TextSecondary = null,
    string? Accent = null, string? Warning = null, string? Info = null)
{

    /// <summary>Rejects settings outside the supported contract.</summary>
    public void Validate()
    {
        foreach (var color in new[] { TextPrimary, TextSecondary, Accent, Warning, Info })
        {
            if (color is not null && (color.Length != 7 || color[0] != '#' || !color[1..].All(Uri.IsHexDigit)))
            { throw new ArgumentException("Palette colors must be #RRGGBB, or empty to inherit."); }
        }
    }
}
