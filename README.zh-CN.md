# SnakeGame

**语言：** [English](README.md) | [简体中文](README.zh-CN.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Русский](README.ru.md)

**SnakeGame** 是一款现代贪吃蛇游戏合集，包含五种贪吃蛇模式与完整的明日方舟式 3D 塔防模式，使用 **Unity / 团结引擎 2022.3** 构建。同一套代码同时发布 **Windows（x64 / x86）**、**Android（arm64-v8a）** 与网页（WebGL）版本。

[![版本](https://img.shields.io/badge/version-9.1.0--beta-00b7c3?style=flat-square)](https://github.com/MEIMAOA/SnakeGame/releases)
[![平台](https://img.shields.io/badge/platforms-Windows%20%7C%20Android%20%7C%20WebGL-0078D4?style=flat-square)](#平台与下载)
[![引擎](https://img.shields.io/badge/engine-Unity%20%2F%20Tuanjie%202022.3-000000?style=flat-square)](https://unity.com/)
[![许可证](https://img.shields.io/badge/license-MIT-00cc6a?style=flat-square)](LICENSE)

---

## 项目简介

- 五种独立的贪吃蛇模式，外加完整的 3D 塔防模式，集成于一个应用。
- 全部界面由 IMGUI 自绘（不依赖第三方 UI 框架），采用 Win11 风格设计。
- 完整多语言：简体中文、英语、德语，运行时即时切换。
- 桌面与移动端输入：Windows 使用鼠标/键盘，Android 使用虚拟摇杆与触屏按钮。
- 画面与性能设置：分辨率、显示模式、帧率上限、抗锯齿、垂直同步。
- 成就、统计与排行榜数据本地保存。
- 同一份源码可构建 Windows、Android 与 WebGL 客户端。

## 游戏模式

| 模式 | 说明 |
|---|---|
| 经典模式 | 吃食物变长，不撞墙、不撞自己，达到 3000 分获胜 |
| 淘汰之王 | 49 条 AI 蛇同场，按 F 挑战附近对手决斗 |
| 团队 4v4 | 与蓝队 AI 协作占领中央区域 |
| 极限模式 | 20 个敌人，存活三分钟，倍率持续增长 |
| 搜打撤 | 搜索容器、收集物资，到达绿色撤离点 |
| 塔防（3D） | 明日方舟式部署、阻挡、技能、费用与十个关卡 |

## 平台与下载

| 平台 | 安装包 | 说明 |
|---|---|---|
| Windows x64 | `SnakeGame-9.1.0-beta-Windows64.zip` | 解压后运行 `SnakeGame.exe` |
| Windows x86 | `SnakeGame-9.1.0-beta-Windows32.zip` | 适用于 32 位 Windows |
| Android | `SnakeGame-9.1.0-beta-Android.apk` | arm64-v8a，Android 8.0（API 26）及以上 |
| WebGL | `SnakeGame-9.1.0-beta-WebGL.zip` | 浏览器版本的静态托管包 |

下载地址：<https://github.com/MEIMAOA/SnakeGame/releases/tag/v9.1.0-beta>

## WebGL 在线试玩

真实游戏（与桌面端同一套代码）可直接在浏览器中运行：

- 在线试玩：<https://meimaoa.github.io/snakegame-web/play/index.html>
- 项目主页：<https://meimaoa.github.io/snakegame-web/>

首次加载约需 15 到 20 秒（WebGL 数据文件约 18 MB）。建议使用桌面浏览器并配合鼠标键盘操作。

## 操作方式

| 平台 | 转向 | 加速 | 暂停 | 其他 |
|---|---|---|---|---|
| Windows | 鼠标瞄准，或 WASD / 方向键 | Ctrl 或 Shift | P 或 Esc | F 动作键，空格切换速度，R 重开 |
| Android | 屏幕任意处拖动（虚拟摇杆） | 按住右下角加速按钮 | 右上角暂停按钮 | 加速按钮上方的动作按钮 |
| WebGL | 鼠标瞄准，或 WASD / 方向键 | Ctrl 或 Shift | P 或 Esc | 与 Windows 相同 |

## 技术栈

- C# 与 Unity / 团结引擎 2022.3 LTS。
- 所有界面、面板与对话框均由 IMGUI 自绘。
- Windows Standalone（x64 / x86）、Android ARM64（IL2CPP）、WebGL 构建。
- 内置 Noto Sans SC 字体（SIL 开放字体许可证），确保网页端中文正常显示。
- 使用 GitHub Actions 自动化发布。

## 从源码构建

环境要求：团结引擎 2022.3.62t16，并安装 Windows、Android、WebGL 构建支持模块。

```powershell
$editor = "C:\Path\To\Tuanjie\Editor\Tuanjie.exe"
$project = "C:\Path\To\SnakeGame"

# Windows x64 / x86
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildWindows
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildWindows32

# Android（APK）
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildAndroid

# WebGL
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildWebGL

# 启动链路验证（181 项）
& $editor -batchmode -quit -projectPath $project -executeMethod ArknightsTD.EditorTools.VerifyLaunch.Run
```

构建产物输出到 `Build/Windows64`、`Build/Windows32`、`Build/Android` 与 `Build/WebGL`。

## 仓库结构

| 路径 | 说明 |
|---|---|
| `Assets/Scripts/Core` | 设置、多语言、进度与共享数据 |
| `Assets/Scripts/View` | 界面、菜单、HUD、显示设置与平台适配 |
| `Assets/Scripts/Snake` | 五种模式的贪吃蛇模拟 |
| `Assets/Scripts/Battle` | 塔防模拟逻辑 |
| `Assets/Scripts/Editor` | 构建、验证与预览工具 |
| `Assets/StreamingAssets/Saukiya-Arknights` | 引入的开源原型源码，不参与编译 |
| `External/Saukiya-Arknights` | 同一开源项目的源码副本，供参考 |
| `SnakeGame-v9.0.0` | 归档的 9.0.0 版本产物 |
| `SnakeGameWpf - 7.0.0` | 归档的旧版 WPF 7.0.0 产物 |

## 多语言

游戏界面支持简体中文（`zh-CN`）、英语（`en-US`）与德语（`de-DE`），可在 `设置 - 画面设置 - 语言` 中切换并立即保存。所有文本通过统一的字典翻译层解析。

## 第三方引用与致谢

- **Saukiya/Arknights**（<https://github.com/Saukiya/Arknights>，作者 **Saukiya**）：实验性「改进塔防模式」引用的明日方舟式塔防原型。该仓库为不含场景与美术资源的 Unity 脚本原型，且未声明开源许可证，版权归原作者所有。源码以只读形式存放于 `External/Saukiya-Arknights` 与 `Assets/StreamingAssets/Saukiya-Arknights`，不参与游戏编译。详情见 `THIRD-PARTY-NOTICES.md`。
- **Noto Sans SC**（作者 Google）：为 WebGL 端中文渲染内置的字体，采用 SIL 开放字体许可证 1.1。许可证见 `Assets/Resources/Fonts/OFL.txt`。

## 许可证

本项目使用 MIT 许可证，完整文本见 [LICENSE](LICENSE)。

## 相关链接

- 代码仓库：<https://github.com/MEIMAOA/SnakeGame>
- 发布页面：<https://github.com/MEIMAOA/SnakeGame/releases>
- 项目主页与在线试玩：<https://meimaoa.github.io/snakegame-web/>
- 作者主页：<https://github.com/MEIMAOA>