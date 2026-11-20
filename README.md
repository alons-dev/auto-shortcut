# AutoShortcut

Un panel de atajos de teclado para Windows. Pulsa **Win + Shift + A** y aparece un pop-up minimalista en la esquina inferior derecha, con bordes redondeados al estilo Windows 11. Haz clic en un atajo y se ejecuta.

- **Sin dependencias**: solo usa lo que ya trae Windows 10 y 11 (Windows PowerShell y .NET Framework). Nada de AutoHotkey, NuGet ni instaladores de terceros.
- **Tu lista**: añade, edita o quita atajos desde el propio pop-up.
- **Clic = acción**: un atajo puede pulsar una combinación de teclas, abrir un programa, una carpeta o una web.
- **Tecla configurable**: Win + Shift + A por defecto, cámbiala por la que quieras.

## Instalar

Abre PowerShell (no hace falta ser administrador) y ejecuta:

```powershell
irm https://raw.githubusercontent.com/alons-dev/auto-shortcut/main/install.ps1 | iex
```

El instalador:

1. Descarga el código de este repositorio (zip de GitHub).
2. Lo copia a `%LOCALAPPDATA%\Programs\AutoShortcut` y comprueba que compila.
3. Crea un acceso directo en el menú Inicio y otro en Inicio de Windows, para que arranque con el PC.
4. Lo registra en **Configuración › Aplicaciones**, desde donde se puede desinstalar.
5. Lo arranca: aparece su icono en la bandeja, junto al reloj.

Para actualizar, vuelve a ejecutar el mismo comando. Tu lista de atajos no se toca.

### Por qué no hay un `.exe`

Windows 11 trae **Smart App Control**, que bloquea los `.exe` sin firma digital que no conoce, y un `.exe` compilado en tu equipo siempre es "desconocido". Por eso AutoShortcut no se compila a `.exe`: lo arranca **Windows PowerShell** (firmado por Microsoft), que compila el código C# en memoria con `Add-Type` (ver [`run.ps1`](run.ps1)). Es la misma idea que AutoHotkey: un programa firmado que ejecuta tu código.

El acceso directo lo lanza con `conhost.exe --headless`, la consola de Windows en modo sin ventana, para que no se vea ninguna consola (tampoco si Windows Terminal es tu terminal predeterminada). Al arrancar tarda uno o dos segundos, porque compila el código.

## Usar

| Acción | Cómo |
|---|---|
| Abrir / cerrar el pop-up | `Win + Shift + A`, o clic en el icono de la bandeja |
| Ejecutar un atajo | Clic sobre él |
| Añadir un atajo | Botón **+** del pop-up |
| Editar, mover o borrar un atajo | Clic derecho sobre él |
| Cambiar la tecla que abre el pop-up | Botón de la rueda (⚙) del pop-up |
| Cerrar | `Esc` o clic fuera del pop-up |

Al crear o editar un atajo de teclas, o al cambiar la tecla del pop-up, puedes escribir la combinación (`Ctrl+Alt+T`) o pulsar **Grabar** y hacerla con el teclado. Grabar funciona incluso con las que Windows se reserva, como `Win+Shift+S`.

## Tu lista de atajos

Se guarda en `%APPDATA%\AutoShortcut\shortcuts.json` y se crea la primera vez con unos atajos de ejemplo. Lo normal es editarla desde el pop-up, pero también puedes hacerlo a mano (clic derecho en el icono de la bandeja › **Editar shortcuts.json**). Los cambios hechos a mano se aplican la próxima vez que abras el pop-up.

```json
{
  "hotkey": "Win+Shift+A",
  "shortcuts": [
    { "name": "Capturar una zona", "keys": "Win+Shift+S" },
    { "name": "Bloc de notas", "run": "notepad.exe" },
    { "name": "Mis documentos", "run": "%USERPROFILE%\\Documents" },
    { "name": "GitHub", "run": "https://github.com" },
    { "name": "Ping a Google", "run": "cmd.exe", "args": "/k ping google.com" }
  ]
}
```

- `hotkey`: la combinación que abre el pop-up. Debe llevar `Win`, `Ctrl` o `Alt`. Si otra app ya la usa, el pop-up lo indica en su cabecera y puedes abrirlo con clic en el icono de la bandeja mientras eliges otra.
- `keys`: al hacer clic se pulsa esa combinación en la ventana que tenías delante (útil para recordar atajos de Windows o de tus apps).
- `run`: al hacer clic se abre ese programa, archivo, carpeta o URL, como si le hicieras doble clic. Admite variables como `%USERPROFILE%`.
- `args` (opcional, con `run`): argumentos para el programa.

