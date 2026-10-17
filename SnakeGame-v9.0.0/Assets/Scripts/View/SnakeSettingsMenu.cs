using System;
using ArknightsTD.Core;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Snake;
using UnityEngine;

namespace ArknightsTD.SnakeView
{
    /// <summary>
    /// 设置界面。分页与原版 <c>SettingsWindow</c> 完全一致：
    /// <list type="number">
    /// <item>背景样式</item>
    /// <item>蛇头颜色</item>
    /// <item>蛇身颜色</item>
    /// <item>主题色</item>
    /// <item>画面设置</item>
    /// <item>实验性功能</item>
    /// </list>
    /// 左侧分页导航 + 右侧选项，底部"取消 / 保存"。
    /// </summary>
    public sealed class SnakeSettingsMenu : MonoBehaviour
    {
        /// <summary>分页标题（原版 NavList 的 6 项）。</summary>
        public static readonly string[] Tabs =
        {
            "背景样式", "蛇头颜色", "蛇身颜色", "主题色", "画面设置", "实验性功能"
        };

        // 主题动态颜色（深色/浅色/Windows11/纯白），设置界面与主界面保持一致
        private static Rgb PanelBg => UiTheme.Background;
        private static Rgb NavBg => UiTheme.IsDark ? UiTheme.Lighten(UiTheme.Background, 0.05)
                                                   : UiTheme.Darken(UiTheme.Background, 0.03);
        private static Rgb NavActive => UiTheme.Selected;
        private static Rgb CardBg => UiTheme.Card;
        private static Rgb TextMain => UiTheme.TextPrimary;
        private static Rgb TextDim => UiTheme.TextSecondary;
        private static Rgb Accent => UiTheme.Accent;
        private static Rgb Danger => UiTheme.Danger;

        private GUIStyle _title, _tabStyle, _tabOn, _label, _small, _btn;
        private Texture2D _panelTex, _navTex, _navOnTex, _cardTex, _accentTex, _dangerTex;

        private int _tab;
        private bool _visible;
        private Vector2 _scroll;

        /// <summary>滚动区内容宽度（所有行/表单按它排版，避免横向超出窗口）。</summary>
        private float _innerW = 600f;

        /// <summary>当前设置（打开界面时从存档载入，保存时写回）。</summary>
        private AppSettings _s;


        /// <summary>关闭回调。</summary>
        public Action OnClosed;

        public bool IsVisible => _visible;

        /// <summary>当前编辑中的设置（测试用）。</summary>
        public AppSettings CurrentSettings => _s;

        private string _lastTheme = "";

        private void EnsureStyles()
        {
            UiTheme.Refresh();
            if (_title != null && _lastTheme == UiTheme.ThemeName) return;
            _lastTheme = UiTheme.ThemeName;
            _panelTex = Solid(PanelBg);
            _navTex = Solid(NavBg);
            _navOnTex = Solid(NavActive);
            _cardTex = Solid(CardBg);
            _accentTex = Solid(Accent);
            _dangerTex = Solid(Danger);

            _title = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold };
            _title.normal.textColor = C(TextMain);
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _label.normal.textColor = C(TextMain);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _small.normal.textColor = C(TextDim);
            _tabStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            _tabStyle.normal.textColor = C(TextMain);
            _tabOn = new GUIStyle(_tabStyle)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _tabOn.normal.textColor = Color.white;
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }

        private static Color C(Rgb c) => new Color32(c.R, c.G, c.B, 255);

