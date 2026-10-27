/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Typography.cs
 * Purpose: Manages installed/custom font selection and bounded text scaling.
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
using System.Windows.Input;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.App;

/// <summary>Typography responsibilities of the studio page.</summary>
public sealed partial class MainPage
{

    /// <summary>Applies the entered custom font name.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Font_Click(object sender, RoutedEventArgs args) => ApplyFont();

    /// <summary>Applies the custom font when Enter is pressed.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Font_KeyDown(object sender, KeyEventArgs args) { if (args.Key == Key.Enter) { ApplyFont(); args.Handled = true; } }

    /// <summary>Saves a valid custom font name and requests a new preview.</summary>
    private void ApplyFont()
    {
        var value = FontBox.Text.Trim();
        fontFamily = string.IsNullOrWhiteSpace(value) ? "Cascadia Mono" : value[..Math.Min(value.Length, 128)];
        FontBox.Text = fontFamily;
        SelectCurrentFont();
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Loads the renderer's font choices into the studio menu.</summary>
    private async Task LoadFontsAsync()
    {
        try
        {
            var fonts = await viewModel.ReadFontsAsync();
            if (unloaded) { return; }
            fontChoices = fonts.Families.Select(name => new PanelChoice(name, name)).Append(new PanelChoice("", "Custom font…")).ToArray();
            updatingFonts = true;
            FontsBox.ItemsSource = fontChoices;
            updatingFonts = false;
            FontListButton.IsEnabled = fonts.Families.Count > 0;
            ToolTipService.SetToolTip(FontsBox, fonts.Notice ?? "Installed monospace fonts. Choose Custom font to type a name.");
            SelectCurrentFont();
        }
        catch (Exception error)
        {
            ToolTipService.SetToolTip(FontListButton, "Font list unavailable: " + error.Message);
            ShowCustomFont();
        }
    }

    /// <summary>Matches the saved font against the available font menu.</summary>
    private void SelectCurrentFont()
    {
        var choice = fontChoices.FirstOrDefault(font => string.Equals(font.Id, fontFamily, StringComparison.OrdinalIgnoreCase));
        updatingFonts = true;
        FontsBox.SelectedItem = choice;
        updatingFonts = false;
        if (choice is null) { ShowCustomFont(); return; }
        FontsBox.Visibility = Visibility.Visible;
        FontBox.Visibility = ApplyFontButton.Visibility = FontListButton.Visibility = Visibility.Collapsed;
        FontLabel.Text = "FONT · MONOSPACE";
    }

    /// <summary>Reveals the custom font field and places keyboard focus in it.</summary>
    private void ShowCustomFont()
    {
        FontsBox.Visibility = Visibility.Collapsed;
        FontBox.Visibility = ApplyFontButton.Visibility = FontListButton.Visibility = Visibility.Visible;
        FontLabel.Text = "FONT FAMILY";
    }

    /// <summary>Opens the installed font menu or its custom-name fallback.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void FontList_Click(object sender, RoutedEventArgs args)
    {
        updatingFonts = true;
        FontsBox.SelectedItem = fontChoices.LastOrDefault();
        updatingFonts = false;
        FontsBox.Visibility = Visibility.Visible;
        FontBox.Visibility = ApplyFontButton.Visibility = FontListButton.Visibility = Visibility.Collapsed;
        FontLabel.Text = "FONT · MONOSPACE";
        FontsBox.IsDropDownOpen = true;
    }

    /// <summary>Clears the font-menu toggle after its popup closes.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void FontMenu_Closed(object? sender, EventArgs args)
    {
        if (!updatingFonts && FontsBox.SelectedItem is PanelChoice { Id.Length: 0 }) { ShowCustomFont(); }
    }

    /// <summary>Applies a selected installed font.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void FontSelection_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingFonts || FontsBox.SelectedItem is not PanelChoice choice) { return; }
        if (choice.Id.Length == 0) { ShowCustomFont(); FontBox.Focus(); return; }
        fontFamily = choice.Id;
        FontBox.Text = fontFamily;
        SaveChoices(); _ = RefreshAsync();
    }

    /// <summary>Decreases the theme text scale by one supported step.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Smaller_Click(object sender, RoutedEventArgs args) => ChangeFontScale(-0.1);

    /// <summary>Increases the theme text scale by one supported step.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Larger_Click(object sender, RoutedEventArgs args) => ChangeFontScale(0.1);

    /// <summary>Applies a bounded text-size step and refreshes the preview.</summary>
    /// <param name="change">Signed font-scale step, clamped to the supported range.</param>
    private void ChangeFontScale(double change) { fontScale = Math.Round(Math.Clamp(fontScale + change, 0.7, 1.5), 1); UpdateFontScale(); SaveChoices(); _ = RefreshAsync(); }

    /// <summary>Show the applied text scale and make its limits visible on the step buttons.</summary>
    private void UpdateFontScale()
    {
        FontScaleText.Text = $"{fontScale:P0}";
        SmallerButton.IsEnabled = fontScale > .7;
        LargerButton.IsEnabled = fontScale < 1.5;
    }
}
