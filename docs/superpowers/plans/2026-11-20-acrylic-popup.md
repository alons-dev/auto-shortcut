# Acrylic pop-up Implementation Plan

> Executed inline (native). Steps use checkbox syntax.

**Goal:** Real Windows 11 acrylic with a 50 % theme tint behind the pop-up; opaque as today wherever acrylic isn't available.

**Spec:** docs/superpowers/specs/2026-11-20-acrylic-popup-design.md

## Global Constraints

- C# 5 only, compiled by Add-Type in Windows PowerShell 5.1 (warnings are errors).
- Acrylic iff build >= 22000 (Windows 11), EnableTransparency != 0 (missing counts as on), no high contrast, no `AUTOSHORTCUT_NO_ACRYLIC`, DWM calls succeed.
- Tint = theme background at alpha 128 (dark 32,32,32; light 243,243,243).
- Opaque mode keeps TextRenderer/ClearType text and today's colors.

## Review Focus

- Caret and selection in the search box on acrylic (the edit control draws outside WM_PAINT).
- Right-click menu open: pop-up inactive, acrylic must not break or leave garbage.
- Scrolling: wheel, thumb drag, track click, scroll kept on refresh, clamped on resize.
- Opaque fallback on this machine via `AUTOSHORTCUT_NO_ACRYLIC=1` looks like before.
- Repaint of partial areas (hover changes) lines up with the rest (clip offset).

### Task 1: When to use acrylic (Theme)

- [ ] Tests in tests/LogicTests.cs: `AcrylicRules` (19045 -> false, 22000 -> true, 22621 -> true, transparency 0 -> false, missing -> true, high contrast -> false) and `GlassColorsSwap` (SetGlass(true): Glass, Subtle has alpha < 255, Tint alpha 128; SetGlass(false): equals Theme.Current() colors).
- [ ] Run: fails (methods missing).
- [ ] Theme: `Glass`, `Tint`, `SetGlass(bool)`, `static bool CanUseAcrylic(int build, int enableTransparency, bool highContrast)`, `static bool AcrylicAvailable()`.
- [ ] Run: 21 passed.

### Task 2: Paint helper and text

- [ ] Test `TextFormatMapping`: `Glass.FormatFor(flags)` maps HorizontalCenter/Right, VerticalCenter, SingleLine->NoWrap, EndEllipsis->EllipsisCharacter, NoPrefix->HotkeyPrefix.None.
- [ ] src/Glass.cs: `Paint(PaintEventArgs, Theme, Action<Graphics>)`, `Text(...)`, `FormatFor(...)`; Ui: `TryAcrylic(IntPtr)`.

### Task 3: Controls

- [ ] IconButton, TabStrip(+TabButton), ChipBar(+Chip, GlassPanel), InputBox(+GlassTextBox), TextLabel, PopupForm background/labels/DWM, ShortcutList own scrolling + overlay bar.
- [ ] Logic tests green; UI suites (test-v2, test-menu-dismiss) green with acrylic and with `AUTOSHORTCUT_NO_ACRYLIC=1`.

### Task 4: Look

- [ ] Screenshots of the real pop-up over a colorful backdrop: dark, light, opaque.
