using System.Collections.Generic;
using ArknightsTD.Core.Geometry;
using UnityEngine;

namespace ArknightsTD.SnakeView
{
    /// <summary>
    /// Windows 11（Fluent）风格 UI 绘制工具：圆角卡片、圆角按钮、分隔线、强调条。
    /// <para>
    /// 做法：运行时程序化生成 9 宫格圆角贴图（每张按「填充色 + 圆角半径」缓存），
    /// 再用 <see cref="GUIStyle"/> 的 border 做九宫格拉伸——这样任意尺寸都是圆角，
    /// 不依赖任何美术资源，也不需要 Canvas/Prefab。
    /// </para>
    /// <para>
    /// 颜色统一取自 <see cref="UiTheme"/>（Win11 / 深色 / 浅色 / 纯白主题都能正确取色），
    /// 因此这里只负责"形状与质感"，配色仍由主题决定。
    /// </para>
    /// </summary>
    public static class Win11Ui
    {
        private static readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, GUIStyle> _styleCache = new Dictionary<string, GUIStyle>();

        /// <summary>圆角半径（1080p 基准，调用方按 UI 缩放乘系数）。</summary>
        public const int Radius = 8;

        // ---- 语义色（Win11 Fluent）----
        public static Color CardFill => C(UiTheme.Card);
        public static Color CardHover => C(UiTheme.CardHover);
        public static Color Accent => C(UiTheme.Accent);
        public static Color Border => C(UiTheme.Border);
        public static Color TextMain => C(UiTheme.TextPrimary);
        public static Color TextDim => C(UiTheme.TextSecondary);
        public static Color Danger => C(UiTheme.Danger);
        public static Color Success => C(UiTheme.Green);
        public static Color Subtle => new Color(CardFill.r, CardFill.g, CardFill.b, 0.55f);

        private static Color C(Rgb c) => new Color32(c.R, c.G, c.B, 255);

        // ==================================================================
        //  贴图 / 样式
        // ==================================================================

        /// <summary>
        /// 亚克力（Acrylic）圆角贴图：半透明底 + 细微噪点 + 顶部高光，
        /// 模拟 Windows 11 的毛玻璃质感（IMGUI 无法真模糊，这是视觉近似）。
        /// </summary>
        private static Texture2D AcrylicTex(Color baseColor, int radius, float opacity)
        {
            int r = Mathf.Clamp(radius, 2, 24);
            long key = (long)(baseColor.r * 255) * 73856093L ^ (long)(baseColor.g * 255) * 19349663L
                     ^ (long)(baseColor.b * 255) * 83492791L ^ (long)r * 2654435761L ^ (long)(opacity * 100);
            string kk = "acr_" + key;
            Texture2D tex;
            if (_texCache.TryGetValue(kk, out tex) && tex != null) return tex;

            int size = r * 2 + 4;
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var rnd = new System.Random((int)(key & 0x7FFFFFFF));
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = 0f, dy = 0f;
                    if (x < r) dx = r - x - 0.5f; else if (x >= size - r) dx = x - (size - r) + 0.5f;
                    if (y < r) dy = r - y - 0.5f; else if (y >= size - r) dy = y - (size - r) + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float cov = Mathf.Clamp01(r - dist + 0.5f);
                    if (cov <= 0f) { tex.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }

                    // 噪点 ±3%、顶部高光 +6%
                    float noise = ((float)rnd.NextDouble() - 0.5f) * 0.06f;
                    float topLight = Mathf.Lerp(0.06f, 0f, (float)y / Mathf.Max(1, size - 1));
                    var c = new Color(
                        Mathf.Clamp01(baseColor.r + noise + topLight),
                        Mathf.Clamp01(baseColor.g + noise + topLight),
                        Mathf.Clamp01(baseColor.b + noise + topLight),
                        Mathf.Clamp01(baseColor.a * opacity) * cov);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            _texCache[kk] = tex;
            return tex;
        }

