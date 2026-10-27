/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsSetupShutdown.cs
 * Purpose: Receives the installer's scoped studio shutdown signal.
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

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;

namespace ThermalrightVibeWare.Photino;

/// <summary>Lets the Windows installer request the normal studio shutdown before replacing files.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsSetupShutdown : IDisposable
{
    private readonly EventWaitHandle _shutdown;
    private readonly RegisteredWaitHandle _registration;
    private int _disposed;

    /// <summary>Listen once the native window is ready, using the current user's default event security.</summary>
    /// <param name="dispatchToUi">Schedules an action on the owning desktop UI thread.</param>
    /// <param name="requestExit">Requests a normal studio shutdown.</param>
    internal WindowsSetupShutdown(Action<Action> dispatchToUi, Action requestExit)
    {
        ArgumentNullException.ThrowIfNull(dispatchToUi);
        ArgumentNullException.ThrowIfNull(requestExit);
        using var process = Process.GetCurrentProcess();
        // The installer retains the process handle and validates its installation path first.
        // The creation FILETIME prevents a reused process ID from naming another studio's event.
        var name = @"Global\UmbertoGiacobbi.ThermalrightVibeWare.SetupClose."
            + process.Id.ToString(CultureInfo.InvariantCulture) + "."
            + process.StartTime.ToFileTimeUtc().ToString(CultureInfo.InvariantCulture);
        _shutdown = new EventWaitHandle(false, EventResetMode.AutoReset, name);
        try
        {
            _registration = ThreadPool.RegisterWaitForSingleObject(_shutdown, (_, _) =>
            {
                if (Volatile.Read(ref _disposed) != 0) { return; }
                try
                {
                    dispatchToUi(() =>
                    {
                        if (Volatile.Read(ref _disposed) == 0) { requestExit(); }
                    });
                }
                catch (Exception error) when (error is InvalidOperationException or ApplicationException or Win32Exception)
                {
                    // A setup request can race with the user closing the native window.
                    Console.Error.WriteLine("Thermalright VibeWare setup: shutdown request unavailable: " + error.Message);
                }
            }, null, Timeout.Infinite, executeOnlyOnce: true);
        }
        catch
        {
            _shutdown.Dispose();
            throw;
        }
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
        // Do not wait for a callback which may itself be waiting for the UI thread.
        // Both callback gates prevent a queued request from reaching an already disposed listener.
        _registration.Unregister(null);
        _shutdown.Dispose();
    }
}
