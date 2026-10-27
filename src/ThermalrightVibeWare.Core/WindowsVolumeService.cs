/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsVolumeService.cs
 * Purpose: Reads per-session Windows output volume and peak levels.
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

using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ThermalrightVibeWare.Core;

/// <summary>Read-only playback-session volumes and peak meters. No capture clients or volume setters.</summary>
public sealed class WindowsVolumeService : IVolumeProvider
{
    private readonly object gate = new();
    private readonly List<MMDevice> endpoints = [];
    private readonly List<Session> sessions = [];
    private DateTime refreshed;
    private bool disposed;
    private sealed record Session(string Key, string Name, int ProcessId, AudioSessionControl Control);

    /// <summary>Reads Windows audio sessions and their read-only volume/peak observations.</summary>
    public IReadOnlyList<AppVolume> Read()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (DateTime.UtcNow - refreshed > TimeSpan.FromSeconds(3)) { Refresh(); }
            var result = new List<AppVolume>();
            foreach (var session in sessions)
            {
                try
                {
                    var volume = session.Control.SimpleAudioVolume;
                    result.Add(new(session.Key, session.Name, session.ProcessId, Math.Round(volume.Volume * 100), volume.Mute, Peak(session.Control)));
                }
                catch (COMException) { refreshed = DateTime.MinValue; }
            }
            return result.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>Samples existing meter handles only; no endpoint/process discovery at the UI meter rate.</summary>
    public IReadOnlyDictionary<string, double?> ReadLevels()
    {
        lock (gate)
        {
            if (disposed) { return new Dictionary<string, double?>(); }
            return sessions.ToDictionary(item => item.Key, item => Peak(item.Control));
        }
    }

    /// <summary>Reads a session's peak meter, returning unavailable when access fails.</summary>
    /// <param name="control">Read-only Windows audio session control used to access its meter.</param>
    private static double? Peak(AudioSessionControl control)
    {
        try { return control.AudioMeterInformation is { } meter ? Math.Clamp(meter.MasterPeakValue, 0, 1) : null; }
        catch (COMException) { return null; }
    }

    /// <summary>Refreshes the Windows output endpoint and its audio session handles.</summary>
    private void Refresh()
    {
        Clear();
        using var devices = new MMDeviceEnumerator();
        foreach (var endpoint in devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            endpoints.Add(endpoint);
            var current = endpoint.AudioSessionManager.Sessions;
            for (var i = 0; i < current.Count; i++)
            {
                var control = current[i];
                try
                {
                    if (control.State == AudioSessionState.AudioSessionStateExpired) { control.Dispose(); continue; }
                    var pid = checked((int)control.GetProcessID);
                    string name;
                    try { using var process = Process.GetProcessById(pid); name = control.IsSystemSoundsSession || pid == 0 ? "System sounds" : process.ProcessName; }
                    catch (ArgumentException) { name = pid == 0 ? "System sounds" : $"Process {pid}"; }
                    sessions.Add(new(endpoint.ID + "|" + control.GetSessionInstanceIdentifier, name, pid, control));
                }
                catch (COMException) { control.Dispose(); }
            }
        }
        refreshed = DateTime.UtcNow;
    }

    /// <summary>Clears the currently held preview or session resources.</summary>
    private void Clear()
    {
        foreach (var session in sessions) { session.Control.Dispose(); }
        sessions.Clear();
        foreach (var endpoint in endpoints) { endpoint.Dispose(); }
        endpoints.Clear();
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        lock (gate) { if (disposed) { return; } Clear(); disposed = true; }
    }
}
