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
    PanelState? Frame = null, string Status = "Local preview", bool DisplayActive = false,
    bool Busy = false, bool StartupPending = false, long Revision = 0, long FrameRevision = 0);

/// <summary>Owns output scheduling and startup independently of every window/dispatcher.</summary>
public sealed class StudioSession : IPanelClient, IAsyncDisposable, IDisposable
{
    private readonly IPanelClient client;
    private readonly SemaphoreSlim operation = new(1, 1), wake = new(0, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object snapshotSync = new();
    private StudioSnapshot snapshot = new();
    private readonly Task worker;
    private readonly string? startupDevice, startupTheme;
    private int stopping, previewVisible, stopRequests;
    private bool outputFaulted;
    private long devicesAt;
    private Task? shutdown;
    public StudioModel Model { get; }
    public StudioViewModel ViewModel { get; }
    public StudioSnapshot Snapshot => Volatile.Read(ref snapshot);
    public bool PreviewVisible { set { Interlocked.Exchange(ref previewVisible, value ? 1 : 0); } }

    /// <summary>Starts the UI-independent worker and optionally schedules saved-device restoration.</summary>
    /// <param name="client">Engine request client owned or shared according to the containing session/host.</param>
    /// <param name="settingsPath">Explicit settings file path, or the default when the constructor allows null.</param>
    /// <param name="restoreLastSession">Whether to restore the saved device/theme and wait for that device at startup.</param>
    public StudioSession(IPanelClient client, string settingsPath, bool restoreLastSession = false)
    {
        this.client = client;
        Model = new(settingsPath);
        ViewModel = new(Model, this);
        if (Model.PersistenceNotice is { } notice) { snapshot = snapshot with { Status = notice }; }
        var p = Model.Preferences;
        if (restoreLastSession)
        {
            startupDevice = p.SelectedDeviceId; startupTheme = p.SelectedTheme;
            snapshot = snapshot with { StartupPending = true };
        }
        worker = Task.Run(RunAsync);
    }

    /// <summary>Publishes the next observation while protecting shared state.</summary>
    /// <param name="edit">Mutation applied while the owning model or snapshot lock is held.</param>
    private void Publish(Func<StudioSnapshot, StudioSnapshot> edit)
    {
        lock (snapshotSync) { var current = snapshot; Volatile.Write(ref snapshot, edit(current) with { Revision = current.Revision + 1 }); }
    }

    /// <summary>Claims a pending startup restore unless Stop has already been requested.</summary>
    private bool ConsumeStartup()
    {
        lock (snapshotSync)
        {
            if (!snapshot.StartupPending || Volatile.Read(ref stopRequests) != 0) { return false; }
            Volatile.Write(ref snapshot, snapshot with { StartupPending = false, Revision = snapshot.Revision + 1 });
            return true;
        }
    }

    /// <summary>Cancels the saved-device wait without opening a display.</summary>
    public void CancelStartup() => Publish(s => s with { StartupPending = false });

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
            return await client.RequestAsync<T>(method, parameters, linked.Token).ConfigureAwait(false);
        }
        finally { operation.Release(); }
    }

    /// <summary>Reloads the theme catalog while holding the engine operation gate.</summary>
    public async Task ReloadCatalogAsync()
    {
        await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try { await LoadCatalogAsync().ConfigureAwait(false); }
        finally { operation.Release(); Wake(); }
    }

    /// <summary>Loads available themes and reconciles the current selection.</summary>
    private async Task LoadCatalogAsync()
    {
        var catalog = await client.RequestAsync<ThemeCatalogDto>("themes", cancellationToken: lifetime.Token).ConfigureAwait(false);
        ViewModel.NormalizeCatalog(catalog);
        Publish(s => s with { Catalog = catalog });
    }

    /// <summary>Refreshes the available display choices.</summary>
    public async Task RefreshDevicesAsync()
    {
        await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try { await DiscoverAsync().ConfigureAwait(false); }
        finally { operation.Release(); Wake(); }
    }

    /// <summary>Refreshes device metadata and records the discovery time.</summary>
    private async Task DiscoverAsync()
    {
        var devices = await client.RequestAsync<PanelChoice[]>("devices", cancellationToken: lifetime.Token).ConfigureAwait(false);
        devicesAt = Environment.TickCount64;
        var p = Model.Preferences;
        var lost = Snapshot.DisplayActive && !devices.Any(d => d.Id == p.SelectedDeviceId);
        if (!Snapshot.StartupPending && !devices.Any(d => d.Id == p.SelectedDeviceId) && (string.IsNullOrEmpty(p.SelectedDeviceId) || devices.Length == 1))
        { Model.Edit(next => next.SelectedDeviceId = devices.FirstOrDefault()?.Id); }
        Publish(s => s with
        {
            Devices = devices,
            DisplayActive = lost ? false : s.DisplayActive,
            Status = lost ? "USB display disconnected. Press Show on display to reconnect." : s.Status
        });
    }

    /// <summary>Starts output on the explicitly selected device.</summary>
    /// <param name="device">Stable device-choice identifier returned by the engine discovery request.</param>
    public async Task StartAsync(string device)
    {
        Publish(s => s with { StartupPending = false });
        await operation.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref stopping) != 0 || Snapshot.DisplayActive) { return; }
            await StartCoreAsync(device).ConfigureAwait(false);
        }
        finally { operation.Release(); Wake(); }
    }

    /// <summary>Resolves the saved theme/profile and requests display output.</summary>
    /// <param name="device">Stable device-choice identifier returned by the engine discovery request.</param>
    private async Task StartCoreAsync(string device)
    {
        var p = Model.Preferences;
        if (string.IsNullOrEmpty(p.SelectedTheme)) { return; }
        Publish(s => s with { Busy = true });
        try
        {
            var result = await client.RequestAsync<DisplayStartResult>("start", new StartOptions(device, p.ProfileSelection, ThemeId: p.SelectedTheme), lifetime.Token).ConfigureAwait(false);
            if (result.Accepted) { outputFaulted = false; Model.Edit(next => { next.SelectedDeviceId = device; next.SelectedProfile = result.ProfileId ?? next.SelectedProfile; }); Save(); }
            Publish(s => s with { DisplayActive = result.Accepted, Status = result.Accepted ? "Display running." : "The display could not start." });
        }
        catch (Exception error) { Publish(s => s with { DisplayActive = false, Status = error.Message }); }
        finally { Publish(s => s with { Busy = false }); }
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
            await client.RequestAsync<object>("stop", cancellationToken: lifetime.Token).ConfigureAwait(false);
            Publish(s => s with { DisplayActive = false, Status = "Display stopped." });
        }
        catch (Exception error) { Publish(s => s with { DisplayActive = false, Status = error.Message }); }
        finally { Interlocked.Decrement(ref stopRequests); Publish(s => s with { Busy = false }); if (acquired) { operation.Release(); } Wake(); }
    }

    /// <summary>Persists the current settings snapshot.</summary>
    public void Save()
    {
        try { Model.Save(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Publish(s => s with { Status = "The preference changed but could not be saved." }); }
    }

    /// <summary>Persists a completed first UI load and reports an unavailable settings store.</summary>
    public void CompleteFirstRun()
    {
        try { Model.CompleteFirstRun(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Publish(s => s with { Status = "First-launch settings could not be saved. The main window will open again next time." }); }
    }

    /// <summary>Advances enabled screens after the saved rotation interval.</summary>
    private void Rotate()
    {
        var state = Model.Read();
        var p = state.Preferences;
        if (!p.CycleThemes || Environment.TickCount64 - p.LastChange < 20000 || Snapshot.Catalog is not { } catalog) { return; }
        var screens = state.Screens().Where(s => s.Enabled && !string.IsNullOrEmpty(s.ThemeId)
            && (!Snapshot.DisplayActive || s.Category != "codex" || Snapshot.Frame?.CodexProcessRunning == true)).ToArray();
        if (screens.Length == 0) { return; }
        var index = Array.FindIndex(screens, s => s.Category == p.SelectedCategory);
        var next = screens[(index + 1) % screens.Length];
        if (screens.Length == 1 && next.Category == p.SelectedCategory)
        {
            var themes = catalog.Themes.Where(t => t.Category == next.Category).ToArray();
            if (themes.Length > 0)
            {
                var current = Array.FindIndex(themes, t => t.Id == p.SelectedTheme);
                next = next with { ThemeId = themes[(current + 1) % themes.Length].Id };
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
                    if (Snapshot.Catalog is null) { await LoadCatalogAsync().ConfigureAwait(false); }
                    if (devicesAt == 0 || Environment.TickCount64 - devicesAt >= 10000) { await DiscoverAsync().ConfigureAwait(false); }

                    // The saved-session exception may wait for one device; Stop always cancels that wait.
                    if (Snapshot.StartupPending)
                    {
                        var startupThemeId = string.IsNullOrEmpty(startupTheme) ? Model.Preferences.SelectedTheme : startupTheme;
                        var device = startupDevice ?? (Snapshot.Devices is { Length: 1 } choices ? choices[0].Id : null);
                        if (!Snapshot.Catalog!.Themes.Any(t => t.Id == startupThemeId) || Model.Preferences.SelectedTheme != startupThemeId)
                        { Publish(s => s with { StartupPending = false, Status = "The saved startup theme changed or is unavailable." }); }
                        else if (device is not null && Snapshot.Devices!.Any(d => d.Id == device))
                        {
                            // Consume before I/O; an ambiguous start is never replayed.
                            if (ConsumeStartup()) { await StartCoreAsync(device).ConfigureAwait(false); }
                        }
                        else { Publish(s => s with { Status = "Waiting for the selected USB display. Stop cancels automatic output for this run." }); }
                    }
                    if (Volatile.Read(ref stopRequests) != 0 || Snapshot.Busy || !Snapshot.DisplayActive && Volatile.Read(ref previewVisible) == 0) { goto WaitForNext; }

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

                    var options = new PreviewOptions(ProfileId: p.SelectedProfile, Light: light, ThemeId: theme,
                        FontFamily: p.FontFamily, FontScale: p.FontScale, RefreshIntervalMilliseconds: p.RefreshMilliseconds,
                        JpegQuality: p.JpegQuality, SaturationPercent: p.SaturationPercent, HueDegrees: p.HueDegrees,
                        ShowSpectrum: p.ShowSpectrum, BrightnessPercent: p.BrightnessPercent, ContrastPercent: p.ContrastPercent,
                        ColorFiltersEnabled: p.ColorFiltersEnabled, Screens: screens, Palettes: palettes,
                        PageIndex: p.SelectedPageIndex, PageRevision: p.PageRevision);

                    // Only the explicitly active display uses present; preview state remains USB-free.
                    var active = Snapshot.DisplayActive;
                    var frame = await client.RequestAsync<PanelState>(active ? "present" : "state", options, lifetime.Token).ConfigureAwait(false);
                    delay = frame.NextFrameMilliseconds is >= 25 and <= 1000 ? Math.Min(delay, frame.NextFrameMilliseconds.Value) : delay;
                    Publish(s => s with { Frame = frame, FrameRevision = s.FrameRevision + 1, DisplayActive = active && !outputFaulted && frame.DisplayRunning, Status = outputFaulted ? s.Status : frame.Status });
                }
                catch (Exception error) when (error is not OperationCanceledException)
                { if (Snapshot.DisplayActive) { outputFaulted = true; } Publish(s => s with { DisplayActive = false, Status = error.Message }); delay = 1000; }
                finally { operation.Release(); }
            WaitForNext:
                // Native thread-pool wait: never depends on WebView, XAML or window events.
                await wake.WaitAsync(Math.Clamp(delay, 25, 1000), lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) { Publish(s => s with { DisplayActive = false, Status = error.Message }); }
    }

    /// <summary>Returns the shared task that saves settings and drains the worker.</summary>
    public Task ShutdownAsync()
    {
        lock (snapshotSync) { return shutdown ??= ShutdownCoreAsync(); }
    }

    /// <summary>Saves settings and waits a bounded time for worker shutdown.</summary>
    private async Task ShutdownCoreAsync()
    {
        Interlocked.Exchange(ref stopping, 1);
        Wake();
        Save();
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
