/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: Thermalright04165302Protocol.cs
 * Purpose: Encodes the independently documented Thermalright device-info and picture packets.
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

using System.Buffers.Binary;

namespace ThermalrightVibeWare.Core;

/// <summary>Defines the independently documented wire constants for Thermalright USB ID 0416:5302.</summary>
public static class Thermalright04165302Protocol
{
    /// <summary>Gets the vendor ID advertised by the supported panel family.</summary>
    public const ushort VendorId = 0x0416;

    /// <summary>Gets the product ID advertised by the currently connected panel family.</summary>
    public const ushort ProductId = 0x5302;

    /// <summary>Gets the documented bulk OUT endpoint.</summary>
    public const byte WriteEndpoint = 0x02;

    /// <summary>Gets the documented bulk IN endpoint.</summary>
    public const byte ReadEndpoint = 0x81;

    /// <summary>Gets the fixed transfer size required by the discovery handshake.</summary>
    public const int TransferSize = 512;

    /// <summary>Gets the device-information command identifier.</summary>
    public const byte DeviceInfoCommand = 0x01;

    /// <summary>Gets the picture command identifier.</summary>
    public const byte PictureCommand = 0x02;

    /// <summary>Gets the documented device-present byte in a successful reply.</summary>
    public const byte DevicePresentFlag = 0x10;

    /// <summary>Gets the four-byte protocol preamble.</summary>
    public static ReadOnlySpan<byte> Magic => [0xDA, 0xDB, 0xDC, 0xDD];

    /// <summary>Creates the non-destructive device-information request packet.</summary>
    public static byte[] CreateDeviceInfoRequest()
    {
        var request = new byte[TransferSize];
        Magic.CopyTo(request);
        request[12] = DeviceInfoCommand;
        return request;
    }

    /// <summary>Wraps an encoded frame and pads the wire packet to a 512-byte boundary.</summary>
    /// <param name="profile">Validated target dimensions and pixel encoding.</param>
    /// <param name="payload">Encoded image payload without the wire-protocol header or padding.</param>
    public static byte[] CreatePicturePacket(DisplayProfile profile, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Width is < 32 or > 2048 || profile.Height is < 32 or > 2048)
        {
            throw new ArgumentException("Unsupported profile dimensions.", nameof(profile));
        }
        if (profile.Encoding != PanelPixelEncoding.Jpeg && payload.Length != profile.FrameBytes)
        {
            throw new ArgumentException("RGB565 payload size must match the selected profile.", nameof(payload));
        }
        if (payload.IsEmpty || payload.Length > 8 * 1024 * 1024)
        {
            throw new ArgumentException("The encoded frame must contain between one byte and 8 MiB.", nameof(payload));
        }
        if (profile.Encoding == PanelPixelEncoding.Jpeg
            && (payload.Length < 4 || payload[0] != 0xFF || payload[1] != 0xD8 || payload[^2] != 0xFF || payload[^1] != 0xD9))
        {
            throw new ArgumentException("The JPEG payload is missing its start/end markers.", nameof(payload));
        }
        if (profile.Encoding == PanelPixelEncoding.Jpeg && payload.Length > 450_000)
        {
            throw new ArgumentException("The JPEG exceeds the panel's 450 KB limit.", nameof(payload));
        }
        var length = checked(20 + payload.Length);
        var packet = new byte[checked((length + 511) / 512 * 512)];
        Magic.CopyTo(packet);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(4, 2), PictureCommand);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(6, 2), profile.Encoding == PanelPixelEncoding.Jpeg ? (ushort)0 : (ushort)1);
        // RGB565 firmware uses the legacy header geometry; JPEG declares the actual raster dimensions.
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8, 2), profile.Encoding == PanelPixelEncoding.Jpeg ? checked((ushort)profile.Width) : (ushort)240);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10, 2), profile.Encoding == PanelPixelEncoding.Jpeg ? checked((ushort)profile.Height) : (ushort)320);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12, 4), PictureCommand);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(16, 4), payload.Length);
        payload.CopyTo(packet.AsSpan(20));
        return packet;
    }
}
