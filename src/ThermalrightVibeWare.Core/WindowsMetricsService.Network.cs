/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsMetricsService.Network.cs
 * Purpose: Filters network sensor access to verified physical Ethernet and Wi-Fi interfaces.
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

using System.Globalization;
using System.Management;
using Lhm = LibreHardwareMonitor.Hardware;

namespace ThermalrightVibeWare.Core;

public sealed partial class WindowsMetricsService
{
    private readonly HashSet<Guid> physicalNetworkAdapters = [];
    private bool networkMetadataRead;
    private string? networkMetadataNotice;

    /// <summary>Allows only configured hardware groups and verified physical network interfaces.</summary>
    /// <param name="hardware">Raw hardware device from the configured LibreHardwareMonitor provider.</param>
    private bool IncludeHardware(Lhm.IHardware hardware)
    {
        if (hardware.HardwareType == Lhm.HardwareType.Storage) { return false; }
        if (hardware.HardwareType != Lhm.HardwareType.Network) { return true; }
        ReadNetworkConfiguration();
        // LHM 0.9.6 uses /nic/<NetworkInterface.Id>; match the Windows GUID, never the display name.
        var identifier = hardware.Identifier.ToString();
        if (!identifier.StartsWith("/nic/", StringComparison.Ordinal)
            || !Guid.TryParse(identifier.AsSpan(5), out var adapterId))
        {
            networkMetadataNotice ??= "Some network entries could not be matched to physical adapter IDs and were hidden.";
            return false;
        }
        return physicalNetworkAdapters.Contains(adapterId);
    }

    // Adapter identity is cached for this reader's lifetime, with no extra per-frame query or link-state requirement.
    /// <summary>Caches verified physical Ethernet and Wi-Fi adapter identities.</summary>
    private void ReadNetworkConfiguration()
    {
        if (networkMetadataRead) { return; }
        networkMetadataRead = true;
        try
        {
            using var query = new ManagementObjectSearcher(new ManagementScope(@"root\StandardCimv2"),
                new ObjectQuery("SELECT InterfaceGuid, HardwareInterface, Virtual, Hidden, IMFilter, EndPointInterface, InterfaceType, NdisPhysicalMedium, PnPDeviceID FROM MSFT_NetAdapter"),
                new System.Management.EnumerationOptions { ReturnImmediately = false, Rewindable = false, Timeout = TimeSpan.FromSeconds(3) });
            using var results = query.Get();
            var count = 0;
            foreach (ManagementObject adapter in results)
            {
                using (adapter)
                {
                    if (++count > 512)
                    {
                        networkMetadataNotice = "Network adapter metadata reached its limit. Unidentified network entries were hidden.";
                        break;
                    }
                    if (adapter["HardwareInterface"] is not bool isHardware)
                    {
                        networkMetadataNotice ??= "Some network adapters lacked physical-device metadata and were hidden.";
                        continue;
                    }
                    if (!isHardware) { continue; }
                    if (adapter["Virtual"] is not bool isVirtual || adapter["Hidden"] is not bool isHidden
                        || adapter["IMFilter"] is not bool isFilter || adapter["EndPointInterface"] is not bool isEndpoint)
                    {
                        networkMetadataNotice ??= "Some network adapters lacked physical-device metadata and were hidden.";
                        continue;
                    }
                    if (isVirtual || isHidden || isFilter || isEndpoint) { continue; }

                    if (adapter["InterfaceType"] is null || adapter["NdisPhysicalMedium"] is null)
                    {
                        networkMetadataNotice ??= "Some network adapters lacked physical-device metadata and were hidden.";
                        continue;
                    }
                    var interfaceType = Convert.ToUInt32(adapter["InterfaceType"], CultureInfo.InvariantCulture);
                    var medium = Convert.ToUInt32(adapter["NdisPhysicalMedium"], CultureInfo.InvariantCulture);
                    // NDIS can leave the physical medium unspecified (0) for real Ethernet and Wi-Fi drivers.
                    var ethernet = interfaceType == 6 && medium is 0 or 14;
                    var wifi = interfaceType == 71 && medium is 0 or 1 or 9;
                    if (!ethernet && !wifi) { continue; }

                    var deviceId = adapter["PnPDeviceID"]?.ToString();
                    if (string.IsNullOrWhiteSpace(deviceId))
                    {
                        networkMetadataNotice ??= "Some network adapters lacked physical-device metadata and were hidden.";
                        continue;
                    }
                    // Root/software/virtual-bus devices and Bluetooth PAN are not physical Ethernet or Wi-Fi ports.
                    if (deviceId.StartsWith(@"ROOT\", StringComparison.OrdinalIgnoreCase)
                        || deviceId.StartsWith(@"SWD\", StringComparison.OrdinalIgnoreCase)
                        || deviceId.StartsWith(@"VMBUS\", StringComparison.OrdinalIgnoreCase)
                        || deviceId.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) { continue; }

                    if (Guid.TryParse(adapter["InterfaceGuid"]?.ToString(), out var adapterId))
                    { physicalNetworkAdapters.Add(adapterId); }
                    else
                    { networkMetadataNotice ??= "Some physical network adapters lacked a valid interface ID and were hidden."; }
                }
            }
        }
        catch (Exception error) when (IsSensorFailure(error))
        {
            physicalNetworkAdapters.Clear();
            networkMetadataNotice = "Physical network adapter metadata is unavailable (" + error.GetType().Name + "). Network entries were hidden.";
        }
    }
}
