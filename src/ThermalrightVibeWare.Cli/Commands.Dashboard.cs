/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: Commands.Dashboard.cs
 * Purpose: Presents dashboard/media observations and explicit Windows playback commands.
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
    /// <summary>Parses dashboard options and displays local observations.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Dashboard(IReadOnlyList<string> args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            if (args.Skip(1).Any(argument => argument != "--watch")) { throw new ArgumentException("Usage: trv dashboard [--watch]. Use trv theme <id> for rendering and display output."); }
            using var service = new DashboardService(WindowsDashboardProviders.Create());
            do
            {
                var snapshot = service.ReadAsync().GetAwaiter().GetResult();
                ShowDashboard(snapshot);
                if (!args.Contains("--watch")) { break; }
                Task.Delay(2000, cancellation.Token).GetAwaiter().GetResult();
            } while (!cancellation.IsCancellationRequested);
            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return 0; }
        finally { Console.CancelKeyPress -= cancel; }
    }

    /// <summary>Prints one dashboard observation and its availability notices.</summary>
    /// <param name="snapshot">Current dashboard observation, including availability notices.</param>
    private static void ShowDashboard(DashboardSnapshot snapshot)
    {
        var media = snapshot.Media;
        AnsiConsole.Write(new Panel(new Markup($"[bold white]{Markup.Escape(media.Title)}[/]\n[grey]{Markup.Escape(media.Artist)}[/]\n\n[rgb(221,176,235)]{(media.Playing ? "● Playing" : "Ⅱ Paused / ready")}[/]"))
            .Header("[bold] NOW PLAYING [/]").BorderColor(new Color(199, 123, 211)).Padding(2, 1));
        ShowVolumes(snapshot.Volumes);
        var metrics = snapshot.Metrics;
        var table = new Table().Border(TableBorder.Rounded).AddColumn("System").AddColumn("Usage").AddColumn("Temperature");
        // value is a measured aggregate reading; unit is its display suffix. Null stays unavailable.
        static string Metric(double? value, string unit) => value is { } number ? $"{number:F0}{unit}" : "—";
        table.AddRow("CPU", Metric(metrics.CpuUsage, "%"), Metric(metrics.CpuTemperature, "°C"));
        table.AddRow(Markup.Escape(metrics.GpuName), Metric(metrics.GpuUsage, "%"), Metric(metrics.GpuTemperature, "°C"));
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]CPU temperature: {Markup.Escape(metrics.CpuTemperatureSource)}[/]");
        if (snapshot.Network is { } network)
        { AnsiConsole.WriteLine($"Network ↓ {NetworkTrafficService.Size(network.DownloadBytesPerSecond)}/s · ↑ {NetworkTrafficService.Size(network.UploadBytesPerSecond)}/s · total ↓ {NetworkTrafficService.Size(network.DownloadBytes)}, ↑ {NetworkTrafficService.Size(network.UploadBytes)} since start."); }
        foreach (var notice in snapshot.Notices) { AnsiConsole.WriteLine(notice); }
    }

    /// <summary>Prints per-session volume percentages and measured peak levels.</summary>
    /// <param name="volumes">Read-only audio-session volume and peak observations.</param>
    private static void ShowVolumes(IReadOnlyList<AppVolume> volumes)
    {
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Application / audio session").AddColumn("Volume").AddColumn("Level / -60..0 dBFS");
        for (var i = 0; i < volumes.Count; i++)
        {
            var volume = volumes[i];
            var filled = (int)Math.Round(AudioLevel.Fraction(volume.Peak) * 16);
            table.AddRow(Markup.Escape(volume.Name), volume.Muted ? $"{volume.Percent:F0}% · muted" : $"{volume.Percent:F0}%",
                $"[green]{new string('━', Math.Min(filled, 10))}[/][yellow]{new string('━', Math.Clamp(filled - 10, 0, 3))}[/][magenta]{new string('━', Math.Max(0, filled - 13))}[/][grey]{new string('─', 16 - filled)}[/] {AudioLevel.Label(volume.Peak)}");
        }
        AnsiConsole.Write(table);
    }

    /// <summary>Parses and sends an explicitly requested Windows playback command.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Media(IReadOnlyList<string> args)
    {
        if (args.Count != 2) { return Fail("Usage: trv media toggle|play|pause|next|previous"); }
        new WindowsMediaService().ControlAsync(args[1]).GetAwaiter().GetResult();
        AnsiConsole.WriteLine("Media command accepted by the selected Windows session.");
        return 0;
    }

    /// <summary>Prints currently available Windows audio session levels.</summary>
    private static int Volumes()
    {
        using var service = new WindowsVolumeService();
        ShowVolumes(service.Read());
        AnsiConsole.WriteLine("Read-only. Volume is the configured gain; the meter shows instantaneous playback level.");
        return 0;
    }
}
