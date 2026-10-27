/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: Commands.Display.cs
 * Purpose: Parses discovery, settings, preview and explicitly requested physical display commands.
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
    /// <summary>Lists supported displays without opening a panel session.</summary>
    private static int Discover()
    {
        var devices = DisplayDiscovery.FindDevices();
        Console.WriteLine("ThermalrightVibeWare — my compatible USB panels");
        if (devices.Count == 0)
        {
            var attached = WinUsbDisplayDiscovery.FindAttachedPanels();
            if (attached.Count == 0)
            {
                Console.WriteLine("No 0416:5302 panel is connected.");
                return 1;
            }

            Console.WriteLine($"I found {attached.Count} attached panel(s), but no supported HID or WinUSB interface is available.");
            return 2;
        }

        for (var index = 0; index < devices.Count; index++)
        {
            Console.WriteLine($"{index + 1}. {devices[index].Id} · {devices[index].Connection}");
        }

        return 0;
    }

    /// <summary>Lists the supported panel profiles.</summary>
    private static int Profiles()
    {
        Console.WriteLine("ThermalrightVibeWare — included screen profiles");
        foreach (var profile in DisplayProfileCatalog.All)
        {
            Console.WriteLine($"{profile.Id,-20} {profile.Width} x {profile.Height}  {profile.Encoding}");
        }

        Console.WriteLine("Custom geometry: --width <pixels> --height <pixels>");
        return 0;
    }

    /// <summary>Parses an explicit physical-device information request.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Probe(IReadOnlyList<string> args)
    {
        var device = GetDevice(args);
        Console.WriteLine($"Checking my {device.Id} panel. This sends only the device-information handshake.");
        using var transport = PanelSession.Open(device);
        var probe = transport.ProbeAsync().GetAwaiter().GetResult();
        Console.WriteLine(probe.Message);
        Console.WriteLine($"PM {probe.ProductMode:X2} / SUB {probe.SubMode:X2} · serial {probe.Serial}");
        Console.WriteLine($"Profile: {probe.DetectedProfile?.Name ?? "unknown; choose an explicit profile"}");
        Console.WriteLine($"Reply: {probe.ResponseHex}");
        return probe.IsCompatible ? 0 : 2;
    }

    /// <summary>Parses an explicit diagnostic pattern write to the selected display.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Pattern(IReadOnlyList<string> args)
    {
        var device = GetDevice(args);
        var profileId = args.Count > 2 && !args[2].StartsWith("--", StringComparison.Ordinal) ? args[2] : "auto";
        var custom = profileId == "custom" ? GetProfile(args) : null;
        Console.WriteLine($"Sending a diagnostic pattern to {device.Id}. Profile: {profileId}.");
        var result = new PanelApplication().SendAsync(device, new PanelPictureRequest(profileId, CustomProfile: custom)).GetAwaiter().GetResult();
        Console.WriteLine(result.Message);
        return 0;
    }

    /// <summary>Parses an explicit repeated still-frame output request.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Hold(IReadOnlyList<string> args)
    {
        var device = GetDevice(args);
        var profileId = args.Count > 2 ? args[2] : "auto";
        Console.WriteLine($"Sending a diagnostic chart to {device.Id} for 20 seconds at 1 fps. Profile: {profileId}.");
        var progress = new Progress<string>(message => Console.WriteLine(message));
        new PanelApplication().PlayAsync(device, new PanelPictureRequest(profileId), TimeSpan.FromSeconds(20), progress, CancellationToken.None).GetAwaiter().GetResult();
        Console.WriteLine("Diagnostic playback ended.");
        return 0;
    }

    /// <summary>Parses an explicit one-shot or repeated image output request.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    /// <param name="play">Whether to repeat the image until cancellation instead of submitting it once.</param>
    private static int Image(IReadOnlyList<string> args, bool play)
    {
        if (args.Count < 3 || args[2].StartsWith("--", StringComparison.Ordinal))
        {
            return Fail("Usage: trv image|play <device-number> <image-path> [profile] [--rotate 0|90|180|270]");
        }
        var app = new PanelApplication();
        var defaults = app.LoadSettings();
        var device = GetDevice(args);
        var profile = args.Count > 3 && !args[3].StartsWith("--", StringComparison.Ordinal) ? args[3] : defaults.ProfileId;
        var rotation = GetRotation(args, defaults.Rotation);
        var request = new PanelPictureRequest(profile, Path.GetFullPath(args[2]), rotation);
        Console.WriteLine($"{(play ? "Displaying" : "Sending")} {Path.GetFileName(request.ImagePath)} on {device.Id}. Profile: {profile}, rotation: {rotation}.");
        if (!play)
        {
            Console.WriteLine(app.SendAsync(device, request).GetAwaiter().GetResult().Message);
            return 0;
        }
        var secondsOption = GetOption(args, "--seconds");
        TimeSpan? duration = null;
        if (secondsOption is not null)
        {
            if (!int.TryParse(secondsOption, out var seconds) || seconds is < 1 or > 86400)
            { throw new ArgumentException("--seconds must be between 1 and 86400."); }
            duration = TimeSpan.FromSeconds(seconds);
        }
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        Console.WriteLine("1 fps. Press Ctrl+C to stop. The image is encoded once and reused.");
        try
        {
            app.PlayAsync(device, request, duration, new Progress<string>(Console.WriteLine), cancellation.Token).GetAwaiter().GetResult();
        }
        finally { Console.CancelKeyPress -= cancel; }
        return 0;
    }

    /// <summary>Handles explicit saved panel settings commands.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Settings(IReadOnlyList<string> args)
    {
        var app = new PanelApplication();
        var current = app.LoadSettings();
        if (args.Count > 1)
        {
            var image = GetOption(args, "--image");
            current = current with
            {
                ProfileId = GetOption(args, "--profile") ?? current.ProfileId,
                Rotation = GetRotation(args, current.Rotation),
                ImagePath = image is null ? current.ImagePath : Path.GetFullPath(image)
            };
            app.SaveSettings(current);
        }
        Console.WriteLine($"Profile: {current.ProfileId}; rotation: {current.Rotation}; image: {current.ImagePath ?? "none"}");
        return 0;
    }

    /// <summary>Renders an explicitly requested local preview file.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Preview(IReadOnlyList<string> args)
    {
        if (args.Count != 3) { return Fail("Usage: trv preview <jpeg-profile> <output.jpg>"); }
        var profile = DisplayProfileCatalog.GetRequired(args[1]);
        if (profile.Encoding != PanelPixelEncoding.Jpeg) { return Fail("Choose a JPEG profile for this preview command."); }
        // CreateNew avoids replacing an existing user image during a preview operation.
        using var output = new FileStream(Path.GetFullPath(args[2]), FileMode.CreateNew);
        output.Write(PanelFrameRenderer.Pattern(profile));
        Console.WriteLine($"Saved {profile.Width} x {profile.Height} JPEG preview. No panel was opened.");
        return 0;
    }
}
