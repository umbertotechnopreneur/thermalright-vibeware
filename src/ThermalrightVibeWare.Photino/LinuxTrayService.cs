/* VBWR B
 * TermalRight VibeWare | Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 * Owns the Linux indicator and startup notification on Photino's GTK thread.
 * VBWR E */

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ThermalrightVibeWare.Photino;

/// <summary>Uses the desktop's existing indicator host and notification service.</summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxTrayService : IDisposable
{
    private readonly int uiThread = Environment.CurrentManagedThreadId;
    private readonly ActivateCallback showCallback, closeCallback;
    private readonly ConnectionCallback connectionCallback;
    private IntPtr indicator, menu, showItem, closeItem;
    private UIntPtr connectionSignal, showSignal, closeSignal;
    private bool disposed;
    public bool Ready { get; private set; }

    /// <summary>Creates a persistent icon without hiding or minimizing the studio.</summary>
    public LinuxTrayService(string iconPath, Action show, Action close)
    {
        if (!File.Exists(iconPath)) { throw new FileNotFoundException("The Linux tray icon is missing.", iconPath); }
        showCallback = (_, _) => InvokeMenu(show);
        closeCallback = (_, _) => InvokeMenu(close);
        connectionCallback = (_, connected, _) => Ready = !disposed && connected != 0;
        try
        {
            indicator = IndicatorNew("thermalright-vibeware", iconPath, 0);
            if (indicator == IntPtr.Zero) { throw new InvalidOperationException("The Linux application indicator could not be created."); }
            connectionSignal = ConnectConnection(indicator, "connection-changed", connectionCallback, IntPtr.Zero, IntPtr.Zero, 0);
            menu = MenuNew();
            if (menu == IntPtr.Zero) { throw new InvalidOperationException("The Linux tray menu could not be created."); }
            _ = RefSink(menu);
            showItem = MenuItemNew("Show studio");
            closeItem = MenuItemNew("Close app");
            if (showItem == IntPtr.Zero || closeItem == IntPtr.Zero) { throw new InvalidOperationException("The Linux tray menu items could not be created."); }
            MenuAppend(menu, showItem);
            MenuAppend(menu, closeItem);
            showSignal = ConnectActivate(showItem, "activate", showCallback, IntPtr.Zero, IntPtr.Zero, 0);
            closeSignal = ConnectActivate(closeItem, "activate", closeCallback, IntPtr.Zero, IntPtr.Zero, 0);
            if (connectionSignal == UIntPtr.Zero || showSignal == UIntPtr.Zero || closeSignal == UIntPtr.Zero)
            { throw new InvalidOperationException("The Linux tray callbacks could not be connected."); }
            WidgetShowAll(menu);
            IndicatorSetMenu(indicator, menu);
            IndicatorSetTitle(indicator, "TermalRight VibeWare");
            IndicatorSetStatus(indicator, 1);
        }
        catch { Dispose(); throw; }
    }

    private void InvokeMenu(Action action)
    {
        if (disposed) { return; }
        try { action(); }
        catch (Exception error) { Console.Error.WriteLine("TermalRight VibeWare tray: " + error.Message); }
    }

    /// <summary>Requests one startup banner after the application reports its loaded UI.</summary>
    public static void NotifyStarted(string iconPath)
    {
        IntPtr notification = IntPtr.Zero, error = IntPtr.Zero;
        try
        {
            if (NotifyInit("TermalRight VibeWare") == 0) { throw new InvalidOperationException("The desktop notification service could not be initialized."); }
            notification = NotificationNew("TermalRight VibeWare started", "The studio is ready.", iconPath);
            if (notification == IntPtr.Zero) { throw new InvalidOperationException("The startup notification could not be created."); }
            NotificationTimeout(notification, 5000);
            if (NotificationShow(notification, out error) == 0)
            { throw new InvalidOperationException("The desktop notification service rejected the startup notification."); }
        }
        catch (Exception failure) when (failure is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        { Console.Error.WriteLine("TermalRight VibeWare notification: " + failure.Message); }
        finally
        {
            if (error != IntPtr.Zero) { ErrorFree(error); }
            if (notification != IntPtr.Zero) { Unref(notification); }
        }
    }

    public void Dispose()
    {
        EnsureUiThread();
        if (disposed) { return; }
        disposed = true;
        Ready = false;
        if (showSignal != UIntPtr.Zero) { Disconnect(showItem, showSignal); }
        if (closeSignal != UIntPtr.Zero) { Disconnect(closeItem, closeSignal); }
        if (connectionSignal != UIntPtr.Zero) { Disconnect(indicator, connectionSignal); }
        if (indicator != IntPtr.Zero) { IndicatorSetStatus(indicator, 0); Unref(indicator); indicator = IntPtr.Zero; }
        if (menu != IntPtr.Zero) { WidgetDestroy(menu); Unref(menu); menu = IntPtr.Zero; }
        GC.KeepAlive(showCallback);
        GC.KeepAlive(closeCallback);
        GC.KeepAlive(connectionCallback);
    }

    private void EnsureUiThread()
    {
        if (Environment.CurrentManagedThreadId != uiThread) { throw new InvalidOperationException("The Linux tray must run on the GTK window thread."); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ActivateCallback(IntPtr widget, IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ConnectionCallback(IntPtr indicator, int connected, IntPtr data);

    [DllImport("libayatana-appindicator3.so.1", EntryPoint = "app_indicator_new")]
    private static extern IntPtr IndicatorNew([MarshalAs(UnmanagedType.LPUTF8Str)] string id, [MarshalAs(UnmanagedType.LPUTF8Str)] string icon, int category);
    [DllImport("libayatana-appindicator3.so.1", EntryPoint = "app_indicator_set_status")]
    private static extern void IndicatorSetStatus(IntPtr indicator, int status);
    [DllImport("libayatana-appindicator3.so.1", EntryPoint = "app_indicator_set_menu")]
    private static extern void IndicatorSetMenu(IntPtr indicator, IntPtr menu);
    [DllImport("libayatana-appindicator3.so.1", EntryPoint = "app_indicator_set_title")]
    private static extern void IndicatorSetTitle(IntPtr indicator, [MarshalAs(UnmanagedType.LPUTF8Str)] string title);
    [DllImport("libgtk-3.so.0", EntryPoint = "gtk_menu_new")]
    private static extern IntPtr MenuNew();
    [DllImport("libgtk-3.so.0", EntryPoint = "gtk_menu_item_new_with_label")]
    private static extern IntPtr MenuItemNew([MarshalAs(UnmanagedType.LPUTF8Str)] string label);
    [DllImport("libgtk-3.so.0", EntryPoint = "gtk_menu_shell_append")]
    private static extern void MenuAppend(IntPtr menu, IntPtr item);
    [DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_show_all")]
    private static extern void WidgetShowAll(IntPtr widget);
    [DllImport("libgtk-3.so.0", EntryPoint = "gtk_widget_destroy")]
    private static extern void WidgetDestroy(IntPtr widget);
    [DllImport("libgobject-2.0.so.0", EntryPoint = "g_object_ref_sink")]
    private static extern IntPtr RefSink(IntPtr value);
    [DllImport("libgobject-2.0.so.0", EntryPoint = "g_object_unref")]
    private static extern void Unref(IntPtr value);
    [DllImport("libgobject-2.0.so.0", EntryPoint = "g_signal_connect_data")]
    private static extern UIntPtr ConnectActivate(IntPtr value, [MarshalAs(UnmanagedType.LPUTF8Str)] string signal, ActivateCallback callback, IntPtr data, IntPtr destroy, uint flags);
    [DllImport("libgobject-2.0.so.0", EntryPoint = "g_signal_connect_data")]
    private static extern UIntPtr ConnectConnection(IntPtr value, [MarshalAs(UnmanagedType.LPUTF8Str)] string signal, ConnectionCallback callback, IntPtr data, IntPtr destroy, uint flags);
    [DllImport("libgobject-2.0.so.0", EntryPoint = "g_signal_handler_disconnect")]
    private static extern void Disconnect(IntPtr value, UIntPtr signal);
    [DllImport("libnotify.so.4", EntryPoint = "notify_init")]
    private static extern int NotifyInit([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport("libnotify.so.4", EntryPoint = "notify_notification_new")]
    private static extern IntPtr NotificationNew([MarshalAs(UnmanagedType.LPUTF8Str)] string summary, [MarshalAs(UnmanagedType.LPUTF8Str)] string body, [MarshalAs(UnmanagedType.LPUTF8Str)] string icon);
    [DllImport("libnotify.so.4", EntryPoint = "notify_notification_set_timeout")]
    private static extern void NotificationTimeout(IntPtr notification, int milliseconds);
    [DllImport("libnotify.so.4", EntryPoint = "notify_notification_show")]
    private static extern int NotificationShow(IntPtr notification, out IntPtr error);
    [DllImport("libglib-2.0.so.0", EntryPoint = "g_error_free")]
    private static extern void ErrorFree(IntPtr error);
}
