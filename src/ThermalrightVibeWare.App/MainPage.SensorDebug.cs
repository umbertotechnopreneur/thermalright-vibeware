/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.SensorDebug.cs
 * Purpose: Presents raw sensor diagnostics without applying theme projection filters.
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

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.App;

public sealed partial class MainPage
{
    private bool sensorDebugReading;
    private SensorDebugDto? lastSensorDebug;

    /// <summary>Returns from sensor diagnostics to the theme preview.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void ThemesTab_Click(object sender, RoutedEventArgs args)
    {
        SensorDebugPanel.Visibility = Visibility.Collapsed;
        ContentArea.Visibility = Visibility.Visible;
        session.PreviewVisible = preview?.IsVisible != false;
        ApplySensorDebugAppearance();
    }

    /// <summary>Opens the raw sensor diagnostic view and refreshes its observation.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void SensorsTab_Click(object sender, RoutedEventArgs args)
    {
        ContentArea.Visibility = Visibility.Collapsed;
        SensorDebugPanel.Visibility = Visibility.Visible;
        session.PreviewVisible = false;
        ApplySensorDebugAppearance();
        await RefreshSensorDebugAsync();
    }

    /// <summary>Refreshes raw sensor diagnostics on request.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void SensorDebugRefresh_Click(object sender, RoutedEventArgs args) => await RefreshSensorDebugAsync();

    /// <summary>Requests raw sensor diagnostics while preventing overlapping refreshes.</summary>
    private async Task RefreshSensorDebugAsync()
    {
        if (!ready || sensorDebugReading) { return; }
        sensorDebugReading = true;
        SensorDebugRefresh.IsEnabled = false;
        SensorDebugNotice.Text = "Reading hardware sensors…";
        try
        {
            var result = await viewModel.ReadSensorDebugAsync();
            if (unloaded) { return; }
            lastSensorDebug = result;
            RenderSensorDebug();
        }
        catch (Exception error)
        {
            SensorDebugNotice.Text = "Refresh failed. Previous readings are stale: " + error.Message;
        }
        finally { sensorDebugReading = false; SensorDebugRefresh.IsEnabled = true; }
    }

    /// <summary>Projects the latest diagnostic snapshot into grouped UI rows.</summary>
    private void RenderSensorDebug()
    {
        if (lastSensorDebug is not { } sample) { return; }
        var expanded = new Dictionary<string, bool>();
        // items are the current tree rows; retain expansion state before rebuilding their observations.
        void Remember(ItemCollection items)
        {
            foreach (var item in items.OfType<TreeViewItem>())
            {
                if (item.Tag is string key) { expanded[key] = item.IsExpanded; }
                Remember(item.Items);
            }
        }
        Remember(SensorDebugTree.Items);
        SensorDebugTree.Items.Clear();
        var nodes = new Dictionary<string, TreeViewItem>();
        foreach (var device in sample.Devices)
        {
            var node = new TreeViewItem
            {
                Header = DebugText($"{device.Name} · {device.Type}", true),
                Tag = device.Id,
                IsExpanded = expanded.GetValueOrDefault(device.Id, false)
            };
            ToolTipService.SetToolTip(node, device.Id);
            nodes.TryAdd(device.Id, node);
        }
        foreach (var device in sample.Devices)
        {
            var node = nodes[device.Id];
            if (device.ParentId is { } parentId && parentId != device.Id && nodes.TryGetValue(parentId, out var parent))
            { parent.Items.Add(node); }
            else { SensorDebugTree.Items.Add(node); }
            if (device.Notice is { } notice) { node.Items.Add(new TreeViewItem { Header = DebugText(notice) }); }
            foreach (var group in device.Sensors.GroupBy(sensor => sensor.Type))
            {
                var groupId = device.Id + "::" + group.Key;
                var category = new TreeViewItem
                {
                    Header = DebugText($"{group.Key} · {group.Count()}"),
                    Tag = groupId,
                    IsExpanded = expanded.GetValueOrDefault(groupId, false)
                };
                category.Items.Add(new TreeViewItem { Header = SensorDebugRow("Sensor", "Current", "Minimum", "Maximum", true) });
                foreach (var sensor in group)
                {
                    var row = new TreeViewItem
                    {
                        Header = SensorDebugRow(sensor.Name, DebugValue(sensor.Value, sensor.Unit),
                        DebugValue(sensor.Minimum, sensor.Unit), DebugValue(sensor.Maximum, sensor.Unit))
                    };
                    ToolTipService.SetToolTip(row, sensor.Id + (sensor.Notice is null ? "" : " · " + sensor.Notice));
                    foreach (var parameter in sensor.Parameters ?? [])
                    {
                        var item = new TreeViewItem { Header = DebugText($"{parameter.Name}: {DebugValue(parameter.Value, "")} · default {DebugValue(parameter.DefaultValue, "")}") };
                        ToolTipService.SetToolTip(item, parameter.Id + " · " + parameter.Description);
                        row.Items.Add(item);
                    }
                    category.Items.Add(row);
                }
                node.Items.Add(category);
            }
        }
        var total = sample.Devices.Sum(device => device.Sensors.Count);
        SensorDebugSummary.Text = $"LibreHardwareMonitor {sample.LibraryVersion} · {sample.Devices.Count} devices · {total} sensors · " +
            $"{(sample.Administrator ? "Administrator" : "Standard user")} · sampled {sample.SampledAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        SensorDebugNotice.Text = string.Join("\n", sample.Notices);
    }

