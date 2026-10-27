/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: DashboardProviders.cs
 * Purpose: Defines platform provider interfaces without selecting or opening hardware.
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

public interface IMediaProvider
{
    /// <summary>Reads the selected media session's metadata and playback state.</summary>
    Task<NowPlaying> ReadAsync();

    /// <summary>Sends an allowed playback command to the selected Windows media session.</summary>
    /// <param name="action">Allowed Windows media playback command.</param>
    Task ControlAsync(string action);
}
public interface IVolumeProvider : IDisposable
{
    /// <summary>Reads per-session volume percentages and available peak levels.</summary>
    IReadOnlyList<AppVolume> Read();

    /// <summary>Reads current audio session levels from cached session handles.</summary>
    IReadOnlyDictionary<string, double?> ReadLevels();
}
public interface IMetricsProvider : IDisposable
{
    /// <summary>Collects supported hardware readings and preserves missing-access notices.</summary>
    HardwareSensorSnapshot ReadSensors();

    /// <summary>Reads aggregate machine usage and temperature observations.</summary>
    MachineMetrics Read();
}
public interface IPlaybackProvider : IDisposable
{
    /// <summary>Starts or refreshes output-loopback analysis and returns its latest observation.</summary>
    PlaybackAnalysis Read();

    /// <summary>Returns the latest analysis with independent band arrays.</summary>
    PlaybackAnalysis Snapshot();

    /// <summary>Stops output loopback and clears its analysis session.</summary>
    void Stop();
}
public interface IArtistProvider : IDisposable
{
    /// <summary>Returns cached artist facts and permits a bounded public-source lookup.</summary>
    /// <param name="media">Current Windows media observation used for artist lookup or artwork.</param>
    ArtistProfile? Read(NowPlaying media);
}

public interface INetworkProvider
{
    /// <summary>Samples physical network counters and their session deltas.</summary>
    NetworkTraffic Read();
}

/// <summary>The host supplies platform implementations; the data layer never selects or opens hardware.</summary>
public sealed record DashboardProviders(IMediaProvider Media, IVolumeProvider Audio, IMetricsProvider Metrics,
    IPlaybackProvider Playback, INetworkProvider Network, IArtistProvider? Artists = null);
