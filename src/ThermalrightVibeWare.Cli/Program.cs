/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: Program.cs
 * Purpose: Parses CLI commands and keeps physical output behind explicit commands.
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

using ThermalrightVibeWare.Core;
using Spectre.Console;
using System.Text;
using System.Globalization;

return args is ["--stdio"] ? await EngineServer.RunAsync() : await ThermalrightVibeWareCli.RunAsync(args);

internal static partial class ThermalrightVibeWareCli
{
    /// <summary>Runs the small, explicit command surface for panel discovery and control.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    public static Task<int> RunAsync(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (!string.Equals(args.FirstOrDefault(), "sensors", StringComparison.OrdinalIgnoreCase)) { Banner(); }
            return Task.FromResult(args.FirstOrDefault()?.ToLowerInvariant() switch
            {
                "discover" => Discover(),
                "profiles" => Profiles(),
                "probe" => Probe(args),
                "pattern" => Pattern(args),
                "hold" => Hold(args),
                "image" => Image(args, false),
                "play" => Image(args, true),
                "settings" => Settings(args),
                "preview" => Preview(args),
                "dashboard" => Dashboard(args),
                "media" => Media(args),
                "volumes" => Volumes(),
                "themes" => Themes(),
                "fonts" => Fonts(),
                "theme" => Theme(args),
                "codex" => Codex(args),
                "diagnostics" => Diagnostics(args),
                "sensors" => Sensors(args),
                "gui" => Gui(args),
                "help" or "--help" or "-h" or null => Help(),
                _ => Fail("I do not know that command. Run 'trv help'.")
            });
        }
        catch (Exception exception)
        {
            return Task.FromResult(Fail(exception.Message));
        }
    }

    /// <summary>Prints the CLI product heading.</summary>
    private static void Banner()
    {
        AnsiConsole.Write(new FigletText("Thermalright VibeWare").Color(new Color(199, 123, 211)));
        AnsiConsole.MarkupLine("[grey]by Umberto Giacobbi[/]");
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
    }

    private sealed class ThemeProgress : IProgress<string>
    {

        /// <summary>Prints a playback progress message to the console.</summary>
        /// <param name="value">Playback progress message printed to the console.</param>
        public void Report(string value) => AnsiConsole.WriteLine(value);
    }

    /// <summary>Parses an allowed rotation or uses the supplied default.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    /// <param name="defaultRotation">Rotation used when the command line omits an explicit value.</param>
    private static int GetRotation(IReadOnlyList<string> args, int defaultRotation)
    {
        var value = GetOption(args, "--rotate");
        if (value is null) { return defaultRotation; }
        if (!int.TryParse(value, out var rotation) || rotation is not (0 or 90 or 180 or 270))
        { throw new ArgumentException("--rotate must be 0, 90, 180, or 270."); }
        return rotation;
    }

    /// <summary>Resolves the explicitly selected attached device.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static DisplayDevice GetDevice(IReadOnlyList<string> args)
    {
        if (args.Count < 2 || !int.TryParse(args[1], out var requested) || requested < 1)
        {
            throw new ArgumentException("Choose a device number from 'trv discover'.");
        }

        var devices = DisplayDiscovery.FindDevices();
        return devices.ElementAtOrDefault(requested - 1)
            ?? throw new ArgumentException($"Device {requested} is not connected. Run 'trv discover'.");
    }

    /// <summary>Resolves an explicit profile or custom screen dimensions.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static DisplayProfile GetProfile(IReadOnlyList<string> args)
    {
        var customWidth = GetOption(args, "--width");
        var customHeight = GetOption(args, "--height");
        if (customWidth is not null || customHeight is not null)
        {
            if (!int.TryParse(customWidth, out var width) || !int.TryParse(customHeight, out var height))
            {
                throw new ArgumentException("Custom geometry needs both --width and --height.");
            }

            return DisplayProfile.CreateCustom(width, height);
        }

        return DisplayProfileCatalog.GetRequired(args[2]);
    }

    /// <summary>Reads the value following a named command-line option.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    /// <param name="option">Exact command-line switch to look up.</param>
    private static string? GetOption(IReadOnlyList<string> args, string option)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], option, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    /// <summary>Parses a finite color-control value or uses the supplied fallback.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    /// <param name="option">Exact command-line switch to look up.</param>
    /// <param name="fallback">Value to use when the optional input is absent.</param>
    private static double GetColorOption(IReadOnlyList<string> args, string option, double fallback)
    {
        var text = GetOption(args, option);
        if (text is null && !args.Contains(option)) { return fallback; }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
        { throw new ArgumentException($"{option} needs a finite number (use a decimal point)."); }
        return value;
    }

    /// <summary>Prints CLI usage and the commands that write to physical devices.</summary>
    private static int Help()
    {
        var commands = new Table().Border(TableBorder.Rounded).AddColumn("Command").AddColumn("What it does");
        commands.AddRow("[bold]trv dashboard[/]", "Now playing, app volumes, CPU / GPU. No USB writes.");
        commands.AddRow("trv dashboard --watch", "Refresh the terminal every 2 seconds. Ctrl+C stops.");
        commands.AddRow("trv media toggle|play|pause|next|previous", "Control the current generic Windows media session.");
        commands.AddRow("trv volumes", "Read-only app volume percentages and colored peak meters.");
        commands.AddRow("trv discover / trv profiles", "List connected panels / supported geometries.");
        commands.AddRow("trv themes", "List installed Multimedia and OpenAI Codex themes from their folders.");
        commands.AddRow("trv fonts", "List installed monospace families available to the Python/Skia renderer.");
        commands.AddRow("trv codex status|prepare", "Check or prepare my optional Codex plugin. Trust its hooks in Codex.");
        commands.AddRow("trv diagnostics status|on|off|open", "Opt-in local error log, capped at 512 KiB plus one backup.");
        commands.AddRow("trv sensors [--diagnose|--all]", "Read hardware once. --all returns the raw device/sensor tree; --diagnose prints initialization details. No audio or USB.");
        commands.AddRow("trv gui", "Open the desktop window from the portable folder.");
        commands.AddRow("trv gui --start-minimized", "Start in the Windows tray. Reuses the existing studio instance.");
        commands.AddRow("trv theme media.aurora --export frame.jpg", "Render a Python/Skia theme to a new file. No USB writes.");
        commands.AddRow("trv theme <id> --export frame.jpg --font <name> --light", "Use the same font and light-mode options as the UI.");
        commands.AddRow("trv theme media.aurora --device 1 --seconds 60", "Explicitly stream a theme to one panel at a target 1 fps; Ctrl+C stops.");
        commands.AddRow("trv theme media.aurora --device 1 --interval-ms 250", "Request updates every 250 ms; rendering and USB can be slower.");
        commands.AddRow("trv theme media.aurora --device 1 --quality medium", "JPEG quality: poor, low, medium or high. The panel size limit still applies.");
        commands.AddRow("trv theme media.aurora --device 1 --interval-ms 25 --quality poor", "Aggressive requested interval, very strong JPEG compression. Actual updates can be slower.");
        commands.AddRow("trv theme <id> --export frame.jpg --saturation 120 --hue -20", "Adjust saturation (0–200%) and hue (-180–180°). Also works with --device.");
        commands.AddRow("trv theme <id> --export frame.jpg --brightness 10 --contrast 115", "Adjust brightness (-100–100%) and contrast (0–200%). Themes can override color values in Python.");
        commands.AddRow("trv theme <id> --device 1 --no-spectrum", "Hide FFT bars in themes that support it. Keep VU meters and decibel values.");
        commands.AddRow("trv --stdio", "JSON Lines engine v1. No banner, no network listener, explicit USB start.");
        AnsiConsole.Write(commands);
        AnsiConsole.MarkupLine("[grey]Panel tools (these touch hardware only when explicitly invoked):[/]");
        Console.WriteLine("trv discover");
        Console.WriteLine("trv profiles");
        Console.WriteLine("trv probe <device-number>");
        Console.WriteLine("trv pattern <device-number> [auto|profile]");
        Console.WriteLine("trv hold <device-number> [auto|profile]  (20-second diagnostic playback)");
        Console.WriteLine("trv pattern <device-number> custom --width 320 --height 320");
        Console.WriteLine("trv image <device-number> <image-path> [profile] [--rotate 0|90|180|270]");
        Console.WriteLine("trv play <device-number> <image-path> [profile] [--seconds 20]");
        Console.WriteLine("trv settings [--profile auto|profile] [--rotate 0|90|180|270] [--image path]");
        Console.WriteLine("trv preview <jpeg-profile> <output.jpg>");
        return 0;
    }

    /// <summary>Prints a command error and returns a failure exit code.</summary>
    /// <param name="message">User-facing text or registered Windows message name used by this call.</param>
    private static int Fail(string message)
    {
        Console.Error.WriteLine($"ThermalrightVibeWare: {message}");
        return 1;
    }
}
