/* VBWR B
 * Project: Thermalright VibeWare
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * Purpose: Reads bounded NVIDIA telemetry from the installed NVML library without changing devices.
 * Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
 * VBWR E */

using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Core;

public sealed record LinuxNvmlDevice(string Id, string Name, string? PciBusId, IReadOnlyList<SensorReadingDto> Readings, string? Notice);
public sealed record LinuxNvmlSnapshot(IReadOnlyList<LinuxNvmlDevice> Devices, string? Notice);

/// <summary>An idle NVIDIA reader. Only an explicit Read initializes NVML and samples GPU telemetry.</summary>
public sealed class LinuxNvmlSensors : IDisposable
{
    private const string LibraryName = "libnvidia-ml.so.1";
    private const uint MaximumDevices = 8;
    private readonly object sync = new();
    private readonly ILinuxNvmlApi api;
    private bool attempted, initialized, disposed;
    private string? statusNotice;

    public LinuxNvmlSensors() : this(new NativeNvmlApi()) { }
    internal LinuxNvmlSensors(ILinuxNvmlApi api)
    {
        ArgumentNullException.ThrowIfNull(api);
        this.api = api;
    }
    public string? StatusNotice { get { lock (sync) { return statusNotice; } } }

    /// <summary>Checks library availability only. It does not initialize NVML or read a GPU.</summary>
    public static bool IsLibraryPresent()
    {
        if (!OperatingSystem.IsLinux()) { return false; }
        if (!NativeLibrary.TryLoad(LibraryName, out var library)) { return false; }
        NativeLibrary.Free(library);
        return true;
    }

