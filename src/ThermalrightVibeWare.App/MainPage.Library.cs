/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Library.cs
 * Purpose: Manages theme/category choices, page navigation and attributed source links.
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
using System.Windows.Controls.Primitives;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Studio;

namespace ThermalrightVibeWare.App;

/// <summary>Library responsibilities of the studio page.</summary>
public sealed partial class MainPage
{

    /// <summary>Requests the catalog and reports errors independently of frame updates.</summary>
    /// <param name="reload">Whether to replace a catalog already loaded by the worker.</param>
    private async Task LoadCatalogAsync(bool reload = false)
    {
        if (session.Snapshot.CatalogRefreshPending) { return; }
        try
        {
            if (reload || session.Snapshot.Catalog is null) { await session.ReloadCatalogAsync(); }
            viewModel.SetOperationNotice(StudioNoticeSource.Catalog, null);
        }
        catch (Exception error) { viewModel.SetOperationNotice(StudioNoticeSource.Catalog, error.Message); }
        await RefreshAsync(queueIfBusy: false);
    }

    /// <summary>Projects catalog and selection changes into the library controls.</summary>
    /// <param name="current">Latest catalog, availability and capabilities.</param>
    /// <param name="preferences">Detached selection and appearance for this update.</param>
    private void ApplyLibrary(StudioSnapshot current, StudioPreferences preferences)
    {
        ReloadButton.IsEnabled = !current.CatalogRefreshPending;
        var catalog = current.Catalog;
        if (catalog is null)
        {
            if (!current.CatalogRefreshPending && viewModel.GetOperationNotice(StudioNoticeSource.Catalog) is { } notice)
            {
                LoadingTitle.Text = "Theme library unavailable";
                LoadingText.Text = notice;
            }
            return;
        }
        var catalogChanged = !ReferenceEquals(appliedCatalog, catalog);
        var categoryChanged = catalogChanged || appliedCategory != preferences.SelectedCategory;
        if (!categoryChanged && appliedTheme == preferences.SelectedTheme) { return; }

        updatingChoices = true;
        try
        {
            if (catalogChanged)
            {
                categoryRows.Clear();
                categoryRows.AddRange(catalog.Categories.Select(category => new CatalogRow(category.Id, category.Name)));
                CategoriesBox.ItemsSource = categoryRows.ToArray();
            }
            if (categoryChanged)
            {
                CategoriesBox.SelectedItem = categoryRows.FirstOrDefault(row => row.Id == preferences.SelectedCategory);
                themeRows.Clear();
                themeRows.AddRange(catalog.Themes.Where(theme => theme.Category == preferences.SelectedCategory)
                    .Select((theme, index) => new CatalogRow(theme.Id, theme.Name, (index + 1).ToString("00"))));
                ThemesBox.ItemsSource = themeRows.ToArray();
            }
            var selectedRow = themeRows.FirstOrDefault(row => row.Id == preferences.SelectedTheme);
            ThemesBox.SelectedItem = selectedRow;
            if (selectedRow is not null) { ThemesBox.ScrollIntoView(selectedRow); }
            appliedCatalog = catalog;
            appliedCategory = preferences.SelectedCategory;
            appliedTheme = preferences.SelectedTheme;
            UpdateSelection(current, catalog, preferences);
        }
        finally { updatingChoices = false; }
    }

    /// <summary>Applies the selected theme's heading, capabilities and library position.</summary>
    /// <param name="current">Negotiated capabilities for the current session.</param>
    /// <param name="catalog">Catalog supplying the selected theme's metadata.</param>
    /// <param name="preferences">Detached preferences for the current render.</param>
    private void UpdateSelection(StudioSnapshot current, ThemeCatalogDto catalog, StudioPreferences preferences)
    {
        var category = preferences.SelectedCategory;
        var selected = catalog.Themes.FirstOrDefault(theme => theme.Id == preferences.SelectedTheme);
        SpectrumBox.IsEnabled = current.Hello?.Supports("levels") == true && selected?.SupportsSpectrum == true;
        SpectrumBox.Visibility = SpectrumBox.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        CodexSetupFrame.Visibility = category == "codex" ? Visibility.Visible : Visibility.Collapsed;

        if (category == "system") { _ = RefreshSensorDriverStatusAsync(); }
        AdministratorPanel.Visibility = windowActions?.CanRestartAsAdministrator == true
            ? Visibility.Visible : Visibility.Collapsed;
        CategoryText.Text = (categoryRows.FirstOrDefault(row => row.Id == category)?.Name ?? category).ToUpperInvariant();
        SelectedThemeText.Text = selected?.Name ?? "No themes installed";
        UpdateThemeDescription(selected);
        ToolTipService.SetToolTip(SelectedThemeText, null);
        var index = themeRows.FindIndex(row => row.Id == preferences.SelectedTheme);
        PositionText.Text = $"{index + 1:00} / {themeRows.Count:00}";
        PreviousButton.IsEnabled = NextButton.IsEnabled = themeRows.Count > 1;
        CycleBox.IsEnabled = catalog.Themes.Count > 1;
        UpdateRows(preferences);
        if (themeRows.Count == 0)
        {
            ClearPreview();
            LoadingTitle.Text = "This category is empty.";
            LoadingText.Text = "Add a theme pack and reload the library.";
        }
    }

