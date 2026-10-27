/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: SingleInstanceService.cs
 * Purpose: Enforces one studio instance per user and forwards activation requests.
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

using System.Security.Principal;

namespace ThermalrightVibeWare.Photino;

/// <summary>Keeps one Windows studio per user and session, across portable and installed copies.</summary>
internal sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex? _mutex;
    private readonly EventWaitHandle? _activation;
    private RegisteredWaitHandle? _registration;
    private int _disposed;

    public bool IsPrimary { get; }

    /// <summary>Create and dispose this owner on the same main thread.</summary>
    public SingleInstanceService()
    {
        if (!OperatingSystem.IsWindows())
        {
            IsPrimary = true;
            return;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User?.Value
            ?? throw new InvalidOperationException("The current Windows user is unavailable.");
        // Local scopes the objects to this sign-in session; the SID also separates users.
        // The name deliberately does not depend on the executable path or app version.
        var name = @"Local\UmbertoGiacobbi.ThermalrightVibeWare." + user;
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        try
        {
            _mutex = new Mutex(false, name);
            try { IsPrimary = _mutex.WaitOne(0); }
            catch (AbandonedMutexException) { IsPrimary = true; }
        }
        catch
        {
            _mutex?.Dispose();
            _activation.Dispose();
            throw;
        }
    }

    /// <summary>Begin listening after the native window is ready. Dispatch must marshal onto its UI thread.</summary>
    /// <param name="dispatchToUi">Schedules an action on the owning desktop UI thread.</param>
    /// <param name="activateWindow">Restores the existing studio window after an activation request.</param>
    public void Listen(Action<Action> dispatchToUi, Action activateWindow)
    {
        ArgumentNullException.ThrowIfNull(dispatchToUi);
        ArgumentNullException.ThrowIfNull(activateWindow);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!IsPrimary || _registration is not null)
        {
            throw new InvalidOperationException("Only the primary studio can register one activation listener.");
        }
        if (_activation is null) { return; }
        _registration = ThreadPool.RegisterWaitForSingleObject(_activation, (_, _) =>
        {
            if (Volatile.Read(ref _disposed) != 0) { return; }
            try
            {
                dispatchToUi(() =>
                {
                    if (Volatile.Read(ref _disposed) == 0) { activateWindow(); }
                });
            }
            catch (InvalidOperationException)
            {
                // A second launch can race with the primary window closing.
                Console.Error.WriteLine("Thermalright VibeWare: the previous studio is closing.");
            }
        }, null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>The auto-reset event retains a request made before the first window is ready.</summary>
    public bool RequestActivation()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _activation?.Set() ?? false;
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
        _registration?.Unregister(null);
        _activation?.Dispose();
        if (IsPrimary) { _mutex?.ReleaseMutex(); }
        _mutex?.Dispose();
    }
}
