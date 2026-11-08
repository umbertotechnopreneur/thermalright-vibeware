/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: StudioSession.cs
 * Purpose: Serializes engine work, saved-session startup and frame scheduling outside the UI.
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

using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Studio;

/// <summary>One immutable observation, replaced atomically; views never hold up the worker.</summary>
public sealed record StudioSnapshot(ThemeCatalogDto? Catalog = null, PanelChoice[]? Devices = null,
    PanelState? Frame = null, string Status = "Local preview", StudioOutput? ActiveOutput = null,
    bool Busy = false, bool StartupPending = false, long Revision = 0, long FrameRevision = 0,
    EngineHelloDto? Hello = null, bool DeviceRefreshPending = false, bool CatalogRefreshPending = false,
    string? PersistenceError = null, string? FrameRequestedThemeId = null, long FramePageRevision = 0)
{
    public bool DisplayActive => ActiveOutput is not null;
    public bool CanStop => !Busy && (DisplayActive || StartupPending);
    public bool CanSelectDevice => Hello?.Supports("devices") == true && CanSelectProfile && !DeviceRefreshPending;
    public bool CanSelectProfile => !Busy && !DisplayActive;

    /// <summary>Checks whether the selected theme and a connected device can be used for explicit output.</summary>
    /// <param name="preferences">The studio selection to evaluate against this engine observation.</param>
    public bool CanStart(StudioPreferences preferences) => Hello?.Supports("start") == true
        && CanSelectProfile && !DeviceRefreshPending && !string.IsNullOrEmpty(preferences.SelectedTheme)
        && Devices?.Any(device => device.Id == preferences.SelectedDeviceId) == true;
}

/// <summary>The device and resolved profile currently open in the engine.</summary>
public sealed record StudioOutput(string DeviceId, string ProfileId);

