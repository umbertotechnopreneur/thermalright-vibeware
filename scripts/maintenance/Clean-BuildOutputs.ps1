# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: Clean-BuildOutputs.ps1
# Purpose: Removes scoped generated repository output while preserving the requested final MSI.
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
param([string]$KeepMsiPath, [string]$KeepPackagePath, [switch]$KeepPreparedWindowsBuild)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($KeepPreparedWindowsBuild -and -not $IsWindows) { throw 'Prepared Windows build cleanup requires Windows.' }
$repo = [IO.Path]::GetFullPath((Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)).TrimEnd('\', '/')
$prefix = $repo + [IO.Path]::DirectorySeparatorChar
$artifacts = Join-Path $repo 'artifacts'
$packages = Join-Path $artifacts 'packages'
if ($KeepMsiPath -and $KeepPackagePath) { throw 'Select only one final package to retain.' }
if ($KeepPackagePath) { $KeepMsiPath = $KeepPackagePath }
$keep = if ($KeepMsiPath) { (Resolve-Path -LiteralPath $KeepMsiPath).Path } else { $null }
$tracked = @(git -c core.quotePath=false -C $repo ls-files)
if ($LASTEXITCODE -ne 0) { throw 'Git could not list tracked files. Cleanup stopped.' }
if ($keep -and ([IO.Path]::GetExtension($keep) -notin @('.msi', '.deb', '.zip') -or [IO.Path]::GetDirectoryName($keep) -ne $packages)) {
    throw 'The retained path must name the final MSI, DEB or ZIP directly under this repository artifacts/packages folder.'
}

function Assert-NoRedirectedPath([string]$Path) {
    for ($current = $Path; ; $current = [IO.Path]::GetDirectoryName($current)) {
        $item = Get-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
        if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to follow a build-folder link: $current"
        }
        if ($current -eq $repo) { return }
    }
}

Assert-NoRedirectedPath $packages
if ($keep) { Assert-NoRedirectedPath $keep }
<#
.SYNOPSIS
Removes only a verified generated path inside this repository.

.PARAMETER Path
Literal local path being validated, read, written or removed by this helper.
#>
function Remove-Generated([string]$Path) {
    $absolute = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    if (-not $absolute.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or $absolute -eq $keep) {
        throw "Refusing to remove a path outside the generated workspace scope: $absolute"
    }
    if (-not (Test-Path -LiteralPath $absolute)) { return }
    Assert-NoRedirectedPath $absolute
    $relative = [IO.Path]::GetRelativePath($repo, $absolute).Replace('\', '/')
    if ($tracked | Where-Object { $_ -eq $relative -or $_.StartsWith($relative + '/', [StringComparison]::OrdinalIgnoreCase) }) {
        throw "Refusing to remove tracked source files: $absolute"
    }
    $item = Get-Item -LiteralPath $absolute -Force
    if ($item.PSIsContainer -and (Get-ChildItem -LiteralPath $absolute -Force -Recurse |
        Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } | Select-Object -First 1)) {
        throw "Refusing to clean generated output containing a link: $absolute"
    }
    Remove-Item -LiteralPath $absolute -Recurse -Force
}
foreach ($item in @(Get-ChildItem -LiteralPath $packages -Force -ErrorAction SilentlyContinue)) {
    if ($item.FullName -ne $keep) { Remove-Generated $item.FullName }
}
if ($KeepPreparedWindowsBuild) {
    foreach ($folder in @('staging', 'msi', 'pn')) { Remove-Generated (Join-Path $artifacts $folder) }
    return
}
foreach ($item in @(Get-ChildItem -LiteralPath $artifacts -Force -ErrorAction SilentlyContinue)) {
    if ($item.FullName -ne $packages) { Remove-Generated $item.FullName }
}
if (-not $keep) { Remove-Generated $packages; Remove-Generated $artifacts }
$folders = @(Get-ChildItem -LiteralPath $repo -Directory -Recurse -Force |
    Where-Object { $_.Name -in @('bin', 'obj', 'temp', 'tmp', '__pycache__') -and $_.FullName -notmatch '\\\.git\\|\\artifacts\\' } |
    Sort-Object { $_.FullName.Length })
foreach ($folder in $folders) { Remove-Generated $folder.FullName }
foreach ($relative in @('src/ThermalrightVibeWare.Photino/wwwroot/libs', 'src/ThermalrightVibeWare.Photino/wwwroot/resources')) {
    Remove-Generated (Join-Path $repo $relative)
}
Write-Host "Build outputs removed. Retained package: $keep"
