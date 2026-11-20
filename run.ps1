# Arranca AutoShortcut directamente desde el codigo fuente, sin crear ningun .exe.
#
#   powershell -WindowStyle Hidden -ExecutionPolicy Bypass -File .\run.ps1
#
# Windows PowerShell (firmado por Microsoft) compila src\*.cs en memoria con Add-Type
# y llama a Program.Main. Asi Smart App Control no tiene ningun .exe sin firmar que
# bloquear. Tarda 1-2 s en arrancar por la compilacion.

$ErrorActionPreference = 'Stop'

# PowerShell 7 (pwsh) usa .NET moderno, sin System.Web.Extensions ni el WinForms de .NET Framework.
if ($PSVersionTable.PSEdition -eq 'Core') {
    throw 'run.ps1 necesita Windows PowerShell: powershell -ExecutionPolicy Bypass -File .\run.ps1'
}

$src = Join-Path $PSScriptRoot 'src'
$files = Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName }

# Las clases son "internal": Add-Type avisa de que no son publicas; es lo esperado.
Add-Type -Path $files -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Web.Extensions -WarningAction SilentlyContinue

$program = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('AutoShortcut.Program', $false) } |
    Where-Object { $_ } | Select-Object -First 1

$program.GetMethod('Main', [Reflection.BindingFlags]'NonPublic,Static').Invoke($null, $null)
