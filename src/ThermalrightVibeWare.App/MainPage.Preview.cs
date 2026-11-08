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
using ThermalrightVibeWare.Studio;

namespace ThermalrightVibeWare.App;

/// <summary>Preview responsibilities of the studio page.</summary>
public sealed partial class MainPage
{

    /// <summary>Applies the latest worker snapshot to visible studio controls.</summary>
    /// <param name="queueIfBusy">Whether to wake the session worker; repeated wakeups do not build a frame backlog.</param>
    private Task RefreshAsync(bool queueIfBusy = true)
    {
        var visible = !unloaded && ready && preview?.IsVisible != false;
        if (ownsSession) { session.PreviewVisible = visible; }
        if (queueIfBusy) { session.Wake(); }
        if (!visible) { lastFrame = null; return Task.CompletedTask; }

        var current = session.Snapshot;
        var studio = viewModel.Read();
        var preferences = studio.Preferences;
        var workspace = viewModel.Workspace;
        ApplyAppearance(preferences);
        ShowSessionStatus(current);
        if (workspace.SensorDebugOpen) { return Task.CompletedTask; }
        if (preferences.SelectedCategory == "codex") { _ = RefreshCodexStatusAsync(); }
        ApplyLibrary(current, preferences);
        ApplyDevices(current.Devices, preferences.SelectedDeviceId);
        UpdateDisplayControls(current, preferences);
        UpdateNavigationState(viewModel.GetPage(preferences, current));
        RenderSensorDriverStatus();

        // Apply metadata only when the worker has published a new frame.
        if (current.Frame is not { } state || current.FrameRevision == appliedFrameRevision && lastFrame is not null) { return Task.CompletedTask; }
        UpdateProfiles(state.Profiles, preferences.ProfileSelection);
        if (!workspace.ShowsPreview || current.FrameRequestedThemeId != preferences.SelectedTheme || previewUpdating) { return Task.CompletedTask; }

        appliedFrameRevision = current.FrameRevision;
        var renderedTheme = state.ActiveThemeId ?? preferences.SelectedTheme;
        UpdateFrameHeading(renderedTheme, state.FrameSuppressed, current.Catalog, preferences.SelectedTheme);
        UpdateArtistSources(state.ArtistSources);
        ScreensHelpText.Text = studio.ScreenEnabled["codex"] && !state.CodexProcessRunning
            ? "Codex is closed. Its display screen is skipped."
            : "Preview any theme. Codex appears while running.";

        // The engine JPEG is reused as-is; keep at most one preview decode in flight.
        if (!state.FrameSuppressed && state.FrameJpeg is { Length: > 0 } frame)
        {
            UpdatePreviewAccent(state.AccentColor, preferences);
            if (lastFrame is null || !lastFrame.AsSpan().SequenceEqual(frame)) { _ = UpdatePreviewAsync(frame, renderedTheme); }
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
    /// <param name="selectedDeviceId">Saved device identifier, which may currently be absent.</param>
    private void ApplyDevices(PanelChoice[]? choices, string? selectedDeviceId)
    {
        if (choices is null || ReferenceEquals(choices, appliedDevices) && selectedDeviceId == appliedDeviceId) { return; }
        updatingChoices = true;
        try
        {
            if (!ReferenceEquals(choices, appliedDevices)) { DevicesBox.ItemsSource = choices; }
            DevicesBox.SelectedItem = choices.FirstOrDefault(item => item.Id == selectedDeviceId);
            appliedDevices = choices;
            appliedDeviceId = selectedDeviceId;
        }
        finally { updatingChoices = false; }
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
            viewModel.SetOperationNotice(StudioNoticeSource.Preview, null);
            FitPreviewSurface();
            LoadingPanel.Visibility = Visibility.Collapsed;
            PreviewBacking.Visibility = Visibility.Visible;
        }
        catch (Exception error)
        {
            if (unloaded || generation != previewGeneration || preview?.IsVisible == false) { return; }
            viewModel.SetOperationNotice(StudioNoticeSource.Preview, error.Message);
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
    /// <param name="selection">Persisted Auto or explicit profile identifier.</param>
    private void UpdateProfiles(IReadOnlyList<PanelChoice> profiles, string selection)
    {
        var choices = new[] { new PanelChoice("auto", "Auto size") }.Concat(profiles.Select(profile => new PanelChoice(profile.Id, ShortProfileName(profile.Id, profile.Name)))).ToArray();
        updatingChoices = true;
        try
        {
            if (ProfilesBox.ItemsSource is not IEnumerable<PanelChoice> existing || !existing.SequenceEqual(choices))
            {
                ProfilesBox.ItemsSource = choices;
                existing = choices;
            }
            ProfilesBox.SelectedItem = existing.FirstOrDefault(profile => profile.Id == selection);
        }
        finally { updatingChoices = false; }
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
