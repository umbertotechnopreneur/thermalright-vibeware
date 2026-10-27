/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Palette.cs
 * Purpose: Edits per-theme and per-appearance palette overrides.
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
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.App;

public sealed partial class MainPage
{
    private string? editingPaletteKey;

    /// <summary>Opens the current theme's palette editor.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Palette_Click(object sender, RoutedEventArgs args)
    {
        // Editing a palette must not follow automatic theme rotation mid-edit.
        editingPaletteKey = selectedTheme + (IsLight ? "@light" : "@dark");
        UpdatePaletteEditor();
        PaletteOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>Loads the current theme/appearance palette overrides into the editor.</summary>
    private void UpdatePaletteEditor()
    {
        if (editingPaletteKey is null) { return; }
        var palette = session.Model.Palettes.GetValueOrDefault(editingPaletteKey) ?? new ThemePalette();
        PaletteHeading.Text = "Theme colors · " + editingPaletteKey;
        PalettePrimary.Text = palette.TextPrimary ?? "";
        PaletteSecondary.Text = palette.TextSecondary ?? "";
        PaletteAccent.Text = palette.Accent ?? "";
        PaletteWarning.Text = palette.Warning ?? "";
        PaletteInfo.Text = palette.Info ?? "";
        PaletteError.Text = "";
    }

    /// <summary>Normalizes an optional palette entry before validation.</summary>
    /// <param name="text">Text to format, normalize or display in this operation.</param>
    private static string? PaletteColor(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToUpperInvariant();

    /// <summary>Validates and saves the entered theme palette overrides.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void ApplyPalette_Click(object sender, RoutedEventArgs args)
    {
        if (editingPaletteKey is null) { return; }
        try
        {
            var palette = new ThemePalette(PaletteColor(PalettePrimary.Text), PaletteColor(PaletteSecondary.Text),
                PaletteColor(PaletteAccent.Text), PaletteColor(PaletteWarning.Text), PaletteColor(PaletteInfo.Text));
            palette.Validate();
            viewModel.SetPalette(editingPaletteKey, palette);
            SaveChoices(); session.Wake();
            PaletteOverlay.Visibility = Visibility.Collapsed;
            editingPaletteKey = null;
        }
        catch (ArgumentException error) { PaletteError.Text = error.Message; }
    }

    /// <summary>Removes the current theme/appearance palette override.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void ResetPalette_Click(object sender, RoutedEventArgs args)
    {
        if (editingPaletteKey is null) { return; }
        viewModel.SetPalette(editingPaletteKey, null);
        SaveChoices(); session.Wake(); UpdatePaletteEditor();
    }

    /// <summary>Closes the palette editor without applying pending entries.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void ClosePalette_Click(object sender, RoutedEventArgs args)
    { PaletteOverlay.Visibility = Visibility.Collapsed; editingPaletteKey = null; }
}
