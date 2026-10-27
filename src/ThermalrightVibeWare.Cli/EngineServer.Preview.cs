/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: EngineServer.Preview.cs
 * Purpose: Builds bounded preview observations separately from physical display presentation.
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

using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Core;

internal sealed partial class EngineServer
{
    /// <summary>Builds the preview observation without sending a frame to USB.</summary>
    /// <param name="options">Validated preview controls, selected profile/theme, screens and palette overrides.</param>
    private async Task<PanelState> StateAsync(PreviewOptions options)
    {
        // Resolve a validated theme/profile before sampling any providers.
        ValidatePreviewOptions(options);
        var theme = FindTheme(options.ThemeId ?? throw new ArgumentException("Choose a theme for the preview."));
        var profile = DisplayProfileCatalog.GetRequired(options.ProfileId) with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 };
        const bool includeSystem = false;
        var includeMedia = theme.Category == "multimedia";
        var includeSensors = theme.Category == "system";

        // Reuse source data within the selected interval, but never across different provider needs.
        if (lastSnapshot is null || snapshotIncludesSystem != includeSystem || snapshotIncludesMedia != includeMedia || snapshotIncludesSensors != includeSensors
            || Environment.TickCount64 - snapshotAt >= options.RefreshIntervalMilliseconds)
        {
            lastSnapshot = await dashboard.ReadAsync(includeSystem, includeMedia, analyzePlayback: theme.Category == "multimedia", includeSensors: includeSensors);
            snapshotAt = Environment.TickCount64;
            snapshotIncludesSystem = includeSystem;
            snapshotIncludesMedia = includeMedia;
            snapshotIncludesSensors = includeSensors;
        }
        var snapshot = lastSnapshot;
        if (options.IncludeDevices && (discoveredAt == 0 || Environment.TickCount64 - discoveredAt >= 5000))
        {
            DiscoverDevices();
        }

