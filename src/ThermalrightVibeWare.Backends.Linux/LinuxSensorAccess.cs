/* VBWR B
 * Project: Thermalright VibeWare
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * Purpose: Explains detected Linux sensor drivers without sampling or changing hardware.
 * Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
 * VBWR E */

using System.Text;

namespace ThermalrightVibeWare.Core;

public sealed partial class LinuxMetricsService
{
    /// <summary>Reads bounded access metadata. Module or library detection never proves a sensor query succeeded.</summary>
    internal HardwareDriverStatus ReadAccessMetadata()
    {
        if (!allowHardwareAccess || !fixtureRoots && !OperatingSystem.IsLinux())
        {
            return new(false, null, "Linux sensor access is disabled. No kernel metadata or NVIDIA library was inspected.",
                [], ["Enable the supported Linux reader before requesting sensor access details."]);
        }

        canonicalProcRoot = CanonicalPath(procRoot);
        canonicalSysRoot = CanonicalPath(sysRoot);
        if (canonicalProcRoot is null || canonicalSysRoot is null)
        { throw new DirectoryNotFoundException("The configured Linux procfs or sysfs root is missing or inaccessible. No alternate root was used."); }

        var sources = new List<HardwareSensorSource>();
        var guidance = new List<string>();
        var notices = new List<string>();
        void Add(string name, string status, string detail)
        {
            if (sources.Count < MaximumDevices)
            { sources.Add(new(Limit(name), status, detail.Length <= 768 ? detail : detail[..768])); }
        }

        var kernel = ReadText(Path.Combine(procRoot, "sys", "kernel", "osrelease"), 256);
        Add("Linux kernel", kernel.Value is null ? "Unavailable" : "Detected",
            kernel.Value is null ? kernel.Notice ?? "Kernel version metadata is unavailable." : kernel.Value);

        var cpuIdentity = ReadAccessPrefix(Path.Combine(procRoot, "cpuinfo"));
        var cpuDriver = cpuIdentity?.Contains("AuthenticAMD", StringComparison.Ordinal) == true ? "k10temp"
            : cpuIdentity?.Contains("GenuineIntel", StringComparison.Ordinal) == true ? "coretemp" : null;
        if (cpuDriver is not null)
        {
            var loaded = AccessModulePresent(cpuDriver);
            Add(cpuDriver, loaded ? "Loaded" : "Not detected", loaded
                ? "CPU temperature module is present in kernel metadata. Individual readings still depend on exposed channels and permissions."
                : "The expected CPU temperature module was not detected in accessible kernel metadata; CPU support has not been established.");
            guidance.Add(loaded
                ? $"For missing CPU temperatures, check {cpuDriver} support for this processor and the readable channels reported by lm-sensors."
                : $"CPU temperatures may need the distribution kernel's {cpuDriver} module. Check the distribution's hardware guide before changing loaded modules.");
        }
        else
        {
            Add("CPU temperature driver", "Not detected", "CPU vendor metadata did not identify a supported driver recommendation.");
            guidance.Add("Check the distribution's CPU sensor support. A missing temperature is unavailable, not a measured zero.");
        }

        var nuvoton = AccessModulePresent("nct6775") || AccessModulePresent("nct6775_core");
        Add("nct6775 / nct6775_core", nuvoton ? "Loaded" : "Not detected", nuvoton
            ? "A Nuvoton hardware-monitoring module is present. The hwmon entries below identify exposed chip names."
            : "No nct6775 module was detected in accessible metadata. This does not establish which sensor chip this motherboard uses.");

        ReadAccessHwmon(sources, notices);
        var gpus = ReadDrmDevices(notices);
        foreach (var gpu in gpus.Take(8))
        {
            Add(gpu.Name, gpu.Driver is null ? "Not detected" : "Bound",
                gpu.Driver is null ? "The DRM device has no accessible driver binding." : $"Kernel DRM driver: {gpu.Driver}. This metadata check does not sample GPU sensors.");
        }

        var nvidia = AccessModulePresent("nvidia") || gpus.Any(gpu => gpu.Driver == "nvidia");
        Add("NVIDIA kernel driver", nvidia ? "Detected" : "Not detected", nvidia
            ? "NVIDIA driver metadata is present. NVML access is verified only by an explicit sensor sample."
            : "No NVIDIA module or DRM binding was detected in accessible metadata. This is not proof that no NVIDIA GPU is installed.");
        if (nvidia)
        {
            var library = !fixtureRoots && OperatingSystem.IsLinux() && LinuxNvmlSensors.IsLibraryPresent();
            Add("NVIDIA NVML", fixtureRoots ? "Not checked" : library ? "Detected" : "Unavailable", fixtureRoots
                ? "Fixture-root inspection does not load host libraries."
                : library ? "libnvidia-ml.so.1 can be loaded. Library presence alone does not prove GPU query access."
                : "libnvidia-ml.so.1 could not be loaded. NVML readings remain unavailable.");
            guidance.Add(library
                ? "If NVML reports a permission or driver error, check the NVIDIA driver and device access using the distribution's supported tools. Missing NVML channels may be unsupported by this GPU."
                : "For NVIDIA readings, use the distribution's supported NVIDIA driver package that provides libnvidia-ml.so.1. The app does not install or replace it.");
        }

        var amdgpu = AccessModulePresent("amdgpu");
        if (amdgpu && !gpus.Any(gpu => gpu.Driver == "amdgpu"))
        { Add("amdgpu", "Loaded", "The AMDGPU module is present; no accessible DRM binding was identified in this metadata check."); }
        Add("Latest sensor sample", !hasRead ? "Not sampled" : driverStatus.Available ? "Available" : "Unavailable",
            !hasRead ? "No sensor sample has confirmed readable measurements in this engine session."
            : driverStatus.Available ? "At least one measurement was readable in the latest sample; other channels may remain unavailable."
            : InitializationFailureDetails ?? "No hwmon or NVML measurement was confirmed in the latest sample.");
        if (notices.Count > 0) { Add("Metadata access", "Partial", string.Join(" ", notices.Take(4))); }

        guidance.Add("Motherboard temperatures, fan RPM and voltages need a kernel hwmon driver for the actual sensor chip. Use the distribution's supported kernel and hardware documentation; installing lm-sensors alone does not add a missing kernel driver.");
        guidance.Add("lm-sensors can inspect available channels. Sensor detection scans and module loading are separate manual actions; this app does not run them or change permissions.");
        guidance.Insert(0, "Open Sensor Debug or select a System theme to request a sensor sample. The driver list checks metadata only; it does not prove a reading is available.");
        guidance.Add("Board labels and voltage conversions from sensors.conf are not applied by this reader. DIMM model, serial, slot and SPD metadata are not implemented on Linux; a driver installation alone does not enable them in the app.");
        return new(driverStatus.Available, null,
            "Linux driver and library metadata was checked without sampling sensors. Detection is separate from usable readings. No module was loaded or changed.",
            sources.ToArray(), guidance.ToArray());
    }

