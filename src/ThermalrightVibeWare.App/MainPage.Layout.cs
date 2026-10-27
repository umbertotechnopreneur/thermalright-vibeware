/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Layout.cs
 * Purpose: Fits studio controls to the available monitor viewport.
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

namespace ThermalrightVibeWare.App;

/// <summary>Layout responsibilities of the studio page.</summary>
public sealed partial class MainPage
{

    /// <summary>Fits the studio controls to the available viewport.</summary>
    /// <param name="width">Available studio viewport width in layout units.</param>
    /// <param name="height">Available studio viewport height in layout units.</param>
    private void ResizeStudio(double width, double height)
    {
        if (width <= 0 || height <= 0) { return; }
        // Let the grid use the real viewport. Only the theme list scrolls;
        // the preview takes the remaining space and keeps its image aspect ratio.
        var compact = height < 740;
        var side = width < 1150 ? 20 : 28;
        HeaderArea.Margin = new Thickness(side, compact ? 10 : 16, side, compact ? 10 : 16);
        ContentArea.Margin = new Thickness(side, 0, side, 0);
        FooterArea.Margin = new Thickness(side, compact ? 8 : 12, side, compact ? 4 : 8);
        ThemeHeadingArea.Margin = new Thickness(2, 2, 2, compact ? 6 : 10);
        NavigationArea.Margin = new Thickness(0, compact ? 6 : 10, 0, compact ? 6 : 10);
        OptionsFrame.Margin = new Thickness(0, compact ? 2 : 6, 0, compact ? 10 : 16);
        FiltersPanel.Margin = new Thickness(0, compact ? 10 : 16, 0, 0);
        DisplayControls.Margin = new Thickness(0, compact ? 10 : 16, 0, 0);
        LibraryColumn.Width = new GridLength(width < 1150 ? 224 : 248);
        LibraryGap.Width = new GridLength(width < 1150 ? 20 : 24);

        // On high-DPI laptops the USB row needs two lines, not horizontal scrolling.
        var narrow = width < 1060;
        Grid.SetColumnSpan(DeviceField, narrow ? 5 : 1);
        Grid.SetColumn(RefreshDevicesButton, narrow ? 5 : 1);
        Grid.SetRow(StartDisplayButton, narrow ? 1 : 0);
        Grid.SetColumn(StartDisplayButton, narrow ? 3 : 4);
        Grid.SetColumnSpan(StartDisplayButton, narrow ? 2 : 1);
        Grid.SetRow(StopDisplayButton, narrow ? 1 : 0);
        Grid.SetRow(DisplayOptionsButton, narrow ? 2 : 1);
        Grid.SetRow(DisplayOptionsPanel, narrow ? 3 : 2);
        StartDisplayButton.Margin = new Thickness(0, narrow ? 8 : 0, 6, 0);
        StopDisplayButton.Margin = new Thickness(0, narrow ? 8 : 0, 0, 0);

        // Keep a useful preview even when the monitor cannot fit the preferred height.
        var shortWindow = height < 600;
        ProductIcon.Width = ProductIcon.Height = shortWindow ? 36 : 62;
        Heading.FontSize = shortWindow ? 22 : 28;
        SelectedThemeText.FontSize = shortWindow ? 22 : 26;
        Eyebrow.Visibility = CategoryText.Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        InitiativeImage.Width = InitiativeImage.Height = shortWindow ? 28 : 36;
        FooterLinks.Margin = new Thickness(0, shortWindow ? 2 : 10, 0, 0);
        foreach (var label in new[] { FontLabel, SizeLabel, GeometryLabel, DeviceLabel, RefreshLabel, QualityLabel })
        { label.Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible; }

        CodexSetupFrame.Margin = new Thickness(14, compact ? 10 : 28, 8, 0);
        ScreensFrame.Margin = new Thickness(14, compact ? 10 : 16, 8, 0);
        // Details are available on demand even on shorter monitors.
        CodexHelpText.Visibility = Visibility.Visible;
        CodexStatusText.TextWrapping = CodexEventText.TextWrapping = compact ? TextWrapping.NoWrap : TextWrapping.Wrap;
        CodexStatusText.TextTrimming = CodexEventText.TextTrimming = TextTrimming.CharacterEllipsis;
    }
}
