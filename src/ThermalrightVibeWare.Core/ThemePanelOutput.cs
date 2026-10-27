/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: ThemePanelOutput.cs
 * Purpose: Sends the rendered JPEG through one explicitly opened panel session.
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

/// <summary>One explicitly opened panel. Reuses the already rendered preview, without another renderer.</summary>
public sealed class ThemePanelOutput : IDisposable
{
    private readonly PanelSession session;
    private byte[]? previousJpeg;
    private string? previousQuality;
    private long submittedAt;
    private bool disposed;

    /// <summary>Takes ownership of the opened panel session and resolved profile.</summary>
    /// <param name="session">Opened panel session whose lifetime is owned by this output instance.</param>
    /// <param name="profile">Validated target dimensions and pixel encoding.</param>
    private ThemePanelOutput(PanelSession session, DisplayProfile profile) { this.session = session; Profile = profile; }
    public DisplayProfile Profile { get; }
    public long SubmittedFrames { get; private set; }

    /// <summary>Opens and handshakes with the explicitly selected display.</summary>
    /// <param name="device">Explicitly selected discovered display descriptor; no automatic device selection occurs here.</param>
    /// <param name="profileId">Profile identifier; auto requests resolution from the device handshake where supported.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    public static async Task<ThemePanelOutput> OpenAsync(DisplayDevice device, string profileId = "auto", CancellationToken cancellationToken = default)
    {
        var requested = profileId == "auto" ? null : DisplayProfileCatalog.GetRequired(profileId);
        var session = PanelSession.Open(device);
        try
        {
            var probe = await session.ProbeAsync(cancellationToken);
            if (!probe.IsCompatible) { throw new IOException(probe.Message); }
            var profile = requested ?? probe.DetectedProfile ?? throw new NotSupportedException("Unknown display size. Choose a profile first.");
            if (requested is not null && probe.DetectedProfile is { } detected && requested.Id == detected.Id)
            { profile = requested with { WireRotation = detected.WireRotation }; }
            return new(session, profile);
        }
        catch { session.Dispose(); throw; }
    }

    /// <summary>Skips identical frames for up to ten seconds. Calls must be serialized by the owner.</summary>
    /// <param name="jpeg">Encoded JPEG bytes, reused for preview and physical output.</param>
    /// <param name="jpegQuality">Requested JPEG preset: poor, low, medium or high.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    public async Task<bool> SendAsync(byte[] jpeg, string jpegQuality = "high", CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var quality = jpegQuality switch { "poor" => 20, "low" => 50, "medium" => 75, "high" => 90, _ => throw new ArgumentException("JPEG quality must be poor, low, medium or high.") };
        // A short unplug/replug can happen between enumerations with the same interface path.
        // Reuse the JPEG, but periodically write it to detect a stale handle. Never reopen it here.
        if (previousQuality == jpegQuality && previousJpeg is not null && previousJpeg.AsSpan().SequenceEqual(jpeg)
            && Environment.TickCount64 - submittedAt < 10_000) { return false; }
        var payload = Profile is { Encoding: PanelPixelEncoding.Jpeg, WireRotation: 0 }
            ? jpeg : PanelFrameRenderer.PrepareJpeg(jpeg, Profile, quality);
        var packet = Thermalright04165302Protocol.CreatePicturePacket(Profile, payload);
        await session.SendPacketAsync(packet, cancellationToken);
        previousJpeg = jpeg;
        previousQuality = jpegQuality;
        submittedAt = Environment.TickCount64;
        SubmittedFrames++;
        return true;
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        previousJpeg = null;
        session.Dispose();
    }
}
