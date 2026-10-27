/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: StdioPanelClient.cs
 * Purpose: Owns one engine child and exchanges bounded JSON-lines requests.
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
using System.Text;
using System.Text.Json;
using ThermalrightVibeWare.Contracts;

namespace ThermalrightVibeWare.Photino;

/// <summary>Owns one hidden child engine and serializes requests. A failed/ambiguous write is never replayed.</summary>
internal sealed class StdioPanelClient : IPanelClient, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private Process? process;
    private bool failed;
    private bool disposed;
    private long nextId;

    /// <summary>Serializes a named engine request and returns its typed response.</summary>
    /// <typeparam name="T">Expected response or payload type used by this operation.</typeparam>
    /// <param name="method">Allow-listed engine or worker method name.</param>
    /// <param name="parameters">Request payload serialized with the versioned contract; null when no payload is needed.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    public async Task<T> RequestAsync<T>(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        // Allow the bounded media read (3 s), Python render (12 s) and USB write (10 s), plus IPC overhead.
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        await gate.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (failed) { throw new IOException("The engine connection failed. Reopen ThermalrightVibeWare deliberately; the last command will not be replayed."); }
            if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException("This UI is portable; Linux/macOS USB and audio providers are not implemented yet. No Windows engine was started."); }
            process ??= StartEngine();
            var id = Interlocked.Increment(ref nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var request = new EngineRequest(id, method, parameters is null ? null : JsonSerializer.SerializeToElement(parameters, PanelContract.Json));
            var line = JsonSerializer.Serialize(request, PanelContract.Json);
            if (line.Length > PanelContract.MaximumRequestCharacters) { throw new ArgumentException("Engine request is too large."); }
            try
            {
                await process.StandardInput.WriteLineAsync(line.AsMemory(), timeout.Token).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(timeout.Token).ConfigureAwait(false);
                var reply = await ReadResponseAsync(process.StandardOutput, timeout.Token).ConfigureAwait(false);
                var response = JsonSerializer.Deserialize<EngineResponse>(reply, PanelContract.Json) ?? throw new IOException("Empty engine response.");
                if (response.Version != PanelContract.Version || response.Id != id) { throw new IOException("Unexpected engine response identity/version."); }
                if (!response.Ok) { throw new EngineCommandException(response.Error ?? "The engine rejected the request."); }
                if (response.Result is not { } result) { throw new IOException("Engine response has no result."); }
                var value = result.Deserialize<T>(PanelContract.Json);
                return value is null ? throw new IOException("Engine response has a null result.") : value;
            }
            catch (EngineCommandException) { throw; }
            catch { failed = true; throw; }
        }
        finally { gate.Release(); }
    }

    /// <summary>Starts the bundled stdio engine without opening a listening port.</summary>
    private static Process StartEngine()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "engine", "trv.exe");
        if (!File.Exists(executable)) { throw new FileNotFoundException("The portable engine folder is missing. Extract the whole ThermalrightVibeWare ZIP.", executable); }
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };
        info.ArgumentList.Add("--stdio");
        var child = Process.Start(info) ?? throw new IOException("Could not start the local ThermalrightVibeWare engine.");
        // Drain diagnostics without persisting private media/session data or blocking the child process.
        _ = DrainErrorsAsync(child.StandardError);
        return child;
    }

    /// <summary>Drains bounded engine diagnostics so stderr cannot block the child.</summary>
    /// <param name="reader">Child-process text stream owned by the caller.</param>
    private static async Task DrainErrorsAsync(StreamReader reader)
    {
        try { var buffer = new char[1024]; while (await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) > 0) { } }
        catch (Exception error) when (error is IOException or ObjectDisposedException) { }
    }

    /// <summary>Reads one bounded protocol response from the child process.</summary>
    /// <param name="reader">Child-process text stream owned by the caller.</param>
    /// <param name="cancellationToken">Cancellation signal for waiting and the current operation.</param>
    private static async Task<string> ReadResponseAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
        {
            var newline = Array.IndexOf(buffer, '\n', 0, count);
            var length = newline >= 0 ? newline : count;
            if (text.Length + length > PanelContract.MaximumResponseCharacters) { throw new IOException("Engine response exceeded the size limit."); }
            text.Append(buffer, 0, length);
            if (newline >= 0)
            {
                if (newline != count - 1) { throw new IOException("The engine emitted unsolicited output."); }
                return text.ToString().TrimEnd('\r');
            }
        }
        throw new IOException("The local engine closed its output.");
    }

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        lifetime.Cancel();
        if (process is not null)
        {
            try
            {
                process.StandardInput.Close();
                if (!process.WaitForExit(10_000))
                {
                    process.Kill(entireProcessTree: true);
                    if (!process.WaitForExit(5000)) { Console.Error.WriteLine("Thermalright VibeWare: engine shutdown did not finish; driver cleanup may still be required."); }
                }
            }
            catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            process.Dispose();
        }
        lifetime.Dispose();
    }

    /// <summary>Distinguishes an engine command rejection from a broken stdio transport.</summary>
    /// <param name="message">Engine's bounded error text for the rejected command.</param>
    private sealed class EngineCommandException(string message) : Exception(message);
}
