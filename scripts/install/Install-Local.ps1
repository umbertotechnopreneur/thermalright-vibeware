# VBWR B
# Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
[CmdletBinding()]
param([switch]$DisableStartup)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-NormalPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]@('\', '/'))
}

function Assert-NoReparsePath([string]$Path) {
    $current = Get-NormalPath $Path
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "A link or junction was found at '$current'. Nothing at that path was replaced."
            }
        }
        $parent = [System.IO.Path]::GetDirectoryName($current)
        if ($parent -eq $current) { break }
        $current = $parent
    }
}

function Assert-ChildPath([string]$Path, [string]$Root) {
    $prefix = (Get-NormalPath $Root) + [System.IO.Path]::DirectorySeparatorChar
    if (-not (Get-NormalPath $Path).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The computed path '$Path' is outside '$Root'."
    }
    Assert-NoReparsePath $Path
}

function Assert-ManagedShortcut([object]$Shell, [string]$Path, [string]$Marker) {
    Assert-NoReparsePath $Path
    if (Test-Path -LiteralPath $Path) {
        $existing = $Shell.CreateShortcut($Path)
        try {
            if ($existing.Description -ne $Marker) {
                throw "Shortcut '$Path' belongs to another configuration and was left unchanged."
            }
        }
        finally { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($existing) }
    }
}

function Save-ManagedShortcut(
    [object]$Shell, [string]$Path, [string]$Marker,
    [string]$Target, [string]$Directory, [string]$Icon,
    [string]$Arguments, [int]$WindowStyle, [string]$BackupDirectory
) {
    Assert-ManagedShortcut $Shell $Path $Marker
    $pendingPath = Join-Path $BackupDirectory ("ThermalrightVibeWare-$([Guid]::NewGuid().ToString('N')).pending.lnk")
    Assert-ChildPath $pendingPath $BackupDirectory
    $pending = $Shell.CreateShortcut($pendingPath)
    try {
        $pending.TargetPath = $Target
        $pending.WorkingDirectory = $Directory
        $pending.Arguments = $Arguments
        $pending.Description = $Marker
        $pending.IconLocation = $Icon + ',0'
        $pending.WindowStyle = $WindowStyle
        $pending.Save()
    }
    finally { [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($pending) }
    if (Test-Path -LiteralPath $Path) {
        $backupPath = Join-Path $BackupDirectory ("ThermalrightVibeWare-$([Guid]::NewGuid().ToString('N')).lnk")
        Assert-ChildPath $backupPath $BackupDirectory
        [System.IO.File]::Replace($pendingPath, $Path, $backupPath)
    }
    else { Move-Item -LiteralPath $pendingPath -Destination $Path }
}

function Save-StableLauncher([string]$Path, [string]$Target, [string]$Root) {
    Assert-ChildPath $Path $Root
    Assert-ChildPath $Target $Root
    $pendingPath = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"
    Assert-ChildPath $pendingPath $Root
    try {
        [System.IO.File]::WriteAllText($pendingPath, "@echo off`r`nstart `"`" `"$Target`" %*`r`n", [Text.Encoding]::ASCII)
        [System.IO.File]::Move($pendingPath, $Path, $true)
    }
    finally {
        if (Test-Path -LiteralPath $pendingPath) { Remove-Item -LiteralPath $pendingPath -Force }
    }
}

$sourceRoot = Get-NormalPath $PSScriptRoot
$sourceExecutable = Join-Path $sourceRoot 'ThermalrightVibeWare.exe'
if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf)) {
    throw 'Run Install-ThermalrightVibeWare.ps1 from the unzipped package, next to ThermalrightVibeWare.exe.'
}

$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$roamingAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
$startupDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
$programsDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
foreach ($folder in @($localAppData, $roamingAppData, $startupDirectory, $programsDirectory)) {
    if ([string]::IsNullOrWhiteSpace($folder)) { throw 'A required current-user Windows folder is unavailable.' }
    Assert-NoReparsePath $folder
}
$installRoot = Join-Path $localAppData 'Umberto Giacobbi.biz\Apps\ThermalrightVibeWare'
$launcherPath = Join-Path $installRoot 'ThermalrightVibeWare.cmd'
$launcherIcon = Join-Path $installRoot 'ThermalrightVibeWare.ico'
$backupRoot = Join-Path $roamingAppData 'Umberto Giacobbi.biz\ThermalrightVibeWare\Startup backups'
$startupShortcut = Join-Path $startupDirectory 'ThermalrightVibeWare.lnk'
$startShortcut = Join-Path $programsDirectory 'Thermalright VibeWare.lnk'
$startupMarker = 'ThermalrightVibeWare auto-start managed by Install-ThermalrightVibeWare.ps1'
$startMarker = 'ThermalrightVibeWare Start menu managed by Install-ThermalrightVibeWare.ps1'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ThermalrightVibeWare'
Assert-ChildPath $installRoot $localAppData
Assert-ChildPath $launcherPath $installRoot
Assert-ChildPath $launcherIcon $installRoot
Assert-ChildPath $backupRoot $roamingAppData
Assert-ChildPath $startupShortcut $startupDirectory
Assert-ChildPath $startShortcut $programsDirectory
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