    private bool AccessModulePresent(string name)
    {
        var module = CanonicalPath(Path.Combine(sysRoot, "module", name));
        return module is not null && WithinSysRoot(module);
    }

    private string? ReadAccessPrefix(string path)
    {
        try
        {
            using var stream = new FileStream(SafeAttributePath(path), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var buffer = new char[4096];
            var count = reader.ReadBlock(buffer, 0, buffer.Length);
            return new string(buffer, 0, count);
        }
        catch (Exception error) when (IsReadFailure(error)) { return null; }
    }

    private void ReadAccessHwmon(List<HardwareSensorSource> sources, List<string> notices)
    {
        try
        {
            var directory = CanonicalPath(Path.Combine(sysRoot, "class", "hwmon"));
            if (directory is null || !WithinSysRoot(directory))
            { AddNotice(notices, "The hwmon metadata directory is unavailable."); return; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var entries = 0;
            foreach (var entry in Directory.EnumerateDirectories(directory))
            {
                if (++entries > MaximumDirectoryEntries || sources.Count >= MaximumDevices - 14)
                { AddNotice(notices, "Sensor access metadata reached its directory limit."); break; }
                var monitor = CanonicalPath(entry);
                if (monitor is null || !WithinSysRoot(monitor) || !seen.Add(monitor)) { continue; }
                using var monitorDirectory = OperatingSystem.IsLinux() ? MonitorDirectory.Open(monitor) : null;
                if (monitorDirectory is not null && (monitorDirectory.IsInvalid || !monitorDirectory.IsAt(monitor)))
                { AddNotice(notices, "A hwmon metadata directory could not be pinned and was skipped."); continue; }
                var backing = ResolveBackingDevice(monitor);
                if (backing is null || IsExcludedDevice(backing, notices)
                    || backing != StripHwmonSuffix(monitor) && IsExcludedDevice(monitor, notices)) { continue; }
                using var physicalDevice = OperatingSystem.IsLinux() ? MonitorDirectory.Open(backing) : null;
                if (physicalDevice is not null && (physicalDevice.IsInvalid || !physicalDevice.IsAt(backing)))
                { AddNotice(notices, "A hwmon metadata backing device could not be pinned and was skipped."); continue; }
                var physicalPath = physicalDevice?.DescriptorPath ?? backing;
                var name = ReadHwmonName(monitorDirectory?.DescriptorPath ?? monitor, monitorDirectory,
                    physicalPath, physicalDevice, out _);
                if (string.IsNullOrWhiteSpace(name.Value))
                {
                    AddNotice(notices, "A non-storage hwmon chip identity is unavailable. " + name.Notice);
                    continue;
                }
                if (IsStorageDevice(name.Value, backing)) { continue; }
                var driver = CanonicalPath(Path.Combine(physicalPath, "driver"));
                var bound = driver is not null && WithinSysRoot(driver) ? Path.GetFileName(driver) : null;
                if (CanonicalPath(entry) != monitor || ResolveBackingDevice(monitor) != backing
                    || monitorDirectory is not null && !monitorDirectory.IsAt(monitor)
                    || physicalDevice is not null && !physicalDevice.IsAt(backing))
                { AddNotice(notices, "A hwmon device changed during metadata inspection and was skipped."); continue; }
                sources.Add(new("hwmon · " + Limit(name.Value), "Exposed", bound is null
                    ? "Chip metadata is readable; no driver binding is exposed here. Sensor values were not sampled by this access check."
                    : $"Bound driver: {Limit(bound)}. Chip metadata is readable; sensor values were not sampled by this access check."));
            }
        }
        catch (Exception error) when (IsReadFailure(error))
        { AddNotice(notices, "Hwmon access metadata unavailable: " + error.GetType().Name + "."); }
    }
}
