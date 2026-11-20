# Compiles src\*.cs into bin\AutoShortcut.exe with the .NET Framework 4.x csc.exe
# that ships with Windows. No Visual Studio, SDK or NuGet needed.
#
#   .\build.ps1                 -> bin\AutoShortcut.exe
#   .\build.ps1 -Out C:\x.exe   -> wherever you want

param(
    [string]$Out = (Join-Path $PSScriptRoot 'bin\AutoShortcut.exe')
)

$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'The .NET Framework 4.x csc.exe was not found.' }

$outDir = Split-Path $Out -Parent
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Windows won't overwrite the exe while it's running.
Get-Process -Name AutoShortcut -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq [IO.Path]::GetFullPath($Out) } |
    ForEach-Object { $_.Kill(); $_.WaitForExit(3000) | Out-Null }

$sources = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs | ForEach-Object { $_.FullName }

$cscArgs = @(
    '/nologo',
    '/target:winexe',          # windowed app: no black console
    '/optimize+',
    '/codepage:65001',         # the .cs files are UTF-8 (symbols like the check mark)
    "/out:$Out",
    '/r:System.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Web.Extensions.dll'   # JavaScriptSerializer to read the JSON
) + $sources

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed (exit code $LASTEXITCODE)." }

Write-Host "OK -> $Out"
