# VBWR B
# Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
#requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('x64', 'ARM64')][string]$Architecture = 'x64')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$destination = Join-Path $repo ('artifacts/native/msi/' + $Architecture.ToLowerInvariant())
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
New-Item -ItemType Directory -Path $destination -Force | Out-Null
& $msbuild (Join-Path $repo 'installer/SetupActions/SetupActions.vcxproj') /nologo /m /v:minimal /p:Configuration=Release "/p:Platform=$Architecture" "/p:OutDir=$destination\" "/p:IntDir=$destination\obj\"
if ($LASTEXITCODE -ne 0) { throw 'Thermalright VibeWare setup helper compilation failed.' }
& $msbuild (Join-Path $repo 'installer/SetupActions/SetupGate.vcxproj') /nologo /m /v:minimal /p:Configuration=Release "/p:Platform=$Architecture" "/p:OutDir=$destination\" "/p:IntDir=$destination\gate-obj\"
if ($LASTEXITCODE -ne 0) { throw 'Thermalright VibeWare setup gate compilation failed.' }
$executable = Join-Path $destination 'ThermalrightVibeWare.SetupActions.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'The setup helper executable is missing.' }
Write-Host "Setup helper ($Architecture): $executable. No tests or cleanup actions executed."
