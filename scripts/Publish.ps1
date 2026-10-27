# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: Publish.ps1
# Purpose: Builds the current source and stages the portable Windows distribution.
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

param(
    [ValidateSet('x64', 'ARM64')][string]$Architecture = 'x64',
    [string]$HostPython = 'python',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$repo = Split-Path $PSScriptRoot -Parent
$runtime = 'win-' + $Architecture.ToLowerInvariant()
$packageName = "ThermalrightVibeWare-$runtime-preview"
$stagingRoot = Join-Path $repo 'artifacts/staging'
$destination = Join-Path $stagingRoot ("$packageName-" + [Guid]::NewGuid().ToString('N'))
$engine = Join-Path $destination 'engine'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $repo 'artifacts/nuget-http'
$env:NUGET_PACKAGES = Join-Path $repo 'artifacts/nuget-stage'
foreach ($name in @('Cli', 'Photino')) {
    dotnet restore (Join-Path $repo "src/ThermalrightVibeWare.$name/ThermalrightVibeWare.$name.csproj") --runtime $runtime -p:Platform=$Architecture
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $name" }
}
& "$PSScriptRoot/maintenance/Assert-DefenderClean.ps1" -Path $env:NUGET_PACKAGES -Source 'https://api.nuget.org/v3/index.json; restored package archives and extracted dependencies'
foreach ($target in @(@{ Project = 'Cli'; Folder = $engine }, @{ Project = 'Photino'; Folder = $destination })) {
    $project = Join-Path $repo "src/ThermalrightVibeWare.$($target.Project)/ThermalrightVibeWare.$($target.Project).csproj"
    dotnet publish $project --no-restore --disable-build-servers --configuration $Configuration --runtime $runtime --self-contained true -p:Platform=$Architecture --output $target.Folder --warnaserror
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($target.Project) / $runtime" }
}
& (Join-Path $PSScriptRoot 'build/Bundle-Python.ps1') -Architecture $Architecture -Destination $destination -HostPython $HostPython
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install/Install-Local.ps1') -Destination (Join-Path $destination 'Install-ThermalrightVibeWare.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install/Uninstall-Local.ps1') -Destination (Join-Path $destination 'Uninstall-ThermalrightVibeWare.ps1') -Force
foreach ($document in @('README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'TRADEMARKS.md', 'PORTABLE.md', 'cave.md', 'lesson.md')) {
    Copy-Item -LiteralPath (Join-Path $repo $document) -Destination $destination -Force
}
Copy-Item -LiteralPath (Join-Path $repo 'licenses') -Destination $destination -Recurse -Force
# Ship the exact MPL-covered source used for this library build.
$sensorSource = Join-Path $destination 'sources/LibreHardwareMonitor'
New-Item -ItemType Directory -Path (Split-Path $sensorSource -Parent) -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'third_party/LibreHardwareMonitor') -Destination $sensorSource -Recurse -Force
$pythonLicense = Join-Path $engine 'python/LICENSE.txt'
if (-not (Test-Path -LiteralPath $pythonLicense)) { throw 'Bundled Python license is missing.' }
Copy-Item -LiteralPath $pythonLicense -Destination (Join-Path $destination 'licenses/Python-PSF-2.0.txt') -Force
# The CLI publish already places the canonical theme catalog under engine/themes.
# Do not add a second root/themes copy that users might edit instead of the active catalog.
if (-not (Test-Path -LiteralPath (Join-Path $engine 'themes'))) {
    throw 'CLI publish did not include engine/themes.'
}
if (Test-Path -LiteralPath (Join-Path $destination 'themes')) {
    throw 'A stale root/themes folder duplicates engine/themes. Remove only that generated folder before repackaging.'
}
if (-not (Test-Path -LiteralPath (Join-Path $engine 'plugins/thermalright-vibeware-codex/install.py'))) {
    throw 'CLI publish did not include engine/plugins/thermalright-vibeware-codex.'
}
foreach ($folder in @('docs')) {
    $target = Join-Path $destination $folder
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $source = Join-Path $repo ($folder + '/*')
    Copy-Item -Path $source -Destination $target -Recurse -Force
}
$packages = Join-Path $repo 'artifacts/packages'
New-Item -ItemType Directory -Path $packages -Force | Out-Null
$archive = Join-Path $packages "$packageName.zip"
Compress-Archive -LiteralPath $destination -DestinationPath $archive -Force
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List
Write-Host "Portable ZIP: $archive. Staging: $destination. No tests or USB commands executed."
