using System.Collections.Generic;
using ArknightsTD.Core.Geometry;

namespace ArknightsTD.Snake
{
    /// <summary>背景样式（原版 BackgroundStyleOptions）。</summary>
    public enum SnakeBackground { SolidBlack, SolidWhite, Grid, Starfield }

    /// <summary>
    /// 贪吃蛇的美术风格配置。
    /// <para>
    /// 完整移植 WPF 版的可选外观（MainWindow 里的 HeadColorOptions /
    /// BodyColorOptions / BackgroundStyleOptions / AccentColors）：
    /// 黑底 + 纯色圆是原版最基本的视觉特征，蛇头与蛇身颜色可换、背景有 4 种样式。
    /// </para>
    /// <para>
    /// 注意：<b>塔防模式不走这套风格</b>（原版 MainWindow.DrawHUD 里
    /// <c>if (GameMode == "towerdefense") return;</c>，塔防有自己的 3D 表现）。
    /// </para>
    /// </summary>
    public static class SnakeSkin
    {
        // ---- 可选颜色（顺序与原版一致）----

        public static readonly Rgb[] HeadColors =
        {
            Rgb.FromHex(0x90EE90),   // LightGreen
            Rgb.FromHex(0xFFFF00),   // Yellow
            Rgb.FromHex(0xFFA500),   // Orange
            Rgb.FromHex(0xFFC0CB),   // Pink
            Rgb.FromHex(0x00FFFF),   // Cyan
            Rgb.FromHex(0xFFFFFF),   // White
        };
        public static readonly string[] HeadColorNames = { "浅绿", "黄", "橙", "粉", "青", "白" };

        public static readonly Rgb[] BodyColors =
        {
            Rgb.FromHex(0x008000),   // Green
            Rgb.FromHex(0x006400),   // DarkGreen
            Rgb.FromHex(0x0000FF),   // Blue
            Rgb.FromHex(0x800080),   // Purple
            Rgb.FromHex(0xA52A2A),   // Brown
            Rgb.FromHex(0x808080),   // Gray
        };
        public static readonly string[] BodyColorNames = { "绿", "深绿", "蓝", "紫", "棕", "灰" };

        public static readonly string[] BackgroundNames = { "纯黑", "纯白", "格子", "星空" };

        /// <summary>原版的强调色（Win11 风格）。</summary>
        public static readonly Rgb[] AccentColors =
        {
            Rgb.FromHex(0x0067C0),   // win11 蓝
            Rgb.FromHex(0xE81123),   // 红
            Rgb.FromHex(0x008A00),   // 绿
            Rgb.FromHex(0xFF8C00),   // 橙
            Rgb.FromHex(0x6B69D6),   // 紫
            Rgb.FromHex(0x00B2D2),   // 青
        };

        /// <summary>食物色板（原版 CreateFood 用的 6 色）。</summary>
        public static readonly Rgb[] FoodColors =
        {
            Rgb.FromHex(0xFF0000),   // 红
            Rgb.FromHex(0xFFA500),   // 橙
            Rgb.FromHex(0xFFFF00),   // 黄
            Rgb.FromHex(0xFFC0CB),   // 粉
            Rgb.FromHex(0x800080),   // 紫
            Rgb.FromHex(0x00FFFF),   // 青
        };

        // ---- 当前选择（可被设置界面修改）----

        public static int HeadColorIndex { get; private set; }
        public static int BodyColorIndex { get; private set; }
        public static int BackgroundStyleIndex { get; private set; }
        public static int AccentColorIndex { get; private set; }

        public static Rgb HeadColor => CustomHeadSet ? CustomHead : HeadColors[HeadColorIndex];
        public static Rgb BodyColor => CustomBodySet ? CustomBody : BodyColors[BodyColorIndex];
        public static SnakeBackground Background => (SnakeBackground)BackgroundStyleIndex;
        public static Rgb AccentColor => AccentColors[AccentColorIndex];

        public static void SetHeadColor(int index)
            => HeadColorIndex = Clamp(index, HeadColors.Length);

        public static void SetBodyColor(int index)
            => BodyColorIndex = Clamp(index, BodyColors.Length);

        public static void SetBackgroundStyle(int index)
            => BackgroundStyleIndex = Clamp(index, BackgroundNames.Length);

        public static void SetAccentColor(int index)
            => AccentColorIndex = Clamp(index, AccentColors.Length);

        /// <summary>
        /// 直接按颜色值指定蛇头色（用于设置界面的 hex 值）。
        /// 若与预置色板中的某一项相同，则同时同步索引，便于循环切换继续工作。
        /// </summary>
        public static void SetHeadColorRgb(Rgb c)
        {
            for (int i = 0; i < HeadColors.Length; i++)
                if (HeadColors[i].R == c.R && HeadColors[i].G == c.G && HeadColors[i].B == c.B)
                { HeadColorIndex = i; return; }
            CustomHead = c;
            CustomHeadSet = true;
        }

        /// <summary>直接按颜色值指定蛇身色。</summary>
        public static void SetBodyColorRgb(Rgb c)
        {
            for (int i = 0; i < BodyColors.Length; i++)
                if (BodyColors[i].R == c.R && BodyColors[i].G == c.G && BodyColors[i].B == c.B)
                { BodyColorIndex = i; return; }
            CustomBody = c;
            CustomBodySet = true;
        }

        /// <summary>自定义蛇头色（不在预置色板内时生效）。</summary>
        public static Rgb CustomHead { get; private set; }
        public static bool CustomHeadSet { get; private set; }
        /// <summary>自定义蛇身色（不在预置色板内时生效）。</summary>
        public static Rgb CustomBody { get; private set; }
        public static bool CustomBodySet { get; private set; }

        private static int Clamp(int v, int count)
            => v < 0 ? 0 : (v >= count ? count - 1 : v);

        // ---- 原版固定的界面色 ----

        /// <summary>Win11 卡片底色（浅色）。</summary>
        public static readonly Rgb Win11Card = Rgb.FromHex(0xF3F3F3);
        /// <summary>Win11 主文字色。</summary>
        public static readonly Rgb Win11Text = Rgb.FromHex(0x161616);
        /// <summary>Win11 次要文字色。</summary>
        public static readonly Rgb Win11TextSecondary = Rgb.FromHex(0x606060);
        /// <summary>浅灰背景。</summary>
        public static readonly Rgb LightGrayBg = Rgb.FromHex(0xF5F5F5);

        /// <summary>背景基色（原版是 Brushes.Black）。</summary>
        public static Rgb BackgroundBase
            => Background == SnakeBackground.SolidWhite ? Rgb.FromHex(0xFFFFFF) : Rgb.FromHex(0x000000);

        /// <summary>该背景下文字应该用深色还是浅色。</summary>
        public static bool BackgroundIsLight => Background == SnakeBackground.SolidWhite;
    }
}
