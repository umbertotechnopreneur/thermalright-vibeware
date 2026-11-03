/* VBWR B
 * Project: Thermalright VibeWare
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * Purpose: Reads bounded hwmon channels and verified DRM GPU devices with stable identities.
 * Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
 * VBWR E */

using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Core;

public sealed partial class LinuxMetricsService
{
    private List<KernelDevice> ReadHwmon(List<string> notices)
    {
        var gpus = ReadDrmDevices(notices);
        var devices = new List<KernelDevice>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deviceIds = new HashSet<string>(StringComparer.Ordinal);
        var readingCount = 0;
        var entryCount = 0;
        try
        {
            var directory = Path.Combine(sysRoot, "class", "hwmon");
            if (!Directory.Exists(directory)) { return devices; }
            var canonicalDirectory = CanonicalPath(directory);
            if (canonicalDirectory is null || !WithinSysRoot(canonicalDirectory))
            { throw new IOException("The hwmon directory resolves outside the configured sysfs root."); }
            foreach (var path in Directory.EnumerateDirectories(canonicalDirectory))
            {
                if (++entryCount > MaximumDirectoryEntries)
                {
                    AddNotice(notices, "Hwmon enumeration reached its directory limit.");
                    break;
                }
                var canonicalMonitor = CanonicalPath(path);
                if (canonicalMonitor is null || !WithinSysRoot(canonicalMonitor))
                {
                    AddNotice(notices, "A hwmon monitor outside the configured sysfs root was skipped.");
                    continue;
                }
                // A class link can be reassigned to another device during hotplug.
                // Keep reads attached to the directory we checked, even if its name is reused.
                using var monitor = OperatingSystem.IsLinux() ? MonitorDirectory.Open(canonicalMonitor) : null;
                if (monitor is not null && (monitor.IsInvalid || !monitor.IsAt(canonicalMonitor)))
                {
                    AddNotice(notices, "A hwmon monitor could not be pinned and was skipped.");
                    continue;
                }
                var monitorPath = monitor?.DescriptorPath ?? canonicalMonitor;
                var backing = ResolveBackingDevice(canonicalMonitor);
                if (backing is null)
                {
                    AddNotice(notices, "A hwmon entry lacked a stable in-root device path and was skipped.");
                    continue;
                }
                // Check physical/PCI storage identity before reading even the chip name or channels.
                if (IsExcludedDevice(backing, notices)
                    || backing != StripHwmonSuffix(canonicalMonitor) && IsExcludedDevice(canonicalMonitor, notices)) { continue; }
                var nameResult = ReadText(Path.Combine(monitorPath, "name"), monitor: monitor);
                var name = nameResult.Value;
                if (string.IsNullOrWhiteSpace(name))
                {
                    AddNotice(notices, "A hwmon source had no readable chip identity and was skipped.");
                    continue;
                }
                if (IsStorageDevice(name, backing)) { continue; }
                if (devices.Count >= MaximumDevices - 2 || readingCount >= MaximumReadings - 4)
                {
                    AddNotice(notices, "Hwmon sampling reached its included-device or reading limit.");
                    break;
                }
                if (seen.Contains(canonicalMonitor)) { continue; }
                var gpu = gpus.FirstOrDefault(item => string.Equals(item.DevicePath, backing, StringComparison.Ordinal));
                var kind = gpu is not null ? SensorKind.Gpu : ClassifyDevice(name, backing);
                var stablePath = SysRelative(backing);
                // The kernel's hwmonN enumeration number is deliberately absent from identities.
                var id = "linux:/hwmon/" + StableHash(stablePath + "\0" + name);
                if (deviceIds.Contains(id))
                {
                    AddNotice(notices, "Two hwmon entries shared the same stable device and source identity; the duplicate was skipped.");
                    continue;
                }
                var readings = ReadChannels(monitorPath, monitor, name, id, Math.Min(MaximumDeviceReadings, MaximumReadings - 4 - readingCount), notices);
                readingCount += readings.Count;
                if (gpu is not null && readings.Count < MaximumDeviceReadings && readingCount < MaximumReadings - 4 && gpu.Driver == "amdgpu")
                {
                    var usagePath = Path.Combine(backing, "gpu_busy_percent");
                    if (File.Exists(usagePath))
                    {
                        var usage = ReadNumber(usagePath, 1, nonnegative: true);
                        if (usage.Value is > 100) { usage = new(null, "GPU busy percentage is outside 0..100."); }
                        readings.Add(new(id + "/gpu_busy_percent", "GPU busy", "Load", "%", usage.Value, null, null, usage.Notice));
                        readingCount++;
                    }
                }
                if (CanonicalPath(path) != canonicalMonitor || ResolveBackingDevice(canonicalMonitor) != backing
                    || monitor is not null && !monitor.IsAt(canonicalMonitor))
                {
                    AddNotice(notices, "A hwmon device changed during sampling. Its readings were discarded.");
                    continue;
                }
                seen.Add(canonicalMonitor);
                deviceIds.Add(id);
                foreach (var reading in readings)
                {
                    if (reading.Type == "Fan" && reading.Value is double rpm) { ObserveFan(reading.Id, rpm); }
                }
                var notice = nameResult.Notice;
                if (kind == SensorKind.Unknown)
                {
                    notice = "This hwmon source has no verified CPU, GPU or motherboard classification. It remains in Sensor Debug only.";
                }
                devices.Add(new(id, backing, Limit(gpu is null ? name : gpu.Name + " (" + name + ")"), kind, readings.ToArray(), notice));
            }
        }
        catch (Exception error) when (IsReadFailure(error))
        {
            AddNotice(notices, "Hwmon enumeration unavailable: " + error.GetType().Name + ".");
        }
        return devices;
    }

