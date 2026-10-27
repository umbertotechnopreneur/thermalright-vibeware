/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: ThemeCatalog.cs
 * Purpose: Discovers local theme manifests and reports invalid pack entries.
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

using System.Text.Json;

namespace ThermalrightVibeWare.Core;

public sealed record ThemeDefinition(string Id, string Name, string Category, string Description, string PackId, string DirectoryPath, bool SupportsSpectrum = false, int PageCount = 1, int PageSeconds = 8);
public sealed record ThemeCatalogSnapshot(IReadOnlyList<ThemeDefinition> Themes, IReadOnlyList<string> Notices);

/// <summary>Finds bounded, manifest-backed theme folders; never imports or executes scripts while listing.</summary>
public static class ThemeCatalog
{
    public static string UserThemesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThermalrightVibeWare", "themes");

    /// <summary>Finds a bundled content directory relative to the application layout.</summary>
    /// <param name="name">Bundled content folder name, such as themes or theme-runtime.</param>
    public static string? FindContentDirectory(string name) => FindContentDirectory(name, AppContext.BaseDirectory);

    /// <summary>Finds a bundled content directory relative to the application layout.</summary>
    /// <param name="name">Bundled content folder name, such as themes or theme-runtime.</param>
    /// <param name="baseDirectory">Base directory from which bundled content locations are resolved.</param>
    internal static string? FindContentDirectory(string name, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || Path.GetFileName(name) != name || Path.IsPathRooted(name))
        { throw new ArgumentException("Content must name one folder beside the engine.", nameof(name)); }
        var candidate = Path.Combine(Path.GetFullPath(baseDirectory), name);
        return Directory.Exists(candidate) && (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) == 0 ? candidate : null;
    }

    /// <summary>Discovers valid local theme pack manifests and records rejected entries.</summary>
    public static ThemeCatalogSnapshot Read()
    {
        var notices = new List<string>();
        var themes = new Dictionary<string, ThemeDefinition>(StringComparer.OrdinalIgnoreCase);
        var bundled = FindContentDirectory("themes");
        var roots = new[] { bundled, UserThemesPath }.Where(path => path is not null).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) { continue; }
            try
            {
                foreach (var pack in Directory.EnumerateDirectories(root!).Order(StringComparer.OrdinalIgnoreCase).Take(64))
                {
                    if ((File.GetAttributes(pack) & FileAttributes.ReparsePoint) != 0) { continue; }
                    var themeRoot = Path.Combine(pack, "themes");
                    if (!Directory.Exists(themeRoot)) { continue; }
                    var packId = Path.GetFileName(pack);
                    var packManifest = Path.Combine(pack, "pack.json");
                    if (File.Exists(packManifest))
                    {
                        try
                        {
                            using var json = ReadManifest(packManifest);
                            ValidateSchema(json.RootElement);
                            packId = Text(json.RootElement, "id", packId);
                            if (string.IsNullOrWhiteSpace(packId) || packId.Length > 100) { throw new ArgumentException("Pack ID must contain 1–100 characters."); }
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
                        { notices.Add($"Pack {packId}: {error.Message}"); continue; }
                    }
                    foreach (var folder in Directory.EnumerateDirectories(themeRoot).Order(StringComparer.OrdinalIgnoreCase).Take(256))
                    {
                        if (themes.Count >= 256) { notices.Add("Theme limit reached (256). Remaining folders were skipped."); return Result(); }
                        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) { continue; }
                        var manifest = Path.Combine(folder, "theme.json");
                        if (!File.Exists(manifest)) { continue; }
                        try
                        {
                            using var json = ReadManifest(manifest);
                            var entry = json.RootElement;
                            ValidateSchema(entry);
                            var id = Text(entry, "id");
                            var name = Text(entry, "name");
                            var category = Text(entry, "category");
                            var description = Text(entry, "description");
                            if (id.Length is 0 or > 100 || !id.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'))
                            { throw new ArgumentException("Theme ID must contain 1–100 letters, digits, dots, hyphens or underscores."); }
                            if (category is not ("multimedia" or "codex" or "system")) { throw new ArgumentException("Category must be multimedia, codex or system."); }
                            if (string.IsNullOrWhiteSpace(name) || name.Length > 160) { throw new ArgumentException("Theme name must contain 1–160 characters."); }
                            if (description.Length > 1200) { throw new ArgumentException("Theme description exceeds 1200 characters."); }
                            var script = Text(entry, "entry", "render.py");
                            if (Path.GetFileName(script) != script || !script.EndsWith(".py", StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(folder, script)))
                            { throw new ArgumentException("Entry must name an existing Python file in the theme folder."); }
                            var supportsSpectrum = entry.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Object
                                && settings.TryGetProperty("showSpectrum", out var spectrum) && spectrum.ValueKind == JsonValueKind.Object
                                && Text(spectrum, "type") == "boolean";
                            var pageCount = 1;
                            if (entry.TryGetProperty("pages", out var pages))
                            {
                                if (pages.ValueKind != JsonValueKind.Array) { throw new ArgumentException("Pages must be an array."); }
                                pageCount = pages.GetArrayLength();
                                var pageIds = new HashSet<string>(StringComparer.Ordinal);
                                foreach (var page in pages.EnumerateArray())
                                {
                                    if (page.ValueKind != JsonValueKind.Object || Text(page, "id") is not { Length: > 0 and <= 40 } pageId || !pageIds.Add(pageId))
                                    { throw new ArgumentException("Page IDs must be unique nonempty strings up to 40 characters."); }
                                    if (page.TryGetProperty("title", out var title) && (title.ValueKind != JsonValueKind.String || title.GetString()!.Length > 80))
                                    { throw new ArgumentException("Page titles must be strings up to 80 characters."); }
                                }
                            }
                            var pageSeconds = 8;
                            if (entry.TryGetProperty("pageSeconds", out var seconds) && (seconds.ValueKind != JsonValueKind.Number || !seconds.TryGetInt32(out pageSeconds)))
                            { throw new ArgumentException("Page interval must be an integer."); }
                            if (pageCount is < 1 or > 8 || pageSeconds is < 4 or > 120) { throw new ArgumentException("Invalid theme page count or interval."); }
                            if (!themes.TryAdd(id, new(id, name, category, description, packId, folder, supportsSpectrum, pageCount, pageSeconds)))
                            { notices.Add($"Duplicate theme ID {id} was skipped; the first installed definition wins."); }
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
                        { notices.Add($"Theme {Path.GetFileName(folder)}: {error.Message}"); }
                    }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { notices.Add($"Cannot read theme folder: {error.Message}"); }
        }
        if (themes.Count == 0) { notices.Add("No themes found. Add a pack under themes/<pack>/themes/<theme>/theme.json."); }
        return Result();

        // Return the same stable catalog ordering for both empty and populated discovery paths.
        ThemeCatalogSnapshot Result() => new(themes.Values.OrderBy(theme => theme.Category).ThenBy(theme => theme.Name, StringComparer.OrdinalIgnoreCase).ToArray(), notices);
    }

    /// <summary>Reads a bounded theme manifest JSON document.</summary>
    /// <param name="path">Local settings, manifest or activity JSON file path used by this operation.</param>
    private static JsonDocument ReadManifest(string path)
    {
        if (new FileInfo(path).Length > 64 * 1024) { throw new ArgumentException("Manifest exceeds 64 KiB."); }
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 16 });
    }

    /// <summary>Reads a string property with the caller's fallback policy.</summary>
    /// <param name="element">JSON object whose property is read or validated.</param>
    /// <param name="property">JSON property name to read.</param>
    /// <param name="fallback">Value to use when the optional input is absent.</param>
    private static string Text(JsonElement element, string property, string fallback = "") =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    /// <summary>Rejects a manifest whose schema version is unsupported.</summary>
    /// <param name="element">JSON object whose property is read or validated.</param>
    private static void ValidateSchema(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("schema", out var schema)
            || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1)
        { throw new ArgumentException("Manifest requires schema: 1."); }
    }
}
