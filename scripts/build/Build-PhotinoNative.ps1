# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: Build-PhotinoNative.ps1
# Purpose: Builds the pinned Photino native source for the selected architecture.
#
# VibeWare: Human intent, AI execution, and plenty of tokens
# Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
#
# Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
# Human guidance: Umberto Giacobbi; maintainability and standard file headers.
#
# Copyright (c) 2026 Umberto Giacobbi
# SPDX-License-Identifier: MIT
#
# VBWR E

#requires -Version 7.0

[CmdletBinding()]
param([ValidateSet('x64', 'ARM64')][string]$Architecture = 'x64', [switch]$PreviewCapture, [switch]$PrepareImports)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$commit = 'd790e5be3127ec628d0a9f1f9fac68e14f339c2a'
$archiveHash = '5C6546959017509B80A1884EADCD963FDDCE23A58D5043FE02F42A962E0B6A17'
$root = Join-Path $repo 'artifacts/native/photino-memory'
$destination = Join-Path $root $Architecture
if ($PreviewCapture) { $destination += '-preview-capture' }
$library = Join-Path $destination 'Photino.Native.dll'
$manifestPath = Join-Path $destination 'build.json'
$header = Join-Path $repo 'native/photino/UtfConversion.h'
$patchKey = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash + (Get-FileHash -LiteralPath $header -Algorithm SHA256).Hash
if ($PreviewCapture) { $patchKey += (Get-FileHash -LiteralPath (Join-Path $repo 'tests/native/WebViewCapture.h') -Algorithm SHA256).Hash }
if ((Test-Path -LiteralPath $manifestPath) -and (Test-Path -LiteralPath $library)) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.patchKey -eq $patchKey -and $manifest.libraryHash -eq (Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash) { return }
}
New-Item -ItemType Directory -Path $root, $destination -Force | Out-Null

# Pinned Apache-2.0 sources remain in ignored build output.
$archive = Join-Path $root 'photino-native.zip'
if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri "https://api.github.com/repos/tryphotino/photino.Native/zipball/$commit" -Headers @{ 'User-Agent' = 'ThermalrightVibeWare-build' } -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveHash) { throw 'Photino source archive checksum mismatch.' }
& "$PSScriptRoot/../maintenance/Assert-DefenderClean.ps1" -Path $archive -Source "https://github.com/tryphotino/photino.Native/commit/$commit" -ScanNewImport:$PrepareImports
# Legacy native NuGet imports use path checks that fail in a long runner checkout.
# Keep extracted sources short even when the repository name is long.
$work = Join-Path $repo ('artifacts/pn/' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
[IO.Compression.ZipFile]::ExtractToDirectory($archive, $work)
& "$PSScriptRoot/../maintenance/Assert-DefenderClean.ps1" -Path $work -Source "Photino.Native source $commit" -ScanNewImport:$PrepareImports
$sourceRoots = @(Get-ChildItem -LiteralPath $work -Directory)
if ($sourceRoots.Count -ne 1) { throw 'Unexpected Photino source layout.' }
$source = $sourceRoots[0].FullName
$native = Join-Path $source 'Photino.Native'
Copy-Item -LiteralPath $header -Destination (Join-Path $native 'UtfConversion.h')
<#
.SYNOPSIS
Applies a required source patch and fails if its expected text is absent.

.PARAMETER Text
Source text or payload-relative path processed by this helper.

.PARAMETER Old
Exact source fragment that the pinned patch expects to find.

.PARAMETER New
Replacement source fragment for that exact pinned input.
#>
function Replace-Required([string]$Text, [string]$Old, [string]$New) {
    if (-not $Text.Contains($Old)) { throw "Pinned Photino patch no longer matches: $Old" }
    return $Text.Replace($Old, $New)
}
<#
.SYNOPSIS
Writes generated build input as UTF-8 without a byte-order mark.

.PARAMETER Path
Literal local path being validated, read, written or removed by this helper.

.PARAMETER Text
Source text or payload-relative path processed by this helper.
#>
function Save-Source([string]$Path, [string]$Text) { [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false)) }
$projectPath = Join-Path $native 'Photino.Native.vcxproj'
$project = Get-Content -LiteralPath $projectPath -Raw
# Use the selected Visual Studio installation's default C++ toolset. Its ARM64
# tools may not include the older v143 toolset pinned by the upstream project.
$project = Replace-Required $project '<PlatformToolset>v143</PlatformToolset>' ''
# The pinned project imports defaults after its configuration groups. Select
# the toolset here so x64 cannot fall back to its legacy v100 toolset.
$project = Replace-Required $project '<Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />' @'
<Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
  <PropertyGroup>
    <PlatformToolset>$(DefaultPlatformToolset)</PlatformToolset>
  </PropertyGroup>