    private List<SensorReadingDto> ReadChannels(string path, MonitorDirectory? monitor, string chip, string id, int maximum, List<string> notices)
    {
        var readings = new List<SensorReadingDto>();
        var entries = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                if (++entries > MaximumDirectoryEntries)
                {
                    AddNotice(notices, "A hwmon device reached its channel directory limit.");
                    break;
                }
                var filename = Path.GetFileName(file);
                if (!TryChannel(filename, out var channel)) { continue; }
                if (readings.Count >= maximum)
                {
                    AddNotice(notices, "A hwmon device reached its channel read limit.");
                    break;
                }
                var label = ReadText(Path.Combine(path, channel.Prefix + "_label"), monitor: monitor);
                var name = string.IsNullOrWhiteSpace(label.Value) ? channel.Prefix : label.Value;
                if (filename.EndsWith("_average", StringComparison.Ordinal)) { name += " (average)"; }
                name = Limit(name);
                var thermistorVoltage = (chip, channel.Prefix) switch
                {
                    ("vt1211", "temp3" or "temp4" or "temp5" or "temp6" or "temp7") => true,
                    ("vt8231", "temp2" or "temp3" or "temp4" or "temp5" or "temp6") => true,
                    ("pc87365" or "pc87366", "temp4" or "temp5" or "temp6") => true,
                    _ => false
                };
                if (thermistorVoltage) { channel = new(channel.Prefix, "Voltage", "mV", 1); }
                var value = chip is ("vt1211" or "vt8231") && channel.Prefix == "temp1"
                    ? new NumberValue(null, "This external diode needs board-specific calibration.")
                    : ReadNumber(file, channel.Scale, nonnegative: channel.Type is "Fan" or "Clock", monitor: monitor);
                if (thermistorVoltage && value.Value is not null)
                { value = value with { Notice = "Thermistor pin voltage. Temperature needs board-specific calibration." }; }
                var faultPath = Path.Combine(path, channel.Prefix + "_fault");
                if (File.Exists(faultPath))
                {
                    var fault = ReadNumber(faultPath, 1, nonnegative: true, monitor: monitor);
                    if (fault.Value is null or > 0)
                    {
                        value = new(null, fault.Value is > 0 ? "The kernel reports a sensor fault." : "The kernel fault flag is unreadable. " + fault.Notice);
                    }
                }
                var readingId = id + "/" + (filename.Length <= 128 ? filename : "attribute-" + StableHash(filename));
                readings.Add(new(readingId, name, channel.Type, channel.Unit, value.Value, null, null, value.Notice));
            }
        }
        catch (Exception error) when (IsReadFailure(error))
        {
            AddNotice(notices, "A hwmon channel list was unavailable: " + error.GetType().Name + ".");
        }
        return readings;
    }

    private List<DrmDevice> ReadDrmDevices(List<string> notices)
    {
        var result = new List<DrmDevice>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = 0;
        try
        {
            var directory = Path.Combine(sysRoot, "class", "drm");
            if (!Directory.Exists(directory)) { return result; }
            var canonicalDirectory = CanonicalPath(directory);
            if (canonicalDirectory is null || !WithinSysRoot(canonicalDirectory))
            { throw new IOException("The DRM directory resolves outside the configured sysfs root."); }
            foreach (var card in Directory.EnumerateDirectories(canonicalDirectory))
            {
                if (++entries > MaximumDirectoryEntries || result.Count >= MaximumDevices)
                {
                    AddNotice(notices, "DRM device enumeration reached its limit.");
                    break;
                }
                var name = Path.GetFileName(card);
                if (!name.StartsWith("card", StringComparison.Ordinal) || name.Length <= 4
                    || !name.AsSpan(4).ContainsOnlyDigits()) { continue; }
                var canonicalCard = CanonicalPath(card);
                if (canonicalCard is null || !WithinSysRoot(canonicalCard)) { continue; }
                var backing = CanonicalPath(Path.Combine(canonicalCard, "device"));
                if (backing is null || !WithinSysRoot(backing) || !seen.Add(backing) || IsExcludedDevice(backing, notices)) { continue; }
                var driverPath = CanonicalPath(Path.Combine(backing, "driver"));
                var driver = driverPath is null || !WithinSysRoot(driverPath) ? null : Path.GetFileName(driverPath);
                var vendor = ReadText(Path.Combine(backing, "vendor")).Value?.ToLowerInvariant();
                var model = ReadText(Path.Combine(backing, "device")).Value;
                var manufacturer = vendor switch { "0x1002" => "AMD", "0x10de" => "NVIDIA", "0x8086" => "Intel", _ => "DRM" };
                result.Add(new(backing, Limit(manufacturer + " GPU" + (string.IsNullOrEmpty(model) ? "" : " " + model)), driver));
            }
        }
        catch (Exception error) when (IsReadFailure(error))
        {
            AddNotice(notices, "DRM device metadata unavailable: " + error.GetType().Name + ".");
        }
        return result;
    }

    private string? ResolveBackingDevice(string path)
    {
        var devicePath = CanonicalPath(Path.Combine(path, "device"));
        if (devicePath is not null) { return WithinSysRoot(devicePath) ? StripHwmonSuffix(devicePath) : null; }
        if (new DirectoryInfo(Path.Combine(path, "device")).LinkTarget is not null) { return null; }
        var monitorPath = CanonicalPath(path);
        if (monitorPath is null || !WithinSysRoot(monitorPath)) { return null; }
        var stripped = StripHwmonSuffix(monitorPath);
        // A class-only hwmon number is not a stable hardware identity.
        return stripped == monitorPath || SysRelative(stripped).StartsWith("/class/", StringComparison.Ordinal) ? null : stripped;
    }

    private static string StripHwmonSuffix(string path)
    {
        var name = Path.GetFileName(path);
        var parent = Path.GetDirectoryName(path);
        if (name.StartsWith("hwmon", StringComparison.Ordinal) && name.Length > 5 && name.AsSpan(5).ContainsOnlyDigits()
            && parent is not null && Path.GetFileName(parent) == "hwmon")
        { return Path.GetDirectoryName(parent) ?? path; }
        return path;
    }

    private bool WithinSysRoot(string path) => canonicalSysRoot is not null && IsWithinRoot(path, canonicalSysRoot);

    private static bool IsWithinRoot(string path, string root) => path == root
        || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private string SysRelative(string path) => "/" + Path.GetRelativePath(canonicalSysRoot!, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string? CanonicalPath(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (root is null) { return null; }
            var current = root;
            foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                var directory = new DirectoryInfo(current);
                if (directory.LinkTarget is not null)
                {
                    current = directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? current;
                }
            }
            return Directory.Exists(current) ? Path.TrimEndingDirectorySeparator(current) : null;
        }
        catch (Exception error) when (IsReadFailure(error)) { return null; }
    }

    private static bool IsStorageDevice(string? name, string devicePath)
    {
        if (name is "nvme" or "drivetemp" or "sata" or "scsi" or "ufs") { return true; }
        var normalized = devicePath.Replace(Path.DirectorySeparatorChar, '/');
        return normalized.Split('/').Any(part => part == "block" || part.StartsWith("nvme", StringComparison.Ordinal)
            || part.StartsWith("ata", StringComparison.Ordinal) && part.Length > 3 && char.IsDigit(part[3])
            || part.StartsWith("host", StringComparison.Ordinal) && part.Length > 4 && char.IsDigit(part[4])
            || part.StartsWith("target", StringComparison.Ordinal) && part.Length > 6 && char.IsDigit(part[6]));
    }

    private bool IsExcludedDevice(string backing, List<string> notices)
    {
        if (IsStorageDevice(null, backing)) { return true; }
        string? current = backing;
        for (var depth = 0; current is not null && depth < 16 && WithinSysRoot(current); depth++, current = Path.GetDirectoryName(current))
        {
            if (!IsPciAddress(Path.GetFileName(current))) { continue; }
            var pciClass = ReadText(Path.Combine(current, "class"), 32);
            var text = pciClass.Value;
            if (text is null || text.Length != 8 || !text.StartsWith("0x", StringComparison.Ordinal)
                || !uint.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            {
                AddNotice(notices, "A PCI-backed hwmon/DRM source had no readable class; it was excluded before sensor reads.");
                return true;
            }
            // PCI base class 01 is a mass-storage controller, regardless of chip or display name.
            if ((value >> 16) == 0x01) { return true; }
        }
        return false;
    }

    private static bool IsPciAddress(string name)
    {
        if (name.Length != 12 || name[4] != ':' || name[7] != ':' || name[10] != '.') { return false; }
        for (var index = 0; index < name.Length; index++)
        {
            if (index is 4 or 7 or 10) { continue; }
            if (!char.IsAsciiHexDigit(name[index])) { return false; }
        }
        return true;
    }

    private static string StableHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static SensorKind ClassifyDevice(string name, string backing)
    {
        if (name is "coretemp" or "k10temp" or "k8temp" or "zenpower") { return SensorKind.Cpu; }
        var platform = backing.Replace(Path.DirectorySeparatorChar, '/').Contains("/devices/platform/", StringComparison.Ordinal);
        if (platform && (name.StartsWith("nct", StringComparison.Ordinal) || name.StartsWith("it8", StringComparison.Ordinal)
            || name.StartsWith("w83", StringComparison.Ordinal) || name.StartsWith("f718", StringComparison.Ordinal)))
        { return SensorKind.Motherboard; }
        return SensorKind.Unknown;
    }

    private NumberValue ReadNumber(string path, double scale, bool nonnegative, MonitorDirectory? monitor = null)
    {
        var text = ReadText(path, 128, monitor);
        if (text.Value is null) { return new(null, text.Notice); }
        if (!double.TryParse(text.Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
        { return new(null, "The kernel value is malformed or non-finite."); }
        value *= scale;
        if (!double.IsFinite(value) || nonnegative && value < 0) { return new(null, "The kernel value is invalid for this channel."); }
        return new(value, null);
    }

    private static bool TryChannel(string filename, out Channel channel)
    {
        channel = default;
        var separator = filename.IndexOf('_');
        if (separator < 1 || filename[(separator + 1)..] is not ("input" or "average")) { return false; }
        var prefix = filename[..separator];
        var index = 0;
        while (index < prefix.Length && char.IsAsciiLetter(prefix[index])) { index++; }
        if (index == 0 || index == prefix.Length || !prefix.AsSpan(index).ContainsOnlyDigits()) { return false; }
        var type = prefix[..index] switch
        {
            "temp" => new Channel(prefix, "Temperature", "°C", 0.001),
            "fan" => new Channel(prefix, "Fan", "RPM", 1),
            "in" => new Channel(prefix, "Voltage", "V", 0.001),
            "curr" => new Channel(prefix, "Current", "A", 0.001),
            "power" => new Channel(prefix, "Power", "W", 0.000001),
            "freq" => new Channel(prefix, "Clock", "MHz", 0.000001),
            _ => default
        };
        if (type.Type is null || filename.EndsWith("_average", StringComparison.Ordinal) && type.Type != "Power") { return false; }
        channel = type;
        return true;
    }

    private readonly record struct NumberValue(double? Value, string? Notice);

    // opendir owns a close-on-exec directory descriptor. Its procfs path continues
    // to refer to this directory after a different device takes the class index.
    private sealed class MonitorDirectory : SafeHandle
    {
        public MonitorDirectory() : base(IntPtr.Zero, true) { }
        public override bool IsInvalid => handle == IntPtr.Zero;
        internal string DescriptorPath => "/proc/self/fd/" + DirectoryFd(this).ToString(CultureInfo.InvariantCulture);
        internal bool IsAt(string path) => new DirectoryInfo(DescriptorPath).LinkTarget == path;
        protected override bool ReleaseHandle() => CloseDirectory(handle) == 0;

        [DllImport("libc", EntryPoint = "opendir")]
        internal static extern MonitorDirectory Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
        [DllImport("libc", EntryPoint = "dirfd")]
        private static extern int DirectoryFd(MonitorDirectory directory);
        [DllImport("libc", EntryPoint = "closedir")]
        private static extern int CloseDirectory(IntPtr directory);
    }
    private readonly record struct Channel(string Prefix, string Type, string Unit, double Scale);
    private sealed record DrmDevice(string DevicePath, string Name, string? Driver);
}

internal static class LinuxSensorPathText
{
    internal static bool ContainsOnlyDigits(this ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) { return false; }
        foreach (var character in text) { if (!char.IsAsciiDigit(character)) { return false; } }
        return true;
    }
}
