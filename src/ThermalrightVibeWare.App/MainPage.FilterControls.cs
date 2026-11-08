/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.FilterControls.cs
 * Purpose: Commits runtime filter sliders and debounces settings persistence and preview wakeups.
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
using ThermalrightVibeWare.Studio;

namespace ThermalrightVibeWare.App;

public sealed partial class MainPage
{
    /// <summary>Copies saved filter values to sliders and labels without triggering edits.</summary>
    /// <param name="preferences">Current filter settings from a detached model read.</param>
    private void UpdateColorControls(StudioPreferences preferences)
    {
        updatingColorControls = true;
        try
        {
            SaturationSlider.Value = preferences.SaturationPercent;
            HueSlider.Value = preferences.HueDegrees;
            BrightnessSlider.Value = preferences.BrightnessPercent;
            ContrastSlider.Value = preferences.ContrastPercent;
        }
        finally { updatingColorControls = false; }
        UpdateColorLabels(preferences);
    }

    /// <summary>Saves current slider values and debounces persistence and preview wakeups.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Color_Changed(object sender, RoutedPropertyChangedEventArgs<double> args)
    {
        if (!ready || updatingColorControls) { return; }
        var control = sender == SaturationSlider ? StudioColorControl.Saturation
            : sender == HueSlider ? StudioColorControl.Hue
            : sender == BrightnessSlider ? StudioColorControl.Brightness : StudioColorControl.Contrast;
        viewModel.SetColor(control, args.NewValue);
        UpdateColorLabels(viewModel.Preferences);
        session.ScheduleSave();
    }

    /// <summary>Projects the model's filter values into the labels and color guides.</summary>
    /// <param name="preferences">Current filter values including any edit just applied.</param>
    private void UpdateColorLabels(StudioPreferences preferences)
    {
        SaturationText.Text = $"{preferences.SaturationPercent:0}%";
        HueText.Text = $"{preferences.HueDegrees:0}°";
        BrightnessText.Text = $"{preferences.BrightnessPercent:+0;-0;0}%";
        ContrastText.Text = $"{preferences.ContrastPercent:0}%";
        UpdateColorRamps(preferences);
    }

    /// <summary>Restores neutral runtime filter values.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void ResetColors_Click(object sender, RoutedEventArgs args)
    {
        viewModel.ResetColors();
        UpdateColorControls(viewModel.Preferences);
        SaveChoices();
        _ = RefreshAsync();
    }
}
