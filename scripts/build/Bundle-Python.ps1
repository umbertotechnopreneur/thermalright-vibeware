# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: Bundle-Python.ps1
# Purpose: Stages approved Python/runtime dependencies for the source-built distribution.
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
    [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Architecture,
    [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.HashSet[string]]$ImportSession,
    [string]$Destination,
    [string]$HostPython = 'python',
    [switch]$PrepareImports
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Windows Python imports require Windows.' }
if (-not $PrepareImports -and [string]::IsNullOrWhiteSpace($Destination)) { throw 'Specify -Destination when bundling Python.' }

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$version = '3.13.16'
$pythonTag = '313'
# Archive digests from https://www.python.org/ftp/python/3.13.16/windows-3.13.16.json.
$architectureTag = if ($Architecture -eq 'ARM64') { 'arm64' } else { 'amd64' }
$platformTag = if ($Architecture -eq 'ARM64') { 'win_arm64' } else { 'win_amd64' }
$expectedPythonSha256 = if ($Architecture -eq 'ARM64') {
    '68ce5ef796aa68dca7661d47164b0a6232a5f3d14f81ae2736150155c3ea8574'
} else {
    '589dc1e9d02549ca5f680710307ff9b77f6e3ffc4aa3efccd7fa54eeeafac94b'
}
$requirements = Join-Path $repo 'theme-runtime/requirements.txt'
$requirementsSha256 = (Get-FileHash -LiteralPath $requirements -Algorithm SHA256).Hash.ToLowerInvariant()
$cache = Join-Path $repo "artifacts/python-cache/$version/$architectureTag/$requirementsSha256"
$pythonArchiveName = "python-$version-embeddable-$architectureTag.zip"
$pythonArchive = Join-Path $cache $pythonArchiveName
$wheelCache = Join-Path $cache 'wheels'
$preparedPython = Join-Path $cache 'python'
$scan = Join-Path $PSScriptRoot '../maintenance/Assert-DefenderClean.ps1'

<#
.SYNOPSIS
Verifies an input file against its pinned SHA-256 before use.

.PARAMETER Path
Literal local path being validated, read, written or removed by this helper.

.PARAMETER Expected
Pinned SHA-256 expected for this exact dependency input.
#>
function Assert-Hash([string]$Path, [string]$Expected) {
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Expected.ToLowerInvariant()) {
        throw "SHA-256 mismatch: $Path (expected $Expected, got $actual)"
    }
}

<#
.SYNOPSIS
Reads exact Python name/version pins and rejects unpinned requirements.

.PARAMETER Path
Literal local path being validated, read, written or removed by this helper.
#>
function Read-PinnedRequirements([string]$Path) {
    $pins = @()
    foreach ($line in Get-Content -LiteralPath $Path) {
        $value = $line.Trim()
        if (-not $value -or $value.StartsWith('#')) { continue }
        if ($value -notmatch '^([A-Za-z0-9][A-Za-z0-9._-]*)==([A-Za-z0-9][A-Za-z0-9.+_-]*)$') {
            throw "Portable requirements must be exact name==version pins: $value"
        }
        $pins += [pscustomobject]@{ Name = $Matches[1]; Version = $Matches[2] }
    }
    if ($pins.Count -eq 0) { throw 'No pinned Python requirements were found.' }
    return $pins
}

<#
.SYNOPSIS
Extracts an approved wheel while rejecting entries outside the Python root.

.PARAMETER Wheel
Approved local wheel archive whose entries are checked before extraction.

.PARAMETER PythonRoot
Destination Python root; archive entries must remain inside its install scheme.
#>
function Expand-Wheel([string]$Wheel, [string]$PythonRoot) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $pythonRootPath = [System.IO.Path]::GetFullPath($PythonRoot)
    $siteRoot = Join-Path $pythonRootPath 'Lib/site-packages'
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Wheel)
    try {
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            if ($name.StartsWith('/') -or @($name.Split('/')) -contains '..' -or $name -match '^[A-Za-z]:') {
                throw "Unsafe wheel entry in $Wheel`: $name"
            }
            $installRoot = $siteRoot
            if ($name -match '^[^/]+\.data/(purelib|platlib|headers|scripts|data)/(.+)$') {
                $installRoot = switch ($Matches[1]) {
                    'purelib' { $siteRoot }
                    'platlib' { $siteRoot }
                    'headers' { Join-Path $pythonRootPath 'Include' }
                    'scripts' { Join-Path $pythonRootPath 'Scripts' }
                    'data' { $pythonRootPath }
                }
                $name = $Matches[2]
            } elseif ($name -match '^[^/]+\.data/') {
                if ($name.EndsWith('/')) { continue }
                throw "Unknown wheel installation scheme in $Wheel`: $name"
            }
            if (-not $name -or $name.EndsWith('/')) { continue }
            $separator = [string][System.IO.Path]::DirectorySeparatorChar
            $installRootPath = [System.IO.Path]::GetFullPath($installRoot)
            $target = [System.IO.Path]::GetFullPath((Join-Path $installRootPath ($name.Replace('/', $separator))))
            if (-not $target.StartsWith($installRootPath + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Wheel entry escapes installation directory: $name"
            }
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            $inputStream = $entry.Open()
            try {
                $outputStream = [System.IO.File]::Create($target)
                try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
            } finally { $inputStream.Dispose() }
        }
    } finally { $zip.Dispose() }
}

<#
.SYNOPSIS
Copies the complete first-party theme runtime into the engine payload.

.PARAMETER Engine
Staged engine directory receiving the complete theme-runtime folder.
#>
function Copy-ThemeRuntime([string]$Engine) {
    $target = Join-Path $Engine 'theme-runtime'
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -Path (Join-Path $repo 'theme-runtime/*') -Destination $target -Recurse -Force
}

