# SnakeGame

**Languages:** [English](README.md) | [简体中文](README.zh-CN.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Русский](README.ru.md)

**SnakeGame** is a modern Snake game collection with five snake modes and a complete Arknights-style 3D tower defense mode, built with **Unity / Tuanjie 2022.3**. The same codebase ships for **Windows (x64 / x86)**, **Android (arm64-v8a)** and the web (WebGL).

[![Version](https://img.shields.io/badge/version-9.1.1--beta-00b7c3?style=flat-square)](https://github.com/MEIMAOA/SnakeGame/releases)
[![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20Android%20%7C%20WebGL-0078D4?style=flat-square)](#platforms-and-downloads)
[![Engine](https://img.shields.io/badge/engine-Unity%20%2F%20Tuanjie%202022.3-000000?style=flat-square)](https://unity.com/)
[![License](https://img.shields.io/badge/license-MIT-00cc6a?style=flat-square)](LICENSE)

---

## Overview

- Five independent snake modes plus a full 3D tower defense mode in one application.
- Custom-drawn IMGUI user interface (no external UI framework), Win11-inspired design.
- Full localization: Simplified Chinese, English and German, switchable at runtime.
- Desktop and mobile input: mouse/keyboard on Windows, virtual joystick and touch buttons on Android.
- Display and performance settings: resolution, display mode, frame limit, anti-aliasing, vertical sync.
- Achievements, statistics and rankings stored locally.
- The same source tree builds the Windows, Android and WebGL clients.

## Game Modes

| Mode | Description |
|---|---|
| Classic | Eat food, grow, reach 3000 points without hitting a wall or yourself |
| Battle Royale (Timed) | 49 AI snakes, press F to challenge a nearby opponent to a duel |
| Team 4v4 | Team up with blue AI snakes and capture the central zone |
| Extreme | 20 enemies, survive three minutes while the multiplier grows |
| Extraction | Search containers, collect loot, reach the green extraction point |
| Tower Defense (3D) | Arknights-style deployment, blocking, skills, cost and ten levels |

## Platforms and Downloads

| Platform | Package | Notes |
|---|---|---|
| Windows x64 | `SnakeGame-9.1.1-beta-Windows64.zip` | Unzip and run `SnakeGame.exe` |
| Windows x86 | `SnakeGame-9.1.1-beta-Windows32.zip` | For 32-bit Windows |
| Android | `SnakeGame-9.1.1-beta-Android.apk` | arm64-v8a, Android 8.0 (API 26) or newer |
| WebGL | `SnakeGame-9.1.1-beta-WebGL.zip` | Static hosting package of the browser build |

Download: <https://github.com/MEIMAOA/SnakeGame/releases/tag/v9.1.1-beta>

## WebGL Online Play

The real game (the same code as the desktop build) runs in the browser:

- Online demo: <https://meimaoa.github.io/snakegame-web/play/index.html>
- Project site: <https://meimaoa.github.io/snakegame-web/>

First load takes roughly 15 to 20 seconds because the WebGL data file is about 18 MB. A desktop browser with mouse and keyboard is recommended.

## Controls

| Platform | Steering | Boost | Pause | Other |
|---|---|---|---|---|
| Windows | Mouse aim, or WASD / arrow keys | Ctrl or Shift | P or Esc | F action key, Space to change speed, R to restart |
| Android | Drag anywhere on the screen (virtual joystick) | Hold the boost button at the bottom right | Pause button at the top right | Action button above the boost button |
| WebGL | Mouse aim, or WASD / arrow keys | Ctrl or Shift | P or Esc | Same as Windows |

## Tech Stack

- C# with Unity / Tuanjie 2022.3 LTS.
- IMGUI custom rendering for every screen, panel and dialog.
- Windows Standalone builds for x64 and x86, Android ARM64 with IL2CPP, WebGL build.
- Bundled Noto Sans SC font (SIL Open Font License) so Chinese text renders correctly on the web.
- GitHub Actions for release automation.

## Build from Source

Requirements: Tuanjie 2022.3.62t16 with the Windows, Android and WebGL build support modules.

```powershell
$editor = "C:\Path\To\Tuanjie\Editor\Tuanjie.exe"
$project = "C:\Path\To\SnakeGame"

# Windows x64 / x86
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildWindows
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildWindows32

# Android (APK)
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildAndroid

# WebGL
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildWebGL

# Launch-chain verification (181 checks)
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.VerifyLaunch.Run
```

Outputs are written to `Build/Windows64`, `Build/Windows32`, `Build/Android` and `Build/WebGL`.

## Repository Layout

| Path | Description |
|---|---|
| `Assets/Scripts/Core` | Settings, localization, progress and shared data |
| `Assets/Scripts/View` | UI, menus, HUD, display setup and platform helpers |
| `Assets/Scripts/Snake` | Snake simulation for all five modes |
| `Assets/Scripts/Battle` | Tower defense simulation |
| `Assets/Scripts/Editor` | Build, verification and preview tools |
| `Assets/StreamingAssets/Saukiya-Arknights` | Vendored open-source prototype, not compiled |
| `External/Saukiya-Arknights` | Source copy of the same project for reference |
| `SnakeGame-v9.0.0` | Archived 9.0.0 release artifacts |
| `SnakeGameWpf - 7.0.0` | Archived legacy WPF 7.0.0 artifacts |

## Localization

The in-game interface supports Simplified Chinese (`zh-CN`), English (`en-US`) and German (`de-DE`). Language can be changed in `Settings - Display - Language` and is saved immediately. Strings are resolved through a single dictionary-based translation layer.

## Third-Party and Credits

- **Saukiya/Arknights** (<https://github.com/Saukiya/Arknights>) by **Saukiya**: an Arknights-style tower defense prototype referenced by the experimental "Improved Tower Defense" option. The repository is a Unity script prototype without scenes or art assets and declares no open-source license, so all rights remain with its author. The source is vendored under `External/Saukiya-Arknights` and `Assets/StreamingAssets/Saukiya-Arknights` for reference only and is not compiled into the game. See `THIRD-PARTY-NOTICES.md` for details.
- **Noto Sans SC** by Google: bundled for CJK text rendering on WebGL, licensed under the SIL Open Font License 1.1. See `Assets/Resources/Fonts/OFL.txt`.

## License

This project is released under the MIT License. See [LICENSE](LICENSE) for the full text.

## Links

- Repository: <https://github.com/MEIMAOA/SnakeGame>
- Releases: <https://github.com/MEIMAOA/SnakeGame/releases>
- Project site and online demo: <https://meimaoa.github.io/snakegame-web/>
- Author: <https://github.com/MEIMAOA>
