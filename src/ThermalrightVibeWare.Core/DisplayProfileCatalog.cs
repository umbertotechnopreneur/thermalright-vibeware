/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: DisplayProfileCatalog.cs
 * Purpose: Maps documented panel modes to supported dimensions and encodings.
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

/// <summary>Provides conservative starting profiles instead of assuming every panel is 360 by 360.</summary>
public static class DisplayProfileCatalog
{
    private static readonly IReadOnlyList<DisplayProfile> Profiles =
    [
        new("square-240", "Square 240 x 240", 240, 240, PanelPixelEncoding.Rgb565LittleEndian),
        new("square-320", "Square 320 x 320", 320, 320),
        new("square-360", "Square 360 x 360 · JPEG", 360, 360, PanelPixelEncoding.Jpeg),
        new("square-480", "Square 480 x 480", 480, 480, PanelPixelEncoding.Rgb565LittleEndian),
        new("portrait-240x320", "Portrait 240 x 320", 240, 320, PanelPixelEncoding.Rgb565LittleEndian),
        new("landscape-320x240", "Landscape 320 x 240", 320, 240, PanelPixelEncoding.Rgb565LittleEndian),
        new("landscape-640x480", "Landscape 640 x 480", 640, 480, PanelPixelEncoding.Rgb565LittleEndian),
        new("wide-1280x480", "Wide 1280 x 480 · JPEG", 1280, 480, PanelPixelEncoding.Jpeg),
        new("wide-854x480", "Wide 854 x 480 · JPEG", 854, 480, PanelPixelEncoding.Jpeg),
        new("wide-960x540", "Wide 960 x 540 · JPEG", 960, 540, PanelPixelEncoding.Jpeg),
        new("wide-800x480", "Wide 800 x 480 · JPEG", 800, 480, PanelPixelEncoding.Jpeg),
        new("wide-1600x720", "Wide 1600 x 720 · JPEG", 1600, 720, PanelPixelEncoding.Jpeg),
        new("wide-1920x462", "Wide 1920 x 462 · JPEG", 1920, 462, PanelPixelEncoding.Jpeg)
    ];

    /// <summary>Gets all included profiles.</summary>
    public static IReadOnlyList<DisplayProfile> All => Profiles;

    /// <summary>Maps documented model bytes, leaving unsupported or ambiguous combinations unresolved.</summary>
    /// <param name="productMode">Product-mode byte returned by the display information exchange.</param>
    /// <param name="subMode">Sub-mode byte returned by the display information exchange.</param>
    public static DisplayProfile? FromProductMode(byte productMode, byte subMode)
    {
        var id = productMode switch
        {
            36 or 37 => "square-240",
            32 or 100 or 101 or 102 => "square-320",
            54 => "square-360",
            72 or 129 => "square-480",
            58 => "portrait-240x320",
            5 or 50 => "landscape-320x240",
            7 or 14 => "landscape-640x480",
            128 or 68 => "wide-1280x480",
            9 or 11 => "wide-854x480",
            10 or 16 => "wide-960x540",
            12 => "wide-800x480",
            63 or 64 => "wide-1600x720",
            65 or 66 => "wide-1920x462",
            1 when subMode == 48 => "wide-1600x720",
            1 when subMode == 49 => "wide-1920x462",
            _ => null
        };
        var profile = id is null ? null : GetRequired(id);
        return profile is { Width: 1280, Height: 480 } && subMode == 2 ? profile with { WireRotation = 90 } : profile;
    }

    /// <summary>Finds a built-in profile by its stable CLI identifier.</summary>
    /// <param name="id">Stable display-profile identifier from the supported catalog.</param>
    public static DisplayProfile GetRequired(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return Profiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unknown profile '{id}'. Run 'trv profiles' to list the included profiles.", nameof(id));
    }
}
