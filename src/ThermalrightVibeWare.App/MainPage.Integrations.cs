/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Integrations.cs
 * Purpose: Shows local Codex setup and diagnostic logging controls.
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


using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.App;

/// <summary>Integrations responsibilities of the studio page.</summary>
public sealed partial class MainPage
{

    /// <summary>Refreshes local Codex setup status with a bounded polling interval.</summary>
    /// <param name="force">Whether to bypass the cached Codex status polling interval.</param>
    private async Task RefreshCodexStatusAsync(bool force = false)
    {
        if (codexLoading || codexPreparing || !force && Environment.TickCount64 - codexStatusAt < 5000) { return; }
        codexLoading = true;
        CodexRefreshButton.IsEnabled = false;
        try
        {
            var status = await viewModel.ReadCodexAsync();
            if (unloaded) { return; }
            ShowCodexStatus(status);
            codexStatusAt = Environment.TickCount64;
        }
        catch (Exception error) { CodexStatusText.Text = error.Message; CodexSummaryText.Text = "Connection check unavailable"; }
        finally { codexLoading = false; CodexRefreshButton.IsEnabled = !codexPreparing; }
    }

    /// <summary>Applies local Codex setup and event details to the studio.</summary>
    /// <param name="status">Latest local plugin preparation, listing and activity observation.</param>
    private void ShowCodexStatus(CodexSetupStatus status)
    {
        CodexStatusText.Text = status.Status;
        CodexSummaryText.Text = status.ActivitySeen ? "Activity received" : status.Prepared && status.Listed ? "Plugin ready · waiting for activity" : "Plugin setup needed";
        CodexEventText.Text = status.LastEvent is { } last ? $"Last event: {last}\n{status.LastEventUtc}" : "No events received yet.";
        CodexHelpText.Text = status.Instructions;
        ToolTipService.SetToolTip(CodexStatusText, status.Status);
        ToolTipService.SetToolTip(CodexEventText, CodexEventText.Text);
        ToolTipService.SetToolTip(CodexPrepareButton, status.Instructions);
        ToolTipService.SetToolTip(CodexSetupFrame, status.Instructions);
        CodexPrepareButton.Content = status.Prepared && status.Listed ? "Plugin prepared" : "Prepare Codex plugin";
        CodexPrepareButton.IsEnabled = !codexPreparing && !(status.Prepared && status.Listed);
    }

    /// <summary>Prepares the local Codex plugin after the user's button click.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void CodexPrepare_Click(object sender, RoutedEventArgs args)
    {
        if (codexPreparing) { return; }
        string? preparationError = null;
        codexPreparing = true;
        CodexPrepareButton.IsEnabled = CodexRefreshButton.IsEnabled = false;
        CodexStatusText.Text = "Preparing the local plugin…";
        try
        {
            var status = await viewModel.ReadCodexAsync(prepare: true);
            if (!unloaded) { ShowCodexStatus(status); codexStatusAt = Environment.TickCount64; }
        }
        catch (Exception error) { preparationError = error.Message; }
        finally
        {
            codexPreparing = false;
            CodexRefreshButton.IsEnabled = true;
            await RefreshCodexStatusAsync(force: true);
            if (preparationError is not null) { CodexStatusText.Text = preparationError; }
        }
    }

    /// <summary>Forces a refresh of Codex setup status.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void CodexRefresh_Click(object sender, RoutedEventArgs args) => await RefreshCodexStatusAsync(force: true);

    /// <summary>Loads the saved diagnostic logging choice.</summary>
    private async Task LoadDiagnosticsAsync()
    {
        diagnosticsChanging = true;
        try
        {
            var status = await viewModel.ReadDiagnosticsAsync();
            diagnosticsEnabled = status.Enabled;
            LogErrorsBox.IsChecked = status.Enabled;
            OpenLogButton.IsEnabled = status.Enabled;
            ToolTipService.SetToolTip(OpenLogButton, status.LogPath);
        }
        catch (Exception error) { StatusText.Text = error.Message; }
        finally { diagnosticsChanging = false; }
    }

    /// <summary>Saves the user's diagnostic logging choice.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void Diagnostics_Changed(object sender, RoutedEventArgs args)
    {
        if (!ready || diagnosticsChanging) { return; }
        diagnosticsChanging = true;
        LogErrorsBox.IsEnabled = false;
        try
        {
            var status = await viewModel.ReadDiagnosticsAsync(LogErrorsBox.IsChecked == true);
            diagnosticsEnabled = status.Enabled;
            LogErrorsBox.IsChecked = status.Enabled;
            OpenLogButton.IsEnabled = status.Enabled;
            ToolTipService.SetToolTip(OpenLogButton, status.LogPath);
        }
        catch (Exception error) { LogErrorsBox.IsChecked = diagnosticsEnabled; StatusText.Text = error.Message; }
        finally { diagnosticsChanging = false; LogErrorsBox.IsEnabled = true; }
    }

    /// <summary>Opens the local diagnostic log on request.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void OpenLog_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            var result = await viewModel.OpenDiagnosticsAsync();
            if (!result.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True)
            { StatusText.Text = "No log file yet. Leave Log errors on, then try again."; }
        }
        catch (Exception error) { StatusText.Text = error.Message; }
    }
}
