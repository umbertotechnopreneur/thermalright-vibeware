/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: StudioPreferences.cs
 * Purpose: Defines saved studio preferences and their defaults.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Modified with AI: OpenAI Codex; documentation, refactoring and first-launch policy; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard file headers.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */

namespace ThermalrightVibeWare.Studio;

public sealed record StudioPreferences
{
    /// <summary>True until the main studio has loaded once and that completion was saved successfully.</summary>
    public bool IsFirstRun { get; set; } = true;

    public int RefreshMilliseconds { get; set; } = 1000;
    public string JpegQuality { get; set; } = "high";
    public string ProfileSelection { get; set; } = "auto";
    public long LastChange { get; set; } = Environment.TickCount64;
    public string SelectedCategory { get; set; } = "multimedia";
    public string SelectedTheme { get; set; } = "";
    public string SelectedProfile { get; set; } = "wide-1280x480";
    public string FontFamily { get; set; } = "Cascadia Mono";
    public int Appearance { get; set; } = 0;
    public double FontScale { get; set; } = 1;
    public double SaturationPercent { get; set; } = 100;
    public double HueDegrees { get; set; } = 0;
    public double BrightnessPercent { get; set; } = 0;
    public double ContrastPercent { get; set; } = 100;
    public bool ColorFiltersEnabled { get; set; } = false;
    public bool CycleThemes { get; set; } = false;
    public string? SelectedDeviceId { get; set; } = null;
    public bool ShowSpectrum { get; set; } = true;
    public int SelectedPageIndex { get; set; } = 0;
    public long PageRevision { get; set; } = 0;
    public bool Light => Appearance == 2 || Appearance == 0 && DateTime.Now.Hour is >= 7 and < 19;
}
