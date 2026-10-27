/* VBWR B
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * File: FirstRunChecks.cs
 * Purpose: Defines offline first-launch persistence checks; execution needs owner approval.
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 * Created with AI: OpenAI Codex; first-launch persistence checks; 2026-10-27.
 * Human guidance: Umberto Giacobbi; show the first launch, then start in the tray.
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 * VBWR E */

using System.Text.Json;
using ThermalrightVibeWare.Studio;

internal static class FirstRunChecks
{
    /// <summary>Defines isolated first-launch defaults, durable completion and failed-save checks.</summary>
    /// <param name="output">Synthetic output directory owned by the approved offline check run.</param>
    /// <param name="check">Existing check recorder; receives a condition and its requirement description.</param>
    public static void Run(string output, Action<bool, string> check)
    {
        var folder = Path.Combine(output, "first-run");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "settings.json");
        var model = new StudioModel(path);
        check(model.Preferences.IsFirstRun, "New settings show the first studio launch.");
        model.Edit(p => p.FontFamily = "First-run fixture");
        model.Save();
        check(new StudioModel(path).Preferences.IsFirstRun, "Saving other preferences does not consume the first launch.");

        model.CompleteFirstRun();
        using (var json = JsonDocument.Parse(File.ReadAllText(path)))
        {
            check(!json.RootElement.GetProperty("isFirstRun").GetBoolean(), "Completed first launch is persisted as a boolean false.");
        }
        var reopened = new StudioModel(path);
        check(!reopened.Preferences.IsFirstRun, "A subsequent launch reads the tray-start state.");
        check(reopened.Preferences.FontFamily == "First-run fixture", "First-launch completion preserves the other choices.");
        var revision = model.Read().Revision;
        model.CompleteFirstRun();
        check(model.Read().Revision == revision, "Repeated page loads do not consume the first launch twice.");

        var missingField = Path.Combine(folder, "missing-field.json");
        File.WriteAllText(missingField, "{\"schemaVersion\":1}");
        check(new StudioModel(missingField).Preferences.IsFirstRun, "A supported file without the flag shows the first launch once.");

        var invalid = Path.Combine(folder, "invalid.json");
        const string invalidJson = "{\"schemaVersion\":1,\"isFirstRun\":\"false\"}";
        File.WriteAllText(invalid, invalidJson);
        var damaged = new StudioModel(invalid);
        check(damaged.Preferences.IsFirstRun && damaged.PersistenceNotice is not null, "Malformed flags keep the studio visible with a settings notice.");
        try { damaged.CompleteFirstRun(); check(false, "Damaged settings must remain preserved."); }
        catch (IOException) { check(damaged.Preferences.IsFirstRun && File.ReadAllText(invalid) == invalidJson, "Failed completion preserves damaged settings and first-launch visibility."); }

        var blockedRoot = Path.Combine(folder, "parent-is-a-file");
        File.WriteAllText(blockedRoot, "Synthetic file blocks directory creation.");
        var blocked = new StudioModel(Path.Combine(blockedRoot, "settings.json"));
        try { blocked.CompleteFirstRun(); check(false, "First-launch completion requires a successful save."); }
        catch (IOException) { check(blocked.Preferences.IsFirstRun, "An unavailable settings destination does not consume the first launch."); }
    }
}
