/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WinUsbPanelTransport.cs
 * Purpose: Owns one WinUSB interface and performs bounded panel protocol transfers.
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

using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ThermalrightVibeWare.Core;

/// <summary>Owns a short-lived WinUSB session for a selected display panel.</summary>
public sealed class WinUsbPanelTransport : IDisposable
{
    private readonly SafeFileHandle deviceHandle;
    private readonly IntPtr interfaceHandle;
    private bool disposed;

    /// <summary>Takes ownership of the opened device and WinUSB interface handles.</summary>
    /// <param name="deviceHandle">Open native device handle.</param>
    /// <param name="interfaceHandle">WinUSB interface handle returned by initialization.</param>
    private WinUsbPanelTransport(SafeFileHandle deviceHandle, IntPtr interfaceHandle)
    {
        this.deviceHandle = deviceHandle;
        this.interfaceHandle = interfaceHandle;
    }

    /// <summary>Opens the WinUSB interface exposed by a discovered panel.</summary>
    /// <param name="device">Explicitly selected discovered display descriptor; no automatic device selection occurs here.</param>
    public static WinUsbPanelTransport Open(DisplayDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        var fileHandle = Native.CreateFile(device.DevicePath, Native.GenericRead | Native.GenericWrite, Native.FileShareRead | Native.FileShareWrite, IntPtr.Zero, Native.OpenExisting, Native.FileFlagOverlapped, IntPtr.Zero);
        if (fileHandle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            fileHandle.Dispose();
            throw new Win32Exception(error, "Could not open the panel WinUSB interface.");
        }

        if (!Native.WinUsb_Initialize(fileHandle, out var winUsbHandle))
        {
            var error = Marshal.GetLastWin32Error();
            fileHandle.Dispose();
            throw new Win32Exception(error, "Could not initialize the panel WinUSB session.");
        }

        var transport = new WinUsbPanelTransport(fileHandle, winUsbHandle);
        try
        {
            transport.ValidatePipes();
            return transport;
        }
        catch { transport.Dispose(); throw; }
    }

    /// <summary>Sends only the documented device-information handshake and returns the decoded reply.</summary>
    public PanelProbeResult Probe()
    {
        ThrowIfDisposed();
        var request = Thermalright04165302Protocol.CreateDeviceInfoRequest();
        WriteExact(request);
        var response = ReadExact(Thermalright04165302Protocol.TransferSize);
        return PanelProbeResult.FromResponse(response);
    }

    /// <summary>Sends an explicitly requested diagnostic RGB565 frame using the selected profile.</summary>
    /// <param name="packet">Complete picture packet padded to 512-byte boundaries.</param>
    public void SendPacket(byte[] packet)
    {
        ThrowIfDisposed();
        WriteExact(packet);
    }

    /// <summary>Verifies that the selected interface exposes the required endpoint layout.</summary>
    private void ValidatePipes()
    {
        var readFound = false;
        var writeFound = false;
        for (byte index = 0; index < 32; index++)
        {
            if (!Native.WinUsb_QueryPipe(interfaceHandle, 0, index, out var pipe))
            {
                var error = Marshal.GetLastWin32Error();
                if (error == 259) { break; }
                throw new Win32Exception(error, "Could not inspect the WinUSB endpoints.");
            }

            if (pipe.PipeType != 2) { continue; }
            readFound |= pipe.PipeId == Thermalright04165302Protocol.ReadEndpoint;
            writeFound |= pipe.PipeId == Thermalright04165302Protocol.WriteEndpoint;
        }

        if (!readFound || !writeFound)
        {
            throw new IOException("This WinUSB interface does not expose the expected bulk endpoints 0x02/0x81. Use its HID interface instead.");
        }

        foreach (var endpoint in new[] { Thermalright04165302Protocol.ReadEndpoint, Thermalright04165302Protocol.WriteEndpoint })
        {
            uint timeoutMs = 3000;
            if (!Native.WinUsb_SetPipePolicy(interfaceHandle, endpoint, 3, sizeof(uint), ref timeoutMs))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not configure the WinUSB transfer timeout.");
            }
        }
    }

    /// <summary>Closes the underlying WinUSB and device handles.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Native.WinUsb_Free(interfaceHandle);
        deviceHandle.Dispose();
        disposed = true;
    }

    /// <summary>Writes the full buffer or reports a short USB transfer.</summary>
    /// <param name="buffer">Bytes to read or write, bounded by the supplied size when present.</param>
    private void WriteExact(byte[] buffer)
    {
        if (!Native.WinUsb_WritePipe(interfaceHandle, Thermalright04165302Protocol.WriteEndpoint, buffer, checked((uint)buffer.Length), out var written, IntPtr.Zero) || written != checked((uint)buffer.Length))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Panel write failed after {written} of {buffer.Length} bytes.");
        }
    }

    /// <summary>Reads exactly the requested reply length or reports a short transfer.</summary>
    /// <param name="expectedLength">Exact number of response bytes required by this protocol exchange.</param>
    private byte[] ReadExact(int expectedLength)
    {
        var response = new byte[expectedLength];
        if (!Native.WinUsb_ReadPipe(interfaceHandle, Thermalright04165302Protocol.ReadEndpoint, response, checked((uint)response.Length), out var read, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Panel read failed.");
        }

        return read == expectedLength ? response : response[..checked((int)read)];
    }

    /// <summary>Rejects use of a closed USB transport.</summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
