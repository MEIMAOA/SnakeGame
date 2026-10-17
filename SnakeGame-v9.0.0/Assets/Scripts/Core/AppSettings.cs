using System;
using System.Collections.Generic;
using ArknightsTD.Core.Geometry;

namespace ArknightsTD.Core
{
    /// <summary>
    /// 应用设置。移植自 WPF 版 <c>AppSettings.cs</c>，字段与默认值<b>逐项对应</b>：
    /// BackgroundStyle / HeadColorHex / BodyColorHex / AccentColorHex / ThemeName /
    /// ResolutionIndex / DisplayMode / FpsTarget / AntiAlias / Vsync / HighFpsEnabled /
    /// SelectedHighFpsIndex / BidirectionalIpc / SpeedBalance / CustomBgEnabled /
    /// CustomBackgroundPath / TowerDefenseEnabled / 窗口几何。
    /// <para>
    /// 与原版的差异（仅为适配引擎，不改变语义）：
    /// <list type="bullet">
    /// <item>颜色用本工程的 <see cref="Rgb"/>，不用 WPF 的 <c>System.Windows.Media.Color</c></item>
    /// <item>持久化改走 <see cref="Store"/>（Unity 下写入 persistentDataPath），
    ///       不用原版的 <c>SecureStorage</c> 加密方案——那个依赖 Windows 机器指纹</item>
    /// <item>序列化用行文本而非 Newtonsoft.Json（工程无该依赖），
    ///       但保留原版的<b>三层降级</b>读取与<b>多份备份</b>思路</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class AppSettings
    {
        // ==================== 外观 ====================

        /// <summary>背景样式索引（0 纯黑 / 1 纯白 / 2 格子 / 3 星空）。</summary>
        public int BackgroundStyle { get; set; } = 2;
        public string HeadColorHex { get; set; } = "#90EE90";
        public string BodyColorHex { get; set; } = "#008000";
        public string AccentColorHex { get; set; } = "#0067C0";

        /// <summary>主题名：Windows11 / 深色 / 浅色 / 纯白。</summary>
        public string ThemeName { get; set; } = "Windows11";

        // ==================== 画面 ====================

        public int ResolutionIndex { get; set; } = 3;
        public int DisplayMode { get; set; } = 2;
        public int FpsTarget { get; set; } = 60;
        public bool AntiAlias { get; set; } = true;
        public bool Vsync { get; set; } = true;
        public bool HighFpsEnabled { get; set; }
        public int SelectedHighFpsIndex { get; set; }

        // ==================== 实验性 ====================

        public bool BidirectionalIpc { get; set; }
        public bool SpeedBalance { get; set; } = true;
        public bool CustomBgEnabled { get; set; } = true;
        public string CustomBackgroundPath { get; set; } = "";
        /// <summary>塔防模式开关（实验性功能，默认关闭）。</summary>
        public bool TowerDefenseEnabled { get; set; } = false;

        // ==================== 窗口几何 ====================

        public double WindowWidth { get; set; } = 1600;
        public double WindowHeight { get; set; } = 900;
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public bool WindowMaximized { get; set; } = false;

        // ==================== 选项表（供设置界面使用）====================

        public static readonly string[] BackgroundStyleNames = { "纯黑", "纯白", "格子", "星空" };
        public static readonly string[] ThemeNames = { "Windows11", "深色", "浅色", "纯白" };
        public static readonly string[] DisplayModeNames = { "窗口", "无边框", "全屏" };
        public static readonly int[] FpsTargetOptions = { 30, 60, 120, 144, 240 };
        public static readonly string[] ResolutionNames =
        {
            "1280×720", "1366×768", "1600×900", "1920×1080", "2560×1440", "3840×2160"
        };
        public static readonly int[][] Resolutions =
        {
            new[] { 1280, 720 }, new[] { 1366, 768 }, new[] { 1600, 900 },
            new[] { 1920, 1080 }, new[] { 2560, 1440 }, new[] { 3840, 2160 },
        };

        // ==================== 加载 / 保存 ====================

        private const string FileName = "app_settings.json";
        private const string PrefsKey = "ArknightsTD.AppSettings";

        /// <summary>最近一次加载的日志（调试与设置界面显示用）。</summary>
        public static string LastLoadLog { get; private set; } = "";

        /// <summary>
        /// 加载设置。保留原版的多层降级思路：
        /// ① 主文件（Unity 走 PlayerPrefs/persistentDataPath）② 备份文件 ③ 默认值。
        /// </summary>
        public static AppSettings Load()
        {
            // ① 主文件
            try
            {
                var raw = Store.Load(FileName, PrefsKey);
                if (!string.IsNullOrEmpty(raw))
                {
                    var s = Deserialize(raw);
                    if (s != null)
                    {
                        LastLoadLog = "从主文件读取成功";
                        return s;
                    }
                    LastLoadLog = "主文件内容无法解析";
                }
                else LastLoadLog = "主文件为空";
            }
            catch (Exception ex) { LastLoadLog = "主文件读取异常：" + ex.Message; }

            // ② 备份文件
            try
            {
                var raw = Store.Load(FileName + ".backup", PrefsKey + ".backup");
                if (!string.IsNullOrEmpty(raw))
                {
                    var s = Deserialize(raw);
                    if (s != null)
                    {
                        LastLoadLog += "；已从备份恢复";
                        try { s.Save(); } catch { }
                        return s;
                    }
                }
            }
            catch (Exception ex) { LastLoadLog += "；备份读取异常：" + ex.Message; }

            // ③ 兜底默认值
            LastLoadLog += "；使用默认设置";
            return new AppSettings();
        }

        /// <summary>保存设置（主文件 + 备份，双写）。</summary>
        public void Save()
        {
            var raw = Serialize();
            try { Store.Save(FileName, PrefsKey, raw); } catch { }
            try { Store.Save(FileName + ".backup", PrefsKey + ".backup", raw); } catch { }
        }

        /// <summary>序列化成行文本（键=值，一行一项）。</summary>
        public string Serialize()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("v1");
            sb.AppendLine($"BackgroundStyle={BackgroundStyle}");
            sb.AppendLine($"HeadColorHex={HeadColorHex}");
            sb.AppendLine($"BodyColorHex={BodyColorHex}");
            sb.AppendLine($"AccentColorHex={AccentColorHex}");
            sb.AppendLine($"ThemeName={ThemeName}");
            sb.AppendLine($"ResolutionIndex={ResolutionIndex}");
            sb.AppendLine($"DisplayMode={DisplayMode}");
            sb.AppendLine($"FpsTarget={FpsTarget}");
            sb.AppendLine($"AntiAlias={(AntiAlias ? 1 : 0)}");
            sb.AppendLine($"Vsync={(Vsync ? 1 : 0)}");
            sb.AppendLine($"HighFpsEnabled={(HighFpsEnabled ? 1 : 0)}");
            sb.AppendLine($"SelectedHighFpsIndex={SelectedHighFpsIndex}");
            sb.AppendLine($"BidirectionalIpc={(BidirectionalIpc ? 1 : 0)}");
            sb.AppendLine($"SpeedBalance={(SpeedBalance ? 1 : 0)}");
            sb.AppendLine($"CustomBgEnabled={(CustomBgEnabled ? 1 : 0)}");
            sb.AppendLine($"CustomBackgroundPath={CustomBackgroundPath}");
            sb.AppendLine($"TowerDefenseEnabled={(TowerDefenseEnabled ? 1 : 0)}");
            sb.AppendLine($"WindowWidth={WindowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.AppendLine($"WindowHeight={WindowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.AppendLine($"WindowLeft={WindowLeft.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.AppendLine($"WindowTop={WindowTop.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.AppendLine($"WindowMaximized={(WindowMaximized ? 1 : 0)}");
            return sb.ToString();
        }

        /// <summary>从行文本恢复；坏行跳过，不抛异常。</summary>
        public static AppSettings Deserialize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var s = new AppSettings();
            var lines = raw.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq);
                string v = line.Substring(eq + 1);

                switch (k)
                {
                    case "BackgroundStyle": s.BackgroundStyle = ParseInt(v, s.BackgroundStyle); break;
                    case "HeadColorHex": s.HeadColorHex = v; break;
                    case "BodyColorHex": s.BodyColorHex = v; break;
                    case "AccentColorHex": s.AccentColorHex = v; break;
                    case "ThemeName": s.ThemeName = v; break;
                    case "ResolutionIndex": s.ResolutionIndex = ParseInt(v, s.ResolutionIndex); break;
                    case "DisplayMode": s.DisplayMode = ParseInt(v, s.DisplayMode); break;
                    case "FpsTarget": s.FpsTarget = ParseInt(v, s.FpsTarget); break;
                    case "AntiAlias": s.AntiAlias = v == "1"; break;
                    case "Vsync": s.Vsync = v == "1"; break;
                    case "HighFpsEnabled": s.HighFpsEnabled = v == "1"; break;
                    case "SelectedHighFpsIndex": s.SelectedHighFpsIndex = ParseInt(v, s.SelectedHighFpsIndex); break;
                    case "BidirectionalIpc": s.BidirectionalIpc = v == "1"; break;
                    case "SpeedBalance": s.SpeedBalance = v == "1"; break;
                    case "CustomBgEnabled": s.CustomBgEnabled = v == "1"; break;
                    case "CustomBackgroundPath": s.CustomBackgroundPath = v; break;
                    case "TowerDefenseEnabled": s.TowerDefenseEnabled = v == "1"; break;
                    case "WindowWidth": s.WindowWidth = ParseDouble(v, s.WindowWidth); break;
                    case "WindowHeight": s.WindowHeight = ParseDouble(v, s.WindowHeight); break;
                    case "WindowLeft": s.WindowLeft = ParseDouble(v, s.WindowLeft); break;
                    case "WindowTop": s.WindowTop = ParseDouble(v, s.WindowTop); break;
                    case "WindowMaximized": s.WindowMaximized = v == "1"; break;
                }
            }
            return s;
        }

        private static int ParseInt(string v, int fallback)
            => int.TryParse(v, out int r) ? r : fallback;

        private static double ParseDouble(string v, double fallback)
            => double.TryParse(v, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out double r) ? r : fallback;

        // ==================== 颜色访问器（对应原版同名方法）====================

        public Rgb HeadColor() => ParseHex(HeadColorHex, Rgb.FromHex(0x90EE90));
        public Rgb BodyColor() => ParseHex(BodyColorHex, Rgb.FromHex(0x008000));
        public Rgb AccentColor() => ParseHex(AccentColorHex, Rgb.FromHex(0x0067C0));

        /// <summary>主题背景色（原版 ThemeBackground）。</summary>
        public Rgb ThemeBackground()
        {
            switch (ThemeName)
            {
                case "深色": return Rgb.FromHex(0x202024);
                case "浅色": return Rgb.FromHex(0xF8F8FA);
                case "纯白": return Rgb.FromHex(0xFFFFFF);
                default: return Rgb.FromHex(0xF5F5F7);
            }
        }

        public Rgb ThemeTextPrimary()
        {
            switch (ThemeName)
            {
                case "深色": return Rgb.FromHex(0xF0F0F0);
                default: return Rgb.FromHex(0x161616);
            }
        }

        public Rgb ThemeTextSecondary()
        {
            switch (ThemeName)
            {
                case "深色": return Rgb.FromHex(0xA0A0A0);
                default: return Rgb.FromHex(0x606060);
            }
        }

        public Rgb ThemeCard()
        {
            switch (ThemeName)
            {
                case "深色": return Rgb.FromHex(0x303036);
                case "纯白": return Rgb.FromHex(0xFFFFFF);
                default: return Rgb.FromHex(0xFFFFFF);
            }
        }

        public Rgb ThemeBorder()
        {
            switch (ThemeName)
            {
                case "深色": return Rgb.FromHex(0x505058);
                default: return Rgb.FromHex(0xD2D2D2);
            }
        }

        /// <summary>解析 #RRGGBB；失败时返回兜底色。</summary>
        public static Rgb ParseHex(string hex, Rgb fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            var t = hex.Trim();
            if (t.StartsWith("#")) t = t.Substring(1);
            if (t.Length != 6) return fallback;
            if (!uint.TryParse(t, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out uint v))
                return fallback;
            return Rgb.FromHex(v);
        }

        /// <summary>把颜色转成 #RRGGBB。</summary>
        public static string ToHex(Rgb c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
