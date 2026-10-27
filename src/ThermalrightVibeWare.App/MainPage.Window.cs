/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: MainPage.Window.cs
 * Purpose: Handles About, close choices, tray minimization and administrator restart.
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

namespace ThermalrightVibeWare.App;

/// <summary>Window responsibilities of the studio page.</summary>
public sealed partial class MainPage
{

    /// <summary>Opens the product information overlay.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void About_Click(object sender, RoutedEventArgs args) => ShowAbout();

    /// <summary>Open About from the page or the native notification-area menu.</summary>
    public void ShowAbout()
    {
        CloseOverlay.Visibility = Visibility.Collapsed;
        AboutOverlay.Visibility = Visibility.Visible;
        CloseAboutButton.Focus();
    }

    /// <summary>Closes the product information overlay.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void CloseAbout_Click(object sender, RoutedEventArgs args) => CloseAbout();

    /// <summary>Offer an explicit exit or background choice for the native close button.</summary>
    public void ShowCloseChoice()
    {
        if (unloaded) { return; }
        AboutOverlay.Visibility = Visibility.Collapsed;
        MinimizeTrayButton.IsEnabled = windowActions?.CanMinimizeToTray == true;
        CloseOverlay.Visibility = Visibility.Visible;
        CancelCloseButton.Focus();
    }

    /// <summary>Dismisses the close choices and keeps the studio open.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void CancelClose_Click(object sender, RoutedEventArgs args) => CloseOverlay.Visibility = Visibility.Collapsed;

    /// <summary>Requests application shutdown through the desktop host.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void Exit_Click(object sender, RoutedEventArgs args) => ExitApplication();

    /// <summary>Requests a guarded administrator restart and reports failure.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private async void Administrator_Click(object sender, RoutedEventArgs args)
    {
        if (restartingAsAdministrator || windowActions?.CanRestartAsAdministrator != true) { return; }
        restartingAsAdministrator = true;
        AdministratorButton.IsEnabled = SensorDebugAdministrator.IsEnabled = false;
        // Keep the existing session alive until Windows accepts the elevated launch.
        try
        {
            colorChanges.Stop();
            if (pendingColorSettings) { CommitColorControls(); }
            SaveChoices();
            if (await windowActions.RestartAsAdministratorAsync()) { ExitApplication(); }
            else { StatusText.Text = "Administrator restart canceled. The current session is still running."; }
        }
        catch (Exception error) { StatusText.Text = "Could not reopen as administrator: " + error.Message; }
        finally { restartingAsAdministrator = false; AdministratorButton.IsEnabled = SensorDebugAdministrator.IsEnabled = true; }
    }

    /// <summary>Flush pending choices before asking the desktop host to stop.</summary>
    public void ExitApplication()
    {
        colorChanges.Stop();
        if (pendingColorSettings) { CommitColorControls(); }
        SaveChoices();
        windowActions?.CloseApplication();
    }

    /// <summary>Saves choices and minimizes the studio to its tray icon.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="args">Event data supplied by the UI or capture framework.</param>
    private void MinimizeTray_Click(object sender, RoutedEventArgs args)
    {
        if (windowActions?.CanMinimizeToTray != true) { return; }
        try
        {
            colorChanges.Stop();
            if (pendingColorSettings) { CommitColorControls(); }
            SaveChoices();
            CloseOverlay.Visibility = Visibility.Collapsed;
            windowActions.MinimizeToTray();
        }
        catch (Exception error) { StatusText.Text = "Could not minimize to tray: " + error.Message; }
    }

    /// <summary>Hides the product overlay and restores keyboard focus.</summary>
    private void CloseAbout() { AboutOverlay.Visibility = Visibility.Collapsed; AboutLink.Focus(); }
}
