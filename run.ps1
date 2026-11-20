# Starts AutoShortcut straight from the source code, without creating any .exe.
#
#   powershell -WindowStyle Hidden -ExecutionPolicy Bypass -File .\run.ps1
#
# Windows PowerShell (signed by Microsoft) compiles src\*.cs in memory with Add-Type
# and calls Program.Main. That way Smart App Control has no unsigned .exe to block.
# Startup takes 1-2 s because of the compilation.

$ErrorActionPreference = 'Stop'

# PowerShell 7 (pwsh) runs on modern .NET, without System.Web.Extensions or the .NET Framework WinForms.
if ($PSVersionTable.PSEdition -eq 'Core') {
    throw 'run.ps1 needs Windows PowerShell: powershell -ExecutionPolicy Bypass -File .\run.ps1'
}

$src = Join-Path $PSScriptRoot 'src'
$files = Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName }

# The classes are "internal": Add-Type warns that they aren't public; that's expected.
Add-Type -Path $files -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Web.Extensions, Accessibility -WarningAction SilentlyContinue

# Code compiled in memory doesn't know its folder: tell it, so it finds assets\icon.png.
$env:AUTOSHORTCUT_HOME = $PSScriptRoot

$program = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('AutoShortcut.Program', $false) } |
    Where-Object { $_ } | Select-Object -First 1

$program.GetMethod('Main', [Reflection.BindingFlags]'NonPublic,Static').Invoke($null, $null)
