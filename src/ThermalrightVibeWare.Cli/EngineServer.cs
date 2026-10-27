/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: EngineServer.cs
 * Purpose: Dispatches bounded stdio requests and keeps preview state separate from explicit USB presentation.
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

using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Core;

/// <summary>Small allow-listed local protocol. USB writes require an explicit start request.</summary>
internal sealed partial class EngineServer : IDisposable
{
    private readonly WindowsMetricsService metrics = new(allowHardwareAccess: true);
    private readonly DashboardService dashboard;

    /// <summary>Creates the engine's Windows data providers without starting display output.</summary>
    public EngineServer() => dashboard = new(WindowsDashboardProviders.Create(metrics));
    private readonly PythonThemeRenderer themeRenderer = new();
    private readonly CodexProcessMonitor codexProcess = new();
    private readonly Dictionary<string, DisplayDevice> devices = [];
    private HardwareDriverStatus? sensorDriver;
    private string status = "Local preview only. The USB display has not been started.";
    private string? previousFingerprint;
    private byte[]? preview;
    private long discoveredAt;
    private DashboardSnapshot? lastSnapshot;
    private long snapshotAt;
    private bool snapshotIncludesSystem;
    private bool snapshotIncludesMedia;
    private bool snapshotIncludesSensors;
    private ThemeCatalogSnapshot? themeCatalog;
    private long previewAt;
    private string? previewNotice;
    private ThemePanelOutput? themeOutput;
    private string? themeDeviceId;
    private double? frameMilliseconds;

