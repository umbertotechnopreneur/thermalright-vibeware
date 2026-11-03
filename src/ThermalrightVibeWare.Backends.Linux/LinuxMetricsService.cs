/* VBWR B
 * Project: Thermalright VibeWare
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * Purpose: Samples existing Linux kernel interfaces on explicit requests only.
 * Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
 * VBWR E */

using System.Diagnostics;
using System.Runtime.InteropServices;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Core;

/// <summary>
/// Reads existing procfs/sysfs files. Construction performs no I/O and starts no worker.
/// No driver, kernel module, SMBus probe, process, network request or writable handle is used.
/// </summary>
public sealed partial class LinuxMetricsService : ISensorProvider
{
    private const int MaximumDevices = 64;
    private const int MaximumDirectoryEntries = 512;
    // Including procfs rows, keep worst-case escaped JSON below the 2 MB response contract.
    private const int MaximumReadings = 1024;
    private const int MaximumDeviceReadings = 256;
    private const int MaximumNotices = 64;
    private const string MemoryMetadataNotice = "DIMM identity, physical slot and SPD data are unavailable from this Linux reader. No SMBus probe or privileged helper was started.";
    private const string KernelNotice = "Linux reads existing kernel files with the current user's permissions. No sensor driver is installed or changed; PawnIO is not used.";
    private readonly object sync = new();
    private readonly string procRoot;
    private readonly string sysRoot;
    private readonly bool allowHardwareAccess;
    private readonly bool fixtureRoots;
    private readonly Func<long> timestamp;
    private readonly Dictionary<string, LinkedListNode<string>> activeFans = new(StringComparer.Ordinal);
    private readonly LinkedList<string> activeFanOrder = new();
    private HardwareSensorSnapshot snapshot = HardwareSensorSnapshot.Empty;
    private HardwareDriverStatus driverStatus = new(false, null, "Linux sensor access has not been sampled. " + KernelNotice);
    private SensorDebugDto? sensorDebug;
    private long lastRead;
    private bool hasRead, disposed, metadataRead;
    private string cpuName = "CPU", boardName = "Motherboard";
    private CpuTimes? previousCpu;
    private string? canonicalSysRoot;
    private string? canonicalProcRoot;

    /// <summary>Creates an idle reader. Explicit reads require hardware access to be enabled.</summary>
    public LinuxMetricsService(bool allowHardwareAccess = false)
        : this("/proc", "/sys", allowHardwareAccess, null, false) { }

    /// <summary>Creates an idle reader for offline fixtures; roots and clock are not accessed in this constructor.</summary>
    internal LinuxMetricsService(string procRoot, string sysRoot, bool allowHardwareAccess = false, Func<long>? timestamp = null)
        : this(procRoot, sysRoot, allowHardwareAccess, timestamp, true) { }

    private LinuxMetricsService(string procRoot, string sysRoot, bool allowHardwareAccess, Func<long>? timestamp, bool fixtureRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(procRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sysRoot);
        if (!Path.IsPathFullyQualified(procRoot) || !Path.IsPathFullyQualified(sysRoot))
        { throw new ArgumentException("Linux procfs and sysfs roots must be absolute paths."); }
        this.procRoot = Path.GetFullPath(procRoot);
        this.sysRoot = Path.GetFullPath(sysRoot);
        this.allowHardwareAccess = allowHardwareAccess;
        this.timestamp = timestamp ?? Stopwatch.GetTimestamp;
        this.fixtureRoots = fixtureRoots;
    }

    /// <summary>Details from the latest explicit sample, never from construction.</summary>
    public string? InitializationFailureDetails { get; private set; }

    /// <summary>Returns cached kernel-access metadata without sampling sensors.</summary>
    public HardwareDriverStatus DriverStatus { get { lock (sync) { return driverStatus; } } }

    /// <summary>Returns cached metadata only; this method opens no procfs, sysfs or device files.</summary>
    public HardwareDriverStatus ReadDriverStatus() => DriverStatus;

    /// <summary>Returns a shared sample, refreshing at most once per second and never in the background.</summary>
    public HardwareSensorSnapshot ReadSensors()
    {
        lock (sync)
        {
            EnsureSample();
            return snapshot;
        }
    }

    /// <summary>Reads aggregate metrics from the same cached sample as themes and Sensor Debug.</summary>
    public MachineMetrics Read()
    {
        var sensors = ReadSensors();
        var gpu = sensors.Gpus.FirstOrDefault();
        return new(sensors.Cpu?.UsagePercent, sensors.Cpu?.TemperatureC, gpu?.UsagePercent, gpu?.TemperatureC,
            gpu?.Name ?? "GPU", sensors.Cpu?.TemperatureC is null ? "Unavailable" : "Linux hwmon: " + sensors.Cpu.TemperatureLabel,
            gpu is null ? "Unavailable" : "Linux DRM and hwmon (first detected GPU)");
    }

