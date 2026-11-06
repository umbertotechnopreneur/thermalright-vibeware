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
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.App;

/// <summary>Library responsibilities of the studio page.</summary>
public sealed partial class MainPage
{

    /// <summary>Loads available themes and reconciles the current selection.</summary>
    private async Task LoadCatalogAsync()
    {
        if (catalogLoading) { return; }
        catalogLoading = true;
        ReloadButton.IsEnabled = false;
        try
        {
            if (session.Snapshot.Catalog is null) { await session.ReloadCatalogAsync(); }
            catalog = session.Snapshot.Catalog ?? throw new IOException("Theme catalog unavailable.");
            if (unloaded) { return; }
            updatingChoices = true;
            categoryRows.Clear();
            foreach (var category in catalog.Categories) { categoryRows.Add(new(category.Id, category.Name)); }
            CategoriesBox.ItemsSource = categoryRows.ToArray();
            CategoriesBox.SelectedItem = categoryRows.FirstOrDefault(row => row.Id == selectedCategory) ?? categoryRows.FirstOrDefault();
            PopulateThemes();
            if (catalog.Notices.Count > 0) { StatusText.Text = string.Join(" · ", catalog.Notices); }
        }
        catch (Exception error)
        {
            StatusText.Text = error.Message;
            LoadingTitle.Text = "Theme library unavailable";
            LoadingText.Text = error.Message;
        }
        finally { updatingChoices = false; catalogLoading = false; ReloadButton.IsEnabled = true; }
    }

    /// <summary>Rebuilds the theme rows for the selected category without reentrant selection events.</summary>
    private void PopulateThemes()
    {
        var wasUpdating = updatingChoices;
        updatingChoices = true;
        themeRows.Clear();
        foreach (var theme in catalog?.Themes.Where(theme => theme.Category == selectedCategory) ?? [])
        { themeRows.Add(new(theme.Id, theme.Name, (themeRows.Count + 1).ToString("00"))); }
        ThemesBox.ItemsSource = themeRows.ToArray();
        ThemesBox.SelectedItem = themeRows.FirstOrDefault(row => row.Id == selectedTheme) ?? themeRows.FirstOrDefault();
        UpdateSelection();
        updatingChoices = wasUpdating;
    }

