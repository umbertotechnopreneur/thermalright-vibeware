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

namespace ThermalrightVibeWare.App;

public sealed partial class MainPage
{

    /// <summary>Shows a brief session status while retaining detailed diagnostics.</summary>
    /// <param name="status">Available status text shown to the user.</param>
    private void ShowSessionStatus(string status)
    {
        TimingDetailsText.Text = status;
        StatusText.Text = viewModel.PersistenceNotice ?? (status.StartsWith("Display · requested ", StringComparison.Ordinal)
            ? "Display running" : status);
    }

    private string studioSection = "preview";

    /// <summary>Switches the central workspace or the separate raw sensor view.</summary>
    private async void StudioTab_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string section }) { return; }
        var debug = section == "sensor-debug";
        if (section != "themes" && !debug) { studioSection = section; }
        ContentArea.Visibility = debug ? Visibility.Collapsed : Visibility.Visible;
        SensorDebugPanel.Visibility = debug ? Visibility.Visible : Visibility.Collapsed;
        AppearancePanel.Visibility = studioSection == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        DisplayFrame.Visibility = studioSection == "display" ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsPanel.Visibility = studioSection == "diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        PreviewFrame.Visibility = ThemeHeadingArea.Visibility = studioSection == "diagnostics" ? Visibility.Collapsed : Visibility.Visible;
        NavigationArea.Visibility = studioSection == "preview" ? Visibility.Visible : Visibility.Collapsed;
        UpdateStudioTabs();
        FitPreviewSurface();
        if (!debug && studioSection == "diagnostics") { await RefreshSensorDriverStatusAsync(); }
        await RefreshAsync();
    }

    /// <summary>Marks top-level and central selections with matching palette contrast.</summary>
    private void UpdateStudioTabs()
    {
        var debug = SensorDebugPanel.Visibility == Visibility.Visible;
        foreach (var button in new[] { ThemesTab, SensorsTab, PreviewTab, AppearanceTab, DisplayTab, DiagnosticsTab })
        {
            var tag = button.Tag as string;
            var selected = tag == "themes" ? !debug : tag == "sensor-debug" ? debug : tag == studioSection;
            button.Background = Brush(selected ? IsLight ? "#E5EEF3" : "#1E303C" : IsLight ? "#F6F7F9" : "#101218");
            button.Foreground = Brush(IsLight ? "#242231" : "#F1F1FA");
            button.BorderBrush = Brush(selected ? IsLight ? "#276C89" : "#7BC8DF" : IsLight ? "#F6F7F9" : "#101218");
            button.BorderThickness = new Thickness(0, 0, 0, 2);
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }
}
