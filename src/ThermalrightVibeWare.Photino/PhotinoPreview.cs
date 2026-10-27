/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: PhotinoPreview.cs
 * Purpose: Bridges engine JPEGs and browser preview acknowledgements.
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
using System.Runtime.InteropServices;
using Photino.NET;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Photino;

/// <summary>One JPEG in flight until the browser has drawn and released its decoded bitmap.</summary>
internal sealed class PhotinoPreview : IStudioPreview, IDisposable
{
    private readonly PhotinoWindow window;
    private readonly object sync = new();
    private TaskCompletionSource<PreviewSize>? pending;
    private long nextId, pendingId;
    private bool disposed;

    // Read native host visibility without asking the browser, which can pause
    // script and image callbacks while its controller is hidden or minimized.
    public bool IsVisible => !OperatingSystem.IsWindows()
        || IsWindowVisible(window.WindowHandle) && !IsIconic(window.WindowHandle);

    /// <summary>Connects the desktop window to the browser's JPEG preview bridge.</summary>
    /// <param name="window">Existing studio host window to size or center.</param>
    public PhotinoPreview(PhotinoWindow window)
    {
        this.window = window;
        window.RegisterWebMessageReceivedHandler(Receive);
    }

    /// <summary>Displays the engine's encoded JPEG and reports its pixel dimensions.</summary>
    /// <param name="jpeg">Encoded JPEG bytes, reused for preview and physical output.</param>
    /// <param name="transition">Whether to cross-fade from the previously displayed frame.</param>
    public async Task<PreviewSize> ShowAsync(byte[] jpeg, bool transition)
    {
        if (jpeg.Length is 0 or > 450_000) { throw new ArgumentException("Preview JPEG exceeds the frame limit.", nameof(jpeg)); }
        var completion = new TaskCompletionSource<PreviewSize>(TaskCreationOptions.RunContinuationsAsynchronously);
        long id;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (pending is not null) { throw new InvalidOperationException("A preview frame is already in flight."); }
            pending = completion;
            id = pendingId = ++nextId;
        }
        try
        {
            var message = JsonSerializer.Serialize(new { type = "trv.preview", version = 1, id, transition, jpeg = Convert.ToBase64String(jpeg) });
            window.Invoke(() => window.SendWebMessage(message));
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
        }
        finally { lock (sync) { if (ReferenceEquals(pending, completion)) { pending = null; } } }
    }

    /// <summary>Clears the currently held preview or session resources.</summary>
    public void Clear()
    {
        lock (sync) { if (disposed) { return; } pending?.TrySetCanceled(); }
        window.Invoke(() => window.SendWebMessage("{\"type\":\"trv.preview.clear\",\"version\":1}"));
    }

    /// <summary>Handles an acknowledgement from the browser preview bridge.</summary>
    /// <param name="sender">Event source supplied by the UI or capture framework; it may be unused.</param>
    /// <param name="message">User-facing text or registered Windows message name used by this call.</param>
    private void Receive(object? sender, string message)
    {
        if (message.Length > 1024) { return; }
        try
        {
            using var json = JsonDocument.Parse(message);
            var value = json.RootElement;
            if (!value.TryGetProperty("type", out var type) || type.GetString() != "trv.preview.done") { return; }
            lock (sync)
            {
                if (disposed || pending is null || value.GetProperty("version").GetInt32() != 1 || value.GetProperty("id").GetInt64() != pendingId) { return; }
                if (value.TryGetProperty("error", out _)) { pending.TrySetException(new IOException("The browser could not show the preview frame.")); return; }
                var width = value.GetProperty("width").GetInt32();
                var height = value.GetProperty("height").GetInt32();
                if (width is < 128 or > 4096 || height is < 128 or > 4096 || (long)width * height > 8_388_608)
                { pending.TrySetException(new IOException("Invalid preview dimensions.")); return; }
                pending.TrySetResult(new(width, height));
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { }
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        lock (sync) { disposed = true; pending?.TrySetCanceled(); pending = null; }
    }

    /// <summary>Checks the native window's visibility flag.</summary>
    /// <param name="handle">Owned native Windows handle.</param>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    /// <summary>Checks whether the native window is minimized.</summary>
    /// <param name="handle">Owned native Windows handle.</param>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);
}
