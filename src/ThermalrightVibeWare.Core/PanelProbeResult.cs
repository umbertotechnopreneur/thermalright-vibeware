/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: PanelProbeResult.cs
 * Purpose: Parses device-info replies and resolves compatible display profiles.
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

namespace ThermalrightVibeWare.Core;

/// <summary>Contains the decoded reply to the documented device-information handshake.</summary>
public sealed record PanelProbeResult(bool IsCompatible, byte ProductMode, byte SubMode, string Serial, string Message)
{
    /// <summary>Gets a local-only hexadecimal record of the actual reply for hardware diagnosis.</summary>
    public string ResponseHex { get; init; } = string.Empty;

    /// <summary>Gets the profile supported by the documented product-mode mapping, or null for an unknown product.</summary>
    public DisplayProfile? DetectedProfile => IsCompatible ? DisplayProfileCatalog.FromProductMode(ProductMode, SubMode) : null;

    /// <summary>Decodes the HID reply, where SUB precedes PM and the unnumbered report may contain only eight data bytes.</summary>
    /// <param name="response">Device-info reply bytes in the layout accepted by this parser.</param>
    public static PanelProbeResult FromHidResponse(ReadOnlySpan<byte> response)
    {
        var hasMagic = response.Length >= 8 && response[..4].SequenceEqual(Thermalright04165302Protocol.Magic);
        var shortReply = response.Length == 8 && response[6] == 0 && response[7] == 0;
        var completeReply = response.Length >= 20 && response[12] == 1;
        var compatible = hasMagic && (shortReply || completeReply);
        var pm = response.Length >= 6 ? response[5] : (byte)0;
        var sub = response.Length >= 5 ? response[4] : (byte)0;
        // The serial marker describes optional data, not success of the device-info command.
        var serial = response.Length >= 36 && response[16] == 0x10
            ? Convert.ToHexString(response.Slice(20, 16)) : string.Empty;
        return new PanelProbeResult(compatible, pm, sub, serial,
            compatible ? $"HID device-info reply accepted ({response.Length} bytes)." : "The HID reply does not match the documented device-info format.")
        { ResponseHex = Convert.ToHexString(response) };
    }

    /// <summary>Creates a result from the 512-byte reply used by the 0416:5302 panel profile.</summary>
    /// <param name="response">Device-info reply bytes in the layout accepted by this parser.</param>
    public static PanelProbeResult FromResponse(ReadOnlySpan<byte> response)
    {
        return FromHidResponse(response);
    }
}
