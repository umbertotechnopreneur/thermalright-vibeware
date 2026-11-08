# VBWR B
# TermalRight VibeWare — Umberto Giacobbi
# SPDX-License-Identifier: MIT
# Internal build operations. Invoked by vibeware.ps1.
# VBWR E

$script:ScriptsRoot = Split-Path $PSScriptRoot -Parent
$script:RepoRoot = Split-Path $script:ScriptsRoot -Parent
$script:NuGetSource = 'https://api.nuget.org/v3/index.json'

function Assert-WindowsImportPrerequisites {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $principal = [Security.Principal.WindowsPrincipal]::new($identity)
        if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            throw 'Open PowerShell as administrator to run the targeted Microsoft Defender import scans.'
        }
    }
    finally { $identity.Dispose() }
    $status = Get-MpComputerStatus -ErrorAction Stop
    if (-not $status.AntivirusEnabled -or -not $status.AMServiceEnabled) {
        throw 'Microsoft Defender is unavailable or disabled. Windows import preparation stopped.'
    }
    $platform = Join-Path $env:ProgramData 'Microsoft/Windows Defender/Platform'
    $scanner = Get-ChildItem -LiteralPath $platform -Directory -ErrorAction SilentlyContinue |
        Where-Object { ($_.Name -split '-', 2)[0] -eq $status.AMProductVersion -and (Test-Path -LiteralPath (Join-Path $_.FullName 'MpCmdRun.exe') -PathType Leaf) } |
        Select-Object -First 1
    if (-not $scanner) { throw 'The active Microsoft Defender command-line scanner is unavailable.' }
}

