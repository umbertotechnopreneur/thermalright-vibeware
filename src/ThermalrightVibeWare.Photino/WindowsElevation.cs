/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: WindowsElevation.cs
 * Purpose: Requests administrator startup while preserving saved-session arguments.
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

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace ThermalrightVibeWare.Photino;

/// <summary>Requests Windows elevation before opening the studio or any of its children.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsElevation
{
    internal const string Argument = "--after-elevation";

    internal static bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>Requests elevation while preserving the studio launch arguments.</summary>
    /// <param name="arguments">Studio launch arguments preserved across elevation.</param>
    internal static bool StartAdministrator(string[] arguments)
    {
        if (IsAdministrator) { return false; }
        string? failure = null;
        try
        {
            using var current = Process.GetCurrentProcess();
            using var replacement = Process.Start(CreateStartInfo(current.Id, current.StartTime.ToUniversalTime().Ticks, arguments));
            if (replacement is not null) { return true; }
            failure = "Windows could not request administrator access.";
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223) { }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException)
        { failure = "Windows could not request administrator access."; Console.Error.WriteLine(error.Message); }
        _ = MessageBox(IntPtr.Zero,
            (failure is null ? "Administrator access was canceled." : failure) +
            "\n\nThermalright VibeWare is not running as administrator. Some CPU, motherboard and fan readings may be unavailable." +
            "\n\nYou can reopen as administrator under Sensor access.",
            "Thermalright VibeWare - administrator access", 0x00010030);
        return false;
    }

    /// <summary>Warns that the previous studio session could not be released.</summary>
    internal static void ShowSessionUnavailable() => _ = MessageBox(IntPtr.Zero,
        "Thermalright VibeWare could not open its session. Another copy may be running as administrator." +
        "\n\nAllow administrator access when reopening the app.", "Thermalright VibeWare - administrator access", 0x00010030);

    /// <summary>Builds an elevated studio restart with previous-process identity checks.</summary>
    /// <param name="previousId">Previous studio process identifier to verify before restart.</param>
    /// <param name="previousStart">Previous studio process start-time ticks used to reject PID reuse.</param>
    /// <param name="arguments">Studio launch arguments preserved across elevation.</param>
    internal static ProcessStartInfo CreateStartInfo(int previousId, long previousStart, IReadOnlyList<string>? arguments = null)
    {
        var executable = Environment.ProcessPath ?? throw new IOException("The app path is unavailable.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        { start.ArgumentList.Add(typeof(WindowsElevation).Assembly.Location); }
        start.ArgumentList.Add(Argument);
        start.ArgumentList.Add(previousId.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(previousStart.ToString(CultureInfo.InvariantCulture));
        foreach (var argument in arguments ?? []) { start.ArgumentList.Add(argument); }
        return start;
    }

    /// <summary>Waits for the identified previous studio before completing a restart.</summary>
    /// <param name="args">Command-line arguments including the command name.</param>
    internal static bool WaitForPreviousStudio(string[] args)
    {
        if (args.Length < 3 || args.Length > 5 || args[0] != Argument || !IsAdministrator
            || args.Skip(3).Any(argument => argument is not "--start-minimized" and not "--restore-last-session")
            || !int.TryParse(args[1], CultureInfo.InvariantCulture, out var id) || id <= 0 || id == Environment.ProcessId
            || !long.TryParse(args[2], CultureInfo.InvariantCulture, out var startedAt) || startedAt <= 0) { return false; }
        try
        {
            using var previous = Process.GetProcessById(id);
            if (previous.StartTime.ToUniversalTime().Ticks != startedAt
                || !string.Equals(previous.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            { return false; }
            // Acquire the single-instance mutex only after the former studio and its engine close.
            return previous.WaitForExit(65_000);
        }
        catch (ArgumentException) { return true; } // The validated launch can reach us after its parent exited.
        catch (InvalidOperationException) { return true; } // The retained parent exited while its metadata was being read.
        catch (Win32Exception) { return false; }
    }

    /// <summary>Shows a native Windows message dialog.</summary>
    /// <param name="owner">Native owner window for the message dialog.</param>
    /// <param name="text">Text to format, normalize or display in this operation.</param>
    /// <param name="title">Dialog title shown by Windows.</param>
    /// <param name="type">Native API type/flag selector for the requested operation.</param>
    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string title, uint type);
}
