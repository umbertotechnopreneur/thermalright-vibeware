/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Display.cs
 * Purpose: Handles explicit device selection, start/stop and output quality choices.
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
using System.Windows.Controls;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.App;

/// <summary>Display responsibilities of the studio page.</summary>
public sealed partial class MainPage
{

    /// <summary>Enables display controls according to session activity and pending startup.</summary>
    private void UpdateDisplayControls()
    {
        StartDisplayButton.IsEnabled = !displayBusy && !displayActive && !devicesLoading && DevicesBox.SelectedItem is PanelChoice && !string.IsNullOrEmpty(selectedTheme);
        StopDisplayButton.IsEnabled = !displayBusy && (displayActive || startupRestorePending);
        DevicesBox.IsEnabled = !displayBusy && !displayActive && !devicesLoading;
        RefreshDevicesButton.IsEnabled = !displayBusy && !displayActive && !devicesLoading;
        ProfilesBox.IsEnabled = !displayBusy && !displayActive;
        PreviewBadgeText.Text = displayActive ? frameSuppressed ? "DISPLAY PAUSED" : "LIVE DISPLAY" : "LOCAL PREVIEW";
    }

    /// <summary>Refreshes the available display choices.</summary>
    private async Task RefreshDevicesAsync()
    {
        if (devicesLoading) { return; }
        devicesLoading = true;
        try { await session.RefreshDevicesAsync(); ApplyDevices(session.Snapshot.Devices); }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { devicesLoading = false; UpdateDisplayControls(); }
    }

    /// <summary>Starts the explicitly selected display session.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void StartDisplay_Click(object sender, RoutedEventArgs args)
    {
        if (displayBusy || displayActive || DevicesBox.SelectedItem is not PanelChoice device) { return; }
        await session.StartAsync(device.Id);
        await RefreshAsync();
    }

    /// <summary>Stops display output and the saved-device startup wait.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void StopDisplay_Click(object sender, RoutedEventArgs args)
    {
        if (displayBusy || !displayActive && !startupRestorePending) { return; }
        await session.StopAsync();
        await RefreshAsync();
    }

    /// <summary>Refreshes the connected display choices.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void RefreshDevices_Click(object sender, RoutedEventArgs args) => await RefreshDevicesAsync();

    /// <summary>Saves the selected display identifier.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Device_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        if (DevicesBox.SelectedItem is PanelChoice device)
        {
            session.CancelStartup();
            selectedDeviceId = device.Id;
            SaveChoices();
        }
        UpdateDisplayControls();
    }

    /// <summary>Saves the selected refresh interval and updates the UI timer.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Refresh_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || RefreshBox.SelectedItem is not PanelChoice choice) { return; }
        refreshMilliseconds = int.Parse(choice.Id, System.Globalization.CultureInfo.InvariantCulture);
        refresh.Interval = TimeSpan.FromMilliseconds(refreshMilliseconds);
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Saves the selected JPEG quality and requests a preview.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Quality_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || QualityBox.SelectedItem is not PanelChoice choice) { return; }
        jpegQuality = choice.Id;
        SaveChoices();
        _ = RefreshAsync();
    }
}