    /// <summary>Applies the selected theme's heading, capabilities and library position.</summary>
    private void UpdateSelection()
    {
        SpectrumBox.IsEnabled = session.Supports("levels") && catalog?.Themes.FirstOrDefault(theme => theme.Id == selectedTheme)?.SupportsSpectrum == true;
        SpectrumBox.Visibility = SpectrumBox.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        CodexSetupFrame.Visibility = selectedCategory == "codex" ? Visibility.Visible : Visibility.Collapsed;

        if (selectedCategory == "system") { _ = RefreshSensorDriverStatusAsync(); }
        AdministratorPanel.Visibility = windowActions?.CanRestartAsAdministrator == true
            ? Visibility.Visible : Visibility.Collapsed;
        CategoryText.Text = (categoryRows.FirstOrDefault(row => row.Id == selectedCategory)?.Name ?? selectedCategory).ToUpperInvariant();
        var selected = catalog?.Themes.FirstOrDefault(theme => theme.Id == selectedTheme);
        SelectedThemeText.Text = themeRows.FirstOrDefault(row => row.Id == selectedTheme)?.Name ?? "No themes installed";
        PagePositionText.Text = selected is null ? "00 / 00" : $"01 / {selected.PageCount:00}";
        PagePreviousButton.IsEnabled = PageNextButton.IsEnabled = selected?.PageCount > 1;
        UpdateThemeDescription(selected);
        ToolTipService.SetToolTip(SelectedThemeText, null);
        PositionText.Text = $"{ThemesBox.SelectedIndex + 1:00} / {themeRows.Count:00}";
        PreviousButton.IsEnabled = NextButton.IsEnabled = themeRows.Count > 1;
        CycleBox.IsEnabled = (catalog?.Themes.Count ?? 0) > 1;
        UpdateDisplayControls();
        UpdateRows();
        UpdateStudioTabs();
        if (themeRows.Count == 0)
        {
            ClearPreview();
            LoadingTitle.Text = catalog is null ? "Theme library unavailable" : "This category is empty.";
            LoadingText.Text = catalog is null ? session.Snapshot.Status : "Add a theme pack and reload the library.";
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
        if (ThemesBox.Items.Count > 0) { ThemesBox.SelectedIndex = (ThemesBox.SelectedIndex + direction + ThemesBox.Items.Count) % ThemesBox.Items.Count; ThemesBox.ScrollIntoView(ThemesBox.SelectedItem); }
    }

    /// <summary>Moves the selected theme's page and resets its page clock.</summary>
    /// <param name="direction">Navigation step; negative moves back and positive moves forward.</param>
    private void NavigatePage(int direction)
    {
        var state = session.Snapshot.Frame;
        var count = Math.Max(1, state?.ActivePageCount ?? catalog?.Themes.FirstOrDefault(theme => theme.Id == selectedTheme)?.PageCount ?? 1);
        var current = Math.Clamp(state?.ActivePageIndex ?? viewModel.Preferences.SelectedPageIndex, 0, count - 1);
        var target = (current + direction + count) % count;
        viewModel.Edit(preferences =>
        {
            preferences.SelectedPageIndex = target;
            preferences.PageRevision++;
            preferences.LastChange = Environment.TickCount64;
        });
        session.Wake();
        _ = RefreshAsync();
    }

    /// <summary>Applies page counters from the rendered frame.</summary>
    /// <param name="state">Latest immutable engine frame/status observation used by the studio.</param>
    private void UpdateNavigationState(PanelState state)
    {
        var count = Math.Max(1, state.ActivePageCount);
        var index = Math.Clamp(state.ActivePageIndex, 0, count - 1);
        PagePositionText.Text = $"{index + 1:00} / {count:00}";
        ToolTipService.SetToolTip(PagePositionText, state.ActivePageTitle);
        PagePreviousButton.IsEnabled = PageNextButton.IsEnabled = !state.FrameSuppressed && count > 1;
    }

    /// <summary>Saves the enabled screen categories and refreshes the preview.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Screen_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        viewModel.SetScreens(MusicScreenBox.IsChecked == true, CodexScreenBox.IsChecked == true, SystemScreenBox.IsChecked == true);
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Label the JPEG actually returned without changing the user's chosen theme.</summary>
    /// <param name="themeId">Stable theme identifier from the current catalog.</param>
    /// <param name="suppressed">Whether the current frame is hidden because no eligible screen is enabled.</param>
    private void UpdateFrameHeading(string themeId, bool suppressed)
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
        if (catalog is null) { return; }
        viewModel.SelectCategory((CategoriesBox.SelectedItem as CatalogRow)?.Id ?? "multimedia", catalog);
        PopulateThemes();
        lastChange = Environment.TickCount64;
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Saves the selected theme and resets the page clock.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Theme_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        if (catalog is null) { return; }
        viewModel.SelectTheme((ThemesBox.SelectedItem as CatalogRow)?.Id ?? "", catalog);
        lastChange = Environment.TickCount64;
        UpdateSelection();
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Saves the explicit or automatic display profile selection.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Profile_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || updatingChoices) { return; }
        var profile = (ProfilesBox.SelectedItem as PanelChoice)?.Id ?? "auto";
        viewModel.Edit(p => { p.ProfileSelection = profile; if (profile != "auto") { p.SelectedProfile = profile; } });
        SaveChoices();
        _ = RefreshAsync();
    }

    /// <summary>Reloads the local theme catalog on request.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void Reload_Click(object sender, RoutedEventArgs args)
    {
        try { await session.ReloadCatalogAsync(); await LoadCatalogAsync(); await RefreshAsync(); }
        catch (Exception error) { StatusText.Text = error.Message; }
    }

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
