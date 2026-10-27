/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: Commands.Theme.cs
 * Purpose: Parses and runs explicitly requested theme playback on the selected display.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Created with AI: OpenAI Codex; extracted existing first-party source; 2026-10-27.
 * Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard file headers.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */

using ThermalrightVibeWare.Core;
using Spectre.Console;
using System.Text;
using System.Globalization;

internal static partial class ThermalrightVibeWareCli
{
    /// <summary>Parses an explicit theme playback request.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Theme(IReadOnlyList<string> args)
    {
        var export = GetOption(args, "--export");
        var deviceNumber = GetOption(args, "--device");
        if (args.Count < 2 || args[1].StartsWith("--", StringComparison.Ordinal) || (export is null) == (deviceNumber is null))
        { return Fail("Usage: trv theme <id> (--export <new.jpg> | --device <number>) [--profile <id>] [--font \"Cascadia Mono\"] [--font-scale 1] [--light] [--saturation 100] [--hue 0] [--brightness 0] [--contrast 100] [--no-spectrum] [--seconds N] [--interval-ms 1000] [--quality high]"); }
        var theme = ThemeCatalog.Read().Themes.FirstOrDefault(item => item.Id == args[1])
            ?? throw new ArgumentException("Theme not found. Run 'trv themes'.");
        var font = GetOption(args, "--font") ?? "Cascadia Mono";
        var scaleText = GetOption(args, "--font-scale") ?? "1";
        var intervalText = GetOption(args, "--interval-ms") ?? "1000";
        var quality = (GetOption(args, "--quality") ?? "high").ToLowerInvariant();
        var saturation = GetColorOption(args, "--saturation", 100);
        var hue = GetColorOption(args, "--hue", 0);
        var brightness = GetColorOption(args, "--brightness", 0);
        var contrast = GetColorOption(args, "--contrast", 100);
        PythonThemeRenderer.ValidateColorAdjustments(saturation, hue, brightness, contrast);
        var showSpectrum = !args.Contains("--no-spectrum");
        if (quality is not ("poor" or "low" or "medium" or "high")) { throw new ArgumentException("--quality must be poor, low, medium or high."); }
        if (!int.TryParse(intervalText, out var interval) || interval is < 25 or > 1000)
        { throw new ArgumentException("--interval-ms must be between 25 and 1000."); }
        if (!double.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
        { throw new ArgumentException("--font-scale must be a number from 0.65 to 1.6 (use a decimal point)."); }
        if (deviceNumber is not null)
        {
            if (!double.IsFinite(scale) || scale is < 0.65 or > 1.6 || font.Length is 0 or > 128 || font.Any(char.IsControl))
            { throw new ArgumentException("Choose a printable font name and --font-scale between 0.65 and 1.6."); }
            var device = GetDevice(new[] { "theme", deviceNumber });
            var profileId = GetOption(args, "--profile") ?? "auto";
            TimeSpan? duration = null;
            if (GetOption(args, "--seconds") is { } secondsText)
            {
                if (!int.TryParse(secondsText, out var seconds) || seconds is < 1 or > 86400)
                { throw new ArgumentException("--seconds must be between 1 and 86400."); }
                duration = TimeSpan.FromSeconds(seconds);
            }
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += cancel;
            try
            {
                AnsiConsole.WriteLine($"Sending theme {theme.Name} to device {device.Id}. Profile: {profileId}.");
                ThemePlayback.RunAsync(theme, device, profileId, duration, new ThemeProgress(), cancellation.Token,
                    args.Contains("--light"), args.Contains("--ambient-drift"), font, scale, interval, quality,
                    saturation, hue, showSpectrum, brightness, contrast).GetAwaiter().GetResult();
            }
            finally { Console.CancelKeyPress -= cancel; }
            return 0;
        }
        if (GetOption(args, "--seconds") is not null) { throw new ArgumentException("--seconds applies only to explicit --device playback."); }
        var profile = DisplayProfileCatalog.GetRequired(GetOption(args, "--profile") ?? "wide-1280x480") with { Encoding = PanelPixelEncoding.Jpeg, WireRotation = 0 };
        var path = Path.GetFullPath(export!);
        if (File.Exists(path)) { throw new IOException("The export file already exists. Choose a new path."); }
        using var service = new DashboardService(WindowsDashboardProviders.Create());
        using var renderer = new PythonThemeRenderer();
        var snapshot = service.ReadAsync(includeSystem: false, includeMedia: theme.Category == "multimedia",
            includeSensors: theme.Category == "system").GetAwaiter().GetResult();
        var frame = renderer.RenderAsync(theme, snapshot, profile, args.Contains("--light"), args.Contains("--ambient-drift"), font, scale, quality,
            saturationPercent: saturation, hueDegrees: hue, showSpectrum: showSpectrum,
            brightnessPercent: brightness, contrastPercent: contrast).GetAwaiter().GetResult();
        using var output = new FileStream(path, FileMode.CreateNew);
        output.Write(frame);
        AnsiConsole.WriteLine($"{theme.Name}: {profile.Width} × {profile.Height} preview saved to {path}");
        foreach (var notice in snapshot.Notices) { AnsiConsole.WriteLine(notice); }
        return 0;
    }
}