'@
Save-Source $projectPath $project
$headerPath = Join-Path $native 'Photino.h'
$text = Get-Content -LiteralPath $headerPath -Raw
$text = Replace-Required $text 'AutoString ToUTF16String(AutoString source);' 'std::wstring ToUTF16String(AutoString source);'
$text = Replace-Required $text 'AutoString ToUTF8String(AutoString source);' 'std::string ToUTF8String(AutoString source);'
$text = Replace-Required $text 'void SetIconFile(AutoString filename);' 'void SetIconFile(const wchar_t* filename);'
$text = Replace-Required $text 'std::wstring ToUTF16String(AutoString source);' "std::wstring _ownedIconFileName;`n`tstd::wstring ToUTF16String(AutoString source);"
$text = Replace-Required $text 'void RefitContent();' "void RefitContent();`n`tvoid SyncWebViewVisibility(bool visible);"
$text = $text.Replace('#define UNICODE', "#ifndef UNICODE`n#define UNICODE`n#endif").Replace('#define _UNICODE', "#ifndef _UNICODE`n#define _UNICODE`n#endif")
Save-Source $headerPath $text
foreach ($fileName in @('Photino.Windows.cpp', 'Photino.Windows.Dialog.cpp')) {
    $path = Join-Path $native $fileName
    $text = (Get-Content -LiteralPath $path -Raw).Replace("`r`n", "`n")
    $text = [regex]::Replace($text, 'AutoString (\w+) = ((?:wndInstance->)?ToUTF16String\([^;]+\));', {
        param($match)
        $name = $match.Groups[1].Value
        "const auto ${name}Storage = $($match.Groups[2].Value);`n`t`tconst auto $name = ${name}Storage.c_str();"
    })
    if ($fileName -eq 'Photino.Windows.cpp') {
        $text = Replace-Required $text '#include "Photino.h"' "#include `"Photino.h`"`n#include `"UtfConversion.h`""
        $text = Replace-Required $text 'void Photino::SendWebMessage(AutoString message)' 'void Photino::SendWebMessage(AutoString messageUtf8)'
        $text = Replace-Required $text 'message = ToUTF16String(message);' "const auto messageStorage = ToUTF16String(messageUtf8);`n`tconst auto message = messageStorage.c_str();"
        $text = Replace-Required $text 'void Photino::SetTitle(AutoString title)' 'void Photino::SetTitle(AutoString titleUtf8)'
        $text = Replace-Required $text 'void Photino::ShowNotification(AutoString title, AutoString body)' 'void Photino::ShowNotification(AutoString titleUtf8, AutoString bodyUtf8)'
        $text = Replace-Required $text 'title = ToUTF16String(title);' "const auto titleStorage = ToUTF16String(titleUtf8);`n`tconst auto title = titleStorage.c_str();"
        $text = Replace-Required $text 'body = ToUTF16String(body);' "const auto bodyStorage = ToUTF16String(bodyUtf8);`n`tconst auto body = bodyStorage.c_str();"
        $text = Replace-Required $text 'void Photino::SetIconFile(AutoString filename)' 'void Photino::SetIconFile(const wchar_t* filename)'
        $text = Replace-Required $text 'this->_iconFileName = filename;' "_ownedIconFileName = filename ? filename : L`"`";`n`tthis->_iconFileName = _ownedIconFileName.empty() ? nullptr : &_ownedIconFileName[0];"
        $text = [regex]::Replace($text, '(?ms)^AutoString Photino::ToUTF8String\(AutoString source\)\s*\{.*?^\}', "std::string Photino::ToUTF8String(AutoString source)`n{`n`treturn ThermalrightVibeWareNative::ToUtf8(source);`n}")
        $text = [regex]::Replace($text, '(?ms)^AutoString Photino::ToUTF16String\(AutoString source\)\s*\{.*?^\}', "std::wstring Photino::ToUTF16String(AutoString source)`n{`n`treturn ThermalrightVibeWareNative::ToUtf16(reinterpret_cast<const char*>(source));`n}")
        # Restoring the host HWND does not restore a WebView created while hidden.
        # Keep the controller in sync for tray hide/show, minimize/restore and its
        # asynchronous creation. RefitContent already runs on WM_SIZE and creation.
        $text = Replace-Required $text "`tcase WM_SIZE:" @'
	case WM_SHOWWINDOW:
	{
		Photino* Photino = hwndToPhotino[hwnd];
		if (Photino) Photino->SyncWebViewVisibility(wParam != FALSE);
		break;
	}
	case WM_SIZE:
'@
        $text = Replace-Required $text "void Photino::RefitContent()`n{`n`tif (_webviewController)" @'
void Photino::SyncWebViewVisibility(bool visible)
{
	if (_webviewController)
		_webviewController->put_IsVisible(visible && !IsIconic(_hWnd));
}

void Photino::RefitContent()
{
	SyncWebViewVisibility(IsWindowVisible(_hWnd) != FALSE);
	if (_webviewController)
'@
        if ($text.Contains('new std::wstring') -or $text.Contains('new std::string')) { throw 'An unowned conversion allocation remains.' }
    }
    else {
        $text = Replace-Required $text 'AutoString title, AutoString defaultPath' 'AutoString titleUtf8, AutoString defaultPathUtf8'
        $text = Replace-Required $text 'AutoString title, AutoString text' 'AutoString titleUtf8, AutoString textUtf8'
        $text = Replace-Required $text 'T* Create(HRESULT* hResult, AutoString titleUtf8, AutoString defaultPathUtf8)' 'T* Create(HRESULT* hResult, const wchar_t* title, const wchar_t* defaultPath)'
        foreach ($name in @('title', 'defaultPath', 'text')) {
            $text = Replace-Required $text "$name = _window->ToUTF16String($name);" "const auto ${name}Storage = _window->ToUTF16String(${name}Utf8);`n`tconst auto $name = ${name}Storage.c_str();"
        }
    }
    Save-Source $path $text
}
if ($PreviewCapture) {
    # This export exists only in the opt-in diagnostic DLL, never in a portable package.
    $text = (Get-Content -LiteralPath $headerPath -Raw).Replace('public:', "public:`n ICoreWebView2* PreviewCaptureWebView() { return _webviewWindow.get(); }")
    Save-Source $headerPath $text
    Copy-Item -LiteralPath (Join-Path $repo 'tests/native/WebViewCapture.h') -Destination (Join-Path $native 'WebViewCapture.h')
    [IO.File]::AppendAllText((Join-Path $native 'Photino.Windows.cpp'), "`n#include `"WebViewCapture.h`"`n")
}
$packages = Join-Path $source 'packages'
foreach ($package in @(
    @{ Id = 'Microsoft.Web.WebView2'; Version = '1.0.1462.37'; Sha512 = 'Kh4XEoWI2KMRwj+E7KePM9gFcKXKqz+7bgSwu+y7MEd6eH87jYhFtEMf8bkJE21I/Qi1lz+5QgfS7CFGzpwM5Q==' },
    @{ Id = 'Microsoft.Windows.ImplementationLibrary'; Version = '1.0.220914.1'; Sha512 = 'sdcQsiPDUX7H1vQ9vJevIaAl3DATr2U08aFNX7dtHVhlM8usentyNcABN8Wl2y2SbI9A8SfwtowvUQufwDd4Sw==' }
)) {
    $id = $package.Id.ToLowerInvariant()
    $version = $package.Version
    $packageArchive = Join-Path $root "$id.$version.nupkg"
    if (-not (Test-Path -LiteralPath $packageArchive)) { Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg" -OutFile $packageArchive }
    $actual = [Convert]::ToBase64String([Convert]::FromHexString((Get-FileHash -LiteralPath $packageArchive -Algorithm SHA512).Hash))
    if ($actual -ne $package.Sha512) { throw "Native SDK checksum mismatch: $id" }
    & "$PSScriptRoot/../maintenance/Assert-DefenderClean.ps1" -Path $packageArchive -Source "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg" -ScanNewImport:$PrepareImports
    [IO.Compression.ZipFile]::ExtractToDirectory($packageArchive, (Join-Path $packages ($package.Id + '.' + $version)))
}
& "$PSScriptRoot/../maintenance/Assert-DefenderClean.ps1" -Path $packages -Source 'Official NuGet: Microsoft.Web.WebView2 1.0.1462.37; Microsoft.Windows.ImplementationLibrary 1.0.220914.1' -ScanNewImport:$PrepareImports
if ($PrepareImports) { Write-Host 'Photino source and SDK imports prepared. No native compilation ran.'; return }
$msbuild = $null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (Test-Path -LiteralPath $vswhere) {
    $found = @(& $vswhere -latest -prerelease -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild\**\Bin\MSBuild.exe')
    if ($found.Count -gt 0) { $msbuild = $found[0] }
}
if (-not $msbuild) {
    $instances = Join-Path $env:ProgramData 'Microsoft/VisualStudio/Packages/_Instances'
    foreach ($state in (Get-ChildItem -LiteralPath $instances -Filter state.json -Recurse -File -ErrorAction SilentlyContinue)) {
        $installation = (Get-Content -LiteralPath $state.FullName -Raw | ConvertFrom-Json).installationPath
        $candidate = Join-Path $installation 'MSBuild/Current/Bin/MSBuild.exe'
        if ((Test-Path -LiteralPath $candidate) -and (Test-Path -LiteralPath (Join-Path $installation 'VC/Tools/MSVC'))) { $msbuild = $candidate; break }
    }
}
if (-not $msbuild) { throw 'Existing Visual Studio C++ tools are required. This script does not install tools.' }
& $msbuild $projectPath /nologo /m /v:minimal /p:Configuration=Release "/p:Platform=$Architecture" "/p:OutDir=$destination\" "/p:IntDir=$work\obj\$Architecture\"
if ($LASTEXITCODE -ne 0) { throw 'Patched Photino.Native build failed.' }
$manifest = @{ sourceCommit = $commit; patchKey = $patchKey; libraryHash = (Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash; architecture = $Architecture }
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
Write-Host "Patched Photino.Native ($Architecture): $library"
