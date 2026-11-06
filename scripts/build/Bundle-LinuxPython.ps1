# VBWR B
# TermalRight VibeWare | Umberto Giacobbi | SPDX-License-Identifier: MIT
# Bundle pinned Linux wheels for an explicit system Python; never install packages.
# VBWR E
#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Destination)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsLinux) { throw 'Linux Python packaging requires Linux.' }
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$interpreter = '/usr/bin/python3'
if (-not (Test-Path -LiteralPath $interpreter)) { throw 'Linux builds require /usr/bin/python3.' }
$version = & $interpreter -I -S -c 'import json, platform, sys; print(json.dumps({"major":sys.version_info.major,"minor":sys.version_info.minor,"architecture":platform.machine()}))'
if ($LASTEXITCODE -ne 0) { throw 'Could not identify the Linux Python interpreter.' }
$python = $version | ConvertFrom-Json
if ($python.major -ne 3 -or $python.minor -lt 11 -or $python.minor -gt 14 -or $python.architecture -ne 'x86_64') {
    throw 'Linux wheels currently require CPython 3.11-3.14 on x64.'
}
$tag = "cp$($python.major)$($python.minor)"
$cache = Join-Path $repo ("artifacts/python-linux/$tag/" + [guid]::NewGuid().ToString('N'))
$download = Join-Path $cache 'wheels'
$packages = Join-Path $cache 'site-packages'
$runtime = Join-Path ([IO.Path]::GetFullPath($Destination)) 'engine/python'
New-Item -ItemType Directory -Path $download -Force | Out-Null
    New-Item -ItemType Directory -Path $packages -Force | Out-Null
    $wheels = @()
    foreach ($line in Get-Content -LiteralPath "$repo/theme-runtime/requirements.txt") {
        $pin = $line.Trim()
        if (-not $pin -or $pin.StartsWith('#')) { continue }
        if ($pin -notmatch '^([A-Za-z0-9_-]+)==([A-Za-z0-9.]+)$') { throw "Unpinned Python dependency: $pin" }
        $name, $release = $Matches[1], $Matches[2]
        $metadata = Invoke-RestMethod "https://pypi.org/pypi/$name/$release/json" -TimeoutSec 30
        $compatible = @($metadata.urls | Where-Object {
            $_.filename -match "-$tag-$tag-.*manylinux_2_28_x86_64\.whl$" -or $_.filename -match '-py3-none-any\.whl$'
        })
        if ($compatible.Count -ne 1) { throw "Expected one compatible pinned Linux wheel for $pin and $tag." }
        $wheel = $compatible[0]
        if ($wheel.size -le 0 -or $wheel.size -gt 200MB -or $wheel.url -notlike 'https://files.pythonhosted.org/*' -or [IO.Path]::GetFileName($wheel.filename) -ne $wheel.filename) {
            throw "Invalid wheel metadata for $pin."
        }
        $path = Join-Path $download $wheel.filename
        if (-not (Test-Path -LiteralPath $path)) { Invoke-WebRequest $wheel.url -OutFile $path -TimeoutSec 120 }
        if ((Get-Item -LiteralPath $path).Length -ne $wheel.size -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $wheel.digests.sha256) {
            throw "Linux wheel SHA-256 or size mismatch: $($wheel.filename)"
        }
        $wheels += $path
    }
    if ($wheels.Count -eq 0) { throw 'No pinned Linux Python dependencies were found.' }
    & clamscan --recursive --infected --no-summary $download
    if ($LASTEXITCODE -ne 0) { throw 'Linux Python archive scan failed or the scanner is unavailable.' }
    foreach ($wheel in $wheels) { [IO.Compression.ZipFile]::ExtractToDirectory($wheel, $packages) }
    & clamscan --recursive --infected --no-summary $packages
    if ($LASTEXITCODE -ne 0) { throw 'Linux Python extracted dependency scan failed.' }
if (Test-Path -LiteralPath $runtime) { Remove-Item -LiteralPath $runtime -Recurse -Force }
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
Copy-Item -LiteralPath $packages -Destination $runtime -Recurse
@{ version = 1; major = $python.major; minor = $python.minor; architecture = 'x64' } |
    ConvertTo-Json | Set-Content -LiteralPath "$runtime/linux-runtime.json" -Encoding utf8NoBOM
Write-Host "Bundled Linux Python $($python.major).$($python.minor) wheels; interpreter: $interpreter."
