/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsPlaybackAnalyzer.cs
 * Purpose: Analyzes bounded Windows output-loopback samples into spectrum and stereo meters.
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

using System.Buffers.Binary;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace ThermalrightVibeWare.Core;

/// <summary>
/// Opt-in, in-memory analysis of the default Windows render endpoint. It never opens an input
/// endpoint, writes audio to disk, or sends audio over a network. Constructing it opens nothing.
/// </summary>
public sealed partial class WindowsPlaybackAnalyzer : IPlaybackProvider
{
    private const int TransformSize = 4096;
    private const int BandCount = 32;
    private const double FloorDbfs = -90;
    private readonly object gate = new();
    private CaptureSession? session;
    private long nextDeviceCheck;
    private bool disposed;
    private PlaybackAnalysis unavailable = Unavailable("Playback analysis is off.");

    /// <summary>Starts lazily and checks the default output or retries a failure at most once a second.</summary>
    public PlaybackAnalysis Read()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!OperatingSystem.IsWindows())
            {
                unavailable = Unavailable("Playback analysis requires the Windows backend.");
                return Copy(unavailable);
            }
            var now = Environment.TickCount64;
            if (now >= nextDeviceCheck)
            {
                nextDeviceCheck = now + 1000;
                MMDevice? candidate = null;
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    candidate = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    if (candidate.DataFlow != DataFlow.Render) { throw new InvalidOperationException("No playback endpoint is available."); }
                    if (session is null || session.Stopped || !StringComparer.Ordinal.Equals(candidate.ID, session.DeviceId))
                    {
                        CloseSession();
                        var selected = candidate;
                        candidate = null; // CaptureSession owns and disposes the selected endpoint, including on failure.
                        session = new CaptureSession(selected);
                    }
                }
                catch (Exception error)
                {
                    CloseSession();
                    unavailable = Unavailable($"Playback analysis unavailable: {error.Message}");
                }
                finally { candidate?.Dispose(); }
            }
            return SnapshotCore();
        }
    }

    /// <summary>Reads only the latest bounded analysis; does not open or enumerate devices.</summary>
    public PlaybackAnalysis Snapshot()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return SnapshotCore();
        }
    }

    /// <summary>Stops output loopback and clears its analysis session.</summary>
    public void Stop()
    {
        lock (gate)
        {
            if (session is null && nextDeviceCheck == 0) { return; }
            CloseSession();
            nextDeviceCheck = 0;
            unavailable = Unavailable("Playback analysis is off.");
        }
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) { return; }
            disposed = true;
            CloseSession();
        }
    }

    /// <summary>Returns the active capture snapshot or an unavailable observation.</summary>
    private PlaybackAnalysis SnapshotCore() => session?.Snapshot() ?? Copy(unavailable);

    /// <summary>Stops and disposes the current output capture session.</summary>
    private void CloseSession()
    {
        var previous = session;
        session = null;
        previous?.Dispose();
    }

    /// <summary>Returns an independent copy so callers cannot mutate the published snapshot.</summary>
    /// <param name="value">Published playback analysis whose mutable band arrays must be copied.</param>
    private static PlaybackAnalysis Copy(PlaybackAnalysis value) => value with
    {
        BandDbfs = (double[])value.BandDbfs.Clone(),
        FrequenciesHz = (double[])value.FrequenciesHz.Clone()
    };

    /// <summary>Creates silent spectrum bands at the configured dBFS floor.</summary>
    private static double[] FloorBands()
    {
        var values = new double[BandCount];
        Array.Fill(values, FloorDbfs);
        return values;
    }

    /// <summary>Calculates logarithmic spectrum band centers below the Nyquist limit.</summary>
    /// <param name="sampleRate">Capture sample rate in samples per second.</param>
    private static double[] BandCenters(int sampleRate)
    {
        var upper = sampleRate > 0 ? Math.Min(20000d, sampleRate / 2d) : 20000d;
        var centers = new double[BandCount];
        for (var band = 0; band < BandCount; band++)
        { centers[band] = 20 * Math.Pow(upper / 20, band / (double)(BandCount - 1)); }
        return centers;
    }

    /// <summary>Creates an unavailable playback observation with an explicit status.</summary>
    /// <param name="status">Available status text shown to the user.</param>
    private static PlaybackAnalysis Unavailable(string status) => new(false, "system-output", 0, TransformSize, 0,
        FloorBands(), BandCenters(0), null, null, null, null, null, null, null, status);

    // WasapiLoopbackCapture's standard constructor requests 100 ms. This documented extension
    // point requests a 50 ms shared-mode buffer, but the endpoint chooses its actual latency.
}
