# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: Assert-DefenderClean.ps1
# Purpose: Scans staged first imports and verifies unchanged files within the current process.
#
# VibeWare: Human intent, AI execution, and plenty of tokens
# Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
#
# Modified with AI: OpenAI Codex; in-memory first-import scan gating; 2026-11-08.
# Human guidance: Umberto Giacobbi; maintainability and standard file headers.
#
# Copyright (c) 2026 Umberto Giacobbi
# SPDX-License-Identifier: MIT
#
# VBWR E

#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.HashSet[string]]$ImportSession,
    [switch]$ScanNewImport
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Microsoft Defender first-import checks require Windows.' }

$resolved = Resolve-Path -LiteralPath $Path
if ($resolved.Provider.Name -ne 'FileSystem') { throw 'A staged filesystem path is required.' }
$target = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($resolved.ProviderPath))
$repo = [IO.Path]::GetFullPath((Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$artifacts = Join-Path $repo 'artifacts'
$artifactPrefix = $artifacts + [IO.Path]::DirectorySeparatorChar
if (-not $target.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'First-import checks accept only staged dependencies under this repository artifacts folder.'
}

# Reject redirected staging ancestors and inspect each directory before entering it.
for ($directory = Split-Path $target -Parent; ; $directory = Split-Path $directory -Parent) {
    if ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'First-import checks do not follow links.'
    }
    if ($directory -eq $artifacts) { break }
}
function Get-StagedImportFile([IO.FileSystemInfo]$Root) {
    $items = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
    $items.Push($Root)
    while ($items.Count -gt 0) {
        $item = $items.Pop()
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'First-import checks do not follow links.'
        }
        if ($item -is [IO.DirectoryInfo]) {
            foreach ($child in Get-ChildItem -LiteralPath $item.FullName -Force) { $items.Push($child) }
            continue
        }
        $item
    }
}

$rootItem = Get-Item -LiteralPath $target -Force
$imports = @(Get-StagedImportFile $rootItem | ForEach-Object {
    [pscustomobject]@{ Path = $_.FullName; Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
if ($imports.Count -eq 0) { throw "No staged import files were found: $target" }
$pending = @($imports | Where-Object { -not $ImportSession.Contains($_.Hash) })
if ($pending.Count -eq 0) { return }
if (-not $ScanNewImport) {
    throw "The current import session has no clean Defender result for $($pending.Count) files in $target. Stage and scan them explicitly before building."
}

$status = Get-MpComputerStatus
if (-not $status.AntivirusEnabled -or -not $status.AMServiceEnabled) {
    throw 'Microsoft Defender is unavailable or disabled.'
}
$platform = Join-Path $env:ProgramData 'Microsoft/Windows Defender/Platform'
$scanner = Get-ChildItem -LiteralPath $platform -Directory |
    Where-Object { ($_.Name -split '-', 2)[0] -eq $status.AMProductVersion } |
    Sort-Object -Property Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'MpCmdRun.exe' } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1
if (-not $scanner) { throw 'The active Microsoft Defender command-line scanner is unavailable.' }

# Give the scanner only new content. Keep its command-local temporary files outside the scan target.
$scanRoot = Join-Path $artifacts ('defender-import-' + [Guid]::NewGuid().ToString('N'))
$scanTarget = Join-Path $scanRoot 'content'
$scanTemp = Join-Path $scanRoot 'scratch'
New-Item -ItemType Directory -Path $scanRoot | Out-Null
try {
    New-Item -ItemType Directory -Path $scanTarget, $scanTemp | Out-Null
    $copies = @(foreach ($file in $pending) {
        $relative = if ($rootItem -is [IO.DirectoryInfo]) { [IO.Path]::GetRelativePath($target, $file.Path) }
            else { [IO.Path]::GetFileName($target) }
        $staged = Join-Path $scanTarget $relative
        New-Item -ItemType Directory -Path (Split-Path $staged -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.Path -Destination $staged
        if ((Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash -ne $file.Hash) {
            throw "The staged import changed while copying: $($file.Path)"
        }
        [pscustomobject]@{ Path = $staged; Hash = $file.Hash }
    })

    Write-Verbose "Checking staged inputs from $Source with Microsoft Defender $($status.AMProductVersion)."
    $scanInfo = [Diagnostics.ProcessStartInfo]::new($scanner)
    $scanInfo.UseShellExecute = $false
    $scanInfo.CreateNoWindow = $true
    $scanInfo.WorkingDirectory = $scanTemp
    $scanInfo.RedirectStandardOutput = $true
    $scanInfo.RedirectStandardError = $true
    $scanInfo.Environment['TEMP'] = $scanTemp
    $scanInfo.Environment['TMP'] = $scanTemp
    foreach ($argument in @('-Scan', '-ScanType', '3', '-File', $scanTarget, '-DisableRemediation')) {
        $scanInfo.ArgumentList.Add($argument)
    }
    $scanProcess = [Diagnostics.Process]::Start($scanInfo)
    try {
        $scanOutput = $scanProcess.StandardOutput.ReadToEndAsync()
        $scanError = $scanProcess.StandardError.ReadToEndAsync()
        $scanProcess.WaitForExit()
        [void]$scanOutput.GetAwaiter().GetResult()
        [void]$scanError.GetAwaiter().GetResult()
        # With remediation disabled, Defender's exit 0 requires a clean custom scan.
        if ($scanProcess.ExitCode -ne 0) {
            throw "Microsoft Defender reported a detection or could not complete the import scan (exit $($scanProcess.ExitCode))."
        }
    }
    finally { $scanProcess.Dispose() }

    if (@(Get-StagedImportFile (Get-Item -LiteralPath $target -Force)).Count -ne $imports.Count) {
        throw "The staged import contents changed during the scan: $target"
    }
    foreach ($file in @($imports + $copies)) {
        if ((Get-FileHash -LiteralPath $file.Path -Algorithm SHA256).Hash -ne $file.Hash) {
            throw "The staged import changed during the scan: $($file.Path)"
        }
    }
}
finally {
    $scanDirectory = Get-Item -LiteralPath $scanRoot -Force
    if ($scanDirectory.FullName -ne $scanRoot -or $scanDirectory.Parent.FullName -ne $artifacts -or
        ($scanDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        ($scanDirectory.Parent.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'The temporary Defender scan directory failed its containment check.'
    }
    Remove-Item -LiteralPath $scanDirectory.FullName -Recurse -Force
}

foreach ($file in $pending) { [void]$ImportSession.Add($file.Hash) }
