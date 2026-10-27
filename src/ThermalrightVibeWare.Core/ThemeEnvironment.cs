/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: ThemeEnvironment.cs
 * Purpose: Caches desktop palette and bounded local Codex activity metadata.
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

using System.Drawing;
using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;

namespace ThermalrightVibeWare.Core;

/// <summary>Small optional theme inputs, sampled locally without starting an extra provider.</summary>
internal sealed class ThemeEnvironment
{
    private string[] palette = ["#523F79", "#285A6D", "#6F3D68", "#2B324C"];
    private string? wallpaperKey;
    private long nextWallpaperRead;
    private readonly Queue<(string Key, object Activity)> history = new();

    /// <summary>Returns a cached wallpaper palette for theme rendering.</summary>
    public IReadOnlyList<string> DesktopPalette()
    {
        if (Environment.TickCount64 < nextWallpaperRead) { return palette; }
        nextWallpaperRead = Environment.TickCount64 + 30_000;
        try
        {
            using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var path = desktop?.GetValue("WallPaper") as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return palette; }
            var file = new FileInfo(path);
            var key = path + "|" + file.LastWriteTimeUtc.Ticks;
            if (key == wallpaperKey || file.Length > 30 * 1024 * 1024) { return palette; }
            using var stream = File.OpenRead(path);
            using var source = RasterImage.Open(stream);
            using var tiny = new Bitmap(source, 64, 64);
            var result = new List<string>();
            foreach (var point in new[] { new Point(8, 8), new Point(40, 8), new Point(8, 40), new Point(40, 40) })
            {
                var red = 0; var green = 0; var blue = 0;
                for (var y = point.Y; y < point.Y + 16; y++)
                {
                    for (var x = point.X; x < point.X + 16; x++)
                    { var pixel = tiny.GetPixel(x, y); red += pixel.R; green += pixel.G; blue += pixel.B; }
                }
                result.Add($"#{red / 256:X2}{green / 256:X2}{blue / 256:X2}");
            }
            palette = result.ToArray();
            wallpaperKey = key;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException) { }
        return palette;
    }

    /// <summary>Reads bounded local Codex activity metadata.</summary>
    public object CodexActivity()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThermalrightVibeWare", "codex", "current.json");
        try
        {
            if (!File.Exists(path)) { return Waiting(); }
            if (new FileInfo(path).Length > 64 * 1024) { return Waiting("Activity file exceeds the size limit"); }
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(file, new JsonDocumentOptions { MaxDepth = 12 });
            var activity = document.RootElement;
            if (activity.ValueKind != JsonValueKind.Object || Text(activity, "kind") != "codex.activity"
                || !activity.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var number) || number != 1)
            { return Waiting("Activity format unavailable"); }
            var timestamp = Text(activity, "atUtc");
            var parsed = DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at);
            var age = DateTimeOffset.UtcNow - at;
            var stale = !parsed || age > TimeSpan.FromMinutes(5) || age < TimeSpan.FromMinutes(-1);
            var live = activity.TryGetProperty("isLive", out var isLive) && isLive.ValueKind == JsonValueKind.True;
            var eventName = Text(activity, "event");
            var eventStatus = Text(activity, "status");
            var tool = Text(activity, "toolKind");
            var session = Text(activity, "sessionKey");
            var stamp = activity.TryGetProperty("atUnixNs", out var unixStamp) && unixStamp.ValueKind == JsonValueKind.Number ? unixStamp.GetRawText() : timestamp;
            var identity = $"{stamp}|{session}|{eventName}";
            if (parsed && !string.IsNullOrEmpty(eventName) && !history.Any(entry => entry.Key == identity))
            {
                history.Enqueue((identity, new { @event = eventName, status = eventStatus, atUtc = timestamp, tool }));
                while (history.Count > 12) { history.Dequeue(); }
            }
            return new
            {
                status = stale ? "Last activity" : eventStatus ?? "Waiting for Codex",
                @event = eventName,
                phase = stale ? "idle" : Text(activity, "phase"),
                atUtc = timestamp,
                isLive = live && !stale,
                stale,
                chatTitle = Text(activity, "chatName"),
                tool,
                session,
                model = (string?)null,
                events = history.Select(entry => entry.Activity).ToArray()
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { return Waiting("Activity unavailable"); }
    }

    /// <summary>Creates an unavailable Codex activity observation with a reason.</summary>
    /// <param name="detail">Optional explanation attached to an unavailable activity observation.</param>
    private object Waiting(string? detail = null) => new
    {
        status = "Waiting for Codex",
        phase = "idle",
        isLive = false,
        stale = false,
        @event = (string?)null,
        atUtc = (string?)null,
        chatTitle = (string?)null,
        tool = (string?)null,
        session = (string?)null,
        model = (string?)null,
        detail,
        events = history.Select(entry => entry.Activity).ToArray()
    };

    /// <summary>Reads a string property with the caller's fallback policy.</summary>
    /// <param name="element">JSON object whose property is read or validated.</param>
    /// <param name="name">JSON string property to read.</param>
    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
