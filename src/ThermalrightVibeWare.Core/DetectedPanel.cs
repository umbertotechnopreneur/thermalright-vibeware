/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: DetectedPanel.cs
 * Purpose: Describes a detected Windows panel identity before a transport is resolved.
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

/// <summary>Represents a matching panel that Windows can see even when no user-mode WinUSB path is registered.</summary>
public sealed record DetectedPanel(string InstanceId)
{
    /// <summary>Gets the USB identifier shown to the user.</summary>
    public string Id => $"{Thermalright04165302Protocol.VendorId:X4}:{Thermalright04165302Protocol.ProductId:X4}";
}
