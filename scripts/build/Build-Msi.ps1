# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: Build-Msi.ps1
# Purpose: Packages the supplied source-built payload into a visible Windows MSI.
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
param(
    [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.HashSet[string]]$ImportSession,
    [ValidateSet('x64', 'ARM64')][string]$Architecture = 'x64',
    [string]$PayloadPath,
    [string]$Version,
    [switch]$PrepareImports
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'WiX imports and Windows MSI packaging require Windows.' }
if (-not $PrepareImports -and [string]::IsNullOrWhiteSpace($PayloadPath)) { throw 'Specify -PayloadPath when packaging the MSI.' }
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$toolRoot = Join-Path $repo 'artifacts/tools/wix4/imports'
$wixVersion = '4.0.6'
$scan = Join-Path $PSScriptRoot '../maintenance/Assert-DefenderClean.ps1'
foreach ($id in @('wix', 'wixtoolset.ui.wixext')) {
    $name = "$id.$wixVersion"
    $archive = Join-Path $toolRoot "$name.nupkg"
    $sourceUrl = "https://api.nuget.org/v3-flatcontainer/$id/$wixVersion/$name.nupkg"
    $extracted = Join-Path $toolRoot $name
    if (-not (Test-Path -LiteralPath $archive)) {
        if (-not $PrepareImports) { throw "Missing staged MSI dependency: $name." }
        New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
        $partial = "$archive.$([guid]::NewGuid().ToString('N')).part"
        try {
            Invoke-WebRequest -Uri $sourceUrl -OutFile $partial
            Move-Item -LiteralPath $partial -Destination $archive
        } finally {
            if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }
        }
    }
    & $scan -Path $archive -Source $sourceUrl -ImportSession $ImportSession -ScanNewImport:$PrepareImports
    if ($PrepareImports -and -not (Test-Path -LiteralPath $extracted)) {
        $temporary = "$extracted.$([guid]::NewGuid().ToString('N')).stage"
        [IO.Compression.ZipFile]::ExtractToDirectory($archive, $temporary)
        Move-Item -LiteralPath $temporary -Destination $extracted
    }
    & $scan -Path $extracted -Source "Extracted $name from NuGet" -ImportSession $ImportSession -ScanNewImport:$PrepareImports
    $specs = @(Get-ChildItem -LiteralPath $extracted -Filter '*.nuspec' -File)
    if ($specs.Count -ne 1) { throw "Missing NuGet package metadata: $name." }
    $metadata = ([xml](Get-Content -LiteralPath $specs[0].FullName -Raw)).package.metadata
    if ($metadata.id -ne $id -or $metadata.version -ne $wixVersion) { throw "Unexpected NuGet package identity: $name." }
}
$wix = Join-Path $toolRoot "wix.$wixVersion/tools/net6.0/any/wix.dll"
$uiExtension = Join-Path $toolRoot "wixtoolset.ui.wixext.$wixVersion/wixext4/WixToolset.UI.wixext.dll"
if (-not (Test-Path -LiteralPath $wix -PathType Leaf) -or -not (Test-Path -LiteralPath $uiExtension -PathType Leaf)) {
    throw 'The staged WiX tool or UI extension is missing.'
}
if ($PrepareImports) { return }