    /// <summary>Returns unfiltered raw channels, including stopped fans and unavailable readings.</summary>
    public SensorDebugDto ReadSensorDebug()
    {
        lock (sync)
        {
            EnsureSample();
            return sensorDebug ?? new("Linux kernel procfs/sysfs", DateTimeOffset.UtcNow, false, [], ["Sensor reader is closed."]);
        }
    }

    private void EnsureSample()
    {
        if (disposed) { return; }
        var now = timestamp();
        if (hasRead && now >= lastRead && Stopwatch.GetElapsedTime(lastRead, now) < TimeSpan.FromSeconds(1)) { return; }
        var notices = new List<string>();
        var sampledAt = DateTimeOffset.UtcNow;
        snapshot = HardwareSensorSnapshot.Empty;
        if (!allowHardwareAccess || (!fixtureRoots && !OperatingSystem.IsLinux()))
        {
            var notice = !allowHardwareAccess ? "Linux hardware reads are disabled: local hardware access has not been authorized."
                : "The Linux sensor provider requires Linux.";
            InitializationFailureDetails = notice;
            driverStatus = new(false, null, notice);
            snapshot = HardwareSensorSnapshot.Empty with { Notice = notice, Driver = driverStatus };
            sensorDebug = new("Linux kernel procfs/sysfs", sampledAt, false, [], [notice]);
            hasRead = true;
            lastRead = timestamp();
            return;
        }

        // Invalid roots are configuration failures, not ordinary missing sensor values.
        // Do not replace them with host paths or return a successful empty cached sample.
        canonicalProcRoot = CanonicalPath(procRoot);
        canonicalSysRoot = CanonicalPath(sysRoot);
        if (canonicalProcRoot is null || canonicalSysRoot is null)
        {
            const string notice = "The configured Linux procfs or sysfs root is missing or inaccessible. No alternate root was used.";
            InitializationFailureDetails = notice;
            driverStatus = new(false, null, notice);
            throw new DirectoryNotFoundException(notice);
        }
        hasRead = true;

        try
        {
            ReadMetadata(notices);
            var cpuUsage = ReadCpuUsage(notices);
            var memory = ReadMemory(notices);
            var devices = ReadHwmon(notices);
            var cpuDevices = devices.Where(device => device.Kind == SensorKind.Cpu).ToArray();
            var cpuReadings = ThemeReadings(cpuDevices).Where(reading => !IsIndividualCpuReading(reading.Name))
                .Take(MaximumDeviceReadings).ToArray();
            var cpuTemperature = SelectCpuTemperature(cpuReadings);
            var cpuClock = cpuReadings.FirstOrDefault(reading => reading.Type == "Clock"
                && (reading.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                    || reading.Name.Contains("Average", StringComparison.OrdinalIgnoreCase)
                    || reading.Name.Equals("CPU", StringComparison.OrdinalIgnoreCase)));
            HardwareDeviceSensors? cpu = cpuUsage is not null || cpuDevices.Length > 0 || previousCpu is not null
                ? new(cpuName, cpuUsage, cpuClock?.Value, cpuTemperature?.Value, ClockLabel: cpuClock?.Name, TemperatureLabel: cpuTemperature?.Name,
                    Fans: ThemeFans(cpuReadings), Readings: cpuReadings) : null;
            var gpuDevices = devices.Where(device => device.Kind == SensorKind.Gpu).GroupBy(device => device.DevicePath, StringComparer.Ordinal);
            var gpus = new List<HardwareDeviceSensors>();
            foreach (var group in gpuDevices.Take(8))
            {
                var readings = ThemeReadings(group).Take(MaximumDeviceReadings).ToArray();
                var primary = SelectTemperature(readings);
                var clock = readings.FirstOrDefault(reading => reading.Type == "Clock");
                gpus.Add(new(group.First().Name, readings.FirstOrDefault(reading => reading.Type == "Load")?.Value,
                    clock?.Value, primary?.Value, ClockLabel: clock?.Name, TemperatureLabel: primary?.Name, Fans: ThemeFans(readings), Readings: readings));
            }
            var boardDevices = devices.Where(device => device.Kind == SensorKind.Motherboard).ToArray();
            var boardReadings = ThemeReadings(boardDevices).Take(MaximumDeviceReadings).ToArray();
            var boardTemperatures = boardReadings.Where(reading => reading.Type == "Temperature" && reading.Value is not null)
                .Select(reading => new HardwareTemperatureSensors(reading.Name, reading.Value!.Value)).ToArray();
            var board = boardDevices.Length == 0 ? null : new HardwareMotherboardSensors(boardName,
                boardTemperatures.FirstOrDefault()?.TemperatureC, boardTemperatures.FirstOrDefault()?.Name,
                ThemeFans(boardReadings), boardTemperatures, boardReadings);
            if (devices.Count == 0) { AddNotice(notices, "No supported non-storage hwmon devices were available. CPU load and RAM totals may still be available."); }
            AddNotice(notices, MemoryMetadataNotice);
            AddNotice(notices, KernelNotice);
            driverStatus = new(devices.Any(device => device.Readings.Any(reading => reading.Value is not null)), null, KernelNotice);
            InitializationFailureDetails = previousCpu is null && memory is null
                && !devices.Any(device => device.Readings.Any(reading => reading.Value is not null))
                ? "No Linux CPU counters, physical RAM totals or non-storage hwmon devices could be read. " + string.Join(" ", notices)
                : null;
            snapshot = new(cpu, gpus, memory, sampledAt, string.Join(" ", notices), board, driverStatus);
            var debugDevices = devices.Select(device => new HardwareDeviceDto(device.Id, null, device.Name,
                device.Kind switch { SensorKind.Cpu => "Cpu", SensorKind.Gpu => "Gpu", SensorKind.Motherboard => "SuperIO", _ => "Unknown" },
                device.Readings, device.Notice)).ToList();
            AddProcDebug(debugDevices, cpuUsage, memory);
            sensorDebug = new("Linux kernel procfs/sysfs", sampledAt, IsRoot(), debugDevices, notices.ToArray());
        }
        catch (Exception error) when (IsReadFailure(error))
        {
            var notice = "Linux sensor sample unavailable: " + error.GetType().Name + ". Previous values were cleared.";
            InitializationFailureDetails = notice;
            driverStatus = new(false, null, KernelNotice);
            snapshot = HardwareSensorSnapshot.Empty with { Notice = notice, Driver = driverStatus };
            sensorDebug = new("Linux kernel procfs/sysfs", sampledAt, IsRoot(), [], [notice, KernelNotice]);
        }
        lastRead = timestamp();
    }