        // Configuration changes invalidate the cached JPEG even when source data was reused.
        var fingerprint = $"{theme.Id}|{profile.Id}|{options.Light}|{options.AmbientDrift}|{options.FontFamily}|{options.FontScale}|{options.JpegQuality}|{options.SaturationPercent}|{options.HueDegrees}|{options.ShowSpectrum}|{options.BrightnessPercent}|{options.ContrastPercent}|{options.ColorFiltersEnabled}|{options.PageIndex}|{options.PageRevision}";
        var paletteKey = theme.Id + (options.Light ? "@light" : "@dark");
        var palette = options.Palettes?.GetValueOrDefault(paletteKey) ?? options.Palettes?.GetValueOrDefault(theme.Id);
        fingerprint += "|" + JsonSerializer.Serialize(palette, PanelContract.Json);
        var configurationChanged = previousFingerprint != fingerprint;
        var refreshInterval = previewNotice is null ? themeRenderer.FrameIntervalMilliseconds(options.RefreshIntervalMilliseconds) : 5000;
        if (options.IncludeFrame && (configurationChanged || Environment.TickCount64 - previewAt >= refreshInterval))
        {
            var renderingStartedAt = Environment.TickCount64;
            if (configurationChanged) { preview = null; }
            try
            {
                preview = await themeRenderer.RenderAsync(theme, snapshot, profile, options.Light, options.AmbientDrift, options.FontFamily, options.FontScale, options.JpegQuality,
                    saturationPercent: options.SaturationPercent, hueDegrees: options.HueDegrees, showSpectrum: options.ShowSpectrum,
                    brightnessPercent: options.BrightnessPercent, contrastPercent: options.ContrastPercent, colorFiltersEnabled: options.ColorFiltersEnabled, palette: palette,
                    pageIndex: options.PageIndex, pageRevision: options.PageRevision);
                previewNotice = null;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            { DiagnosticLog.ThemeRenderFailure(error.GetType().Name); previewNotice = "Theme preview: " + error.Message; }
            previousFingerprint = fingerprint;
            previewAt = previewNotice is null ? renderingStartedAt : Environment.TickCount64;
        }

        // Project the observation without submitting it to USB; present owns physical frame output.
        var metrics = snapshot.Metrics;
        if (snapshot.Sensors?.Driver is { } driver) { sensorDriver = driver; }
        return new(snapshot.Media.Title, snapshot.Media.Artist, snapshot.Media.Album, snapshot.Media.Playing,
            snapshot.Media.Source,
            snapshot.Volumes.Select(item => new AudioLevelDto(item.Key, item.Name, item.Percent, item.Muted, item.Peak)).ToArray(),
            new(metrics.CpuUsage, metrics.CpuTemperature, metrics.GpuUsage, metrics.GpuTemperature, metrics.GpuName, metrics.CpuTemperatureSource,
                sensorDriver is null ? null : SensorDriverStatus()),
            snapshot.Network is { } network ? new(network.DownloadBytesPerSecond, network.UploadBytesPerSecond, network.DownloadBytes, network.UploadBytes, "Since engine start; adapter counters may overlap on VPN/bridges") : null,
            DeviceChoices(),
            DisplayProfileCatalog.All.Select(item => new PanelChoice(item.Id, item.Name)).ToArray(),
            themeOutput is not null, Volatile.Read(ref status), options.IncludeFrame ? preview : null,
            previewNotice is null ? snapshot.Notices : snapshot.Notices.Concat([previewNotice]).ToArray(),
            frameMilliseconds, themeOutput?.SubmittedFrames ?? 0,
            options.IncludeFrame && preview is not null ? themeRenderer.AccentColor : null,
            options.IncludeFrame && preview is not null && themeRenderer.IsTransitioning
                ? themeRenderer.FrameIntervalMilliseconds(options.RefreshIntervalMilliseconds) : null,
            theme.Id, codexProcess.IsRunning(), ArtistSources: snapshot.ArtistProfile is { } artist
                ? new(artist.SourceUrl, artist.PhotoSourceUrl, artist.AlbumSales?.SourceUrl) : null,
            ActivePageId: themeRenderer.ActivePageId, ActivePageTitle: themeRenderer.ActivePageTitle,
            ActivePageIndex: themeRenderer.ActivePageIndex, ActivePageCount: themeRenderer.ActivePageCount);
    }

    /// <summary>Rejects invalid renderer and display options before work starts.</summary>
    /// <param name="options">Validated preview controls, selected profile/theme, screens and palette overrides.</param>
    private static void ValidatePreviewOptions(PreviewOptions options)
    {
        if (options.RefreshIntervalMilliseconds is < 25 or > 1000) { throw new ArgumentException("Refresh interval must be between 25 and 1000 ms."); }
        if (options.PageIndex is < 0 or > 7 || options.PageRevision < 0) { throw new ArgumentException("Invalid theme page selection."); }
        if (options.JpegQuality is not ("poor" or "low" or "medium" or "high")) { throw new ArgumentException("JPEG quality must be Poor, Low, Medium or High."); }
        PythonThemeRenderer.ValidateColorAdjustments(options.SaturationPercent, options.HueDegrees, options.BrightnessPercent, options.ContrastPercent);
        ScreenSelectionPolicy.Validate(options.Screens);
        if (options.Palettes is { } palettes)
        {
            if (palettes.Count > 16 || palettes.Keys.Any(key => key.Length > 110)) { throw new ArgumentException("Too many palette overrides."); }
            foreach (var palette in palettes.Values) { if (palette is null) { throw new ArgumentException("Invalid palette."); } palette.Validate(); }
        }
    }

    /// <summary>Resolves a catalog theme by its stable identifier.</summary>
    /// <param name="id">Stable theme identifier from the validated local theme catalog.</param>
    private ThemeDefinition FindTheme(string id) => (themeCatalog ??= ThemeCatalog.Read()).Themes.FirstOrDefault(item => item.Id == id)
        ?? throw new ArgumentException("Theme not found. Refresh the theme catalog.");
}
