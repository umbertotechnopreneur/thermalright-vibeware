// VBWR B
// TermalRight VibeWare | Umberto Giacobbi | SPDX-License-Identifier: MIT
// NVML observations supplied entirely by an offline native API fixture.
// VBWR E

using ThermalrightVibeWare.Core;

internal static class LinuxNvmlChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var api = new FixtureNvmlApi();
        using (var reader = new LinuxNvmlSensors(api))
        {
            check(api.Initializations == 0, "Constructing the NVML provider performs no driver initialization.");
            var sample = reader.Read();
            check(sample.Devices.Count == 1, "A valid NVML device is projected once.");
            var gpu = sample.Devices.Single();
            check(gpu.Readings.Any(reading => reading.Type == "Load" && reading.Value == 0), "A measured zero GPU load remains zero.");
            check(gpu.Readings.Single(reading => reading.Type == "Temperature").Value == 0, "A measured zero temperature is not replaced by unavailable.");
            check(gpu.Readings.Single(reading => reading.Unit == "W").Value is { } watts && Math.Abs(watts - 153.456) < 0.000001,
                "NVML power converts milliwatts to watts exactly once.");
            check(gpu.Readings.Any(reading => reading.Unit == "GiB" && reading.Value == 8)
                && gpu.Readings.Any(reading => reading.Unit == "GiB" && reading.Value == 2), "GPU memory bytes are represented in GiB.");
            check(gpu.Readings.Any(reading => reading.Type == "Control" && reading.Unit == "%" && reading.Value == 42)
                && gpu.Readings.All(reading => reading.Unit != "RPM" && reading.Type != "Fan"), "NVML fan percentage is never invented as fan RPM.");
            var repeated = reader.Read();
            check(api.Initializations == 1 && repeated.Devices.Single().Id == gpu.Id, "Repeated reads keep one NVML session and a stable GPU identity.");
            reader.Dispose();
            reader.Dispose();
            check(api.Shutdowns == 1 && api.Disposals == 1, "NVML shutdown and native resource release happen exactly once.");
        }

        var absent = new FixtureNvmlApi { InitializationStatus = 12 };
        using (var reader = new LinuxNvmlSensors(absent))
        {
            var first = reader.Read();
            _ = reader.Read();
            check(first.Devices.Count == 0 && !string.IsNullOrWhiteSpace(first.Notice), "A missing NVML library returns an explicit unavailable observation.");
            check(absent.Initializations == 1, "An unavailable native library is not repeatedly initialized on every read.");
        }
        check(absent.Shutdowns == 0 && absent.Disposals == 1, "Failed initialization never calls NVML shutdown.");

        var missingLibrary = new FixtureNvmlApi { InitializationError = new DllNotFoundException("Fixture library missing") };
        using (var reader = new LinuxNvmlSensors(missingLibrary))
        {
            var sample = reader.Read();
            check(sample.Devices.Count == 0 && sample.Notice?.Contains(nameof(DllNotFoundException), StringComparison.Ordinal) == true,
                "A missing native library exception becomes an explicit unavailable result without opening host libraries.");
        }

        var partial = new FixtureNvmlApi { TemperatureStatus = 3, PowerStatus = 4, MemoryStatus = 13 };
        using (var reader = new LinuxNvmlSensors(partial))
        {
            var gpu = reader.Read().Devices.Single();
            var unavailable = gpu.Readings.Where(reading => reading.Type == "Temperature" || reading.Unit is "W" or "GiB").ToArray();
            check(unavailable.Length >= 3 && unavailable
                .All(reading => reading.Value is null && !string.IsNullOrWhiteSpace(reading.Notice)),
                "Unsupported, denied and missing API readings remain explicitly unavailable.");
            check(gpu.Readings.Any(reading => reading.Unit == "MHz" && reading.Value == 1400), "Unavailable fields do not discard independent valid clock readings.");
        }

        var unidentified = new FixtureNvmlApi { IdentityStatus = 3 };
        using (var reader = new LinuxNvmlSensors(unidentified))
        {
            var sample = reader.Read();
            check(sample.Devices.Count == 0 && !string.IsNullOrWhiteSpace(sample.Notice), "A GPU without a stable identity is not given an index-based replacement identity.");
        }

        var many = new FixtureNvmlApi { Count = 128 };
        using (var reader = new LinuxNvmlSensors(many))
        {
            var sample = reader.Read();
            check(sample.Devices.Count == 8 && many.Handles == 8, "NVML enumeration is bounded before opening device handles.");
            check(sample.Devices.Select(device => device.Id).Distinct(StringComparer.Ordinal).Count() == 8,
                "Different GPU UUIDs keep distinct identities even with identical model names.");
        }

        var idle = new FixtureNvmlApi();
        using (var reader = new LinuxNvmlSensors(idle)) { }
        check(idle.Initializations == 0 && idle.Shutdowns == 0 && idle.Disposals == 1,
            "Disposing an unused provider performs no driver session operations.");

        check(LinuxNvmlSensors.NormalizePciBusId("00000000:06:00.0") == "0000:06:00.0",
            "NVML and Linux PCI domain widths normalize to the same physical identity.");
        check(new[] { "0000:100:00.0", "0000:06:20.0", "0000:06:00.8", "0000:06:00.0\n" }
            .All(value => LinuxNvmlSensors.NormalizePciBusId(value) is null), "Malformed PCI identities cannot match a real DRM device.");

        using (var reader = new LinuxNvmlSensors(new FixtureNvmlApi { FanPercent = 150 }))
        {
            check(reader.Read().Devices.Single().Readings.Single(reading => reading.Type == "Control").Value == 150,
                "An NVML intended fan percentage above 100 remains a percentage rather than being clipped or reinterpreted as RPM.");
        }
    }

    private sealed class FixtureNvmlApi : ILinuxNvmlApi
    {
        internal int InitializationStatus, TemperatureStatus, PowerStatus, MemoryStatus, IdentityStatus;
        internal Exception? InitializationError;
        internal uint Count = 1;
        internal uint FanPercent = 42;
        internal int Initializations, Shutdowns, Disposals, Handles;
        public int Initialize() { Initializations++; if (InitializationError is not null) { throw InitializationError; } return InitializationStatus; }
        public int Shutdown() { Shutdowns++; return 0; }
        public int DeviceCount(out uint count) { count = Count; return 0; }
        public int DeviceHandle(uint index, out IntPtr handle) { Handles++; handle = (IntPtr)(index + 1); return 0; }
        public int DeviceName(IntPtr device, out string value) { value = "Fixture NVIDIA GPU"; return 0; }
        public int DeviceUuid(IntPtr device, out string value) { value = "GPU-fixture-" + device; return IdentityStatus; }
        public int DevicePciBusId(IntPtr device, out string value) { value = "00000000:06:00.0"; return IdentityStatus; }
        public int Utilization(IntPtr device, out uint gpu, out uint memory) { gpu = 0; memory = 7; return 0; }
        public int Temperature(IntPtr device, out uint value) { value = 0; return TemperatureStatus; }
        public int Power(IntPtr device, out uint value) { value = 153456; return PowerStatus; }
        public int Clock(IntPtr device, uint type, out uint value) { value = type == 0 ? 1400u : 7000u; return 0; }
        public int Memory(IntPtr device, out ulong total, out ulong free, out ulong used)
        { total = 8UL * 1024 * 1024 * 1024; used = 2UL * 1024 * 1024 * 1024; free = total - used; return MemoryStatus; }
        public int FanSpeed(IntPtr device, out uint value) { value = FanPercent; return 0; }
        public void Dispose() { Disposals++; }
    }
}
