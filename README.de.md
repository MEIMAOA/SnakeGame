# SnakeGame

**Sprachen:** [English](README.md) | [简体中文](README.zh-CN.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Русский](README.ru.md)

**SnakeGame** ist eine moderne Snake-Spielsammlung mit fünf Snake-Modi und einem vollständigen 3D-Tower-Defense-Modus im Arknights-Stil, entwickelt mit **Unity / Tuanjie 2022.3**. Derselbe Quellcode wird für **Windows (x64 / x86)**, **Android (arm64-v8a)** und das Web (WebGL) veröffentlicht.

[![Version](https://img.shields.io/badge/version-9.1.1--beta-00b7c3?style=flat-square)](https://github.com/MEIMAOA/SnakeGame/releases)
[![Plattformen](https://img.shields.io/badge/platforms-Windows%20%7C%20Android%20%7C%20WebGL-0078D4?style=flat-square)](#plattformen-und-downloads)
[![Engine](https://img.shields.io/badge/engine-Unity%20%2F%20Tuanjie%202022.3-000000?style=flat-square)](https://unity.com/)
[![Lizenz](https://img.shields.io/badge/license-MIT-00cc6a?style=flat-square)](LICENSE)

---

## Überblick

- Fünf eigenständige Snake-Modi plus ein vollständiger 3D-Tower-Defense-Modus in einer Anwendung.
- Vollständig selbst gezeichnete IMGUI-Oberfläche ohne externes UI-Framework, im Stil von Windows 11.
- Vollständige Lokalisierung: vereinfachtes Chinesisch, Englisch und Deutsch, zur Laufzeit umschaltbar.
- Eingabe für Desktop und Mobilgeräte: Maus und Tastatur unter Windows, virtueller Joystick und Touch-Tasten unter Android.
- Anzeige- und Leistungseinstellungen: Auflösung, Anzeigemodus, Bildratenlimit, Kantenglättung, V-Sync.
- Erfolge, Statistiken und Ranglisten werden lokal gespeichert.
- Derselbe Quellbaum erzeugt Windows-, Android- und WebGL-Clients.

## Spielmodi

| Modus | Beschreibung |
|---|---|
| Klassisch | Futter fressen, wachsen und 3000 Punkte erreichen, ohne Wand oder sich selbst zu treffen |
| Battle Royale (Timed) | 49 KI-Schlangen, F fordert einen Gegner in der Nähe zum Duell |
| Team 4v4 | Mit blauen KI-Schlangen die zentrale Zone einnehmen |
| Extrem | 20 Gegner, drei Minuten überleben, während der Multiplikator steigt |
| Extraktion | Container durchsuchen, Beute sammeln und den grünen Extraktionspunkt erreichen |
| Tower Defense (3D) | Arknights-Stil mit Aufstellung, Blockade, Fähigkeiten, Kosten und zehn Leveln |

## Plattformen und Downloads

| Plattform | Paket | Hinweis |
|---|---|---|
| Windows x64 | `SnakeGame-9.1.1-beta-Windows64.zip` | Entpacken und `SnakeGame.exe` starten |
| Windows x86 | `SnakeGame-9.1.1-beta-Windows32.zip` | Für 32-Bit-Windows |
| Android | `SnakeGame-9.1.1-beta-Android.apk` | arm64-v8a, Android 8.0 (API 26) oder neuer |
| WebGL | `SnakeGame-9.1.1-beta-WebGL.zip` | Statisches Hosting-Paket der Browserversion |

Download: <https://github.com/MEIMAOA/SnakeGame/releases/tag/v9.1.1-beta>

## WebGL im Browser spielen

Das echte Spiel (gleicher Code wie die Desktop-Version) läuft im Browser:

- Online-Demo: <https://meimaoa.github.io/snakegame-web/play/index.html>
- Projektseite: <https://meimaoa.github.io/snakegame-web/>

Der erste Ladevorgang dauert etwa 15 bis 20 Sekunden, da die WebGL-Datei rund 18 MB groß ist. Ein Desktop-Browser mit Maus und Tastatur wird empfohlen.

## Steuerung

| Plattform | Lenken | Boost | Pause | Weiteres |
|---|---|---|---|---|
| Windows | Mauszielen oder WASD / Pfeiltasten | Strg oder Umschalt | P oder Esc | F für Aktionen, Leertaste für Tempo, R für Neustart |
| Android | Irgendwo auf dem Bildschirm ziehen (virtueller Joystick) | Boost-Taste unten rechts halten | Pause-Taste oben rechts | Aktionstaste über der Boost-Taste |
| WebGL | Mauszielen oder WASD / Pfeiltasten | Strg oder Umschalt | P oder Esc | Wie unter Windows |

## Technik

- C# mit Unity / Tuanjie 2022.3 LTS.
- Alle Bildschirme, Panels und Dialoge werden mit IMGUI selbst gezeichnet.
- Windows-Standalone für x64 und x86, Android ARM64 mit IL2CPP, WebGL-Build.
- Gebündelte Schriftart Noto Sans SC (SIL Open Font License), damit chinesischer Text im Web korrekt dargestellt wird.
- GitHub Actions für die Release-Automatisierung.

## Aus dem Quellcode bauen

Voraussetzungen: Tuanjie 2022.3.62t16 mit den Build-Support-Modulen für Windows, Android und WebGL.

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

# Verifikation der Startkette (181 Prüfungen)
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.VerifyLaunch.Run
```

Die Ergebnisse liegen in `Build/Windows64`, `Build/Windows32`, `Build/Android` und `Build/WebGL`.

## Repository-Struktur

| Pfad | Beschreibung |
|---|---|
| `Assets/Scripts/Core` | Einstellungen, Lokalisierung, Fortschritt und gemeinsame Daten |
| `Assets/Scripts/View` | Oberfläche, Menüs, HUD, Anzeigeeinstellungen und Plattformhilfen |
| `Assets/Scripts/Snake` | Snake-Simulation für alle fünf Modi |
| `Assets/Scripts/Battle` | Simulation des Tower Defense |
| `Assets/Scripts/Editor` | Werkzeuge für Build, Verifikation und Vorschau |
| `Assets/StreamingAssets/Saukiya-Arknights` | Eingebundener Open-Source-Prototyp, wird nicht kompiliert |
| `External/Saukiya-Arknights` | Quellkopie desselben Projekts als Referenz |
| `SnakeGame-v9.0.0` | Archivierte Artefakte der Version 9.0.0 |
| `SnakeGameWpf - 7.0.0` | Archivierte Artefakte der alten WPF-Version 7.0.0 |

## Lokalisierung

Die Benutzeroberfläche unterstützt vereinfachtes Chinesisch (`zh-CN`), Englisch (`en-US`) und Deutsch (`de-DE`). Die Sprache lässt sich unter `Einstellungen - Anzeige - Sprache` ändern und wird sofort gespeichert. Alle Texte werden über eine einheitliche wörterbuchbasierte Übersetzungsschicht aufgelöst.

## Drittanbieter und Danksagung

- **Saukiya/Arknights** (<https://github.com/Saukiya/Arknights>) von **Saukiya**: ein Tower-Defense-Prototyp im Arknights-Stil, auf den die experimentelle Option "Verbesserter Tower-Defense-Modus" verweist. Das Repository ist ein Unity-Skriptprototyp ohne Szenen und Grafikressourcen und enthält keine Open-Source-Lizenz; alle Rechte liegen beim Autor. Der Quellcode liegt nur zur Referenz unter `External/Saukiya-Arknights` und `Assets/StreamingAssets/Saukiya-Arknights` und wird nicht in das Spiel kompiliert. Weitere Angaben in `THIRD-PARTY-NOTICES.md`.
- **Noto Sans SC** von Google: eingebundene Schriftart für die chinesische Darstellung im WebGL-Build, lizenziert unter der SIL Open Font License 1.1. Siehe `Assets/Resources/Fonts/OFL.txt`.

## Lizenz

Dieses Projekt steht unter der MIT-Lizenz. Der vollständige Text befindet sich in [LICENSE](LICENSE).

## Links

- Repository: <https://github.com/MEIMAOA/SnakeGame>
- Releases: <https://github.com/MEIMAOA/SnakeGame/releases>
- Projektseite und Online-Demo: <https://meimaoa.github.io/snakegame-web/>
- Autor: <https://github.com/MEIMAOA>