    /// <summary>Creates a diagnostic text control using the current studio palette.</summary>
    /// <param name="text">Text to format, normalize or display in this operation.</param>
    /// <param name="heading">Whether to style this diagnostic row as a heading.</param>
    private TextBlock DebugText(string text, bool heading = false) => new()
    {
        Text = text,
        FontSize = heading ? 13 : 11,
        FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = Brush(IsLight ? "#242231" : "#F1F1FA"),
        Margin = new Thickness(0, 3, 0, 3)
    };

    /// <summary>Builds an aligned sensor row with value and extrema columns.</summary>
    /// <param name="name">Display name of the sensor or diagnostic heading.</param>
    /// <param name="value">Formatted current sensor reading, including its unavailable marker.</param>
    /// <param name="minimum">Formatted minimum reading for the diagnostic row.</param>
    /// <param name="maximum">Formatted maximum reading observed by the sensor provider.</param>
    /// <param name="heading">Whether to style this diagnostic row as a heading.</param>
    private Grid SensorDebugRow(string name, string value, string minimum, string maximum, bool heading = false)
    {
        var row = new Grid { Width = 760 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });
        for (var i = 0; i < 3; i++) { row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); }
        var values = new[] { name, value, minimum, maximum };
        for (var i = 0; i < values.Length; i++)
        {
            var text = DebugText(values[i], heading);
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(text, i);
            row.Children.Add(text);
        }
        return row;
    }

    /// <summary>Formats an available sensor reading with its unit.</summary>
    /// <param name="value">Optional measured sensor value; null is formatted as unavailable.</param>
    /// <param name="unit">Display unit appended only when a reading is available.</param>
    private static string DebugValue(double? value, string unit) => value is double number
        ? number.ToString("0.###", CultureInfo.InvariantCulture) + (unit.Length == 0 ? "" : " " + unit) : "Unavailable";

    /// <summary>Applies the current studio palette to sensor diagnostics.</summary>
    private void ApplySensorDebugAppearance()
    {
        var ink = Brush(IsLight ? "#242231" : "#F1F1FA");
        var muted = Brush(IsLight ? "#716B80" : "#9C9CAF");
        var transparent = new SolidColorBrush(Colors.Transparent);
        var debug = SensorDebugPanel.Visibility == Visibility.Visible;
        foreach (var button in new[] { ThemesTab, SensorsTab, SensorDebugRefresh, SensorDebugAdministrator })
        { button.Background = transparent; button.Foreground = ink; button.BorderThickness = new Thickness(0); }
        var selected = debug ? SensorsTab : ThemesTab;
        selected.BorderThickness = new Thickness(0, 0, 0, 2);
        selected.BorderBrush = Brush(IsLight ? "#7854B5" : "#C9AEF3");
        SensorDebugHeading.Foreground = ink;
        SensorDebugSummary.Foreground = SensorDebugNotice.Foreground = SensorDebugHelp.Foreground = muted;
        SensorDebugTree.Background = transparent;
        SensorDebugTree.Foreground = ink;
        SensorDebugAdministrator.Visibility = windowActions?.CanRestartAsAdministrator == true ? Visibility.Visible : Visibility.Collapsed;
        if (lastSensorDebug is not null) { RenderSensorDebug(); }
    }
}
