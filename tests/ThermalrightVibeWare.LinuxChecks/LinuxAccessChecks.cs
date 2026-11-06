// VBWR B
// TermalRight VibeWare | Umberto Giacobbi | SPDX-License-Identifier: MIT
// Access inventory fixtures never read host procfs, sysfs or native libraries.
// VBWR E

using ThermalrightVibeWare.Core;

internal static class LinuxAccessChecks
{
    internal static void Run(string directory, Action<bool, string> check)
    {
        var amd = CreateRoots(Path.Combine(directory, "amd"), "AuthenticAMD");
        Module(amd.Sys, "k10temp");
        Module(amd.Sys, "nct6775_core");
        Module(amd.Sys, "nvidia");
        Chip(amd.Sys, 0, "nct6792", "not a temperature");
        Chip(amd.Sys, 1, "nvme", "999999");
        using (var reader = new LinuxMetricsService(amd.Proc, amd.Sys, allowHardwareAccess: true))
        {
            check(!reader.DriverStatus.Available && reader.DriverStatus.Sources is null,
                "Constructing a Linux provider leaves driver inventory and sampling idle.");
            var status = reader.ReadDriverStatus();
            var sources = status.Sources ?? throw new InvalidOperationException("Missing Linux access inventory.");
            check(sources.Any(source => source.Name == "Linux kernel" && source.Detail == "7.0-fixture"),
                "Kernel metadata comes from the selected fixture root.");
            check(sources.Any(source => source.Name == "k10temp" && source.Status == "Loaded"),
                "AMD temperature module presence is reported without a sensor query.");
            check(sources.Any(source => source.Name.Contains("nct6775", StringComparison.Ordinal) && source.Status == "Loaded"),
                "The shared Nuvoton core module is recognized independently of a chip reading.");
            check(sources.Any(source => source.Name.Contains("nct6792", StringComparison.Ordinal) && source.Status == "Exposed"),
                "Exposed motherboard chip identities are preserved.");
            check(sources.All(source => !source.Name.Contains("nvme", StringComparison.OrdinalIgnoreCase)),
                "Storage hwmon devices are omitted from the sensor access inventory.");
            check(sources.Any(source => source.Name == "Latest sensor sample" && source.Status == "Not sampled")
                && !status.Available && reader.InitializationFailureDetails is null,
                "Loaded modules and exposed chip names never claim successful sensor access.");
            check(sources.Any(source => source.Name == "NVIDIA NVML" && source.Status == "Not checked"),
                "Fixture driver detection never loads the host's NVIDIA library.");
            check(status.Guidance is { Count: > 0 } guidance
                && guidance.Any(text => text.Contains("lm-sensors", StringComparison.Ordinal))
                && guidance.Any(text => text.Contains("SPD", StringComparison.Ordinal)),
                "Guidance distinguishes kernel support, diagnostic tools and unimplemented RAM metadata.");
        }

        var intel = CreateRoots(Path.Combine(directory, "intel"), "GenuineIntel");
        using (var reader = new LinuxMetricsService(intel.Proc, intel.Sys, allowHardwareAccess: true))
        {
            var status = reader.ReadDriverStatus();
            check(status.Sources!.Any(source => source.Name == "coretemp" && source.Status == "Not detected"),
                "A missing Intel temperature module is explicitly not detected.");
            check(status.Guidance!.Any(text => text.Contains("coretemp", StringComparison.Ordinal)),
                "CPU guidance follows the selected fixture vendor rather than the host CPU.");
            check(!status.Available, "Missing modules never manufacture usable readings.");
        }

        using (var disabled = new LinuxMetricsService(amd.Proc, amd.Sys))
        {
            var status = disabled.ReadDriverStatus();
            check(!status.Available && status.Sources is { Count: 0 },
                "Disabled hardware access does not inspect driver metadata.");
        }

        using (var missing = new LinuxMetricsService(Path.Combine(directory, "missing-proc"), Path.Combine(directory, "missing-sys"), allowHardwareAccess: true))
        {
            try { _ = missing.ReadDriverStatus(); check(false, "Missing fixture roots must not fall back to host roots."); }
            catch (DirectoryNotFoundException) { check(true, "Missing fixture roots fail explicitly without host access."); }
        }
    }

    private static (string Proc, string Sys) CreateRoots(string root, string vendor)
    {
        var proc = Path.Combine(root, "proc");
        var sys = Path.Combine(root, "sys");
        Directory.CreateDirectory(Path.Combine(proc, "sys", "kernel"));
        Directory.CreateDirectory(Path.Combine(sys, "class", "hwmon"));
        Directory.CreateDirectory(Path.Combine(sys, "class", "drm"));
        File.WriteAllText(Path.Combine(proc, "sys", "kernel", "osrelease"), "7.0-fixture\n");
        File.WriteAllText(Path.Combine(proc, "cpuinfo"), "processor : 0\nvendor_id : " + vendor + "\n");
        return (proc, sys);
    }

    private static void Module(string sys, string name) => Directory.CreateDirectory(Path.Combine(sys, "module", name));

    private static void Chip(string sys, int index, string name, string value)
    {
        var monitor = Path.Combine(sys, "class", "hwmon", "hwmon" + index);
        Directory.CreateDirectory(Path.Combine(monitor, "device"));
        File.WriteAllText(Path.Combine(monitor, "name"), name + "\n");
        File.WriteAllText(Path.Combine(monitor, "temp1_input"), value);
    }
}