function Initialize-WindowsBuildImports {
    param(
        [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Architecture,
        [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.HashSet[string]]$ImportSession,
        [string]$HostPython = 'python',
        [switch]$IncludeSolution,
        [switch]$IncludeMsi
    )
    $ErrorActionPreference = 'Stop'
    Assert-WindowsImportPrerequisites
    $runtime = 'win-' + $Architecture.ToLowerInvariant()
    $env:NUGET_PACKAGES = Join-Path $script:RepoRoot 'artifacts/nuget-stage'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $script:RepoRoot 'artifacts/nuget-http'
    if (Test-Path -LiteralPath $env:NUGET_PACKAGES) {
        throw 'The NuGet import stage already exists. Clean the generated folders before staging a new Windows build.'
    }

    Write-Host 'Staging Windows build imports for targeted Microsoft Defender checks.'
    $verifyRestore = @{
        Path = $env:NUGET_PACKAGES
        Source = $script:NuGetSource
        ImportSession = $ImportSession
        ScanNewImport = $true
    }
    if ($IncludeSolution) {
        dotnet restore (Join-Path $script:RepoRoot 'ThermalrightVibeWare.slnx') --source $script:NuGetSource -p:Platform=$Architecture
        if ($LASTEXITCODE -ne 0) { throw 'Solution restore failed.' }
        & "$script:ScriptsRoot/maintenance/Assert-DefenderClean.ps1" @verifyRestore
        if ($Architecture -eq 'x64') {
            $checks = Join-Path $script:RepoRoot 'tests/ThermalrightVibeWare.StudioChecks/ThermalrightVibeWare.StudioChecks.csproj'
            dotnet restore $checks --source $script:NuGetSource
            if ($LASTEXITCODE -ne 0) { throw 'Studio checks restore failed.' }
            & "$script:ScriptsRoot/maintenance/Assert-DefenderClean.ps1" @verifyRestore
        }
    }
    foreach ($name in @('Cli', 'Photino')) {
        $project = Join-Path $script:RepoRoot "src/ThermalrightVibeWare.$name/ThermalrightVibeWare.$name.csproj"
        $restore = @('restore', $project, '--source', $script:NuGetSource, '--runtime', $runtime, "-p:Platform=$Architecture", '-p:SelfContained=true')
        if (-not $IncludeSolution) { $restore += '-p:NuGetAudit=false' }
        dotnet @restore
        if ($LASTEXITCODE -ne 0) { throw "Restore failed: $name" }
        # A later restore may evaluate the imported package's MSBuild targets.
        & "$script:ScriptsRoot/maintenance/Assert-DefenderClean.ps1" @verifyRestore
    }
    & "$script:ScriptsRoot/build/Build-PhotinoNative.ps1" -Architecture $Architecture -ImportSession $ImportSession -PrepareImports
    & "$script:ScriptsRoot/build/Bundle-Python.ps1" -Architecture $Architecture -HostPython $HostPython -ImportSession $ImportSession -PrepareImports
    if ($IncludeMsi) {
        & "$script:ScriptsRoot/build/Build-Msi.ps1" -Architecture $Architecture -ImportSession $ImportSession -PrepareImports
    }
}

function Invoke-Package {
    param(
        [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Architecture,
        [Parameter(Mandatory)][Collections.Generic.HashSet[string]]$ImportSession,
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
        [switch]$SkipArchive
    )
$ErrorActionPreference = 'Stop'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$repo = $script:RepoRoot
$runtime = 'win-' + $Architecture.ToLowerInvariant()
$packageName = "ThermalrightVibeWare-$runtime-preview"
$stagingRoot = Join-Path $repo 'artifacts/staging'
$destination = Join-Path $stagingRoot ("$packageName-" + [Guid]::NewGuid().ToString('N'))
$engine = Join-Path $destination 'engine'
$nugetStage = Join-Path $repo 'artifacts/nuget-stage'
if ($env:NUGET_PACKAGES -ne $nugetStage) { throw 'Run Windows import preparation in this process before packaging.' }
& "$script:ScriptsRoot/maintenance/Assert-DefenderClean.ps1" -Path $nugetStage -Source $script:NuGetSource -ImportSession $ImportSession
foreach ($target in @(@{ Project = 'Cli'; Framework = 'net10.0-windows10.0.19041.0'; Folder = $engine }, @{ Project = 'Photino'; Framework = 'net10.0'; Folder = $destination })) {
    $project = Join-Path $repo "src/ThermalrightVibeWare.$($target.Project)/ThermalrightVibeWare.$($target.Project).csproj"
    dotnet publish $project --framework $target.Framework --no-restore --disable-build-servers --configuration $Configuration --runtime $runtime --self-contained true -p:Platform=$Architecture --output $target.Folder --warnaserror
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($target.Project) / $runtime" }
}
& "$script:ScriptsRoot/build/Bundle-Python.ps1" -Architecture $Architecture -Destination $destination -ImportSession $ImportSession
Copy-Item -LiteralPath (Join-Path $script:ScriptsRoot 'install/Install-Local.ps1') -Destination (Join-Path $destination 'Install-ThermalrightVibeWare.ps1') -Force
Copy-Item -LiteralPath (Join-Path $script:ScriptsRoot 'install/Uninstall-Local.ps1') -Destination (Join-Path $destination 'Uninstall-ThermalrightVibeWare.ps1') -Force
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
if (-not $SkipArchive) {
    $archive = Join-Path $packages "$packageName.zip"
    Compress-Archive -LiteralPath $destination -DestinationPath $archive -Force
    Write-Host "Portable ZIP: $archive"
}
Write-Host "Staging: $destination"

}

function Invoke-Reinstall {
    param(
        [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Architecture,
        [Parameter(Mandatory)][Collections.Generic.HashSet[string]]$ImportSession,
        [Parameter(Mandatory)][ref]$CompletedMsiPath
    )
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = $script:RepoRoot
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
    if ($previous.Build -ge 65535) { throw 'MSI build version limit reached. Update major/minor explicitly.' }
    $version = '{0}.{1}.{2}' -f $previous.Major, $previous.Minor, ($previous.Build + 1)
    Write-Host "Building Thermalright VibeWare $version from current source."
    Invoke-Package -Architecture $Architecture -Configuration Debug -ImportSession $ImportSession -SkipArchive
    $payloads = @(Get-ChildItem -LiteralPath (Join-Path $repo 'artifacts/staging') -Directory)
    if ($payloads.Count -ne 1) { throw 'Expected exactly one fresh payload.' }
    & "$script:ScriptsRoot/build/Build-Msi.ps1" -PayloadPath $payloads[0].FullName -Architecture $Architecture -Version $version -ImportSession $ImportSession
    $msi = Join-Path $repo "artifacts/packages/ThermalrightVibeWare-win-$($Architecture.ToLowerInvariant())-$version.msi"
    if (-not (Test-Path -LiteralPath $msi -PathType Leaf)) { throw 'The source-built MSI is missing. The existing installation was preserved.' }
    $CompletedMsiPath.Value = $msi

    foreach ($product in @($installer.RelatedProducts($upgradeCode))) {
        Write-Host "Uninstalling Thermalright VibeWare $product with visible Windows Installer UI."
        $process = Start-Process msiexec.exe -ArgumentList @('/x', $product, '/norestart') -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw "Uninstall failed or cancelled (exit $($process.ExitCode)). Reinstall stopped." }
    }
    if (@($installer.RelatedProducts($upgradeCode)).Count -ne 0) { throw 'A previous MSI is still registered.' }
}
finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) }
& "$script:ScriptsRoot/install/Uninstall-Local.ps1"
# Reserve the completed version before running the visible installer.
Set-Content -LiteralPath $versionFile -Value $version -Encoding ascii
$process = Start-Process msiexec.exe -ArgumentList @('/i', ('"' + $msi + '"'), '/norestart') -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Install failed or cancelled (exit $($process.ExitCode)). MSI retained: $msi" }
Write-Host "Installed $version; Windows Installer completed successfully."
Write-Host 'Installer completion choices control app launch and release news.'

}

Export-ModuleMember -Function Assert-WindowsImportPrerequisites, Initialize-WindowsBuildImports, Invoke-Package, Invoke-Reinstall
