/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.xaml.cs
 * Purpose: Owns studio control state and connects the page lifetime to its shared session.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Modified with AI: OpenAI Codex; documentation, refactoring and first-launch policy; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard file headers.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */

using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OpenSilver.Themes.Modern;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Studio;

namespace ThermalrightVibeWare.App;

/// <summary>A manifest-driven theme library and preview. The engine owns data and rendering.</summary>
public sealed partial class MainPage : Page
{
    private readonly IStudioWindowActions? windowActions;
    private readonly IStudioPreview? preview;
    private readonly StudioSession session;
    public StudioSession Session => session;
    public event Action? Ready;
    private readonly bool ownsSession;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly List<CatalogRow> categoryRows = [];
    private readonly List<CatalogRow> themeRows = [];
    private StudioViewModel viewModel => session.ViewModel;
    private static readonly int[] RefreshIntervals = [1000, 500, 250, 100, 50, 25];
    private ThemeCatalogDto? appliedCatalog;
    private string? appliedCategory, appliedTheme, appliedDeviceId;
    private bool unloaded, ready, updatingChoices, restartingAsAdministrator;
    private long appliedFrameRevision;
    private bool updatingColorControls;
    private string? lastRenderedTheme;
    private byte[]? lastFrame;
    private bool previewUpdating;
    private int previewGeneration;
    private bool? appliedLight;
    private PanelChoice[]? appliedDevices;

    /// <summary>Connects studio controls, saved choices and UI events to the shared session.</summary>
    /// <param name="client">Engine request client owned or shared according to the containing session/host.</param>
    /// <param name="windowActions">Optional desktop actions supplied by the host.</param>
    /// <param name="presentationSettingsPath">Optional settings file path; null selects the per-user default.</param>
    /// <param name="preview">Optional host bridge that displays the engine's existing JPEG.</param>
    /// <param name="restoreLastSession">Whether to restore the saved device/theme and wait for that device at startup.</param>
    /// <param name="session">Optional existing studio session; the page owns a newly created session only.</param>
    public MainPage(IPanelClient client, IStudioWindowActions? windowActions = null, string? presentationSettingsPath = null, IStudioPreview? preview = null, bool restoreLastSession = false, StudioSession? session = null)
    {
        this.windowActions = windowActions;
        this.preview = preview;
        // The host may share its worker; only a page-created session belongs to this page.
        this.session = session ?? new StudioSession(client,
            presentationSettingsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThermalrightVibeWare", "presentation.json"),
            restoreLastSession);
        ownsSession = session is null;

        InitializeControls();
        AttachPageEvents();
    }

    /// <summary>Restores saved controls before enabling change handlers and applying the studio palette.</summary>
    private void InitializeControls()
    {
        InitializeComponent();
        InitializePreviewAppearance();
        var studio = viewModel.Read();
        var preferences = studio.Preferences;

        // Populate selectors before ready enables their change handlers.
        RefreshBox.ItemsSource = RefreshIntervals.Select(value => new PanelChoice(value.ToString(), value == 1000 ? "1 s" : $"{value} ms")).ToArray();
        RefreshBox.SelectedItem = ((PanelChoice[])RefreshBox.ItemsSource).First(item => item.Id == preferences.RefreshMilliseconds.ToString());
        QualityBox.ItemsSource = new[] { new PanelChoice("poor", "Poor"), new PanelChoice("low", "Low"), new PanelChoice("medium", "Medium"), new PanelChoice("high", "High") };
        QualityBox.SelectedItem = ((PanelChoice[])QualityBox.ItemsSource).First(item => item.Id == preferences.JpegQuality);
        refresh.Interval = TimeSpan.FromMilliseconds(preferences.RefreshMilliseconds);
        FontBox.Text = preferences.FontFamily;
        UpdateFontScale(preferences.FontScale);
        UpdateColorControls(preferences);
        FiltersBox.IsChecked = preferences.ColorFiltersEnabled;
        UpdateFilterVisibility(preferences);
        SpectrumBox.IsChecked = preferences.ShowSpectrum;
        CycleBox.IsChecked = preferences.CycleThemes;
        MusicScreenBox.IsChecked = studio.ScreenEnabled["multimedia"];
        CodexScreenBox.IsChecked = studio.ScreenEnabled["codex"];
        SystemScreenBox.IsChecked = studio.ScreenEnabled["system"];

        ready = true;
        ApplyAppearance(preferences);
        ApplyWorkspace(viewModel.Workspace);
        UpdateDisplayControls(session.Snapshot, preferences);
        AboutVersion.Text = $"Display studio · {typeof(App).Assembly.GetName().Version?.ToString(3)}";
    }

    /// <summary>Connects page lifetime, refresh timers and keyboard navigation after controls are ready.</summary>
    private void AttachPageEvents()
    {
        // Initial data loading runs once the visual tree is ready.
        Loaded += async (_, _) =>
        {
            unloaded = false;
            ApplyWorkspace(viewModel.Workspace);
            await LoadCatalogAsync();
            await RefreshDevicesAsync();
            await LoadDiagnosticsAsync();
            await RefreshAsync();
            await LoadFontsAsync();
            if (!unloaded)
            {
                // Only a loaded main page consumes first-run visibility; failed/aborted startup does not.
                this.session.CompleteFirstRun();
                refresh.Start();
                Ready?.Invoke();
            }
        };
        Unloaded += (_, _) =>
        {
            unloaded = true;
            session.PreviewRequested = false;
            if (ownsSession) { _ = this.session.ShutdownAsync(); }
            refresh.Stop();
            ClearPreview();
        };

        // UI timers observe the session; the session owns actual engine scheduling.
        refresh.Tick += async (_, _) => await RefreshAsync(queueIfBusy: false);

        // Overlays and text/dropdown controls consume navigation before theme shortcuts.
        KeyDown += (_, args) =>
        {
            if (PaletteOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { ClosePalette_Click(this, args); args.Handled = true; } return; }
            if (CloseOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { CloseOverlay.Visibility = Visibility.Collapsed; args.Handled = true; } return; }
            if (AboutOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { CloseAbout(); args.Handled = true; } return; }
            if (viewModel.Workspace.SensorDebugOpen) { return; }
            if (ProfilesBox.IsDropDownOpen || DevicesBox.IsDropDownOpen || RefreshBox.IsDropDownOpen || QualityBox.IsDropDownOpen || FontsBox.IsDropDownOpen || args.OriginalSource is TextBox) { return; }
            if (args.Key == Key.PageUp) { Navigate(-1); args.Handled = true; }
            if (args.Key == Key.PageDown) { Navigate(1); args.Handled = true; }
        };
        SizeChanged += (_, args) => ResizeStudio(args.NewSize.Width, args.NewSize.Height);
    }

    /// <summary>Saves whether Multimedia themes show output spectrum analysis.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Spectrum_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready) { return; }
        var enabled = SpectrumBox.IsChecked == true;
        viewModel.Edit(p => p.ShowSpectrum = enabled);
        SaveChoices(); _ = RefreshAsync();
    }

    /// <summary>Start a fresh twenty-second interval when automatic theme cycling is enabled.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Cycle_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready) { return; }
        viewModel.SetRotation(CycleBox.IsChecked == true);
        SaveChoices();
    }

    /// <summary>Saves the studio model through its session owner.</summary>
    private void SaveChoices() => session.Save();

}
