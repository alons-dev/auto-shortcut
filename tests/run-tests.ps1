# Runs the logic tests (tests\LogicTests.cs) against the code in src.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
#
# The tests are compiled together with the app (Add-Type), so they can use its
# internal classes. Exit code = number of failed tests. ASCII-only (see install.ps1).

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -eq 'Core') {
    throw 'run-tests.ps1 needs Windows PowerShell: powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1'
}

$root = Split-Path $PSScriptRoot -Parent
$files = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs) + @(Get-ChildItem $PSScriptRoot -Filter *.cs) |
    ForEach-Object { $_.FullName }

try {
    Add-Type -Path $files -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Web.Extensions -WarningAction SilentlyContinue
}
catch {
    Write-Host "FAIL compile: $($_.Exception.Message)"
    exit 1
}

$failed = [AutoShortcut.Tests.LogicTests]::Run()
exit $failed
