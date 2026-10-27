/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Preview.cs
 * Purpose: Applies worker snapshots and displays the engine's existing JPEG.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Created with AI: OpenAI Codex; extracted existing first-party source; 2026-10-27.
 * Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard file headers.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */


using System.Windows;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.App;

/// <summary>Preview responsibilities of the studio page.</summary>
public sealed partial class MainPage
{

    /// <summary>Applies the latest worker snapshot to visible studio controls.</summary>
    /// <param name="queueIfBusy">Whether to wake the session worker; repeated wakeups do not build a frame backlog.</param>
    private Task RefreshAsync(bool queueIfBusy = true)
    {
        session.PreviewVisible = SensorDebugPanel.Visibility != Visibility.Visible && preview?.IsVisible != false;
        if (queueIfBusy) { session.Wake(); }
        var previewUnavailable = unloaded || !ready || preview?.IsVisible == false;
        if (previewUnavailable) { lastFrame = null; return Task.CompletedTask; }

        // Observe one published frame revision; the UI never schedules a second engine loop.
        var current = session.Snapshot;
        ApplyAppearance();
        if (SensorDebugPanel.Visibility == Visibility.Visible) { ShowSessionStatus(current.Status); return Task.CompletedTask; }
        if (selectedCategory == "codex") { _ = RefreshCodexStatusAsync(); }
        ApplyDevices(current.Devices);
        var p = viewModel.Preferences;
        if ((CategoriesBox.SelectedItem as CatalogRow)?.Id != p.SelectedCategory || (ThemesBox.SelectedItem as CatalogRow)?.Id != p.SelectedTheme)
        {
            updatingChoices = true;
            CategoriesBox.SelectedItem = categoryRows.FirstOrDefault(row => row.Id == p.SelectedCategory);
            PopulateThemes();
            updatingChoices = false;
        }
        UpdateDisplayControls();
        ShowSessionStatus(current.Status);

        // Apply metadata only when the worker has published a new frame.
        if (current.Frame is not { } state || current.FrameRevision == appliedFrameRevision && lastFrame is not null) { return Task.CompletedTask; }
        appliedFrameRevision = current.FrameRevision;
        UpdateProfiles(state.Profiles);
        var renderedTheme = state.ActiveThemeId ?? selectedTheme;
        var category = catalog?.Themes.FirstOrDefault(theme => theme.Id == renderedTheme)?.Category ?? selectedCategory;
        UpdateFrameHeading(renderedTheme, state.FrameSuppressed);
        UpdateNavigationState(renderedTheme, state);
        UpdateArtistSources(state.ArtistSources);
        SourceText.Text = state.FrameSuppressed ? "No enabled screen is available." : category == "codex" ? "OpenAI Codex · local activity"
            : category == "system" ? "Local hardware sensors" : state.Source;
        if (state.System.Driver is { } driver) { ApplySensorDriverStatus(driver); }
        if (state.Notices.Count > 0) { StatusText.Text = string.Join(" · ", new[] { viewModel.PersistenceNotice }.Where(n => n is not null).Concat(state.Notices)); }
        ScreensHelpText.Text = screenEnabled["codex"] && !codexProcessRunning
            ? "Codex is closed. Its display screen is skipped."
            : "Preview any theme. Codex appears while running.";

        // The engine JPEG is reused as-is; keep at most one preview decode in flight.
        if (!state.FrameSuppressed && state.FrameJpeg is { Length: > 0 } frame)
        {
            UpdatePreviewAccent(state.AccentColor);
            if (!previewUpdating && (lastFrame is null || !lastFrame.AsSpan().SequenceEqual(frame))) { _ = UpdatePreviewAsync(frame, renderedTheme); }
        }
        else
        {
            ClearPreview();
            LoadingTitle.Text = state.FrameSuppressed ? "No active screens." : "The theme needs attention.";
            LoadingText.Text = state.Notices.Count > 0 ? string.Join(" · ", state.Notices) : "No frame is available yet.";
        }
        return Task.CompletedTask;
    }

