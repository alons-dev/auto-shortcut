# AutoShortcut - instalador (no necesita permisos de administrador)
#
#   irm https://raw.githubusercontent.com/alons-dev/auto-shortcut/main/install.ps1 | iex
#
# Tambien se puede ejecutar desde una copia del repositorio:  .\install.ps1
#
# Que hace:
#   1. Descarga el codigo de GitHub (o usa la copia local).
#   2. Lo copia a %LOCALAPPDATA%\Programs\AutoShortcut y comprueba que compila.
#   3. Crea accesos directos en el menu Inicio y en Inicio de Windows (arranca con el PC).
#   4. Lo registra en Configuracion > Aplicaciones para poder desinstalarlo.
#   5. Lo arranca.
#
# La app se ejecuta con Windows PowerShell, que compila el codigo C# en memoria
# (ver run.ps1). No se crea ningun .exe: asi Smart App Control no la bloquea.
# Se lanza con "conhost --headless" para que no aparezca ninguna ventana de consola,
# tampoco si Windows Terminal es la terminal predeterminada.
#
# Este archivo es solo ASCII a proposito: Windows PowerShell 5.1 lee los scripts
# sin BOM como ANSI y las tildes saldrian rotas.

& {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'   # la barra de progreso hace muy lentas las descargas

    $Repo     = 'alons-dev/auto-shortcut'
    $Branch   = 'main'
    $Version  = '0.1.0'
    $AppDir   = Join-Path $env:LOCALAPPDATA 'Programs\AutoShortcut'
    $PsExe    = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $Conhost  = Join-Path $env:WINDIR 'System32\conhost.exe'
    $RegKey   = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AutoShortcut'
    $LaunchArgs = "--headless `"$PsExe`" -NoProfile -ExecutionPolicy Bypass -File `"$AppDir\run.ps1`""

    function Step([string]$text) { Write-Host "  - $text" }

    # Cierra AutoShortcut si esta en marcha (instalado o lanzado desde una copia del codigo).
    function Stop-AutoShortcut {
        Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
            Where-Object { $_.CommandLine -match 'run\.ps1' -and $_.CommandLine -match 'AutoShortcut|auto-shortcut' } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Get-Process -Name AutoShortcut -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }

    # Icono: cuadrado redondeado azul con el teclado de la fuente de iconos de Windows.
    # Se guarda como .ico con varias imagenes PNG (formato valido desde Windows Vista).
    function New-AppIcon([string]$Path) {
        Add-Type -AssemblyName System.Drawing
        $fontName = 'Segoe Fluent Icons'
        $probe = New-Object System.Drawing.Font($fontName, 10)
        if ($probe.Name -ne $fontName) { $fontName = 'Segoe MDL2 Assets' }
        $probe.Dispose()

        $images = foreach ($size in 256, 48, 32, 16) {
            $bmp = New-Object System.Drawing.Bitmap $size, $size
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.SmoothingMode = 'AntiAlias'
            $g.TextRenderingHint = 'AntiAliasGridFit'
            $m = [Math]::Max(1, [int]($size / 32)); $w = $size - 2 * $m; $d = [int]($size * 0.36)
            $shape = New-Object System.Drawing.Drawing2D.GraphicsPath
            $shape.AddArc($m, $m, $d, $d, 180, 90)
            $shape.AddArc($m + $w - $d, $m, $d, $d, 270, 90)
            $shape.AddArc($m + $w - $d, $m + $w - $d, $d, $d, 0, 90)
            $shape.AddArc($m, $m + $w - $d, $d, $d, 90, 90)
            $shape.CloseFigure()
            $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 103, 192))), $shape)
            $font = New-Object System.Drawing.Font($fontName, [float]($size * 0.55), [System.Drawing.GraphicsUnit]::Pixel)
            $fmt = New-Object System.Drawing.StringFormat
            $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
            $g.DrawString([string][char]0xE765, $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 0, $size, $size), $fmt)
            $g.Dispose(); $font.Dispose()
            $ms = New-Object System.IO.MemoryStream
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $bmp.Dispose()
            ,@($size, $ms.ToArray())
        }

        $fs = [System.IO.File]::Create($Path)
        $bw = New-Object System.IO.BinaryWriter $fs
        $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$images.Count)   # cabecera ICO
        $offset = 6 + 16 * $images.Count
        foreach ($img in $images) {                                                     # un registro por imagen
            $s = if ($img[0] -ge 256) { 0 } else { $img[0] }                            # 0 significa 256
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
        $lnk.IconLocation = "$AppDir\icon.ico,0"
        $lnk.Description = 'Panel de atajos de teclado'
        $lnk.Save()
    }

    Write-Host ''
    Write-Host "Instalando AutoShortcut $Version" -ForegroundColor Cyan

    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('AutoShortcut-' + [guid]::NewGuid().ToString('N'))
    try {
        # 0. Requisitos: Windows PowerShell 5.1 y .NET Framework 4.5 o superior (vienen con Windows 10 y 11)
        if (-not (Test-Path $PsExe)) { throw 'No se encuentra Windows PowerShell (powershell.exe).' }
        $net = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue).Release
        if (-not $net -or $net -lt 378389) { throw 'Hace falta .NET Framework 4.5 o superior.' }

        # 1. Codigo fuente: copia local o zip de GitHub
        New-Item -ItemType Directory -Path $tmp | Out-Null
        $source = $null
        if ($PSScriptRoot -and (Test-Path (Join-Path $PSScriptRoot 'src\Program.cs'))) {
            $source = $PSScriptRoot
            Step "Usando la copia local: $source"
        }
        else {
            # AUTOSHORTCUT_ZIP permite probar el instalador con otro zip (URL o ruta local).
            $zip = if ($env:AUTOSHORTCUT_ZIP) { $env:AUTOSHORTCUT_ZIP } else { "https://github.com/$Repo/archive/refs/heads/$Branch.zip" }
            Step "Descargando $zip"
            if (Test-Path $zip) { Copy-Item $zip "$tmp\source.zip" }
            else {
                [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
                Invoke-WebRequest $zip -OutFile "$tmp\source.zip" -UseBasicParsing
            }
            Expand-Archive "$tmp\source.zip" "$tmp\x" -Force
            $source = Get-ChildItem "$tmp\x" -Directory | Select-Object -First 1 -ExpandProperty FullName   # auto-shortcut-main
            if (-not $source -or -not (Test-Path "$source\src\Program.cs")) { throw 'El zip descargado no tiene el formato esperado.' }
        }

        # 2. Cerrar la version en marcha (si la hay) para poder reemplazar los archivos
        Stop-AutoShortcut

        # 3. Copiar archivos. src se reemplaza entero para no dejar archivos viejos.
        Step "Copiando a $AppDir"
        New-Item -ItemType Directory -Force -Path $AppDir | Out-Null
        if (Test-Path "$AppDir\src") { Remove-Item "$AppDir\src" -Recurse -Force }
        Copy-Item "$source\src" "$AppDir\src" -Recurse
        Copy-Item "$source\run.ps1", "$source\uninstall.ps1", "$source\LICENSE" $AppDir -Force
        # Quitar la marca "descargado de Internet" para que Windows no ponga pegas al ejecutarlos.
        Get-ChildItem $AppDir -Recurse -File | Unblock-File

        # 4. Comprobar que compila antes de crear accesos directos
        Step 'Comprobando que el codigo compila'
        $check = "`$f = Get-ChildItem '$AppDir\src' -Filter *.cs | ForEach-Object { `$_.FullName }; " +
                 "Add-Type -Path `$f -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Web.Extensions -WarningAction SilentlyContinue"
        # Con 'Stop', la primera linea de error del proceso hijo cortaria aqui sin mostrar el resto.
        $ErrorActionPreference = 'Continue'
        $output = & $PsExe -NoProfile -ExecutionPolicy Bypass -Command $check 2>&1
        $ErrorActionPreference = 'Stop'
        if ($LASTEXITCODE -ne 0) { throw "El codigo no compila:`n$($output | Out-String)" }

        # 5. Icono y accesos directos
        Step 'Creando accesos directos (menu Inicio y arranque con Windows)'
        New-AppIcon "$AppDir\icon.ico"
        # 'Create': si la carpeta no existe (pasa en algunos usuarios con la de Inicio), se crea;
        # sin esta opcion .NET devuelve una ruta vacia.
        $startMenu = [Environment]::GetFolderPath('Programs', 'Create')
        $startup = [Environment]::GetFolderPath('Startup', 'Create')
        New-Shortcut (Join-Path $startMenu 'AutoShortcut.lnk')
        New-Shortcut (Join-Path $startup 'AutoShortcut.lnk')

        # 6. Registro en Configuracion > Aplicaciones (HKCU: solo para este usuario, sin administrador)
        Step 'Registrando en Configuracion > Aplicaciones'
        $uninstall = "`"$PsExe`" -NoProfile -ExecutionPolicy Bypass -File `"$AppDir\uninstall.ps1`""
        $size = [int]((Get-ChildItem $AppDir -Recurse -File | Measure-Object Length -Sum).Sum / 1KB)
        New-Item -Path $RegKey -Force | Out-Null
        $values = @{
            DisplayName = 'AutoShortcut'; DisplayVersion = $Version; Publisher = 'alons-dev'
            DisplayIcon = "$AppDir\icon.ico"; InstallLocation = $AppDir
            UninstallString = $uninstall; QuietUninstallString = "$uninstall -Quiet"
            URLInfoAbout = "https://github.com/$Repo"
        }
        foreach ($k in $values.Keys) { New-ItemProperty -Path $RegKey -Name $k -Value $values[$k] -PropertyType String -Force | Out-Null }
        foreach ($k in 'NoModify', 'NoRepair') { New-ItemProperty -Path $RegKey -Name $k -Value 1 -PropertyType DWord -Force | Out-Null }
        New-ItemProperty -Path $RegKey -Name EstimatedSize -Value $size -PropertyType DWord -Force | Out-Null

        # 7. Arrancar y comprobar que sigue viva
        Step 'Arrancando AutoShortcut'
        Start-Process $Conhost -ArgumentList $LaunchArgs -WorkingDirectory $AppDir
        $running = $false
        for ($i = 0; $i -lt 30 -and -not $running; $i++) {
            Start-Sleep -Milliseconds 500
            $running = [bool](Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
                Where-Object { $_.CommandLine -like "*$AppDir\run.ps1*" })
        }
        if (-not $running) { throw 'AutoShortcut no ha arrancado. Mira %APPDATA%\AutoShortcut\error.log si existe.' }

        $hotkey = 'Win+Shift+A'
        $cfg = Join-Path $env:APPDATA 'AutoShortcut\shortcuts.json'
        if (Test-Path $cfg) { try { $hotkey = (Get-Content $cfg -Raw -Encoding UTF8 | ConvertFrom-Json).hotkey } catch { } }

        Write-Host ''
        Write-Host 'Listo. AutoShortcut esta en marcha y se abrira solo al iniciar Windows.' -ForegroundColor Green
        Write-Host "  Abrelo con $hotkey o con clic en su icono de la bandeja (junto al reloj)."
        Write-Host '  Si esa combinacion la usa otra app, cambiala en el boton de la rueda del panel.'
        Write-Host '  Para desinstalar: Configuracion > Aplicaciones > AutoShortcut.'
        Write-Host ''
    }
    catch {
        Write-Host ''
        Write-Host "No se ha podido instalar: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  (linea $($_.InvocationInfo.ScriptLineNumber): $($_.InvocationInfo.Line.Trim()))" -ForegroundColor DarkGray
        Write-Host ''
    }
    finally {
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}
