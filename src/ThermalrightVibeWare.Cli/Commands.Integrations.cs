/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: Commands.Integrations.cs
 * Purpose: Handles local studio launch, theme/font listing, Codex setup and sensor/log diagnostics.
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
    /// <summary>Opens the bundled studio with the permitted launch arguments.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Gui(IReadOnlyList<string> args)
    {
        if (args.Count > 2 || args.Count == 2 && args[1] != "--start-minimized")
        { return Fail("Usage: trv gui [--start-minimized]"); }
        var executable = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "ThermalrightVibeWare.exe"));
        if (!File.Exists(executable)) { throw new FileNotFoundException("Open the GUI from the portable folder. ThermalrightVibeWare.exe is missing beside engine/.", executable); }
        var start = new System.Diagnostics.ProcessStartInfo(executable)
        { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
        if (args.Contains("--start-minimized")) { start.ArgumentList.Add("--start-minimized"); }
        using var process = System.Diagnostics.Process.Start(start);
        if (process is null) { throw new IOException("The Thermalright VibeWare window could not open."); }
        AnsiConsole.WriteLine("Thermalright VibeWare opened. Its window owns one hidden CLI engine.");
        return 0;
    }

    /// <summary>Lists themes discovered from local pack manifests.</summary>
    private static int Themes()
    {
        var catalog = ThemeCatalog.Read();
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Theme ID").AddColumn("Name").AddColumn("Category").AddColumn("Pack");
        foreach (var theme in catalog.Themes)
        { table.AddRow(Markup.Escape(theme.Id), Markup.Escape(theme.Name), theme.Category == "codex" ? "OpenAI Codex" : "Multimedia", Markup.Escape(theme.PackId)); }
        AnsiConsole.Write(table);
        foreach (var notice in catalog.Notices) { AnsiConsole.WriteLine(notice); }
        AnsiConsole.WriteLine($"My packs: {ThemeCatalog.UserThemesPath}");
        AnsiConsole.WriteLine("Theme folders are listed from manifests. Scripts are only loaded for an explicit preview.");
        return catalog.Themes.Count > 0 ? 0 : 1;
    }

    /// <summary>Lists font families available to the configured renderer.</summary>
    private static int Fonts()
    {
        using var renderer = new PythonThemeRenderer();
        var catalog = renderer.ListFontsAsync().GetAwaiter().GetResult();
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Monospace font family");
        foreach (var family in catalog.Families) { table.AddRow(Markup.Escape(family)); }
        AnsiConsole.Write(table);
        if (catalog.Notice is not null) { AnsiConsole.WriteLine(catalog.Notice); }
        AnsiConsole.WriteLine("Local renderer fonts. No theme, audio source or USB panel was opened.");
        return 0;
    }

    /// <summary>Handles explicit local Codex setup/status commands.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Codex(IReadOnlyList<string> args)
    {
        if (args.Count != 2 || args[1] is not ("status" or "prepare"))
        { return Fail("Usage: trv codex status|prepare"); }
        var result = args[1] == "prepare" ? CodexIntegration.PrepareAsync().GetAwaiter().GetResult() : CodexIntegration.Status();
        AnsiConsole.MarkupLine($"[bold]Thermalright VibeWare Codex:[/] {Markup.Escape(result.Status)}");
        AnsiConsole.WriteLine($"Plugin files: {(result.Prepared ? "prepared" : "not prepared")}; personal marketplace: {(result.Listed ? "listed" : "not listed")}.");
        if (result.ActivitySeen) { AnsiConsole.WriteLine($"Last local event: {result.LastEvent} at {result.LastEventUtc}"); }
        AnsiConsole.WriteLine(result.Instructions);
        return 0;
    }

    /// <summary>Handles explicit local logging status and settings commands.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Diagnostics(IReadOnlyList<string> args)
    {
        if (args.Count != 2 || args[1] is not ("status" or "on" or "off" or "open"))
        { return Fail("Usage: trv diagnostics status|on|off|open"); }
        if (args[1] == "open")
        {
            if (!DiagnosticLog.Open()) { return Fail("No local engine log exists yet."); }
            AnsiConsole.WriteLine("Opened the local engine log.");
            return 0;
        }
        var result = args[1] switch
        {
            "on" => DiagnosticLog.SetEnabled(true),
            "off" => DiagnosticLog.SetEnabled(false),
            _ => DiagnosticLog.Status()
        };
        AnsiConsole.WriteLine($"Error logging: {(result.Enabled ? "on" : "off")}. {result.LogPath}");
        AnsiConsole.WriteLine("Local only. The log omits prompts, chat titles, commands, tool arguments and results.");
        return 0;
    }

    /// <summary>Handles read-only sensor diagnostics and driver-location commands.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    private static int Sensors(IReadOnlyList<string> args)
    {
        if (args.Count > 2 || args.Count == 2 && args[1] is not ("--diagnose" or "--all"))
        { return Fail("Usage: trv sensors [--diagnose|--all]"); }
        using var sensors = new WindowsMetricsService(allowHardwareAccess: true);
        if (args.Contains("--all"))
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(sensors.ReadSensorDebug(), ThermalrightVibeWare.Contracts.PanelContract.Json));
            return sensors.InitializationFailureDetails is null ? 0 : 1;
        }
        var snapshot = sensors.ReadSensors();
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(snapshot, ThermalrightVibeWare.Contracts.PanelContract.Json));
        if (snapshot.Notice is { } notice) { Console.Error.WriteLine(notice); }
        if (args.Contains("--diagnose") && sensors.InitializationFailureDetails is { } details)
        { Console.Error.WriteLine(details); }
        return sensors.InitializationFailureDetails is null ? 0 : 1;
    }
}