    private IEnumerable<HardwareReading> ThemeReadings(IEnumerable<KernelDevice> devices)
    {
        foreach (var reading in devices.SelectMany(device => device.Readings))
        {
            if (reading.Value is not double value) { continue; }
            if (reading.Type == "Fan" && !activeFans.ContainsKey(reading.Id)) { continue; }
            yield return new(reading.Id, reading.Name, reading.Type, value, reading.Unit);
        }
    }

    private void ObserveFan(string id, double rpm)
    {
        if (activeFans.TryGetValue(id, out var existing))
        {
            activeFanOrder.Remove(existing);
            activeFanOrder.AddLast(existing);
            return;
        }
        if (rpm <= 0) { return; }
        activeFans.Add(id, activeFanOrder.AddLast(id));
        if (activeFans.Count > MaximumReadings * 2)
        {
            activeFans.Remove(activeFanOrder.First!.Value);
            activeFanOrder.RemoveFirst();
        }
    }

    private static HardwareFanSensors[] ThemeFans(IEnumerable<HardwareReading> readings) => readings
        .Where(reading => reading.Type == "Fan" && reading.Value is not null)
        .Take(16).Select(reading => new HardwareFanSensors(reading.Name, reading.Value!.Value)).ToArray();

    private static HardwareReading? SelectCpuTemperature(IEnumerable<HardwareReading> readings) => readings
        .Where(reading => reading.Type == "Temperature" && reading.Value is not null)
        .OrderBy(reading => reading.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ? 0
            : reading.Name.Equals("Tdie", StringComparison.OrdinalIgnoreCase) ? 1
            : reading.Name.Equals("Tctl", StringComparison.OrdinalIgnoreCase) ? 2 : 3)
        .FirstOrDefault(reading => !reading.Name.StartsWith("Core ", StringComparison.OrdinalIgnoreCase));

    private static HardwareReading? SelectTemperature(IEnumerable<HardwareReading> readings) => readings
        .FirstOrDefault(reading => reading.Type == "Temperature" && reading.Value is not null);

