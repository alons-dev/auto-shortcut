# AutoShortcut - installer (no administrator rights needed)
#
#   irm https://raw.githubusercontent.com/alons-dev/auto-shortcut/main/install.ps1 | iex
#
# It can also be run from a copy of the repository:  .\install.ps1
#
# What it does:
#   1. Downloads the code from GitHub (or uses the local copy).
#   2. Copies it to %LOCALAPPDATA%\Programs\AutoShortcut and checks that it compiles.
#   3. Creates shortcuts in the Start menu and in Windows Startup (runs at sign-in).
#   4. Registers it in Settings > Apps so it can be uninstalled.
#   5. Starts it.
#
# The app runs on Windows PowerShell, which compiles the C# code in memory
# (see run.ps1). No .exe is created, so Smart App Control doesn't block it.
# It is launched with "conhost --headless" so no console window shows up, even
# when Windows Terminal is the default terminal.
#
# Keep this file ASCII-only: Windows PowerShell 5.1 reads scripts without a BOM
# as ANSI, so non-ASCII characters would come out garbled.

& {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'   # the progress bar makes downloads very slow

    $Repo     = 'alons-dev/auto-shortcut'
    $Branch   = 'main'
    $Version  = '0.2.1'
    $AppDir   = Join-Path $env:LOCALAPPDATA 'Programs\AutoShortcut'
    $PsExe    = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $Conhost  = Join-Path $env:WINDIR 'System32\conhost.exe'
    $RegKey   = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AutoShortcut'
    $IconFile = Join-Path $AppDir "icon-$Version.ico"
    $LaunchArgs = "--headless `"$PsExe`" -NoProfile -ExecutionPolicy Bypass -File `"$AppDir\run.ps1`""

    function Step([string]$text) { Write-Host "  - $text" }

    # Closes AutoShortcut if it's running (installed or launched from a copy of the code).
    function Stop-AutoShortcut {
        Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
            Where-Object { $_.CommandLine -match 'run\.ps1' -and $_.CommandLine -match 'AutoShortcut|auto-shortcut' } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Get-Process -Name AutoShortcut -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }

    # Icon for the shortcuts and Settings > Apps: assets\icon.png resized to the
    # usual icon sizes and saved as an .ico holding PNG images (valid since Vista).
    function New-AppIcon([string]$Png, [string]$Path) {
        Add-Type -AssemblyName System.Drawing
        $source = [System.Drawing.Image]::FromFile($Png)
        $images = foreach ($size in 256, 48, 32, 24, 16) {
            $bmp = New-Object System.Drawing.Bitmap $size, $size
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.InterpolationMode = 'HighQualityBicubic'
            $g.PixelOffsetMode = 'HighQuality'
            $g.DrawImage($source, 0, 0, $size, $size)
            $g.Dispose()
            $ms = New-Object System.IO.MemoryStream
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $bmp.Dispose()
            ,@($size, $ms.ToArray())
        }
        $source.Dispose()

        $fs = [System.IO.File]::Create($Path)
        $bw = New-Object System.IO.BinaryWriter $fs
        $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$images.Count)   # ICO header
        $offset = 6 + 16 * $images.Count
        foreach ($img in $images) {                                                     # one entry per image
            $s = if ($img[0] -ge 256) { 0 } else { $img[0] }                            # 0 means 256
            $bw.Write([byte]$s); $bw.Write([byte]$s); $bw.Write([byte]0); $bw.Write([byte]0)
            $bw.Write([uint16]1); $bw.Write([uint16]32)
            $bw.Write([uint32]$img[1].Length); $bw.Write([uint32]$offset)
            $offset += $img[1].Length
        }
        foreach ($img in $images) { $bw.Write([byte[]]$img[1]) }
        $bw.Close()
    }

    function New-Shortcut([string]$Path) {
        $shell = New-Object -ComObject WScript.Shell
        $lnk = $shell.CreateShortcut($Path)
        $lnk.TargetPath = $Conhost
        $lnk.Arguments = $LaunchArgs
        $lnk.WorkingDirectory = $AppDir
        $lnk.IconLocation = "$IconFile,0"
        $lnk.Description = 'Keyboard shortcuts panel'
        $lnk.Save()
    }

    Write-Host ''
    Write-Host "Installing AutoShortcut $Version" -ForegroundColor Cyan

    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('AutoShortcut-' + [guid]::NewGuid().ToString('N'))
    try {
        # 0. Requirements: Windows PowerShell 5.1 and .NET Framework 4.5+ (both ship with Windows 10 and 11)
        if (-not (Test-Path $PsExe)) { throw 'Windows PowerShell (powershell.exe) was not found.' }
        $net = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue).Release
        if (-not $net -or $net -lt 378389) { throw '.NET Framework 4.5 or later is required.' }

        # 1. Source code: local copy or GitHub zip
        New-Item -ItemType Directory -Path $tmp | Out-Null
        $source = $null
        if ($PSScriptRoot -and (Test-Path (Join-Path $PSScriptRoot 'src\Program.cs'))) {
            $source = $PSScriptRoot
            Step "Using the local copy: $source"
        }
        else {
            # AUTOSHORTCUT_ZIP lets you test the installer with another zip (URL or local path).
            $zip = if ($env:AUTOSHORTCUT_ZIP) { $env:AUTOSHORTCUT_ZIP } else { "https://github.com/$Repo/archive/refs/heads/$Branch.zip" }
            Step "Downloading $zip"
            if (Test-Path $zip) { Copy-Item $zip "$tmp\source.zip" }
            else {
                [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
                Invoke-WebRequest $zip -OutFile "$tmp\source.zip" -UseBasicParsing
            }
            Expand-Archive "$tmp\source.zip" "$tmp\x" -Force
            $source = Get-ChildItem "$tmp\x" -Directory | Select-Object -First 1 -ExpandProperty FullName   # auto-shortcut-main
            if (-not $source -or -not (Test-Path "$source\src\Program.cs")) { throw 'The downloaded zip is not in the expected format.' }
        }

        # 2. Close the running version (if any) so its files can be replaced
        Stop-AutoShortcut

        # 3. Copy files. src is replaced entirely so no stale files are left behind.
        Step "Copying to $AppDir"
        New-Item -ItemType Directory -Force -Path $AppDir | Out-Null
        if (Test-Path "$AppDir\src") { Remove-Item "$AppDir\src" -Recurse -Force }
        Copy-Item "$source\src" "$AppDir\src" -Recurse
        Copy-Item "$source\run.ps1", "$source\uninstall.ps1", "$source\LICENSE" $AppDir -Force
        New-Item -ItemType Directory -Force -Path "$AppDir\assets" | Out-Null
        Copy-Item "$source\assets\icon.png" "$AppDir\assets\icon.png" -Force
        # Remove the "downloaded from the Internet" mark so Windows doesn't object to running them.
        Get-ChildItem $AppDir -Recurse -File | Unblock-File

        # 4. Check that it compiles before creating shortcuts
        Step 'Checking that the code compiles'
        $check = "`$f = Get-ChildItem '$AppDir\src' -Filter *.cs | ForEach-Object { `$_.FullName }; " +
                 "Add-Type -Path `$f -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Web.Extensions, Accessibility -WarningAction SilentlyContinue"
        # With 'Stop', the first error line from the child process would abort here without showing the rest.
        $ErrorActionPreference = 'Continue'
        $output = & $PsExe -NoProfile -ExecutionPolicy Bypass -Command $check 2>&1
        $ErrorActionPreference = 'Stop'
        if ($LASTEXITCODE -ne 0) { throw "The code doesn't compile:`n$($output | Out-String)" }

        # 5. Icon and shortcuts
        Step 'Creating shortcuts (Start menu and Windows Startup)'
        # A new file name per version: Explorer caches icons by path, so reusing
        # icon.ico would keep showing the old icon after an update.
        Get-ChildItem $AppDir -Filter 'icon*.ico' | Remove-Item -Force -ErrorAction SilentlyContinue
        New-AppIcon "$AppDir\assets\icon.png" $IconFile
        # 'Create': if the folder doesn't exist (it happens with Startup for some users) it's
        # created; without this option .NET returns an empty path.
        $startMenu = [Environment]::GetFolderPath('Programs', 'Create')
        $startup = [Environment]::GetFolderPath('Startup', 'Create')
        New-Shortcut (Join-Path $startMenu 'AutoShortcut.lnk')
        New-Shortcut (Join-Path $startup 'AutoShortcut.lnk')

        # 6. Register in Settings > Apps (HKCU: current user only, no administrator needed)
        Step 'Registering in Settings > Apps'
        $uninstall = "`"$PsExe`" -NoProfile -ExecutionPolicy Bypass -File `"$AppDir\uninstall.ps1`""
        $size = [int]((Get-ChildItem $AppDir -Recurse -File | Measure-Object Length -Sum).Sum / 1KB)
        New-Item -Path $RegKey -Force | Out-Null
        $values = @{
            DisplayName = 'AutoShortcut'; DisplayVersion = $Version; Publisher = 'alons-dev'
            DisplayIcon = $IconFile; InstallLocation = $AppDir
            UninstallString = $uninstall; QuietUninstallString = "$uninstall -Quiet"
            URLInfoAbout = "https://github.com/$Repo"
        }
        foreach ($k in $values.Keys) { New-ItemProperty -Path $RegKey -Name $k -Value $values[$k] -PropertyType String -Force | Out-Null }
        foreach ($k in 'NoModify', 'NoRepair') { New-ItemProperty -Path $RegKey -Name $k -Value 1 -PropertyType DWord -Force | Out-Null }
        New-ItemProperty -Path $RegKey -Name EstimatedSize -Value $size -PropertyType DWord -Force | Out-Null

        # 7. Start it and check that it stays up
        Step 'Starting AutoShortcut'
        Start-Process $Conhost -ArgumentList $LaunchArgs -WorkingDirectory $AppDir
        $running = $false
        for ($i = 0; $i -lt 30 -and -not $running; $i++) {
            Start-Sleep -Milliseconds 500
            $running = [bool](Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
                Where-Object { $_.CommandLine -like "*$AppDir\run.ps1*" })
        }
        if (-not $running) { throw "AutoShortcut didn't start. Check %APPDATA%\AutoShortcut\error.log if it exists." }

        $hotkey = 'Win+Shift+Z'
        $cfg = Join-Path $env:APPDATA 'AutoShortcut\shortcuts.json'
        if (Test-Path $cfg) { try { $hotkey = (Get-Content $cfg -Raw -Encoding UTF8 | ConvertFrom-Json).hotkey } catch { } }
        # The app moves the old default (Win+Shift+A, which Windows 11 uses) to Win+Shift+Z
        # on start; it may not have saved it yet.
        if (-not $hotkey -or $hotkey -eq 'Win+Shift+A') { $hotkey = 'Win+Shift+Z' }

        Write-Host ''
        Write-Host 'Done. AutoShortcut is running and will start automatically with Windows.' -ForegroundColor Green
        Write-Host "  Open it with $hotkey or by clicking its icon in the system tray (next to the clock)."
        Write-Host '  If another app already uses that combination, change it with the gear button in the panel.'
        Write-Host '  To uninstall: Settings > Apps > AutoShortcut.'
        Write-Host ''
    }
    catch {
        Write-Host ''
        Write-Host "Installation failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  (line $($_.InvocationInfo.ScriptLineNumber): $($_.InvocationInfo.Line.Trim()))" -ForegroundColor DarkGray
        Write-Host ''
    }
    finally {
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}
