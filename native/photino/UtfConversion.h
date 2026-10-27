/* VBWR B
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 * File: UtfConversion.h
 * Purpose: Returns owned UTF-8/UTF-16 conversions for the source-built Photino host.
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 * Modified with AI: OpenAI Codex; file and parameter documentation; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard source headers.
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 * VBWR E */
#pragma once
#include <Windows.h>
#include <string>

namespace ThermalrightVibeWareNative
{
    // Owned values survive subsequent conversions and are freed at scope exit.
    /** Converts strict UTF-8 into an owned UTF-16 string; invalid/empty input returns empty.
     * @param source Null-terminated UTF-8 input borrowed only for the conversion; nullptr is allowed.
     */
    inline std::wstring ToUtf16(const char* source)
    {
        if (!source || !*source) return {};
        const int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, source, -1, nullptr, 0);
        if (length <= 0) return {};
        std::wstring result(length, L'\0');
        if (!MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, source, -1, &result[0], length)) return {};
        result.resize(length - 1);
        return result;
    }

    /** Converts strict UTF-16 into an owned UTF-8 string; invalid/empty input returns empty.
     * @param source Null-terminated UTF-16 input borrowed only for the conversion; nullptr is allowed.
     */
    inline std::string ToUtf8(const wchar_t* source)
    {
        if (!source || !*source) return {};
        const int length = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, source, -1, nullptr, 0, nullptr, nullptr);
        if (length <= 0) return {};
        std::string result(length, '\0');
        if (!WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, source, -1, &result[0], length, nullptr, nullptr)) return {};
        result.resize(length - 1);
        return result;
    }
}
