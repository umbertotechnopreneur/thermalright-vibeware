/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: CodexIntegration.cs
 * Purpose: Reports and explicitly prepares the bundled local Codex plugin.
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

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Core;

/// <summary>Opt-in local staging only. Codex installation and hook trust stay in Codex.</summary>
public static class CodexIntegration
{
    private const string PluginName = "thermalright-vibeware-codex";
    private const string SourcePath = "./.codex/plugins/thermalright-vibeware-codex";
    private const string Instructions = "Press Prepare only if you want Codex activity. Then restart Codex, install Thermalright VibeWare Codex from your personal Plugins Directory, and review and trust its hooks there. No account login is needed.";

    /// <summary>Reads local plugin setup and activity status without preparing the plugin.</summary>
    public static CodexSetupStatus Status()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var staged = Path.Combine(home, ".codex", "plugins", PluginName, "plugin.json");
        var marketplace = Path.Combine(home, ".agents", "plugins", "marketplace.json");
        var prepared = false;
        var listed = false;
        try
        {
            using var manifest = ReadSmallJson(staged, 64 * 1024);
            if (manifest is not null && manifest.RootElement.ValueKind == JsonValueKind.Object &&
                manifest.RootElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
            { prepared = name.GetString() == PluginName; }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        try
        {
            using var catalog = ReadSmallJson(marketplace, 1024 * 1024);
            if (catalog is not null && catalog.RootElement.ValueKind == JsonValueKind.Object &&
                catalog.RootElement.TryGetProperty("plugins", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in entries.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object || !Text(item, "name").Equals(PluginName, StringComparison.Ordinal)) { continue; }
                    if (item.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.Object &&
                        Text(source, "source") == "local" && Text(source, "path") == SourcePath) { listed = true; break; }
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }

        string? lastEvent = null;
        string? lastEventUtc = null;
        var current = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThermalrightVibeWare", "codex", "current.json");
        try
        {
            using var activity = ReadSmallJson(current, 8 * 1024);
            if (activity is not null && activity.RootElement.ValueKind == JsonValueKind.Object &&
                Text(activity.RootElement, "kind") == "codex.activity" &&
                activity.RootElement.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number &&
                version.TryGetInt32(out var schema) && schema == 1)
            {
                var timestamp = Text(activity.RootElement, "atUtc");
                if (DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) &&
                    at >= DateTimeOffset.UtcNow.AddMinutes(-5) && at <= DateTimeOffset.UtcNow.AddMinutes(1))
                {
                    lastEventUtc = at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
                    var eventName = Text(activity.RootElement, "event");
                    if (KnownEvents.Contains(eventName)) { lastEvent = eventName; }
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        var activitySeen = lastEvent is not null;
        var status = !prepared || !listed ? "Not prepared" : activitySeen ? "Recent Codex activity seen" : "Prepared; install and trust hooks in Codex";
        return new(prepared, listed, activitySeen, status, lastEvent, lastEventUtc, Instructions);
    }

    /// <summary>Prepares the local Codex plugin without trusting a theme-provided hook.</summary>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    public static async Task<CodexSetupStatus> PrepareAsync(CancellationToken cancellationToken = default)
    {
        var source = ThemeCatalog.FindContentDirectory("plugins");
        var script = source is null ? null : Path.Combine(source, PluginName, "install.py");
        if (script is null || !File.Exists(script)) { throw new FileNotFoundException("The bundled Thermalright VibeWare Codex plugin is missing."); }
        var configured = Environment.GetEnvironmentVariable("THERMALRIGHT_VIBEWARE_PYTHON");
        var bundled = Path.Combine(AppContext.BaseDirectory, "python", "python.exe");
        var executable = !string.IsNullOrWhiteSpace(configured) ? configured : File.Exists(bundled) ? bundled : "python";
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(script)!
        };
        process.StartInfo.ArgumentList.Add("-B");
        process.StartInfo.ArgumentList.Add(script);
        process.StartInfo.ArgumentList.Add("prepare");
        if (!process.Start()) { throw new IOException("The local Python plugin preparer could not start."); }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var output = ReadBoundedAsync(process.StandardOutput, deadline.Token);
            var errors = ReadBoundedAsync(process.StandardError, deadline.Token);
            await Task.WhenAll(process.WaitForExitAsync(deadline.Token), output, errors);
            if (process.ExitCode != 0) { throw new IOException("Could not prepare the local Codex plugin. Its files were not enabled or trusted in Codex."); }
        }
        catch
        {
            try { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
            { /* Keep the original preparation failure. */ }
            throw;
        }
        return Status();
    }

    /// <summary>Drains child output within the configured character limit.</summary>
    /// <param name="reader">Child-process text stream owned by the caller.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    private static async Task ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[512];
        var total = 0;
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0) { return; }
            total += count;
            if (total > 4096) { throw new IOException("Codex plugin preparer output exceeded 4 KiB."); }
        }
    }

    /// <summary>Reads a local JSON file only when it fits the configured byte limit.</summary>
    /// <param name="path">Local settings, manifest or activity JSON file path used by this operation.</param>
    /// <param name="limit">Maximum permitted response/file length in bytes.</param>
    private static JsonDocument? ReadSmallJson(string path, long limit)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > limit) { return null; }
        using var stream = file.OpenRead();
        return JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 16 });
    }

    /// <summary>Reads a string property with the caller's fallback policy.</summary>
    /// <param name="element">JSON object whose property is read or validated.</param>
    /// <param name="key">Dictionary key or JSON property name to look up.</param>
    private static string Text(JsonElement element, string key) =>
        element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static readonly HashSet<string> KnownEvents = [
        "SessionStart", "SessionEnd", "UserPromptSubmit", "PreToolUse", "PermissionRequest", "PostToolUse",
        "PreCompact", "PostCompact", "SubagentStart", "SubagentStop", "Stop", "Interrupt"
    ];
}
