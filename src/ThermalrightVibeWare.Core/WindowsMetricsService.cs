/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsMetricsService.cs
 * Purpose: Collects supported hardware metrics and tracks observed fan activity.
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
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Lhm = LibreHardwareMonitor.Hardware;

namespace ThermalrightVibeWare.Core;

/// <summary>
/// Reads CPU/GPU/board sensors through Libre Hardware Monitor and physical RAM through Windows.
/// Construction opens no hardware. This adapter never installs, extracts or removes a sensor driver.
/// </summary>
/// <param name="allowHardwareAccess">Whether explicit reads may open the configured hardware provider; construction opens no hardware.</param>
public sealed partial class WindowsMetricsService(bool allowHardwareAccess = false) : IMetricsProvider
{
    private const int MaximumHardware = 64;
    private const int MaximumDepth = 8;
    private const int MaximumSensorsPerDevice = 1024;
    private const int MaximumGpus = 8;
    private const int MaximumFans = 16;
    private const string DriverUnavailableNotice = "PawnIO is not installed. CPU temperature/clock and motherboard/fan readings need it; GPU and RAM may still be available.";
    private readonly object sync = new();
    private readonly Dictionary<string, LinkedListNode<string>> observedFans = new(StringComparer.Ordinal);
    private readonly LinkedList<string> observedFanOrder = new();
    private Lhm.Computer? computer;
    private HardwareSensorSnapshot snapshot = HardwareSensorSnapshot.Empty;
    private HardwareDriverStatus driverStatus = new(false, null,
        "The sensor driver has not been checked yet. Open a System theme to check it.");
    private bool initializationAttempted, disposed;
    private long lastRead;

    /// <summary>Local failure details, printed only by the explicitly requested CLI diagnostic command.</summary>
    public string? InitializationFailureDetails { get; private set; }

    /// <summary>The registered PawnIO service file, when present. No driver is created or opened.</summary>
    public static string? ExpectedDriverPath => ReadDriverInstallationStatus().Path;

    /// <summary>Returns the last driver status without starting or sampling the sensor library.</summary>
    public HardwareDriverStatus DriverStatus { get { lock (sync) { return driverStatus; } } }

    /// <summary>Checks existing registration only. A found installation does not prove usable sensor access.</summary>
    public static HardwareDriverStatus ReadDriverInstallationStatus()
    {
        if (!System.OperatingSystem.IsWindows())
        { return new(false, null, "PawnIO sensor access is only implemented on Windows."); }
        try
        {
            var installed = LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled;
            using var service = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO", writable: false);
            var path = NormalizeDriverPath(service?.GetValue("ImagePath") as string);
            return new(installed, path, installed
                ? "PawnIO installation found. Sensor access has not been verified; Thermalright VibeWare does not install or change this driver."
                : DriverUnavailableNotice);
        }
        catch (Exception error) when (IsSensorFailure(error))
        { return new(false, null, "PawnIO installation could not be checked. No driver was installed or changed."); }
    }