$pins = @(Read-PinnedRequirements $requirements)
$pythonUrl = "https://www.python.org/ftp/python/$version/$pythonArchiveName"
if (-not (Test-Path -LiteralPath $pythonArchive)) {
    if (-not $PrepareImports) { throw 'Python imports must be prepared before packaging.' }
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $partial = "$pythonArchive.$([guid]::NewGuid().ToString('N')).part"
    try {
        Invoke-WebRequest -Uri $pythonUrl -OutFile $partial -MaximumRedirection 3
        Assert-Hash $partial $expectedPythonSha256
        Move-Item -LiteralPath $partial -Destination $pythonArchive
    } finally {
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }
    }
}
Assert-Hash $pythonArchive $expectedPythonSha256
& $scan -Path $pythonArchive -Source $pythonUrl -ImportSession $ImportSession -ScanNewImport:$PrepareImports

if (-not (Test-Path -LiteralPath $wheelCache)) {
    if (-not $PrepareImports) { throw 'Python wheels must be prepared before packaging.' }
    $command = Get-Command $HostPython -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Python with pip is required only on the packaging PC to download wheels. Install Python or pass -HostPython <path>; ZIP users do not need it."
    }
    try { & $HostPython -m pip --version | Out-Null } catch {
        throw "Cannot run pip through '$HostPython' on the packaging PC: $_"
    }
    if ($LASTEXITCODE -ne 0) {
        throw "'$HostPython' has no working pip. Install pip on the packaging PC or pass -HostPython <path>."
    }
    $staging = "$wheelCache.$([guid]::NewGuid().ToString('N')).stage"
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    & $HostPython -m pip --isolated download --no-cache-dir --disable-pip-version-check --index-url https://pypi.org/simple --only-binary=:all: --no-deps --implementation cp --python-version 3.13 --abi cp313 --platform $platformTag --dest $staging --requirement $requirements
    if ($LASTEXITCODE -ne 0) { throw "pip could not download CPython 3.13 $platformTag wheels. Partial files remain in $staging" }
    Move-Item -LiteralPath $staging -Destination $wheelCache
}
$wheels = @(Get-ChildItem -LiteralPath $wheelCache -Filter '*.whl' -File | Sort-Object Name)
if ($wheels.Count -ne $pins.Count) { throw "Expected $($pins.Count) pinned wheels; found $($wheels.Count) in $wheelCache." }
if ($PrepareImports) {
    foreach ($pin in $pins) {
        $metadata = Invoke-RestMethod -Uri "https://pypi.org/pypi/$($pin.Name)/$($pin.Version)/json"
        $matching = @($wheels | Where-Object { $metadata.urls.filename -contains $_.Name })
        if ($matching.Count -ne 1) { throw "Expected one official PyPI wheel for $($pin.Name)==$($pin.Version) / $platformTag" }
        $file = $matching[0]
        $release = @($metadata.urls | Where-Object { $_.filename -eq $file.Name })[0]
        Assert-Hash $file.FullName $release.digests.sha256
    }
}
foreach ($wheel in $wheels) {
    & $scan -Path $wheel.FullName -Source 'https://pypi.org/; pinned wheel' -ImportSession $ImportSession -ScanNewImport:$PrepareImports
}
if ($PrepareImports -and -not (Test-Path -LiteralPath $preparedPython)) {
    $temporary = "$preparedPython.$([guid]::NewGuid().ToString('N')).stage"
    Expand-Archive -LiteralPath $pythonArchive -DestinationPath $temporary
    foreach ($wheel in $wheels) { Expand-Wheel $wheel.FullName $temporary }
    Move-Item -LiteralPath $temporary -Destination $preparedPython
}
& $scan -Path $preparedPython -Source "Extracted Python $version and pinned PyPI wheels" -ImportSession $ImportSession -ScanNewImport:$PrepareImports
if (-not (Test-Path -LiteralPath (Join-Path $preparedPython 'python.exe') -PathType Leaf)) { throw 'The staged Python executable is missing.' }
if ($PrepareImports) { return }

$destination = [IO.Path]::GetFullPath($Destination)
$engine = Join-Path $destination 'engine'
$pythonDestination = Join-Path $engine 'python'
$pythonStage = Join-Path $engine "python.$([guid]::NewGuid().ToString('N')).stage"
New-Item -ItemType Directory -Path $engine -Force | Out-Null
Copy-Item -LiteralPath $preparedPython -Destination $pythonStage -Recurse
$pth = Join-Path $pythonStage "python$pythonTag._pth"
@("python$pythonTag.zip", '.', 'Lib/site-packages', '../theme-runtime', 'import site') |
    Set-Content -LiteralPath $pth -Encoding ascii

if (Test-Path -LiteralPath $pythonDestination) {
    $artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))
    $resolvedDestination = [System.IO.Path]::GetFullPath($destination)
    $resolvedEngine = [System.IO.Path]::GetFullPath($engine)
    $resolvedPython = [System.IO.Path]::GetFullPath($pythonDestination)
    if (-not $resolvedDestination.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
        $resolvedPython -ne (Join-Path $resolvedEngine 'python')) {
        throw "Refusing to replace unsafe Python destination: $pythonDestination"
    }
    foreach ($component in @($artifactsRoot, $resolvedDestination, $resolvedEngine, $resolvedPython)) {
        $item = Get-Item -LiteralPath $component -Force
        if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Refusing to replace Python through a reparse point: $component"
        }
    }
    Remove-Item -LiteralPath $pythonDestination -Recurse -Force
}
Move-Item -LiteralPath $pythonStage -Destination $pythonDestination
Copy-ThemeRuntime $engine
Write-Host "Bundled Python $version $platformTag with $($wheels.Count) pinned PyPI wheels in $pythonDestination"
