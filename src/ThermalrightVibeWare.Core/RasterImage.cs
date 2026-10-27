/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: RasterImage.cs
 * Purpose: Reads raster images without transferring ownership of caller streams.
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
using System.Drawing;

namespace ThermalrightVibeWare.Core;

/// <summary>Checks common raster headers before giving their pixels to GDI+.</summary>
internal static class RasterImage
{
    internal const long MaximumBytes = 30 * 1024 * 1024;
    internal const long MaximumPixels = 16_777_216;

    // The caller owns the stream and keeps it alive until the returned image is disposed.
    /// <summary>Decodes a raster while keeping ownership of the input stream with the caller.</summary>
    /// <param name="stream">Input stream owned by the caller.</param>
    internal static Image Open(Stream stream)
    {
        if (!stream.CanSeek || stream.Length is <= 0 or > MaximumBytes)
        { throw new IOException("The source image exceeds the 30 MiB limit or cannot be inspected."); }
        stream.Position = 0;
        var (width, height) = Dimensions(stream);
        if (width <= 0 || height <= 0 || width > MaximumPixels / height)
        { throw new IOException("The source image exceeds 16 megapixels or has invalid dimensions."); }
        stream.Position = 0;
        var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
        if (image.Width == width && image.Height == height) { return image; }
        image.Dispose();
        throw new IOException("The decoded image does not match its inspected dimensions.");
    }

    /// <summary>Reads raster dimensions while keeping ownership of the input stream with the caller.</summary>
    /// <param name="stream">Input stream owned by the caller.</param>
    private static (long Width, long Height) Dimensions(Stream stream)
    {
        Span<byte> header = stackalloc byte[26];
        stream.ReadExactly(header);
        if (header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && header[12..16].SequenceEqual("IHDR"u8))
        { return (BinaryPrimitives.ReadUInt32BigEndian(header[16..20]), BinaryPrimitives.ReadUInt32BigEndian(header[20..24])); }
        if (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8))
        { return (BinaryPrimitives.ReadUInt16LittleEndian(header[6..8]), BinaryPrimitives.ReadUInt16LittleEndian(header[8..10])); }
        if (header[..2].SequenceEqual("BM"u8))
        {
            var dib = BinaryPrimitives.ReadUInt32LittleEndian(header[14..18]);
            if (dib == 12) { return (BinaryPrimitives.ReadUInt16LittleEndian(header[18..20]), BinaryPrimitives.ReadUInt16LittleEndian(header[20..22])); }
            if (dib >= 40) { return (BinaryPrimitives.ReadInt32LittleEndian(header[18..22]), Math.Abs((long)BinaryPrimitives.ReadInt32LittleEndian(header[22..26]))); }
        }
        if (header[0] == 0xFF && header[1] == 0xD8)
        {
            stream.Position = 2;
            Span<byte> lengthBytes = stackalloc byte[2];
            Span<byte> frame = stackalloc byte[5];
            var metadataEnd = Math.Min(stream.Length, 1024 * 1024);
            // Bound metadata scanning too. Unusual or unsupported images use the existing fallback.
            while (stream.Position < metadataEnd)
            {
                if (stream.ReadByte() != 0xFF) { break; }
                int marker;
                do { marker = stream.ReadByte(); } while (marker == 0xFF && stream.Position < metadataEnd);
                if (marker is < 0 or 0x00 or 0xDA or 0xD9 or 0xFF) { break; }
                if (marker is 0x01 or >= 0xD0 and <= 0xD8) { continue; }
                stream.ReadExactly(lengthBytes);
                var length = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
                if (length < 2 || length - 2 > stream.Length - stream.Position) { break; }
                if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
                {
                    if (length < 8) { break; }
                    stream.ReadExactly(frame);
                    return (BinaryPrimitives.ReadUInt16BigEndian(frame[3..5]), BinaryPrimitives.ReadUInt16BigEndian(frame[1..3]));
                }
                stream.Position += length - 2;
            }
        }
        throw new IOException("The image header is invalid or is not a supported PNG, JPEG, GIF or BMP raster.");
    }
}