if (-not $Version) { $Version = (Get-Content (Join-Path $repo 'installer/Version.txt') -Raw).Trim() }
$payload = (Resolve-Path -LiteralPath $PayloadPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $payload 'ThermalrightVibeWare.exe'))) { throw 'The published studio is missing.' }
& (Join-Path $PSScriptRoot 'Build-SetupActions.ps1') -Architecture $Architecture
$setupHelper = Join-Path $repo "artifacts/native/msi/$($Architecture.ToLowerInvariant())/ThermalrightVibeWare.SetupActions.exe"
if (-not (Test-Path -LiteralPath $setupHelper)) { throw 'The native installer cleanup helper is missing.' }
$generated = Join-Path $repo ('artifacts/msi/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $generated -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $generated 'ThermalrightVibeWare.cmd'), "@echo off`r`nstart `"`" `"%~dp0MSI\ThermalrightVibeWare.exe`" %*`r`n", [Text.Encoding]::ASCII)
$namespace = 'http://wixtoolset.org/schemas/v4/wxs'
$document = [Xml.XmlDocument]::new()
<#
.SYNOPSIS
Appends a WiX XML element with the supplied attributes.

.PARAMETER Parent
Parent XML node receiving the new WiX element.

.PARAMETER Name
WiX XML element name.

.PARAMETER Attributes
XML attribute names and values assigned to the new element.
#>
function Add-Element($Parent, [string]$Name, [hashtable]$Attributes) {
    $element = $document.CreateElement($Name, $namespace)
    foreach ($attribute in $Attributes.GetEnumerator()) { $element.SetAttribute($attribute.Key, [string]$attribute.Value) }
    [void]$Parent.AppendChild($element)
    return $element
}
<#
.SYNOPSIS
Derives a stable legal WiX identifier from a relative payload path.

.PARAMETER Text
Source text or payload-relative path processed by this helper.
#>
function Get-Identifier([string]$Text) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text.ToLowerInvariant()))).Substring(0, 32)
}
$root = Add-Element $document 'Wix' @{}
$fragment = Add-Element $root 'Fragment' @{}
$directoryRoot = Add-Element $fragment 'DirectoryRef' @{ Id = 'INSTALLFOLDER' }
$group = Add-Element $fragment 'ComponentGroup' @{ Id = 'Payload' }
$directories = @{ '' = $directoryRoot }
foreach ($file in (Get-ChildItem -LiteralPath $payload -File -Recurse | Sort-Object FullName)) {
    $relative = [IO.Path]::GetRelativePath($payload, $file.FullName)
    if ($relative -in @('Install-ThermalrightVibeWare.ps1', 'Uninstall-ThermalrightVibeWare.ps1')) { continue }
    if ($file.Name -eq 'OpenHardwareMonitorLib.sys') { throw 'A live sensor driver must never be harvested into the MSI payload.' }
    $folder = [IO.Path]::GetDirectoryName($relative)
    $current = ''
    $parent = $directoryRoot
    foreach ($part in ($folder -split '[\\/]' | Where-Object { $_ })) {
        $current = if ($current) { "$current/$part" } else { $part }
        if (-not $directories.ContainsKey($current)) {
            $directories[$current] = Add-Element $parent 'Directory' @{ Id = 'D' + (Get-Identifier $current); Name = $part }
        }
        $parent = $directories[$current]
    }
    $id = Get-Identifier $relative
    $guidBytes = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes("ThermalrightVibeWare.$Architecture.$relative"))
    $component = Add-Element $parent 'Component' @{ Id = "C$id"; Guid = [Guid]::new([byte[]]$guidBytes[0..15]).ToString(); Bitness = 'always64' }
    Add-Element $component 'File' @{ Id = "F$id"; Source = $file.FullName; Name = $file.Name } | Out-Null
    Add-Element $component 'RegistryValue' @{ Root = 'HKCU'; Key = 'Software\Umberto Giacobbi.biz\ThermalrightVibeWare\MSI\Files'; Name = "C$id"; Type = 'integer'; Value = '1'; KeyPath = 'yes' } | Out-Null
    Add-Element $group 'ComponentRef' @{ Id = "C$id" } | Out-Null
}
$source = Join-Path $generated 'Payload.wxs'
$document.Save($source)
$revision = (git -C $repo rev-parse --short HEAD).Trim()
$output = Join-Path $repo "artifacts/packages/ThermalrightVibeWare-win-$($Architecture.ToLowerInvariant())-$Version.msi"
# Pass the verified extension path so WiX does not consult its extension cache.
& dotnet --roll-forward Major $wix build (Join-Path $repo 'installer/Package.wxs') $source -arch $Architecture.ToLowerInvariant() -ext $uiExtension -culture en-US -d "PayloadRoot=$payload" -d "GeneratedRoot=$generated" -d "InstallerRoot=$(Join-Path $repo 'installer')" -d "SetupHelper=$setupHelper" -d "SetupGate=$(Join-Path (Split-Path $setupHelper) 'ThermalrightVibeWare.SetupGate.dll')" -d "ProductVersion=$Version" -d "SourceRevision=$revision" -intermediatefolder (Join-Path $generated 'intermediate') -pdbtype none -out $output
if ($LASTEXITCODE -ne 0) { throw 'MSI compilation failed.' }
Write-Host "MSI: $output"