En `\` dentro del JSON se escribe doble (`\\`). Si el archivo tiene un error, la app avisa y sigue con la última lista buena; no lo sobrescribe.

**Teclas que entiende:** `Win`, `Ctrl`, `Alt`, `Shift`, letras, números, `F1`–`F24`, `Up` `Down` `Left` `Right` (o `↑ ↓ ← →`), `Enter`, `Esc`, `Tab`, `Space`, `Home`, `End`, `PageUp`, `PageDown`, `Delete`, `Backspace`, `PrintScreen`, `.` `,` `-`. También en español: `Supr`, `Inicio`, `Fin`, `RePág`, `AvPág`, `Espacio`, `Retroceso`, `ImprPant`.

## Cómo está construido

```
auto-shortcut/
├── src/             Código C# (WinForms) de la aplicación
├── run.ps1          Arranca la app compilando src/ en memoria (lo que usa la instalación)
├── install.ps1      Descarga, comprueba e instala (el comando de arriba)
├── uninstall.ps1    Quita la app, sus accesos directos y su registro
├── build.ps1        Compila src/ → bin/AutoShortcut.exe (solo para desarrollo)
├── README.md
└── LICENSE
```

Los `.ps1` están escritos solo en ASCII (sin tildes) a propósito: Windows PowerShell 5.1 lee los scripts sin BOM como ANSI y las tildes saldrían rotas.

Las piezas principales:

- **Hotkey global** — `RegisterHotKey` de la API Win32 (`user32.dll`). Windows avisa a la app con un mensaje `WM_HOTKEY` aunque esté en segundo plano.
- **Pop-up** — un formulario WinForms sin bordes, colocado sobre la barra de tareas a la derecha. Las esquinas redondeadas se las pide a Windows 11 con `DwmSetWindowAttribute` (la misma API que usan las ventanas del sistema).
- **Lista de atajos** — JSON leído con `JavaScriptSerializer` (incluido en .NET Framework) y escrito a mano para que quede legible. Sin librerías externas.
- **Ejecutar** — al abrir el pop-up se recuerda qué ventana estaba delante. Al hacer clic se le devuelve el foco y se "pulsa" la combinación con `SendInput`; programas, carpetas y URLs se abren con `Process.Start`.
- **Filas** — un control propio dibujado con GDI+: nombre a la izquierda y las teclas como "keycaps" a la derecha.
- **Grabar combinaciones** — un hook de teclado de bajo nivel (`SetWindowsHookEx` con `WH_KEYBOARD_LL`) recibe las teclas antes que los atajos de Windows. Solo está activo mientras grabas: se suelta con la primera combinación, con `Esc`, a los 10 segundos o si la ventana pierde el foco.
- **Estilo Windows 11** — iconos de la fuente *Segoe Fluent Icons*, menús y cuadros de texto dibujados con los colores del tema, y barra de título oscura pedida a DWM.

| Archivo | Qué hace |
|---|---|
| `Program.cs` | Punto de entrada: una sola instancia, nitidez en pantallas con zoom, registro de errores |
| `TrayApp.cs` | Icono de la bandeja; une configuración, hotkey, pop-up, acciones y editor |
| `HotkeyWindow.cs` | Hotkey global con `RegisterHotKey` |
| `PopupForm.cs` | El pop-up: posición, esquinas redondeadas, lista, botones y menú |
| `ShortcutRow.cs` / `IconButton.cs` | Una fila clicable / un botón de icono |
| `EditorForm.cs` / `SettingsForm.cs` | Ventanas para editar un atajo y cambiar la tecla |
| `ThemedDialog.cs` / `InputBox.cs` / `MenuRenderer.cs` | Ventanas, cuadros de texto y menús con el tema |
| `KeyField.cs` / `KeyRecorder.cs` | Campo de combinación con el botón Grabar |
| `Config.cs` / `Shortcut.cs` | Leer y guardar `shortcuts.json` |
| `KeyCombo.cs` | Texto `"Win+Shift+A"` ⇄ teclas |
| `KeySender.cs` / `ActionRunner.cs` | Ejecutar un atajo |
| `Theme.cs` / `Ui.cs` | Colores claro/oscuro, escala y dibujo |

Si algo falla dentro de la app, se apunta en `%APPDATA%\AutoShortcut\error.log`.

### Desarrollar

```powershell
git clone https://github.com/alons-dev/auto-shortcut.git
cd auto-shortcut
powershell -ExecutionPolicy Bypass -File .\run.ps1      # arranca la app desde el código
powershell -ExecutionPolicy Bypass -File .\install.ps1  # instala tu copia local
.\build.ps1                                             # compila con csc.exe → bin\AutoShortcut.exe
```

`run.ps1` necesita **Windows PowerShell** (`powershell`), no PowerShell 7 (`pwsh`), porque usa .NET Framework. Si ya tienes AutoShortcut instalado y en marcha, ciérralo antes (menú del icono › Salir): solo puede haber una copia abierta.

`bin\AutoShortcut.exe` funciona si Smart App Control está desactivado; si está activado, Windows puede bloquearlo.

## Desinstalar

Desde **Configuración › Aplicaciones › AutoShortcut › Desinstalar**, o con:

```powershell
& "$env:LOCALAPPDATA\Programs\AutoShortcut\uninstall.ps1"
```

Tu lista se queda en `%APPDATA%\AutoShortcut` por si vuelves. Para borrarla también, añade `-RemoveSettings` al comando de arriba. Si ya desinstalaste sin esa opción (el desinstalador se borra a sí mismo), borra la carpeta directamente:

```powershell
Remove-Item "$env:APPDATA\AutoShortcut" -Recurse -Force
```

## Limitaciones

- Windows no deja que una app normal envíe teclas a ventanas abiertas como administrador.
- Si otra app ya usa la combinación elegida, Windows no deja registrarla: el pop-up te avisará para que elijas otra.
- `Ctrl+Alt+Supr` y `Win+L` las gestiona Windows en un nivel protegido y no se pueden simular.
- Las teclas `Fn` del portátil (brillo, volumen del fabricante…) no son teclas de Windows y no se pueden enviar.

## Licencia

[MIT](LICENSE)
