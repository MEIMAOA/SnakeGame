using System;
using UnityEngine;
using ArknightsTD.Core;
using ArknightsTD.Core.Geometry;

namespace ArknightsTD.SnakeView
{
    /// <summary>
    /// 界面主题。照搬原版 <c>AppSettings.ThemeName</c> 的四个主题
    /// （Windows11 / 深色 / 浅色 / 纯白）与 <c>ThemeBackground/ThemeTextPrimary/
    /// ThemeTextSecondary/ThemeCard/ThemeBorder</c> 的配色规则。
    /// <para>
    /// 原版里游戏内 HUD 固定用 Win11Card/Win11Text，不随主题变化；
    /// 这里保持一致：只有菜单/设置/成就/排行等界面跟随主题。
    /// </para>
    /// </summary>
    public static class UiTheme
    {
        private static AppSettings _s;

        /// <summary>当前设置（懒加载）。</summary>
        public static AppSettings Settings
        {
            get
            {
                if (_s == null) _s = AppSettings.Load();
                return _s;
            }
        }

        private static float _lastRefresh = -999f;

        /// <summary>
        /// 重新从存档载入。带 1 秒节流——调用方（各界面 EnsureStyles、
        /// 战场 Sync）每帧都会调它，若每帧读盘会白白浪费 IO。
        /// </summary>
        public static void Refresh()
        {
            float now = Time.unscaledTime;
            if (now - _lastRefresh < 1f) return;
            _lastRefresh = now;
            _s = AppSettings.Load();
        }

        public static string ThemeName => Settings.ThemeName;
        public static bool IsDark => ThemeName == "深色";

        public static Rgb Background => Settings.ThemeBackground();
        public static Rgb TextPrimary => Settings.ThemeTextPrimary();
        public static Rgb TextSecondary => Settings.ThemeTextSecondary();
        public static Rgb Card => Settings.ThemeCard();
        public static Rgb Border => Settings.ThemeBorder();
        public static Rgb Accent => Settings.AccentColor();

        public static Rgb Danger => Rgb.FromHex(0xE81123);
        public static Rgb Green => Rgb.FromHex(0x2FA84F);
        public static Rgb Gray => Rgb.FromHex(0x6A7280);

        /// <summary>卡片悬停色：深色主题提亮，浅色主题压暗。</summary>
        public static Rgb CardHover
            => IsDark ? Lighten(Card, 0.14) : Darken(Card, 0.06);

        /// <summary>选中/高亮色。</summary>
        public static Rgb Selected => IsDark ? Rgb.FromHex(0x2F6FB0) : Rgb.FromHex(0xCDE4F7);

        /// <summary>面板底（比背景稍亮/稍暗）。</summary>
        public static Rgb PanelBg => IsDark ? Lighten(Background, 0.06) : Darken(Background, 0.03);

        /// <summary>半透明遮罩（暂停/结算用）。</summary>
        public static Rgb Overlay => IsDark
            ? Rgb.FromHex(0x0A0A0C)
            : Rgb.FromHex(0xE8E8EC);

        /// <summary>遮罩下文字在浅色主题需要深色。</summary>
        public static Rgb OverlayText => IsDark ? TextPrimary : Rgb.FromHex(0x161616);

        public static Rgb Lighten(Rgb c, double amount)
        {
            return new Rgb(
                (byte)Math.Min(255, c.R + (255 - c.R) * amount),
                (byte)Math.Min(255, c.G + (255 - c.G) * amount),
                (byte)Math.Min(255, c.B + (255 - c.B) * amount));
        }

        public static Rgb Darken(Rgb c, double amount)
        {
            return new Rgb(
                (byte)(c.R * (1 - amount)),
                (byte)(c.G * (1 - amount)),
                (byte)(c.B * (1 - amount)));
        }
    }
}