    private static bool IsIndividualCpuReading(string name)
    {
        foreach (var prefix in new[] { "Core", "Thread", "CPU Core", "CPU Thread" })
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { continue; }
            var suffix = name.AsSpan(prefix.Length).TrimStart();
            if (!suffix.IsEmpty && suffix[0] == '#') { suffix = suffix[1..].TrimStart(); }
            if (!suffix.IsEmpty && char.IsAsciiDigit(suffix[0])) { return true; }
        }
        return false;
    }

    private static void AddProcDebug(List<HardwareDeviceDto> devices, double? cpuUsage, HardwareMemorySensors? memory)
    {
        devices.Insert(0, new("linux:proc/stat", null, "Aggregate CPU usage", "Cpu", [
            new("linux:proc/stat/cpu", "CPU Total", "Load", "%", cpuUsage, null, null,
                cpuUsage is null ? "CPU usage needs two valid monotonic aggregate samples." : null)]));
        if (memory is not null)
        {
            devices.Add(new("linux:proc/meminfo", null, "Physical memory", "Memory", [
                new("linux:proc/meminfo/total", "Physical memory total", "Data", "GiB", memory.TotalGiB, null, null),
                new("linux:proc/meminfo/used", "Physical memory used", "Data", "GiB", memory.UsedGiB, null, null),
                new("linux:proc/meminfo/load", "Physical memory used", "Load", "%", memory.UsagePercent, null, null)], memory.MetadataNotice));
        }
    }

    private TextValue ReadText(string path, int maximumCharacters = 256, MonitorDirectory? monitor = null)
    {
        try
        {
            using var stream = new FileStream(SafeAttributePath(path, monitor), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var buffer = new char[maximumCharacters + 1];
            var count = reader.ReadBlock(buffer, 0, buffer.Length);
            return count > maximumCharacters ? new(null, "File exceeds the read limit.") : new(new string(buffer, 0, count).Trim(), null);
        }
        catch (Exception error) when (IsReadFailure(error)) { return new(null, "Read unavailable: " + error.GetType().Name + "."); }
    }

    private TextValue ReadFirstLine(string path, int maximumCharacters)
    {
        try
        {
            using var stream = new FileStream(SafeAttributePath(path), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var buffer = new char[maximumCharacters];
            var count = 0;
            while (true)
            {
                var character = reader.Read();
                if (character < 0 || character == '\n') { break; }
                if (count == maximumCharacters) { return new(null, "Line exceeds the read limit."); }
                buffer[count++] = (char)character;
            }
            return new(new string(buffer, 0, count).Trim(), null);
        }
        catch (Exception error) when (IsReadFailure(error)) { return new(null, "Read unavailable: " + error.GetType().Name + "."); }
    }

    private string SafeAttributePath(string path, MonitorDirectory? monitor = null)
    {
        var fullPath = Path.GetFullPath(path);
        // A pinned monitor supplies only direct child names. Resolving its fd link
        // back into a pathname here would lose the hotplug protection.
        var attributePath = fullPath;
        var parent = Path.GetDirectoryName(fullPath);
        if (monitor is not null)
        {
            if (parent != monitor.DescriptorPath) { throw new IOException("The kernel attribute is outside the pinned hwmon directory."); }
        }
        else
        {
            var canonicalParent = parent is null ? null : CanonicalPath(parent);
            if (canonicalParent is null || !(WithinSysRoot(canonicalParent)
                || canonicalProcRoot is not null && IsWithinRoot(canonicalParent, canonicalProcRoot)))
            { throw new IOException("The kernel attribute is outside the configured roots or its parent is unavailable."); }
            attributePath = Path.Combine(canonicalParent, Path.GetFileName(fullPath));
        }
        // Real hwmon/procfs attributes are regular kernel files. Do not follow an arbitrary leaf link.
        if (new FileInfo(attributePath).LinkTarget is not null)
        { throw new IOException("Symbolic-link kernel attributes are not read."); }
        return attributePath;
    }

    private static bool IsReadFailure(Exception error) => error is IOException or UnauthorizedAccessException
        or System.Security.SecurityException or ArgumentException or NotSupportedException or OverflowException;

    private static void AddNotice(List<string> notices, string notice)
    {
        if (notices.Count < MaximumNotices && !notices.Contains(notice, StringComparer.Ordinal)) { notices.Add(notice); }
    }

    private static string Limit(string value) => value.Length <= 160 ? value : value[..160];

    private static bool IsRoot()
    {
        if (!OperatingSystem.IsLinux()) { return false; }
        try { return GetEffectiveUserId() == 0; }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { return false; }
    }

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();

    /// <summary>Closes this reader without changing kernel state or user data.</summary>
    public void Dispose()
    {
        lock (sync)
        {
            disposed = true;
            activeFans.Clear();
            activeFanOrder.Clear();
            snapshot = HardwareSensorSnapshot.Empty;
            sensorDebug = null;
            driverStatus = new(false, null, "The Linux sensor reader is closed.");
        }
    }

    private readonly record struct TextValue(string? Value, string? Notice);
    private enum SensorKind { Unknown, Cpu, Gpu, Motherboard }
    private sealed record KernelDevice(string Id, string DevicePath, string Name, SensorKind Kind, IReadOnlyList<SensorReadingDto> Readings, string? Notice);
}
