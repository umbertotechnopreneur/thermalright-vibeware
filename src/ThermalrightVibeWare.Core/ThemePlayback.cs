/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: ThemePlayback.cs
 * Purpose: Runs explicitly requested theme rendering and physical output until cancellation.
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

namespace ThermalrightVibeWare.Core;

/// <summary>Explicit theme output to one selected panel; never reconnects or starts from a preview request.</summary>
public static class ThemePlayback
{

    /// <summary>Renders and submits the selected theme until cancellation or the requested duration.</summary>
    /// <param name="theme">Validated theme definition, including its local folder and category.</param>
    /// <param name="device">Explicitly selected discovered display descriptor; no automatic device selection occurs here.</param>
    /// <param name="profileId">Profile identifier; auto requests resolution from the device handshake where supported.</param>
    /// <param name="duration">Optional output duration; null runs until cancellation or disconnection.</param>
    /// <param name="progress">Optional sink for submitted-frame progress messages.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    /// <param name="light">Whether to use the light appearance.</param>
    /// <param name="ambientDrift">Whether the theme may animate its ambient background.</param>
    /// <param name="fontFamily">Printable font family name resolved by the Python renderer.</param>
    /// <param name="fontScale">Multiplier applied to the theme's text sizes.</param>
    /// <param name="refreshIntervalMilliseconds">Requested frame interval in milliseconds; this is not a measured frame rate.</param>
    /// <param name="jpegQuality">Requested JPEG preset: poor, low, medium or high.</param>
    /// <param name="saturationPercent">Saturation from 0 to 200 percent; 100 is neutral.</param>
    /// <param name="hueDegrees">Hue rotation from -180 to 180 degrees; zero is neutral.</param>
    /// <param name="showSpectrum">Whether Multimedia themes may use Windows output loopback analysis.</param>
    /// <param name="brightnessPercent">Brightness offset from -100 to 100 percent; zero is neutral.</param>
    /// <param name="contrastPercent">Contrast from 0 to 200 percent; 100 is neutral.</param>
    public static async Task RunAsync(ThemeDefinition theme, DisplayDevice device, string profileId,
        TimeSpan? duration, IProgress<string>? progress, CancellationToken cancellationToken,
        bool light = false, bool ambientDrift = false, string fontFamily = "Cascadia Mono", double fontScale = 1,
        int refreshIntervalMilliseconds = 1000, string jpegQuality = "high",
        double saturationPercent = 100, double hueDegrees = 0, bool showSpectrum = true,
        double brightnessPercent = 0, double contrastPercent = 100)
    {
        if (duration is { } requestedDuration && (requestedDuration <= TimeSpan.Zero || requestedDuration > TimeSpan.FromDays(1)))
        { throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be between one second and one day."); }
        if (refreshIntervalMilliseconds is < 25 or > 1000)
        { throw new ArgumentOutOfRangeException(nameof(refreshIntervalMilliseconds), "Refresh interval must be between 25 and 1000 ms."); }
        if (jpegQuality is not ("poor" or "low" or "medium" or "high")) { throw new ArgumentException("JPEG quality must be poor, low, medium or high."); }
        PythonThemeRenderer.ValidateColorAdjustments(saturationPercent, hueDegrees, brightnessPercent, contrastPercent);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (duration is { } limit) { stop.CancelAfter(limit); }
        var token = stop.Token;
        using var service = new DashboardService(WindowsDashboardProviders.Create());
        using var renderer = new PythonThemeRenderer();
        var codexProcess = new CodexProcessMonitor();
        byte[]? inactiveFrame = null;
        try
        {
            progress?.Report($"Reading the selected panel's profile: {device.Id}.");
            using var output = await ThemePanelOutput.OpenAsync(device, profileId, token);
            var profile = output.Profile;
            progress?.Report($"{theme.Name} · {profile.Name} · requested {refreshIntervalMilliseconds} ms. Ctrl+C stops.");
            while (!token.IsCancellationRequested)
            {
                var started = Environment.TickCount64;
                if (theme.Category == "codex" && !codexProcess.IsRunning())
                {
                    inactiveFrame ??= PanelFrameRenderer.Blank(profile with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 });
                    if (await output.SendAsync(inactiveFrame, jpegQuality, token))
                    { progress?.Report("Codex is closed. Its screen is hidden until its process is running."); }
                    await Task.Delay(1000, token);
                    continue;
                }
                var snapshot = await service.ReadAsync(includeSystem: false, includeMedia: theme.Category == "multimedia",
                    analyzePlayback: theme.Category == "multimedia", includeSensors: theme.Category == "system");
                token.ThrowIfCancellationRequested();
                var jpeg = await renderer.RenderAsync(theme, snapshot, profile with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 },
                    light, ambientDrift, fontFamily, fontScale, jpegQuality, token, saturationPercent, hueDegrees, showSpectrum,
                    brightnessPercent, contrastPercent);
                if (await output.SendAsync(jpeg, jpegQuality, token))
                { progress?.Report($"{theme.Name} · frame {output.SubmittedFrames} submitted · {profile.Width} × {profile.Height}"); }
                // One in-flight frame only. Slow rendering/USB reduces the rate instead of building a queue.
                var interval = renderer.FrameIntervalMilliseconds(refreshIntervalMilliseconds);
                await Task.Delay((int)Math.Max(1, interval - (Environment.TickCount64 - started)), token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        progress?.Report("Theme playback stopped.");
    }
}
