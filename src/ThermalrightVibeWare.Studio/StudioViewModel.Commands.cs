/* VBWR B
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * Purpose: Studio navigation, workspace selection and preference commands.
 * Created with AI: OpenAI Codex; studio state refactor; 2026-11-08.
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 * VBWR E */

using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Studio;

public enum StudioSection { Themes, SensorDebug, Preview, Appearance, Display, Diagnostics }
public enum StudioColorControl { Saturation, Hue, Brightness, Contrast }

public sealed record StudioWorkspace(StudioSection ThemeSection = StudioSection.Preview, bool SensorDebugOpen = false)
{
    public bool ShowsPreview => !SensorDebugOpen && ThemeSection != StudioSection.Diagnostics;
}

public readonly record struct StudioPage(int Index, int Count, string? Title = null, bool IsSelectedTheme = true)
{
    public bool CanNavigate => IsSelectedTheme && Count > 1;
}

public sealed partial class StudioViewModel
{
    public StudioWorkspace Workspace { get; private set; } = new();

    /// <summary>Selects a workspace while remembering the last central tab when opening Sensor Debug.</summary>
    /// <param name="section">Top-level destination or a tab within the theme workspace.</param>
    public void SelectSection(StudioSection section) => Workspace = section switch
    {
        StudioSection.SensorDebug => Workspace with { SensorDebugOpen = true },
        StudioSection.Themes => Workspace with { SensorDebugOpen = false },
        _ => new(section)
    };

    /// <summary>Moves through the catalog themes in the selected category, wrapping at either end.</summary>
    /// <param name="catalog">Latest catalog used by the session.</param>
    /// <param name="direction">One step, either -1 or 1.</param>
    public void NavigateTheme(ThemeCatalogDto catalog, int direction) => model.Update(state =>
    {
        var p = state.Preferences;
        var themes = catalog.Themes.Where(theme => theme.Category == p.SelectedCategory).ToArray();
        if (themes.Length < 2) { return; }
        var index = Array.FindIndex(themes, theme => theme.Id == p.SelectedTheme);
        var next = index < 0 ? (direction < 0 ? themes.Length - 1 : 0) : (index + direction + themes.Length) % themes.Length;
        SelectExplicit(state, p.SelectedCategory, themes[next].Id);
    });

    /// <summary>Applies a user selection after canceling an outstanding automatic start.</summary>
    /// <param name="state">Settings transaction in which the new selection will be recorded.</param>
    /// <param name="category">Selected category identifier.</param>
    /// <param name="theme">Selected theme identifier.</param>
    private void SelectExplicit(StudioState state, string category, string theme)
    {
        if (state.Preferences.SelectedCategory != category || state.Preferences.SelectedTheme != theme) { cancelStartup?.Invoke(); }
        Select(state, category, theme);
    }

    /// <summary>Resolves page metadata from a frame of the current request or from the selected catalog theme.</summary>
    /// <param name="preferences">Current detached preferences, including the selected theme and page revision.</param>
    /// <param name="snapshot">Latest session observation with catalog and frame request metadata.</param>
    public StudioPage GetPage(StudioPreferences preferences, StudioSnapshot snapshot)
    {
        if (snapshot.FrameRequestedThemeId == preferences.SelectedTheme && snapshot.Frame is { } frame)
        {
            if (frame.FrameSuppressed) { return default; }
            var count = Math.Max(1, frame.ActivePageCount);
            if (frame.ActiveThemeId is { } activeTheme && activeTheme != preferences.SelectedTheme)
            {
                return new(Math.Clamp(frame.ActivePageIndex, 0, count - 1), count, frame.ActivePageTitle, IsSelectedTheme: false);
            }
            var current = snapshot.FramePageRevision == preferences.PageRevision;
            return new(Math.Clamp(current ? frame.ActivePageIndex : preferences.SelectedPageIndex, 0, count - 1),
                count, current ? frame.ActivePageTitle : null);
        }

        var selected = snapshot.Catalog?.Themes.FirstOrDefault(theme => theme.Id == preferences.SelectedTheme);
        if (selected is null) { return default; }
        var selectedCount = Math.Max(1, selected.PageCount);
        return new(Math.Clamp(preferences.SelectedPageIndex, 0, selectedCount - 1), selectedCount);
    }

    /// <summary>Moves the current theme's page and starts its page clock again.</summary>
    /// <param name="snapshot">Session observation available when the command was issued.</param>
    /// <param name="direction">One step, either -1 or 1.</param>
    public void NavigatePage(StudioSnapshot snapshot, int direction) => model.Edit(preferences =>
    {
        var page = GetPage(preferences, snapshot);
        if (!page.CanNavigate) { return; }
        preferences.SelectedPageIndex = (page.Index + direction + page.Count) % page.Count;
        preferences.PageRevision++;
        preferences.LastChange = Environment.TickCount64;
    });

    /// <summary>Stores the explicit preview profile or retains the last resolved preview size in Auto.</summary>
    /// <param name="profile">A profile identifier from the engine or the Auto choice.</param>
    public void SelectProfile(string profile) => model.Edit(p =>
    {
        if (p.ProfileSelection == profile) { return; }
        cancelStartup?.Invoke();
        p.ProfileSelection = profile;
        if (profile != "auto") { p.SelectedProfile = profile; }
    });

    /// <summary>Updates rotation and its clock in a single edit.</summary>
    /// <param name="enabled">Whether to rotate the enabled screens automatically.</param>
    public void SetRotation(bool enabled) => model.Edit(p =>
    {
        p.CycleThemes = enabled;
        p.LastChange = Environment.TickCount64;
    });

    /// <summary>Updates the participation of one screen in display output and restarts the rotation clock.</summary>
    /// <param name="category">Studio screen category identifier.</param>
    /// <param name="enabled">Whether this category may appear on the display.</param>
    public void SetScreen(string category, bool enabled) => model.Update(state =>
    {
        if (!state.ScreenEnabled.ContainsKey(category)) { throw new ArgumentException("Unknown screen category.", nameof(category)); }
        state.ScreenEnabled[category] = enabled;
        state.Preferences.LastChange = Environment.TickCount64;
    });

    /// <summary>Stores one filter adjustment without reading other controls.</summary>
    /// <param name="control">Filter to adjust.</param>
    /// <param name="value">Slider value in percent or hue degrees.</param>
    public void SetColor(StudioColorControl control, double value) => model.Edit(p =>
    {
        var rounded = Math.Round(value);
        switch (control)
        {
            case StudioColorControl.Saturation: p.SaturationPercent = rounded; break;
            case StudioColorControl.Hue: p.HueDegrees = rounded; break;
            case StudioColorControl.Brightness: p.BrightnessPercent = rounded; break;
            case StudioColorControl.Contrast: p.ContrastPercent = rounded; break;
            default: throw new ArgumentOutOfRangeException(nameof(control));
        }
    });

    /// <summary>Restores all four neutral filter values atomically.</summary>
    public void ResetColors() => model.Edit(p =>
    {
        p.SaturationPercent = p.ContrastPercent = 100;
        p.HueDegrees = p.BrightnessPercent = 0;
    });
}
