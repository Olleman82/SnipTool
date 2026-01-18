---
name: wpf-dark-theme
description: Create or refine a WPF dark theme using the SnipTool dark‑blue pattern. Use when a user asks for dark mode, contrast fixes, theme resource dictionaries, or window chrome adjustments in WPF apps (App.xaml, Themes/Dark.xaml, Window styling).
---

# WPF Dark Theme (SnipTool pattern)

## Overview
Apply the SnipTool dark‑blue theme pattern to a WPF app with consistent resources, readable contrast, and a dark native title bar.

## Workflow
1. Read `references/dark-theme-pattern.md` for palette + resource keys.
2. Ensure theme dictionaries exist and are loaded via `App.ApplyTheme(bool)`.
3. Update `App.xaml` styles so TextBlock/SectionHeader/CheckBox/Hyperlink use theme brushes.
4. Keep the standard window title bar (minimize/close/drag) and set DWM caption color in dark mode.
5. Verify contrast in headers and cards; ensure no black text on dark surfaces.

## Output checklist
- Dark palette is blue‑leaning (not pure black).
- Standard title bar is present and dark in dark mode.
- All headings/text use `AppText`/`AppMutedText`.
- Cards, inputs, and toasts use theme brushes.

## References
- `references/dark-theme-pattern.md` (palette + WPF resource keys)