    /// <summary>Resolves a registered driver path without installing or starting it.</summary>
    /// <param name="path">Registered driver binary path to normalize.</param>
    private static string? NormalizeDriverPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) { return null; }
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (path.StartsWith(@"\??\", StringComparison.Ordinal)) { path = path[4..]; }
        if (path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        { path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path[12..]); }
        if (path.StartsWith(@"System32\", StringComparison.OrdinalIgnoreCase))
        { path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path); }
        return Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : null;
    }

    /// <summary>Returns the last sample for one second, using monotonic time and no background worker.</summary>
    public HardwareSensorSnapshot ReadSensors()
    {
        lock (sync)
        {
            if (disposed) { return HardwareSensorSnapshot.Empty; }
            var now = Stopwatch.GetTimestamp();
            if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead, now) < TimeSpan.FromSeconds(1))
            {
                return snapshot;
            }
            lastRead = now;
            if (!EnsureComputer()) { return snapshot; }

            try
            {
                HardwareDeviceSensors? cpu = null;
                var memory = ReadPhysicalMemory();
                string? motherboardName = null;
                var boardTemperatures = new List<HardwareTemperatureSensors>();
                var boardFans = new List<HardwareFanSensors>();
                var boardReadings = new List<HardwareReading>();
                var gpus = new List<HardwareDeviceSensors>();
                var dimms = new List<HardwareMemoryModule>();
                var failed = false;
                var boardFanChannelsReported = false;
                foreach (var hardware in EnumerateHardware(computer!.Hardware))
                {
                    try
                    {
                        hardware.Update();
                        var sensors = hardware.Sensors.Take(MaximumSensorsPerDevice).ToArray();
                        switch (hardware.HardwareType)
                        {
                            case Lhm.HardwareType.Cpu when cpu is null:
                                boardFanChannelsReported |= HasFanChannels(sensors);
                                cpu = ReadCpu(hardware.Name, sensors);
                                break;
                            case Lhm.HardwareType.GpuNvidia or Lhm.HardwareType.GpuAmd or Lhm.HardwareType.GpuIntel when gpus.Count < MaximumGpus:
                                gpus.Add(ReadGpu(hardware.Name, sensors));
                                break;
                            case Lhm.HardwareType.Motherboard:
                                motherboardName = hardware.Name;
                                boardReadings.AddRange(ReadThemeValues(sensors, 256 - boardReadings.Count));
                                break;
                            case Lhm.HardwareType.Memory when hardware.Identifier.ToString().StartsWith("/memory/dimm/", StringComparison.Ordinal) && dimms.Count < 16:
                                dimms.Add(ReadSpdModule(hardware, sensors));
                                break;
                            case Lhm.HardwareType.SuperIO or Lhm.HardwareType.EmbeddedController when driverStatus.Available:
                                // Only the board's own chip supplies these readings.
                                boardFanChannelsReported |= HasFanChannels(sensors);
                                IReadOnlyList<HardwareReading> themeReadings = ReadThemeValues(sensors, MaximumSensorsPerDevice);
                                if (hardware.Parent is Lhm.Motherboard.Motherboard motherboard)
                                {
                                    themeReadings = ProjectMotherboardReadings(motherboard.Manufacturer, motherboard.Model,
                                        hardware.Identifier.ToString(), themeReadings);
                                }
                                boardReadings.AddRange(themeReadings.Take(256 - boardReadings.Count));
                                foreach (var reading in themeReadings.Where(item => item.Type == "Temperature"))
                                {
                                    if (reading.Value is double value && boardTemperatures.Count < 16)
                                    { boardTemperatures.Add(new(reading.Name, value)); }
                                }
                                boardFans.AddRange(ReadFans(sensors).Take(MaximumFans - boardFans.Count));
                                break;
                        }
                    }
                    catch (Exception error) when (IsSensorFailure(error)) { failed = true; }
                }
                if (cpu is not null && !driverStatus.Available)
                { cpu = cpu with { ClockMHz = null, TemperatureC = null, TemperatureLabel = null, Fans = [] }; }
                var cpuFans = boardFans.Where(fan => fan.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (cpu is not null && cpuFans.Length > 0)
                { cpu = cpu with { Fans = (cpu.Fans ?? []).Concat(cpuFans).Take(MaximumFans).ToArray() }; }
                var otherFans = boardFans.Where(fan => cpu is null || !fan.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase)).ToArray();
                var board = CreateMotherboard(motherboardName, boardTemperatures, otherFans, boardReadings);
                if (memory is not null)
                {
                    var clock = cpu?.Readings?.FirstOrDefault(reading => reading.Type == "Clock" && reading.Name.Equals("Memory", StringComparison.OrdinalIgnoreCase))?.Value;
                    memory = memory with { ClockMHz = clock, Modules = MergeMemoryModules(memoryModules, dimms), SpdModules = dimms };
                }
                snapshot = new(cpu, gpus.ToArray(), memory, DateTimeOffset.UtcNow,
                    AvailabilityNotice(driverStatus, cpu, board, failed, boardFanChannelsReported), board, driverStatus);
            }
            catch (Exception error) when (IsSensorFailure(error))
            {
                // Clear a failed sample rather than presenting our previous sample as current.
                snapshot = HardwareSensorSnapshot.Empty with
                { Notice = "Libre Hardware Monitor could not read this sample. Sensor values are unavailable.", Driver = driverStatus };
            }
            lastRead = Stopwatch.GetTimestamp();
            return snapshot;
        }
    }

    /// <summary>Returns the terminal dashboard metrics from the same cached library sample.</summary>
    public MachineMetrics Read()
    {
        var sensors = ReadSensors();
        var gpu = sensors.Gpus.FirstOrDefault();
        return new(sensors.Cpu?.UsagePercent, sensors.Cpu?.TemperatureC, gpu?.UsagePercent, gpu?.TemperatureC,
            gpu?.Name ?? "GPU",
            sensors.Cpu?.TemperatureC is null ? "Unavailable" : $"Libre Hardware Monitor: {sensors.Cpu.TemperatureLabel}",
            gpu is null ? "Unavailable" : "Libre Hardware Monitor (first detected GPU)");
    }

    /// <summary>Lazily opens the configured sensor provider when hardware access is allowed.</summary>
    private bool EnsureComputer()
    {
        if (computer is not null) { return true; }
        if (initializationAttempted) { return false; }
        initializationAttempted = true;
        if (!System.OperatingSystem.IsWindows())
        {
            snapshot = HardwareSensorSnapshot.Empty with
            { Notice = "Hardware sensors are not implemented for this system.", Driver = driverStatus };
            return false;
        }
        if (RuntimeInformation.OSArchitecture is not (Architecture.X64 or Architecture.X86)
            || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.X86))
        {
            snapshot = HardwareSensorSnapshot.Empty with
            { Notice = "CPU and motherboard sensors have not been enabled for Windows ARM64.", Driver = driverStatus };
            return false;
        }
        if (!allowHardwareAccess)
        {
            snapshot = HardwareSensorSnapshot.Empty with
            {
                Notice = "Hardware sensors are disabled: local hardware access has not been authorized.",
                Driver = driverStatus
            };
            return false;
        }

        Lhm.Computer? candidate = null;
        try
        {
            driverStatus = ReadDriverInstallationStatus();
            ReadMemoryConfiguration();
            candidate = new Lhm.Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                // Owner-approved full readout, including the library's RAM/SPD driver path.
                IsMemoryEnabled = true,
                IsControllerEnabled = true,
                // The owner excludes disks from both polling and Sensor Debug.
                IsStorageEnabled = false,
                IsNetworkEnabled = true,
                IsPsuEnabled = true,
                IsBatteryEnabled = true,
                IsPowerMonitorEnabled = true
            };
            candidate.Open();
            computer = candidate;
            if (!driverStatus.Available)
            {
                InitializationFailureDetails = driverStatus.Notice;
            }
            return true;
        }
        catch (Exception error) when (IsSensorFailure(error))
        {
            InitializationFailureDetails = error.ToString();
            driverStatus = new(false, driverStatus.Path,
                "Libre Hardware Monitor could not start its sensor reader. Run 'trv sensors --diagnose' for details.");
            try { candidate?.Close(); }
            catch (Exception closeError) when (IsSensorFailure(closeError)) { }
            snapshot = HardwareSensorSnapshot.Empty with
            {
                Notice = $"Libre Hardware Monitor could not start ({error.GetBaseException().GetType().Name}). Run 'trv sensors --diagnose' for details.",
                Driver = driverStatus
            };
            return false;
        }
    }

    /// <summary>Projects aggregate CPU readings without individual core/thread rows.</summary>
    /// <param name="name">CPU identity reported by the hardware provider.</param>
    /// <param name="sensors">Raw sensor channels supplied by the selected hardware device.</param>
    private HardwareDeviceSensors ReadCpu(string name, Lhm.ISensor[] sensors)
    {
        var usage = FindValue(sensors, Lhm.SensorType.Load, "CPU Total", percentage: true);
        var averageClock = FindValue(sensors, Lhm.SensorType.Clock, "Cores (Average)", nonnegative: true);
        var clocks = sensors.Where(sensor => sensor.SensorType == Lhm.SensorType.Clock
            && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
            && !sensor.Name.Contains("Bus", StringComparison.OrdinalIgnoreCase)
            && !sensor.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase)
            && !sensor.Name.Contains("Average", StringComparison.OrdinalIgnoreCase))
            .Select(sensor => Value(sensor, nonnegative: true)).OfType<double>().ToArray();
        var temperatures = sensors.Where(sensor => sensor.SensorType == Lhm.SensorType.Temperature)
            .Select(sensor => (Sensor: sensor, Value: Value(sensor))).Where(item => item.Value is not null).ToArray();
        var preferredLabels = new[] { "CPU Package", "Core (Tdie)", "Core (Tctl/Tdie)", "Package" };
        var package = preferredLabels.Select(label => temperatures.FirstOrDefault(item => item.Sensor.Name.Equals(label, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(item => item.Value is not null);
        var temperature = package.Value;
        var temperatureLabel = temperature is not null ? package.Sensor.Name : null;
        if (temperature is null && temperatures.Length > 0)
        {
            temperature = temperatures.Max(item => item.Value);
            temperatureLabel = "Hottest CPU sensor";
        }
        var clock = averageClock ?? (clocks.Length == 0 ? (double?)null : clocks.Average());
        return new(name, usage, clock, temperature,
            clock is null ? null : "Average clock", temperatureLabel, ReadFans(sensors), ReadThemeValues(sensors));
    }

    /// <summary>Projects available GPU usage, temperature, clock and hardware readings.</summary>
    /// <param name="name">GPU identity reported by the hardware provider.</param>
    /// <param name="sensors">Raw sensor channels supplied by the selected hardware device.</param>
    private HardwareDeviceSensors ReadGpu(string name, Lhm.ISensor[] sensors) => new(name,
        FindValue(sensors, Lhm.SensorType.Load, "GPU Core", percentage: true),
        FindValue(sensors, Lhm.SensorType.Clock, "GPU Core", nonnegative: true),
        FindValue(sensors, Lhm.SensorType.Temperature, "GPU Core"), "Core clock", "GPU core", ReadFans(sensors), ReadThemeValues(sensors));

    /// <summary>Combines motherboard temperatures, fans and electrical readings.</summary>
    /// <param name="name">Reported motherboard identity, or null when unavailable.</param>
    /// <param name="temperatures">Named motherboard temperature readings.</param>
    /// <param name="fans">Fan observations that have passed the session activity rule.</param>
    /// <param name="readings">Raw or projected hardware readings, according to the declared type.</param>
    internal static HardwareMotherboardSensors? CreateMotherboard(string? name,
        IReadOnlyList<HardwareTemperatureSensors> temperatures, IReadOnlyList<HardwareFanSensors> fans,
        IReadOnlyList<HardwareReading>? readings = null)
    {
        if (name is null && temperatures.Count == 0 && fans.Count == 0 && (readings?.Count ?? 0) == 0) { return null; }
        // Keep every named reading, but never present an unidentified sensor as the board's main temperature.
        var primary = temperatures.Where(item => IsBoardTemperature(item.Name))
            .OrderBy(item => item.Name.Contains("board", StringComparison.OrdinalIgnoreCase) ? 0 : 1).FirstOrDefault();
        return new(name ?? "Motherboard", primary?.TemperatureC, primary?.Name, fans, temperatures, readings);
    }

    /// <summary>Explains missing hardware readings without treating driver registration as access.</summary>
    /// <param name="driver">Driver installation metadata; availability does not prove sensor access.</param>
    /// <param name="cpu">Available aggregate CPU readings, or null when inaccessible.</param>
    /// <param name="board">Available motherboard readings, or null when inaccessible.</param>
    /// <param name="readFailed">Whether a recoverable sensor read failure occurred.</param>
    /// <param name="boardFanChannelsReported">Whether raw board data reported fan channels, even if none are visible yet.</param>
    internal static string? AvailabilityNotice(HardwareDriverStatus driver, HardwareDeviceSensors? cpu,
        HardwareMotherboardSensors? board, bool readFailed, bool boardFanChannelsReported = false)
    {
        if (!driver.Available) { return driver.Notice ?? DriverUnavailableNotice; }
        var missing = new List<string>();
        if (cpu?.TemperatureC is null) { missing.Add("CPU temperature"); }
        if (board?.Temperatures is not { Count: > 0 }) { missing.Add("motherboard temperatures"); }
        if (!boardFanChannelsReported && (cpu?.Fans?.Count ?? 0) + (board?.Fans.Count ?? 0) == 0) { missing.Add("board/CPU fan RPM"); }
        if (missing.Count > 0)
        {
            return $"Libre Hardware Monitor did not provide {string.Join(", ", missing)}. Available readings are shown; unidentified sensor names are kept as reported.";
        }
        return readFailed ? "Some Libre Hardware Monitor measurements could not be read. Other available readings are shown." : null;
    }

    /// <summary>Recognizes named motherboard temperature channels.</summary>
    /// <param name="name">Raw temperature-channel name reported by the sensor provider.</param>
    private static bool IsBoardTemperature(string name) => name.Contains("Mainboard", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Motherboard", StringComparison.OrdinalIgnoreCase)
        || name.Equals("System", StringComparison.OrdinalIgnoreCase)
        || name.Equals("SYS", StringComparison.OrdinalIgnoreCase);

    /// <summary>Detects whether the raw sensor list contains fan channels.</summary>
    /// <param name="sensors">Raw sensor channels supplied by the selected hardware device.</param>
    private static bool HasFanChannels(Lhm.ISensor[] sensors) => sensors.Any(sensor =>
        sensor.SensorType == Lhm.SensorType.Fan && Value(sensor, nonnegative: true) is >= 0 and <= 20000);

    /// <summary>Projects fans observed spinning during this engine session.</summary>
    /// <param name="sensors">Raw sensor channels supplied by the selected hardware device.</param>
    private HardwareFanSensors[] ReadFans(Lhm.ISensor[] sensors) => sensors
        .Where(sensor => sensor.SensorType == Lhm.SensorType.Fan)
        .Select(sensor => (Id: sensor.Identifier.ToString(), sensor.Name, Rpm: Value(sensor, nonnegative: true)))
        .Where(item => item.Rpm is >= 0 and <= 20000)
        .Where(item => ObserveFan(item.Id, item.Rpm!.Value))
        .Take(MaximumFans)
        .Select(item => new HardwareFanSensors(item.Name, item.Rpm!.Value))
        .ToArray();

    /// <summary>Remember real activity per channel for this reader's lifetime, independently of themes.</summary>
    /// <param name="id">Exact sensor identifier retained across pages and themes during this engine session.</param>
    /// <param name="rpm">Measured fan speed in revolutions per minute; zero remains meaningful after activity.</param>
    private bool ObserveFan(string id, double rpm)
    {
        if (observedFans.TryGetValue(id, out var existing))
        {
            observedFanOrder.Remove(existing);
            observedFanOrder.AddLast(existing);
            return true;
        }
        if (rpm <= 0) { return false; }
        observedFans.Add(id, observedFanOrder.AddLast(id));
        // Bound hot-plug history while retaining channels that are still being sampled.
        if (observedFans.Count > MaximumHardware * MaximumFans)
        {
            observedFans.Remove(observedFanOrder.First!.Value);
            observedFanOrder.RemoveFirst();
        }
        return true;
    }

    /// <summary>Reads physical RAM totals and usage through Windows.</summary>
    private HardwareMemorySensors? ReadPhysicalMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0 || status.AvailablePhysical > status.TotalPhysical)
        { return null; }
        const double gibibyte = 1024d * 1024 * 1024;
        return new(status.TotalPhysical / gibibyte,
            (status.TotalPhysical - status.AvailablePhysical) / gibibyte, status.MemoryLoad,
            memoryType, memoryConfiguredSpeed, Modules: memoryModules, MetadataNotice: memoryMetadataNotice);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    /// <summary>Reads physical memory totals and usage from Windows.</summary>
    /// <param name="status">Sized native structure receiving Windows physical memory counters.</param>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    /// <summary>Finds an available sensor matching the requested type and name.</summary>
    /// <param name="sensors">Raw sensor channels supplied by the selected hardware device.</param>
    /// <param name="type">Raw sensor type used to select readings or their display unit.</param>
    /// <param name="name">Exact sensor name to match using ordinal case-insensitive comparison.</param>
    /// <param name="percentage">Whether to clamp a reading to the 0..100 percentage range.</param>
    /// <param name="nonnegative">Whether to reject negative readings as unavailable.</param>
    private static double? FindValue(IEnumerable<Lhm.ISensor> sensors, Lhm.SensorType type, string name,
        bool percentage = false, bool nonnegative = false) => sensors
        .Where(sensor => sensor.SensorType == type && sensor.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        .Select(sensor => Value(sensor, percentage, nonnegative)).FirstOrDefault(value => value is not null);

    /// <summary>Reads a finite sensor value with optional range restrictions.</summary>
    /// <param name="sensor">Raw sensor channel whose value is being projected.</param>
    /// <param name="percentage">Whether to clamp a reading to the 0..100 percentage range.</param>
    /// <param name="nonnegative">Whether to reject negative readings as unavailable.</param>
    private static double? Value(Lhm.ISensor sensor, bool percentage = false, bool nonnegative = false)
    {
        if (sensor.Value is not float value || !float.IsFinite(value)) { return null; }
        if (percentage && value is < 0 or > 100 || nonnegative && value < 0) { return null; }
        return value;
    }

    /// <summary>Traverses allowed sensor devices without collecting excluded groups.</summary>
    /// <param name="roots">Root hardware devices from which allowed children are traversed.</param>
    private IEnumerable<Lhm.IHardware> EnumerateHardware(IEnumerable<Lhm.IHardware> roots)
    {
        var visited = new HashSet<Lhm.IHardware>(ReferenceEqualityComparer.Instance);
        foreach (var root in roots.Where(IncludeHardware).Take(MaximumHardware))
        {
            foreach (var hardware in Visit(root, 0)) { yield return hardware; }
            if (visited.Count >= MaximumHardware) { yield break; }
        }

        // Visit only allowed devices, bounding depth before updating/enumerating children.
        // hardware is the current raw device; depth counts edges from a root device.
        IEnumerable<Lhm.IHardware> Visit(Lhm.IHardware hardware, int depth)
        {
            if (visited.Count >= MaximumHardware || !visited.Add(hardware)) { yield break; }
            yield return hardware;
            if (depth >= MaximumDepth || visited.Count >= MaximumHardware) { yield break; }
            // Visit child sensor chips before later roots can fill the shared device budget.
            foreach (var child in hardware.SubHardware.Where(IncludeHardware).Take(MaximumHardware))
            {
                foreach (var descendant in Visit(child, depth + 1)) { yield return descendant; }
                if (visited.Count >= MaximumHardware) { yield break; }
            }
        }
    }

    /// <summary>Identifies recoverable sensor failures while preserving fatal runtime exceptions.</summary>
    /// <param name="error">Exception being classified for recoverable handling.</param>
    private static bool IsSensorFailure(Exception error) => error is not (OutOfMemoryException or StackOverflowException or AccessViolationException);

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) { return; }
            disposed = true;
            try { computer?.Close(); }
            catch (Exception error) when (IsSensorFailure(error)) { }
            finally
            {
                computer = null;
                snapshot = HardwareSensorSnapshot.Empty;
                observedFans.Clear();
                observedFanOrder.Clear();
            }
        }
    }
}