    /// <summary>Shows the selected theme description once in the main workspace.</summary>
    /// <param name="theme">Selected catalog theme, or null when no theme is available.</param>
    private void UpdateThemeDescription(ThemeChoiceDto? theme)
    {
        ThemeDescriptionText.Text = theme?.Description ?? "";
    }

    /// <summary>Shows only the attributed source links available for this frame.</summary>
    /// <param name="sources">Available attributed artist links; null hides the source links.</param>
    private void UpdateArtistSources(ArtistSourcesDto? sources)
    {
        // link is an existing source button; a missing value hides it instead of inventing a URL.
        static void Set(HyperlinkButton link, string? value)
        {
            var valid = Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https";
            link.NavigateUri = valid ? uri : null;
            link.Visibility = valid ? Visibility.Visible : Visibility.Collapsed;
        }
        Set(ArtistBiographyLink, sources?.BiographyUrl);
        Set(ArtistPhotoLink, sources?.PhotoUrl);
        Set(ArtistSalesLink, sources?.SalesUrl);
        ArtistSourcesPanel.Visibility = sources is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Moves the theme selection in the requested direction.</summary>
    /// <param name="direction">Navigation step; negative moves back and positive moves forward.</param>
    private void Navigate(int direction)
    {
        if (session.Snapshot.Catalog is not { } catalog) { return; }
        viewModel.NavigateTheme(catalog, direction);
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Moves the selected theme's page and resets its page clock.</summary>
    /// <param name="direction">Navigation step; negative moves back and positive moves forward.</param>
    private void NavigatePage(int direction)
    {
        viewModel.NavigatePage(session.Snapshot, direction);
        _ = RefreshAsync();
    }

    /// <summary>Projects the page counter, title and availability.</summary>
    /// <param name="page">Current navigation data resolved by the model.</param>
    private void UpdateNavigationState(StudioPage page)
    {
        PagePositionText.Text = page.Count == 0 ? "00 / 00" : $"{page.Index + 1:00} / {page.Count:00}";
        ToolTipService.SetToolTip(PagePositionText, page.Title);
        PagePreviousButton.IsEnabled = PageNextButton.IsEnabled = page.CanNavigate;
    }

    /// <summary>Saves the enabled screen categories and refreshes the preview.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Screen_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready || updatingChoices || sender is not ToggleButton screen) { return; }
        var category = screen == MusicScreenBox ? "multimedia" : screen == CodexScreenBox ? "codex" : "system";
        viewModel.SetScreen(category, screen.IsChecked == true);
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Label the JPEG actually returned without changing the user's chosen theme.</summary>
    /// <param name="themeId">Stable theme identifier from the current catalog.</param>
    /// <param name="suppressed">Whether the current frame is hidden because no eligible screen is enabled.</param>
    /// <param name="catalog">Latest theme metadata for the displayed title.</param>
    /// <param name="selectedTheme">Theme selected in the studio model.</param>
    private void UpdateFrameHeading(string themeId, bool suppressed, ThemeCatalogDto? catalog, string selectedTheme)
    {
        if (suppressed)
        {
            CategoryText.Text = "DISPLAY PAUSED";
            SelectedThemeText.Text = "No active screens";
            ThemeDescriptionText.Text = "";
            ToolTipService.SetToolTip(SelectedThemeText, null);
            return;
        }
        var theme = catalog?.Themes.FirstOrDefault(item => item.Id == themeId);
        if (theme is null) { return; }
        var category = categoryRows.FirstOrDefault(item => item.Id == theme.Category)?.Name ?? theme.Category;
        CategoryText.Text = category.ToUpperInvariant();
        SelectedThemeText.Text = theme.Name;
        UpdateThemeDescription(theme);
        ToolTipService.SetToolTip(SelectedThemeText, themeId != selectedTheme ? "The selected screen is unavailable. Showing this enabled screen on the display." : null);
    }

    /// <summary>Saves a category selection and rebuilds its theme choices.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Category_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        if (session.Snapshot.Catalog is not { } catalog || CategoriesBox.SelectedItem is not CatalogRow category) { return; }
        viewModel.SelectCategory(category.Id, catalog);
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Saves the selected theme and resets the page clock.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Theme_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        if (session.Snapshot.Catalog is not { } catalog || ThemesBox.SelectedItem is not CatalogRow theme) { return; }
        viewModel.SelectTheme(theme.Id, catalog);
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Saves the explicit or automatic display profile selection.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Profile_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices || !session.Snapshot.CanSelectProfile || ProfilesBox.SelectedItem is not PanelChoice profile) { return; }
        viewModel.SelectProfile(profile.Id);
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Reloads the local theme catalog on request.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void Reload_Click(object sender, RoutedEventArgs args) => await LoadCatalogAsync(reload: true);

    /// <summary>Selects the previous theme.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Previous_Click(object sender, RoutedEventArgs args) => Navigate(-1);

    /// <summary>Selects the next theme.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Next_Click(object sender, RoutedEventArgs args) => Navigate(1);

    /// <summary>Selects the previous page in the current theme.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void PagePrevious_Click(object sender, RoutedEventArgs args) => NavigatePage(-1);

    /// <summary>Selects the next page in the current theme.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void PageNext_Click(object sender, RoutedEventArgs args) => NavigatePage(1);
}
