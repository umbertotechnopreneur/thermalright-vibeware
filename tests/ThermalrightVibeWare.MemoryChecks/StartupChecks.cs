// VBWR B
// Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
// Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
// VBWR E

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using OpenSilver.Photino;
using Photino.NET;
using ThermalrightVibeWare.App;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Photino;

internal static class StartupChecks
{
    internal static int Run()
    {
        var output = Path.Combine(AppContext.BaseDirectory, "startup-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        using var guard = WindowsMemoryGuard.Start();
        using var lifetime = new CancellationTokenSource();
        var firstClient = new Program.FixtureClient();
        var exitCode = 1;
        var started = 0;
        PhotinoWindow? window = null;
        PhotinoPreview? preview = null;
        window = new PhotinoWindow().SetTitle("Thermalright VibeWare - offline startup checks")
            .SetLogVerbosity(0).SetSize(1400, 940).SetMinimized(true)
            .RegisterWindowCreatedHandler((sender, _) => ShowWindow(((PhotinoWindow)sender!).WindowHandle, 0))
            .ConfigureOpenSilver(() =>
            {
                preview = new PhotinoPreview(window!);
                var app = new App(firstClient, presentationSettingsPath: Settings(output), preview: preview, restoreLastSession: true);
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += async (_, _) =>
                {
                    if (firstClient.Frames == 0 || Interlocked.Exchange(ref started, 1) != 0) { return; }
                    timer.Stop();
                    try { await ExerciseAsync(app, firstClient, preview, window!, output); exitCode = 0; }
                    catch (Exception error) { Console.Error.WriteLine(error); }
                    finally { window!.Invoke(() => window.Close()); }
                };
                timer.Start();
                return app;
            }).Load(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(90), lifetime.Token);
                Console.Error.WriteLine("Offline startup checks timed out.");
                window.Invoke(() => window.Close());
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        });
        try { window.WaitForClose(); }
        finally { lifetime.Cancel(); preview?.Dispose(); }
        return exitCode;
    }

    private static async Task ExerciseAsync(App app, Program.FixtureClient first, PhotinoPreview preview, PhotinoWindow window, string output)
    {
        var count = 0;
        void Check(bool value, string name)
        {
            if (!value) { throw new InvalidOperationException(name); }
            count++;
            Console.WriteLine($"Startup check {count}: {name}");
        }
        async Task<MainPage> Page(Program.FixtureClient client, bool restore = true, string? theme = "fixture-3", string? device = "offline-fixture", IStudioWindowActions? actions = null)
        {
            var page = new MainPage(client, windowActions: actions, presentationSettingsPath: Settings(output, theme, device), preview: preview, restoreLastSession: restore);
            app.RootVisual = page;
            // A hidden preview now intentionally produces no frames. Catalog and
            // controls still initialize while the independent startup worker runs.
            await WaitAsync(() => Ready(page) && client.FontRequests > 0);
            return page;
        }
        async Task Poll(MainPage page)
        {
            await (Task)typeof(MainPage).GetMethod("RefreshDevicesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
        }
        await WaitAsync(() => Ready((MainPage)app.RootVisual) && first.PresentRequests >= 2);
        Check(first.StartRequests == 1 && first.LastStart is { DeviceId: "offline-fixture", ThemeId: "fixture-3", ProfileId: "auto" }, "hidden startup starts the exact saved device and theme once");
        Check(first.LastPresent is { FontFamily: "Consolas", FontScale: 1.1, RefreshIntervalMilliseconds: 1000, JpegQuality: "low", SaturationPercent: 160, HueDegrees: 10, BrightnessPercent: 10, ContrastPercent: 80, ShowSpectrum: false, ColorFiltersEnabled: true }, "hidden output uses saved render settings");

        var normal = new Program.FixtureClient();
        var normalPage = await Page(normal, restore: false);
        await Poll(normalPage);
        Check(normal.StartRequests == 0 && normal.PresentRequests == 0, "normal launches remain preview-only");

        var late = new Program.FixtureClient { Devices = [new("other-screen", "Other offline screen")] };
        var latePage = await Page(late);
        await WaitAsync(() => late.DeviceRequests >= 2, TimeSpan.FromSeconds(16));
        Check(late.StartRequests == 0, "the real ten-second timer never substitutes another screen");
        late.Devices = [new("offline-fixture", "Saved offline screen")];
        await Poll(latePage);
        // An earlier preview can still be finishing when start returns; the queued present follows it.
        Console.WriteLine($"Late-device transition: starts={late.StartRequests}, presents={late.PresentRequests}");
        await WaitAsync(() => Ready(latePage) && late.StartRequests == 1 && late.PresentRequests > 0);
        Check(late.StartRequests == 1 && late.PresentRequests > 0, "a late saved device starts without UI interaction");
        await Poll(latePage);
        Check(late.StartRequests == 1, "later polls cannot duplicate startup");
        late.Devices = [];
        await Poll(latePage);
        late.Devices = [new("offline-fixture", "Saved offline screen")];
        await Poll(latePage);
        Check(late.StartRequests == 1, "a disconnected session is not automatically restarted");

        var stopped = new Program.FixtureClient { Devices = [] };
        var stoppedPage = await Page(stopped);
        var stop = (Button)stoppedPage.FindName("StopDisplayButton");
        Check(stop.IsEnabled, "Stop is available while startup waits for USB");
        ((IInvokeProvider)new ButtonAutomationPeer(stop).GetPattern(PatternInterface.Invoke)).Invoke();
        await WaitAsync(() => stopped.StopRequests == 1 && Ready(stoppedPage));
        stopped.Devices = [new("offline-fixture", "Saved offline screen")];
        await Poll(stoppedPage);
        Check(stopped.StartRequests == 0, "Stop cancels the pending startup attempt");

        var failed = new Program.FixtureClient { RejectStart = true };
        var failedPage = await Page(failed);
        await Poll(failedPage);
        await Poll(failedPage);
        Check(failed.StartRequests == 1 && failed.PresentRequests == 0, "a failed start is never replayed");

        var missingTheme = new Program.FixtureClient();
        var missingPage = await Page(missingTheme, theme: "removed-theme");
        await Poll(missingPage);
        Check(missingTheme.StartRequests == 0, "a missing saved theme cannot silently start the first theme");

        var unconfigured = new Program.FixtureClient();
        var unconfiguredPage = await Page(unconfigured, device: null);
        await Poll(unconfiguredPage);
        Check(unconfigured.StartRequests == 0, "an unconfigured startup never chooses the only available screen");

        var closing = new Program.FixtureClient();
        var actions = new ClosingActions();
        var closingPage = await Page(closing, restore: false, actions: actions);
        ((ListBox)closingPage.FindName("ThemesBox")).SelectedIndex = 7;
        ((ComboBox)closingPage.FindName("QualityBox")).SelectedIndex = 2;
        ((ComboBox)closingPage.FindName("RefreshBox")).SelectedIndex = 2;
        ((Slider)closingPage.FindName("BrightnessSlider")).Value = 17;
        closingPage.ExitApplication();
        var savedPath = closingPage.Session.Model.SettingsPath;
        using (var saved = JsonDocument.Parse(File.ReadAllText(savedPath)))
        {
            Check(actions.CloseRequests == 1 && saved.RootElement.GetProperty("themeId").GetString() == "fixture-7"
                && saved.RootElement.GetProperty("brightnessPercent").GetDouble() == 17, "closing flushes the current theme and undebounced slider into one settings file");
        }
        var reopened = new Program.FixtureClient();
        var reopenedPage = new MainPage(reopened, presentationSettingsPath: savedPath, preview: preview);
        app.RootVisual = reopenedPage;
        window.Invoke(() => ShowWindow(window.WindowHandle, 9));
        await WaitAsync(() => Ready(reopenedPage) && reopened.FontRequests > 0 && reopened.Frames > 0);
        Check(reopened.StartRequests == 0 && reopened.LastPreview is { ThemeId: "fixture-7", JpegQuality: "medium", RefreshIntervalMilliseconds: 250, BrightnessPercent: 17 }, "reopening restores the same current settings directly in the preview");
        Console.WriteLine($"{count} hidden-window startup checks passed. Only a fake client was used. No USB backend was opened.");
    }

    private static bool Ready(MainPage page) => page.Session.Snapshot.Catalog is not null && !page.Session.Snapshot.Busy
        && (bool)typeof(MainPage).GetField("ready", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!
        && !page.Session.Snapshot.DeviceRefreshPending;

    private static async Task WaitAsync(Func<bool> ready, TimeSpan? timeout = null)
    {
        var watch = Stopwatch.StartNew();
        while (!ready())
        {
            if (watch.Elapsed > (timeout ?? TimeSpan.FromSeconds(12))) { throw new TimeoutException("The offline startup condition was not reached."); }
            await Task.Delay(100);
        }
    }

    private static string Settings(string output, string? theme = "fixture-3", string? device = "offline-fixture")
    {
        var path = Path.Combine(output, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { category = "multimedia", themeId = theme, deviceId = device, profile = "wide-1280x480", profileSelection = "auto", font = "Consolas", fontScale = 1.1, refreshMilliseconds = 1000, jpegQuality = "low", saturationPercent = 160, hueDegrees = 10, brightnessPercent = 10, contrastPercent = 80, showSpectrum = false, colorFiltersEnabled = true }));
        return path;
    }

    private sealed class ClosingActions : IStudioWindowActions
    {
        public int CloseRequests { get; private set; }
        public bool CanMinimizeToTray => false;
        public void MinimizeToTray() => throw new InvalidOperationException("No tray action in this fixture.");
        public void CloseApplication() => CloseRequests++;
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
}
