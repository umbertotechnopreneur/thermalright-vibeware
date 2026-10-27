/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: PanelSession.cs
 * Purpose: Owns one selected HID/WinUSB connection and its bounded transfers.
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
using System.Runtime.InteropServices;

namespace ThermalrightVibeWare.Core;

/// <summary>Provides one shared, bounded panel session for the CLI and desktop application.</summary>
public sealed class PanelSession : IDisposable
{
    private readonly DisplayDevice device;
    private readonly FileStream? hid;
    private readonly WinUsbPanelTransport? winUsb;

    /// <summary>Opens the selected device using its declared HID or WinUSB transport.</summary>
    /// <param name="device">Explicitly selected discovered display descriptor; no automatic device selection occurs here.</param>
    private PanelSession(DisplayDevice device)
    {
        this.device = device;
        if (device.Transport == PanelTransportKind.WinUsb)
        {
            winUsb = WinUsbPanelTransport.Open(device);
            return;
        }

        var handle = Native.CreateFile(device.DevicePath, Native.GenericRead | Native.GenericWrite,
            Native.FileShareRead | Native.FileShareWrite, IntPtr.Zero, Native.OpenExisting,
            Native.FileFlagOverlapped, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error, "Could not open the display HID interface.");
        }

        try { hid = new FileStream(handle, FileAccess.ReadWrite, 1, isAsync: true); }
        catch { handle.Dispose(); throw; }
    }

    /// <summary>Opens only the selected native panel interface.</summary>
    /// <param name="device">Explicitly selected discovered display descriptor; no automatic device selection occurs here.</param>
    public static PanelSession Open(DisplayDevice device) => new(device);

    /// <summary>Requests device information once and validates the reply using the selected transport layout.</summary>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    public async Task<PanelProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (winUsb is not null)
        {
            return await Task.Run(winUsb.Probe, cancellationToken).ConfigureAwait(false);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var report = new byte[device.OutputReportBytes];
            Thermalright04165302Protocol.CreateDeviceInfoRequest().CopyTo(report, 1);
            await Task.Delay(50, deadline.Token).ConfigureAwait(false);
            // Windows HID includes Report ID zero; it is not part of the wire-protocol header.
            await hid!.WriteAsync(report, deadline.Token).ConfigureAwait(false);
            await Task.Delay(200, deadline.Token).ConfigureAwait(false);
            var response = new byte[device.InputReportBytes];
            var read = await hid.ReadAsync(response, deadline.Token).ConfigureAwait(false);
            if (read < 2 || response[0] != 0)
            {
                throw new IOException("The display returned an unexpected HID report ID or empty reply.");
            }

            return PanelProbeResult.FromHidResponse(response.AsSpan(1, read - 1));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The display did not finish its HID device-info exchange within three seconds.");
        }
    }

    /// <summary>Submits an already encoded and padded picture packet with a bounded transfer deadline.</summary>
    /// <param name="packet">Complete picture packet padded to 512-byte boundaries.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    public async Task<int> SendPacketAsync(byte[] packet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.Length == 0 || packet.Length % 512 != 0)
        {
            throw new ArgumentException("A picture packet must be padded to 512-byte boundaries.", nameof(packet));
        }

        if (winUsb is not null)
        {
            await Task.Run(() => winUsb.SendPacket(packet), cancellationToken).ConfigureAwait(false);
            return packet.Length;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var report = new byte[device.OutputReportBytes];
            for (var offset = 0; offset < packet.Length; offset += 512)
            {
                packet.AsMemory(offset, 512).CopyTo(report.AsMemory(1));
                await hid!.WriteAsync(report, deadline.Token).ConfigureAwait(false);
            }
            await Task.Delay(1, deadline.Token).ConfigureAwait(false);
            return packet.Length;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The display frame transfer exceeded ten seconds. Check the USB connection before retrying.");
        }
    }

    /// <summary>Closes the selected interface without changing firmware, drivers, or startup state.</summary>
    public void Dispose()
    {
        hid?.Dispose();
        winUsb?.Dispose();
    }
}
