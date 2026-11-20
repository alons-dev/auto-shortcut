# AutoShortcut - uninstaller
#
#   From Settings > Apps > AutoShortcut > Uninstall, or:
#   & "$env:LOCALAPPDATA\Programs\AutoShortcut\uninstall.ps1"
#
#   -RemoveSettings   also deletes your shortcut list (%APPDATA%\AutoShortcut)
#   -Quiet            doesn't wait for a key press at the end
#
# ASCII-only on purpose (see install.ps1).

param(
    [switch]$RemoveSettings,
    [switch]$Quiet
)

$ErrorActionPreference = 'Continue'
$AppDir   = Join-Path $env:LOCALAPPDATA 'Programs\AutoShortcut'
$Settings = Join-Path $env:APPDATA 'AutoShortcut'
$RegKey   = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AutoShortcut'

Write-Host ''
Write-Host 'Uninstalling AutoShortcut' -ForegroundColor Cyan

# 1. Close the app
Write-Host '  - Closing AutoShortcut'
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
    Where-Object { $_.CommandLine -match 'run\.ps1' -and $_.CommandLine -match 'AutoShortcut|auto-shortcut' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Get-Process -Name AutoShortcut -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 2. Shortcuts and registry entry
Write-Host '  - Removing shortcuts and the Apps entry'
# Fixed paths: GetFolderPath returns an empty string if the folder doesn't exist.
$programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
Remove-Item (Join-Path $programs 'AutoShortcut.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $programs 'Startup\AutoShortcut.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item $RegKey -Recurse -Force -ErrorAction SilentlyContinue

# 3. App files. Windows won't delete the folder while the terminal is inside it, so
#    only in that case move to the user's folder. (When run with &, changing the
#    location affects the caller's session: don't do it without a reason.)
Write-Host "  - Deleting $AppDir"
if ((Get-Location).ProviderPath -like "$AppDir*") { Set-Location $HOME }
Remove-Item $AppDir -Recurse -Force -ErrorAction SilentlyContinue

# 4. Your shortcut list is kept unless asked otherwise
if ($RemoveSettings) {
    Write-Host "  - Deleting your shortcut list ($Settings)"
    Remove-Item $Settings -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host 'AutoShortcut has been uninstalled.' -ForegroundColor Green
if (-not $RemoveSettings -and (Test-Path $Settings)) {
    Write-Host "  Your shortcut list is still in $Settings in case you reinstall it."
    Write-Host '  To delete it too:  Remove-Item "$env:APPDATA\AutoShortcut" -Recurse -Force'
}
Write-Host ''

if (-not $Quiet -and $Host.Name -eq 'ConsoleHost') {
    Write-Host 'Press any key to close...'
    try { [void]$Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown') } catch { }
}
