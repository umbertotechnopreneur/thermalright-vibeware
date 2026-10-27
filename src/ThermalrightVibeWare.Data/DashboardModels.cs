/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: DashboardModels.cs
 * Purpose: Defines media, audio, system and sourced artist observations.
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

/// <summary>Contains the current Windows media-session information without an external account.</summary>
public sealed record NowPlaying(string Title, string Artist, string Album, string Source,
    bool Playing, byte[]? Artwork, double PositionSeconds, double DurationSeconds, bool? Shuffle = null, string? PlaybackStatus = null)
{
    public static NowPlaying Empty { get; } = new("Nothing playing yet", "Open a media player", "", "", false, null, 0, 0);
}

/// <summary>Read-only volume and instantaneous peak of one Windows playback session, never microphone audio.</summary>
public sealed record AppVolume(string Key, string Name, int ProcessId, double Percent, bool Muted, double? Peak = null);

/// <summary>Labels missing sensor data explicitly and records the source of available values.</summary>
public sealed record MachineMetrics(double? CpuUsage, double? CpuTemperature, double? GpuUsage,
    double? GpuTemperature, string GpuName, string CpuTemperatureSource, string GpuSource);

/// <summary>Local playback analysis of the system output mix, not an app or microphone recording.</summary>
public sealed record PlaybackAnalysis(bool Available, string Source, int SampleRate, int FftSize, int Channels,
    double[] BandDbfs, double[] FrequenciesHz, double? LeftPeakDbfs, double? RightPeakDbfs,
    double? LeftRmsDbfs, double? RightRmsDbfs, double? LeftPeakHoldDbfs, double? RightPeakHoldDbfs,
    DateTimeOffset? UpdatedAtUtc, string Status);

/// <summary>A bounded snapshot used by the UI, CLI, and USB renderer.</summary>
public sealed record DashboardSnapshot(NowPlaying Media, IReadOnlyList<AppVolume> Volumes,
    MachineMetrics Metrics, IReadOnlyList<string> Notices, NetworkTraffic? Network = null, PlaybackAnalysis? Playback = null,
    HardwareSensorSnapshot? Sensors = null, ArtistProfile? ArtistProfile = null);

/// <summary>Attributed online artist context; absent fields stay absent.</summary>
public sealed record ArtistFact(string Label, string Value);
public sealed record AlbumSales(string Album, double Units, string Label, string AsOf, string SourceUrl);
public sealed record ArtistProfile(string RequestedArtist, string Name, string Biography, string SourceUrl,
    string BiographyCredit, byte[]? Photo, string? PhotoCredit, string? PhotoSourceUrl,
    IReadOnlyList<ArtistFact> Facts, AlbumSales? AlbumSales);
