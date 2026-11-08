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

using System.Collections.Immutable;
using System.Text.Json;
using ThermalrightVibeWare.Contracts;
namespace ThermalrightVibeWare.Studio;

/// <summary>UI-independent selection commands shared by the view and background session.</summary>
/// <param name="studioModel">Shared studio state owner; edits and reads pass through its transaction boundary.</param>
/// <param name="client">Optional engine client for typed auxiliary operations; null leaves those operations unavailable.</param>
/// <param name="cancelPendingStartup">Cancels a saved startup request before an explicit selection changes.</param>
public sealed partial class StudioViewModel(StudioModel studioModel, IPanelClient? client = null, Action? cancelPendingStartup = null)
{
    private readonly StudioModel model = studioModel;
    private readonly Action? cancelStartup = cancelPendingStartup;
    private readonly object auxiliarySync = new();
    private StudioAuxiliaryState auxiliary = new();
    private long? lastCodexAttempt;
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

    /// <summary>Publishes one operation's progress and result, retaining its last successful value on failure.</summary>
    /// <typeparam name="T">Result published by the operation.</typeparam>
    /// <param name="read">Selects the operation from the latest snapshot.</param>
    /// <param name="write">Replaces that operation in the latest snapshot.</param>
    /// <param name="request">Starts the operation after its busy state is published.</param>
    /// <param name="canStart">Optional guard evaluated under the same lock as the busy state.</param>
    /// <returns>Whether a request ran, including requests that reported an error.</returns>
    private async Task<bool> RunAuxiliaryAsync<T>(Func<StudioAuxiliaryState, StudioOperation<T>> read,
        Func<StudioAuxiliaryState, StudioOperation<T>, StudioAuxiliaryState> write, Func<Task<T>> request,
        Func<StudioAuxiliaryState, bool>? canStart = null)
    {
        lock (auxiliarySync)
        {
            var state = auxiliary;
            var operation = read(state);
            if (operation.Busy || canStart?.Invoke(state) == false) { return false; }
            Volatile.Write(ref auxiliary, write(state, operation with { Busy = true }));
        }

        try
        {
            var value = await request().ConfigureAwait(false);
            Publish(s => write(s, new(value)));
        }
        catch (Exception error)
        {
            Publish(s => write(s, read(s) with { Busy = false, Error = error.Message }));
        }
        return true;
    }

    /// <summary>Reads Codex setup status, spacing automatic attempts even if a request fails.</summary>
    /// <param name="force">Whether an explicit refresh may bypass the interval.</param>
    public Task<bool> ReadCodexAsync(bool force = false) => RunAuxiliaryAsync(
        s => s.Codex, (s, operation) => s with { Codex = operation },
        () => RequestAsync<CodexSetupStatus>("codex.status"),
        s => !s.CodexPreparation.Busy && BeginCodexAttempt(force));

    /// <summary>Prepares the local plugin and publishes the status returned by the engine.</summary>
    public Task<bool> PrepareCodexAsync() => RunAuxiliaryAsync(
        s => s.CodexPreparation, (s, operation) => s with { CodexPreparation = operation },
        async () =>
        {
            var status = await RequestAsync<CodexSetupStatus>("codex.prepare").ConfigureAwait(false);
            Publish(s => s with { Codex = new(status) });
            return true;
        }, s => !s.Codex.Busy && BeginCodexAttempt(force: true));

    /// <summary>Claims the next Codex attempt while the auxiliary state is locked.</summary>
    /// <param name="force">Whether to start before the automatic polling interval has elapsed.</param>
    private bool BeginCodexAttempt(bool force)
    {
        var now = Environment.TickCount64;
        if (!force && lastCodexAttempt is long previous && now - previous < 5000) { return false; }
        lastCodexAttempt = now;
        return true;
    }

    /// <summary>Reads diagnostic settings, optionally changing their enabled state.</summary>
    /// <param name="enabled">New logging choice, or null to read the current choice without changing it.</param>
    public Task<bool> ReadDiagnosticsAsync(bool? enabled = null) => RunAuxiliaryAsync(
        s => s.Diagnostics, (s, operation) => s with { Diagnostics = operation },
        () => RequestAsync<DiagnosticsStatus>(enabled.HasValue ? "diagnostics.set" : "diagnostics.status",
            enabled.HasValue ? new SetDiagnosticsOptions(enabled.Value) : null));

    /// <summary>Asks the engine to open its local diagnostic log.</summary>
    public Task<bool> OpenDiagnosticsAsync() => RunAuxiliaryAsync(
        s => s.OpenDiagnostics, (s, operation) => s with { OpenDiagnostics = operation },
        () => OpenLocationAsync("diagnostics.open", "No log file yet. Leave Log errors on, then try again."));

    /// <summary>Reads driver installation metadata without opening hardware.</summary>
    public Task<bool> ReadSensorDriverAsync() => RunAuxiliaryAsync(
        s => s.SensorDriver, (s, operation) => s with { SensorDriver = operation },
        () => RequestAsync<SensorDriverDto>("sensors.driver.status"));

