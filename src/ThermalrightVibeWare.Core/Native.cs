/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: Native.cs
 * Purpose: Declares Windows HID, SetupAPI and WinUSB ABI structures and calls.
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

using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;

namespace ThermalrightVibeWare.Core;

internal static class Native
{
    internal const uint DigcfPresent = 0x00000002;
    internal const uint DigcfAllClasses = 0x00000004;
    internal const uint DigcfDeviceInterface = 0x00000010;
    internal const int ErrorInsufficientBuffer = 122;
    internal const int ErrorNoMoreItems = 259;
    internal const uint GenericRead = 0x80000000;
    internal const uint GenericWrite = 0x40000000;
    internal const uint FileShareRead = 0x00000001;
    internal const uint FileShareWrite = 0x00000002;
    internal const uint OpenExisting = 3;
    internal const uint FileAttributeNormal = 0x00000080;
    internal const uint FileFlagOverlapped = 0x40000000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct HidCaps
    {
        internal ushort Usage;
        internal ushort UsagePage;
        internal ushort InputReportByteLength;
        internal ushort OutputReportByteLength;
        internal ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        internal ushort[] Reserved;
        internal ushort NumberLinkCollectionNodes;
        internal ushort NumberInputButtonCaps;
        internal ushort NumberInputValueCaps;
        internal ushort NumberInputDataIndices;
        internal ushort NumberOutputButtonCaps;
        internal ushort NumberOutputValueCaps;
        internal ushort NumberOutputDataIndices;
        internal ushort NumberFeatureButtonCaps;
        internal ushort NumberFeatureValueCaps;
        internal ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PipeInformation
    {
        internal int PipeType;
        internal byte PipeId;
        internal ushort MaximumPacketSize;
        internal byte Interval;
    }

    /// <summary>Reads the Windows HID interface class identifier.</summary>
    /// <param name="hidGuid">Receives the Windows HID interface class GUID.</param>
    [DllImport("hid.dll")]
    internal static extern void HidD_GetHidGuid(out Guid hidGuid);

    /// <summary>Allocates parsed HID descriptor data for an open interface.</summary>
    /// <param name="deviceHandle">Open native device handle.</param>
    /// <param name="preparsedData">HID descriptor data allocated by Windows and released by HidD_FreePreparsedData.</param>
    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetPreparsedData(SafeFileHandle deviceHandle, out IntPtr preparsedData);

    /// <summary>Releases HID descriptor data allocated by Windows.</summary>
    /// <param name="preparsedData">HID descriptor data allocated by Windows and released by HidD_FreePreparsedData.</param>
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    /// <summary>Reads report sizes and usages from parsed HID descriptor data.</summary>
    /// <param name="preparsedData">HID descriptor data allocated by Windows and released by HidD_FreePreparsedData.</param>
    /// <param name="capabilities">Receives HID usage and report-size capabilities.</param>
    [DllImport("hid.dll")]
    internal static extern int HidP_GetCaps(IntPtr preparsedData, out HidCaps capabilities);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SpDeviceInterfaceData
    {
        internal int cbSize;
        internal Guid InterfaceClassGuid;
        internal int Flags;
        internal IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SpDevinfoData
    {
        internal int cbSize;
        internal Guid ClassGuid;
        internal int DevInst;
        internal IntPtr Reserved;
    }

    /// <summary>Opens a Windows device information set for the supplied class.</summary>
    /// <param name="classGuid">Device setup class GUID or pointer accepted by SetupAPI.</param>
    /// <param name="enumerator">SetupAPI enumerator filter, or a null pointer for no filter.</param>
    /// <param name="parent">Native parent window handle, or zero when no owner is needed.</param>
    /// <param name="flags">Bitwise flags defined by the called Windows API.</param>
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr parent, uint flags);

    /// <summary>Opens a device information set filtered by enumerator.</summary>
    /// <param name="classGuid">Device setup class GUID or pointer accepted by SetupAPI.</param>
    /// <param name="enumerator">SetupAPI enumerator filter, or a null pointer for no filter.</param>
    /// <param name="parent">Native parent window handle, or zero when no owner is needed.</param>
    /// <param name="flags">Bitwise flags defined by the called Windows API.</param>
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetClassDevsW")]
    internal static extern IntPtr SetupDiGetClassDevsForEnumerator(IntPtr classGuid, string enumerator, IntPtr parent, uint flags);

    /// <summary>Releases a Windows device information set.</summary>
    /// <param name="deviceInfoSet">Device information set owned by the caller.</param>
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    /// <summary>Reads one device interface entry by index.</summary>
    /// <param name="deviceInfoSet">Device information set owned by the caller.</param>
    /// <param name="deviceInfoData">Device information entry or optional pointer, according to the declared type.</param>
    /// <param name="interfaceClassGuid">Windows interface class GUID to enumerate.</param>
    /// <param name="memberIndex">Zero-based device or interface enumeration index.</param>
    /// <param name="deviceInterfaceData">Interface metadata structure initialized with its native size.</param>
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

    /// <summary>Reads one device information entry by index.</summary>
    /// <param name="deviceInfoSet">Device information set owned by the caller.</param>
    /// <param name="memberIndex">Zero-based device or interface enumeration index.</param>
    /// <param name="deviceInfoData">Device information entry or optional pointer, according to the declared type.</param>
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SpDevinfoData deviceInfoData);

