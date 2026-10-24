# SnakeGame

**Языки:** [English](README.md) | [简体中文](README.zh-CN.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Русский](README.ru.md)

**SnakeGame** — современный сборник игр «Змейка» с пятью режимами змейки и полноценным 3D-режимом tower defense в стиле Arknights, разработанный на **Unity / Tuanjie 2022.3**. Один и тот же код собирается для **Windows (x64 / x86)**, **Android (arm64-v8a)** и веба (WebGL).

[![Версия](https://img.shields.io/badge/version-9.1.1--beta-00b7c3?style=flat-square)](https://github.com/MEIMAOA/SnakeGame/releases)
[![Платформы](https://img.shields.io/badge/platforms-Windows%20%7C%20Android%20%7C%20WebGL-0078D4?style=flat-square)](#платформы-и-загрузки)
[![Движок](https://img.shields.io/badge/engine-Unity%20%2F%20Tuanjie%202022.3-000000?style=flat-square)](https://unity.com/)
[![Лицензия](https://img.shields.io/badge/license-MIT-00cc6a?style=flat-square)](LICENSE)

---

## Обзор

- Пять независимых режимов змейки плюс полноценный 3D-режим tower defense в одном приложении.
- Полностью самописный интерфейс на IMGUI без сторонних UI-фреймворков, в стиле Windows 11.
- Полная локализация: упрощённый китайский, английский и немецкий языки с переключением во время работы.
- Управление на ПК и мобильных устройствах: мышь и клавиатура в Windows, виртуальный джойстик и сенсорные кнопки в Android.
- Настройки изображения и производительности: разрешение, режим экрана, ограничение fps, сглаживание, вертикальная синхронизация.
- Достижения, статистика и таблицы лидеров сохраняются локально.
- Из одного дерева исходников собираются клиенты Windows, Android и WebGL.

## Режимы игры

| Режим | Описание |
|---|---|
| Классический | Ешьте еду, растите и наберите 3000 очков, не врезавшись в стену или в себя |
| Battle Royale (Timed) | 49 змеек ИИ, клавиша F вызывает ближайшего соперника на дуэль |
| Команда 4v4 | Взаимодействуйте с синими змейками ИИ и захватите центральную зону |
| Экстремальный | 20 противников, продержитесь три минуты, пока растёт множитель |
| Эвакуация | Обыскивайте контейнеры, собирайте добычу и доберитесь до зелёной точки эвакуации |
| Tower Defense (3D) | Стиль Arknights: расстановка, блокировка, навыки, стоимость и десять уровней |

## Платформы и загрузки

| Платформа | Пакет | Примечание |
|---|---|---|
| Windows x64 | `SnakeGame-9.1.1-beta-Windows64.zip` | Распакуйте и запустите `SnakeGame.exe` |
| Windows x86 | `SnakeGame-9.1.1-beta-Windows32.zip` | Для 32-битной Windows |
| Android | `SnakeGame-9.1.1-beta-Android.apk` | arm64-v8a, Android 8.0 (API 26) или новее |
| WebGL | `SnakeGame-9.1.1-beta-WebGL.zip` | Статический пакет браузерной версии |

Загрузка: <https://github.com/MEIMAOA/SnakeGame/releases/tag/v9.1.1-beta>

## Играть онлайн (WebGL)

Настоящая игра (тот же код, что и в версии для ПК) работает в браузере:

- Онлайн-демо: <https://meimaoa.github.io/snakegame-web/play/index.html>
- Сайт проекта: <https://meimaoa.github.io/snakegame-web/>

Первая загрузка занимает около 15–20 секунд, так как файл WebGL весит примерно 18 МБ. Рекомендуется настольный браузер с мышью и клавиатурой.

## Управление

| Платформа | Поворот | Ускорение | Пауза | Другое |
|---|---|---|---|---|
| Windows | Наведение мышью или WASD / стрелки | Ctrl или Shift | P или Esc | Клавиша F для действий, пробел меняет скорость, R перезапускает |
| Android | Перетаскивание в любом месте экрана (виртуальный джойстик) | Удерживайте кнопку ускорения справа внизу | Кнопка паузы справа вверху | Кнопка действия над кнопкой ускорения |
| WebGL | Наведение мышью или WASD / стрелки | Ctrl или Shift | P или Esc | Как в Windows |

## Технологии

- C# и Unity / Tuanjie 2022.3 LTS.
- Все экраны, панели и диалоги рисуются через IMGUI.
- Сборки Windows Standalone x64 и x86, Android ARM64 с IL2CPP, сборка WebGL.
- Встроенный шрифт Noto Sans SC (SIL Open Font License) для корректного отображения китайского текста в вебе.
- GitHub Actions для автоматизации релизов.

## Сборка из исходников

Требуется: Tuanjie 2022.3.62t16 с модулями поддержки сборки Windows, Android и WebGL.

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

# Проверка цепочки запуска (181 проверка)
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.VerifyLaunch.Run
```

Результаты сохраняются в `Build/Windows64`, `Build/Windows32`, `Build/Android` и `Build/WebGL`.

## Структура репозитория

| Путь | Описание |
|---|---|
| `Assets/Scripts/Core` | Настройки, локализация, прогресс и общие данные |
| `Assets/Scripts/View` | Интерфейс, меню, HUD, настройки изображения и поддержка платформ |
| `Assets/Scripts/Snake` | Симуляция змейки для всех пяти режимов |
| `Assets/Scripts/Battle` | Симуляция tower defense |
| `Assets/Scripts/Editor` | Инструменты сборки, проверки и предпросмотра |
| `Assets/StreamingAssets/Saukiya-Arknights` | Встроенный прототип с открытым кодом, не компилируется |
| `External/Saukiya-Arknights` | Копия исходников того же проекта для справки |
| `SnakeGame-v9.0.0` | Архивные артефакты версии 9.0.0 |
| `SnakeGameWpf - 7.0.0` | Архивные артефакты старой версии WPF 7.0.0 |

## Локализация

Интерфейс поддерживает упрощённый китайский (`zh-CN`), английский (`en-US`) и немецкий (`de-DE`). Язык меняется в разделе `Настройки - Изображение - Язык` и сохраняется сразу. Все строки проходят через единый словарный слой перевода.

## Сторонние проекты и благодарности

- **Saukiya/Arknights** (<https://github.com/Saukiya/Arknights>), автор **Saukiya**: прототип tower defense в стиле Arknights, на который ссылается экспериментальный режим «улучшенный tower defense». Репозиторий представляет собой прототип скриптов Unity без сцен и графических ресурсов и не содержит лицензии открытого исходного кода, поэтому все права принадлежат автору. Исходники размещены только для справки в `External/Saukiya-Arknights` и `Assets/StreamingAssets/Saukiya-Arknights` и не компилируются в игру. Подробности в `THIRD-PARTY-NOTICES.md`.
- **Noto Sans SC** от Google: встроенный шрифт для отображения китайского текста в сборке WebGL, лицензия SIL Open Font License 1.1. См. `Assets/Resources/Fonts/OFL.txt`.

## Лицензия

Проект распространяется по лицензии MIT. Полный текст находится в [LICENSE](LICENSE).

## Ссылки

- Репозиторий: <https://github.com/MEIMAOA/SnakeGame>
- Релизы: <https://github.com/MEIMAOA/SnakeGame/releases>
- Сайт проекта и онлайн-демо: <https://meimaoa.github.io/snakegame-web/>
- Автор: <https://github.com/MEIMAOA>
