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

using System.Windows;
using System.Windows.Controls;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Studio;

namespace ThermalrightVibeWare.App;

public sealed partial class MainPage
{
    private (StudioOperation<SensorDriverDto> Status, StudioOperation<bool> Folder, bool GuidanceVisible)? appliedSensorDriver;

    /// <summary>Loads driver metadata for the sensor setup disclosure.</summary>
    private async Task RefreshSensorDriverStatusAsync()
    {
        if (!ready || unloaded) { return; }
        var pending = viewModel.ReadSensorDriverAsync();
        RenderSensorDriverStatus();
        await pending;
        RenderSensorDriverStatus();
    }

    /// <summary>Projects the latest driver observation and driver folder operation into the disclosure.</summary>
    private void RenderSensorDriverStatus()
    {
        if (unloaded) { return; }

        var auxiliary = viewModel.Auxiliary;
        var status = auxiliary.SensorDriver;
        var folder = auxiliary.OpenSensorDriverFolder;
        var guidanceVisible = auxiliary.SensorGuidanceVisible;
        if (appliedSensorDriver is { } applied && ReferenceEquals(status, applied.Status)
            && ReferenceEquals(folder, applied.Folder) && guidanceVisible == applied.GuidanceVisible) { return; }
        appliedSensorDriver = (status, folder, guidanceVisible);

        if (status.Value is not { } driver)
        {
            SensorDriverFolderButton.IsEnabled = false;
            if (status.Error is { } error)
            {
                SensorDriverText.Visibility = Visibility.Visible;
                SensorDriverText.Text = "Sensor driver status could not be read: " + error;
                SensorSummaryText.Text = "Access check unavailable";
            }
            return;
        }

        var linux = session.Snapshot.Hello?.Platform == "linux";
        SensorSetupHeading.Text = "Driver details";
        SensorSetupHeading.Visibility = linux ? Visibility.Collapsed : Visibility.Visible;
        SensorDriverHelp.Visibility = linux ? Visibility.Collapsed : Visibility.Visible;
        SensorDriverHelp.Text = "Install PawnIO separately, then restart. TermalRight VibeWare does not install drivers.";
        SensorSummaryText.Text = status.Error is not null ? "Access check unavailable"
            : folder.Error is not null ? "Driver folder unavailable"
            : linux ? driver.Available ? "Readings available" : "Access not confirmed"
            : windowActions?.CanRestartAsAdministrator == true ? "Administrator access needed"
            : driver.Available ? "PawnIO found" : "PawnIO unavailable · see details";
        var errorNotice = status.Error is { } statusError ? "Sensor driver status could not be read: " + statusError
            : folder.Error is { } folderError ? "Could not open the driver folder: " + folderError : null;
        SensorDriverText.Text = errorNotice ?? driver.Notice ?? (driver.Available
            ? "PawnIO installation found. Individual sensor readings show what this computer exposes."
            : "PawnIO is unavailable. CPU temperature and motherboard/fan readings may be missing.");
        SensorDriverText.Visibility = linux && driver.Available && errorNotice is null ? Visibility.Collapsed : Visibility.Visible;
        var sources = driver.Sources?.Where(source => source.Name != "Latest sensor sample").ToArray() ?? [];
        ShowSensorSources(sources, guidanceVisible);
        SensorGuidanceButton.Visibility = linux && driver.Guidance?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SensorGuidanceButton.Content = guidanceVisible ? "Back to detected drivers" : "How to get more readings";
        SensorGuidanceText.Text = driver.Guidance is null ? "" : string.Join("\n\n", driver.Guidance);
        SensorGuidanceText.Visibility = linux && guidanceVisible ? Visibility.Visible : Visibility.Collapsed;
        var path = driver.Path ?? "";
        if (SensorDriverPath.Text != path) { SensorDriverPath.Text = path; }
        SensorDriverPath.Visibility = path.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        SensorDriverFolderButton.IsEnabled = !folder.Busy && !status.Busy && status.Error is null
            && session.Supports("sensors.driver.open") && path.Length > 0;
        SensorDriverFolderButton.Visibility = linux ? Visibility.Collapsed : Visibility.Visible;
        ToolTipService.SetToolTip(SensorDriverPath, driver.FileExists ? path : path + " (file not present)");
        ToolTipService.SetToolTip(SensorDriverText, SensorDriverText.Text);
    }

    /// <summary>Reveals Linux setup guidance without changing access or saved settings.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void SensorGuidance_Click(object sender, RoutedEventArgs args)
    {
        viewModel.ToggleSensorGuidance();
        RenderSensorDriverStatus();
    }

    /// <summary>Arranges detected sensor sources into two visual columns.</summary>
    /// <param name="sources">Sensor source metadata from the latest driver observation.</param>
    /// <param name="guidanceVisible">Whether the guidance disclosure replaces the source list.</param>
    private void ShowSensorSources(IReadOnlyList<SensorSourceDto> sources, bool guidanceVisible)
    {
        var split = (sources.Count + 1) / 2;
        var columns = new[] { sources.Take(split).ToArray(), sources.Skip(split).ToArray() };
        var labels = new[] { SensorSourcesText, SensorSourcesRightText };
        SensorSourcesPanel.Visibility = sources.Count > 0 && !guidanceVisible ? Visibility.Visible : Visibility.Collapsed;
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
        var pending = viewModel.OpenSensorDriverFolderAsync();
        RenderSensorDriverStatus();
        await pending;
        await RefreshSensorDriverStatusAsync();
    }
}