/// <summary>Owns output scheduling and startup independently of every window/dispatcher.</summary>
public sealed class StudioSession : IPanelClient, IAsyncDisposable, IDisposable
{
    private readonly IPanelClient client;
    private readonly SemaphoreSlim operation = new(1, 1), wake = new(0, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object snapshotSync = new();
    private readonly object persistenceSync = new();
    private readonly object shutdownSync = new();
    private StudioSnapshot snapshot = new();
    private readonly Task worker;
    private readonly StartupSelection? startup;
    private int stopping, previewVisible, previewRequested = 1, stopRequests;
    private bool outputFaulted;
    private long devicesAt;
    private const int SaveDelayMilliseconds = 150;
    private Timer? saveTimer;
    private long? saveAt;
    private Task? shutdown;
    private Exception? initializationError;
    public StudioModel Model { get; }
    public StudioViewModel ViewModel { get; }
    public StudioSnapshot Snapshot => Volatile.Read(ref snapshot);
    public bool Supports(string method) => Snapshot.Hello?.Supports(method) == true;

    /// <summary>Native window visibility, independent of the selected studio section.</summary>
    public bool PreviewVisible
    {
        set
        {
            var visible = value ? 1 : 0;
            if (Interlocked.Exchange(ref previewVisible, visible) != visible) { Wake(); }
        }
    }

    /// <summary>Whether the selected studio section needs an on-screen preview.</summary>
    public bool PreviewRequested
    {
        set
        {
            var requested = value ? 1 : 0;
            if (Interlocked.Exchange(ref previewRequested, requested) != requested) { Wake(); }
        }
    }

    private bool WantsPreview => Volatile.Read(ref previewVisible) != 0 && Volatile.Read(ref previewRequested) != 0;

    private sealed record StartupSelection(string DeviceId, string ProfileId, string ThemeId);

    /// <summary>Starts the UI-independent worker and optionally schedules saved-device restoration.</summary>
    /// <param name="client">Engine request client owned or shared according to the containing session/host.</param>
    /// <param name="settingsPath">Explicit settings file path, or the default when the constructor allows null.</param>
    /// <param name="restoreLastSession">Whether to restore the saved device/theme and wait for that device at startup.</param>
    public StudioSession(IPanelClient client, string settingsPath, bool restoreLastSession = false)
    {
        this.client = client;
        Model = new(settingsPath);
        ViewModel = new(Model, this, CancelStartup);
        if (restoreLastSession)
        {
            var preferences = Model.Preferences;
            if (preferences.SelectedDeviceId is { Length: > 0 } device && preferences.SelectedTheme.Length > 0)
            {
                startup = new(device, preferences.ProfileSelection, preferences.SelectedTheme);
                snapshot = snapshot with { StartupPending = true };
            }
        }
        worker = Task.Run(RunAsync);
    }

    /// <summary>Publishes the next observation while protecting shared state.</summary>
    /// <param name="edit">Returns the new observation, or null to leave the current observation unchanged.</param>
    private bool Publish(Func<StudioSnapshot, StudioSnapshot?> edit)
    {
        lock (snapshotSync)
        {
            var next = edit(snapshot);
            if (next is null) { return false; }
            Volatile.Write(ref snapshot, next with { Revision = snapshot.Revision + 1 });
            return true;
        }
    }

    /// <summary>Negotiates once before scheduling provider work; an invalid handshake needs a deliberate restart.</summary>
    private async Task EnsureHelloAsync()
    {
        if (initializationError is not null) { throw new IOException("Engine initialization failed. Reopen the studio after fixing the engine package.", initializationError); }
        if (Snapshot.Hello is not null) { return; }
        try
        {
            var hello = await client.RequestAsync<EngineHelloDto>("hello", cancellationToken: lifetime.Token).ConfigureAwait(false);
            if (hello is null) { throw new IOException("The engine returned no capability declaration."); }
            hello.Validate();
            Publish(s => s with { Hello = hello, StartupPending = s.StartupPending && hello.Supports("start") });
        }
        catch (Exception error) when (error is not OperationCanceledException)
        { initializationError = error; throw; }
    }

    private void RequireSupported(string method)
    {
        if (!Supports(method)) { throw new PlatformNotSupportedException($"The {Snapshot.Hello?.Platform ?? "unavailable"} engine does not support '{method}'."); }
    }

    /// <summary>Claims a pending startup restore unless Stop has already been requested.</summary>
    private bool ConsumeStartup() => Publish(s => s.StartupPending && Volatile.Read(ref stopRequests) == 0
        ? s with { StartupPending = false, Busy = true } : null);

    /// <summary>Cancels the saved-device wait without opening a display.</summary>
    public void CancelStartup() => Publish(s => s.StartupPending ? s with { StartupPending = false, Status = "Local preview" } : null);

    /// <summary>Signals the worker once without accumulating queued wakeups.</summary>
    public void Wake() { if (wake.CurrentCount == 0) { try { wake.Release(); } catch (SemaphoreFullException) { } } }

    /// <summary>Serializes a named engine request and returns its typed response.</summary>
    /// <typeparam name="T">Expected response or payload type used by this operation.</typeparam>
    /// <param name="method">Allow-listed engine or worker method name.</param>
    /// <param name="parameters">Request payload serialized with the versioned contract; null when no payload is needed.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    public async Task<T> RequestAsync<T>(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        await operation.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref stopping) != 0) { throw new OperationCanceledException("The studio is closing."); }
            await EnsureHelloAsync().ConfigureAwait(false);
            RequireSupported(method);
            return await client.RequestAsync<T>(method, parameters, linked.Token).ConfigureAwait(false);
        }
        finally { operation.Release(); }
    }

    /// <summary>Reloads the theme catalog while holding the engine operation gate.</summary>
    public async Task ReloadCatalogAsync()
    {
        if (!Publish(s => s.CatalogRefreshPending ? null : s with { CatalogRefreshPending = true })) { return; }
        try
        {
            await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
            try { await LoadCatalogAsync().ConfigureAwait(false); }
            finally { operation.Release(); }
        }
        finally { Publish(s => s with { CatalogRefreshPending = false }); Wake(); }
    }

    /// <summary>Loads available themes and reconciles the current selection.</summary>
    private async Task LoadCatalogAsync()
    {
        await EnsureHelloAsync().ConfigureAwait(false);
        var catalog = await client.RequestAsync<ThemeCatalogDto>("themes", cancellationToken: lifetime.Token).ConfigureAwait(false);
        ViewModel.NormalizeCatalog(catalog);
        Publish(s => s with { Catalog = catalog });
    }

    /// <summary>Refreshes the available display choices.</summary>
    public async Task RefreshDevicesAsync()
    {
        if (!Publish(s => s.DeviceRefreshPending ? null : s with { DeviceRefreshPending = true })) { return; }
        try
        {
            await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
            try { await DiscoverAsync().ConfigureAwait(false); }
            finally { operation.Release(); }
        }
        finally { Publish(s => s with { DeviceRefreshPending = false }); Wake(); }
    }

    /// <summary>Refreshes device metadata and records the discovery time.</summary>
    private async Task DiscoverAsync()
    {
        await EnsureHelloAsync().ConfigureAwait(false);
        if (!Supports("devices"))
        { devicesAt = Environment.TickCount64; Publish(s => s with { Devices = [] }); return; }
        var devices = await client.RequestAsync<PanelChoice[]>("devices", cancellationToken: lifetime.Token).ConfigureAwait(false);
        devicesAt = Environment.TickCount64;
        var current = Snapshot;
        var lost = current.ActiveOutput is { } output && !devices.Any(device => device.Id == output.DeviceId);
        if (lost) { outputFaulted = true; }
        if (!current.StartupPending && !current.DisplayActive && devices.FirstOrDefault() is { } first
            && string.IsNullOrEmpty(Model.Preferences.SelectedDeviceId))
        {
            Model.Edit(preferences =>
            {
                if (string.IsNullOrEmpty(preferences.SelectedDeviceId)) { preferences.SelectedDeviceId = first.Id; }
            });
        }
        Publish(s => s with
        {
            Devices = devices,
            ActiveOutput = lost ? null : s.ActiveOutput,
            Status = lost ? "USB display disconnected. Press Show on display to reconnect." : s.Status
        });
    }

    /// <summary>Records an explicit selection and cancels deferred startup.</summary>
    /// <param name="device">Stable identifier of a currently discovered device.</param>
    public void SelectDevice(string device)
    {
        if (!Publish(s => s.CanSelectDevice && s.Devices?.Any(choice => choice.Id == device) == true
            ? s with { StartupPending = false, Status = s.StartupPending ? "Local preview" : s.Status } : null)) { return; }
        Model.Edit(preferences => preferences.SelectedDeviceId = device);
        Save();
        Wake();
    }

    /// <summary>Starts output on the device and theme selected in the studio model.</summary>
    public Task StartSelectedAsync()
    {
        var preferences = Model.Preferences;
        return Snapshot.CanStart(preferences) ? StartAsync(preferences.SelectedDeviceId!, preferences) : Task.CompletedTask;
    }

    /// <summary>Starts output on the explicitly selected device.</summary>
    /// <param name="device">Stable device-choice identifier returned by the engine discovery request.</param>
    public Task StartAsync(string device) => StartAsync(device, Model.Preferences);

    /// <summary>Serializes the captured display request with other engine operations.</summary>
    /// <param name="device">Stable identifier of the requested device.</param>
    /// <param name="preferences">The selection snapshot taken when the request was made.</param>
    private async Task StartAsync(string device, StudioPreferences preferences)
    {
        var options = new StartOptions(device, preferences.ProfileSelection, ThemeId: preferences.SelectedTheme);
        CancelStartup();
        await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try
        {
            await EnsureHelloAsync().ConfigureAwait(false);
            RequireSupported("start");
            if (Volatile.Read(ref stopping) != 0 || Volatile.Read(ref stopRequests) != 0
                || Snapshot.DisplayActive || string.IsNullOrEmpty(options.ThemeId)) { return; }
            Publish(s => s with { Busy = true });
            await StartCoreAsync(options, preferences).ConfigureAwait(false);
        }
        finally { operation.Release(); Wake(); }
    }

    /// <summary>Starts the captured request and publishes the output identity confirmed by the engine.</summary>
    /// <param name="options">The device, profile choice and theme reserved for this start.</param>
    /// <param name="preferences">Selection values observed before starting, used to preserve newer edits.</param>
    private async Task StartCoreAsync(StartOptions options, StudioPreferences preferences)
    {
        try
        {
            var result = await client.RequestAsync<DisplayStartResult>("start", options, lifetime.Token).ConfigureAwait(false);
            if (!result.Accepted) { throw new IOException("The display could not start."); }
            var profile = result.ProfileId;
            if (string.IsNullOrEmpty(profile))
            {
                await client.RequestAsync<object>("stop", cancellationToken: lifetime.Token).ConfigureAwait(false);
                throw new IOException("The engine did not identify the display profile.");
            }

            outputFaulted = false;
            Publish(s => s with { ActiveOutput = new(options.DeviceId, profile), Status = "Display running." });
            Model.Edit(next =>
            {
                if (next.SelectedDeviceId == preferences.SelectedDeviceId) { next.SelectedDeviceId = options.DeviceId; }
                // The last resolved size is a preview preference. The active profile lives in ActiveOutput.
                if (next.ProfileSelection == options.ProfileId && next.SelectedProfile == preferences.SelectedProfile)
                { next.SelectedProfile = profile; }
                if (next.SelectedTheme == options.ThemeId) { next.LastChange = Environment.TickCount64; }
            });
            Save();
        }
        catch (Exception error) { outputFaulted = true; Publish(s => s with { ActiveOutput = null, Status = error.Message }); }
        finally { Publish(s => s with { Busy = Volatile.Read(ref stopRequests) != 0 }); }
    }

    /// <summary>Stops output and cancels any pending startup restore.</summary>
    public async Task StopAsync()
    {
        // Consume startup before waiting for an in-flight request. Stop is never lost.
        Interlocked.Increment(ref stopRequests);
        Publish(s => s with { StartupPending = false, Busy = true });
        var acquired = false;
        try
        {
            await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
            acquired = true;
            await EnsureHelloAsync().ConfigureAwait(false);
            await client.RequestAsync<object>("stop", cancellationToken: lifetime.Token).ConfigureAwait(false);
            outputFaulted = false;
            Publish(s => s with { ActiveOutput = null, Status = "Display stopped." });
        }
        catch (Exception error) { outputFaulted = true; Publish(s => s with { ActiveOutput = null, Status = error.Message }); }
        finally { Interlocked.Decrement(ref stopRequests); Publish(s => s with { Busy = Volatile.Read(ref stopRequests) != 0 }); if (acquired) { operation.Release(); } Wake(); }
    }

    /// <summary>Persists the current settings snapshot.</summary>
    public void Save()
    {
        lock (persistenceSync)
        {
            saveAt = null;
            saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                Model.Save();
                Publish(s => s.PersistenceError is null ? null : s with { PersistenceError = null });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { Publish(s => s with { PersistenceError = "The preference changed but could not be saved." }); }
        }
    }

    /// <summary>Debounces persistence and a preview wake after rapid preference edits.</summary>
    public void ScheduleSave()
    {
        lock (persistenceSync)
        {
            if (Volatile.Read(ref stopping) != 0) { return; }
            saveAt = Environment.TickCount64 + SaveDelayMilliseconds;
            saveTimer ??= new(_ => SaveWhenDue(), null, Timeout.Infinite, Timeout.Infinite);
            saveTimer.Change(SaveDelayMilliseconds, Timeout.Infinite);
        }
    }

    /// <summary>Flushes the latest edit only after its full debounce interval.</summary>
    private void SaveWhenDue()
    {
        lock (persistenceSync)
        {
            if (saveAt is not { } due || Volatile.Read(ref stopping) != 0) { return; }
            var remaining = due - Environment.TickCount64;
            if (remaining > 0)
            {
                saveTimer!.Change(remaining, Timeout.Infinite);
                return;
            }
            Save();
        }
        Wake();
    }

    /// <summary>Persists a completed first UI load and reports an unavailable settings store.</summary>
    public void CompleteFirstRun()
    {
        lock (persistenceSync)
        {
            if (!Model.Preferences.IsFirstRun) { return; }
            try
            {
                Model.CompleteFirstRun();
                Publish(s => s.PersistenceError is null ? null : s with { PersistenceError = null });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { Publish(s => s with { PersistenceError = "First-launch settings could not be saved. The main window will open again next time." }); }
        }
    }

    /// <summary>Advances enabled screens after the saved rotation interval.</summary>
    private void Rotate()
    {
        var current = Snapshot;
        if (current.StartupPending || current.Catalog is not { } catalog) { return; }
        var state = Model.Read();
        var p = state.Preferences;
        if (!p.CycleThemes || Environment.TickCount64 - p.LastChange < 20000) { return; }
        var screens = state.Screens().Where(s => s.Enabled && !string.IsNullOrEmpty(s.ThemeId)
            && (!current.DisplayActive || s.Category != "codex" || current.Frame?.CodexProcessRunning == true)).ToArray();
        if (screens.Length == 0) { return; }
        var index = Array.FindIndex(screens, s => s.Category == p.SelectedCategory);
        var next = screens[(index + 1) % screens.Length];
        if (screens.Length == 1 && next.Category == p.SelectedCategory)
        {
            var themes = catalog.Themes.Where(t => t.Category == next.Category).ToArray();
            if (themes.Length > 0)
            {
                var themeIndex = Array.FindIndex(themes, t => t.Id == p.SelectedTheme);
                next = next with { ThemeId = themes[(themeIndex + 1) % themes.Length].Id };
            }
        }
        Model.Update(latest =>
        {
            // A user edit made during rotation wins over this stale observation.
            if (latest.Revision != state.Revision) { return; }
            StudioViewModel.Select(latest, next.Category, next.ThemeId);
        });
        Save();
    }

    /// <summary>Attempts the captured startup request once its saved device is present.</summary>
    private async Task RestoreStartupAsync()
    {
        var current = Snapshot;
        if (!current.StartupPending || startup is null) { return; }

        var preferences = Model.Preferences;
        if (current.Catalog?.Themes.Any(choice => choice.Id == startup.ThemeId) != true || preferences.SelectedTheme != startup.ThemeId)
        {
            if (Publish(s => s.StartupPending ? s with { StartupPending = false, Status = "The saved startup theme changed or is unavailable." } : null))
            { outputFaulted = true; }
            return;
        }

        if (current.Devices?.Any(choice => choice.Id == startup.DeviceId) == true)
        {
            // Consume before I/O; an ambiguous start is never replayed.
            if (ConsumeStartup()) { await StartCoreAsync(new(startup.DeviceId, startup.ProfileId, ThemeId: startup.ThemeId), preferences).ConfigureAwait(false); }
            return;
        }

        Publish(s => s.StartupPending ? s with { Status = "Waiting for the selected USB display. Stop cancels automatic output for this run." } : null);
    }

    /// <summary>Serializes discovery, startup restoration and frame scheduling without a frame backlog.</summary>
    private async Task RunAsync()
    {
        try
        {
            while (Volatile.Read(ref stopping) == 0)
            {
                var delay = Model.Preferences.RefreshMilliseconds;
                await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
                try
                {
                    if (Volatile.Read(ref stopping) != 0) { break; }
                    await EnsureHelloAsync().ConfigureAwait(false);
                    if (Snapshot.Catalog is null) { await LoadCatalogAsync().ConfigureAwait(false); }
                    if (devicesAt == 0 || Supports("devices") && Environment.TickCount64 - devicesAt >= 10000) { await DiscoverAsync().ConfigureAwait(false); }

                    await RestoreStartupAsync().ConfigureAwait(false);
                    var current = Snapshot;
                    if (Volatile.Read(ref stopRequests) != 0 || current.Busy || !current.DisplayActive && !WantsPreview) { goto WaitForNext; }

                    // Take a detached preference snapshot so one frame uses one coherent configuration.
                    Rotate();
                    var state = Model.Read();
                    var p = state.Preferences;
                    var screens = state.Screens();
                    var theme = string.IsNullOrEmpty(p.SelectedTheme) ? screens.FirstOrDefault(s => s.Enabled && s.ThemeId.Length > 0)?.ThemeId ?? Snapshot.Catalog?.Themes.FirstOrDefault()?.Id : p.SelectedTheme;
                    if (string.IsNullOrEmpty(theme)) { goto WaitForNext; }
                    var light = p.Light;
                    var appearanceSuffix = light ? "@light" : "@dark";
                    var activePaletteKeys = new[] { theme }.Concat(screens.Select(screen => screen.ThemeId))
                        .SelectMany(id => new[] { id, id + appearanceSuffix }).ToHashSet(StringComparer.Ordinal);
                    var palettes = state.Palettes.Where(pair => activePaletteKeys.Contains(pair.Key)).ToDictionary();

                    var options = new PreviewOptions(ProfileId: current.ActiveOutput?.ProfileId ?? p.SelectedProfile, Light: light, ThemeId: theme,
                        FontFamily: p.FontFamily, FontScale: p.FontScale, RefreshIntervalMilliseconds: p.RefreshMilliseconds,
                        JpegQuality: p.JpegQuality, SaturationPercent: p.SaturationPercent, HueDegrees: p.HueDegrees,
                        ShowSpectrum: p.ShowSpectrum, BrightnessPercent: p.BrightnessPercent, ContrastPercent: p.ContrastPercent,
                        ColorFiltersEnabled: p.ColorFiltersEnabled, Screens: screens, Palettes: palettes,
                        PageIndex: p.SelectedPageIndex, PageRevision: p.PageRevision);

                    // Only the explicitly active display uses present; preview state remains USB-free.
                    var frame = await client.RequestAsync<PanelState>(current.DisplayActive ? "present" : "state", options, lifetime.Token).ConfigureAwait(false);
                    delay = frame.NextFrameMilliseconds is >= 25 and <= 1000 ? Math.Min(delay, frame.NextFrameMilliseconds.Value) : delay;
                    if (frame.System.Driver is { } driver) { ViewModel.ObserveSensorDriver(driver); }
                    Publish(s => s with
                    {
                        Frame = frame,
                        FrameRevision = s.FrameRevision + 1,
                        FrameRequestedThemeId = theme,
                        FramePageRevision = p.PageRevision,
                        ActiveOutput = frame.DisplayRunning ? current.ActiveOutput : null,
                        Status = outputFaulted || s.StartupPending ? s.Status : frame.Status
                    });
                }
                catch (Exception error) when (error is not OperationCanceledException)
                { if (Snapshot.DisplayActive) { outputFaulted = true; } Publish(s => s with { ActiveOutput = null, Status = error.Message }); if (initializationError is not null) { return; } delay = 1000; }
                finally { operation.Release(); }
            WaitForNext:
                // Native thread-pool wait: never depends on WebView, XAML or window events.
                await wake.WaitAsync(Math.Clamp(delay, 25, 1000), lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) { Publish(s => s with { ActiveOutput = null, Status = error.Message }); }
    }

    /// <summary>Returns the shared task that saves settings and drains the worker.</summary>
    public Task ShutdownAsync()
    {
        lock (shutdownSync) { return shutdown ??= ShutdownCoreAsync(); }
    }

    /// <summary>Saves settings and waits a bounded time for worker shutdown.</summary>
    private async Task ShutdownCoreAsync()
    {
        Interlocked.Exchange(ref stopping, 1);
        Wake();
        Save();
        lock (persistenceSync)
        {
            saveTimer?.Dispose();
            saveTimer = null;
        }
        try { await worker.WaitAsync(TimeSpan.FromSeconds(40)).ConfigureAwait(false); }
        catch (TimeoutException) { lifetime.Cancel(); }
        finally { lifetime.Cancel(); }
        // The host next closes engine stdin. EOF lets the engine dispose USB,
        // Python, audio and sensors before the host's final bounded kill fallback.
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose() => ShutdownAsync().GetAwaiter().GetResult();

    /// <summary>Asynchronously releases resources owned by this instance.</summary>
    public async ValueTask DisposeAsync() => await ShutdownAsync().ConfigureAwait(false);
}