        /// <summary>取一张圆角纯色贴图（9 宫格用）。</summary>
        private static Texture2D RoundTex(Color color, int radius, bool borderOnly, Color borderColor)
        {
            int r = Mathf.Clamp(radius, 2, 24);
            string key = $"{(int)(color.r * 255)}_{(int)(color.g * 255)}_{(int)(color.b * 255)}_" +
                         $"{(int)(color.a * 255)}_{r}_{(borderOnly ? 1 : 0)}_" +
                         $"{(int)(borderColor.r * 255)}_{(int)(borderColor.g * 255)}_{(int)(borderColor.b * 255)}";
            Texture2D tex;
            if (_texCache.TryGetValue(key, out tex) && tex != null) return tex;

            int size = r * 2 + 4;
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 圆角覆盖率（到最近圆角圆心的距离）
                    float dx = 0f, dy = 0f;
                    if (x < r) dx = r - x - 0.5f; else if (x >= size - r) dx = x - (size - r) + 0.5f;
                    if (y < r) dy = r - y - 0.5f; else if (y >= size - r) dy = y - (size - r) + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float cov = Mathf.Clamp01(r - dist + 0.5f);
                    if (cov <= 0f) { tex.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }

                    Color c = color;
                    if (borderOnly)
                    {
                        // 只画 1px 圆角描边
                        bool onEdge = cov < 0.85f;
                        c = onEdge ? borderColor : new Color(borderColor.r, borderColor.g, borderColor.b, 0f);
                    }
                    c.a *= Mathf.Clamp01(cov);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            _texCache[key] = tex;
            return tex;
        }

        private static GUIStyle RoundStyle(Texture2D tex, int radius)
        {
            string key = tex.GetInstanceID() + "_" + radius;
            GUIStyle st;
            if (_styleCache.TryGetValue(key, out st) && st != null) return st;

            int b = Mathf.Clamp(radius + 1, 2, 24);
            st = new GUIStyle
            {
                border = new RectOffset(b, b, b, b),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { background = tex },
            };
            _styleCache[key] = st;
            return st;
        }

        // ==================================================================
        //  绘制 API
        // ==================================================================

        /// <summary>亚克力圆角块（半透明 + 噪点 + 高光）。</summary>
        public static void Acrylic(Rect rect, Color color, int radius = Radius, float opacity = 0.86f)
        {
            var tex = AcrylicTex(color, radius, opacity);
            GUI.Box(rect, GUIContent.none, RoundStyle(tex, radius));
        }

        /// <summary>圆角填充块（Fluent 卡片）。</summary>
        public static void Panel(Rect rect, Color color, int radius = Radius)
            => GUI.Box(rect, GUIContent.none, RoundStyle(RoundTex(color, radius, false, color), radius));

        /// <summary>圆角卡片 + 细描边（Win11 卡片质感）。</summary>
        public static void Card(Rect rect, int radius = Radius, bool hovered = false)
        {
            var fill = hovered ? CardHover : CardFill;
            // 亚克力玻璃底 + 1px 描边 + 顶部高光（Windows 11 卡片质感）
            Acrylic(rect, fill, radius, hovered ? 0.94f : 0.86f);
            Panel(rect, Border, radius);
            var inner = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
            if (inner.width > 2 && inner.height > 2) Acrylic(inner, fill, Mathf.Max(2, radius - 1),
                hovered ? 0.94f : 0.86f);
            var top = new Rect(rect.x + radius, rect.y + 1, rect.width - radius * 2, 1);
            if (top.width > 0) Panel(top, new Color(1f, 1f, 1f, 0.16f), 1);
        }

        /// <summary>圆角按钮。返回 true 表示本帧被点击。</summary>
        public static bool Button(Rect rect, string text, GUIStyle textStyle,
            bool primary = false, bool enabled = true)
        {
            bool hover = enabled && rect.Contains(Event.current.mousePosition);
            var fill = primary ? Accent : (hover ? CardHover : CardFill);
            if (!enabled) fill = new Color(fill.r, fill.g, fill.b, 0.45f);

            Panel(rect, fill);
            if (primary)
            {
                // 主按钮：外圈柔光
                var glow = new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);
                Panel(glow, new Color(Accent.r, Accent.g, Accent.b, 0.22f));
                Panel(rect, Accent);
            }