        private static Texture2D Solid(Rgb c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, C(c));
            t.Apply();
            return t;
        }



        /// <summary>
        /// 自绘按钮：贴图 + 文字 + 悬停高亮 + 点击检测。'
        /// 不依赖引擎默认 GUI.Button 皮肤（默认皮肤在不同 DPI/主题下样式不可控）。
        /// </summary>
        private bool SelfButton(Rect r, string text)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            GUI.DrawTexture(r, hover ? Solid(UiTheme.CardHover) : _cardTex);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), hover ? _accentTex : Solid(UiTheme.Border));

            var st = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(r, text, st);

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        /// <summary>打开设置界面（会从存档重新载入，保证"取消"能丢弃改动）。</summary>
        public void Open()
        {
            if (_visible) return;
            _s = AppSettings.Load();
            _visible = true;
            _tab = 0;
            _scroll = Vector2.zero;
        }

        public void Close()
        {
            // 幂等：已关闭时直接返回。
            // OnClosed 里的回调可能又会触发界面切换，不做保护会无限递归（实测 StackOverflow）。
            if (!_visible) return;
            _visible = false;

            var cb = OnClosed;
            OnClosed = null;          // 先摘掉回调，避免重入
            if (cb != null) cb();
        }

        /// <summary>把设置真正应用到美术系统。</summary>
        public static void ApplyToSkin(AppSettings s)
        {
            if (s == null) return;
            SnakeSkin.SetBackgroundStyle(s.BackgroundStyle);

            // 颜色按 hex 精确匹配到皮肤表；匹配不到则直接按 hex 生效
            var head = AppSettings.ParseHex(s.HeadColorHex, SnakeSkin.HeadColors[0]);
            var body = AppSettings.ParseHex(s.BodyColorHex, SnakeSkin.BodyColors[0]);
            var accent = AppSettings.ParseHex(s.AccentColorHex, SnakeSkin.AccentColors[0]);

            SnakeSkin.SetHeadColorRgb(head);
            SnakeSkin.SetBodyColorRgb(body);
            int ai = -1;
            for (int i = 0; i < SnakeSkin.AccentColors.Length; i++)
                if (SnakeSkin.AccentColors[i].R == accent.R &&
                    SnakeSkin.AccentColors[i].G == accent.G &&
                    SnakeSkin.AccentColors[i].B == accent.B) { ai = i; break; }
            if (ai >= 0) SnakeSkin.SetAccentColor(ai);
        }

        private void OnGUI()
        {
            if (!_visible) return;
            var root = GetComponent<SnakeUiRoot>();
            if (root != null) return;      // 有 UiRoot 时由它主动调用 DrawNow，避免绘制顺序不确定
            DrawNow();
        }

        /// <summary>
        /// 立即绘制设置界面。由 <see cref="SnakeUiRoot"/> 在"设置"界面里主动调用。
        /// <para>
        /// 为什么不依赖自己的 OnGUI：两个组件的 OnGUI 执行顺序不确定，
        /// 实测会出现"UiRoot 画完背景、设置界面没画出来"的全黑画面。
        /// 改为由 UiRoot 顺序调用后，绘制时机完全确定。
        /// </para>
        /// </summary>
        public void DrawNow()
        {
            if (!_visible) return;
            EnsureStyles();
            if (_s == null) _s = AppSettings.Load();

            float w = Screen.width, h = Screen.height;
            // ★ 设置界面也显示背景（自定义背景图 / 主题底色），上面叠一层半透明保证可读
            var rootUi = GetComponent<SnakeUiRoot>();
            if (rootUi != null) rootUi.DrawBackdropNow();
            else Win11Ui.Panel(new Rect(0, 0, w, h), Win11Ui.CardFill, 0);
            Win11Ui.Panel(new Rect(0, 0, w, h),
                new Color(Win11Ui.CardFill.r, Win11Ui.CardFill.g, Win11Ui.CardFill.b, 0.42f), 0);

            // 标题
            GUI.Label(new Rect(28, 18, 600, 34), "设置", _title);
            GUI.Label(new Rect(28, 52, 700, 20),
                $"存档：{AppSettings.LastLoadLog}", _small);

            // 左侧分页
            float navX = 28, navY = 88, navW = 170, navH = 42;
            Win11Ui.Card(new Rect(navX - 10, navY - 10, navW + 22, Tabs.Length * (navH + 6) + 20), Win11Ui.Radius + 2);
            for (int i = 0; i < Tabs.Length; i++)
            {
                var r = new Rect(navX, navY + i * (navH + 6), navW, navH);
                bool on = _tab == i;
                if (on) Win11Ui.Panel(r, Win11Ui.Accent, Win11Ui.Radius);
                else Win11Ui.Panel(r, new Color(Win11Ui.CardFill.r, Win11Ui.CardFill.g, Win11Ui.CardFill.b, 0.35f), Win11Ui.Radius);
                GUI.Label(new Rect(r.x + 14, r.y, r.width - 20, r.height), Tabs[i], on ? _tabOn : _tabStyle);
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    _tab = i;
                    Event.current.Use();
                }
            }

            // 右侧内容
            float cx = navX + navW + 36, cy = navY - 8;
            float cw = w - cx - 28;
            _innerW = cw - 56f;   // 滚动内容可用宽度（行/表单都按它排版）
            Win11Ui.Card(new Rect(cx, cy, cw, h - cy - 96), Win11Ui.Radius + 4);
            GUI.Label(new Rect(cx + 20, cy + 14, cw - 40, 28), Tabs[_tab], _title);

            _scroll = GUI.BeginScrollView(
                new Rect(cx + 16, cy + 52, cw - 32, h - cy - 160), _scroll,
                new Rect(0, 0, cw - 56, 900));

            float y = 10;
            switch (_tab)
            {
                case 0: DrawBackgroundTab(cx, cw, ref y); break;
                case 1: DrawColorTab(cx, cw, ref y, true); break;
                case 2: DrawColorTab(cx, cw, ref y, false); break;
                case 3: DrawAccentTab(cx, cw, ref y); break;
                case 4: DrawDisplayTab(cx, cw, ref y); break;
                default: DrawExperimentalTab(cx, cw, ref y); break;
            }

            GUI.EndScrollView();

            // 底部按钮（全部自绘：贴图 + 文字 + 点击检测，不使用引擎默认 GUI.Button）
            float by = Mathf.Max(cy + 60f, h - 66);
            if (SelfButton(new Rect(28, by, 110, 38), "← 返回")) Close();
            if (SelfButton(new Rect(w - 268, by, 110, 38), "取消")) Close();
            if (SelfButton(new Rect(w - 148, by, 110, 38), "保存"))
            {
                _s.Save();
                ApplyToSkin(_s);
                Close();
            }

        }

        // ==================== 各分页 ====================

        private void DrawBackgroundTab(float cx, float cw, ref float y)
        {
            GUI.Label(new Rect(10, y, cw - 60, 22), "背景样式（对应设置项 BackgroundStyle）", _small);
            y += 30;
            for (int i = 0; i < AppSettings.BackgroundStyleNames.Length; i++)
            {
                if (OptionRow(cx, cw, ref y, AppSettings.BackgroundStyleNames[i],
                        _s.BackgroundStyle == i))
                    _s.BackgroundStyle = i;
            }

            y += 10;
            GUI.Label(new Rect(10, y, cw - 60, 22), "自定义背景图片", _small);
            y += 30;

            // 启用开关
            if (OptionRow(cx, cw, ref y, "启用自定义背景（CustomBgEnabled）", _s.CustomBgEnabled))
                _s.CustomBgEnabled = !_s.CustomBgEnabled;

            // 路径输入（原版用文件对话框，这里也提供输入框兜底，兼容无法弹窗的环境）
            GUI.Label(new Rect(10, y, cw - 60, 20), "图片路径（JPG / PNG / BMP / GIF，支持中文路径）", _small);
            y += 24;
            var pathField = new Rect(10, y, Mathf.Max(80f, _innerW - 190f), 30);
            GUI.DrawTexture(pathField, Solid(UiTheme.PanelBg));
            GUI.DrawTexture(new Rect(pathField.x, pathField.y + pathField.height, pathField.width, 1f),
                Solid(UiTheme.Border));
            var tmp = GUI.TextField(pathField, _s.CustomBackgroundPath ?? "", 200);
            if (tmp != _s.CustomBackgroundPath) _s.CustomBackgroundPath = tmp;

            // 浏览（打开文件对话框）
            if (SelfButton(new Rect(pathField.xMax + 8, y, 80, 30), "浏览…"))
            {
                // 系统自带文件对话框（Windows 原生）
                var picked = PickImageFile();
                if (!string.IsNullOrEmpty(picked))
                {
                    _s.CustomBackgroundPath = picked;
                    _s.CustomBgEnabled = true;
                }
            }
            if (SelfButton(new Rect(pathField.xMax + 96, y, 60, 30), "清除"))
                _s.CustomBackgroundPath = "";

            y += 40;
            GUI.Label(new Rect(10, y, cw - 60, 20),
                string.IsNullOrEmpty(_s.CustomBackgroundPath)
                    ? "当前：未设置（使用纯色背景样式）"
                    : "当前：" + System.IO.Path.GetFileName(_s.CustomBackgroundPath), _small);
            y += 26;
        }

        private void DrawColorTab(float cx, float cw, ref float y, bool isHead)
        {
            var colors = isHead ? SnakeSkin.HeadColors : SnakeSkin.BodyColors;
            var names = isHead ? SnakeSkin.HeadColorNames : SnakeSkin.BodyColorNames;
            var curHex = isHead ? _s.HeadColorHex : _s.BodyColorHex;
            var cur = AppSettings.ParseHex(curHex, colors[0]);

            GUI.Label(new Rect(10, y, cw - 60, 22),
                isHead ? "蛇头颜色（HeadColorHex）" : "蛇身颜色（BodyColorHex）", _small);
            y += 30;

            for (int i = 0; i < colors.Length; i++)
            {
                bool sel = colors[i].R == cur.R && colors[i].G == cur.G && colors[i].B == cur.B;
                if (OptionRow(cx, cw, ref y, names[i], sel, colors[i]))
                {
                    var hex = AppSettings.ToHex(colors[i]);
                    if (isHead) _s.HeadColorHex = hex; else _s.BodyColorHex = hex;
                }
            }
        }

        private void DrawAccentTab(float cx, float cw, ref float y)
        {
            var cur = AppSettings.ParseHex(_s.AccentColorHex, SnakeSkin.AccentColors[0]);
            GUI.Label(new Rect(10, y, cw - 60, 22), "主题色（AccentColorHex）", _small);
            y += 30;

            for (int i = 0; i < SnakeSkin.AccentColors.Length; i++)
            {
                var c = SnakeSkin.AccentColors[i];
                bool sel = c.R == cur.R && c.G == cur.G && c.B == cur.B;
                if (OptionRow(cx, cw, ref y, $"#{c.R:X2}{c.G:X2}{c.B:X2}", sel, c))
                    _s.AccentColorHex = AppSettings.ToHex(c);
            }

            y += 12;
            GUI.Label(new Rect(10, y, cw - 60, 22), "主题（ThemeName）", _small);
            y += 30;
            for (int i = 0; i < AppSettings.ThemeNames.Length; i++)
            {
                if (OptionRow(cx, cw, ref y, AppSettings.ThemeNames[i],
                        _s.ThemeName == AppSettings.ThemeNames[i]))
                    _s.ThemeName = AppSettings.ThemeNames[i];
            }
        }

        private void DrawDisplayTab(float cx, float cw, ref float y)
        {
            GUI.Label(new Rect(10, y, cw - 60, 22), "分辨率（ResolutionIndex）", _small);
            y += 30;
            for (int i = 0; i < AppSettings.ResolutionNames.Length; i++)
            {
                if (OptionRow(cx, cw, ref y, AppSettings.ResolutionNames[i], _s.ResolutionIndex == i))
                    _s.ResolutionIndex = i;
            }

            y += 12;
            GUI.Label(new Rect(10, y, cw - 60, 22), "显示模式（DisplayMode）", _small);
            y += 30;
            for (int i = 0; i < AppSettings.DisplayModeNames.Length; i++)
            {
                if (OptionRow(cx, cw, ref y, AppSettings.DisplayModeNames[i], _s.DisplayMode == i))
                    _s.DisplayMode = i;
            }

            y += 12;
            GUI.Label(new Rect(10, y, cw - 60, 22), "帧率上限（FpsTarget）", _small);
            y += 30;
            for (int i = 0; i < AppSettings.FpsTargetOptions.Length; i++)
            {
                if (OptionRow(cx, cw, ref y, AppSettings.FpsTargetOptions[i] + " FPS",
                        _s.FpsTarget == AppSettings.FpsTargetOptions[i]))
                    _s.FpsTarget = AppSettings.FpsTargetOptions[i];
            }

            y += 12;
            if (OptionRow(cx, cw, ref y, "抗锯齿（AntiAlias）", _s.AntiAlias))
                _s.AntiAlias = !_s.AntiAlias;
            if (OptionRow(cx, cw, ref y, "垂直同步（Vsync）", _s.Vsync))
                _s.Vsync = !_s.Vsync;
            if (OptionRow(cx, cw, ref y, "高帧率模式（HighFpsEnabled）", _s.HighFpsEnabled))
                _s.HighFpsEnabled = !_s.HighFpsEnabled;
        }

        private void DrawExperimentalTab(float cx, float cw, ref float y)
        {
            GUI.Label(new Rect(10, y, cw - 60, 22), "以下为实验性功能", _small);
            y += 30;

            if (OptionRow(cx, cw, ref y, "速度平衡（SpeedBalance）", _s.SpeedBalance))
                _s.SpeedBalance = !_s.SpeedBalance;
            if (OptionRow(cx, cw, ref y, "双向 IPC（BidirectionalIpc）", _s.BidirectionalIpc))
                _s.BidirectionalIpc = !_s.BidirectionalIpc;

            y += 14;
            GUI.Label(new Rect(10, y, cw - 60, 40),
                "说明：塔防模式已是正式内容，随时可从「开始游戏 → 模式选择」进入（第 6 张卡 / 数字键 6）；\n" +
                "其余模式均为 2D 俯视，保持原版美术风格。", _small);
            y += 46;
        }

        /// <summary>
        /// <summary>
        /// 打开系统自带的文件选择对话框（Windows 原生）。
        /// 编辑器用 UnityEditor.OpenFilePanel；独立版另起 powershell 进程调 WinForms OpenFileDialog，
        /// 避免在 Unity 进程内直接 P/Invoke comdlg32 导致线程崩溃。
        /// </summary>
        private static string PickImageFile()
        {
#if UNITY_EDITOR
            var p = UnityEditor.EditorUtility.OpenFilePanel(
                "选择背景图片", "", "jpg,jpeg,png,bmp,gif");
            return string.IsNullOrEmpty(p) ? null : p;
#else
            try
            {
                string script =
                    "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8;" +
                    "Add-Type -AssemblyName System.Windows.Forms;" +
                    "$d = New-Object System.Windows.Forms.OpenFileDialog;" +
                    "$d.Title = '选择背景图片';" +
                    "$d.Filter = '图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*';" +
                    "if ($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::Out.Write($d.FileName) }";

                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe");
                psi.Arguments = "-NoProfile -STA -WindowStyle Hidden -Command " + "\"" + script + "\"";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = System.Text.Encoding.UTF8;   // ★ 中文路径按 UTF-8 解码
                psi.StandardErrorEncoding = System.Text.Encoding.UTF8;

                using (var pr = System.Diagnostics.Process.Start(psi))
                {
                    if (pr == null) return null;
                    string outp = pr.StandardOutput.ReadToEnd();
                    pr.WaitForExit(180000);
                    outp = (outp ?? "").Trim();
                    return outp.Length == 0 ? null : outp;
                }
            }
            catch { return null; }
#endif
        }

        /// 一行可点击选项。返回 true 表示本帧被点击。
        /// </summary>
        private bool OptionRow(float cx, float cw, ref float y, string text, bool selected,
            Rgb? swatch = null, Rgb? tint = null)
        {
            var r = new Rect(10, y, Mathf.Max(120f, _innerW - 20f), 34);
            bool hover = r.Contains(Event.current.mousePosition);
            var bg = selected ? NavActive : (hover ? Rgb.FromHex(0x3A3A42) : CardBg);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height), Solid(bg));

            if (swatch.HasValue)
            {
                GUI.DrawTexture(new Rect(r.x + 8, r.y + 7, 20, 20),
                    Solid(swatch.Value));
                GUI.Label(new Rect(r.x + 36, r.y + 6, r.width - 44, 24), text, _label);
            }
            else
            {
                GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, 24), text,
                    selected ? _tabOn : _label);
            }

            if (selected)
            {
                var col = tint.HasValue ? tint.Value : Accent;
                GUI.DrawTexture(new Rect(r.x, r.y, 4, r.height), Solid(col));
            }

            y += 38;

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }
    }
}
