/* VBWR B
 * Project: Thermalright VibeWare
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * Purpose: Composes Linux previews from kernel sensors and explicit unavailable sources.
 * Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
 * VBWR E */

#if !WINDOWS
namespace ThermalrightVibeWare.Core;

/// <summary>Linux preview sources. Construction performs no sampling or device access.</summary>
public static class LinuxDashboardProviders
{
    public static DashboardProviders Create(IMetricsProvider? metrics = null) => new(new UnavailableMedia(), new UnavailableAudio(),
        metrics ?? new LinuxMetricsService(allowHardwareAccess: true), new UnavailablePlayback(), new UnavailableNetwork());

    private sealed class UnavailableMedia : IMediaProvider
    {
        public Task<NowPlaying> ReadAsync() => Task.FromResult(new NowPlaying("Media unavailable on Linux", "", "", "", false,
            null, 0, 0, PlaybackStatus: "unavailable"));

        public Task ControlAsync(string action) => throw new PlatformNotSupportedException("Linux media sessions are not implemented.");
    }

    private sealed class UnavailableAudio : IVolumeProvider
    {
        public IReadOnlyList<AppVolume> Read() => throw new PlatformNotSupportedException("Linux audio sessions are not implemented.");
        public IReadOnlyDictionary<string, double?> ReadLevels() => throw new PlatformNotSupportedException("Linux audio levels are not implemented.");
        public void Dispose() { }
    }

    private sealed class UnavailablePlayback : IPlaybackProvider
    {
        private static PlaybackAnalysis Observation() => new(false, "Linux output analysis unavailable", 0, 0, 0, [], [],
            null, null, null, null, null, null, null, "Linux output loopback is not implemented. No audio device was opened.");

        public PlaybackAnalysis Read() => Observation();
        public PlaybackAnalysis Snapshot() => Observation();
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class UnavailableNetwork : INetworkProvider
    {
        public NetworkTraffic Read() => throw new PlatformNotSupportedException("Linux physical network counters are not implemented.");
    }
}
#endif
