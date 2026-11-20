# Compila src\*.cs en bin\AutoShortcut.exe con el csc.exe de .NET Framework 4.x,
# que viene con Windows. No necesita Visual Studio, SDK ni NuGet.
#
#   .\build.ps1                 -> bin\AutoShortcut.exe
#   .\build.ps1 -Out C:\x.exe   -> donde quieras (lo usa install.ps1)

param(
    [string]$Out = (Join-Path $PSScriptRoot 'bin\AutoShortcut.exe')
)

$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'No se encuentra csc.exe de .NET Framework 4.x.' }

$outDir = Split-Path $Out -Parent
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Si el exe esta en marcha, Windows no deja sobrescribirlo.
Get-Process -Name AutoShortcut -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq [IO.Path]::GetFullPath($Out) } |
    ForEach-Object { $_.Kill(); $_.WaitForExit(3000) | Out-Null }

$sources = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs | ForEach-Object { $_.FullName }

$cscArgs = @(
    '/nologo',
    '/target:winexe',          # app de ventanas: sin consola negra
    '/optimize+',
    '/codepage:65001',         # los .cs estan en UTF-8 (tildes, enes)
    "/out:$Out",
    '/r:System.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Web.Extensions.dll'   # JavaScriptSerializer para leer el JSON
) + $sources

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "La compilacion ha fallado (codigo $LASTEXITCODE)." }

Write-Host "OK -> $Out"