    public LinuxNvmlSnapshot Read()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!attempted)
            {
                attempted = true;
                try
                {
                    var result = api.Initialize();
                    initialized = result == 0;
                    if (!initialized) { statusNotice = "NVIDIA NVML initialization unavailable: " + Describe(result); }
                }
                catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
                { statusNotice = "NVIDIA NVML unavailable: " + error.GetType().Name + ". Use the NVIDIA driver provided for your distribution; this app does not install one."; }
            }
            if (!initialized) { return new([], statusNotice); }
            var countResult = api.DeviceCount(out var count);
            if (countResult != 0)
            {
                statusNotice = "NVIDIA NVML GPU enumeration unavailable: " + Describe(countResult);
                return new([], statusNotice);
            }
            var devices = new List<LinuxNvmlDevice>();
            var notices = new List<string>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            if (count > MaximumDevices) { notices.Add("NVIDIA NVML GPU enumeration was limited to eight devices."); }
            for (uint index = 0; index < Math.Min(count, MaximumDevices); index++)
            {
                var handleResult = api.DeviceHandle(index, out var device);
                if (handleResult != 0 || device == IntPtr.Zero)
                { notices.Add("NVIDIA GPU access unavailable: " + (handleResult == 0 ? "The driver returned an empty handle." : Describe(handleResult))); continue; }
                var uuidResult = api.DeviceUuid(device, out var uuid);
                var pciResult = api.DevicePciBusId(device, out var pci);
                var pciBusId = pciResult == 0 ? NormalizePciBusId(pci) : null;
                var identity = uuidResult == 0 && ValidText(uuid, 96) ? "uuid:" + uuid : pciBusId is not null ? "pci:" + pciBusId : null;
                if (identity is null)
                { notices.Add("An NVIDIA GPU had no usable UUID or PCI identity and was skipped."); continue; }
                var id = "linux:/nvml/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
                if (!identities.Add(id)) { notices.Add("An ambiguous duplicate NVIDIA GPU identity was skipped."); continue; }
                var nameResult = api.DeviceName(device, out var name);
                if (nameResult != 0 || !ValidText(name, 256)) { name = "NVIDIA GPU (NVML)"; }
                var deviceNotices = new List<string>();
                if (nameResult != 0) { deviceNotices.Add("GPU name unavailable: " + Describe(nameResult)); }
                if (pciResult != 0) { deviceNotices.Add("PCI identity unavailable: " + Describe(pciResult)); }
                if (pciResult == 0 && pciBusId is null) { deviceNotices.Add("The NVIDIA driver returned an invalid PCI identity."); }
                var readings = new List<SensorReadingDto>();
                var utilizationResult = api.Utilization(device, out var gpuLoad, out var memoryLoad);
                Add(readings, id, "gpu-load", "GPU busy", "Load", "%", utilizationResult, gpuLoad, percent: true);
                Add(readings, id, "memory-controller-load", "Memory controller busy", "Load", "%", utilizationResult, memoryLoad, percent: true);
                var temperatureResult = api.Temperature(device, out var temperature);
                Add(readings, id, "temperature", "GPU temperature", "Temperature", "°C", temperatureResult, temperature);
                var powerResult = api.Power(device, out var power);
                Add(readings, id, "power", "GPU board power", "Power", "W", powerResult, power, 1000);
                var graphicsResult = api.Clock(device, 0, out var graphicsClock);
                Add(readings, id, "graphics-clock", "GPU graphics clock", "Clock", "MHz", graphicsResult, graphicsClock);
                var memoryClockResult = api.Clock(device, 2, out var memoryClock);
                Add(readings, id, "memory-clock", "GPU memory clock", "Clock", "MHz", memoryClockResult, memoryClock);
                var memoryResult = api.Memory(device, out var total, out var free, out var used);
                AddMemory(readings, id, "memory-total", "GPU memory total", memoryResult, total);
                AddMemory(readings, id, "memory-used", "GPU memory used", memoryResult, used);
                AddMemory(readings, id, "memory-free", "GPU memory free", memoryResult, free);
                var fanResult = api.FanSpeed(device, out var fanPercent);
                // NVML documents an intended percentage, possibly above 100, not a measured tachometer speed.
                Add(readings, id, "fan-percent", "Fan target speed", "Control", "%", fanResult, fanPercent,
                    notice: "NVML intended fan percentage, not measured RPM. A blocked fan may still report a target speed.");
                devices.Add(new(id, name, pciBusId, readings, deviceNotices.Count == 0 ? null : string.Join(" ", deviceNotices)));
            }
            if (count == 0) { notices.Add("NVML initialized, but no NVIDIA GPU was reported."); }
            statusNotice = notices.Count == 0 ? $"NVIDIA NVML read {devices.Count} GPU(s). Unsupported or inaccessible readings remain unavailable."
                : string.Join(" ", notices);
            return new(devices, statusNotice);
        }
    }

    private static bool ValidText(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && !value.Any(char.IsControl);

    internal static string? NormalizePciBusId(string? value)
    {
        if (!ValidText(value, 32)) { return null; }
        var parts = value!.Split(':');
        if (parts.Length != 3) { return null; }
        var deviceFunction = parts[2].Split('.');
        if (deviceFunction.Length != 2 || !Hex(parts[0], out var domain) || !Hex(parts[1], out var bus)
            || !Hex(deviceFunction[0], out var device) || !Hex(deviceFunction[1], out var function)
            || bus > 255 || device > 31 || function > 7) { return null; }
        return $"{domain:x4}:{bus:x2}:{device:x2}.{function:x}";
    }

    private static bool Hex(string value, out uint number) => uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out number);

    private static void Add(List<SensorReadingDto> readings, string id, string suffix, string name, string type, string unit,
        int result, uint raw, double divisor = 1, bool percent = false, string? notice = null)
    {
        var unavailable = result != 0 ? Describe(result) : raw == uint.MaxValue ? "NVML did not provide this value."
            : percent && raw > 100 ? "NVML returned a percentage outside 0..100." : null;
        readings.Add(new(id + "/" + suffix, name, type, unit, unavailable is null ? raw / divisor : null, null, null,
            unavailable is null ? notice : unavailable));
    }

    private static void AddMemory(List<SensorReadingDto> readings, string id, string suffix, string name, int result, ulong bytes)
    {
        var notice = result != 0 ? Describe(result) : bytes == ulong.MaxValue ? "NVML did not provide this value." : null;
        readings.Add(new(id + "/" + suffix, name, "Data", "GiB", notice is null ? bytes / 1073741824d : null, null, null, notice));
    }

    private static string Describe(int result) => result switch
    {
        3 => "NVML does not support this reading on this GPU.",
        4 => "NVML access was denied for the current user.",
        9 => "The NVIDIA kernel driver is not loaded.",
        12 => "The NVIDIA NVML library is missing.",
        13 => "The installed NVML library does not export this query.",
        15 => "The NVIDIA GPU is no longer accessible.",
        18 => "The NVIDIA driver and NVML library versions do not match.",
        25 => "The installed NVML library does not support this query's structure version.",
        _ => $"NVML returned error {result}."
    };

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) { return; }
            disposed = true;
            try { if (initialized) { _ = api.Shutdown(); } }
            finally { initialized = false; api.Dispose(); }
        }
    }
}

/// <summary>Injectable native boundary for offline checks; no application setters are exposed.</summary>
internal interface ILinuxNvmlApi : IDisposable
{
    int Initialize();
    int Shutdown();
    int DeviceCount(out uint count);
    int DeviceHandle(uint index, out IntPtr handle);
    int DeviceName(IntPtr device, out string value);
    int DeviceUuid(IntPtr device, out string value);
    int DevicePciBusId(IntPtr device, out string value);
    int Utilization(IntPtr device, out uint gpu, out uint memory);
    int Temperature(IntPtr device, out uint value);
    int Power(IntPtr device, out uint value);
    int Clock(IntPtr device, uint type, out uint value);
    int Memory(IntPtr device, out ulong total, out ulong free, out ulong used);
    int FanSpeed(IntPtr device, out uint value);
}

internal sealed class NativeNvmlApi : ILinuxNvmlApi
{
    private IntPtr library;
    private SimpleQuery? shutdown;
    private CountQuery? count;
    private HandleQuery? handle;
    private TextQuery? name, uuid;
    private PciQuery? pci;
    private UtilizationQuery? utilization;
    private TypedUIntQuery? temperature, clock;
    private UIntQuery? power, fan;
    private MemoryQuery? memory;

