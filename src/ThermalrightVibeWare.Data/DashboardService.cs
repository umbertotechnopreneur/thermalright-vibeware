/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: DashboardService.cs
 * Purpose: Combines requested data providers while preserving unavailable-source notices.
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

namespace ThermalrightVibeWare.Core;

/// <summary>Shared local data sources for both desktop and CLI. No Spotify API, login, or telemetry.</summary>
public sealed class DashboardService : IDisposable
{
    public IMediaProvider Media { get; }
    public IVolumeProvider Audio { get; }
    private readonly IMetricsProvider metrics;
    private readonly INetworkProvider network;
    private readonly IArtistProvider? artists;
    private readonly IPlaybackProvider playback;

    /// <summary>Connects the dashboard's media, audio, metrics and public artist providers.</summary>
    /// <param name="providers">Dashboard data providers whose lifetime is managed by this service.</param>
    public DashboardService(DashboardProviders providers)
    {
        Media = providers.Media; Audio = providers.Audio; metrics = providers.Metrics;
        network = providers.Network; playback = providers.Playback; artists = providers.Artists;
    }

    /// <summary>Collects the requested dashboard sources and preserves per-source availability notices.</summary>
    /// <param name="includeSystem">Whether to collect system metrics for this observation.</param>
    /// <param name="includeMedia">Whether to collect media metadata and audio session information.</param>
    /// <param name="analyzePlayback">Whether to enable output loopback analysis for this observation.</param>
    /// <param name="includeSensors">Whether to include the detailed hardware sensor snapshot.</param>
    public async Task<DashboardSnapshot> ReadAsync(bool includeSystem = true, bool includeMedia = true, bool analyzePlayback = false,
        bool includeSensors = false)
    {
        var includePlayback = analyzePlayback && includeMedia;
        // The constructor does not open an audio device. Only this explicit multimedia path does.
        var notices = new List<string>();
        PlaybackAnalysis? analysis = null;
        try
        {
            if (includePlayback) { analysis = await Task.Run(playback.Read); }
            else { await Task.Run(playback.Stop); }
        }
        catch (Exception error) { notices.Add($"Playback unavailable: {error.Message}"); }
        var media = NowPlaying.Empty;
        IReadOnlyList<AppVolume> volumes = [];
        MachineMetrics machine;
        HardwareSensorSnapshot? sensors = null;
        if (includeMedia)
        {
            try { media = await Media.ReadAsync(); }
            catch (Exception error) { notices.Add($"Media unavailable: {error.Message}"); }
            try { volumes = await Task.Run(Audio.Read); }
            catch (Exception error) { notices.Add($"Mixer unavailable: {error.Message}"); }
        }
        var artistProfile = includeMedia ? artists?.Read(media) : null;
        if (includeSensors)
        {
            try { sensors = await Task.Run(metrics.ReadSensors); }
            catch (Exception error)
            {
                sensors = HardwareSensorSnapshot.Empty with { Notice = "Sensors unavailable: " + error.Message };
            }
            if (!string.IsNullOrWhiteSpace(sensors.Notice)) { notices.Add(sensors.Notice); }
        }
        if (!includeSystem)
        {
            return new(media, volumes, new(null, null, null, null, "", "Not sampled for themes", "Not sampled for themes"), notices,
                Playback: analysis, Sensors: sensors, ArtistProfile: artistProfile);
        }
        try { machine = await Task.Run(metrics.Read); }
        catch (Exception error)
        {
            notices.Add($"Sensors unavailable: {error.Message}");
            machine = new(null, null, null, null, "GPU", "Unavailable", "Unavailable");
        }
        NetworkTraffic? traffic = null;
        try { traffic = network.Read(); }
        catch (Exception error) { notices.Add($"Network counters unavailable: {error.Message}"); }
        return new(media, volumes, machine, notices, traffic, analysis, sensors, artistProfile);
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        artists?.Dispose();
        try { playback.Dispose(); }
        finally
        {
            try { Audio.Dispose(); }
            finally { metrics.Dispose(); }
        }
    }
}
