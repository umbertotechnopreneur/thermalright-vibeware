# VBWR B
# Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
#requires -Version 7.0
[CmdletBinding()]
param([string]$KeepMsiPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)).TrimEnd('\', '/')
$prefix = $repo + [IO.Path]::DirectorySeparatorChar
$artifacts = Join-Path $repo 'artifacts'
$packages = Join-Path $artifacts 'packages'
$keep = if ($KeepMsiPath) { (Resolve-Path -LiteralPath $KeepMsiPath).Path } else { $null }
$tracked = @(git -C $repo ls-files)
if ($keep -and ([IO.Path]::GetExtension($keep) -ne '.msi' -or [IO.Path]::GetDirectoryName($keep) -ne $packages)) {
    throw 'KeepMsiPath must name the final MSI directly under this repository artifacts/packages folder.'
}
function Remove-Generated([string]$Path) {
    $absolute = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    if (-not $absolute.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or $absolute -eq $keep) {
        throw "Refusing to remove a path outside the generated workspace scope: $absolute"
    }
    if (-not (Test-Path -LiteralPath $absolute)) { return }
    $relative = [IO.Path]::GetRelativePath($repo, $absolute).Replace('\', '/')
    if ($tracked | Where-Object { $_ -eq $relative -or $_.StartsWith($relative + '/', [StringComparison]::OrdinalIgnoreCase) }) {
        throw "Refusing to remove tracked source files: $absolute"
    }
    $item = Get-Item -LiteralPath $absolute -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Refusing to follow a build-folder link: $absolute" }
    if ($item.PSIsContainer -and (Get-ChildItem -LiteralPath $absolute -Force -Recurse |
        Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } | Select-Object -First 1)) {
        throw "Refusing to clean generated output containing a link: $absolute"
    }
    Remove-Item -LiteralPath $absolute -Recurse -Force
}
foreach ($item in @(Get-ChildItem -LiteralPath $artifacts -Force -ErrorAction SilentlyContinue)) {
    if ($item.FullName -ne $packages) { Remove-Generated $item.FullName }
}
foreach ($item in @(Get-ChildItem -LiteralPath $packages -Force -ErrorAction SilentlyContinue)) {
    if ($item.FullName -ne $keep) { Remove-Generated $item.FullName }
}
if (-not $keep) { Remove-Generated $packages; Remove-Generated $artifacts }
$folders = @(Get-ChildItem -LiteralPath $repo -Directory -Recurse -Force |
    Where-Object { $_.Name -in @('bin', 'obj', 'temp', 'tmp', '__pycache__') -and $_.FullName -notmatch '\\\.git\\|\\artifacts\\' } |
    Sort-Object { $_.FullName.Length })
foreach ($folder in $folders) { Remove-Generated $folder.FullName }
foreach ($relative in @('src/ThermalrightVibeWare.Photino/wwwroot/libs', 'src/ThermalrightVibeWare.Photino/wwwroot/resources')) {
    Remove-Generated (Join-Path $repo $relative)
}
Write-Host "Build outputs removed. Retained MSI: $keep"
