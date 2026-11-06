# VBWR B
# TermalRight VibeWare | Umberto Giacobbi
# SPDX-License-Identifier: MIT
# One operator console. Armata was studied for command names and menu structure;
# its proprietary implementation is not copied here.
# VBWR E
#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('help', 'preflight-check', 'preflight', 'build', 'up', 'launch', 'package', 'clean')]
    [string]$Command,
    [ValidateSet('x64', 'ARM64')][string]$Architecture = 'x64',
    [switch]$Plain
)
function Initialize-Winget {
    if (Get-Command winget -ErrorAction SilentlyContinue) { return }
    # Microsoft's bootstrap module installs App Installer and its dependencies.
    Install-PackageProvider -Name NuGet -Force -Scope CurrentUser | Out-Null
    Install-Module Microsoft.WinGet.Client -Repository PSGallery -Scope CurrentUser -Force -AllowClobber
    Import-Module Microsoft.WinGet.Client
    Repair-WinGetPackageManager -Force -Latest
    $env:PATH = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { throw 'App Installer preparation did not expose winget. Restart the terminal after completing the installer.' }
}

# Windows PowerShell can prepare the shell before loading PowerShell 7 operations.
if ($PSVersionTable.PSVersion.Major -lt 7) {
    if ($Command -notin @('preflight', 'preflight-check')) { throw 'Run this script with preflight in Windows PowerShell to prepare PowerShell 7 first.' }
    if (-not (Get-Command pwsh -ErrorAction SilentlyContinue)) {
        if ($Command -eq 'preflight-check') { throw 'PowerShell 7 is missing. Run preflight to install it.' }
        Initialize-Winget
        & winget install --exact --id Microsoft.PowerShell --source winget --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -ne 0) { throw 'PowerShell installation failed. Resolve the installer error and retry.' }
        $env:PATH = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
    }
    & pwsh -NoProfile -File $PSCommandPath $Command -Architecture $Architecture
    exit $LASTEXITCODE
}
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$linuxOutput = Join-Path $repo 'artifacts/linux-debug'
$linuxRuntime = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ThermalrightVibeWare/debug'
$linuxPackages = @('libgtk-3-0t64', 'libwebkit2gtk-4.1-0', 'dpkg-dev', 'clamav', 'clamav-freshclam', 'libssl3t64', 'zlib1g', 'libstdc++6', 'libgcc-s1', 'build-essential', 'pkg-config', 'libgtk-3-dev', 'libwebkit2gtk-4.1-dev', 'libnotify-dev', 'libnotify4', 'libayatana-appindicator3-1', 'python3', 'libegl1', 'libgl1', 'libexpat1')
$catalog = @(
    @{ Name = 'preflight-check'; Title = 'Inspect readiness'; Hint = 'Read-only tools and platform' },
    @{ Name = 'preflight'; Title = 'Pre-flight'; Hint = 'Install missing build prerequisites' },
    @{ Name = 'up'; Title = 'Build and start'; Hint = 'Debug build for this computer' },
    @{ Name = 'build'; Title = 'Build package'; Hint = 'Linux Debug folder; Windows visible MSI reinstall' },
    @{ Name = 'launch'; Title = 'Launch studio'; Hint = 'Open the existing local build' },
    @{ Name = 'package'; Title = 'Prepare distribution'; Hint = 'CI: Linux DEB or Windows ZIP' },
    @{ Name = 'clean'; Title = 'Clean generated build files'; Hint = 'Remove repository build output' },
    @{ Name = 'help'; Title = 'Commands and help'; Hint = 'CLI usage and limits' }
)
function Invoke-Checked([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Program failed (exit $LASTEXITCODE)." }
}
function Get-Readiness {
    $checks = [ordered]@{}
    $checks['PowerShell 7'] = $PSVersionTable.PSVersion.Major -ge 7
    $checks['Git'] = [bool](Get-Command git -ErrorAction SilentlyContinue)
    $checks['.NET 10 SDK'] = $false
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $checks['.NET 10 SDK'] = @(& dotnet --list-sdks | Where-Object { $_ -match '^10\.0\.' }).Count -gt 0
    }
    if ($IsLinux) {
        $checks['ClamAV signature database'] = @(Get-ChildItem /var/lib/clamav -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -in '.cvd', '.cld' }).Count -gt 0
        $checks['ICU runtime'] = @(& dpkg-query -W '-f=${db:Status-Status}\n' 'libicu[0-9]*' 2>$null | Where-Object { $_ -eq 'installed' }).Count -gt 0
        foreach ($package in $linuxPackages) {
            $status = & dpkg-query -W '-f=${Status}\n' $package 2>$null
            $checks[$package] = $LASTEXITCODE -eq 0 -and @($status | Where-Object { $_ -eq 'install ok installed' }).Count -gt 0
        }
    } elseif ($IsWindows) {
        $checks['Windows Package Manager'] = [bool](Get-Command winget -ErrorAction SilentlyContinue)
        $checks['Microsoft Defender scanner'] = [bool](Get-ChildItem "$env:ProgramData/Microsoft/Windows Defender/Platform/*/MpCmdRun.exe" -ErrorAction SilentlyContinue)
        $checks['WebView2 runtime'] = (Test-Path "${env:ProgramFiles(x86)}/Microsoft/EdgeWebView/Application") -or (Test-Path "$env:LOCALAPPDATA/Microsoft/EdgeWebView/Application")
        $checks['Visual C++ runtime'] = Test-Path "HKLM:/SOFTWARE/Microsoft/VisualStudio/14.0/VC/Runtimes/$Architecture"
        $checks['Python with pip'] = $false
        if (Get-Command python -ErrorAction SilentlyContinue) {
            & python -m pip --version *> $null
            $checks['Python with pip'] = $LASTEXITCODE -eq 0
        }
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $checks['C++ tools and Windows SDK'] = $false
        if (Test-Path $vswhere) {
            $components = @('Microsoft.VisualStudio.Component.VC.Tools.x86.x64', 'Microsoft.VisualStudio.Component.Windows11SDK.26100')
            if ($Architecture -eq 'ARM64') { $components += 'Microsoft.VisualStudio.Component.VC.Tools.ARM64' }
            $checks['C++ tools and Windows SDK'] = @(& $vswhere -latest -products '*' -requires @components -property installationPath).Count -gt 0
        }
    } else { throw 'Only Windows and Ubuntu Linux are supported by this console.' }
    return $checks
}
function Invoke-Preflight([switch]$Install) {
    if ($IsLinux) {
        $os = Get-Content /etc/os-release -Raw
        if ($os -notmatch '(?m)^ID=ubuntu$') { throw 'Automatic Linux preparation currently requires Ubuntu.' }
    }
    $checks = Get-Readiness
    foreach ($name in $checks.Keys) {
        $status = if ($checks[$name]) { 'ready' } else { 'missing' }
        Write-Host ('{0,-32} {1}' -f $name, $status)
    }
    if (@($checks.Values | Where-Object { -not $_ }).Count -eq 0) { return }
    if (-not $Install) { throw 'Prerequisites are missing. Run vibeware.ps1 preflight to install them.' }
    if ($IsLinux) {
        $packages = @($linuxPackages | Where-Object { -not $checks[$_] })
        if (-not $checks['.NET 10 SDK']) { $packages += 'dotnet-sdk-10.0' }
        if (-not $checks['Git']) { $packages += 'git' }
        if (-not $checks['ICU runtime']) { $packages += 'libicu-dev' }
        if ($packages.Count -gt 0) {
            Invoke-Checked sudo @('apt-get', 'update')
            Invoke-Checked sudo (@('apt-get', '-o', 'DPkg::Lock::Timeout=60', 'install', '-y') + $packages)
        }
        if (-not $checks['ClamAV signature database']) { Invoke-Checked sudo @('freshclam') }
    } else {
        Initialize-Winget
        $ids = @()
        if (-not $checks['Microsoft Defender scanner']) { throw 'Microsoft Defender scanner is unavailable. Restore the Windows security component before building; preflight does not change Defender settings.' }
        if (-not $checks['Git']) { $ids += 'Git.Git' }
        if (-not $checks['WebView2 runtime']) { $ids += 'Microsoft.EdgeWebView2Runtime' }
        if (-not $checks['Visual C++ runtime']) { $ids += "Microsoft.VCRedist.2015+.$Architecture" }
        if (-not $checks['.NET 10 SDK']) { $ids += 'Microsoft.DotNet.SDK.10' }
        if (-not $checks['Python with pip']) { $ids += 'Python.Python.3.13' }
        foreach ($id in $ids) {
            Invoke-Checked winget @('install', '--exact', '--id', $id, '--source', 'winget', '--accept-package-agreements', '--accept-source-agreements', '--disable-interactivity')
        }
        if (-not $checks['C++ tools and Windows SDK']) {
            $override = '--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.Windows11SDK.26100 --includeRecommended'
            if ($Architecture -eq 'ARM64') { $override += ' --add Microsoft.VisualStudio.Component.VC.Tools.ARM64' }
            $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
            $instances = @()
            if (Test-Path $vswhere) { $instances = @(& $vswhere -latest -products '*' -property installationPath) }
            if ($instances.Count -gt 0) {
                $setup = Join-Path (Split-Path $vswhere -Parent) 'setup.exe'
                $arguments = 'modify --installPath "' + $instances[0] + '" ' + $override.Replace('--wait ', '')
                $process = Start-Process $setup -ArgumentList $arguments -Wait -PassThru
                if ($process.ExitCode -ne 0) { throw "C++ tools preparation stopped (exit $($process.ExitCode)). Complete any required restart, then retry preflight." }
            } else {
                Invoke-Checked winget @('install', '--exact', '--id', 'Microsoft.VisualStudio.2022.BuildTools', '--source', 'winget', '--accept-package-agreements', '--accept-source-agreements', '--override', $override)
            }
        }
        $env:PATH = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
    }
    Invoke-Preflight
}
function Invoke-LinuxBuild {
    if ($Architecture -ne 'x64') { throw 'Linux builds currently support x64 only.' }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    # Scope dependency imports to this build, never scan the user's shared cache.
    $env:NUGET_PACKAGES = Join-Path $repo ('artifacts/nuget-stage/' + [guid]::NewGuid().ToString('N'))
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $repo 'artifacts/nuget-http'
    foreach ($name in @('Photino', 'Cli')) {
        $project = Join-Path $repo "src/ThermalrightVibeWare.$name/ThermalrightVibeWare.$name.csproj"
        Invoke-Checked dotnet @('restore', $project, '-p:TargetFramework=net10.0', '-r', 'linux-x64')
    }
    Invoke-Checked clamscan @('--recursive', '--infected', '--no-summary', $env:NUGET_PACKAGES)
    foreach ($name in @('Photino', 'Cli')) {
        $project = Join-Path $repo "src/ThermalrightVibeWare.$name/ThermalrightVibeWare.$name.csproj"
        $output = if ($name -eq 'Cli') { Join-Path $linuxOutput 'engine' } else { $linuxOutput }
        Invoke-Checked dotnet @('publish', $project, '-f', 'net10.0', '-c', 'Debug', '-r', 'linux-x64', '--self-contained', 'true', '--no-restore', '--disable-build-servers', '-o', $output)
    }
    & "$PSScriptRoot/build/Build-PhotinoLinux.ps1" -Destination $linuxOutput
    & "$PSScriptRoot/build/Bundle-LinuxPython.ps1" -Destination $linuxOutput
}
function Invoke-Selection([string]$Name) {
    switch ($Name) {
        'help' {
            Write-Host 'TermalRight VibeWare | Umberto Giacobbi'
            Write-Host 'pwsh -NoProfile -File ./scripts/vibeware.ps1 <command> [-Architecture x64|ARM64] [-Plain]'
            foreach ($item in $catalog) { Write-Host ('{0,-18} {1} — {2}' -f $item.Name, $item.Title, $item.Hint) }
            Write-Host 'Linux: local theme preview and sensor diagnostics; USB, audio and media are unavailable. Themes run Python code; use only the official repository.'
        }
        'preflight-check' { Invoke-Preflight }
        'preflight' { Invoke-Preflight -Install }
        { $_ -in 'build', 'up' } {
            Invoke-Preflight
            if ($IsLinux) {
                Invoke-LinuxBuild
                New-Item -ItemType Directory -Path $linuxRuntime -Force | Out-Null
                # Copy outside the checkout so repository output can be cleaned after launch.
                Copy-Item -Path "$linuxOutput/*" -Destination $linuxRuntime -Recurse -Force
                & "$PSScriptRoot/maintenance/Clean-BuildOutputs.ps1"
            }
            else {
                Import-Module "$PSScriptRoot/modules/Build.psm1" -Force
                Invoke-Reinstall -Architecture $Architecture
            }
            if ($Name -eq 'up' -and $IsLinux) { Invoke-Selection launch }
        }
        'launch' {
            $executable = if ($IsLinux) { Join-Path $linuxRuntime 'ThermalrightVibeWare' } else { Join-Path $env:LOCALAPPDATA 'Umberto Giacobbi.biz/Apps/ThermalrightVibeWare/MSI/ThermalrightVibeWare.exe' }
            if (-not (Test-Path -LiteralPath $executable)) { throw 'Studio build is missing. Run up first.' }
            if ($IsLinux) {
                # Keep the desktop app outside the build console's process lifetime.
                # This unit is transient: no service file or sign-in entry is installed.
                if (-not (Get-Command systemd-run -ErrorAction SilentlyContinue)) { throw 'Linux launch requires systemd-run and an active user service manager.' }
                $unit = 'thermalright-vibeware-studio-' + [guid]::NewGuid().ToString('N')
                Invoke-Checked systemd-run @('--user', '--collect', '--quiet', "--unit=$unit", "--property=WorkingDirectory=$(Split-Path $executable -Parent)", $executable)
                Start-Sleep -Milliseconds 1500
                $state = & systemctl --user show "$unit.service" --property=ActiveState --value 2>$null
                if ($LASTEXITCODE -ne 0 -or $state -ne 'active') { throw "Studio startup failed. Read journalctl --user -u $unit.service for details." }
                Write-Host "Studio started independently of this console. Log: journalctl --user -u $unit.service"
                return
            }
            $process = Start-Process -FilePath $executable -WorkingDirectory (Split-Path $executable -Parent) -PassThru
            if ($process.WaitForExit(1500) -and $process.ExitCode -ne 0) { throw "Studio startup failed (exit $($process.ExitCode)). See the native startup error above." }
        }
        'package' {
            Invoke-Preflight
            if ($IsLinux) {
                Invoke-LinuxBuild
                & "$PSScriptRoot/build/Build-Deb.ps1" -PayloadPath $linuxOutput
                $version = (Get-Content "$repo/installer/Version.txt" -Raw).Trim()
                & "$PSScriptRoot/maintenance/Clean-BuildOutputs.ps1" -KeepPackagePath "$repo/artifacts/packages/thermalright-vibeware_${version}_amd64.deb"
            } else {
                Invoke-Preflight
                Import-Module "$PSScriptRoot/modules/Build.psm1" -Force
                Invoke-Package -Architecture $Architecture -Configuration Debug
            }
        }
        'clean' { & "$PSScriptRoot/maintenance/Clean-BuildOutputs.ps1" }
    }
}
try {
    if ($Command) { Invoke-Selection $Command }
    else {
        if ([Console]::IsInputRedirected) { throw 'The menu needs an interactive terminal. Supply a command, for example: vibeware.ps1 help.' }
        while ($true) {
            Write-Host "`nTermalRight VibeWare | Umberto Giacobbi"
            for ($i = 0; $i -lt $catalog.Count; $i++) { Write-Host ('  {0}. {1} — {2}' -f ($i + 1), $catalog[$i].Title, $catalog[$i].Hint) }
            Write-Host '  0. Exit'
            $choice = Read-Host 'Choose an action'
            if ($choice -eq '0') { break }
            $index = 0
            if (-not [int]::TryParse($choice, [ref]$index) -or $index -lt 1 -or $index -gt $catalog.Count) { Write-Host 'Choose a listed number.'; continue }
            try { Invoke-Selection $catalog[$index - 1].Name }
            catch { Write-Host "Action stopped: $($_.Exception.Message)" -ForegroundColor Red }
            [void](Read-Host 'Press Enter to return to the menu')
        }
    }
} catch { Write-Error "Action stopped: $($_.Exception.Message)"; exit 1 }
