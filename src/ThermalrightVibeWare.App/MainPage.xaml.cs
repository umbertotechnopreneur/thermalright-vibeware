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
    private readonly bool ownsSession;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer colorChanges = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly List<CatalogRow> categoryRows = [];
    private readonly List<CatalogRow> themeRows = [];
    private StudioViewModel viewModel => session.ViewModel;
    private IReadOnlyDictionary<string, bool> screenEnabled => viewModel.Read().ScreenEnabled;
    private static readonly int[] RefreshIntervals = [1000, 500, 250, 100, 50, 25];
    private ThemeCatalogDto? catalog;
    private bool unloaded, ready, updatingChoices, catalogLoading;
    private bool displayActive => session.Snapshot.DisplayActive;
    private bool displayBusy => session.Snapshot.Busy;
    private bool startupRestorePending => session.Snapshot.StartupPending;
    private bool devicesLoading;
    private bool codexLoading, codexPreparing, diagnosticsChanging, diagnosticsEnabled, restartingAsAdministrator;
    private long codexStatusAt, appliedFrameRevision;
    private bool codexProcessRunning => session.Snapshot.Frame?.CodexProcessRunning == true;
    private bool frameSuppressed => session.Snapshot.Frame?.FrameSuppressed == true;
    private bool updatingColorControls, pendingColorSettings, updatingFonts;
    private PanelChoice[] fontChoices = [];
    private string? lastRenderedTheme;
    private byte[]? lastFrame;
    private bool previewUpdating;
    private int previewGeneration;
    private bool? appliedLight;
    private PanelChoice[]? appliedDevices;
    private int refreshMilliseconds { get => viewModel.Preferences.RefreshMilliseconds; set => viewModel.Edit(p => p.RefreshMilliseconds = value); }
    private string jpegQuality { get => viewModel.Preferences.JpegQuality; set => viewModel.Edit(p => p.JpegQuality = value); }
    private string profileSelection { get => viewModel.Preferences.ProfileSelection; set => viewModel.Edit(p => p.ProfileSelection = value); }
    private long lastChange { get => viewModel.Preferences.LastChange; set => viewModel.Edit(p => p.LastChange = value); }
    private string selectedCategory => viewModel.Preferences.SelectedCategory;
    private string selectedTheme => viewModel.Preferences.SelectedTheme;
    private string fontFamily { get => viewModel.Preferences.FontFamily; set => viewModel.Edit(p => p.FontFamily = value); }
    private int appearance { get => viewModel.Preferences.Appearance; set => viewModel.Edit(p => p.Appearance = value); }
    private double fontScale { get => viewModel.Preferences.FontScale; set => viewModel.Edit(p => p.FontScale = value); }
    private double saturationPercent { get => viewModel.Preferences.SaturationPercent; set => viewModel.Edit(p => p.SaturationPercent = value); }
    private double hueDegrees { get => viewModel.Preferences.HueDegrees; set => viewModel.Edit(p => p.HueDegrees = value); }
    private double brightnessPercent { get => viewModel.Preferences.BrightnessPercent; set => viewModel.Edit(p => p.BrightnessPercent = value); }
    private double contrastPercent { get => viewModel.Preferences.ContrastPercent; set => viewModel.Edit(p => p.ContrastPercent = value); }
    private bool colorFiltersEnabled { get => viewModel.Preferences.ColorFiltersEnabled; set => viewModel.Edit(p => p.ColorFiltersEnabled = value); }
    private bool cycleThemes { get => viewModel.Preferences.CycleThemes; set => viewModel.Edit(p => p.CycleThemes = value); }
    private string? selectedDeviceId { get => viewModel.Preferences.SelectedDeviceId; set => viewModel.Edit(p => p.SelectedDeviceId = value); }
    private bool showSpectrum { get => viewModel.Preferences.ShowSpectrum; set => viewModel.Edit(p => p.ShowSpectrum = value); }

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

        // Populate selectors before ready enables their change handlers.
        RefreshBox.ItemsSource = RefreshIntervals.Select(value => new PanelChoice(value.ToString(), value == 1000 ? "1 s" : $"{value} ms")).ToArray();
        RefreshBox.SelectedItem = ((PanelChoice[])RefreshBox.ItemsSource).First(item => item.Id == refreshMilliseconds.ToString());
        QualityBox.ItemsSource = new[] { new PanelChoice("poor", "Poor"), new PanelChoice("low", "Low"), new PanelChoice("medium", "Medium"), new PanelChoice("high", "High") };
        QualityBox.SelectedItem = ((PanelChoice[])QualityBox.ItemsSource).First(item => item.Id == jpegQuality);
        refresh.Interval = TimeSpan.FromMilliseconds(refreshMilliseconds);
        FontBox.Text = fontFamily;
        UpdateFontScale();
        UpdateColorControls();
        FiltersBox.IsChecked = colorFiltersEnabled;
        UpdateFilterVisibility();
        SpectrumBox.IsChecked = showSpectrum;
        CycleBox.IsChecked = cycleThemes;
        MusicScreenBox.IsChecked = screenEnabled["multimedia"];
        CodexScreenBox.IsChecked = screenEnabled["codex"];
        SystemScreenBox.IsChecked = screenEnabled["system"];

        ready = true;
        ApplyAppearance();
        AboutVersion.Text = $"Display studio · {typeof(App).Assembly.GetName().Version?.ToString(3)}";
    }

    /// <summary>Connects page lifetime, refresh timers and keyboard navigation after controls are ready.</summary>
    private void AttachPageEvents()
    {
        // Initial data loading runs once the visual tree is ready.
        Loaded += async (_, _) =>
        {
            unloaded = false;
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
            }
        };
        Unloaded += (_, _) =>
        {
            unloaded = true;
            if (ownsSession) { _ = this.session.ShutdownAsync(); }
            refresh.Stop(); colorChanges.Stop();
            ClearPreview();
            if (pendingColorSettings) { CommitColorControls(); }
            SaveChoices();
        };

        // UI timers observe the session; the session owns actual engine scheduling.
        refresh.Tick += async (_, _) => await RefreshAsync(queueIfBusy: false);
        colorChanges.Tick += async (_, _) =>
        {
            colorChanges.Stop(); CommitColorControls();
            SaveChoices(); await RefreshAsync();
        };

        // Overlays and text/dropdown controls consume navigation before theme shortcuts.
        KeyDown += (_, args) =>
        {
            if (PaletteOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { ClosePalette_Click(this, args); args.Handled = true; } return; }
            if (CloseOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { CloseOverlay.Visibility = Visibility.Collapsed; args.Handled = true; } return; }
            if (AboutOverlay.Visibility == Visibility.Visible)
            { if (args.Key == Key.Escape) { CloseAbout(); args.Handled = true; } return; }
            if (SensorDebugPanel.Visibility == Visibility.Visible) { return; }
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
        showSpectrum = SpectrumBox.IsChecked == true;
        SaveChoices(); _ = RefreshAsync();
    }

    /// <summary>Start a fresh twenty-second interval when automatic theme cycling is enabled.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Cycle_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready) { return; }
        cycleThemes = CycleBox.IsChecked == true;
        lastChange = Environment.TickCount64;
        SaveChoices();
    }

    /// <summary>Saves the studio model through its session owner.</summary>
    private void SaveChoices() => session.Save();

}
