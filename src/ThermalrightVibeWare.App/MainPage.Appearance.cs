/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Appearance.cs
 * Purpose: Applies light/dark studio colors and appearance selections.
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
using System.Windows.Media;
using OpenSilver.Themes.Modern;

namespace ThermalrightVibeWare.App;

/// <summary>Appearance responsibilities of the studio page.</summary>
public sealed partial class MainPage
{
    private bool IsLight => viewModel.Preferences.Light;

    /// <summary>Applies the selected light/dark palette to studio controls.</summary>
    private void ApplyAppearance()
    {
        var light = IsLight;
        if (appliedLight == light) { return; }
        appliedLight = light;
        ((ModernTheme)Application.Current.Theme).CurrentPalette = light ? ModernTheme.Palettes.Light : ModernTheme.Palettes.Dark;
        OpenSilver.Interop.ExecuteJavaScriptVoid("document.documentElement.style.colorScheme = $0", light ? "light" : "dark");
        Root.Background = Brush(light ? "#FAF9FC" : "#101218");
        var transparent = new SolidColorBrush(Colors.Transparent);
        var surface = Brush(light ? "#FFFFFF" : "#191C25");
        var line = Brush(light ? "#DDD9E7" : "#303441");
        var ink = Brush(light ? "#242231" : "#F1F1FA");
        var muted = Brush(light ? "#716B80" : "#9C9CAF");
        var accent = Brush(light ? "#7854B5" : "#C9AEF3");
        // Sections share the page, without cards. Only the modal needs its own surface.
        foreach (var section in new[] { LibraryFrame, AppearanceFrame, OptionsFrame, DisplayFrame, ScreensFrame, CodexSetupFrame, PreviewBadge, PreviewFrame }) { section.Background = transparent; }
        SensorSetupFrame.Background = transparent;
        SensorSetupHeading.Foreground = accent;
        SensorDriverText.Foreground = ink;
        SensorDriverPath.Foreground = muted;
        SensorDriverHelp.Foreground = muted;
        SensorSummaryText.Foreground = CodexSummaryText.Foreground = muted;
        TimingDetailsText.Foreground = muted;
        SensorDriverFolderButton.Background = surface;
        SensorDriverFolderButton.Foreground = ink;
        SensorDriverFolderButton.BorderBrush = line;
        PaletteFrame.Background = surface; PaletteFrame.BorderBrush = line;
        foreach (var text in new[] { PaletteHeading, PalettePrimaryLabel, PaletteSecondaryLabel, PaletteAccentLabel, PaletteWarningLabel, PaletteInfoLabel }) { text.Foreground = ink; }
        PaletteHelp.Foreground = muted; PaletteError.Foreground = Brush(light ? "#A12D35" : "#FFADB3");
        foreach (var box in new[] { PalettePrimary, PaletteSecondary, PaletteAccent, PaletteWarning, PaletteInfo }) { box.Background = surface; box.Foreground = ink; box.BorderBrush = line; }
        foreach (var button in new[] { PaletteButton, PaletteReset, PaletteCancel, PaletteApply }) { button.Background = surface; button.Foreground = ink; button.BorderBrush = line; }
        AboutFrame.Background = surface;
        AboutFrame.BorderBrush = line;
        CloseFrame.Background = surface;
        CloseFrame.BorderBrush = line;
        CloseHeading.Foreground = ink;
        CloseDescription.Foreground = muted;
        foreach (var button in new[] { CancelCloseButton, ExitButton, MinimizeTrayButton })
        { button.Background = surface; button.Foreground = ink; button.BorderBrush = line; button.BorderThickness = new Thickness(1); }
        FiltersPanel.Background = Brush(light ? "#F0EDF5" : "#171A23");
        DisplayDivider.BorderBrush = line;
        foreach (var text in new[] { Heading, SelectedThemeText, LoadingTitle, PositionText, PagePositionText, FontScaleText, SaturationText, HueText, BrightnessText, ContrastText, AboutHeading, AboutAuthor, AboutDisclosureHeading, CodexStatusText }) { text.Foreground = ink; }
        foreach (var text in new[] { Subtitle, Author, HeaderDot, CatalogText, ThemeNavigationText, PageNavigationText, SourceText, StatusText, LoadingText, FontLabel, SizeLabel, GeometryLabel, DeviceLabel, RefreshLabel, QualityLabel, SaturationLabel, HueLabel, BrightnessLabel, ContrastLabel, FiltersStatus, ScreensHelpText, CodexEventText, CodexHelpText, FooterDot2, AboutVersion, AboutDisclosure, AboutInitiative, AdministratorHelp }) { text.Foreground = muted; }
        foreach (var text in new[] { Eyebrow, LibraryHeading, CategoryText, PreviewBadgeText, ScreensHeading, CodexSetupHeading }) { text.Foreground = accent; }
        foreach (var link in new[] { Manifesto, AboutLink, TermsLink, PrivacyLink, AboutManifesto, ProjectLink, InitiativeLink, WebsiteLink, AboutTerms, AboutPrivacy }) { link.Foreground = accent; }
        ThemePagesText.Foreground = muted;
        foreach (var link in new[] { ArtistBiographyLink, ArtistPhotoLink, ArtistSalesLink }) { link.Foreground = accent; }
        CategoriesBox.Background = ThemesBox.Background = transparent;
        FontBox.Background = surface;
        FontBox.Foreground = ink;
        FontBox.BorderBrush = line;
        FontBox.BorderThickness = new Thickness(1);
        // Modern's PopupBorder binds to this same background. Keep it opaque in both palettes.
        foreach (var combo in new[] { ProfilesBox, DevicesBox, RefreshBox, QualityBox, FontsBox })
        {
            combo.Background = surface;
            combo.Foreground = ink;
            combo.BorderBrush = line;
            combo.BorderThickness = new Thickness(1);
        }
        CycleBox.Foreground = muted;
        LogErrorsBox.Foreground = muted;
        SpectrumBox.Foreground = muted;
        FiltersBox.Foreground = ink;
        foreach (var box in new[] { MusicScreenBox, CodexScreenBox, SystemScreenBox }) { box.Foreground = ink; }
        foreach (var button in new[] { ReloadButton, PreviousButton, NextButton, PagePreviousButton, PageNextButton, ApplyFontButton, FontListButton, ResetColorsButton, SmallerButton, LargerButton, CloseAboutButton, RefreshDevicesButton, StartDisplayButton, StopDisplayButton, CodexRefreshButton, CodexPrepareButton, OpenLogButton, AdministratorButton })
        { button.Background = transparent; button.Foreground = ink; button.BorderThickness = new Thickness(0); }
        foreach (var button in new[] { ScreensDetailsButton, CodexDetailsButton, SensorDetailsButton, AppearanceDetailsButton, DisplayOptionsButton, DiagnosticsDetailsButton })
        { button.Background = transparent; button.Foreground = accent; button.BorderThickness = new Thickness(0); }
        StartDisplayButton.Background = Brush(light ? "#68468F" : "#C5AAE8");
        StartDisplayButton.Foreground = Brush(light ? "#FFFFFF" : "#1C1428");
        foreach (var button in new[] { SmallerButton, LargerButton, ResetColorsButton, StopDisplayButton, ApplyFontButton })
        {
            button.Background = surface;
            button.BorderBrush = line;
            button.BorderThickness = new Thickness(1);
        }
        UpdateRows();
        UpdateAppearanceButtons();
        UpdateDisplayControls();
        ApplySensorDebugAppearance();
    }