    /// <summary>Reads a device's Windows instance identifier.</summary>
    /// <param name="deviceInfoSet">Device information set owned by the caller.</param>
    /// <param name="deviceInfoData">Device information entry or optional pointer, according to the declared type.</param>
    /// <param name="deviceInstanceId">Caller-provided buffer receiving the device instance identifier.</param>
    /// <param name="deviceInstanceIdSize">Capacity of the instance identifier buffer in characters.</param>
    /// <param name="requiredSize">Receives the buffer size required by Windows.</param>
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData, StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

    /// <summary>Reads the size or content of an interface's device path.</summary>
    /// <param name="deviceInfoSet">Device information set owned by the caller.</param>
    /// <param name="deviceInterfaceData">Interface metadata structure initialized with its native size.</param>
    /// <param name="deviceInterfaceDetailData">Caller-allocated detail buffer, or zero when querying its required size.</param>
    /// <param name="detailDataSize">Size of the interface detail buffer in bytes.</param>
    /// <param name="requiredSize">Receives the buffer size required by Windows.</param>
    /// <param name="deviceInfoData">Device information entry or optional pointer, according to the declared type.</param>
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint detailDataSize, out uint requiredSize, IntPtr deviceInfoData);

    /// <summary>Opens the selected Windows device path with the supplied access flags.</summary>
    /// <param name="fileName">Exact Windows file or device-interface path to open.</param>
    /// <param name="desiredAccess">Requested Windows file/device access mask.</param>
    /// <param name="shareMode">Allowed sharing flags for the opened file/device handle.</param>
    /// <param name="securityAttributes">Optional security attributes pointer; zero uses defaults.</param>
    /// <param name="creationDisposition">Windows open/create disposition for the selected path.</param>
    /// <param name="flagsAndAttributes">Windows file attributes and asynchronous-I/O flags.</param>
    /// <param name="templateFile">Optional template file handle; zero means none.</param>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    /// <summary>Opens the WinUSB interface associated with a device handle.</summary>
    /// <param name="deviceHandle">Open native device handle.</param>
    /// <param name="interfaceHandle">WinUSB interface handle returned by initialization.</param>
    [DllImport("winusb.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinUsb_Initialize(SafeFileHandle deviceHandle, out IntPtr interfaceHandle);

    /// <summary>Releases an owned WinUSB interface handle.</summary>
    /// <param name="interfaceHandle">WinUSB interface handle returned by initialization.</param>
    [DllImport("winusb.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinUsb_Free(IntPtr interfaceHandle);

    /// <summary>Reads endpoint metadata for an interface's pipe index.</summary>
    /// <param name="interfaceHandle">WinUSB interface handle returned by initialization.</param>
    /// <param name="alternateSetting">WinUSB interface alternate-setting number.</param>
    /// <param name="pipeIndex">Zero-based endpoint index within the interface setting.</param>
    /// <param name="pipeInformation">Receives the endpoint type, address and packet metadata.</param>
    [DllImport("winusb.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinUsb_QueryPipe(IntPtr interfaceHandle, byte alternateSetting, byte pipeIndex, out PipeInformation pipeInformation);

    /// <summary>Sets a policy on the selected WinUSB endpoint.</summary>
    /// <param name="interfaceHandle">WinUSB interface handle returned by initialization.</param>
    /// <param name="pipeId">USB endpoint address selected for the transfer or policy.</param>
    /// <param name="policyType">WinUSB policy selector corresponding to the supplied value.</param>
    /// <param name="valueLength">Size of the policy value in bytes.</param>
    /// <param name="value">Input to validate, convert or format according to this method's contract.</param>
    [DllImport("winusb.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinUsb_SetPipePolicy(IntPtr interfaceHandle, byte pipeId, uint policyType, uint valueLength, ref uint value);

    /// <summary>Writes a buffer to the selected WinUSB endpoint.</summary>
    /// <param name="interfaceHandle">WinUSB interface handle returned by initialization.</param>
    /// <param name="pipeId">USB endpoint address selected for the transfer or policy.</param>
    /// <param name="buffer">Bytes to read or write, bounded by the supplied size when present.</param>
    /// <param name="bufferLength">Number of bytes Windows may read from or write to the buffer.</param>
    /// <param name="lengthTransferred">Receives the actual number of bytes transferred.</param>
    /// <param name="overlapped">Optional OVERLAPPED pointer; zero requests synchronous I/O.</param>
    [DllImport("winusb.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinUsb_WritePipe(IntPtr interfaceHandle, byte pipeId, byte[] buffer, uint bufferLength, out uint lengthTransferred, IntPtr overlapped);

    /// <summary>Reads a bounded response from the selected WinUSB endpoint.</summary>
    /// <param name="interfaceHandle">WinUSB interface handle returned by initialization.</param>
    /// <param name="pipeId">USB endpoint address selected for the transfer or policy.</param>
    /// <param name="buffer">Bytes to read or write, bounded by the supplied size when present.</param>
    /// <param name="bufferLength">Number of bytes Windows may read from or write to the buffer.</param>
    /// <param name="lengthTransferred">Receives the actual number of bytes transferred.</param>
    /// <param name="overlapped">Optional OVERLAPPED pointer; zero requests synchronous I/O.</param>
    [DllImport("winusb.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WinUsb_ReadPipe(IntPtr interfaceHandle, byte pipeId, byte[] buffer, uint bufferLength, out uint lengthTransferred, IntPtr overlapped);
}
