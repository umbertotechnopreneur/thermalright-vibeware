# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: Reinstall.ps1
# Purpose: Builds a Debug MSI from current source and visibly reinstalls it.
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
param([ValidateSet('x64', 'ARM64')][string]$Architecture = 'x64')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$versionFile = Join-Path $repo 'installer/Version.txt'
$previous = [version](Get-Content -LiteralPath $versionFile -Raw).Trim()
$upgradeCode = '{0E45EA55-B9C3-44D2-AD0A-D3BB78B53096}'
$installer = New-Object -ComObject WindowsInstaller.Installer
try {
    $products = @($installer.RelatedProducts($upgradeCode))
    foreach ($product in $products) {
        $installedVersion = [version]$installer.ProductInfo($product, 'VersionString')
        if ($installedVersion -gt $previous) { $previous = $installedVersion }
    }
    foreach ($product in $products) {
        Write-Host "Uninstalling Thermalright VibeWare $product with visible Windows Installer UI."
        $process = Start-Process msiexec.exe -ArgumentList @('/x', $product, '/norestart') -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw "Uninstall failed or cancelled (exit $($process.ExitCode)). Build stopped." }
    }
    if (@($installer.RelatedProducts($upgradeCode)).Count -ne 0) { throw 'A previous MSI is still registered.' }
}
finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) }
& "$PSScriptRoot/install/Uninstall-Local.ps1"
& "$PSScriptRoot/maintenance/Clean-BuildOutputs.ps1"
if ($previous.Build -ge 65535) { throw 'MSI build version limit reached. Update major/minor explicitly.' }
$version = '{0}.{1}.{2}' -f $previous.Major, $previous.Minor, ($previous.Build + 1)
# Reserve the version before building. Retries never reuse an installed version.
Set-Content -LiteralPath $versionFile -Value $version -Encoding ascii
Write-Host "Building Thermalright VibeWare $version from clean output folders."
& "$PSScriptRoot/Publish.ps1" -Architecture $Architecture -Configuration Debug
$payloads = @(Get-ChildItem -LiteralPath (Join-Path $repo 'artifacts/staging') -Directory)
if ($payloads.Count -ne 1) { throw 'Expected exactly one fresh payload.' }
$payload = $payloads[0].FullName
& "$PSScriptRoot/build/Build-Msi.ps1" -PayloadPath $payload -Architecture $Architecture -Version $version
$msi = Join-Path $repo "artifacts/packages/ThermalrightVibeWare-win-$($Architecture.ToLowerInvariant())-$version.msi"
$process = Start-Process msiexec.exe -ArgumentList @('/i', ('"' + $msi + '"'), '/norestart') -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Install failed or cancelled (exit $($process.ExitCode)). MSI retained." }
$installed = Join-Path $env:LOCALAPPDATA 'Umberto Giacobbi.biz/Apps/ThermalrightVibeWare/MSI'
$checked = 0
foreach ($file in Get-ChildItem -LiteralPath $payload -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath($payload, $file.FullName)
    if ($relative -in @('Install-ThermalrightVibeWare.ps1', 'Uninstall-ThermalrightVibeWare.ps1')) { continue }
    $destination = Join-Path $installed $relative
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf) -or
        (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
        throw "Installed file differs from the fresh payload: $relative"
    }
    $checked++
}
if (Test-Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ThermalrightVibeWare') {
    throw 'The legacy uninstall registration is still present.'
}
$installer = New-Object -ComObject WindowsInstaller.Installer
try {
    $products = @($installer.RelatedProducts($upgradeCode))
    if ($products.Count -ne 1 -or $installer.ProductInfo($products[0], 'VersionString') -ne $version) {
        throw 'Expected exactly one MSI registration with the new version.'
    }
}
finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) }
Write-Host "Installed $version; $checked installed files match the fresh payload."
Get-FileHash -LiteralPath $msi -Algorithm SHA256 | Format-List
& "$PSScriptRoot/maintenance/Clean-BuildOutputs.ps1" -KeepMsiPath $msi
Write-Host "Finished. Kept only $msi. Installer completion choices control app launch and release news. No tests or USB commands executed by this script."
