# VBWR B
# TermalRight VibeWare | Umberto Giacobbi
# SPDX-License-Identifier: MIT
# Package the Linux UI; no installation, tests or device operations.
# VBWR E
#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PayloadPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsLinux) { throw 'DEB packaging requires Linux and dpkg-deb.' }
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$payload = (Resolve-Path -LiteralPath $PayloadPath).Path
foreach ($name in @('ThermalrightVibeWare', 'Photino.Native.so', 'engine/trv', 'engine/python/linux-runtime.json', 'wwwroot/index.html')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $name) -PathType Leaf)) { throw "Linux payload is missing $name." }
}
$python = Get-Content -LiteralPath (Join-Path $payload 'engine/python/linux-runtime.json') -Raw | ConvertFrom-Json
if ($python.version -ne 1 -or $python.major -ne 3 -or $python.minor -lt 11 -or $python.minor -gt 14 -or $python.architecture -ne 'x64') {
    throw 'Invalid Linux Python dependency manifest.'
}
$pythonDepends = "python3 (>= 3.$($python.minor)), python3 (<< 3.$($python.minor + 1))"
$version = (Get-Content "$repo/installer/Version.txt" -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid package version.' }
$work = Join-Path ([IO.Path]::GetTempPath()) ('thermalright-vibeware-deb-' + [guid]::NewGuid().ToString('N'))
# Debian needs real Unix modes, which an NTFS checkout cannot preserve.
# TMPDIR can select the temporary filesystem; nothing is installed there.
$root = Join-Path $work 'package'
try {
    $app = Join-Path $root 'opt/thermalright-vibeware'
    $desktop = Join-Path $root 'usr/share/applications'
    $icons = Join-Path $root 'usr/share/icons/hicolor/256x256/apps'
    New-Item -ItemType Directory -Path "$root/DEBIAN", $app, $desktop, $icons -Force | Out-Null
    Copy-Item -Path "$payload/*" -Destination $app -Recurse -Force
    Copy-Item "$repo/src/ThermalrightVibeWare.App/Assets/thermalright-vibeware-icon.png" "$icons/thermalright-vibeware.png"
    @"
Package: thermalright-vibeware
Version: $version
Architecture: amd64
Maintainer: Umberto Giacobbi
Section: utils
Priority: optional
Depends: $pythonDepends, libgtk-3-0t64, libwebkit2gtk-4.1-0, libnotify4, libayatana-appindicator3-1, libegl1, libgl1, libexpat1, libc6, libgcc-s1, libstdc++6, libicu78 | libicu76 | libicu74, libssl3t64, zlib1g
Description: TermalRight VibeWare local theme studio
 Local Linux theme preview and sensor diagnostics. USB, audio and media are unavailable.
"@ | Set-Content "$root/DEBIAN/control" -Encoding utf8NoBOM
    @'
[Desktop Entry]
Type=Application
Name=TermalRight VibeWare
Exec=/opt/thermalright-vibeware/ThermalrightVibeWare
Icon=thermalright-vibeware
Terminal=false
Categories=Utility;
'@ | Set-Content "$desktop/thermalright-vibeware.desktop" -Encoding utf8NoBOM
    $licenses = Join-Path $root 'usr/share/doc/thermalright-vibeware'
    New-Item -ItemType Directory -Path $licenses -Force | Out-Null
    foreach ($name in @('LICENSE', 'TRADEMARKS.md', 'THIRD_PARTY_NOTICES.md')) { Copy-Item "$repo/$name" $licenses }
    Copy-Item "$repo/licenses" $licenses -Recurse
    # NTFS inputs can all appear executable. Normalize files and directories explicitly.
    foreach ($item in @(Get-ChildItem -LiteralPath $root -Recurse -Force)) {
        $mode = if ($item.PSIsContainer) { 493 } else { 420 } # 0755 / 0644
        [IO.File]::SetUnixFileMode($item.FullName, [IO.UnixFileMode]$mode)
    }
    & chmod 755 $root "$app/ThermalrightVibeWare" "$app/engine/trv"
    if ($LASTEXITCODE -ne 0) { throw 'Could not set package permissions.' }
    if ([IO.File]::GetUnixFileMode("$root/DEBIAN") -ne [IO.UnixFileMode]493) {
        throw 'DEB staging requires Unix permissions. Set TMPDIR to a Linux filesystem.'
    }
    $packages = Join-Path $repo 'artifacts/packages'
    New-Item -ItemType Directory -Path $packages -Force | Out-Null
    $output = Join-Path $packages "thermalright-vibeware_${version}_amd64.deb"
    & dpkg-deb --root-owner-group --build $root $output
    if ($LASTEXITCODE -ne 0) { throw 'DEB packaging failed.' }
    Write-Host "DEB: $output"
} finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
