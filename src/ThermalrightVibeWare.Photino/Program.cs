/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: Program.cs
 * Purpose: Starts the single desktop studio with setup gating, elevation and memory protection.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Modified with AI: OpenAI Codex; documentation, refactoring and first-launch policy; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard file headers.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */

using System.Drawing;
using OpenSilver.Photino;
using Photino.NET;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Studio;

namespace ThermalrightVibeWare.Photino;

/// <summary>Starts one studio window sized to the current monitor's work area.</summary>
internal static class Program
{
    /// <summary>Connect the local engine client and open the desktop presentation.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    [STAThread]
    private static int Main(string[] args)
    {
        if (OperatingSystem.IsWindows() && args.FirstOrDefault() == WindowsMemoryGuard.Argument)
        { return WindowsMemoryGuard.Run(args); }
        if (args.FirstOrDefault() == WindowsElevation.Argument)
        {
            if (!OperatingSystem.IsWindows() || !WindowsElevation.WaitForPreviousStudio(args)) { return 2; }
            args = args.Skip(3).ToArray();
        }
        if (args.Any(argument => argument is not "--start-minimized" and not "--restore-last-session"))
        { Console.Error.WriteLine("Usage: ThermalrightVibeWare.exe [--start-minimized] [--restore-last-session]"); return 2; }
        var restoreLastSession = OperatingSystem.IsWindows();
        var requestedTrayStart = args.Contains("--start-minimized") || args.Contains("--restore-last-session");
        if (OperatingSystem.IsWindows() && WindowsSetupGate.IsInstalling()) { if (!requestedTrayStart) { WindowsSetupGate.ShowBusy(); } return 3; }
        // Request elevation before creating the job, engine, WebView or single-instance objects.
        // The child waits for this launcher to exit and keeps the sign-in/tray arguments.
        if (OperatingSystem.IsWindows() && WindowsElevation.StartAdministrator(args)) { return 0; }
        SingleInstanceService instance;
        try { instance = new SingleInstanceService(); }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        { WindowsElevation.ShowSessionUnavailable(); return 1; }
        using var instanceLifetime = instance;
        if (!instance.IsPrimary)
        {
            if (!requestedTrayStart) { instance.RequestActivation(); }
            return 0;
        }

        using var memoryGuard = StartMemoryGuard();
        if (OperatingSystem.IsWindows() && memoryGuard is null) { return 1; }
        using var client = new StdioPanelClient();
        using var session = new StudioSession(client, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThermalrightVibeWare", "presentation.json"), restoreLastSession);
        // First launch always shows the studio, including a launch from the sign-in shortcut.
        // Later Windows launches start in the tray; the icon and activation channel can reopen it.
        var isFirstRun = session.Model.Preferences.IsFirstRun;
        var startMinimized = OperatingSystem.IsWindows() && !isFirstRun;
        PhotinoWindow? window = null;
        WindowsTrayService? tray = null;
        WindowsSetupShutdown? setupShutdown = null;
        App.App? application = null;
        PhotinoPreview? preview = null;
        var exiting = 0;
        var actions = new StudioWindowActions(
            () => OperatingSystem.IsWindows() && tray is { Ready: true },
            () => OperatingSystem.IsWindows() && memoryGuard is not null && !WindowsElevation.IsAdministrator,
            () => OperatingSystem.IsWindows() && memoryGuard is not null
                ? memoryGuard.RestartAsAdministratorAsync() : Task.FromResult(false),
            () => window?.Invoke(() => { if (OperatingSystem.IsWindows()) { tray?.HideWindow(); } }),
            () => window?.Invoke(() =>
            {
                if (Interlocked.Exchange(ref exiting, 1) != 0) { return; }
                session.Save();
                if (OperatingSystem.IsWindows()) { tray?.Dispose(); }
                window?.Close();
            }));

        // Restore the existing instance on the native UI thread; do not create another studio.
        void ShowStudio()
        {
            if (Volatile.Read(ref exiting) != 0 || window is null) { return; }
            window.Invoke(() =>
            {
                if (Volatile.Read(ref exiting) != 0) { return; }
                if (OperatingSystem.IsWindows() && tray is { Ready: true })
                {
                    tray.ShowWindow();
                    if (!tray.IsMaximized) { CenterOnWorkArea(window); }
                }
                else { window.SetMinimized(false); CenterOnWorkArea(window); }
            });
        }

        var page = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        window = new PhotinoWindow()
            .SetTitle("Thermalright VibeWare")
            // The installer owns the Start entry; Photino toasts create another one.
            .SetNotificationsEnabled(false)
            .SetUseOsDefaultSize(false)
            .SetUseOsDefaultLocation(false)
            .SetSize(1400, 940)
            .SetResizable(true)
            .SetMinimized(startMinimized && OperatingSystem.IsWindows())
            .SetLogVerbosity(0)
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "Assets", OperatingSystem.IsWindows() ? "thermalright-vibeware.ico" : "thermalright-vibeware-icon.png"))
            .Center()
            .ConfigureOpenSilver(() => application = new App.App(client, actions, preview: preview = new PhotinoPreview(window!), restoreLastSession: restoreLastSession, session: session))
            .RegisterWindowClosingHandler((_, _) =>
            {
                if (Volatile.Read(ref exiting) != 0 || application is null) { return false; }
                ShowStudio();
                application.RequestClose();
                return true;
            })
            .RegisterWindowCreatedHandler((sender, _) =>
            {
                var created = (PhotinoWindow)sender!;
                FitToWorkArea(created);
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        tray = new WindowsTrayService(created.WindowHandle, Path.Combine(AppContext.BaseDirectory, "Assets", "thermalright-vibeware.ico"),
                            ShowStudio,
                            () => actions.CloseApplication(),
                            showAbout: () => { ShowStudio(); application?.RequestAbout(); },
                            openManifesto: () => OpenWebsite("https://umbertogiacobbi.biz/vibeware/manifesto"),
                            openWebsite: () => OpenWebsite("https://umbertogiacobbi.biz"),
                            openGitHub: () => OpenWebsite("https://github.com/umbertotechnopreneur/thermalright-vibeware"));
                        tray.Error += error => Console.Error.WriteLine("Thermalright VibeWare tray: " + error.Message);
                        if (startMinimized) { tray.HideWindow(); }
                        else if (isFirstRun) { tray.ShowWindow(); }
                    }
                    catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
                    { Console.Error.WriteLine("Thermalright VibeWare tray: " + error.Message); created.SetMinimized(false); }
                    try
                    {
                        setupShutdown = new WindowsSetupShutdown(action =>
                        {
                            if (Volatile.Read(ref exiting) == 0) { created.Invoke(action); }
                        }, () =>
                        {
                            if (Volatile.Read(ref exiting) != 0) { return; }
                            actions.CloseApplication();
                        });
                    }
                    catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
                    { Console.Error.WriteLine("Thermalright VibeWare setup: shutdown listener unavailable: " + error.Message); }
                }
                instance.Listen(action =>
                {
                    if (Volatile.Read(ref exiting) == 0) { created.Invoke(action); }
                }, ShowStudio);
            })
            .Load(page);
        // Native visibility is advisory only. The session never waits for the UI.
        using var visibility = new System.Threading.Timer(_ => session.PreviewVisible = preview?.IsVisible == true, null, 0, 250);
        try { window.WaitForClose(); }
        finally
        {
            Interlocked.Exchange(ref exiting, 1);
            preview?.Dispose();
            if (OperatingSystem.IsWindows()) { setupShutdown?.Dispose(); tray?.Dispose(); }
        }
        return 0;
    }

    /// <summary>Starts the required guard or reports why startup cannot continue.</summary>
    private static WindowsMemoryGuard.Session? StartMemoryGuard()
    {
        if (!OperatingSystem.IsWindows()) { return null; }
        try { return WindowsMemoryGuard.Start(); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException or OperationCanceledException)
        { WindowsMemoryGuard.ShowUnavailable(); return null; }
    }

    /// <summary>Reopen an existing studio centered without discarding its chosen size.</summary>
    /// <param name="window">Existing studio host window to size or center.</param>
    private static void CenterOnWorkArea(PhotinoWindow window)
    {
        var monitors = window.Monitors;
        if (monitors.Count == 0) { return; }
        var center = new Point(window.Location.X + window.Size.Width / 2, window.Location.Y + window.Size.Height / 2);
        var monitor = monitors[0];
        foreach (var candidate in monitors)
        {
            if (candidate.MonitorArea.Contains(center)) { monitor = candidate; break; }
        }
        var area = monitor.WorkArea;
        if (area.Width <= 0 || area.Height <= 0) { return; }
        window.SetLocation(new Point(area.Left + Math.Max(0, (area.Width - window.Size.Width) / 2),
            area.Top + Math.Max(0, (area.Height - window.Size.Height) / 2)));
    }

    /// <summary>Adapts the native host's window and guarded-restart actions for the portable view.</summary>
    /// <param name="canMinimize">Reads whether the tray is available for this instance.</param>
    /// <param name="canRestart">Reads whether a guarded administrator restart is available.</param>
    /// <param name="restart">Requests the guarded administrator restart and reports its result.</param>
    /// <param name="minimize">Hides the existing window in its tray.</param>
    /// <param name="close">Requests normal host shutdown.</param>
    private sealed class StudioWindowActions(Func<bool> canMinimize, Func<bool> canRestart, Func<Task<bool>> restart, Action minimize, Action close) : IStudioWindowActions
    {
        public bool CanMinimizeToTray => canMinimize();
        public bool CanRestartAsAdministrator => canRestart();

        /// <summary>Requests a guarded restart with administrator rights.</summary>
        public Task<bool> RestartAsAdministratorAsync() => restart();

        /// <summary>Delegates studio minimization to the desktop host.</summary>
        public void MinimizeToTray() => minimize();

        /// <summary>Delegates application shutdown to the desktop host.</summary>
        public void CloseApplication() => close();
    }

    /// <summary>Opens an explicitly requested website with the default browser.</summary>
    /// <param name="url">Explicit URL to open or query, according to this method's role.</param>
    private static void OpenWebsite(string url)
    {
        using var browser = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }

    /// <summary>Choose the initial window size once, while respecting display scaling and taskbars.</summary>
    /// <param name="window">Existing studio host window to size or center.</param>
    private static void FitToWorkArea(PhotinoWindow window)
    {
        // Monitor information is available only after Photino creates its native window.
        // Run once at startup, never recenter while the user moves or resizes the window.
        try
        {
            var monitors = window.Monitors;
            if (monitors.Count == 0) { return; }
            var currentCenter = new Point(window.Location.X + window.Size.Width / 2, window.Location.Y + window.Size.Height / 2);
            var monitor = monitors[0];
            foreach (var candidate in monitors)
            {
                if (candidate.MonitorArea.Contains(currentCenter)) { monitor = candidate; break; }
            }
            var area = monitor.WorkArea;
            if (area.Width <= 0 || area.Height <= 0) { return; }
            // Windows reports physical pixels: preserve usable space at larger display scaling too.
            var scale = OperatingSystem.IsWindows() ? Math.Clamp(window.ScreenDpi / 96d, 1, 3) : 1;
            // Cap the preferred size, rather than making a large screen fill up to 90%.
            var width = Math.Max(1, Math.Min((int)(1400 * scale), (int)(area.Width * .92)));
            var height = Math.Max(1, Math.Min((int)(940 * scale), (int)(area.Height * .92)));
            window.SetMinSize(Math.Min((int)(1040 * scale), width), Math.Min((int)(640 * scale), height));
            window.SetSize(width, height);
            window.SetLocation(new Point(area.Left + (area.Width - width) / 2, area.Top + (area.Height - height) / 2));
        }
        catch (Exception error) when (error is ApplicationException or InvalidOperationException or ArgumentException or NotSupportedException)
        { Console.Error.WriteLine("Thermalright VibeWare: automatic window placement unavailable."); }
    }
}
