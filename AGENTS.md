# AGENTS.md

## Project

Always rebuild and restart the app when you finish changes.
SnipTool är ett Windows‑verktyg för extremt snabb skärmklippning i burst‑flöden. Fokus är 5–30 klipp i följd utan friktion: global hotkey → markera → autosave. Appen är en WPF tray‑app (.NET 8) med en transparent overlay för rektangelmarkering, enklast möjliga settings‑fönster, och automatisk filstruktur/namngivning.

### Core flow
- Global hotkey → overlay → spara direkt till disk
- Session/burst: alla klipp går till samma datum/segmentmapp
- Toast med snabbåtgärder (Undo/Open folder)
- Valfritt kopiera till clipboard

### Current defaults (dev)
- Save root: `D:\Screenshots`
- Filnamn: `{date}_{time}_{counter}.png` (legacy `HHmmss_###.png` stöds)
- Hotkeys: `PrintScreen` (rectangle), `Alt+PrintScreen` (window), `Ctrl+PrintScreen` (fullscreen), `Shift+PrintScreen` (repeat last)
- Burst default: inaktiv (sparar till root); New burst skapar ny sessionmapp
- Dotnet path: `D:\dotnet`
- PrintScreen note: Windows Snipping Tool kan behöva stängas av i Settings → Accessibility → Keyboard

## Skills
A skill is a set of local instructions to follow that is stored in a `SKILL.md` file. Below is the list of skills that can be used. Each entry includes a name, description, and file path so you can open the source for full instructions when using a specific skill.

### Available skills
- tapscribe-test-runner: Build and restart TapScribe for local testing. Use when the user says a new version is ready for testing, asks to start/restart the app after code changes, or wants the latest build launched. (file: C:/Users/OlleSöderqvist/.codex/skills/tapscribe-test-runner/SKILL.md)
- skill-creator: Guide for creating effective skills. Use when users want to create or update a skill that extends Codex's capabilities. (file: C:/Users/OlleSöderqvist/.codex/skills/.system/skill-creator/SKILL.md)
- skill-installer: Install Codex skills into $CODEX_HOME/skills from a curated list or a GitHub repo path. Use when a user asks to list installable skills, install a curated skill, or install a skill from another repo. (file: C:/Users/OlleSöderqvist/.codex/skills/.system/skill-installer/SKILL.md)
- gemini-imagegen: Generate AI images via OpenRouter Gemini (text-to-image and image-to-image). Use when the user asks for AI image generation, image transformations, or style transfer. (file: C:/Users/OlleSöderqvist/.codex/skills/gemini-imagegen/SKILL.md)
