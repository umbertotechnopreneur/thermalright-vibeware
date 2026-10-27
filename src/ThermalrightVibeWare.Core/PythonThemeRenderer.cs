/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: PythonThemeRenderer.cs
 * Purpose: Owns the persistent Python/Skia worker and validates its versioned JPEG replies.
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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Core;

/// <summary>A persistent Python/Skia worker. It draws local previews and has no USB responsibilities.</summary>
public sealed class PythonThemeRenderer : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ThemeEnvironment environment = new();
    private Process? worker;
    private string? sentArtworkKey;
    private byte[]? artwork;
    private string? artworkKey;
    private string lastDiagnostic = "";
    private bool disposed;

    /// <summary>The latest successful frame's theme accent, derived from cached cover or wallpaper colors.</summary>
    public string? AccentColor { get; private set; }

    /// <summary>Track and page changes temporarily request frames faster than the user's normal refresh.</summary>
    public bool IsTransitioning { get; private set; }

    public string? ActivePageId { get; private set; }
    public string? ActivePageTitle { get; private set; }
    public int ActivePageIndex { get; private set; }
    public int ActivePageCount { get; private set; } = 1;

    /// <summary>Requests faster transition frames within the caller's refresh interval.</summary>
    /// <param name="requestedMilliseconds">User's requested refresh interval in milliseconds.</param>
    public int FrameIntervalMilliseconds(int requestedMilliseconds) => IsTransitioning ? Math.Min(requestedMilliseconds, 40) : requestedMilliseconds;

    /// <summary>Requests one bounded JPEG from the persistent Python/Skia worker.</summary>
    /// <param name="theme">Validated theme definition, including its local folder and category.</param>
    /// <param name="snapshot">Current dashboard observation, including availability notices.</param>
    /// <param name="profile">Validated target dimensions and pixel encoding.</param>
    /// <param name="light">Whether to use the light appearance.</param>
    /// <param name="ambientDrift">Whether the theme may animate its ambient background.</param>
    /// <param name="fontFamily">Printable font family name resolved by the Python renderer.</param>
    /// <param name="fontScale">Multiplier applied to the theme's text sizes.</param>
    /// <param name="jpegQuality">Requested JPEG preset: poor, low, medium or high.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    /// <param name="saturationPercent">Saturation from 0 to 200 percent; 100 is neutral.</param>
    /// <param name="hueDegrees">Hue rotation from -180 to 180 degrees; zero is neutral.</param>
    /// <param name="showSpectrum">Whether Multimedia themes may use Windows output loopback analysis.</param>
    /// <param name="brightnessPercent">Brightness offset from -100 to 100 percent; zero is neutral.</param>
    /// <param name="contrastPercent">Contrast from 0 to 200 percent; 100 is neutral.</param>
    /// <param name="colorFiltersEnabled">Whether to apply the final color pass, including theme filter overrides.</param>
    /// <param name="palette">Optional theme color overrides; null uses the theme's own palette.</param>
    /// <param name="pageIndex">Requested zero-based theme page index.</param>
    /// <param name="pageRevision">Change counter used to restart page timing after an explicit page selection.</param>
    public async Task<byte[]> RenderAsync(ThemeDefinition theme, DashboardSnapshot snapshot, DisplayProfile profile,
        bool light = false, bool ambientDrift = false, string fontFamily = "Cascadia Mono", double fontScale = 1,
        string jpegQuality = "high", CancellationToken cancellationToken = default,
        double saturationPercent = 100, double hueDegrees = 0, bool showSpectrum = true,
        double brightnessPercent = 0, double contrastPercent = 100, bool colorFiltersEnabled = true, ThemePalette? palette = null,
        int pageIndex = 0, long pageRevision = 0)
    {
        // Reject host controls before starting a worker or loading theme code.
        if (string.IsNullOrWhiteSpace(fontFamily) || fontFamily.Length > 128 || fontFamily.Any(char.IsControl)) { throw new ArgumentException("Choose a font family of 1–128 printable characters."); }
        if (!double.IsFinite(fontScale) || fontScale is < 0.65 or > 1.6) { throw new ArgumentException("Font scale must be between 0.65 and 1.6."); }
        if (jpegQuality is not ("poor" or "low" or "medium" or "high")) { throw new ArgumentException("JPEG quality must be poor, low, medium or high."); }
        palette?.Validate();
        ValidateColorAdjustments(saturationPercent, hueDegrees, brightnessPercent, contrastPercent);

        await gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            EnsureWorker();
            var id = Guid.NewGuid().ToString("N");

            // Artwork is hashed only when its source buffer changes, then sent only when needed.
            var media = snapshot.Media;
            if (!ReferenceEquals(artwork, media.Artwork))
            {
                artwork = media.Artwork;
                artworkKey = artwork is null ? null : Convert.ToHexString(SHA256.HashData(artwork));
            }
            var mediaState = new Dictionary<string, object?>
            {
                ["title"] = media.Title,
                ["artist"] = media.Artist,
                ["album"] = media.Album,
                ["source"] = media.Source,
                ["playing"] = media.Playing,
                ["playbackStatus"] = media.PlaybackStatus,
                ["positionSeconds"] = media.PositionSeconds,
                ["durationSeconds"] = media.DurationSeconds,
                ["coverKey"] = artworkKey,
                ["quality"] = null,
                ["shuffle"] = media.Shuffle
            };
            if (sentArtworkKey != artworkKey || artworkKey is null) { mediaState["coverBase64"] = artwork is null ? null : Convert.ToBase64String(artwork); }
            var matchedVolumes = snapshot.Volumes.Where(volume => volume.Name.Length >= 3 && media.Source.Contains(volume.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            // A source can expose multiple audio sessions; do not pass a different app's gain as media volume.
            var mediaVolume = matchedVolumes.Length == 1 ? matchedVolumes[0] : null;
            var now = DateTimeOffset.Now;

            // Keep the worker envelope versioned; provider observations remain explicitly nullable.
            var request = new
            {
                version = 1,
                id,
                method = "render",
                themePath = theme.DirectoryPath,
                width = profile.Width,
                height = profile.Height,
                light,
                ambientDrift,
                fontFamily,
                fontScale,
                jpegQuality,
                saturationPercent,
                hueDegrees,
                showSpectrum,
                brightnessPercent,
                contrastPercent,
                colorFiltersEnabled,
                pageIndex,
                pageRevision,
                paletteVersion = 1,
                palette,
                state = new
                {
                    media = mediaState,
                    audio = new
                    {
                        volumePercent = mediaVolume?.Percent,
                        peak = mediaVolume?.Peak,
                        muted = mediaVolume?.Muted,
                        analysis = snapshot.Playback,
                        sessions = snapshot.Volumes.Select(volume => new { name = volume.Name, volumePercent = volume.Percent, muted = volume.Muted, peak = volume.Peak })
                    },
                    clock = new { iso = now.ToString("O", CultureInfo.InvariantCulture), locale = CultureInfo.CurrentCulture.Name },
                    desktop = new { palette = environment.DesktopPalette() },
                    codex = environment.CodexActivity(),
                    sensors = snapshot.Sensors,
                    artistProfile = snapshot.ArtistProfile
                }
            };
            try
            {
                using var response = await ExchangeAsync(request, id, "Theme rendering", cancellationToken);
                var root = response.RootElement;
                var jpeg = Convert.FromBase64String(root.GetProperty("jpegBase64").GetString() ?? "");
                var validLength = jpeg.Length is >= 4 and <= 450_000;
                var validMarkers = validLength && jpeg[0] == 0xff && jpeg[1] == 0xd8 && jpeg[^2] == 0xff && jpeg[^1] == 0xd9;
                if (!validMarkers)
                { throw new IOException("Python theme returned an invalid or oversized JPEG."); }

                // Publish metadata only after the bounded frame has passed validation.
                var accent = root.TryGetProperty("accentColor", out var accentValue) && accentValue.ValueKind == JsonValueKind.String
                    ? accentValue.GetString() : null;
                AccentColor = accent is { Length: 7 } && accent[0] == '#' && accent[1..].All(Uri.IsHexDigit) ? accent : null;
                IsTransitioning = root.TryGetProperty("transitionActive", out var transition) && transition.GetBoolean();
                ActivePageId = root.TryGetProperty("pageId", out var pageId) ? pageId.GetString() : null;
                ActivePageTitle = root.TryGetProperty("pageTitle", out var pageTitle) ? pageTitle.GetString() : null;
                ActivePageIndex = root.TryGetProperty("pageIndex", out var activePageIndex) ? activePageIndex.GetInt32() : 0;
                ActivePageCount = root.TryGetProperty("pageCount", out var pageCount) ? pageCount.GetInt32() : 1;
                sentArtworkKey = artworkKey;
                return jpeg;
            }
            catch { StopWorker(); throw; }
        }
        finally { gate.Release(); }
    }

    /// <summary>Lists the same monospace fonts the renderer can use, without loading a theme.</summary>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    public async Task<(IReadOnlyList<string> Families, string? Notice)> ListFontsAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            EnsureWorker();
            var id = Guid.NewGuid().ToString("N");
            try
            {
                using var response = await ExchangeAsync(new { version = 1, id, method = "fonts" }, id, "Font discovery", cancellationToken);
                var root = response.RootElement;
                if (!root.TryGetProperty("families", out var values) || values.ValueKind != JsonValueKind.Array || values.GetArrayLength() > 512)
                { throw new IOException("Python worker returned an invalid font catalog."); }
                var families = new List<string>();
                foreach (var value in values.EnumerateArray())
                {
                    var family = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                    if (string.IsNullOrWhiteSpace(family) || family.Length > 128 || family.Any(char.IsControl))
                    { throw new IOException("Python worker returned an invalid font family."); }
                    families.Add(family);
                }
                string? notice = null;
                if (root.TryGetProperty("notice", out var message) && message.ValueKind != JsonValueKind.Null)
                {
                    if (message.ValueKind != JsonValueKind.String || message.GetString()!.Length > 1000)
                    { throw new IOException("Python worker returned an invalid font notice."); }
                    notice = message.GetString();
                }
                return (families.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(), notice);
            }
            catch { StopWorker(); throw; }
        }
        finally { gate.Release(); }
    }

    /// <summary>Rejects non-finite or out-of-range runtime color settings.</summary>
    /// <param name="saturationPercent">Saturation from 0 to 200 percent; 100 is neutral.</param>
    /// <param name="hueDegrees">Hue rotation from -180 to 180 degrees; zero is neutral.</param>
    /// <param name="brightnessPercent">Brightness offset from -100 to 100 percent; zero is neutral.</param>
    /// <param name="contrastPercent">Contrast from 0 to 200 percent; 100 is neutral.</param>
    public static void ValidateColorAdjustments(double saturationPercent, double hueDegrees,
        double brightnessPercent = 0, double contrastPercent = 100)
    {
        if (!double.IsFinite(saturationPercent) || saturationPercent is < 0 or > 200)
        { throw new ArgumentOutOfRangeException(nameof(saturationPercent), "Saturation must be a finite percentage from 0 to 200."); }
        if (!double.IsFinite(hueDegrees) || hueDegrees is < -180 or > 180)
        { throw new ArgumentOutOfRangeException(nameof(hueDegrees), "Hue must be a finite angle from -180 to 180 degrees."); }
        if (!double.IsFinite(brightnessPercent) || brightnessPercent is < -100 or > 100)
        { throw new ArgumentOutOfRangeException(nameof(brightnessPercent), "Brightness must be a finite percentage from -100 to 100."); }
        if (!double.IsFinite(contrastPercent) || contrastPercent is < 0 or > 200)
        { throw new ArgumentOutOfRangeException(nameof(contrastPercent), "Contrast must be a finite percentage from 0 to 200."); }
    }

    // Both operations share the caller's gate, process, framing checks and bounded deadline.
    /// <summary>Exchanges one versioned request with the worker and validates its response ID.</summary>
    /// <param name="request">Versioned worker payload serialized as one JSON line.</param>
    /// <param name="id">Request correlation ID that must be echoed by the worker response.</param>
    /// <param name="operation">Human-readable operation name included in worker failure messages.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    private async Task<JsonDocument> ExchangeAsync(object request, string id, string operation, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        var token = deadline.Token;
        try
        {
            await worker!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, Json).AsMemory(), token);
            await worker.StandardInput.FlushAsync(token);
            var line = await ReadResponseAsync(worker.StandardOutput.BaseStream, token);
            var response = JsonDocument.Parse(line);
            try
            {
                var root = response.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out var version)
                    || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
                { throw new IOException("Python worker response uses an unsupported protocol version."); }
                if (!root.TryGetProperty("id", out var responseId) || responseId.ValueKind != JsonValueKind.String || responseId.GetString() != id)
                { throw new IOException("Python worker response ID did not match."); }
                if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
                {
                    var message = root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString() : "Python worker failed.";
                    throw new IOException(message);
                }
                return response;
            }
            catch { response.Dispose(); throw; }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException($"{operation} exceeded 12 seconds. The worker will restart on the next request."); }
    }

    /// <summary>Starts the configured Python worker if no usable child is running.</summary>
    private void EnsureWorker()
    {
        if (worker is { HasExited: false }) { return; }
        StopWorker();
        var runtime = ThemeCatalog.FindContentDirectory("theme-runtime") ?? throw new IOException("The theme-runtime folder is missing beside the engine.");
        var script = Path.Combine(runtime, "worker.py");
        if (!File.Exists(script)) { throw new IOException("The theme-runtime/worker.py file is missing."); }
        var bundledPython = Path.Combine(AppContext.BaseDirectory, "python", "python.exe");
        var configured = Environment.GetEnvironmentVariable("THERMALRIGHT_VIBEWARE_PYTHON");
        var executable = !string.IsNullOrWhiteSpace(configured) ? configured : bundledPython;
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
        { throw new IOException("The bundled Python runtime is missing. THERMALRIGHT_VIBEWARE_PYTHON may specify an existing absolute interpreter path for development."); }
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = runtime
        };
        start.ArgumentList.Add("-u");
        start.ArgumentList.Add(script);
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        lastDiagnostic = "";
        try { worker = Process.Start(start) ?? throw new IOException("Python could not start."); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException)
        { throw new IOException("Python theme runtime unavailable. Set THERMALRIGHT_VIBEWARE_PYTHON to Python 3.11+ with skia-python, or include python/python.exe in the portable folder. " + error.Message, error); }
        _ = ReadDiagnosticsAsync(worker);
    }

    /// <summary>Reads one bounded protocol response from the child process.</summary>
    /// <param name="stream">Input stream owned by the caller.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    private async Task<string> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Read pipe chunks directly: ReadLineAsync can allocate an unbounded unterminated line.
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) { throw new IOException("Python worker closed its output. " + lastDiagnostic); }
            var newline = Array.IndexOf(buffer, (byte)'\n', 0, read);
            var count = newline >= 0 ? newline : read;
            if (output.Length + count > 900_000) { throw new IOException("Python worker response exceeds the frame limit."); }
            output.Write(buffer, 0, count);
            if (newline < 0) { continue; }
            for (var index = newline + 1; index < read; index++)
            { if (buffer[index] != (byte)'\r' && buffer[index] != (byte)'\n') { throw new IOException("Unexpected extra data in Python worker output."); } }
            return new UTF8Encoding(false, true).GetString(output.GetBuffer(), 0, checked((int)output.Length)).TrimEnd('\r');
        }
    }

    /// <summary>Drains bounded worker diagnostics without using protocol stdout.</summary>
    /// <param name="process">Owned worker/engine child whose diagnostic stream is being drained.</param>
    private async Task ReadDiagnosticsAsync(Process process)
    {
        try
        {
            var buffer = new byte[1024];
            while (true)
            {
                var read = await process.StandardError.BaseStream.ReadAsync(buffer.AsMemory());
                if (read == 0) { break; }
                // Theme prints are redirected here; a long unterminated print must not grow an unbounded line buffer.
                if (ReferenceEquals(worker, process))
                { Volatile.Write(ref lastDiagnostic, Encoding.UTF8.GetString(buffer, Math.Max(0, read - 512), Math.Min(read, 512)).Trim()); }
            }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException) { }
    }

    /// <summary>Stops and disposes the owned Python worker.</summary>
    private void StopWorker()
    {
        var process = worker;
        worker = null;
        sentArtworkKey = null;
        IsTransitioning = false;
        if (process is null) { return; }
        try { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        process.Dispose();
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose() { disposed = true; StopWorker(); }
}
