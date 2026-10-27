/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: StudioViewModel.cs
 * Purpose: Applies studio selection rules and exposes auxiliary engine status to views.
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

using ThermalrightVibeWare.Contracts;
namespace ThermalrightVibeWare.Studio;

/// <summary>UI-independent selection commands shared by the view and background session.</summary>
/// <param name="model">Shared studio state owner; edits and reads pass through its transaction boundary.</param>
/// <param name="client">Optional engine client for typed auxiliary operations; null leaves those operations unavailable.</param>
public sealed class StudioViewModel(StudioModel model, IPanelClient? client = null)
{
    private readonly object auxiliarySync = new();
    private StudioAuxiliaryState auxiliary = new();
    public StudioAuxiliaryState Auxiliary => Volatile.Read(ref auxiliary);
    public string? PersistenceNotice => model.PersistenceNotice;

    /// <summary>Serializes a named engine request and returns its typed response.</summary>
    /// <typeparam name="T">Expected response or payload type used by this operation.</typeparam>
    /// <param name="method">Allow-listed engine or worker method name.</param>
    /// <param name="options">Optional typed engine payload serialized with the versioned contract.</param>
    private async Task<T> RequestAsync<T>(string method, object? options = null)
        => await (client ?? throw new InvalidOperationException("No engine client is attached.")).RequestAsync<T>(method, options).ConfigureAwait(false);

    /// <summary>Publishes the next observation while protecting shared state.</summary>
    /// <param name="update">Function producing the next auxiliary observation from the current one.</param>
    private void Publish(Func<StudioAuxiliaryState, StudioAuxiliaryState> update)
    { lock (auxiliarySync) { Volatile.Write(ref auxiliary, update(auxiliary)); } }

    /// <summary>Reads Codex setup status and optionally prepares the local plugin.</summary>
    /// <param name="prepare">Whether to prepare the local Codex plugin before reading its status.</param>
    public async Task<CodexSetupStatus> ReadCodexAsync(bool prepare = false)
    {
        var value = await RequestAsync<CodexSetupStatus>(prepare ? "codex.prepare" : "codex.status").ConfigureAwait(false);
        Publish(s => s with { Codex = value }); return value;
    }

    /// <summary>Reads diagnostic settings, optionally changing their enabled state.</summary>
    /// <param name="enabled">New logging choice, or null to read the current choice without changing it.</param>
    public async Task<DiagnosticsStatus> ReadDiagnosticsAsync(bool? enabled = null)
    {
        var value = await RequestAsync<DiagnosticsStatus>(enabled.HasValue ? "diagnostics.set" : "diagnostics.status",
            enabled.HasValue ? new SetDiagnosticsOptions(enabled.Value) : null).ConfigureAwait(false);
        Publish(s => s with { Diagnostics = value }); return value;
    }

    /// <summary>Asks the engine to open its local diagnostic log.</summary>
    public Task<System.Text.Json.JsonElement> OpenDiagnosticsAsync() => RequestAsync<System.Text.Json.JsonElement>("diagnostics.open");

    /// <summary>Reads driver installation metadata without opening hardware.</summary>
    public Task<SensorDriverDto> ReadSensorDriverAsync() => RequestAsync<SensorDriverDto>("sensors.driver.status");

    /// <summary>Requests raw read-only sensor diagnostics from the engine.</summary>
    public Task<SensorDebugDto> ReadSensorDebugAsync() => RequestAsync<SensorDebugDto>("sensors.debug");

    /// <summary>Asks the engine to show the installed sensor driver location.</summary>
    public Task<System.Text.Json.JsonElement> OpenSensorDriverFolderAsync() => RequestAsync<System.Text.Json.JsonElement>("sensors.driver.open");

    /// <summary>Loads the worker's available font families and publishes the result.</summary>
    public async Task<FontCatalogDto> ReadFontsAsync()
    {
        var value = await RequestAsync<FontCatalogDto>("fonts").ConfigureAwait(false);
        Publish(s => s with { Fonts = value }); return value;
    }
    public StudioPreferences Preferences => model.Preferences;

    /// <summary>Returns an independent studio state snapshot.</summary>
    public StudioState Read() => model.Read();

    /// <summary>Applies an edit to the studio preferences under the model lock.</summary>
    /// <param name="edit">Mutation applied while the owning model or snapshot lock is held.</param>
    public void Edit(Action<StudioPreferences> edit) => model.Edit(edit);