            var st = new GUIStyle(textStyle);
            st.alignment = TextAnchor.MiddleCenter;
            st.normal.textColor = primary ? Color.white : TextMain;
            if (!enabled) st.normal.textColor = TextDim;
            GUI.Label(rect, text, st);

            return enabled && Event.current.type == EventType.MouseDown
                && rect.Contains(Event.current.mousePosition);
        }

        /// <summary>一行可点击列表项（Win11 列表行，悬停/选中用浅底）。</summary>
        public static bool Row(Rect rect, bool selected, bool enabled = true)
        {
            bool hover = enabled && rect.Contains(Event.current.mousePosition);
            if (selected) Panel(rect, new Color(Accent.r, Accent.g, Accent.b, 0.22f), 6);
            else if (hover) Panel(rect, CardHover, 6);

            // 选中项左侧强调条
            if (selected) Panel(new Rect(rect.x, rect.y + 6, 3, rect.height - 12),
                Accent, 2);

            return enabled && Event.current.type == EventType.MouseDown
                && rect.Contains(Event.current.mousePosition);
        }

        /// <summary>1px 分隔线（Win11 Divider）。</summary>
        public static void Divider(Rect rect)
        {
            var t = RoundTex(Border, 2, false, Border);
            GUI.DrawTexture(rect, t);
        }

        /// <summary>顶部强调条（卡片标题上方 3px）。</summary>
        public static void AccentBar(Rect card, Color? color = null)
            => Panel(new Rect(card.x + 12, card.y, card.width - 24, 3), color ?? Accent, 2);

        /// <summary>
        /// 画一段"自适应"文字：从 maxSize 开始逐步缩小字号，直到能放进 rect；
        /// wordWrap 打开时按高度约束换行。用于卡片标题/说明，彻底避免文字溢出。
        /// </summary>
        public static void FittedLabel(Rect rect, string text, int maxSize, int minSize,
            Color color, bool bold = false, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = true)
        {
            var st = new GUIStyle(GUI.skin.label)
            {
                wordWrap = wrap,
                alignment = anchor,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            };
            st.normal.textColor = color;

            for (int size = maxSize; size >= minSize; size--)
            {
                st.fontSize = size;
                var need = st.CalcSize(new GUIContent(text));
                if (need.x <= rect.width + 0.5f && (!wrap ? need.y <= rect.height + 0.5f : need.y <= rect.height + 0.5f))
                {
                    GUI.Label(rect, text, st);
                    return;
                }
            }
            st.fontSize = minSize;
            GUI.Label(rect, text, st);
        }

        /// <summary>标题文字样式（Segoe UI 风味：粗体大字）。</summary>
        public static GUIStyle TitleStyle(int size = 52)
        {
            string key = "title" + size;
            GUIStyle st;
            if (_styleCache.TryGetValue(key, out st) && st != null) return st;
            st = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            st.normal.textColor = TextMain;
            _styleCache[key] = st;
            return st;
        }

        /// <summary>副标题文字样式。</summary>
        public static GUIStyle SubtleStyle(int size = 15)
        {
            string key = "subtle" + size;
            GUIStyle st;
            if (_styleCache.TryGetValue(key, out st) && st != null) return st;
            st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleCenter };
            st.normal.textColor = TextDim;
            _styleCache[key] = st;
            return st;
        }

        /// <summary>正文文字样式（左对齐）。</summary>
        public static GUIStyle BodyStyle(int size = 15)
        {
            string key = "body" + size;
            GUIStyle st;
            if (_styleCache.TryGetValue(key, out st) && st != null) return st;
            st = new GUIStyle(GUI.skin.label) { fontSize = size };
            st.normal.textColor = TextMain;
            _styleCache[key] = st;
            return st;
        }
    }
}