    /// <summary>Applies a changed device list without overwriting the saved selection.</summary>
    /// <param name="choices">Latest device choices, or null before the first discovery completes.</param>
    private void ApplyDevices(PanelChoice[]? choices)
    {
        if (choices is null || ReferenceEquals(choices, appliedDevices)) { return; }
        appliedDevices = choices;
        var wasUpdating = updatingChoices;
        updatingChoices = true;
        DevicesBox.ItemsSource = choices;
        DevicesBox.SelectedItem = choices.FirstOrDefault(item => item.Id == selectedDeviceId);
        updatingChoices = wasUpdating;
    }

    /// <summary>Displays a changed JPEG and discards stale preview completions.</summary>
    /// <param name="frame">Already encoded JPEG bytes from the engine.</param>
    /// <param name="renderedTheme">Stable identifier of the theme that produced this JPEG.</param>
    private async Task UpdatePreviewAsync(byte[] frame, string renderedTheme)
    {
        previewUpdating = true;
        var generation = previewGeneration;
        try
        {
            if (preview is null) { throw new InvalidOperationException("The desktop preview is unavailable."); }
            var themeChanged = lastRenderedTheme is not null && lastRenderedTheme != renderedTheme;
            var size = await preview.ShowAsync(frame, themeChanged);
            // A completed decode may belong to a preview cleared while this await was pending.
            if (unloaded || generation != previewGeneration || !preview.IsVisible) { return; }
            previewPixelWidth = size.Width;
            previewPixelHeight = size.Height;
            lastFrame = frame;
            lastRenderedTheme = renderedTheme;
            FitPreviewSurface();
            LoadingPanel.Visibility = Visibility.Collapsed;
            PreviewBacking.Visibility = Visibility.Visible;
        }
        catch (Exception error)
        {
            if (unloaded || generation != previewGeneration || preview?.IsVisible == false) { return; }
            StatusText.Text = error.Message;
            ClearPreview();
            LoadingTitle.Text = "Preview unavailable.";
            LoadingText.Text = error.Message;
        }
        finally { previewUpdating = false; }
    }

    /// <summary>Invalidates in-flight previews and restores the empty-preview message.</summary>
    private void ClearPreview()
    {
        previewGeneration++;
        preview?.Clear();
        lastFrame = null;
        lastRenderedTheme = null;
        PreviewBacking.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Visible;
    }

    /// <summary>Updates profile choices while preserving the current selection.</summary>
    /// <param name="profiles">Validated display profile choices returned by the engine.</param>
    private void UpdateProfiles(IReadOnlyList<PanelChoice> profiles)
    {
        var choices = new[] { new PanelChoice("auto", "Auto size") }.Concat(profiles.Select(profile => new PanelChoice(profile.Id, ShortProfileName(profile.Id, profile.Name)))).ToArray();
        if (ProfilesBox.ItemsSource is IEnumerable<PanelChoice> existing && existing.SequenceEqual(choices)) { return; }
        updatingChoices = true;
        ProfilesBox.ItemsSource = choices;
        ProfilesBox.SelectedItem = choices.FirstOrDefault(profile => profile.Id == profileSelection) ?? choices.First();
        profileSelection = ((PanelChoice)ProfilesBox.SelectedItem).Id;
        updatingChoices = false;
    }

    /// <summary>Formats known profile dimensions for the studio's compact selector.</summary>
    /// <param name="id">Stable profile identifier containing the catalog dimension suffix.</param>
    /// <param name="name">Full catalog profile label used when dimensions cannot be derived from its ID.</param>
    private static string ShortProfileName(string id, string name)
    {
        var dimensions = id.Replace("wide-", "").Replace("landscape-", "").Replace("portrait-", "");
        if (dimensions.StartsWith("square-", StringComparison.Ordinal)) { dimensions = dimensions[7..] + " × " + dimensions[7..]; }
        return dimensions.Any(char.IsDigit) ? dimensions.Replace("x", " × ") : name;
    }
}
