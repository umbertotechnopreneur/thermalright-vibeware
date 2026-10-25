# VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT. VBWR E
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Source,
    [switch]$ScanNewImport
)
$ErrorActionPreference = 'Stop'
$target = (Resolve-Path -LiteralPath $Path).Path
$repo = [IO.Path]::GetFullPath((Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$artifacts = Join-Path $repo 'artifacts'
if (-not $target.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'First-import checks accept only staged dependencies under this repository artifacts folder.'
}
$files = @(if (Test-Path -LiteralPath $target -PathType Leaf) { Get-Item -LiteralPath $target }
    else { Get-ChildItem -LiteralPath $target -File -Recurse -Force })
if ($files.Count -eq 0) { throw "No staged import files were found: $target" }
if ((Get-Item -LiteralPath $target -Force).Attributes -band [IO.FileAttributes]::ReparsePoint -or
    ($files | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })) {
    throw 'First-import checks do not follow links.'
}
$evidenceRoot = Join-Path $env:LOCALAPPDATA 'ThermalrightVibeWare/build-security'
$evidencePath = Join-Path $evidenceRoot 'first-import-scans.jsonl'
$cleanHashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($recordPath in @($evidencePath, (Join-Path $repo 'docs/dependency-scan-manifest.jsonl'), (Join-Path $artifacts 'security/scans.jsonl'))) {
    if (-not (Test-Path -LiteralPath $recordPath)) { continue }
    foreach ($line in Get-Content -LiteralPath $recordPath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $previous = $line | ConvertFrom-Json
        if ($previous.result -ne 'Completed without detections (exit 0)') { continue }
        foreach ($file in $previous.files) { [void]$cleanHashes.Add($file.sha256) }
    }
}
$pending = @($files | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    if (-not $cleanHashes.Contains($hash)) { [pscustomobject]@{ path = $_.FullName; sha256 = $hash } }
})
if ($pending.Count -eq 0) {
    Write-Host "Reusing first-import scan evidence for $($files.Count) unchanged files: $target"
    return
}
if (-not $ScanNewImport) {
    $pending | Select-Object -First 20 | ForEach-Object {
        Write-Host "Missing evidence: $([IO.Path]::GetRelativePath($target, $_.path)) ($($_.sha256))"
    }
    throw "First-import evidence is missing for $($pending.Count) files in $target. Explicitly stage and scan them with Assert-DefenderClean.ps1 -ScanNewImport before building."
}
$status = Get-MpComputerStatus
if (-not $status.AntivirusEnabled) { throw 'Microsoft Defender is unavailable or disabled.' }
$scanner = Join-Path $env:ProgramData 'Microsoft/Windows Defender/Platform/CurrentVersion/MpCmdRun.exe'
if (-not (Test-Path -LiteralPath $scanner)) { throw 'Microsoft Defender command-line scanner is unavailable.' }
# Scan only content lacking clean evidence, never the installed toolchain or outputs.
$scanTarget = Join-Path $artifacts ('import-scan/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scanTarget -Force | Out-Null
foreach ($file in $pending) {
    $relative = if (Test-Path -LiteralPath $target -PathType Leaf) { [IO.Path]::GetFileName($target) }
        else { [IO.Path]::GetRelativePath($target, $file.path) }
    $staged = Join-Path $scanTarget $relative
    New-Item -ItemType Directory -Path (Split-Path $staged -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.path -Destination $staged
    if ((Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash -ne $file.sha256) {
        throw "The staged import changed while copying: $($file.path)"
    }
}
Write-Host "Defender first import: $Source; $($pending.Count) new files staged at $scanTarget"
& $scanner -Scan -ScanType 3 -File $scanTarget -DisableRemediation
if ($LASTEXITCODE -ne 0) { throw "Defender did not complete cleanly for $scanTarget (exit $LASTEXITCODE). Import stopped." }
$record = @{
    utc = [DateTime]::UtcNow.ToString('o'); source = $Source; target = $target
    command = "MpCmdRun.exe -Scan -ScanType 3 -File `"$scanTarget`" -DisableRemediation"
    result = 'Completed without detections (exit 0)'
    files = $pending
}
New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null
$record | ConvertTo-Json -Depth 5 -Compress | Add-Content -LiteralPath $evidencePath