    /// <summary>Marks the selected appearance mode and clears other button accents.</summary>
    private void UpdateAppearanceButtons()
    {
        var buttons = new[] { AutoButton, DarkButton, LightButton };
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].Background = new SolidColorBrush(Colors.Transparent);
            buttons[i].Foreground = Brush(i == appearance ? IsLight ? "#623A8D" : "#EBD6FF" : IsLight ? "#716B80" : "#9C9CAF");
            buttons[i].BorderBrush = i == appearance ? Brush(IsLight ? "#7854B5" : "#C9AEF3") : new SolidColorBrush(Colors.Transparent);
            buttons[i].BorderThickness = new Thickness(0, 0, 0, 2);
            buttons[i].FontWeight = i == appearance ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    /// <summary>Refreshes category and theme row selection colors.</summary>
    private void UpdateRows()
    {
        foreach (var row in categoryRows) { row.Update(IsLight, row.Id == selectedCategory); }
        foreach (var row in themeRows) { row.Update(IsLight, row.Id == selectedTheme); }
    }

    /// <summary>Selects the operating system's appearance mode.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Auto_Click(object sender, RoutedEventArgs args) => SetAppearance(0);

    /// <summary>Selects the dark studio appearance.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Dark_Click(object sender, RoutedEventArgs args) => SetAppearance(1);

    /// <summary>Selects the light studio appearance.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Light_Click(object sender, RoutedEventArgs args) => SetAppearance(2);

    /// <summary>Stores an appearance mode and refreshes its visible controls.</summary>
    /// <param name="value">Appearance mode: 0 follows Windows, 1 selects dark, 2 selects light.</param>
    private void SetAppearance(int value) { appearance = value; ApplyAppearance(); UpdateAppearanceButtons(); SaveChoices(); _ = RefreshAsync(); }

    /// <summary>Converts a hexadecimal color to a solid studio brush.</summary>
    /// <param name="hex">Hexadecimal theme color; null is allowed where the signature permits it.</param>
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
}
