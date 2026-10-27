/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: App.xaml.cs
 * Purpose: Creates the OpenSilver application and routes desktop window requests.
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

using System.Windows;
using ThermalrightVibeWare.Contracts;
using ThermalrightVibeWare.Studio;

namespace ThermalrightVibeWare.App;

public sealed partial class App : Application
{
    private readonly MainPage page;

    /// <summary>Creates the OpenSilver application around the supplied studio session and host services.</summary>
    /// <param name="client">Engine request client owned or shared according to the containing session/host.</param>
    /// <param name="windowActions">Optional desktop actions supplied by the host.</param>
    /// <param name="presentationSettingsPath">Optional settings file path; null selects the per-user default.</param>
    /// <param name="preview">Optional host bridge that displays the engine's existing JPEG.</param>
    /// <param name="restoreLastSession">Whether to restore the saved device/theme and wait for that device at startup.</param>
    /// <param name="session">Optional existing studio session; the page owns a newly created session only.</param>
    public App(IPanelClient client, IStudioWindowActions? windowActions = null, string? presentationSettingsPath = null, IStudioPreview? preview = null, bool restoreLastSession = false, StudioSession? session = null)
    {
        InitializeComponent();
        page = new MainPage(client, windowActions, presentationSettingsPath, preview, restoreLastSession, session);
        RootVisual = page;
    }

    /// <summary>Marshal a native title-bar close request onto the presentation dispatcher.</summary>
    public void RequestClose() => page.Dispatcher.BeginInvoke(page.ShowCloseChoice);

    /// <summary>Dispatches the host About request onto the page's UI thread.</summary>
    public void RequestAbout() => page.Dispatcher.BeginInvoke(page.ShowAbout);

    /// <summary>Dispatches the host exit request onto the page's UI thread.</summary>
    public void RequestExit() => page.Dispatcher.BeginInvoke(page.ExitApplication);
}
