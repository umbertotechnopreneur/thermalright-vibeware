/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: DisplayDevice.cs
 * Purpose: Describes a discovered panel interface and its transport/report sizes.
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

/// <summary>Represents a supported USB panel exposed through its WinUSB interface.</summary>
public sealed record DisplayDevice(string DevicePath, ushort VendorId, ushort ProductId,
    PanelTransportKind Transport = PanelTransportKind.WinUsb, int InputReportBytes = 0, int OutputReportBytes = 0)
{
    /// <summary>Gets a compact identifier suitable for CLI and UI output.</summary>
    public string Id => $"{VendorId:X4}:{ProductId:X4}";

    /// <summary>Gets a description of the native transport and report geometry.</summary>
    public string Connection => Transport == PanelTransportKind.Hid
        ? $"HID · input {InputReportBytes} bytes · output {OutputReportBytes} bytes"
        : "WinUSB";
}

/// <summary>Identifies the Windows driver used by an exposed panel interface.</summary>
public enum PanelTransportKind
{
    WinUsb,
    Hid
}
