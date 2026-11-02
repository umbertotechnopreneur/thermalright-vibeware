/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsMediaService.cs
 * Purpose: Reads and controls the selected generic Windows media session.
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

using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ThermalrightVibeWare.Core;

/// <summary>Uses the current generic Windows media session without a service-specific account.</summary>
public sealed class WindowsMediaService : IMediaProvider
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private string? artworkKey;
    private byte[]? cachedArtwork;
    private NowPlaying? metadata;
    private long metadataAt;

    /// <summary>Resolves the current Windows media session within the request lifetime.</summary>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    private async Task<GlobalSystemMediaTransportControlsSession?> SessionAsync(CancellationToken cancellationToken)
    {
        manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken);
        return manager.GetCurrentSession()
            ?? manager.GetSessions().FirstOrDefault(session => session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            ?? manager.GetSessions().FirstOrDefault();
    }

    /// <summary>Reads current metadata; thumbnail bytes are reloaded only when the track identity changes.</summary>
    public async Task<NowPlaying> ReadAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var session = await SessionAsync(deadline.Token);
        if (session is null) { metadata = null; cachedArtwork = null; artworkKey = null; return NowPlaying.Empty; }
        // Metadata/artwork can be expensive. Timeline and cached audio meters still update at the selected rate.
        if (metadata is null || metadata.Source != session.SourceAppUserModelId || Environment.TickCount64 - metadataAt >= 1000)
        {
            var properties = await session.TryGetMediaPropertiesAsync().AsTask(deadline.Token);
            var key = $"{session.SourceAppUserModelId}\n{properties.Title}\n{properties.Artist}\n{properties.AlbumTitle}";
            if (key != artworkKey)
            {
                cachedArtwork = null;
                if (properties.Thumbnail is not null)
                {
                    using var stream = await properties.Thumbnail.OpenReadAsync().AsTask(deadline.Token);
                    if (stream.Size is > 0 and <= 5 * 1024 * 1024)
                    {
                        using var reader = new DataReader(stream.GetInputStreamAt(0));
                        var size = checked((uint)stream.Size);
                        await reader.LoadAsync(size).AsTask(deadline.Token);
                        cachedArtwork = new byte[size];
                        reader.ReadBytes(cachedArtwork);
                    }
                }
                artworkKey = key;
            }
            metadata = new(properties.Title, properties.Artist, properties.AlbumTitle, session.SourceAppUserModelId, false, cachedArtwork, 0, 0);
            metadataAt = Environment.TickCount64;
        }
        var timeline = session.GetTimelineProperties();
        var playback = session.GetPlaybackInfo();
        var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        var position = Math.Max(0, (timeline.Position - timeline.StartTime).TotalSeconds);
        var duration = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
        // Windows defines Position at LastUpdatedTime; advance only when the session provides a playback rate.
        if (playing && playback.PlaybackRate is { } rate && double.IsFinite(rate) && rate > 0 && timeline.LastUpdatedTime > DateTimeOffset.UnixEpoch)
        {
            var advanced = position + Math.Max(0, (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds) * rate;
            if (double.IsFinite(advanced)) { position = advanced; }
        }
        if (duration > 0) { position = Math.Clamp(position, 0, duration); }
        return metadata with
        {
            Playing = playing,
            PositionSeconds = position,
            DurationSeconds = duration,
            Shuffle = playback.IsShuffleActive,
            PlaybackStatus = playback.PlaybackStatus.ToString()
        };
    }

    /// <summary>Controls the selected local player only after a user invokes a media command.</summary>
    /// <param name="action">Allowed Windows media playback command.</param>
    public async Task ControlAsync(string action)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var session = await SessionAsync(deadline.Token) ?? throw new InvalidOperationException("No Windows media session is available. Open a media player first.");
        var accepted = action switch
        {
            "toggle" => await session.TryTogglePlayPauseAsync().AsTask(deadline.Token),
            "play" => await session.TryPlayAsync().AsTask(deadline.Token),
            "pause" => await session.TryPauseAsync().AsTask(deadline.Token),
            "next" => await session.TrySkipNextAsync().AsTask(deadline.Token),
            "previous" => await session.TrySkipPreviousAsync().AsTask(deadline.Token),
            _ => throw new ArgumentException("Choose play, pause, toggle, next, or previous.", nameof(action))
        };
        if (!accepted) { throw new InvalidOperationException("The player declined this media command."); }
    }
}