    /// <summary>Publishes driver metadata from an engine frame without clearing an explicit access check's error.</summary>
    /// <param name="driver">Driver status observed in the latest engine frame.</param>
    public void ObserveSensorDriver(SensorDriverDto driver) => Publish(s => s with { SensorDriver = s.SensorDriver with { Value = driver } });

    /// <summary>Requests raw read-only sensor diagnostics from the engine.</summary>
    public Task<bool> ReadSensorDebugAsync() => RunAuxiliaryAsync(
        s => s.SensorDebug, (s, operation) => s with { SensorDebug = operation },
        () => RequestAsync<SensorDebugDto>("sensors.debug"));

    /// <summary>Asks the engine to show the installed sensor driver location.</summary>
    public Task<bool> OpenSensorDriverFolderAsync() => RunAuxiliaryAsync(
        s => s.OpenSensorDriverFolder, (s, operation) => s with { OpenSensorDriverFolder = operation },
        () => OpenLocationAsync("sensors.driver.open", "The driver location is not available."));

    /// <summary>Asks the engine to open a location and reports an explicit refusal as an operation error.</summary>
    /// <param name="method">Engine method for the requested location.</param>
    /// <param name="unavailable">Notice shown if the engine declines the operation.</param>
    private async Task<bool> OpenLocationAsync(string method, string unavailable)
    {
        var result = await RequestAsync<JsonElement>(method).ConfigureAwait(false);
        if (!result.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True)
        { throw new IOException(unavailable); }
        return true;
    }

    /// <summary>Loads the worker's available font families and publishes the result.</summary>
    public Task<bool> ReadFontsAsync() => RunAuxiliaryAsync(
        s => s.Fonts, (s, operation) => s with { Fonts = operation },
        () => RequestAsync<FontCatalogDto>("fonts"));

    /// <summary>Sets or clears a notice for an operation handled by the studio or desktop host.</summary>
    /// <param name="source">Operation that owns the notice.</param>
    /// <param name="message">Notice to display, or null after that operation succeeds.</param>
    public void SetOperationNotice(StudioNoticeSource source, string? message) => Publish(s => s with
    {
        OperationNotices = message is null ? s.OperationNotices.Remove(source) : s.OperationNotices.SetItem(source, message)
    });

    /// <summary>Reads the notice owned by one studio or desktop operation.</summary>
    /// <param name="source">Operation whose current notice is requested.</param>
    public string? GetOperationNotice(StudioNoticeSource source) => Auxiliary.OperationNotices.GetValueOrDefault(source);

    /// <summary>Toggles Linux sensor guidance for the current studio session.</summary>
    public void ToggleSensorGuidance() => Publish(s => s with { SensorGuidanceVisible = !s.SensorGuidanceVisible });

    /// <summary>Remembers which raw sensor nodes the user has expanded.</summary>
    /// <param name="id">Stable device or sensor group identifier.</param>
    /// <param name="expanded">Whether the group is now expanded.</param>
    public void SetSensorGroupExpanded(string id, bool expanded) => Publish(s => s with
    {
        ExpandedSensorGroups = expanded ? s.ExpandedSensorGroups.Add(id) : s.ExpandedSensorGroups.Remove(id)
    });

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
        SelectExplicit(s, category, s.ScreenThemes.GetValueOrDefault(category, ""));
    });

    /// <summary>Selects a theme and updates the category's saved selection.</summary>
    /// <param name="theme">Stable theme identifier from the current catalog.</param>
    /// <param name="catalog">Available theme definitions used to validate and resolve selections.</param>
    public void SelectTheme(string theme, ThemeCatalogDto catalog) => model.Update(s =>
    {
        var selected = catalog.Themes.FirstOrDefault(t => t.Id == theme);
        if (selected is not null) { SelectExplicit(s, selected.Category, selected.Id); }
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

public enum StudioNoticeSource { Catalog, Devices, Preview, Restart, Tray }

public sealed record StudioOperation<T>(T? Value = default, bool Busy = false, string? Error = null);

public sealed record StudioAuxiliaryState
{
    public StudioOperation<CodexSetupStatus> Codex { get; init; } = new();
    public StudioOperation<bool> CodexPreparation { get; init; } = new();
    public StudioOperation<DiagnosticsStatus> Diagnostics { get; init; } = new();
    public StudioOperation<bool> OpenDiagnostics { get; init; } = new();
    public StudioOperation<FontCatalogDto> Fonts { get; init; } = new();
    public StudioOperation<SensorDriverDto> SensorDriver { get; init; } = new();
    public StudioOperation<bool> OpenSensorDriverFolder { get; init; } = new();
    public StudioOperation<SensorDebugDto> SensorDebug { get; init; } = new();
    public bool SensorGuidanceVisible { get; init; }
    public ImmutableHashSet<string> ExpandedSensorGroups { get; init; } = ImmutableHashSet.Create<string>(StringComparer.Ordinal);
    internal ImmutableSortedDictionary<StudioNoticeSource, string> OperationNotices { get; init; } = ImmutableSortedDictionary<StudioNoticeSource, string>.Empty;
    public IReadOnlyList<string> Notices => [.. OperationNotices.Values, .. new[] { Diagnostics.Error, OpenDiagnostics.Error }.OfType<string>()];
}
