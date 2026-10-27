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

    /// <summary>Toggles the requested details panel while preserving its state.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Disclosure_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button selected) { return; }
        (Button Button, FrameworkElement Panel, string Title)[] sections =
        [
            (ScreensDetailsButton, ScreensDetails, "Screens on display"),
            (CodexDetailsButton, CodexDetails, "Codex connection"),
            (SensorDetailsButton, SensorDetails, "Sensor access"),
            (AppearanceDetailsButton, OptionsFrame, "Appearance"),
            (DisplayOptionsButton, DisplayOptionsPanel, "Display options"),
            (DiagnosticsDetailsButton, DiagnosticsDetails, "Diagnostics")
        ];
        var expand = sections.Any(section => section.Button == selected && section.Panel.Visibility != Visibility.Visible);
        var sidebar = selected == ScreensDetailsButton || selected == CodexDetailsButton || selected == SensorDetailsButton;
        foreach (var (button, panel, title) in sections)
        {
            if (button != selected && !(sidebar && (button == ScreensDetailsButton || button == CodexDetailsButton || button == SensorDetailsButton))) { continue; }
            var visible = button == selected && expand;
            panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            button.Content = (visible ? "⌄ " : "› ") + title;
        }
    }
}
