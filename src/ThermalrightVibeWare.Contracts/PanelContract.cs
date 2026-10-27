/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: PanelContract.cs
 * Purpose: Defines versioned stdio messages and host/UI contracts independent of USB code.
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

namespace ThermalrightVibeWare.Contracts;

/// <summary>Shows the engine's existing JPEG in the configuration UI; never renders or sends USB frames.</summary>
public interface IStudioPreview
{
    bool IsVisible => true;

    /// <summary>Displays the engine's encoded JPEG and reports its pixel dimensions.</summary>
    /// <param name="jpeg">Encoded JPEG bytes, reused for preview and physical output.</param>
    /// <param name="transition">Whether to cross-fade from the previously displayed frame.</param>
    Task<PreviewSize> ShowAsync(byte[] jpeg, bool transition);

    /// <summary>Clears the currently held preview or session resources.</summary>
    void Clear();
}

public sealed record PreviewSize(int Width, int Height);

/// <summary>JSON Lines v1. Stdout contains responses only; diagnostics belong on stderr.</summary>
public static class PanelContract
{
    public const int Version = 1;
    public const int MaximumRequestCharacters = 16_384;
    public const int MaximumResponseCharacters = 2_000_000;
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
}

public sealed record EngineRequest(string Id, string Method, JsonElement? Parameters = null, int Version = PanelContract.Version);
public sealed record EngineResponse(string Id, bool Ok, JsonElement? Result = null, string? Error = null, int Version = PanelContract.Version);
public sealed record PanelChoice(string Id, string Name);
public sealed record ThemeChoiceDto(string Id, string Name, string Category, string Description, string PackId, bool SupportsSpectrum = false, int PageCount = 1, int PageSeconds = 8);
public sealed record ThemeCatalogDto(IReadOnlyList<PanelChoice> Categories, IReadOnlyList<ThemeChoiceDto> Themes, IReadOnlyList<string> Notices);
public sealed record FontCatalogDto(IReadOnlyList<string> Families, string? Notice = null);
public sealed record ScreenSelectionDto(string Category, string ThemeId, bool Enabled = true);
public sealed record PreviewOptions(string ProfileId = "wide-1280x480", bool IncludeFrame = true, bool Light = false, bool AmbientDrift = false, bool IncludeDevices = false,
    string? ThemeId = null, string FontFamily = "Cascadia Mono", double FontScale = 1, int RefreshIntervalMilliseconds = 1000,
    string JpegQuality = "high", double SaturationPercent = 100, double HueDegrees = 0, bool ShowSpectrum = true,
    double BrightnessPercent = 0, double ContrastPercent = 100, bool ColorFiltersEnabled = true,
    IReadOnlyList<ScreenSelectionDto>? Screens = null, IReadOnlyDictionary<string, ThemePalette>? Palettes = null,
    int PageIndex = 0, long PageRevision = 0);
public sealed record StartOptions(string DeviceId, string ProfileId = "auto", string? ThemeId = null);
public sealed record DisplayStartResult(bool Accepted, string? ProfileId = null);
public sealed record MediaOptions(string Action);
public sealed record CodexSetupStatus(bool Prepared, bool Listed, bool ActivitySeen, string Status, string? LastEvent, string? LastEventUtc, string Instructions);
public sealed record DiagnosticsStatus(bool Enabled, string LogPath);
public sealed record SetDiagnosticsOptions(bool Enabled);
public sealed record AudioLevelDto(string Id, string Name, double VolumePercent, bool Muted, double? Peak);
public sealed record SensorDriverDto(bool Available, string? Path, string? Notice, bool FileExists);
public sealed record SensorReadingDto(string Id, string Name, string Type, string Unit,
    double? Value, double? Minimum, double? Maximum, string? Notice = null, IReadOnlyList<SensorParameterDto>? Parameters = null);
public sealed record SensorParameterDto(string Id, string Name, string Description, double? Value, double? DefaultValue);
public sealed record HardwareDeviceDto(string Id, string? ParentId, string Name, string Type,
    IReadOnlyList<SensorReadingDto> Sensors, string? Notice = null);
public sealed record SensorDebugDto(string LibraryVersion, DateTimeOffset SampledAtUtc,
    bool Administrator, IReadOnlyList<HardwareDeviceDto> Devices, IReadOnlyList<string> Notices);
public sealed record SystemDto(double? CpuUsage, double? CpuTemperature, double? GpuUsage, double? GpuTemperature, string GpuName, string CpuTemperatureSource,
    SensorDriverDto? Driver = null);
public sealed record NetworkDto(double? DownloadBytesPerSecond, double? UploadBytesPerSecond, long DownloadBytes, long UploadBytes, string Scope);
public sealed record ArtistSourcesDto(string BiographyUrl, string? PhotoUrl, string? SalesUrl);
public sealed record PanelState(string Title, string Artist, string Album, bool Playing, string Source,
    IReadOnlyList<AudioLevelDto> Audio, SystemDto System, NetworkDto? Network,
    IReadOnlyList<PanelChoice> Devices, IReadOnlyList<PanelChoice> Profiles,
    bool DisplayRunning, string Status, byte[]? FrameJpeg, IReadOnlyList<string> Notices,
    double? FrameMilliseconds = null, long SubmittedFrames = 0, string? AccentColor = null, int? NextFrameMilliseconds = null,
    string? ActiveThemeId = null, bool CodexProcessRunning = false, bool FrameSuppressed = false, ArtistSourcesDto? ArtistSources = null,
    string? ActivePageId = null, string? ActivePageTitle = null, int ActivePageIndex = 0, int ActivePageCount = 1);

/// <summary>The presentation depends on this contract, not the USB driver or operating system APIs.</summary>
public interface IPanelClient
{

    /// <summary>Serializes a named engine request and returns its typed response.</summary>
    /// <typeparam name="T">Expected response or payload type used by this operation.</typeparam>
    /// <param name="method">Allow-listed engine or worker method name.</param>
    /// <param name="parameters">Request payload serialized with the versioned contract; null when no payload is needed.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    Task<T> RequestAsync<T>(string method, object? parameters = null, CancellationToken cancellationToken = default);
}

/// <summary>Desktop window actions supplied by the host, separate from the USB engine.</summary>
public interface IStudioWindowActions
{
    bool CanMinimizeToTray { get; }
    bool CanRestartAsAdministrator => false;

    /// <summary>Requests a guarded restart with administrator rights.</summary>
    Task<bool> RestartAsAdministratorAsync() => Task.FromResult(false);

    /// <summary>Delegates studio minimization to the desktop host.</summary>
    void MinimizeToTray();

    /// <summary>Delegates application shutdown to the desktop host.</summary>
    void CloseApplication();
}
