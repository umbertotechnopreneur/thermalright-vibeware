# VBWR B
# TermalRight VibeWare | Umberto Giacobbi | SPDX-License-Identifier: MIT
# Build the pinned Apache-2.0 Photino bridge against WebKitGTK 4.1.
# VBWR E
#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Destination)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$root = Join-Path $repo 'artifacts/native/linux'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$archive = Join-Path $root 'photino.zip'
$commit = 'd790e5be3127ec628d0a9f1f9fac68e14f339c2a'
if (-not (Test-Path $archive)) {
    Invoke-WebRequest "https://api.github.com/repos/tryphotino/photino.Native/zipball/$commit" -OutFile $archive
}
if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne '5C6546959017509B80A1884EADCD963FDDCE23A58D5043FE02F42A962E0B6A17') { throw 'Photino source archive checksum mismatch.' }
& clamscan --recursive --infected --no-summary $archive
if ($LASTEXITCODE -ne 0) { throw 'Photino archive scan failed or scanner is unavailable.' }
$work = Join-Path $root ([guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $archive -DestinationPath $work
& clamscan --recursive --infected --no-summary $work
if ($LASTEXITCODE -ne 0) { throw 'Photino extracted source scan failed.' }
$source = Join-Path @(Get-ChildItem $work -Directory)[0].FullName 'Photino.Native'
# Wayland has no global primary monitor. Select the monitor containing the window,
# or monitor zero for a window that has not been realized yet.
$linuxSource = Join-Path $source 'Photino.Linux.cpp'
$text = [IO.File]::ReadAllText($linuxSource).Replace("`r`n", "`n")
$old = 'GdkMonitor *m = gdk_display_get_primary_monitor(d);'
if (-not $text.Contains($old)) { throw 'Pinned Photino monitor patch no longer matches.' }
$text = $text.Replace($old, @'
GdkWindow *nativeWindow = gtk_widget_get_window(_window);
    GdkMonitor *m = nativeWindow != nullptr
        ? gdk_display_get_monitor_at_window(d, nativeWindow)
        : gdk_display_get_monitor(d, 0);
'@)
$text = $text.Replace('gdk_display_get_primary_monitor() returned NULL', 'No monitor is available for this studio window')
# A missing display or monitor must never reach a native call with a null object.
$start = $text.IndexOf('void Photino::Center()')
$end = $text.IndexOf('void Photino::ClearBrowserAutoFill()', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Pinned Photino centering function was not found.' }
$center = $text.Substring($start, $end - $start)
$center = $center.Replace('gtk_widget_destroy(dialog);', "gtk_widget_destroy(dialog);`n        return;")
$center = $center.Replace('gdk_monitor_get_geometry(m, &screen);', 'gdk_monitor_get_workarea(m, &screen);')
$center = $center.Replace('(screen.width - windowWidth) / 2', 'screen.x + (screen.width - windowWidth) / 2')
$center = $center.Replace('(screen.height - windowHeight) / 2', 'screen.y + (screen.height - windowHeight) / 2')
$text = $text.Substring(0, $start) + $center + $text.Substring($end)
[IO.File]::WriteAllText($linuxSource, $text)
$flags = @(& pkg-config --cflags --libs gtk+-3.0 webkit2gtk-4.1 libnotify)
if ($LASTEXITCODE -ne 0) { throw 'GTK, WebKitGTK 4.1 and libnotify development packages are required. Run preflight.' }
$arguments = @('-std=c++20', '-O0', '-g', '-shared', '-fPIC', '-o', (Join-Path $Destination 'Photino.Native.so'), "$source/Photino.Linux.Dialog.cpp", "$source/Photino.Linux.cpp", "$source/Exports.cpp")
& c++ @arguments @($flags -split '\s+' | Where-Object { $_ })
if ($LASTEXITCODE -ne 0) { throw 'Linux Photino Debug compilation failed.' }
Write-Host 'Built Photino Native Debug against WebKitGTK 4.1.'
