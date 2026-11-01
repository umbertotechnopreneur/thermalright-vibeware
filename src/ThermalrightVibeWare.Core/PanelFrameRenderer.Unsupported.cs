/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: PanelFrameRenderer.Unsupported.cs
 * Purpose: Keeps unavailable Windows image operations explicit in the Linux sensor engine.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Created with AI: OpenAI Codex; explicit unsupported-platform errors; 2026-11-02.
 * Human guidance: Umberto Giacobbi; implement Linux sensors before USB and rendering.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */

#if !WINDOWS
namespace ThermalrightVibeWare.Core;

/// <summary>Linux currently provides sensor diagnostics, without the Windows image conversion backend.</summary>
public static class PanelFrameRenderer
{
    public static byte[] PrepareJpeg(byte[] jpeg, DisplayProfile profile, int jpegQuality = 90) => throw Unavailable();
    public static byte[] Blank(DisplayProfile profile, int jpegQuality = 90) => throw Unavailable();
    public static byte[] Pattern(DisplayProfile profile) => throw Unavailable();
    public static byte[] Image(string path, DisplayProfile profile, int rotation = 0) => throw Unavailable();

    private static PlatformNotSupportedException Unavailable() => new("Image conversion and physical display output are not implemented in the Linux sensor engine.");
}
#endif
