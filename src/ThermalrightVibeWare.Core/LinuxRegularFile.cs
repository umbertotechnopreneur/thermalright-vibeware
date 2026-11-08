/* VBWR B
 * Project: Thermalright VibeWare
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * Purpose: Opens a verified regular Linux file without activating links or devices.
 * Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
 * VBWR E */

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace ThermalrightVibeWare.Core;

[SupportedOSPlatform("linux")]
internal static class LinuxRegularFile
{
    internal static FileStream OpenRead(string path)
    {
        var noFollow = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => 1 << 17,
            Architecture.Arm64 => 1 << 15,
            _ => throw new IOException("Safe manifest reads require Linux x64 or ARM64.")
        };
        try
        {
            // O_PATH obtains an identity without opening a FIFO or device for I/O.
            var descriptor = OpenNative(path, (1 << 21) | (1 << 19) | noFollow);
            if (descriptor < 0) { throw new IOException("Cannot inspect the theme manifest."); }
            using var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
            if (Statx(handle, "", 0x1000, 1, out var identity) != 0 || (identity.Mask & 1) == 0)
            { throw new IOException("Cannot verify the theme manifest file type."); }
            if ((identity.Mode & 0xf000) != 0x8000)
            { throw new IOException("Theme manifests must be regular files, without symbolic links."); }
            // Reopen the pinned inode, so replacing the original path cannot redirect this read.
            return File.OpenRead($"/proc/self/fd/{descriptor}");
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        { throw new IOException("Safe Linux manifest reads are unavailable.", error); }
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct FileIdentity
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(28)] public ushort Mode;
    }

    [DllImport("libc", EntryPoint = "open")]
    private static extern int OpenNative(string path, int flags);
    [DllImport("libc", EntryPoint = "statx")]
    private static extern int Statx(SafeFileHandle descriptor, string path, int flags, uint mask, out FileIdentity identity);
}
