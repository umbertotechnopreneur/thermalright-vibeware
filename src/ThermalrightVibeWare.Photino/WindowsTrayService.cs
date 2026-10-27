/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsTrayService.cs
 * Purpose: Owns the Windows tray icon, menu and native window message callbacks.
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

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ThermalrightVibeWare.Photino;

/// <summary>Owns the Windows tray icon on Photino's existing native UI thread.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsTrayService : IDisposable
{
    private const uint AddIcon = 0;
    private const uint ModifyIcon = 1;
    private const uint DeleteIcon = 2;
    private const uint FocusIcon = 3;
    private const uint SetIconVersion = 4;
    private const uint IconMessage = 1;
    private const uint IconImage = 2;
    private const uint IconTip = 4;
    private const uint IconShowTip = 0x80;
    private const uint IconVersion4 = 4;
    private const uint CallbackMessage = 0x8000 + 0x351;
    private const uint IconSelectMessage = 0x0400;
    private const uint IconKeySelectMessage = 0x0401;
    private const uint ContextMenuMessage = 0x007B;
    private const uint NonClientDestroyMessage = 0x0082;
    private const uint QueryEndSessionMessage = 0x0011;
    private const uint EndSessionMessage = 0x0016;
    private const uint SystemCommandMessage = 0x0112;
    private const uint MinimizeCommand = 0xF020;
    private const uint LoadFromFile = 0x0010;
    private const uint ImageIcon = 1;
    private const uint MenuReturnCommand = 0x0100;
    private const uint MenuNoNotify = 0x0080;
    private const uint MenuRightButton = 0x0002;
    private const uint MenuWorkArea = 0x10000;
    private const uint MenuSeparator = 0x0800;
    private static readonly UIntPtr SubclassId = new(0x5650);
    private static readonly SubclassProcDelegate SubclassProcedure = WindowSubclassProcedure;

    private readonly IntPtr _windowHandle;
    private readonly IntPtr _iconHandle;
    private readonly uint _uiThread;
    private readonly uint _taskbarCreatedMessage;
    private readonly Action _show;
    private readonly Action _closeRequested;
    private readonly Action? _showAbout;
    private readonly Action? _openManifesto;
    private readonly Action? _openWebsite;
    private readonly Action? _openGitHub;
    private GCHandle _selfHandle;
    private bool _subclassInstalled;
    private bool _iconRegistered;
    private bool _disposed;

    public bool IsMaximized => IsZoomed(_windowHandle);

    /// <summary>Checks whether the native window is maximized.</summary>
    /// <param name="window">Native studio window handle.</param>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr window);

    public bool Ready => !_disposed && _iconRegistered;
    public event Action<Exception>? Error;

    /// <summary>Callbacks run on the native UI thread. The show callback may restore and center the window.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    /// <param name="iconPath">Local path of the product icon loaded for the tray.</param>
    /// <param name="show">Restores the existing studio window from its tray icon.</param>
    /// <param name="closeRequested">Opens the studio's explicit close choices.</param>
    /// <param name="showAbout">Optional callback opening the product information view.</param>
    /// <param name="openManifesto">Optional callback opening the VibeWare manifesto.</param>
    /// <param name="openWebsite">Optional callback opening the author's website.</param>
    /// <param name="openGitHub">Optional callback opening the product repository.</param>
    public WindowsTrayService(IntPtr windowHandle, string iconPath, Action show, Action closeRequested,
        Action? showAbout = null, Action? openManifesto = null, Action? openWebsite = null, Action? openGitHub = null)
    {
        if (windowHandle == IntPtr.Zero) { throw new ArgumentException("A window handle is required.", nameof(windowHandle)); }
        ArgumentNullException.ThrowIfNull(show);
        ArgumentNullException.ThrowIfNull(closeRequested);
        if (!File.Exists(iconPath)) { throw new FileNotFoundException("The tray icon is missing.", iconPath); }

        _windowHandle = windowHandle;
        _show = show;
        _closeRequested = closeRequested;
        _showAbout = showAbout;
        _openManifesto = openManifesto;
        _openWebsite = openWebsite;
        _openGitHub = openGitHub;
        _uiThread = GetWindowThreadProcessId(windowHandle, out _);
        if (_uiThread == 0) { throw new Win32Exception(Marshal.GetLastWin32Error(), "The studio window is unavailable."); }
        EnsureUiThread();

        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        if (_taskbarCreatedMessage == 0) { throw new Win32Exception(Marshal.GetLastWin32Error(), "Taskbar registration failed."); }
        _iconHandle = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 0, 0, LoadFromFile);
        if (_iconHandle == IntPtr.Zero) { throw new Win32Exception(Marshal.GetLastWin32Error(), "The tray icon could not be loaded."); }

        try
        {
            _selfHandle = GCHandle.Alloc(this);
            if (!SetWindowSubclass(_windowHandle, SubclassProcedure, SubclassId, GCHandle.ToIntPtr(_selfHandle)))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Tray callbacks could not be installed.");
            }
            _subclassInstalled = true;
            EnsureIconRegistered();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Hide only after Windows has a working tray icon from which the app can be restored.</summary>
    public void HideWindow()
    {
        EnsureUsable();
        EnsureIconRegistered();
        _ = ShowWindow(_windowHandle, 0);
        if (IsWindowVisible(_windowHandle)) { throw new Win32Exception("The studio could not be hidden."); }
    }

    /// <summary>Restore without changing a maximized window to normal size.</summary>
    public void ShowWindow()
    {
        EnsureUsable();
        RestoreWindow();
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        if (_disposed) { return; }
        EnsureUiThread();
        _disposed = true;
        if (_iconRegistered)
        {
            var data = CreateIconData();
            _ = ShellNotifyIcon(DeleteIcon, ref data);
            _iconRegistered = false;
        }
        if (_subclassInstalled)
        {
            _ = RemoveWindowSubclass(_windowHandle, SubclassProcedure, SubclassId);
            _subclassInstalled = false;
        }
        if (_selfHandle.IsAllocated) { _selfHandle.Free(); }
        if (_iconHandle != IntPtr.Zero) { _ = DestroyIcon(_iconHandle); }
    }

    /// <summary>Rejects operations after tray resources have been disposed.</summary>
    private void EnsureUsable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureUiThread();
    }

    /// <summary>Rejects tray operations outside the owning Windows thread.</summary>
    private void EnsureUiThread()
    {
        if (GetCurrentThreadId() != _uiThread)
        {
            throw new InvalidOperationException("Tray operations must run on the studio UI thread.");
        }
    }

    /// <summary>Restores and centers the existing studio window.</summary>
    private void RestoreWindow()
    {
        _ = ShowWindow(_windowHandle, IsIconic(_windowHandle) ? 9 : 5);
        _ = SetForegroundWindow(_windowHandle);
    }

    /// <summary>Registers the notification-area icon if it is not present.</summary>
    private void EnsureIconRegistered()
    {
        var data = CreateIconData();
        if (_iconRegistered && ShellNotifyIcon(ModifyIcon, ref data)) { return; }
        _iconRegistered = false;
        if (!ShellNotifyIcon(AddIcon, ref data))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The tray icon could not be added.");
        }
        _iconRegistered = true;
        // The shell forgets this setting when its taskbar restarts, so set it after every add.
        if (!ShellNotifyIcon(SetIconVersion, ref data))
        {
            var error = Marshal.GetLastWin32Error();
            _ = ShellNotifyIcon(DeleteIcon, ref data);
            _iconRegistered = false;
            throw new Win32Exception(error, "The tray icon could not be configured.");
        }
    }

    /// <summary>Shows the tray menu at the requested screen position.</summary>
    /// <param name="packedPosition">Packed screen coordinates supplied with the tray context-menu message.</param>
    private void ShowContextMenu(UIntPtr packedPosition)
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) { throw new Win32Exception(Marshal.GetLastWin32Error(), "The tray menu could not be created."); }
        uint selection;
        try
        {
            AddMenuItem(menu, 1, "Open Thermalright VibeWare");
            if (_showAbout is not null) { AddMenuItem(menu, 2, "About Thermalright VibeWare"); }
            if (_openManifesto is not null || _openWebsite is not null || _openGitHub is not null)
            {
                AddSeparator(menu);
                if (_openManifesto is not null) { AddMenuItem(menu, 3, "VibeWare manifesto"); }
                if (_openWebsite is not null) { AddMenuItem(menu, 4, "Website"); }
                if (_openGitHub is not null) { AddMenuItem(menu, 5, "GitHub"); }
            }
            AddSeparator(menu);
            AddMenuItem(menu, 6, "Exit");
            // Version 4 gives the notification icon anchor in wParam, including keyboard activation.
            var packed = unchecked((uint)packedPosition.ToUInt64());
            var point = new Point { X = unchecked((short)(packed & 0xFFFF)), Y = unchecked((short)(packed >> 16)) };
            if (point.X == -1 && point.Y == -1 && !GetCursorPos(out point))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The tray menu position is unavailable.");
            }
            _ = SetForegroundWindow(_windowHandle);
            selection = TrackPopupMenuEx(menu, MenuReturnCommand | MenuNoNotify | MenuRightButton | MenuWorkArea,
                point.X, point.Y, _windowHandle, IntPtr.Zero);
        }
        finally
        {
            _ = DestroyMenu(menu);
            _ = PostMessage(_windowHandle, 0, IntPtr.Zero, IntPtr.Zero);
        }

        if (selection == 1) { _show(); }
        else if (selection == 2) { _showAbout?.Invoke(); }
        else if (selection == 3) { _openManifesto?.Invoke(); }
        else if (selection == 4) { _openWebsite?.Invoke(); }
        else if (selection == 5) { _openGitHub?.Invoke(); }
        else if (selection == 6) { _closeRequested(); }
        else if (!_disposed && _iconRegistered)
        {
            // Restore notification-area keyboard focus after the menu is cancelled.
            var data = CreateIconData();
            _ = ShellNotifyIcon(FocusIcon, ref data);
        }
    }

    /// <summary>Appends an actionable item to the native tray menu.</summary>
    /// <param name="menu">Native popup-menu handle owned by the caller.</param>
    /// <param name="id">Native menu command or callback identifier.</param>
    /// <param name="label">Visible menu label or reading label.</param>
    private static void AddMenuItem(IntPtr menu, uint id, string label)
    {
        if (!AppendMenu(menu, 0, new UIntPtr(id), label))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The tray menu could not be populated.");
        }
    }

    /// <summary>Appends a separator to the native tray menu.</summary>
    /// <param name="menu">Native popup-menu handle owned by the caller.</param>
    private static void AddSeparator(IntPtr menu)
    {
        if (!AppendMenu(menu, MenuSeparator, UIntPtr.Zero, string.Empty))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The tray menu divider could not be added.");
        }
    }

    /// <summary>Creates the shell notification structure for this studio window.</summary>
    private IconData CreateIconData() => new()
    {
        Size = Marshal.SizeOf<IconData>(),
        WindowHandle = _windowHandle,
        Id = 1,
        Flags = IconMessage | IconImage | IconTip | IconShowTip,
        CallbackMessage = CallbackMessage,
        IconHandle = _iconHandle,
        ToolTip = "Thermalright VibeWare",
        Info = string.Empty,
        InfoTitle = string.Empty,
        TimeoutOrVersion = IconVersion4
    };

    /// <summary>Routes tray and window messages to the owning studio instance.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    /// <param name="message">Windows message identifier used to interpret wParam and lParam.</param>
    /// <param name="wParam">Windows message word parameter, interpreted by the message identifier.</param>
    /// <param name="lParam">Windows message long parameter, interpreted by the message identifier.</param>
    /// <param name="subclassId">Identifier used to add/remove this window subclass callback.</param>
    /// <param name="referenceData">Native callback context retained by the window subclass.</param>
    private static IntPtr WindowSubclassProcedure(IntPtr windowHandle, uint message,
        UIntPtr wParam, IntPtr lParam, UIntPtr subclassId, IntPtr referenceData)
    {
        if (referenceData != IntPtr.Zero &&
            GCHandle.FromIntPtr(referenceData).Target is WindowsTrayService service && !service._disposed)
        {
            if (message == NonClientDestroyMessage)
            {
                service.Dispose();
                return DefSubclassProc(windowHandle, message, wParam, lParam);
            }
            try
            {
                if (message == QueryEndSessionMessage) { return new IntPtr(1); }
                if (message == EndSessionMessage && wParam != UIntPtr.Zero) { service._closeRequested(); return IntPtr.Zero; }
                if (message == service._taskbarCreatedMessage)
                {
                    service._iconRegistered = false;
                    service.EnsureIconRegistered();
                }
                else if (message == SystemCommandMessage && (wParam.ToUInt64() & 0xFFF0) == MinimizeCommand)
                {
                    service.HideWindow();
                    return IntPtr.Zero;
                }
                else if (message == CallbackMessage)
                {
                    var activation = unchecked((uint)lParam.ToInt64()) & 0xFFFF;
                    if (activation is IconSelectMessage or IconKeySelectMessage) { service._show(); }
                    else if (activation == ContextMenuMessage) { service.ShowContextMenu(wParam); }
                    return IntPtr.Zero;
                }
            }
            catch (Exception error)
            {
                // Never let a managed exception cross the native callback. Keep a failed tray visible.
                if (!service._disposed) { service.RestoreWindow(); }
                try { service.Error?.Invoke(error); }
                catch (Exception) { Console.Error.WriteLine("Thermalright VibeWare: a tray action failed."); }
            }
        }
        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct IconData
    {
        public int Size;
        public IntPtr WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string ToolTip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid GuidItem;
        public IntPtr BalloonIconHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr SubclassProcDelegate(IntPtr windowHandle, uint message,
        UIntPtr wParam, IntPtr lParam, UIntPtr subclassId, IntPtr referenceData);

    /// <summary>Returns the calling Windows thread identifier.</summary>
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    /// <summary>Reads the owning thread and process of a native window.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    /// <param name="processId">Receives the owning process identifier from Windows.</param>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    /// <summary>Registers a named Windows message shared with Explorer.</summary>
    /// <param name="message">User-facing text or registered Windows message name used by this call.</param>
    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string message);

    /// <summary>Adds, updates or removes the notification-area icon.</summary>
    /// <param name="message">Windows message identifier used to interpret wParam and lParam.</param>
    /// <param name="data">Notification icon structure read by the Windows shell.</param>
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint message, ref IconData data);

    /// <summary>Loads the configured product icon from a local file.</summary>
    /// <param name="instance">Native module instance handle, or zero when loading an icon from file.</param>
    /// <param name="path">Local settings, manifest or activity JSON file path used by this operation.</param>
    /// <param name="type">Native API type/flag selector for the requested operation.</param>
    /// <param name="width">Width in viewport units or panel pixels, according to the declared type and operation.</param>
    /// <param name="height">Height in viewport units or panel pixels, according to the declared type and operation.</param>
    /// <param name="flags">Bitwise flags defined by the called Windows API.</param>
    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string path, uint type, int width, int height, uint flags);

    /// <summary>Releases an owned native icon handle.</summary>
    /// <param name="icon">Native icon handle to release.</param>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    /// <summary>Checks the native window's visibility flag.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    /// <summary>Checks whether the native window is minimized.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr windowHandle);

    /// <summary>Applies a native window show/restore command.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    /// <param name="command">Windows window show/restore command.</param>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    /// <summary>Requests foreground activation for the studio window.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    /// <summary>Allocates a native popup menu.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    /// <summary>Appends a command or separator to a native menu.</summary>
    /// <param name="menu">Native popup-menu handle owned by the caller.</param>
    /// <param name="flags">Bitwise flags defined by the called Windows API.</param>
    /// <param name="id">Native menu command or callback identifier.</param>
    /// <param name="label">Visible menu label or reading label.</param>
    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string label);

    /// <summary>Shows a native popup menu and returns the selected command.</summary>
    /// <param name="menu">Native popup-menu handle owned by the caller.</param>
    /// <param name="flags">Bitwise flags defined by the called Windows API.</param>
    /// <param name="x">Horizontal screen coordinate in pixels.</param>
    /// <param name="y">Vertical screen coordinate in pixels.</param>
    /// <param name="ownerWindow">Native owner window for the popup menu.</param>
    /// <param name="parameters">Request payload serialized with the versioned contract; null when no payload is needed.</param>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr ownerWindow, IntPtr parameters);

    /// <summary>Reads the current cursor position in screen coordinates.</summary>
    /// <param name="point">Receives the current cursor position in screen pixels.</param>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    /// <summary>Releases an owned native menu handle.</summary>
    /// <param name="menu">Native popup-menu handle owned by the caller.</param>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    /// <summary>Posts a message to the window's owning thread.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    /// <param name="message">Windows message identifier used to interpret wParam and lParam.</param>
    /// <param name="wParam">Windows message word parameter, interpreted by the message identifier.</param>
    /// <param name="lParam">Windows message long parameter, interpreted by the message identifier.</param>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    /// <summary>Installs the studio's native window message callback.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    /// <param name="procedure">Rooted native window subclass callback.</param>
    /// <param name="subclassId">Identifier used to add/remove this window subclass callback.</param>
    /// <param name="referenceData">Native callback context retained by the window subclass.</param>
    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr windowHandle, SubclassProcDelegate procedure, UIntPtr subclassId, IntPtr referenceData);

    /// <summary>Removes the studio's native window message callback.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    /// <param name="procedure">Rooted native window subclass callback.</param>
    /// <param name="subclassId">Identifier used to add/remove this window subclass callback.</param>
    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr windowHandle, SubclassProcDelegate procedure, UIntPtr subclassId);

    /// <summary>Forwards unhandled messages through the window subclass chain.</summary>
    /// <param name="windowHandle">Native handle of the studio window.</param>
    /// <param name="message">Windows message identifier used to interpret wParam and lParam.</param>
    /// <param name="wParam">Windows message word parameter, interpreted by the message identifier.</param>
    /// <param name="lParam">Windows message long parameter, interpreted by the message identifier.</param>
    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr windowHandle, uint message, UIntPtr wParam, IntPtr lParam);
}