    /// <summary>Processes bounded JSON-lines requests until stdin closes, then releases engine resources.</summary>
    public static async Task<int> RunAsync()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);
        using var engine = new EngineServer();
        try
        {
            while (await ReadRequestLineAsync() is { } line)
            {
                var id = "";
                var method = "unknown";
                string? errorType = null;
                var started = Stopwatch.GetTimestamp();
                EngineResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize<EngineRequest>(line, PanelContract.Json) ?? throw new ArgumentException("Expected a request object.");
                    id = request.Id;
                    method = request.Method;
                    if (string.IsNullOrWhiteSpace(id) || id.Length > 64) { throw new ArgumentException("Request ID must contain 1 to 64 characters."); }
                    if (request.Version != PanelContract.Version) { throw new ArgumentException("Unsupported protocol version."); }
                    response = new(id, true, JsonSerializer.SerializeToElement(await engine.HandleAsync(request), PanelContract.Json));
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                { errorType = error.GetType().Name; response = new(id, false, Error: error.Message); }
                DiagnosticLog.Rpc(method, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, response.Ok, errorType,
                    engine.themeOutput?.SubmittedFrames ?? 0);
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response, PanelContract.Json));
                await Console.Out.FlushAsync();
            }
            await engine.StopAsync();
            return 0;
        }
        catch (Exception error)
        { await Console.Error.WriteLineAsync($"ThermalrightVibeWare engine: {error.Message}"); return 1; }
    }

    /// <summary>Reads one bounded JSON request line from standard input.</summary>
    private static async Task<string?> ReadRequestLineAsync()
    {
        var text = new StringBuilder();
        var character = new char[1];
        while (await Console.In.ReadAsync(character.AsMemory()) != 0)
        {
            if (character[0] == '\n') { return text.ToString().TrimEnd('\r'); }
            if (text.Length >= PanelContract.MaximumRequestCharacters) { throw new IOException("Request exceeds the 16 KiB limit."); }
            text.Append(character[0]);
        }
        return text.Length == 0 ? null : text.ToString();
    }

    /// <summary>Dispatches an allow-listed engine request.</summary>
    /// <param name="request">Versioned engine request containing a method, correlation ID and optional JSON parameters.</param>
    private async Task<object> HandleAsync(EngineRequest request)
    {
        switch (request.Method)
        {
            case "hello": return new { protocol = PanelContract.Version, platform = "windows", usbWritesRequireStart = true, themeMirror = true };
            case "themes":
                themeCatalog = ThemeCatalog.Read();
                // An explicit refresh can replace a pack at the same ID; discard frames from the previous catalog.
                previousFingerprint = null;
                preview = null;
                previewNotice = null;
                return new ThemeCatalogDto([new("multimedia", "Music"), new("codex", "Codex"), new("system", "System")],
                    themeCatalog.Themes.Select(theme => new ThemeChoiceDto(theme.Id, theme.Name, theme.Category, theme.Description, theme.PackId, theme.SupportsSpectrum, theme.PageCount, theme.PageSeconds)).ToArray(), themeCatalog.Notices);
            case "fonts":
                var fonts = await themeRenderer.ListFontsAsync();
                return new FontCatalogDto(fonts.Families, fonts.Notice);
            case "state": return await StateAsync(Parameters<PreviewOptions>(request, new()));
            case "present": return await PresentAsync(Parameters<PreviewOptions>(request));
            case "devices":
                DiscoverDevices();
                return DeviceChoices();
            case "levels": return dashboard.Audio.ReadLevels();
            case "start":
                var options = Parameters<StartOptions>(request);
                if (themeOutput is not null) { throw new InvalidOperationException("Stop the current display session first."); }
                if (!devices.TryGetValue(options.DeviceId, out var selected)) { throw new ArgumentException("Refresh and choose a connected panel."); }
                var connected = DisplayDiscovery.FindDevices().SingleOrDefault(item => item.DevicePath == selected.DevicePath && item.Transport == selected.Transport)
                    ?? throw new IOException("The selected panel disconnected. Refresh the device list.");
                if (options.ProfileId != "auto") { _ = DisplayProfileCatalog.GetRequired(options.ProfileId); }
                _ = FindTheme(options.ThemeId ?? throw new ArgumentException("Choose a theme before starting the display."));
                themeOutput = await ThemePanelOutput.OpenAsync(connected, options.ProfileId);
                themeDeviceId = options.DeviceId;
                frameMilliseconds = null;
                previousFingerprint = null;
                preview = null;
                previewNotice = null;
                Volatile.Write(ref status, $"Display ready · {themeOutput.Profile.Width} × {themeOutput.Profile.Height}");
                return new DisplayStartResult(true, themeOutput.Profile.Id);
            case "stop": await StopAsync(); return new { stopped = true };
            case "media":
                await dashboard.Media.ControlAsync(Parameters<MediaOptions>(request).Action);
                return new { accepted = true };
            case "codex.status": return CodexIntegration.Status();
            case "codex.prepare": return await CodexIntegration.PrepareAsync();
            case "diagnostics.status": return DiagnosticLog.Status();
            case "diagnostics.set": return DiagnosticLog.SetEnabled(Parameters<SetDiagnosticsOptions>(request).Enabled);
            case "diagnostics.open": return new { accepted = DiagnosticLog.Open() };
            case "sensors.driver.status": return SensorDriverStatus();
            case "sensors.debug": return await Task.Run(metrics.ReadSensorDebug);
            case "sensors.driver.open": return new { accepted = OpenSensorDriverFolder() };
            default: throw new ArgumentException("Unknown method. Use hello, themes, fonts, devices, state, present, levels, start, stop, media, codex.status, codex.prepare, diagnostics.*, sensors.debug, or sensors.driver.*.");
        }
    }

    /// <summary>Reads cached driver metadata without opening sensor hardware.</summary>
    private SensorDriverDto SensorDriverStatus()
    {
        // Registry/cached metadata only. Opening this panel must not open hardware.
        var driver = sensorDriver ?? WindowsMetricsService.ReadDriverInstallationStatus();
        return new(driver.Available, driver.Path, driver.Notice, driver.Path is not null && File.Exists(driver.Path));
    }

    /// <summary>Shows a verified driver path in Windows Explorer.</summary>
    private bool OpenSensorDriverFolder()
    {
        var driver = SensorDriverStatus();
        if (driver.Path is not { } path || !Path.IsPathFullyQualified(path)) { return false; }
        var directory = Path.GetDirectoryName(path);
        if (directory is null || !Directory.Exists(directory)) { return false; }
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
        { UseShellExecute = false };
        start.ArgumentList.Add(driver.FileExists ? "/select," + path : directory);
        return Process.Start(start) is not null;
    }

    /// <summary>Refreshes the engine's stable device-choice identifiers.</summary>
    private void DiscoverDevices()
    {
        // Build a complete snapshot before replacing the last successful enumeration.
        var found = new Dictionary<string, DisplayDevice>();
        foreach (var device in DisplayDiscovery.FindDevices())
        {
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(device.DevicePath + device.Transport)))[..16];
            found.TryAdd(key, device);
        }
        devices.Clear();
        foreach (var entry in found) { devices.Add(entry.Key, entry.Value); }
        discoveredAt = Environment.TickCount64;
        if (themeOutput is not null && (themeDeviceId is null || !devices.ContainsKey(themeDeviceId)))
        {
            CloseThemeOutput();
            Volatile.Write(ref status, "USB display disconnected. Reconnect it, then press Show on display.");
        }
    }

    /// <summary>Projects cached devices into presentation choices.</summary>
    private PanelChoice[] DeviceChoices() => devices.Select(item => new PanelChoice(item.Key, $"{item.Value.Id} · {item.Value.Connection}")).ToArray();

    /// <summary>Submits a frame only after an explicit display start.</summary>
    /// <param name="options">Validated preview controls, selected profile/theme, screens and palette overrides.</param>
    private async Task<PanelState> PresentAsync(PreviewOptions options)
    {
        var output = themeOutput ?? throw new InvalidOperationException("Choose a panel and press Show on display first.");
        try
        {
            if (options.ThemeId is null || !options.IncludeFrame) { throw new ArgumentException("Display output requires a theme frame."); }
            if (options.ProfileId != output.Profile.Id) { throw new ArgumentException("Use the connected display's detected profile."); }
            ValidatePreviewOptions(options);
            var started = Stopwatch.GetTimestamp();
            var processRunning = codexProcess.IsRunning();
            var requested = FindTheme(options.ThemeId);
            var chosen = ScreenSelectionPolicy.Resolve(requested, (themeCatalog ??= ThemeCatalog.Read()).Themes, options.Screens, processRunning);
            if (chosen is null)
            {
                var fingerprint = $"blank|{output.Profile.Id}|{options.JpegQuality}";
                if (previousFingerprint != fingerprint || preview is null)
                {
                    var quality = options.JpegQuality switch { "poor" => 20, "low" => 50, "medium" => 75, _ => 90 };
                    preview = PanelFrameRenderer.Blank(output.Profile with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 }, quality);
                    previousFingerprint = fingerprint;
                    previewNotice = null;
                }
                await output.SendAsync(preview, options.JpegQuality);
                frameMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                Volatile.Write(ref status, "No active screens. Enable a screen to resume.");
                return new("", "", "", false, "", [], new(null, null, null, null, "", ""), null,
                    DeviceChoices(), DisplayProfileCatalog.All.Select(item => new PanelChoice(item.Id, item.Name)).ToArray(),
                    true, Volatile.Read(ref status), preview, [], frameMilliseconds, output.SubmittedFrames,
                    CodexProcessRunning: processRunning, FrameSuppressed: true);
            }
            var state = await StateAsync(options with { ThemeId = chosen.Id });
            if (previewNotice is not null || state.FrameJpeg is null) { throw new IOException(previewNotice ?? "The theme did not produce a frame."); }
            await output.SendAsync(state.FrameJpeg, options.JpegQuality);
            frameMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Volatile.Write(ref status, $"Display · requested {options.RefreshIntervalMilliseconds} ms · last update {frameMilliseconds:F0} ms · {options.JpegQuality} JPEG");
            return state with { Status = Volatile.Read(ref status), FrameMilliseconds = frameMilliseconds, SubmittedFrames = output.SubmittedFrames };
        }
        catch
        {
            CloseThemeOutput();
            Volatile.Write(ref status, "Display stopped. Check the connection and press Show on display to try again.");
            throw;
        }
    }

    /// <summary>Stops output and cancels any pending startup restore.</summary>
    private Task StopAsync()
    {
        CloseThemeOutput();
        Volatile.Write(ref status, "Display stopped.");
        return Task.CompletedTask;
    }

    /// <summary>Disposes the active display session and clears its selected device.</summary>
    private void CloseThemeOutput()
    {
        themeOutput?.Dispose();
        themeOutput = null;
        themeDeviceId = null;
        frameMilliseconds = null;
    }

    /// <summary>Deserializes request parameters or uses the permitted fallback.</summary>
    /// <typeparam name="T">Expected response or payload type used by this operation.</typeparam>
    /// <param name="request">Versioned engine request containing a method, correlation ID and optional JSON parameters.</param>
    /// <param name="fallback">Value to use when the optional input is absent.</param>
    private static T Parameters<T>(EngineRequest request, T? fallback = default) where T : class =>
        request.Parameters?.Deserialize<T>(PanelContract.Json) ?? fallback ?? throw new ArgumentException("Missing parameters.");

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose() { CloseThemeOutput(); themeRenderer.Dispose(); dashboard.Dispose(); }
}
