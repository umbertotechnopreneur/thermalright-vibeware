/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: SetupGate.cpp
 * Purpose: Holds the installer's per-user launch gate until commit or rollback.
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

#include <windows.h>
#include <msi.h>
#include <msiquery.h>
#include <sddl.h>
#include <shellapi.h>
#include <objbase.h>
#include <string>
#include <vector>

// The MSI custom-action host owns this handle until commit/rollback or process
// exit. An installer crash therefore cannot leave a persistent "installing" flag.
static HANDLE transactionGate = nullptr;

/** Builds the per-user setup gate name from the effective Windows user SID.
 */
static std::wstring GateName() {
    HANDLE token = nullptr;
    if (!OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &token)) {
        if (GetLastError() != ERROR_NO_TOKEN || !OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token)) { return {}; }
    }
    DWORD bytes = 0;
    GetTokenInformation(token, TokenUser, nullptr, 0, &bytes);
    std::vector<BYTE> data(bytes);
    if (!bytes || !GetTokenInformation(token, TokenUser, data.data(), bytes, &bytes)) { CloseHandle(token); return {}; }
    CloseHandle(token);
    LPWSTR sid = nullptr;
    if (!ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(data.data())->User.Sid, &sid)) { return {}; }
    const std::wstring name = L"Global\\UmbertoGiacobbi.ThermalrightVibeWare.SetupGate." + std::wstring(sid);
    LocalFree(sid);
    return name;
}

/** Holds the per-user launch gate during installer file changes.
 */
extern "C" __declspec(dllexport) UINT __stdcall BeginSetup(MSIHANDLE) {
    try {
        const auto name = GateName();
        if (name.empty()) { return ERROR_INSTALL_FAILURE; }
        HANDLE gate = CreateEventW(nullptr, TRUE, TRUE, name.c_str());
        if (!gate) { return ERROR_INSTALL_FAILURE; }
        if (!SetEvent(gate)) { CloseHandle(gate); return ERROR_INSTALL_FAILURE; }
        transactionGate = gate;
        return ERROR_SUCCESS;
    }
    catch (...) { return ERROR_INSTALL_FAILURE; }
}

/** Releases the installer launch gate on commit or rollback.
 */
extern "C" __declspec(dllexport) UINT __stdcall EndSetup(MSIHANDLE) {
    try {
        const auto name = GateName();
        if (name.empty()) { return ERROR_INSTALL_FAILURE; }
        HANDLE gate = OpenEventW(EVENT_MODIFY_STATE, FALSE, name.c_str());
        if (!gate) { return GetLastError() == ERROR_FILE_NOT_FOUND ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE; }
        const BOOL reset = ResetEvent(gate);
        CloseHandle(gate);
        if (transactionGate) { CloseHandle(transactionGate); transactionGate = nullptr; }
        return reset ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    }
    catch (...) { return ERROR_INSTALL_FAILURE; }
}

/** Reads a bounded property from the current MSI session.
 * @param installer Current MSI session handle supplied by Windows Installer.
 * @param name Executable name or MSI property name used by this helper.
 */
static std::wstring SetupProperty(MSIHANDLE installer, const wchar_t* name) {
    DWORD length = 0;
    wchar_t empty = L'\0';
    const UINT status = MsiGetPropertyW(installer, name, &empty, &length);
    if ((status != ERROR_MORE_DATA && status != ERROR_SUCCESS) || length > 32767) { return {}; }
    std::wstring value(static_cast<size_t>(length) + 1, L'\0');
    DWORD capacity = static_cast<DWORD>(value.size());
    if (MsiGetPropertyW(installer, name, value.data(), &capacity) != ERROR_SUCCESS) { return {}; }
    value.resize(capacity);
    return value;
}

/** Checks whether an interactive successful-install completion action was selected.
 * @param installer Current MSI session handle supplied by Windows Installer.
 * @param option Exact successful-install checkbox property to inspect.
 */
static bool CompletionSelected(MSIHANDLE installer, const wchar_t* option) {
    return SetupProperty(installer, L"UILevel") == L"5" &&
        SetupProperty(installer, L"Installed").empty() &&
        SetupProperty(installer, L"REMOVE").empty() &&
        SetupProperty(installer, option) == L"1";
}

/** Shows a nonfatal warning for a requested completion action that could not open.
 * @param installer Current MSI session handle supplied by Windows Installer.
 * @param message User-facing failure or warning text without private runtime payloads.
 */
static void CompletionWarning(MSIHANDLE installer, const wchar_t* message) {
    MSIHANDLE record = MsiCreateRecord(0);
    if (!record) { return; }
    MsiRecordSetStringW(record, 0, message);
    MsiProcessMessage(installer, INSTALLMESSAGE_WARNING, record);
    MsiCloseHandle(record);
}

/** Opens the fixed product or release target using the original interactive context.
 * @param target Fixed installed-app path or fixed tagged Releases URL requested by the completion page.
 * @param directory Verified directory to enumerate or use as the completion action's working directory.
 */
static bool OpenCompletionTarget(const std::wstring& target, const wchar_t* directory = nullptr) {
    SHELLEXECUTEINFOW execute{};
    execute.cbSize = sizeof(execute);
    execute.fMask = SEE_MASK_FLAG_NO_UI;
    execute.lpVerb = L"open";
    execute.lpFile = target.c_str();
    execute.lpDirectory = directory;
    execute.nShow = SW_SHOWNORMAL;
    const HRESULT apartment = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
    const BOOL opened = ShellExecuteExW(&execute);
    if (SUCCEEDED(apartment)) { CoUninitialize(); }
    return opened != FALSE;
}

/** Starts the installed app only for the selected successful interactive completion.
 * @param installer Current MSI session handle supplied by Windows Installer.
 */
extern "C" __declspec(dllexport) UINT __stdcall StartThermalrightVibeWareAfterInstall(MSIHANDLE installer) {
    try {
        if (!CompletionSelected(installer, L"TRV_START_AFTER_INSTALL")) { return ERROR_SUCCESS; }
        std::wstring folder = SetupProperty(installer, L"INSTALLFOLDER");
        if (!folder.empty() && folder.back() != L'\\') { folder += L'\\'; }
        if (folder.empty() || !OpenCompletionTarget(folder + L"ThermalrightVibeWare.exe", folder.c_str())) {
            CompletionWarning(installer, L"Setup finished, but Thermalright VibeWare could not start. Open it from Start.");
        }
    }
    catch (...) { CompletionWarning(installer, L"Setup finished, but Thermalright VibeWare could not start. Open it from Start."); }
    return ERROR_SUCCESS;
}

/** Opens the fixed tagged Releases link only for the selected successful completion.
 * @param installer Current MSI session handle supplied by Windows Installer.
 */
extern "C" __declspec(dllexport) UINT __stdcall SeeLatestThermalrightVibeWareNews(MSIHANDLE installer) {
    try {
        if (!CompletionSelected(installer, L"TRV_SEE_LATEST_NEWS")) { return ERROR_SUCCESS; }
        // Fixed campaign tags identify this installer link, with no user/session ID.
        const std::wstring url = L"https://github.com/umbertotechnopreneur/thermalright-vibeware/releases"
            L"?utm_source=thermalright-vibeware&utm_medium=installer&utm_campaign=release_news";
        if (!OpenCompletionTarget(url)) {
            CompletionWarning(installer, L"Setup finished, but the browser could not open. Find Thermalright VibeWare releases on GitHub.");
        }
    }
    catch (...) { CompletionWarning(installer, L"Setup finished, but the browser could not open. Find Thermalright VibeWare releases on GitHub."); }
    return ERROR_SUCCESS;
}