    public int Initialize()
    {
        if (!OperatingSystem.IsLinux()) { throw new DllNotFoundException("NVML Linux requires Linux."); }
        if (!NativeLibrary.TryLoad("libnvidia-ml.so.1", out library)) { throw new DllNotFoundException("libnvidia-ml.so.1 is missing."); }
        var initialize = Required<SimpleQuery>("nvmlInit_v2");
        shutdown = Required<SimpleQuery>("nvmlShutdown");
        count = Required<CountQuery>("nvmlDeviceGetCount_v2");
        handle = Required<HandleQuery>("nvmlDeviceGetHandleByIndex_v2");
        name = Query<TextQuery>("nvmlDeviceGetName");
        uuid = Query<TextQuery>("nvmlDeviceGetUUID");
        pci = Query<PciQuery>("nvmlDeviceGetPciInfo_v3");
        utilization = Query<UtilizationQuery>("nvmlDeviceGetUtilizationRates");
        temperature = Query<TypedUIntQuery>("nvmlDeviceGetTemperature");
        power = Query<UIntQuery>("nvmlDeviceGetPowerUsage");
        clock = Query<TypedUIntQuery>("nvmlDeviceGetClockInfo");
        memory = Query<MemoryQuery>("nvmlDeviceGetMemoryInfo_v2");
        fan = Query<UIntQuery>("nvmlDeviceGetFanSpeed");
        return initialize();
    }

    private T Required<T>(string export) where T : Delegate => Query<T>(export) ?? throw new EntryPointNotFoundException(export);
    private T? Query<T>(string export) where T : Delegate => NativeLibrary.TryGetExport(library, export, out var address)
        ? Marshal.GetDelegateForFunctionPointer<T>(address) : null;
    public int Shutdown() => shutdown!();
    public int DeviceCount(out uint value) => count!(out value);
    public int DeviceHandle(uint index, out IntPtr value) => handle!(index, out value);
    public int DeviceName(IntPtr device, out string value) => Text(name, device, 256, out value);
    public int DeviceUuid(IntPtr device, out string value) => Text(uuid, device, 96, out value);
    private static int Text(TextQuery? query, IntPtr device, int capacity, out string value)
    {
        var bytes = new byte[capacity];
        var result = query is null ? 13 : query(device, bytes, (uint)bytes.Length);
        var end = Array.IndexOf(bytes, (byte)0);
        value = result == 0 && end >= 0 ? Encoding.UTF8.GetString(bytes, 0, end) : "";
        return result;
    }

    public int DevicePciBusId(IntPtr device, out string value)
    {
        var info = new PciInfo { LegacyBusId = new byte[16], BusId = new byte[32] };
        var result = pci is null ? 13 : pci(device, ref info);
        var end = Array.IndexOf(info.BusId, (byte)0);
        value = result == 0 && end >= 0 ? Encoding.ASCII.GetString(info.BusId, 0, end) : "";
        return result;
    }

    public int Utilization(IntPtr device, out uint gpu, out uint memoryValue)
    {
        var values = new UtilizationInfo();
        var result = utilization is null ? 13 : utilization(device, out values);
        gpu = values.Gpu; memoryValue = values.Memory;
        return result;
    }

    public int Temperature(IntPtr device, out uint value) { value = 0; return temperature is null ? 13 : temperature(device, 0, out value); }
    public int Power(IntPtr device, out uint value) { value = 0; return power is null ? 13 : power(device, out value); }
    public int Clock(IntPtr device, uint type, out uint value) { value = 0; return clock is null ? 13 : clock(device, type, out value); }
    public int FanSpeed(IntPtr device, out uint value) { value = 0; return fan is null ? 13 : fan(device, out value); }
    public int Memory(IntPtr device, out ulong total, out ulong free, out ulong used)
    {
        var values = new MemoryInfo { Version = (uint)Marshal.SizeOf<MemoryInfo>() | 2u << 24 };
        var result = memory is null ? 13 : memory(device, ref values);
        total = values.Total; free = values.Free; used = values.Used;
        return result;
    }

    public void Dispose()
    {
        if (library == IntPtr.Zero) { return; }
        NativeLibrary.Free(library);
        library = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PciInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] LegacyBusId;
        public uint Domain, Bus, Device, PciDeviceId, PciSubSystemId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] BusId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UtilizationInfo { public uint Gpu, Memory; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo { public uint Version; public ulong Total, Reserved, Free, Used; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SimpleQuery();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CountQuery(out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int HandleQuery(uint index, out IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TextQuery(IntPtr device, [Out] byte[] text, uint capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PciQuery(IntPtr device, ref PciInfo info);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int UtilizationQuery(IntPtr device, out UtilizationInfo utilization);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int UIntQuery(IntPtr device, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TypedUIntQuery(IntPtr device, uint type, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int MemoryQuery(IntPtr device, ref MemoryInfo info);
}
