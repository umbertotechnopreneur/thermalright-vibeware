/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: StudioModel.cs
 * Purpose: Protects mutable studio state and returns independent snapshots.
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

using ThermalrightVibeWare.Contracts;
namespace ThermalrightVibeWare.Studio;

/// <summary>One transaction owns preferences, screen choices and palettes. All reads are detached.</summary>
public sealed class StudioModel
{
    private readonly object sync = new();
    private readonly StudioSettingsStore store;
    private StudioState state;

    /// <summary>Loads the studio state from the selected settings store.</summary>
    /// <param name="path">Local settings, manifest or activity JSON file path used by this operation.</param>
    public StudioModel(string path) { store = new(path); state = store.Load(); }
    public string SettingsPath => store.Path;
    public string? PersistenceNotice => store.Notice;

    /// <summary>Copies the current studio state while holding its lock.</summary>
    public StudioState Read() { lock (sync) { return state.Copy(); } }
    public StudioPreferences Preferences => Read().Preferences;
    public IReadOnlyDictionary<string, string> ScreenThemes => Read().ScreenThemes;
    public IReadOnlyDictionary<string, bool> ScreenEnabled => Read().ScreenEnabled;
    public IReadOnlyDictionary<string, ThemePalette> Palettes => Read().Palettes;

    /// <summary>Applies a state mutation while holding the model lock.</summary>
    /// <param name="edit">Mutation applied while the owning model or snapshot lock is held.</param>
    public void Update(Action<StudioState> edit)
    {
        lock (sync)
        {
            var next = state.Copy();
            edit(next);
            // Detach again: a caller retaining its editor cannot change committed state.
            next.Revision = state.Revision + 1;
            state = next.Copy();
        }
    }

    /// <summary>Applies an edit to the studio preferences under the model lock.</summary>
    /// <param name="edit">Mutation applied while the owning model or snapshot lock is held.</param>
    public void Edit(Action<StudioPreferences> edit) => Update(s => edit(s.Preferences));

    /// <summary>Projects saved category selections into engine screen options.</summary>
    public ScreenSelectionDto[] Screens() => Read().Screens();

    /// <summary>Persists the current settings snapshot.</summary>
    public void Save() { lock (sync) { store.Save(state); } }

    /// <summary>Records the first visible studio load only after its settings were saved successfully.</summary>
    /// <remarks>A failed save leaves the flag true, so the next launch still shows the main window.</remarks>
    public void CompleteFirstRun()
    {
        lock (sync)
        {
            if (!state.Preferences.IsFirstRun) { return; }

            var next = state.Copy();
            next.Preferences.IsFirstRun = false;
            next.Revision = state.Revision + 1;
            store.Save(next);
            state = next;
        }
    }
}

public sealed class StudioState
{
    public long Revision { get; internal set; }
    public StudioPreferences Preferences { get; init; } = new();
    public Dictionary<string, string> ScreenThemes { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, bool> ScreenEnabled { get; init; } = new(StringComparer.Ordinal)
    { ["multimedia"] = true, ["codex"] = false, ["system"] = true };
    public Dictionary<string, ThemePalette> Palettes { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Returns an independent copy so callers cannot mutate the published snapshot.</summary>
    public StudioState Copy() => new()
    {
        Revision = Revision,
        Preferences = Preferences with { },
        ScreenThemes = new(ScreenThemes, StringComparer.Ordinal),
        ScreenEnabled = new(ScreenEnabled, StringComparer.Ordinal),
        Palettes = new(Palettes, StringComparer.Ordinal)
    };

    /// <summary>Projects saved category selections into engine screen options.</summary>
    public ScreenSelectionDto[] Screens() => ScreenEnabled.OrderBy(p => p.Key)
        .Select(p => new ScreenSelectionDto(p.Key, ScreenThemes.GetValueOrDefault(p.Key, ""), p.Value)).ToArray();
}