    /// <summary>Reconciles saved selections with the available theme catalog.</summary>
    /// <param name="catalog">Available theme definitions used to validate and resolve selections.</param>
    public void NormalizeCatalog(ThemeCatalogDto catalog) => model.Update(s =>
    {
        foreach (var category in catalog.Categories)
        {
            var remembered = s.ScreenThemes.GetValueOrDefault(category.Id);
            if (!catalog.Themes.Any(t => t.Category == category.Id && t.Id == remembered))
            { s.ScreenThemes[category.Id] = catalog.Themes.FirstOrDefault(t => t.Category == category.Id)?.Id ?? ""; }
        }
        var p = s.Preferences;
        if (!catalog.Categories.Any(c => c.Id == p.SelectedCategory))
        { p.SelectedCategory = catalog.Categories.FirstOrDefault()?.Id ?? "multimedia"; }
        if (!catalog.Themes.Any(t => t.Category == p.SelectedCategory && t.Id == p.SelectedTheme))
        { p.SelectedTheme = s.ScreenThemes.GetValueOrDefault(p.SelectedCategory, ""); }
        s.ScreenThemes[p.SelectedCategory] = p.SelectedTheme;
    });

    /// <summary>Selects a category and resolves its saved or first available theme.</summary>
    /// <param name="category">Stable internal category identifier, such as multimedia, codex or system.</param>
    /// <param name="catalog">Available theme definitions used to validate and resolve selections.</param>
    public void SelectCategory(string category, ThemeCatalogDto catalog) => model.Update(s =>
    {
        if (!catalog.Categories.Any(c => c.Id == category)) { return; }
        Select(s, category, s.ScreenThemes.GetValueOrDefault(category, ""));
    });

    /// <summary>Selects a theme and updates the category's saved selection.</summary>
    /// <param name="theme">Stable theme identifier from the current catalog.</param>
    /// <param name="catalog">Available theme definitions used to validate and resolve selections.</param>
    public void SelectTheme(string theme, ThemeCatalogDto catalog) => model.Update(s =>
    {
        var selected = catalog.Themes.FirstOrDefault(t => t.Id == theme);
        if (selected is not null) { Select(s, selected.Category, selected.Id); }
    });

    /// <summary>Stores a category/theme selection and resets its page clock.</summary>
    /// <param name="state">Detached studio preferences, category selections and palette overrides.</param>
    /// <param name="category">Stable internal category identifier, such as multimedia, codex or system.</param>
    /// <param name="theme">Stable theme identifier from the current catalog.</param>
    internal static void Select(StudioState state, string category, string theme)
    {
        var changed = state.Preferences.SelectedCategory != category || state.Preferences.SelectedTheme != theme;
        state.Preferences.SelectedCategory = category; state.Preferences.SelectedTheme = theme;
        if (changed) { state.Preferences.SelectedPageIndex = 0; state.Preferences.PageRevision++; }
        state.Preferences.LastChange = Environment.TickCount64; state.ScreenThemes[category] = theme;
    }

    /// <summary>Updates which categories may participate in display rotation.</summary>
    /// <param name="multimedia">Whether Music screens may participate in output rotation.</param>
    /// <param name="codex">Whether Codex screens may participate in output rotation.</param>
    /// <param name="system">Whether System screens may participate in output rotation.</param>
    public void SetScreens(bool multimedia, bool codex, bool system) => model.Update(s =>
    {
        s.ScreenEnabled["multimedia"] = multimedia; s.ScreenEnabled["codex"] = codex; s.ScreenEnabled["system"] = system;
        s.Preferences.LastChange = Environment.TickCount64;
    });

    /// <summary>Stores or removes a theme palette override.</summary>
    /// <param name="key">Dictionary key or JSON property name to look up.</param>
    /// <param name="palette">Optional theme color overrides; null uses the theme's own palette.</param>
    public void SetPalette(string key, ThemePalette? palette)
    {
        palette?.Validate();
        model.Update(s => { if (palette is null) { s.Palettes.Remove(key); } else { s.Palettes[key] = palette; } });
    }
}

public sealed record StudioAuxiliaryState(CodexSetupStatus? Codex = null, DiagnosticsStatus? Diagnostics = null, FontCatalogDto? Fonts = null);
