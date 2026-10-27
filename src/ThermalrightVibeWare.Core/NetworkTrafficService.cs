/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: NetworkTrafficService.cs
 * Purpose: Samples verified physical network counters for session traffic totals.
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

using System.Diagnostics;
using System.Net.NetworkInformation;

namespace ThermalrightVibeWare.Core;

/// <summary>Reads OS counters, not packets. Totals start with this service and may overlap on VPN/bridge adapters.</summary>
public sealed class NetworkTrafficService : INetworkProvider
{
    private readonly Dictionary<string, (long Received, long Sent)> previous = [];
    private long lastRead, receivedTotal, sentTotal;

    /// <summary>Samples verified physical network counters and calculates session deltas.</summary>
    public NetworkTraffic Read()
    {
        var now = Stopwatch.GetTimestamp();
        long received = 0, sent = 0;
        var present = new HashSet<string>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(item => item.OperationalStatus == OperationalStatus.Up
            && item.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)))
        {
            try
            {
                var counters = adapter.GetIPStatistics();
                present.Add(adapter.Id);
                if (previous.TryGetValue(adapter.Id, out var before))
                {
                    received += Math.Max(0, counters.BytesReceived - before.Received);
                    sent += Math.Max(0, counters.BytesSent - before.Sent);
                }
                previous[adapter.Id] = (counters.BytesReceived, counters.BytesSent);
            }
            catch (NetworkInformationException) { }
        }
        foreach (var missing in previous.Keys.Where(key => !present.Contains(key)).ToArray()) { previous.Remove(missing); }
        receivedTotal += received;
        sentTotal += sent;
        var seconds = lastRead == 0 ? 0 : Stopwatch.GetElapsedTime(lastRead, now).TotalSeconds;
        lastRead = now;
        return new(seconds > 0 ? received / seconds : null, seconds > 0 ? sent / seconds : null, receivedTotal, sentTotal);
    }

    /// <summary>Formats an available byte count using binary size units.</summary>
    /// <param name="bytes">Available byte count or byte rate to format; null means unavailable.</param>
    public static string Size(double? bytes)
    {
        if (bytes is null) { return "—"; }
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var value = bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:F1} {units[unit]}";
    }
}
