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
    private bool sensorGuidanceVisible;
    private IReadOnlyList<SensorSourceDto> sensorSources = [];

    /// <summary>Loads driver metadata for the sensor setup disclosure.</summary>
    private async Task RefreshSensorDriverStatusAsync()
    {
        if (!ready || sensorDriverReading) { return; }
        sensorDriverReading = true;
        try { ApplySensorDriverStatus(await viewModel.ReadSensorDriverAsync()); }
        catch (Exception error)
        {
            SensorDriverText.Visibility = Visibility.Visible;
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
        var linux = session.Snapshot.Hello?.Platform == "linux";
        SensorSetupHeading.Text = "Driver details";
        SensorSetupHeading.Visibility = linux ? Visibility.Collapsed : Visibility.Visible;
        SensorDriverHelp.Visibility = linux ? Visibility.Collapsed : Visibility.Visible;
        SensorDriverHelp.Text = "Install PawnIO separately, then restart. TermalRight VibeWare does not install drivers.";
        SensorSummaryText.Text = linux ? driver.Available ? "Readings available" : "Access not confirmed"
            : sensorDriverActionNotice is not null ? "Driver folder unavailable"
            : windowActions?.CanRestartAsAdministrator == true ? "Administrator access needed"
            : driver.Available ? "PawnIO found" : "PawnIO unavailable · see details";
        SensorDriverText.Text = sensorDriverActionNotice ?? driver.Notice ?? (driver.Available
            ? "PawnIO installation found. Individual sensor readings show what this computer exposes."
            : "PawnIO is unavailable. CPU temperature and motherboard/fan readings may be missing.");
        SensorDriverText.Visibility = linux && driver.Available ? Visibility.Collapsed : Visibility.Visible;
        sensorSources = driver.Sources?.Where(source => source.Name != "Latest sensor sample").ToArray() ?? [];
        ShowSensorSources();
        SensorGuidanceButton.Visibility = linux && driver.Guidance?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SensorGuidanceText.Text = driver.Guidance is null ? "" : string.Join("\n\n", driver.Guidance);
        SensorGuidanceText.Visibility = linux && sensorGuidanceVisible ? Visibility.Visible : Visibility.Collapsed;
        var path = driver.Path ?? "";
        if (SensorDriverPath.Text != path) { SensorDriverPath.Text = path; }
        SensorDriverPath.Visibility = path.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        SensorDriverFolderButton.IsEnabled = session.Supports("sensors.driver.open") && path.Length > 0;
        SensorDriverFolderButton.Visibility = linux ? Visibility.Collapsed : Visibility.Visible;
        ToolTipService.SetToolTip(SensorDriverPath, driver.FileExists ? path : path + " (file not present)");
        ToolTipService.SetToolTip(SensorDriverText, SensorDriverText.Text);
    }

    /// <summary>Reveals Linux setup guidance without changing access or saved settings.</summary>
    private void SensorGuidance_Click(object sender, RoutedEventArgs args)
    {
        sensorGuidanceVisible = !sensorGuidanceVisible;
        SensorGuidanceText.Visibility = sensorGuidanceVisible ? Visibility.Visible : Visibility.Collapsed;
        SensorGuidanceButton.Content = sensorGuidanceVisible ? "Back to detected drivers" : "How to get more readings";
        ShowSensorSources();
    }

    private void ShowSensorSources()
    {
        var split = (sensorSources.Count + 1) / 2;
        var columns = new[] { sensorSources.Take(split).ToArray(), sensorSources.Skip(split).ToArray() };
        var labels = new[] { SensorSourcesText, SensorSourcesRightText };
        SensorSourcesPanel.Visibility = sensorSources.Count > 0 && !sensorGuidanceVisible ? Visibility.Visible : Visibility.Collapsed;
        for (var index = 0; index < columns.Length; index++)
        {
            labels[index].Text = string.Join("\n", columns[index].Select(source => $"{source.Name} · {source.Status}"));
            ToolTipService.SetToolTip(labels[index], string.Join("\n\n", columns[index].Select(source => $"{source.Name}: {source.Detail}")));
        }
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
