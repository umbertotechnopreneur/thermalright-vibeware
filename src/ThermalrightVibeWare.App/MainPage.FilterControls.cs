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

namespace ThermalrightVibeWare.App;

public sealed partial class MainPage
{
    /// <summary>Copies saved filter values to sliders and labels without triggering edits.</summary>
    private void UpdateColorControls()
    {
        updatingColorControls = true;
        SaturationSlider.Value = saturationPercent;
        HueSlider.Value = hueDegrees;
        BrightnessSlider.Value = brightnessPercent;
        ContrastSlider.Value = contrastPercent;
        SaturationText.Text = $"{saturationPercent:0}%";
        HueText.Text = $"{hueDegrees:0}°";
        BrightnessText.Text = $"{brightnessPercent:+0;-0;0}%";
        ContrastText.Text = $"{contrastPercent:0}%";
        updatingColorControls = false;
        UpdateColorRamps();
    }

    /// <summary>Saves current slider values and debounces persistence and preview wakeups.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Color_Changed(object sender, RoutedPropertyChangedEventArgs<double> args)
    {
        if (!ready || updatingColorControls) { return; }
        // Keep current choices in the model immediately, including during native
        // tray/setup shutdown. Debounce only disk persistence and preview wakeups.
        CommitColorControls();
        SaturationText.Text = $"{Math.Round(SaturationSlider.Value):0}%";
        HueText.Text = $"{Math.Round(HueSlider.Value):0}°";
        BrightnessText.Text = $"{Math.Round(BrightnessSlider.Value):+0;-0;0}%";
        ContrastText.Text = $"{Math.Round(ContrastSlider.Value):0}%";
        UpdateColorRamps();
        pendingColorSettings = true;
        colorChanges.Stop(); colorChanges.Start();
    }

    /// <summary>Copies current slider values into the model before persistence is debounced.</summary>
    private void CommitColorControls()
    {
        viewModel.Edit(p =>
        {
            p.SaturationPercent = Math.Round(SaturationSlider.Value);
            p.HueDegrees = Math.Round(HueSlider.Value);
            p.BrightnessPercent = Math.Round(BrightnessSlider.Value);
            p.ContrastPercent = Math.Round(ContrastSlider.Value);
        });
        pendingColorSettings = false;
    }

    /// <summary>Restores neutral runtime filter values.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void ResetColors_Click(object sender, RoutedEventArgs args)
    {
        colorChanges.Stop(); pendingColorSettings = false;
        saturationPercent = contrastPercent = 100;
        hueDegrees = brightnessPercent = 0;
        UpdateColorControls(); SaveChoices(); _ = RefreshAsync();
    }
}