$shell = New-Object -ComObject WScript.Shell
try {
    Assert-ManagedShortcut $shell $startupShortcut $startupMarker
    if ($DisableStartup) {
        if (Test-Path -LiteralPath $startupShortcut) {
            $backupPath = Join-Path $backupRoot ("ThermalrightVibeWare-$([Guid]::NewGuid().ToString('N')).lnk")
            Assert-ChildPath $backupPath $backupRoot
            Move-Item -LiteralPath $startupShortcut -Destination $backupPath
            Write-Host "Automatic startup is off. The previous shortcut is saved at: $backupPath"
        }
        else { Write-Host 'Automatic startup was already off.' }
        return
    }

    Assert-ManagedShortcut $shell $startShortcut $startMarker
    if (Test-Path -LiteralPath $uninstallKey) {
        $registered = Get-ItemProperty -LiteralPath $uninstallKey
        if ($registered.PSObject.Properties.Name -notcontains 'ThermalrightVibeWareManaged' -or $registered.ThermalrightVibeWareManaged -ne 1) {
            throw 'Another ThermalrightVibeWare uninstall entry already exists and was left unchanged.'
        }
    }
    foreach ($required in @((Join-Path $sourceRoot 'Uninstall-ThermalrightVibeWare.ps1'), (Join-Path $sourceRoot 'Assets\thermalright-vibeware.ico'))) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Package file is missing: $required" }
    }
    Assert-NoReparsePath $sourceRoot
    $packageItems = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -Force)
    if ($packageItems | Where-Object { ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 } | Select-Object -First 1) {
        throw 'The package contains a link or junction. Its files were left unchanged.'
    }
    # Include DLLs, themes and assets: the native host EXE alone does not identify a release.
    $manifest = New-Object System.Text.StringBuilder
    foreach ($file in ($packageItems | Where-Object { -not $_.PSIsContainer } | Sort-Object FullName)) {
        $relativePath = $file.FullName.Substring($sourceRoot.Length + 1).Replace('\', '/')
        $fileHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        [void]$manifest.Append($relativePath).Append(':').Append($fileHash).Append("`n")
    }
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try { $packageHash = [BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($manifest.ToString()))).Replace('-', '').ToLowerInvariant() }
    finally { $hasher.Dispose() }
    $releasePath = Join-Path $installRoot ("ThermalrightVibeWare-" + $packageHash.Substring(0, 12))
    Assert-ChildPath $releasePath $installRoot
    if ([string]::Equals($sourceRoot, $releasePath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'This package is already in its managed install folder. Run the installer from an unzipped package.'
    }
    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    if (-not (Test-Path -LiteralPath $releasePath)) {
        $stagingPath = "$releasePath.staging.$([Guid]::NewGuid().ToString('N'))"
        Assert-ChildPath $stagingPath $installRoot
        try {
            New-Item -ItemType Directory -Path $stagingPath | Out-Null
            Get-ChildItem -LiteralPath $sourceRoot -Force |
                ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $stagingPath -Recurse -Force }
            Assert-ChildPath $stagingPath $installRoot
            Assert-ChildPath $releasePath $installRoot
            if (Test-Path -LiteralPath $releasePath) { throw 'Another installation created this release while it was being copied.' }
            Move-Item -LiteralPath $stagingPath -Destination $releasePath
        }
        finally {
            if (Test-Path -LiteralPath $stagingPath) {
                Assert-ChildPath $stagingPath $installRoot
                if (Get-ChildItem -LiteralPath $stagingPath -Recurse -Force | Where-Object { ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 } | Select-Object -First 1) {
                    throw "The staging folder contains a link and was kept: $stagingPath"
                }
                Remove-Item -LiteralPath $stagingPath -Recurse -Force
            }
        }
    }
    foreach ($folder in @($startupDirectory, $programsDirectory)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
    $installedExecutable = Join-Path $releasePath 'ThermalrightVibeWare.exe'
    $installedIcon = Join-Path $releasePath 'Assets\thermalright-vibeware.ico'
    Save-StableLauncher $launcherPath $installedExecutable $installRoot
    Copy-Item -LiteralPath $installedIcon -Destination $launcherIcon -Force
    Save-ManagedShortcut $shell $startShortcut $startMarker $launcherPath $installRoot $launcherIcon '' 1 $backupRoot
    Save-ManagedShortcut $shell $startupShortcut $startupMarker $launcherPath $installRoot $launcherIcon '--start-minimized --restore-last-session' 7 $backupRoot

    $uninstaller = Join-Path $releasePath 'Uninstall-ThermalrightVibeWare.ps1'
    $powershellPath = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) 'WindowsPowerShell\v1.0\powershell.exe'
    $uninstallCommand = '"' + $powershellPath + '" -NoProfile -ExecutionPolicy Bypass -File "' + $uninstaller + '"'
    New-Item -Path $uninstallKey -Force | Out-Null
    foreach ($property in @{
        DisplayName = 'Thermalright VibeWare'; Publisher = 'Umberto Giacobbi'; DisplayIcon = $installedIcon
        InstallLocation = $releasePath; UninstallString = $uninstallCommand
        DisplayVersion = (Get-Item -LiteralPath $installedExecutable).VersionInfo.ProductVersion
        InstallDate = (Get-Date).ToString('yyyyMMdd'); URLInfoAbout = 'https://github.com/umbertotechnopreneur/thermalright-vibeware'
    }.GetEnumerator()) {
        New-ItemProperty -LiteralPath $uninstallKey -Name $property.Key -Value $property.Value -PropertyType String -Force | Out-Null
    }
    foreach ($name in @('NoModify', 'NoRepair', 'ThermalrightVibeWareManaged')) {
        New-ItemProperty -LiteralPath $uninstallKey -Name $name -Value 1 -PropertyType DWord -Force | Out-Null
    }
    Write-Host "Thermalright VibeWare is installed at: $releasePath"
    Write-Host "Stable launcher: $launcherPath"
    Write-Host "Open it from Start: $startShortcut"
    Write-Host 'At Windows sign-in, it starts in the tray and starts the saved USB display and theme. Stop cancels output for the current run.'
}
finally {
    if ($null -ne $shell -and [System.Runtime.InteropServices.Marshal]::IsComObject($shell)) {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    }
}
