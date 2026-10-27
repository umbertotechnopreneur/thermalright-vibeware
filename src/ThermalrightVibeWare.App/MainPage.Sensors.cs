/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Sensors.cs
 * Purpose: Shows driver metadata and the requested driver folder action.
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

using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.App;

public sealed partial class MainPage
{
    private bool sensorDriverReading;
    private string? sensorDriverActionNotice;

    /// <summary>Loads driver metadata for the sensor setup disclosure.</summary>
    private async Task RefreshSensorDriverStatusAsync()
    {
        if (!ready || sensorDriverReading) { return; }
        sensorDriverReading = true;
        try { ApplySensorDriverStatus(await viewModel.ReadSensorDriverAsync()); }
        catch (Exception error)
        {
            SensorDriverText.Text = "Sensor driver status could not be read: " + error.Message;
            SensorSummaryText.Text = "Access check unavailable";
            SensorDriverFolderButton.IsEnabled = false;
        }
        finally { sensorDriverReading = false; }
    }

    /// <summary>Shows driver availability and its verified local path.</summary>
    /// <param name="driver">Driver installation metadata; availability does not prove sensor access.</param>
    private void ApplySensorDriverStatus(SensorDriverDto driver)
    {
        SensorSummaryText.Text = sensorDriverActionNotice is not null ? "Driver folder unavailable"
            : windowActions?.CanRestartAsAdministrator == true ? "Administrator access needed"
            : driver.Available ? "PawnIO found" : "PawnIO unavailable · see details";
        SensorDriverText.Text = sensorDriverActionNotice ?? driver.Notice ?? (driver.Available
            ? "PawnIO installation found. Individual sensor readings show what this computer exposes."
            : "PawnIO is unavailable. CPU temperature and motherboard/fan readings may be missing.");
        var path = driver.Path ?? "";
        if (SensorDriverPath.Text != path) { SensorDriverPath.Text = path; }
        SensorDriverPath.Visibility = path.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        SensorDriverFolderButton.IsEnabled = path.Length > 0;
        ToolTipService.SetToolTip(SensorDriverPath, driver.FileExists ? path : path + " (file not present)");
        ToolTipService.SetToolTip(SensorDriverText, SensorDriverText.Text);
    }

    /// <summary>Opens the verified sensor driver folder on request.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void SensorDriverFolder_Click(object sender, RoutedEventArgs args)
    {
        SensorDriverFolderButton.IsEnabled = false;
        sensorDriverActionNotice = null;
        try
        {
            var result = await viewModel.OpenSensorDriverFolderAsync();
            if (!result.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True)
            { sensorDriverActionNotice = "The driver location is not available. No folder was opened; nothing was installed or allowed."; }
        }
        catch (Exception error) { sensorDriverActionNotice = "Could not open the driver folder: " + error.Message; }
        finally { await RefreshSensorDriverStatusAsync(); }
    }
}
