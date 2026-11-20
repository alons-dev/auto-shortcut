# AutoShortcut - desinstalador
#
#   Desde Configuracion > Aplicaciones > AutoShortcut > Desinstalar, o:
#   & "$env:LOCALAPPDATA\Programs\AutoShortcut\uninstall.ps1"
#
#   -RemoveSettings   borra tambien tu lista de atajos (%APPDATA%\AutoShortcut)
#   -Quiet            no espera a que pulses una tecla al terminar
#
# Solo ASCII a proposito (ver install.ps1).

param(
    [switch]$RemoveSettings,
    [switch]$Quiet
)

$ErrorActionPreference = 'Continue'
$AppDir   = Join-Path $env:LOCALAPPDATA 'Programs\AutoShortcut'
$Settings = Join-Path $env:APPDATA 'AutoShortcut'
$RegKey   = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AutoShortcut'

Write-Host ''
Write-Host 'Desinstalando AutoShortcut' -ForegroundColor Cyan

# 1. Cerrar la app
Write-Host '  - Cerrando AutoShortcut'
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
    Where-Object { $_.CommandLine -match 'run\.ps1' -and $_.CommandLine -match 'AutoShortcut|auto-shortcut' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Get-Process -Name AutoShortcut -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 2. Accesos directos y registro
Write-Host '  - Quitando accesos directos y el registro en Aplicaciones'
# Rutas fijas: GetFolderPath devuelve una cadena vacia si la carpeta no existe.
$programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
Remove-Item (Join-Path $programs 'AutoShortcut.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $programs 'Startup\AutoShortcut.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item $RegKey -Recurse -Force -ErrorAction SilentlyContinue

# 3. Archivos de la app (salimos de la carpeta antes de borrarla)
Write-Host "  - Borrando $AppDir"
Set-Location $env:TEMP
Remove-Item $AppDir -Recurse -Force -ErrorAction SilentlyContinue

# 4. Tu lista de atajos se conserva salvo que se pida lo contrario
if ($RemoveSettings) {
    Write-Host "  - Borrando tu lista de atajos ($Settings)"
    Remove-Item $Settings -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host 'AutoShortcut se ha desinstalado.' -ForegroundColor Green
if (-not $RemoveSettings -and (Test-Path $Settings)) {
    Write-Host "  Tu lista de atajos sigue en $Settings por si vuelves a instalarlo."
}
Write-Host ''

if (-not $Quiet -and $Host.Name -eq 'ConsoleHost') {
    Write-Host 'Pulsa una tecla para cerrar...'
    try { [void]$Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown') } catch { }
}
