// VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
using System.Text.Json;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Studio;

var output = Path.Combine("artifacts", "studio-checks", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(output);
var checks = 0;
void Check(bool value, string name) { if (!value) { throw new InvalidOperationException(name); } checks++; }
async Task Until(Func<bool> predicate)
{
    using var timeout = new CancellationTokenSource(5000);
    while (!predicate()) { await Task.Delay(20, timeout.Token); }
}

var path = Path.Combine(output, "settings.json");
var client = new FakeClient();
await using (var session = new StudioSession(client, path))
{
    await Until(() => session.Snapshot.Devices is not null);
    Check(client.Starts == 0 && client.Presents == 0, "Manual startup never sends output.");
    session.Model.Edit(p => { p.SelectedTheme = "system.a"; p.SelectedCategory = "system"; p.RefreshMilliseconds = 25; p.Appearance = 1; p.SelectedPageIndex = 2; p.PageRevision = 3; });
    session.Model.Update(s => s.ScreenThemes["system"] = "system.a");
    session.Model.Update(s => s.ScreenEnabled["multimedia"] = false);
    session.ViewModel.SetPalette("system.a@dark", new(TextPrimary: "#123456", Warning: "#FEDCBA"));
    await session.StartAsync("fixture");
    // There is no UI, dispatcher, native window, preview acknowledgement or provider.
    await Until(() => client.Presents >= 4);
    Check(client.LastOptions is { PageIndex: 2, PageRevision: 3 }, "Page selection crosses the UI-independent preview contract.");
    Check(session.Snapshot.DisplayActive, "Output runs with no UI attached.");
    var count = client.Presents;
    session.PreviewVisible = false;
    await Until(() => client.Presents >= count + 4);
    Check(client.LastOptions?.Palettes?.ContainsKey("system.a@dark") == true, "Palette crosses the output contract.");
    session.Model.Edit(p => { p.CycleThemes = true; p.LastChange = Environment.TickCount64 - 20001; });
    session.Wake();
    await Until(() => client.LastOptions?.ThemeId == "system.b");
    Check(session.Model.Preferences.SelectedTheme == "system.b", "Rotation updates the model without a UI callback.");
    Check(session.Model.Preferences.SelectedPageIndex == 0 && session.Model.Preferences.PageRevision == 4, "A theme change starts its first page with a new page revision.");
    await session.StopAsync();
    count = client.Presents;
    await Task.Delay(150);
    Check(client.Presents == count && client.Starts == 1, "Stop never automatically restarts output.");
    Check(client.MaximumInFlight == 1, "No overlapping engine calls.");
    session.Save();
    var saved = new StudioModel(path);
    Check(saved.Palettes["system.a@dark"].TextPrimary == "#123456", "Palette survives restart.");
    Check(saved.Preferences.SelectedTheme == "system.b", "Rotation preference survives restart.");
    var copy = saved.Preferences; copy.SelectedTheme = "uncommitted";
    Check(saved.Preferences.SelectedTheme == "system.b", "A view cannot mutate a preference snapshot.");
}

var startup = new FakeClient { Connected = false };
await using (var session = new StudioSession(startup, path, restoreLastSession: true))
{
    await Until(() => session.Snapshot.Devices is not null);
    Check(session.Snapshot.StartupPending && startup.Starts == 0, "Startup waits for the saved device.");
    await session.StopAsync();
    startup.Connected = true;
    await session.RefreshDevicesAsync();
    await Task.Delay(100);
    Check(startup.Starts == 0 && !session.Snapshot.StartupPending, "Stop cancels deferred startup.");
}

var restored = new FakeClient();
await using (var session = new StudioSession(restored, path, restoreLastSession: true))
{
    await Until(() => restored.Presents >= 2);
    Check(restored.Starts == 1, "Startup restores once without loading any UI.");
    restored.FailPresent = true;
    await Until(() => !session.Snapshot.DisplayActive);
    var count = restored.Presents;
    session.PreviewVisible = true;
    await Task.Delay(1200);
    Check(restored.Presents == count && restored.Starts == 1, "A failed output session cannot resume through preview state.");
    restored.FailPresent = false;
    await session.StartAsync("fixture");
    await Until(() => restored.Presents > count);
    Check(restored.Starts == 2, "Explicit Start can recover output.");
    restored.HoldNextPresent = new(TaskCreationOptions.RunContinuationsAsynchronously);
    await Until(() => restored.Held);
    var close = session.ShutdownAsync();
    Check(!close.IsCompleted, "Shutdown drains the in-flight request.");
    restored.HoldNextPresent.TrySetResult();
    await close.WaitAsync(TimeSpan.FromSeconds(2));
    count = restored.Presents;
    await Task.Delay(100);
    Check(restored.Presents == count, "Shutdown admits no further frames.");
}

try { new ThemePalette(Accent: "red").Validate(); throw new InvalidOperationException("Invalid color accepted."); }
catch (ArgumentException) { checks++; }
FirstRunChecks.Run(output, Check);
await ArchitectureChecks.RunAsync(output, Check);
Console.WriteLine($"{checks} studio checks passed. Fake client only; no UI, USB, audio, sensors or installer actions.");

sealed class FakeClient : IPanelClient
{
    public volatile bool Connected = true, FailPresent, Held;
    public volatile int Starts, Presents, MaximumInFlight;
    private int inFlight;
    private bool running;
    public PreviewOptions? LastOptions;
    public TaskCompletionSource? HoldNextPresent;
    public async Task<T> RequestAsync<T>(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        MaximumInFlight = Math.Max(MaximumInFlight, Interlocked.Increment(ref inFlight));
        try
        {
            object result;
            switch (method)
            {
                case "themes": result = new ThemeCatalogDto([new("system", "System")], [new("system.a", "A", "system", "", "fixture", PageCount: 3), new("system.b", "B", "system", "", "fixture", PageCount: 3)], []); break;
                case "devices": result = Connected ? new[] { new PanelChoice("fixture", "Offline fixture") } : []; break;
                case "start": Interlocked.Increment(ref Starts); running = true; result = new DisplayStartResult(true, "wide-1280x480"); break;
                case "stop": running = false; result = new { stopped = true }; break;
                case "state":
                case "present":
                    LastOptions = (PreviewOptions)parameters!;
                    if (method == "present")
                    {
                        Interlocked.Increment(ref Presents);
                        if (FailPresent) { throw new IOException("Synthetic failed present."); }
                        if (HoldNextPresent is { } hold) { Held = true; await hold.Task.WaitAsync(cancellationToken); HoldNextPresent = null; }
                    }
                    await Task.Delay(5, cancellationToken);
                    result = new PanelState("", "", "", false, "fixture", [], new(null, null, null, null, "", ""), null,
                        [], [], running, "Fixture", null, [], ActiveThemeId: LastOptions.ThemeId,
                        ActivePageId: "activity", ActivePageTitle: "Live activity", ActivePageIndex: LastOptions.PageIndex, ActivePageCount: 3);
                    break;
                default: throw new InvalidOperationException(method);
            }
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(result, PanelContract.Json), PanelContract.Json)!;
        }
        finally { Interlocked.Decrement(ref inFlight); }
    }
}
