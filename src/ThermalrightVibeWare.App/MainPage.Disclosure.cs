/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Disclosure.cs
 * Purpose: Reveals advanced studio details while keeping brief status visible.
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

using System.Windows;
using System.Windows.Controls;
using ThermalrightVibeWare.Studio;

namespace ThermalrightVibeWare.App;

public sealed partial class MainPage
{

    /// <summary>Shows a brief session status while retaining detailed diagnostics.</summary>
    /// <param name="current">Latest session snapshot, including persistent errors and the current frame.</param>
    private void ShowSessionStatus(StudioSnapshot current)
    {
        var status = current.Status;
        var frame = current.Frame;
        var notices = new[] { current.PersistenceError, viewModel.PersistenceNotice }.OfType<string>()
            .Concat(viewModel.Auxiliary.Notices).Concat(current.Catalog?.Notices ?? []).Distinct(StringComparer.Ordinal).ToArray();
        TimingDetailsText.Text = string.Join("\n\n", notices.Append(status).Concat(frame?.Notices ?? []).Distinct(StringComparer.Ordinal));
        var routine = status is "Local preview" or "Local preview only. The USB display has not been started."
            or "Linux local preview. USB output, media sessions and audio are unavailable." or "Display running." or "Display running"
            || status.StartsWith("Display · requested ", StringComparison.Ordinal);
        var outputStatus = current.DisplayActive ? frame?.FrameSuppressed == true ? "Display paused." : "Display running." : "";
        var summary = routine ? outputStatus : status;
        if (string.IsNullOrEmpty(summary) && frame is not null)
        {
            if (!frame.FrameSuppressed && frame.FrameJpeg is null && frame.Notices.Count > 0)
            { summary = "Theme preview unavailable. See Diagnostics."; }
            else if (frame.System.Driver is { Available: false })
            { summary = "Some hardware readings are unavailable. See Diagnostics."; }
        }
        StatusText.Text = string.Join(" · ", notices.Append(summary).Where(message => !string.IsNullOrEmpty(message)).Distinct(StringComparer.Ordinal));
    }

    private void SessionDetails_Click(object sender, RoutedEventArgs args)
    {
        var visible = TimingDetailsText.Visibility != Visibility.Visible;
        TimingDetailsText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SessionDetailsButton.Content = visible ? "Hide details" : "Details";
    }

    /// <summary>Switches the central workspace or the separate raw sensor view.</summary>
    private async void StudioTab_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button button || ReadStudioSection(button) is not { } section) { return; }
        viewModel.SelectSection(section);
        var workspace = viewModel.Workspace;
        ApplyWorkspace(workspace);
        if (!workspace.ShowsPreview) { ClearPreview(); }
        FitPreviewSurface();
        if (!workspace.SensorDebugOpen && workspace.ThemeSection == StudioSection.Diagnostics) { await RefreshSensorDriverStatusAsync(); }
        await RefreshAsync();
    }

    /// <summary>Applies workspace visibility and signals demand for local preview.</summary>
    /// <param name="workspace">Selected workspace and central tab.</param>
    private void ApplyWorkspace(StudioWorkspace workspace)
    {
        var debug = workspace.SensorDebugOpen;
        var section = workspace.ThemeSection;
        ContentArea.Visibility = debug ? Visibility.Collapsed : Visibility.Visible;
        SensorDebugPanel.Visibility = debug ? Visibility.Visible : Visibility.Collapsed;
        AppearancePanel.Visibility = section == StudioSection.Appearance ? Visibility.Visible : Visibility.Collapsed;
        DisplayFrame.Visibility = section == StudioSection.Display ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsPanel.Visibility = section == StudioSection.Diagnostics ? Visibility.Visible : Visibility.Collapsed;
        PreviewFrame.Visibility = ThemeHeadingArea.Visibility = workspace.ShowsPreview ? Visibility.Visible : Visibility.Collapsed;
        NavigationArea.Visibility = section == StudioSection.Preview ? Visibility.Visible : Visibility.Collapsed;
        session.PreviewRequested = workspace.ShowsPreview;
        UpdateStudioTabs(workspace);
    }

    /// <summary>Marks top-level and central selections with matching palette contrast.</summary>
    /// <param name="workspace">Selected workspace and central tab.</param>
    private void UpdateStudioTabs(StudioWorkspace workspace)
    {
        foreach (var button in new[] { ThemesTab, SensorsTab, PreviewTab, AppearanceTab, DisplayTab, DiagnosticsTab })
        {
            var section = ReadStudioSection(button);
            var selected = section == StudioSection.SensorDebug ? workspace.SensorDebugOpen
                : !workspace.SensorDebugOpen && (section == StudioSection.Themes || section == workspace.ThemeSection);
            button.Background = Brush(selected ? IsLight ? "#E5EEF3" : "#1E303C" : IsLight ? "#F6F7F9" : "#101218");
            button.Foreground = Brush(IsLight ? "#242231" : "#F1F1FA");
            button.BorderBrush = Brush(selected ? IsLight ? "#276C89" : "#7BC8DF" : IsLight ? "#F6F7F9" : "#101218");
            button.BorderThickness = new Thickness(0, 0, 0, 2);
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    /// <summary>Maps a workspace button's XAML tag to its model value.</summary>
    /// <param name="button">Top-level workspace or central tab button.</param>
    private static StudioSection? ReadStudioSection(Button button) => button.Tag switch
    {
        "themes" => StudioSection.Themes,
        "sensor-debug" => StudioSection.SensorDebug,
        "preview" => StudioSection.Preview,
        "appearance" => StudioSection.Appearance,
        "display" => StudioSection.Display,
        "diagnostics" => StudioSection.Diagnostics,
        _ => null
    };
}
