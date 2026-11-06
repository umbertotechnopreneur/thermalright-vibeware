# VBWR B
# TermalRight VibeWare — Umberto Giacobbi
# SPDX-License-Identifier: MIT
# Internal build operations. Invoked by vibeware.ps1.
# VBWR E

$script:ScriptsRoot = Split-Path $PSScriptRoot -Parent

function Invoke-Package {
    param([string]$Architecture, [string]$HostPython = 'python', [string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$repo = Split-Path $script:ScriptsRoot -Parent
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
& "$script:ScriptsRoot/maintenance/Assert-DefenderClean.ps1" -Path $env:NUGET_PACKAGES -Source 'https://api.nuget.org/v3/index.json; restored package archives and extracted dependencies'
foreach ($target in @(@{ Project = 'Cli'; Framework = 'net10.0-windows10.0.19041.0'; Folder = $engine }, @{ Project = 'Photino'; Framework = 'net10.0'; Folder = $destination })) {
    $project = Join-Path $repo "src/ThermalrightVibeWare.$($target.Project)/ThermalrightVibeWare.$($target.Project).csproj"
    dotnet publish $project --framework $target.Framework --no-restore --disable-build-servers --configuration $Configuration --runtime $runtime --self-contained true -p:Platform=$Architecture --output $target.Folder --warnaserror
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($target.Project) / $runtime" }
}
& (Join-Path $script:ScriptsRoot 'build/Bundle-Python.ps1') -Architecture $Architecture -Destination $destination -HostPython $HostPython
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
$archive = Join-Path $packages "$packageName.zip"
Compress-Archive -LiteralPath $destination -DestinationPath $archive -Force
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List
Write-Host "Portable ZIP: $archive. Staging: $destination. No tests or USB commands executed."

}

function Invoke-Reinstall {
    param([string]$Architecture)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path $script:ScriptsRoot -Parent
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
& "$script:ScriptsRoot/install/Uninstall-Local.ps1"
& "$script:ScriptsRoot/maintenance/Clean-BuildOutputs.ps1"
if ($previous.Build -ge 65535) { throw 'MSI build version limit reached. Update major/minor explicitly.' }
$version = '{0}.{1}.{2}' -f $previous.Major, $previous.Minor, ($previous.Build + 1)
# Reserve the version before building. Retries never reuse an installed version.
Set-Content -LiteralPath $versionFile -Value $version -Encoding ascii
Write-Host "Building Thermalright VibeWare $version from clean output folders."
Invoke-Package -Architecture $Architecture -Configuration Debug
$payloads = @(Get-ChildItem -LiteralPath (Join-Path $repo 'artifacts/staging') -Directory)
if ($payloads.Count -ne 1) { throw 'Expected exactly one fresh payload.' }
$payload = $payloads[0].FullName
& "$script:ScriptsRoot/build/Build-Msi.ps1" -PayloadPath $payload -Architecture $Architecture -Version $version
$msi = Join-Path $repo "artifacts/packages/ThermalrightVibeWare-win-$($Architecture.ToLowerInvariant())-$version.msi"
$process = Start-Process msiexec.exe -ArgumentList @('/i', ('"' + $msi + '"'), '/norestart') -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Install failed or cancelled (exit $($process.ExitCode)). MSI retained." }
Write-Host "Installed $version; Windows Installer completed successfully."
& "$script:ScriptsRoot/maintenance/Clean-BuildOutputs.ps1" -KeepMsiPath $msi
Write-Host "Finished. Kept only $msi. Installer completion choices control app launch and release news. No tests or USB commands executed by this script."

}

Export-ModuleMember -Function Invoke-Package, Invoke-Reinstall
