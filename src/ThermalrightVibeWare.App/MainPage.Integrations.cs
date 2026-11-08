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


using System.Windows;
using System.Windows.Controls;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Studio;

namespace ThermalrightVibeWare.App;

/// <summary>Integrations responsibilities of the studio page.</summary>
public sealed partial class MainPage
{

    private (StudioOperation<CodexSetupStatus> Status, StudioOperation<bool> Preparation)? appliedCodex;

    /// <summary>Refreshes local Codex setup status when the model permits another check.</summary>
    /// <param name="force">Whether an explicit refresh may bypass the automatic polling interval.</param>
    private async Task RefreshCodexStatusAsync(bool force = false)
    {
        var pending = viewModel.ReadCodexAsync(force);
        ApplyCodexState();
        if (await pending) { ApplyCodexState(); }
    }

    /// <summary>Applies the current status and preparation operation to the Codex controls.</summary>
    private void ApplyCodexState()
    {
        if (unloaded) { return; }

        var auxiliary = viewModel.Auxiliary;
        var status = auxiliary.Codex;
        var preparation = auxiliary.CodexPreparation;
        if (appliedCodex is { } applied && ReferenceEquals(status, applied.Status) && ReferenceEquals(preparation, applied.Preparation)) { return; }
        appliedCodex = (status, preparation);

        var busy = status.Busy || preparation.Busy;
        CodexRefreshButton.IsEnabled = !busy && session.Supports("codex.status");
        CodexPrepareButton.IsEnabled = !busy && session.Supports("codex.prepare");

        if (status.Value is { } current)
        {
            CodexSummaryText.Text = current.ActivitySeen ? "Activity received" : current.Prepared && current.Listed
                ? "Plugin ready · waiting for activity" : "Plugin setup needed";
            CodexEventText.Text = current.LastEvent is { } last ? $"Last event: {last}\n{current.LastEventUtc}" : "No events received yet.";
            CodexHelpText.Text = current.Instructions;
            CodexPrepareButton.Content = current.Prepared && current.Listed ? "Prepare plugin again" : "Prepare Codex plugin";
            ToolTipService.SetToolTip(CodexEventText, CodexEventText.Text);
            ToolTipService.SetToolTip(CodexPrepareButton, current.Instructions);
            ToolTipService.SetToolTip(CodexSetupFrame, current.Instructions);
        }

        var error = preparation.Error ?? status.Error;
        if (preparation.Busy) { CodexSummaryText.Text = "Preparing the local plugin…"; }
        else if (error is not null) { CodexSummaryText.Text = preparation.Error is null ? "Connection check unavailable" : "Plugin setup failed"; }
        CodexStatusText.Text = preparation.Busy ? "Preparing the local plugin…" : error ?? status.Value?.Status ?? "Checking the local plugin…";
        ToolTipService.SetToolTip(CodexStatusText, CodexStatusText.Text);
    }

    /// <summary>Prepares the local Codex plugin after the user's button click.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void CodexPrepare_Click(object sender, RoutedEventArgs args)
    {
        var pending = viewModel.PrepareCodexAsync();
        ApplyCodexState();
        await pending;
        ApplyCodexState();
    }

    /// <summary>Forces a refresh of Codex setup status.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void CodexRefresh_Click(object sender, RoutedEventArgs args) => await RefreshCodexStatusAsync(force: true);

    /// <summary>Loads the current diagnostic logging choice from the engine.</summary>
    private Task LoadDiagnosticsAsync() => UpdateDiagnosticsAsync();

    /// <summary>Loads or updates diagnostic logging and applies the latest operation state.</summary>
    /// <param name="enabled">New choice, or null to read the current one.</param>
    private async Task UpdateDiagnosticsAsync(bool? enabled = null)
    {
        var pending = viewModel.ReadDiagnosticsAsync(enabled);
        ApplyDiagnosticsState();
        await pending;
        ApplyDiagnosticsState();
    }

    /// <summary>Projects diagnostic logging and open-log availability into the controls.</summary>
    private void ApplyDiagnosticsState()
    {
        if (unloaded) { return; }

        var auxiliary = viewModel.Auxiliary;
        var diagnostics = auxiliary.Diagnostics;
        var enabled = diagnostics.Value?.Enabled == true;
        if (!diagnostics.Busy) { LogErrorsBox.IsChecked = enabled; }
        LogErrorsBox.IsEnabled = !diagnostics.Busy && session.Supports("diagnostics.set");
        OpenLogButton.IsEnabled = enabled && !diagnostics.Busy && !auxiliary.OpenDiagnostics.Busy && session.Supports("diagnostics.open");
        ToolTipService.SetToolTip(OpenLogButton, auxiliary.OpenDiagnostics.Error ?? diagnostics.Value?.LogPath);
    }

    /// <summary>Sends an explicit logging choice to the model, ignoring updates from the current snapshot.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void Diagnostics_Changed(object sender, RoutedEventArgs args)
    {
        var diagnostics = viewModel.Auxiliary.Diagnostics;
        var enabled = LogErrorsBox.IsChecked == true;
        if (!ready || diagnostics.Busy || enabled == (diagnostics.Value?.Enabled == true)) { return; }
        await UpdateDiagnosticsAsync(enabled);
    }

    /// <summary>Opens the local diagnostic log on request.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void OpenLog_Click(object sender, RoutedEventArgs args)
    {
        var pending = viewModel.OpenDiagnosticsAsync();
        ApplyDiagnosticsState();
        await pending;
        ApplyDiagnosticsState();
    }
}
