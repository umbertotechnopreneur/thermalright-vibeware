/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsSetupGate.cs
 * Purpose: Prevents studio startup while the current user's installer changes files.
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

using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ThermalrightVibeWare.Photino;

[SupportedOSPlatform("windows")]
internal static class WindowsSetupGate
{

    /// <summary>Checks the current user's setup gate before allowing a studio launch.</summary>
    internal static bool IsInstalling()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var handle = OpenEvent(0x00100000, false, @"Global\UmbertoGiacobbi.ThermalrightVibeWare.SetupGate." + identity.User!.Value);
        if (handle == IntPtr.Zero) { return Marshal.GetLastWin32Error() != 2; }
        try { return WaitForSingleObject(handle, 0) != 258; }
        finally { CloseHandle(handle); }
    }

    /// <summary>Shows why the studio cannot start while setup is changing files.</summary>
    internal static void ShowBusy() => MessageBox(IntPtr.Zero,
        "Thermalright VibeWare setup is running. Open the app after installation or removal finishes.", "Thermalright VibeWare", 0x40);

    /// <summary>Opens an existing named Windows event.</summary>
    /// <param name="access">Requested access mask for the named Windows event.</param>
    /// <param name="inherit">Whether child processes may inherit the opened handle.</param>
    /// <param name="name">Name to match, display or resolve in this operation.</param>
    [DllImport("kernel32.dll", EntryPoint = "OpenEventW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenEvent(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, string name);

    /// <summary>Waits for a native object's signal within the supplied deadline.</summary>
    /// <param name="handle">Owned native Windows handle.</param>
    /// <param name="milliseconds">Elapsed operation duration or native wait timeout in milliseconds.</param>
    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    /// <summary>Releases an owned Windows kernel handle.</summary>
    /// <param name="handle">Owned native Windows handle.</param>
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>Shows a native Windows message dialog.</summary>
    /// <param name="owner">Native owner window for the message dialog.</param>
    /// <param name="text">Text to format, normalize or display in this operation.</param>
    /// <param name="title">Dialog title shown by Windows.</param>
    /// <param name="type">Native API type/flag selector for the requested operation.</param>
    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string title, uint type);
}
