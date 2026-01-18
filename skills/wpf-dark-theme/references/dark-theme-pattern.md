# SnipTool Dark Theme Pattern (WPF)

## Palette (Dark)
Use blue‑leaning dark tones (avoid pure black):
- AppBackgroundColor: #0F1724
- SurfaceBackgroundColor: #141F2F
- AppTextColor: #E6EEF9
- AppMutedTextColor: #9FB2CC
- AppBorderColor: #233145
- AccentColor: #4DA3FF
- AccentDarkColor: #2B7FD8
- InputBackgroundColor: #162236
- ToggleTrackColor: #2A3A55
- ToggleThumbColor: #E6EEF9
- ToastBackgroundColor: #162236
- ToastButtonBackgroundColor: #1D2A40
- ToastButtonBorderColor: #2A3A55
- OverlayBackgroundColor: #60101824
- SelectionFillColor: #30FFFFFF

## Required WPF Resource Keys
Keep these keys aligned across `Themes/Dark.xaml` and `Themes/Light.xaml`:
- AppBackground, SurfaceBackground, AppText, AppMutedText, AppBorder
- Accent, AccentDark
- InputBackground
- ToggleTrack, ToggleThumb
- ToastBackground, ToastButtonBackground, ToastButtonBorder
- OverlayBackground, SelectionFill

## App.xaml style rules
- `TextBlock` Foreground -> `AppText`
- `SectionHeader` Foreground -> `AppText`
- `CheckBox` Foreground -> `AppText`
- `Hyperlink` Foreground -> `Accent`
- `TextBox` Background -> `InputBackground`

## Dark title bar (native)
- Keep the standard title bar for minimize/close/drag.
- Apply DWM dark mode and caption color to **every window** (main + dialogs), after the window is shown.
- Use DWM attributes:
  - `DWMWA_USE_IMMERSIVE_DARK_MODE` (20, fallback 19)
  - `DWMWA_CAPTION_COLOR` set to `AppBackground`

## Contrast checks
- Headings must not be black on dark backgrounds.
- Cards use `SurfaceBackground` with `AppBorder`.
- Toggle track/accents remain readable on `AppBackground`.
