/* VBWR B
 * Project: Thermalright VibeWare
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * Purpose: Keeps Linux diagnostic files private to the current user.
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 * VBWR E */

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace ThermalrightVibeWare.Core;

[SupportedOSPlatform("linux")]
internal sealed class LinuxDiagnosticFiles(SafeFileHandle directory) : IDisposable
{
    private const UnixFileMode PrivateDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const int ForeignWrite = (int)(UnixFileMode.GroupWrite | UnixFileMode.OtherWrite);
    private const int FileTypeMask = 0xf000, DirectoryType = 0x4000, RegularType = 0x8000, PermissionsMask = 0xfff;
    private const int CloseOnExec = 1 << 19, PathOnly = 1 << 21;
    private const uint StatFields = 0x0f; // STATX_TYPE | STATX_MODE | STATX_NLINK | STATX_UID

    internal static string GetDirectoryPath()
    {
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!Path.IsPathFullyQualified(data ?? ""))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
            if (!Path.IsPathFullyQualified(home) || Path.GetFullPath(home) == "/") { return ""; }
            data = Path.Combine(home, ".local", "share");
        }
        return Path.Combine(Path.GetFullPath(data!), "ThermalrightVibeWare", "logs");
    }

    internal static LinuxDiagnosticFiles Open(string path)
    {
        if (!Path.IsPathFullyQualified(path)) { throw new IOException("No absolute per-user diagnostics path is available."); }
        try
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = OpenPath("/") ?? throw new IOException("Cannot access the filesystem root.");
            try
            {
                var root = Identity(current);
                if (root.Owner != 0 || (root.Mode & ForeignWrite) != 0)
                { throw new IOException("The filesystem root is not trusted for diagnostic files."); }
                for (var index = 0; index < parts.Length; index++)
                {
                    var child = Path.Combine(DescriptorPath(current), parts[index]);
                    var next = OpenPath(child);
                    if (next is null)
                    {
                        Directory.CreateDirectory(child, PrivateDirectory);
                        next = OpenPath(child) ?? throw new IOException("The diagnostics directory disappeared.");
                    }
                    current.Dispose();
                    current = next;
                    var identity = Identity(current);
                    var privateDirectory = index >= parts.Length - 2;
                    // The app data root may already exist with the user's normal umask.
                    // Restrict only our two private directories after verifying ownership/type.
                    if (privateDirectory && (identity.Mode & FileTypeMask) == DirectoryType && identity.Owner == EffectiveUserId())
                    {
                        RestrictMode(current, identity.Mode, PrivateDirectory);
                        identity = Identity(current);
                    }
                    if ((identity.Mode & FileTypeMask) != DirectoryType || (identity.Mode & ForeignWrite) != 0
                        || (identity.Owner != EffectiveUserId() && (privateDirectory || identity.Owner != 0)))
                    { throw new IOException("The diagnostics path contains an unsafe directory."); }
                    if (privateDirectory) { RestrictMode(current, identity.Mode, PrivateDirectory); }
                }
                // Keep a directory descriptor suitable for flock. The proc link refers to the verified inode.
                var handle = OpenNative(DescriptorPath(current), CloseOnExec);
                if (handle.IsInvalid) { handle.Dispose(); throw new IOException("Cannot open the private diagnostics directory."); }
                return new(handle);
            }
            finally { current.Dispose(); }
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        { throw new IOException("Safe Linux diagnostic file access is unavailable.", error); }
    }

    internal bool ReadEnabled()
    {
        using var settings = ExistingFile("diagnostics.json");
        if (settings is null) { return true; }
        using var stream = File.OpenRead(DescriptorPath(settings));
        var bytes = new byte[257];
        var length = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        if (length > 256) { return false; }
        using var json = JsonDocument.Parse(bytes.AsMemory(0, length));
        return json.RootElement.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True;
    }

    internal void SetEnabled(bool enabled)
    {
        LockForUpdate();
        using var previous = ExistingFile("diagnostics.json");
        var temporary = ChildPath($"diagnostics.{Guid.NewGuid():N}.tmp");
        using (var stream = CreateFile(temporary))
        {
            try
            {
                stream.Write(enabled ? "{\"enabled\":true}\n"u8 : "{\"enabled\":false}\n"u8);
                stream.Flush();
                File.Move(temporary, ChildPath("diagnostics.json"), overwrite: true);
            }
            finally { File.Delete(temporary); }
        }
    }

    internal void Append(byte[] line, long maximum)
    {
        LockForUpdate();
        var logPath = ChildPath("engine.log");
        using var existing = ExistingFile("engine.log");
        if (existing is not null)
        {
            using var stream = File.Open(DescriptorPath(existing), FileMode.Open, FileAccess.Write, FileShare.Read);
            if (stream.Length <= maximum - line.Length)
            {
                stream.Seek(0, SeekOrigin.End);
                stream.Write(line);
                return;
            }
            using var previous = ExistingFile("engine.log.1");
            File.Move(logPath, ChildPath("engine.log.1"), overwrite: true);
        }
        using var output = CreateFile(logPath);
        output.Write(line);
    }

    private SafeFileHandle? ExistingFile(string name)
    {
        var file = OpenPath(ChildPath(name));
        if (file is null) { return null; }
        try
        {
            var identity = Identity(file);
            if ((identity.Mode & FileTypeMask) != RegularType || identity.Owner != EffectiveUserId()
                || identity.Links != 1 || (identity.Mode & ForeignWrite) != 0)
            { throw new IOException("The diagnostics entry is not a safe user-owned regular file."); }
            RestrictMode(file, identity.Mode, PrivateFile);
            return file;
        }
        catch { file.Dispose(); throw; }
    }

    private static FileStream CreateFile(string path) => new(path, new FileStreamOptions
    {
        Mode = FileMode.CreateNew,
        Access = FileAccess.Write,
        Share = FileShare.Read,
        UnixCreateMode = PrivateFile
    });

    private static void RestrictMode(SafeFileHandle handle, ushort mode, UnixFileMode allowed)
    {
        var permissions = (UnixFileMode)(mode & PermissionsMask);
        if ((permissions & allowed) != permissions) { File.SetUnixFileMode(DescriptorPath(handle), permissions & allowed); }
    }

    private void LockForUpdate()
    {
        if (Flock(directory, 2 | 4) != 0) { throw new IOException("The diagnostics directory is busy."); }
    }

    private string ChildPath(string name) => Path.Combine(DescriptorPath(directory), name);
    private static string DescriptorPath(SafeFileHandle handle) => $"/proc/self/fd/{handle.DangerousGetHandle().ToInt64()}";

    private static SafeFileHandle? OpenPath(string path)
    {
        // O_PATH checks identity without opening a FIFO/device for I/O. O_NOFOLLOW is architecture-specific.
        var noFollow = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => 1 << 17,
            Architecture.Arm64 => 1 << 15,
            _ => throw new IOException("Safe diagnostic file access requires Linux x64 or ARM64.")
        };
        var handle = OpenNative(path, PathOnly | noFollow | CloseOnExec);
        if (!handle.IsInvalid) { return handle; }
        var error = Marshal.GetLastPInvokeError();
        handle.Dispose();
        if (error == 2) { return null; } // ENOENT
        throw new IOException("Could not inspect a Linux diagnostics path.");
    }

    private static FileIdentity Identity(SafeFileHandle handle)
    {
        if (Statx(handle, "", 0x1000, StatFields, out var identity) != 0 || (identity.Mask & StatFields) != StatFields)
        { throw new IOException("Cannot verify Linux diagnostic file identity."); }
        return identity;
    }

    // Layout from the Linux statx UAPI, identical on x64 and ARM64.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct FileIdentity
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(16)] public uint Links;
        [FieldOffset(20)] public uint Owner;
        [FieldOffset(28)] public ushort Mode;
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern SafeFileHandle OpenNative(string path, int flags);
    [DllImport("libc", EntryPoint = "statx")]
    private static extern int Statx(SafeFileHandle descriptor, string path, int flags, uint mask, out FileIdentity identity);
    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint EffectiveUserId();
    [DllImport("libc", EntryPoint = "flock")]
    private static extern int Flock(SafeFileHandle handle, int operation);

    public void Dispose() => directory.Dispose();
}
