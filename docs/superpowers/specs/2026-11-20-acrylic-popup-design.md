# Acrylic pop-up — design

## Intent

The pop-up should look like the Windows 11 taskbar and Start menu: what is behind it
shows through, blurred, with the theme color on top. Text stays sharp and readable.

## Decisions (the user's)

- **Real acrylic** from Windows (live blur by DWM), not a blurred screenshot.
- **Extra tint**: a layer of the theme background at 50 % over the acrylic, so it
  reads like the taskbar and text stays legible on loud wallpapers.
- **Windows 10 keeps working**: where acrylic isn't available the pop-up is opaque,
  exactly as today, with no setting to touch.

## Approaches considered

| Approach | Trade-off | Outcome |
|---|---|---|
| Simulated acrylic (blurred screenshot as background) | Sharp ClearType text, works on 10 and 11; background doesn't update while open | Rejected by the user |
| Real acrylic (live blur by Windows) | Live blur; every piece of the pop-up must paint with real alpha | **Chosen** |
| Window opacity | Text becomes see-through too, no blur | Discarded |

A throwaway spike (scratchpad) confirmed on build 26300: acrylic shows on the
borderless pop-up, GDI+ text drawn into a premultiplied DIB looks right in dark and
light, and the search `TextBox` renders correctly when repainted through a DIB with
alpha forced to 255.

**Which Windows API.** The documented backdrop (`DWMWA_SYSTEMBACKDROP_TYPE` =
transient window) was tried first. It works when the window is shown by its own
process, but when the pop-up is opened by the global hotkey while another app is in
front (always, in real use) Windows draws a flat gray instead of the blur. The window's
accent policy (`SetWindowCompositionAttribute`, `ACCENT_ENABLE_ACRYLICBLURBEHIND`) blurs
in every case, keeps the rounded corners and honors per-pixel alpha, so it is used,
with the frame extended over the client area. It is undocumented but long-standing
(the taskbar used it); if Windows refuses it the pop-up goes opaque.

## When acrylic is used

Acrylic only when all hold, otherwise opaque (today's look):

- Windows 11 (build >= 22000). Windows 10 has the same blur but laggy and without
  rounded corners, so it stays opaque.
- "Transparency effects" is on (`HKCU\...\Themes\Personalize\EnableTransparency` not 0).
- High contrast is off.
- `AUTOSHORTCUT_NO_ACRYLIC` is not set (escape hatch, also used by tests).
- The DWM calls succeed when the window is created; if they fail the pop-up switches
  to opaque before its first paint.

Only the pop-up uses acrylic. Dialogs (editor, settings) and the right-click menu stay
as they are.

## How it's built

- **Theme** gains `Glass` and `Tint`. `SetGlass(bool)` swaps the colors that must be
  translucent on acrylic (subtle text, hover, pressed, keycaps, borders); foreground,
  input background and accent stay opaque in both modes.
- **One paint helper** (`Glass.Paint`): every pop-up control paints through an
  off-screen 32-bit buffer of the area being repainted, then copies it to the window.
  On acrylic the buffer starts as the tint and keeps its alpha; otherwise it starts as
  the opaque background. This also replaces WinForms double buffering in those controls.
- **Text** (`Glass.Text`): on acrylic GDI+ `DrawString` (grayscale antialiasing, like
  Windows on acrylic); otherwise `TextRenderer` (ClearType), so Windows 10 looks the same.
- **Containers** (form, tab strip, chip bar and its panels) paint the tint too.
- **Labels** in the header become a small drawn label control.
- **Search box**: the inner `TextBox` repaints through a DIB with alpha 255, and
  repaints after the messages where the edit control draws on its own (typing, keys,
  mouse selection, set text/selection, focus).
- **List scrolling**: the native scrollbar can't draw on acrylic, so the list scrolls
  by itself with a thin Windows 11-style overlay bar (thin line, wider on hover, drag
  the thumb, click the track to page, mouse wheel). Same in both modes, so there is a
  single scrolling path.

## Left to the builder

Exact translucent color values (start from the spike's), wheel step, scrollbar sizes.
