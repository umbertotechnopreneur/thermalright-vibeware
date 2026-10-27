/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: DisplayDiscovery.cs
 * Purpose: Enumerates supported present panel interfaces and HID capabilities.
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

/// <summary>Enumerates supported native HID and WinUSB paths without sending panel commands.</summary>
public static class DisplayDiscovery
{
    /// <summary>Lists validated HID interfaces first, followed by exposed WinUSB interfaces.</summary>
    public static IReadOnlyList<DisplayDevice> FindDevices()
    {
        Native.HidD_GetHidGuid(out var guid);
        var devices = InterfacePaths(guid)
            .Where(path => path.Contains("vid_0416&pid_5302&mi_00", StringComparison.OrdinalIgnoreCase))
            .Select(ReadPresentHidCapabilities).Where(device => device is not null).Cast<DisplayDevice>().ToList();
        // HID is usable without configuring the optional vendor-specific sibling.
        if (devices.Count == 0)
        {
            devices.AddRange(WinUsbDisplayDiscovery.FindSupportedDevices());
        }

        return devices.OrderBy(device => device.DevicePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Enumerates present interface paths for the supplied Windows class GUID.</summary>
    /// <param name="interfaceClass">Windows interface class GUID to enumerate.</param>
    internal static IEnumerable<string> InterfacePaths(Guid interfaceClass)
    {
        var set = Native.SetupDiGetClassDevs(ref interfaceClass, IntPtr.Zero, IntPtr.Zero,
            Native.DigcfPresent | Native.DigcfDeviceInterface);
        if (set == new IntPtr(-1))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "USB interface enumeration failed.");
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new Native.SpDeviceInterfaceData { cbSize = Marshal.SizeOf<Native.SpDeviceInterfaceData>() };
                if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref interfaceClass, index, ref data))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == Native.ErrorNoMoreItems) { yield break; }
                    throw new Win32Exception(error, "USB interface enumeration failed.");
                }

                Native.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out var size, IntPtr.Zero);
                if (Marshal.GetLastWin32Error() != Native.ErrorInsufficientBuffer)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "USB interface size query failed.");
                }

                var buffer = Marshal.AllocHGlobal(checked((int)size));
                try
                {
                    Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref data, buffer, size, out _, IntPtr.Zero))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "USB interface path query failed.");
                    }

                    var path = Marshal.PtrToStringUni(buffer + sizeof(int));
                    if (!string.IsNullOrEmpty(path)) { yield return path; }
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
        }
        finally { Native.SetupDiDestroyDeviceInfoList(set); }
    }

    /// <summary>Reads capabilities only while the device interface is still present.</summary>
    /// <param name="path">Literal Windows device-interface path to inspect.</param>
    private static DisplayDevice? ReadPresentHidCapabilities(string path)
    {
        try { return ReadHidCapabilities(path); }
        // Unplugging between SetupDi enumeration and descriptor lookup is normal.
        catch (Win32Exception error) when (error.NativeErrorCode is 2 or 6 or 31 or 1167) { return null; }
    }

    /// <summary>Reads HID capability metadata and filters unsupported report layouts.</summary>
    /// <param name="path">Literal Windows device-interface path to inspect.</param>
    private static DisplayDevice? ReadHidCapabilities(string path)
    {
        // Zero desired access queries descriptors without opening a read/write session.
        using var handle = Native.CreateFile(path, 0, Native.FileShareRead | Native.FileShareWrite,
            IntPtr.Zero, Native.OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid || !Native.HidD_GetPreparsedData(handle, out var preparsed))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read the display HID descriptor.");
        }

        try
        {
            var status = Native.HidP_GetCaps(preparsed, out var caps);
            if (status != 0x00110000) { throw new IOException($"HID capabilities failed: 0x{status:X8}."); }
            if (caps.UsagePage != 0xFF06 || caps.OutputReportByteLength != 513 || caps.InputReportByteLength < 9)
            {
                return null;
            }

            return new DisplayDevice(path, 0x0416, 0x5302, PanelTransportKind.Hid,
                caps.InputReportByteLength, caps.OutputReportByteLength);
        }
        finally { Native.HidD_FreePreparsedData(preparsed); }
    }
}
