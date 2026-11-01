/* VBWR B
 * Project: Thermalright VibeWare
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * Purpose: Reads aggregate CPU counters and physical RAM without swap or estimates.
 * Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
 * VBWR E */

using System.Globalization;

namespace ThermalrightVibeWare.Core;

public sealed partial class LinuxMetricsService
{
    private double? ReadCpuUsage(List<string> notices)
    {
        var contents = ReadFirstLine(Path.Combine(procRoot, "stat"), 2048);
        if (contents.Value is null)
        {
            previousCpu = null;
            AddNotice(notices, "Aggregate CPU counters unavailable. " + contents.Notice);
            return null;
        }
        var line = contents.Value.StartsWith("cpu ", StringComparison.Ordinal) ? contents.Value : null;
        var fields = line?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields is null || fields.Length < 5)
        {
            previousCpu = null;
            AddNotice(notices, "Aggregate CPU counters are missing or malformed.");
            return null;
        }

        ulong total = 0, idle = 0;
        for (var index = 1; index <= Math.Min(8, fields.Length - 1); index++)
        {
            if (!ulong.TryParse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || ulong.MaxValue - total < value)
            {
                previousCpu = null;
                AddNotice(notices, "Aggregate CPU counters are invalid; a new baseline is required.");
                return null;
            }
            total += value;
            if (index is 4 or 5) { idle += value; }
        }
        // guest and guest_nice (fields 9/10) are already included in user/nice.
        var current = new CpuTimes(total, idle);
        var previous = previousCpu;
        previousCpu = current;
        if (previous is not CpuTimes baseline) { return null; }
        if (current.Total <= baseline.Total || current.Idle < baseline.Idle)
        {
            AddNotice(notices, "CPU counters did not advance monotonically; usage is unavailable for this sample.");
            return null;
        }
        var totalDelta = current.Total - baseline.Total;
        var idleDelta = current.Idle - baseline.Idle;
        if (idleDelta > totalDelta)
        {
            AddNotice(notices, "CPU idle counters exceeded elapsed CPU time; usage is unavailable.");
            return null;
        }
        return 100d * (totalDelta - idleDelta) / totalDelta;
    }

    private HardwareMemorySensors? ReadMemory(List<string> notices)
    {
        var contents = ReadText(Path.Combine(procRoot, "meminfo"), 65536);
        if (contents.Value is null)
        {
            AddNotice(notices, "Physical RAM totals unavailable. " + contents.Notice);
            return null;
        }
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var line in contents.Value.Split('\n').Take(512))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 3 || fields[0] is not ("MemTotal:" or "MemAvailable:") || fields[2] != "kB") { continue; }
            if (ulong.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                // procfs kB means KiB. Swap and committed virtual memory are never included.
                values[fields[0]] = value / (1024d * 1024d);
            }
        }
        if (!values.TryGetValue("MemTotal:", out var total) || total <= 0)
        {
            AddNotice(notices, "Physical RAM MemTotal is unavailable or invalid.");
            return null;
        }
        if (!values.TryGetValue("MemAvailable:", out var available) || available > total)
        {
            const string missing = "Physical RAM usage is unavailable: a valid MemAvailable value was not reported. No MemFree or swap estimate is substituted.";
            AddNotice(notices, missing);
            return new(total, null, null, MetadataNotice: missing + " " + MemoryMetadataNotice);
        }
        var used = total - available;
        return new(total, used, 100d * used / total, MetadataNotice: MemoryMetadataNotice);
    }

    private void ReadMetadata(List<string> notices)
    {
        if (metadataRead) { return; }
        metadataRead = true;
        var cpuInfo = ReadText(Path.Combine(procRoot, "cpuinfo"), 131072);
        if (cpuInfo.Value is not null)
        {
            foreach (var line in cpuInfo.Value.Split('\n').Take(512))
            {
                var separator = line.IndexOf(':');
                if (separator <= 0 || line[..separator].Trim() is not ("model name" or "Hardware")) { continue; }
                var value = line[(separator + 1)..].Trim();
                if (value.Length > 0) { cpuName = Limit(value); break; }
            }
        }
        var vendor = ReadText(Path.Combine(sysRoot, "class", "dmi", "id", "board_vendor"));
        var model = ReadText(Path.Combine(sysRoot, "class", "dmi", "id", "board_name"));
        var name = string.Join(" ", new[] { vendor.Value, model.Value }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (name.Length > 0) { boardName = Limit(name); }
        else { AddNotice(notices, "Motherboard identity is unavailable from accessible DMI files."); }
    }

    private readonly record struct CpuTimes(ulong Total, ulong Idle);
}
