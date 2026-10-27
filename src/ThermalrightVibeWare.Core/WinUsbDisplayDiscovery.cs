/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WinUsbDisplayDiscovery.cs
 * Purpose: Enumerates supported WinUSB panel interfaces without submitting frames.
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
using Microsoft.Win32;

namespace ThermalrightVibeWare.Core;

/// <summary>Finds present WinUSB interfaces for the supported panel without any hardware-monitoring service.</summary>
public static class WinUsbDisplayDiscovery
{
    /// <summary>Returns present 0416:5302 WinUSB interfaces.</summary>
    public static IReadOnlyList<DisplayDevice> FindSupportedDevices()
    {
        var devices = new List<DisplayDevice>();
        // A registry child name is not evidence of a published device interface.
        // Enumerate the classes Windows has actually registered and filter their real paths.
        using var classes = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceClasses");
        foreach (var name in classes?.GetSubKeyNames() ?? [])
        {
            if (Guid.TryParse(name, out var interfaceClass)) { FindSupportedDevices(interfaceClass, devices); }
        }

        return devices.DistinctBy(device => device.DevicePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Finds matching panels that Windows has enumerated but may not yet expose to WinUSB applications.</summary>
    public static IReadOnlyList<DetectedPanel> FindAttachedPanels()
    {
        var deviceSet = Native.SetupDiGetClassDevsForEnumerator(IntPtr.Zero, "USB", IntPtr.Zero, Native.DigcfPresent | Native.DigcfAllClasses);
        if (deviceSet == new IntPtr(-1))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate attached USB devices.");
        }

        try
        {
            var panels = new List<DetectedPanel>();
            for (uint index = 0; ; index++)
            {
                var deviceInfo = new Native.SpDevinfoData { cbSize = Marshal.SizeOf<Native.SpDevinfoData>() };
                if (!Native.SetupDiEnumDeviceInfo(deviceSet, index, ref deviceInfo))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == Native.ErrorNoMoreItems)
                    {
                        break;
                    }

                    throw new Win32Exception(error, "USB device enumeration failed.");
                }

                var instanceId = new System.Text.StringBuilder(512);
                if (Native.SetupDiGetDeviceInstanceId(deviceSet, ref deviceInfo, instanceId, instanceId.Capacity, out _)
                    && instanceId.ToString().Contains("USB\\VID_0416&PID_5302&MI_01", StringComparison.OrdinalIgnoreCase))
                {
                    panels.Add(new DetectedPanel(instanceId.ToString()));
                }
            }

            return panels;
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(deviceSet);
        }
    }

    /// <summary>Enumerates supported present USB interfaces without sending frames.</summary>
    /// <param name="interfaceClass">Windows interface class GUID to enumerate.</param>
    /// <param name="devices">Destination collection to which supported device descriptors are appended.</param>
    private static void FindSupportedDevices(Guid interfaceClass, ICollection<DisplayDevice> devices)
    {
        var deviceSet = Native.SetupDiGetClassDevs(ref interfaceClass, IntPtr.Zero, IntPtr.Zero, Native.DigcfPresent | Native.DigcfDeviceInterface);
        if (deviceSet == new IntPtr(-1))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate USB device interfaces.");
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var interfaceData = new Native.SpDeviceInterfaceData { cbSize = Marshal.SizeOf<Native.SpDeviceInterfaceData>() };
                if (!Native.SetupDiEnumDeviceInterfaces(deviceSet, IntPtr.Zero, ref interfaceClass, index, ref interfaceData))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == Native.ErrorNoMoreItems)
                    {
                        break;
                    }

                    throw new Win32Exception(error, "Could not read a USB device interface.");
                }

                Native.SetupDiGetDeviceInterfaceDetail(deviceSet, ref interfaceData, IntPtr.Zero, 0, out var requiredBytes, IntPtr.Zero);
                if (Marshal.GetLastWin32Error() != Native.ErrorInsufficientBuffer || requiredBytes == 0)
                {
                    continue;
                }

                var detailBuffer = Marshal.AllocHGlobal(checked((int)requiredBytes));
                try
                {
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!Native.SetupDiGetDeviceInterfaceDetail(deviceSet, ref interfaceData, detailBuffer, requiredBytes, out _, IntPtr.Zero))
                    {
                        continue;
                    }

                    var path = Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, sizeof(int)));
                    if (path is not null && path.Contains("vid_0416&pid_5302&mi_01", StringComparison.OrdinalIgnoreCase))
                    {
                        devices.Add(new DisplayDevice(path, Thermalright04165302Protocol.VendorId, Thermalright04165302Protocol.ProductId));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuffer);
                }
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(deviceSet);
        }

    }
}
