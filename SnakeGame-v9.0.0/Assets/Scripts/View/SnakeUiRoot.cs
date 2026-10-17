using System;
using System.Collections.Generic;
using ArknightsTD.Core;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Snake;
using UnityEngine;

namespace ArknightsTD.SnakeView
{
    /// <summary>界面层级。</summary>
    public enum UiScreen
    {
        /// <summary>开始界面（标题 + 开始游戏 / 设置 / 退出 / 成就 / 排行）。</summary>
        Start,
        /// <summary>模式选择。</summary>
        ModeSelect,
        /// <summary>搜打撤地图选择（原版 ShowMapSelect）。</summary>
        MapSelect,
        /// <summary>设置。</summary>
        Settings,
        /// <summary>成就。</summary>
        Achievements,
        /// <summary>排行。</summary>
        Rankings,
        /// <summary>对局中。</summary>
        Playing,
        /// <summary>对局暂停（ESC 呼出的暂停菜单）。</summary>
        Paused,
        /// <summary>塔防关卡选择（明日方舟式作战列表）。</summary>
        TDLevelSelect,
    }

    /// <summary>
    /// 界面总控。负责整个导航流程：
    /// <code>
    /// 开始界面
    ///  ├─ 开始游戏 → 模式选择 → 对局
    ///  ├─ 设置     → 设置界面
    ///  ├─ 退出     → 退出游戏
    ///  ├─ 成就     → 成就列表
    ///  └─ 排行     → 排行榜
    /// </code>
    /// <para>
    /// 各子界面的绘制分散在各自的组件里（<see cref="SnakeModeMenu"/> /
    /// <see cref="SnakeSettingsMenu"/>），本类只做<b>导航与背景绘制</b>，
    /// 避免一个巨型 OnGUI 难以维护。
    /// </para>
    /// </summary>
    public sealed class SnakeUiRoot : MonoBehaviour
    {
        // ---- 配色（Win11 风格，与原版一致）----
        private static readonly Rgb Backdrop = Rgb.FromHex(0x0E1116);
        private static readonly Rgb CardBg = Rgb.FromHex(0x1B1F27);
        private static readonly Rgb CardHover = Rgb.FromHex(0x262C36);
        private static readonly Rgb TextMain = Rgb.FromHex(0xF2F5F8);
        private static readonly Rgb TextDim = Rgb.FromHex(0x9AA4B2);
        private static readonly Rgb Accent = Rgb.FromHex(0xFF8C00);
        private static readonly Rgb Accent2 = Rgb.FromHex(0x00B2D2);
        private static readonly Rgb Danger = Rgb.FromHex(0xE81123);
        private static readonly Rgb Green = Rgb.FromHex(0x2FA84F);
        private static readonly Rgb Gray = Rgb.FromHex(0x6A7280);
        private string _lastTheme = "";

        private Texture2D _dimVeilTex;

        private GUIStyle _title, _subtitle, _btn, _label, _small, _big, _row, _rowDim, _sectionTitle;
        private Texture2D _backTex, _cardTex, _cardHoverTex, _accentTex, _accent2Tex, _dangerTex, _greenTex, _grayTex;

        private SnakeBootstrap _boot;
        private SnakeSettingsMenu _settings;
        private SnakeModeMenu _modeMenu;

        // ---- 界面自定义背景图（原版 CustomBgEnabled / CustomBackgroundPath）----
        private string _uiBgPath = "";
        private Texture2D _uiBgTex;

        /// <summary>
        /// 当前界面。
        /// ★ 不能命名为 Screen——那会遮蔽 UnityEngine.Screen，
        ///   导致同一文件里的 Screen.width / Screen.height 全部解析错误（已实测踩到）。
        /// </summary>
        public UiScreen Current { get; private set; } = UiScreen.Start;

        /// <summary>排行界面当前查看的模式。</summary>
        private SnakeMode _rankMode = SnakeMode.Timed;

        /// <summary>需要退出游戏的标记（OnGUI 里不能直接 Application.Quit）。</summary>
        private bool _quitRequested;

        /// <summary>显示界面状态调试条（仅诊断用）。</summary>
        public bool ShowDebugBar = false;   // 正式版：关闭左上角调试参数

        public void Bind(SnakeBootstrap boot) => _boot = boot;

        private void Awake()
        {
            if (_boot == null) _boot = GetComponent<SnakeBootstrap>();
        }

        // ==================================================================
        //  导航
        // ==================================================================

        /// <summary>切换界面，并屏蔽该帧的输入（避免一次按键被两层界面同时消费）。</summary>
        private void SwitchTo(UiScreen screen)
        {
            Current = screen;
            BlockInputThisFrame = true;
        }

        public void GoStart() => SwitchTo(UiScreen.Start);
        public void GoModeSelect() => SwitchTo(UiScreen.ModeSelect);
        public void GoTDLevelSelect() => SwitchTo(UiScreen.TDLevelSelect);
        public void GoSettings() { SwitchTo(UiScreen.Settings); EnsureSettings().Open(); }
        public void GoAchievements() => SwitchTo(UiScreen.Achievements);
        public void GoRankings() => SwitchTo(UiScreen.Rankings);

        /// <summary>
        /// 切到对局界面。只负责"隐藏所有界面"，
        /// <b>不</b>创建控制器——那由 <see cref="SnakeBootstrap.StartModeNow"/> 负责。
        /// 搜打撤模式先进入地图选择（原版 ShowMapSelect 流程）。
        /// </summary>
        public void GoPlaying(SnakeMode mode)
        {
            try
            {
                SnakeModeMenu.Probe($"UiRoot.GoPlaying {mode} boot={(_boot != null)}");
                if (mode == SnakeMode.Extraction)
                {
                    // 搜打撤：先选地图（原版点卡片 → ShowMapSelect）
                    SwitchTo(UiScreen.MapSelect);
                    return;
                }

                if (_boot != null) _boot.StartModeNow(mode);
                else SetPlayingScreen();
            }
            catch (System.Exception ex)
            {
                SnakeModeMenu.Probe($"GoPlaying EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 只切到"对局中"界面（不创建控制器）。
        /// 由 <see cref="SnakeBootstrap.StartModeNow"/> 调用，避免 GoPlaying ↔ StartModeNow 互相递归。
        /// </summary>
        public void SetPlayingScreen()
        {
            CloseSubScreens();
            Current = UiScreen.Playing;
            BlockInputThisFrame = true;
        }

        /// <summary>
        /// 本帧屏蔽界面输入。
        /// 用途：切换界面的那一帧，按键（如数字键 1）会同时被"上一层"和"新一层"处理——
        /// 实测按 1 进模式选择后，同一帧就被卡片的数字键逻辑捕获直接开局。
        /// </summary>
        public bool BlockInputThisFrame { get; private set; }

        /// <summary>返回上一层（Esc 用）。</summary>
        public void GoBack()
        {
            switch (Current)
            {
                case UiScreen.ModeSelect:
                case UiScreen.Achievements:
                case UiScreen.Rankings:
                    GoStart();
                    break;
                case UiScreen.MapSelect:
                case UiScreen.TDLevelSelect:
                    GoModeSelect();
                    break;
                case UiScreen.Settings:
                    if (CloseSubScreens()) GoStart();
                    break;
            }
        }

        /// <summary>关闭设置等子界面，返回是否有关闭动作。</summary>
        private bool CloseSubScreens()
        {
            bool closed = false;
            if (_settings != null && _settings.IsVisible) { _settings.Close(); closed = true; }
            if (_modeMenu != null && _modeMenu.IsVisible) { _modeMenu.enabled = false; closed = true; }
            return closed;
        }

        private SnakeSettingsMenu EnsureSettings()
        {
            if (_settings == null) _settings = GetComponent<SnakeSettingsMenu>();
            if (_settings == null) _settings = gameObject.AddComponent<SnakeSettingsMenu>();
            // 统一回调：保存或取消后都回到开始界面
            _settings.OnClosed = () =>
            {
                var s = AppSettings.Load();
                SnakeSettingsMenu.ApplyToSkin(s);
                SnakeBootstrap.ApplyRuntimeSettings(s);
                GoStart();
            };
            return _settings;
        }

        private SnakeModeMenu EnsureModeMenu()
        {
            if (_modeMenu == null) _modeMenu = GetComponent<SnakeModeMenu>();
            if (_modeMenu == null) _modeMenu = gameObject.AddComponent<SnakeModeMenu>();
            return _modeMenu;
        }

        /// <summary>设置界面是否正在显示（供 Bootstrap 判断是否拦截按键）。</summary>
        public bool SettingsVisible => _settings != null && _settings.IsVisible;

        private void Update()
        {
            if (_quitRequested)
            {
                QuitGame();
                return;
            }

            // Esc 逐层返回
            if (BlockInputThisFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape) && Current != UiScreen.Playing)
            {
                if (SettingsVisible) { _settings.Close(); GoStart(); }
                else if (Current != UiScreen.Start) GoBack();
            }
        }

        private void QuitGame()
        {
            _quitRequested = false;
            SnakeProgress.Save();
            AppSettings.Load().Save();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ==================================================================
        //  样式
        // ==================================================================

        /// <summary>
        /// 样式随主题（UiTheme）变化；主题切换后自动重建。
        /// 颜色语义对齐原版：背景 ThemeBackground、卡片 ThemeCard、
        /// 主文字 ThemeTextPrimary、次文字 ThemeTextSecondary。
        /// </summary>
        /// <summary>
        /// 界面背景：设置里指定了背景图且已启用时，按 Windows 壁纸"填充"方式
        /// 铺满整个界面（等比放大裁边）；否则用主题纯色。
        /// <para>
        /// ★ 只作用于【界面】（开始/模式选择/成就/排行/设置），
        ///   不作用于游戏内战场——战场仍用原版背景样式（纯黑/纯白/格子/星空）。
        /// </para>
        /// </summary>
        public void DrawBackdropNow()
        {
            var st = UiTheme.Settings;
            bool use = st != null && st.CustomBgEnabled
                    && !string.IsNullOrEmpty(st.CustomBackgroundPath);
            if (!use)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _backTex);
                return;
            }

            if (_uiBgTex == null || _uiBgPath != st.CustomBackgroundPath)
            {
                _uiBgPath = st.CustomBackgroundPath;
                _uiBgTex = null;
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(_uiBgPath);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes)) _uiBgTex = tex;
                    else Destroy(tex);
                }
                catch
                {
                    _uiBgTex = null;
                }
            }

            if (_uiBgTex == null)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _backTex);
                return;
            }

            // 填充模式：cover-crop
            float sa = (float)Screen.width / Mathf.Max(1, Screen.height);
            float ia = (float)_uiBgTex.width / Mathf.Max(1, _uiBgTex.height);
            var src = new Rect(0f, 0f, 1f, 1f);
            if (ia > sa)
            {
                float span = sa / ia;
                src = new Rect((1f - span) * 0.5f, 0f, span, 1f);
            }
            else
            {
                float span = ia / sa;
                src = new Rect(0f, (1f - span) * 0.5f, 1f, span);
            }
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, Screen.width, Screen.height), _uiBgTex, src);
        }

        private void EnsureStyles()
        {
            UiTheme.Refresh();
            string theme = UiTheme.ThemeName;
            if (_title != null && theme == _lastTheme) return;
            _lastTheme = theme;

            _backTex = Solid(UiTheme.Background);
            _cardTex = Solid(UiTheme.Card);
            _cardHoverTex = Solid(UiTheme.CardHover);
            _accentTex = Solid(UiTheme.Accent);
            _accent2Tex = Solid(Accent2);
            _dangerTex = Solid(UiTheme.Danger);
            _greenTex = Solid(UiTheme.Green);
            _grayTex = Solid(UiTheme.Gray);
            _dimVeilTex = Solid(new Rgb(0, 0, 0));
            _dimVeilTex = Solid(new Rgb(0, 0, 0));

            _title = new GUIStyle(GUI.skin.label)
            { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = C(UiTheme.TextPrimary);

            _subtitle = new GUIStyle(GUI.skin.label)
            { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            _subtitle.normal.textColor = C(UiTheme.TextSecondary);

            _big = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _big.normal.textColor = C(UiTheme.TextPrimary);

            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _label.normal.textColor = C(UiTheme.TextPrimary);

            _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _small.normal.textColor = C(UiTheme.TextSecondary);

            _sectionTitle = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _sectionTitle.normal.textColor = C(UiTheme.TextPrimary);

            _btn = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            _btn.normal.textColor = C(UiTheme.TextPrimary);

            _row = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _row.normal.textColor = C(UiTheme.TextPrimary);
            _rowDim = new GUIStyle(_row);
            _rowDim.normal.textColor = C(UiTheme.TextSecondary);
        }

        private static Color C(Rgb c) => new Color32(c.R, c.G, c.B, 255);

        private static Texture2D Solid(Rgb c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, C(c));
            t.Apply();
            return t;
        }

        // ==================================================================
        //  绘制入口
        // ==================================================================

        private void OnGUI()
        {
            // 对局中不画任何界面（HUD 由 SnakeHud 负责）。
            // 这里用【事实】判断而不是只信 Current 枚举：
            // 只要场上已有对局在跑（Bootstrap 处于 Playing 且控制器存在），就绝不画菜单。
            // 实测出现过 Current 未及时切换、导致菜单叠在游戏画面上的情况，
            // 用事实判断可以从结构上杜绝该类叠加。
            // 暂停菜单：优先于"对局不画任何界面"的守卫。
            if (Current == UiScreen.Paused) { EnsureStyles(); DrawPause(); return; }

            if (Current == UiScreen.Playing) return;
            if (_boot == null) _boot = GetComponent<SnakeBootstrap>();
            if (_boot != null && _boot.State == BootstrapState.Playing && _boot.Controller != null) return;

            EnsureStyles();
            if (_boot == null) _boot = GetComponent<SnakeBootstrap>();

            // 背景
            // 背景（有自定义背景图时画图，否则用主题纯色）
            DrawBackdropNow();

            switch (Current)
            {
                case UiScreen.Start: DrawStart(); break;
                case UiScreen.ModeSelect: DrawModeSelect(); break;
                case UiScreen.MapSelect: DrawMapSelect(); break;
                case UiScreen.Achievements: DrawAchievements(); break;
                case UiScreen.Rankings: DrawRankings(); break;
                case UiScreen.Settings: DrawSettingsHost(); break;
                case UiScreen.TDLevelSelect: DrawTDLevelSelect(); break;
            }

            // 调试状态条（定位界面状态不同步用）
            if (ShowDebugBar)
            {
                var dbg = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                dbg.normal.textColor = Color.yellow;
                string ctrl = _boot != null && _boot.Controller != null ? "有" : "无";
                GUI.Label(new Rect(8, 4, 700, 20),
                    $"[DEBUG] Current={Current}  BootState={(_boot != null ? _boot.State.ToString() : "null")}  " +
                    $"Controller={ctrl}  BlockInput={BlockInputThisFrame}", dbg);
            }

            // 帧末清除：保证"切换界面的那一帧"不再消费输入
            if (Event.current.type == EventType.Repaint) BlockInputThisFrame = false;
        }

        // ==================================================================
        //  1. 开始界面
        // ==================================================================

        private static readonly (string label, UiScreen screen)[] StartItems =
        {
            ("开始游戏", UiScreen.ModeSelect),
            ("设置", UiScreen.Settings),
            ("退出", UiScreen.Start),        // 特殊处理
            ("成就", UiScreen.Achievements),
            ("排行", UiScreen.Rankings),
        };

        private void DrawStart()
        {
            float w = Screen.width, h = Screen.height;

            // 标题
            GUI.Label(new Rect(0, h * 0.10f, w, 70), "贪吃蛇  v9.0.0", _title);
            GUI.Label(new Rect(0, h * 0.10f + 72f, w, 24),
                "SNAKE  ·  经典 / 淘汰之王 / 占领 / 极限 / 搜打撤", _subtitle);

            // 主菜单按钮（垂直排列）
            float bw = 320f, bh = 56f, gap = 14f;
            float bx = w * 0.5f - bw * 0.5f;
            float by = h * 0.34f;

            for (int i = 0; i < StartItems.Length; i++)
            {
                var item = StartItems[i];
                var r = new Rect(bx, by + i * (bh + gap), bw, bh);
                bool hover = r.Contains(Event.current.mousePosition);
                bool isQuit = item.label == "退出";

                // Windows 11 圆角按钮（首个为主按钮）
                Win11Ui.Panel(r, hover ? Win11Ui.CardHover : Win11Ui.CardFill, Win11Ui.Radius + 3);
                Win11Ui.Panel(r, Win11Ui.Border, Win11Ui.Radius + 3);
                Win11Ui.Panel(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2),
                    hover ? Win11Ui.CardHover : Win11Ui.CardFill, Win11Ui.Radius + 2);
                // 左侧强调条
                Win11Ui.Panel(new Rect(r.x + 6f, r.y + 12f, 4f, r.height - 24f),
                    isQuit ? Win11Ui.Danger : (i == 0 ? Win11Ui.Accent : new Color(0.21f, 0.71f, 0.91f)), 2);
                GUI.Label(r, item.label, _btn);

                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    ActivateStartItem(item.label, item.screen);
                    return;
                }
            }

            // 数字键快捷（切换界面那一帧不响应，避免误触）
            if (BlockInputThisFrame) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) { GoModeSelect(); return; }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { GoSettings(); return; }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { _quitRequested = true; return; }
            if (Input.GetKeyDown(KeyCode.Alpha4)) { GoAchievements(); return; }
            if (Input.GetKeyDown(KeyCode.Alpha5)) { GoRankings(); return; }

            // 底部统计
            GUI.Label(new Rect(0, h - 56, w, 22),
                $"场次 {SnakeProgress.TotalGames}    胜场 {SnakeProgress.WonGames}    " +
                $"击杀 {SnakeProgress.TotalKills}    成就 {SnakeProgress.UnlockedCount}/" +
                $"{SnakeProgress.Achievements.Length}",
                new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(0, h - 32, w, 22),
                "数字键 1-5 快捷选择", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
        }

        private void ActivateStartItem(string label, UiScreen target)
        {
            switch (label)
            {
                case "退出": _quitRequested = true; break;
                default: Current = target; break;
            }
        }

        // ==================================================================
        //  2. 模式选择
        // ==================================================================

        private void DrawModeSelect()
        {
            // 硬守卫：只要有对局控制器存在，就绝不画模式选择（并顺手把状态纠正回来）
            if (_boot != null && _boot.Controller != null)
            {
                Current = UiScreen.Playing;
                return;
            }

            float w = Screen.width, h = Screen.height;

            DrawHeader("选择模式", "点击卡片或按数字键 1-6 开始");

            var menu = EnsureModeMenu();
            // ★ 模式选项卡居中：5 个贪吃蛇模式 + 塔防共 6 张卡整体居中
            const int totalCards = 6;
            float cw6 = SnakeModeMenu.CardW, gap6 = SnakeModeMenu.CardGap;
            float totalW6 = totalCards * cw6 + (totalCards - 1) * gap6;
            float x0All = w * 0.5f - totalW6 * 0.5f;
            menu.DrawCards(this, x0All);
            menu.enabled = true;   // 让它自己处理点击与外观选项

            // ---- 6. 塔防（正式模式，始终可选）----
            // 布局与 DrawCards 严格一致：5 张贪吃蛇卡以屏幕中心居中，塔防卡紧随其后。
            {
                float cw2 = SnakeModeMenu.CardW, ch2 = SnakeModeMenu.CardH;
                int n5 = SnakeModeMenu.Modes.Length;
                // 与 5 张蛇模式卡同一行、同一基线（整体居中后的第 6 格）
                var r2 = new Rect(x0All + n5 * (cw2 + SnakeModeMenu.CardGap), h * 0.28f, cw2, ch2);

                bool over2 = r2.Contains(Event.current.mousePosition);
                Win11Ui.Card(r2, Win11Ui.Radius + 2, over2);
                Win11Ui.Panel(new Rect(r2.x + 12, r2.y, r2.width - 24, 3), Win11Ui.Accent, 2);
                GUI.Label(new Rect(r2.x + 12, r2.y + 12, cw2 - 24, 26), "6. 塔防（3D）", _big);
                GUI.Label(new Rect(r2.x + 12, r2.y + 44, cw2 - 24, 60),
                    "明日方舟式塔防\n3D 斜视战场 · 10 关", _small);
                GUI.Label(new Rect(r2.x + 12, r2.y + ch2 - 34, cw2 - 24, 30), "点击进入", _small);

                if (over2 && (Event.current.type == EventType.MouseDown
                           || Event.current.type == EventType.MouseUp))
                {
                    Event.current.Use();
                    StartTowerDefense();
                    return;
                }
            }

            // 数字键 6：进入塔防（前 5 个模式键由 SnakeModeMenu.DrawCards 处理）
            if (!BlockInputThisFrame && Input.GetKeyDown(KeyCode.Alpha6))
            {
                BlockInputThisFrame = true;
                StartTowerDefense();
                return;
            }

            DrawBackButton();
        }

        // ==================================================================
        //  2b. 搜打撤地图选择（原版 DrawMapSelect）
        // ==================================================================

        /// <summary>选择作战地图：点卡片 → 开局。</summary>
        private void DrawMapSelect()
        {
            float w = Screen.width, h = Screen.height;
            DrawHeader("选择作战地图", "搜打撤 · 收集分数，搜索容器撤离");

            var maps = ArknightsTD.Snake.SnakeExtractionMode.Maps;

            float cw = 236f, ch = 150f, gap = 16f;
            float total = maps.Length * cw + (maps.Length - 1) * gap;
            float x0 = w * 0.5f - total * 0.5f;
            float y0 = h * 0.28f;

            for (int i = 0; i < maps.Length; i++)
            {
                var map = maps[i];
                var r = new Rect(x0 + i * (cw + gap), y0, cw, ch);

                bool over = r.Contains(Event.current.mousePosition);
                GUI.DrawTexture(r, over ? _cardHoverTex : _cardTex);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), _accentTex);

                GUI.Label(new Rect(r.x + 12, r.y + 12, cw - 24, 26), map.Name, _big);
                GUI.Label(new Rect(r.x + 12, r.y + 44, cw - 24, 60),
                    $"{map.WorldWidth:0}×{map.WorldHeight:0}\n" +
                    $"容器 {map.ContainerPositions.Count} · 敌人 {map.EnemyCount}\n" +
                    (map.HasFirebombs ? "⚠ 火炸弹" : ""), _small);
                GUI.Label(new Rect(r.x + 12, r.y + ch - 34, cw - 24, 30), "点击进入", _small);

                if (over && Event.current.type == EventType.MouseDown)
                {
                    Event.current.Use();
                    if (_boot != null) _boot.ExtractionMapIndex = i;
                    if (_boot != null) _boot.StartModeNow(SnakeMode.Extraction);
                    else SetPlayingScreen();
                    return;
                }
            }

            DrawBackButton();
        }

        // ==================================================================
        //  3. 设置（宿主只画标题栏，内容由 SnakeSettingsMenu 负责）
        // ==================================================================

        private void DrawSettingsHost()
        {
            var sm = EnsureSettings();
            if (!sm.IsVisible) sm.Open();
            sm.DrawNow();      // 由 UiRoot 顺序绘制，保证一定画出来
        }

        // ==================================================================
        //  4. 成就
        // ==================================================================

        private void DrawAchievements()
        {
            float w = Screen.width, h = Screen.height;
            DrawHeader("成就",
                $"已解锁 {SnakeProgress.UnlockedCount} / {SnakeProgress.Achievements.Length}");

            var defs = SnakeProgress.Achievements;
            float cw = 720f, ch = 76f, gap = 12f;
            float cx = w * 0.5f - cw * 0.5f;
            float cy = h * 0.22f;

            for (int i = 0; i < defs.Length; i++)
            {
                var a = defs[i];
                bool ok = SnakeProgress.IsUnlocked(a.Id);
                var r = new Rect(cx, cy + i * (ch + gap), cw, ch);

                Win11Ui.Card(r, Win11Ui.Radius + 1);
                // 左侧状态条
                GUI.DrawTexture(new Rect(r.x, r.y, 5f, r.height), ok ? _greenTex : _grayTex);

                // 图标（原版用 ✔ / ✘）
                var icon = new GUIStyle(GUI.skin.label)
                { fontSize = 26, alignment = TextAnchor.MiddleCenter };
                icon.normal.textColor = ok ? C(Green) : C(Gray);
                GUI.Label(new Rect(r.x + 18f, r.y + 12f, 44f, 40f), ok ? "✔" : "✘", icon);

                // 名称 + 描述（原版：解锁为绿色，未解锁为普通色）
                var nameStyle = new GUIStyle(_big) { fontSize = 19 };
                nameStyle.normal.textColor = ok ? C(Green) : C(TextMain);
                GUI.Label(new Rect(r.x + 76f, r.y + 12f, cw - 100f, 28f), a.Name, nameStyle);

                GUI.Label(new Rect(r.x + 76f, r.y + 42f, cw - 100f, 22f), a.Description, _small);
            }

            DrawBackButton();
        }

        // ==================================================================
        //  5. 排行
        // ==================================================================

        private void DrawRankings()
        {
            float w = Screen.width, h = Screen.height;
            DrawHeader("排行", "历史成绩（每模式保留前 20 名）");

            // 模式切换标签
            var modes = SnakeModeMenu.Modes;
            float tabW = 120f, tabH = 34f, tabGap = 8f;
            float totalW = modes.Length * tabW + (modes.Length - 1) * tabGap;
            float tx = w * 0.5f - totalW * 0.5f;
            float ty = h * 0.20f;

            for (int i = 0; i < modes.Length; i++)
            {
                var r = new Rect(tx + i * (tabW + tabGap), ty, tabW, tabH);
                bool on = _rankMode == modes[i];
                bool hover = r.Contains(Event.current.mousePosition);
                GUI.DrawTexture(r, on ? _accent2Tex : (hover ? _cardHoverTex : _cardTex));
                var st = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
                if (on) st.normal.textColor = Color.black;
                GUI.Label(r, SnakeModeConfig.For(modes[i]).DisplayName, st);

                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    _rankMode = modes[i];
                    Event.current.Use();
                }
            }

            // 表头
            var list = SnakeScores.ForMode(_rankMode);
            float lw = 760f, rowH = 32f;
            float lx = w * 0.5f - lw * 0.5f;
            float ly = ty + tabH + 20f;

            GUI.DrawTexture(new Rect(lx, ly, lw, rowH), _cardHoverTex);
            GUI.Label(new Rect(lx + 16, ly + 6, 80, 22), "排名", _small);
            GUI.Label(new Rect(lx + 110, ly + 6, 120, 22), "分数", _small);
            GUI.Label(new Rect(lx + 250, ly + 6, 120, 22), "体长", _small);
            GUI.Label(new Rect(lx + 390, ly + 6, 120, 22), "时长", _small);
            GUI.Label(new Rect(lx + 530, ly + 6, 200, 22), "时间", _small);

            if (list.Count == 0)
            {
                GUI.Label(new Rect(lx, ly + rowH + 40f, lw, 30f),
                    "暂无记录——去玩一局吧", new GUIStyle(_label)
                    { alignment = TextAnchor.MiddleCenter });
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    var r = new Rect(lx, ly + rowH * (i + 1), lw, rowH);
                    if (i % 2 == 1) GUI.DrawTexture(r, _cardTex);

                    // 前三名用强调色
                    var st = i < 3 ? new GUIStyle(_row) : _rowDim;
                    if (i < 3) st.normal.textColor = C(Accent);

                    GUI.Label(new Rect(r.x + 16, r.y + 6, 90, 22), $"#{i + 1}", st);
                    GUI.Label(new Rect(r.x + 110, r.y + 6, 130, 22), e.Score.ToString(), st);
                    GUI.Label(new Rect(r.x + 250, r.y + 6, 130, 22), e.BodyCells.ToString(), _rowDim);
                    GUI.Label(new Rect(r.x + 390, r.y + 6, 130, 22), $"{e.Seconds:0.0}s", _rowDim);
                    GUI.Label(new Rect(r.x + 530, r.y + 6, 220, 22),
                        e.Won ? e.Date + "  胜" : e.Date, _rowDim);

                    if (ly + rowH * (i + 2) > h - 80) break;   // 放不下就截断
                }
            }

            DrawBackButton();
        }

        // ==================================================================
        //  通用部件
        // ==================================================================

        /// <summary>
        /// 进入塔防模式：停掉贪吃蛇对局，启用场景里的 BattleBootstrap 并显式开打。
        /// 塔防是 3D 斜视，与贪吃蛇的 2D 俯视互斥，因此必须先彻底拆掉贪吃蛇侧的一切。
        /// <para>
        /// ★ 必须调用 <see cref="ArknightsTD.View.BattleBootstrap.BeginBattle"/>：
        ///   仅靠 SetActive(true) 触发 Start() 只在第一次有效，
        ///   退出塔防再进入时 Start 不会重跑 → 战斗起不来（"无法开始游戏"的根因）。
        /// </para>
        /// </summary>

        // ==================================================================
        //  塔防关卡选择（明日方舟式作战列表）
        // ==================================================================

        private void DrawTDLevelSelect()
        {
            float w = Screen.width, h = Screen.height;
            DrawHeader("作战选择", "选择关卡开始作战 · 通关后解锁下一关");

            var levels = ArknightsTD.Data.TDLevels.All;
            int unlocked = Mathf.Clamp(PlayerProgress.MaxUnlocked, 1, levels.Count);

            float rowW = 620f, rowH = 64f, gap = 10f;
            float x0 = w * 0.5f - rowW * 0.5f;
            float y0 = h * 0.22f;

            for (int i = 0; i < levels.Count; i++)
            {
                var lv = levels[i];
                bool isUnlocked = i < unlocked;
                bool isCleared = i < unlocked - 1;
                var r = new Rect(x0, y0 + i * (rowH + gap), rowW, rowH);

                bool over = r.Contains(Event.current.mousePosition);
                Win11Ui.Card(r, Win11Ui.Radius + 2, over && isUnlocked);
                Win11Ui.Panel(new Rect(r.x + 6f, r.y + 12f, 4f, r.height - 24f),
                    isUnlocked ? (isCleared ? Win11Ui.Success : Win11Ui.Accent) : Win11Ui.TextDim, 2);

                GUI.Label(new Rect(r.x + 18, r.y + 10, 60, 30), (i + 1).ToString("00"), _big);
                GUI.Label(new Rect(r.x + 84, r.y + 8, rowW - 300, 24), lv.Name, _label);
                GUI.Label(new Rect(r.x + 84, r.y + 34, rowW - 300, 22),
                    $"{lv.Description}    波次 {lv.TotalWaves}    难度 {lv.Difficulty}", _small);

                string status = isCleared ? "已通过" : (isUnlocked ? "可作战" : "未解锁");
                var statusStyle = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight };
                statusStyle.normal.textColor = isCleared ? C(UiTheme.Green)
                    : isUnlocked ? C(UiTheme.Accent) : C(UiTheme.TextSecondary);
                GUI.Label(new Rect(r.x + rowW - 200, r.y + rowH * 0.5f - 11, 170, 22), status, statusStyle);

                if (!isUnlocked) GUI.DrawTexture(r, _dimVeilTex);

                if (isUnlocked && over && (Event.current.type == EventType.MouseDown
                                        || Event.current.type == EventType.MouseUp))
                {
                    Event.current.Use();
                    StartTowerDefense(i + 1);
                    return;
                }
            }

            DrawBackButton();
        }

        public void StartTowerDefense() => StartTowerDefense(1);

        /// <summary>进入塔防指定关卡。</summary>
        public void StartTowerDefense(int levelIndex)
        {
            SnakeModeMenu.Probe($"StartTowerDefense: begin level={levelIndex}");
            if (_boot != null) _boot.ShowStartScreen();     // 先清掉贪吃蛇对局与界面
            Current = UiScreen.Playing;

            var td = FindObjectOfType<ArknightsTD.View.BattleBootstrap>(true);
            if (td == null)
            {
                SnakeModeMenu.Probe("StartTowerDefense: 场景里没有 BattleBootstrap ← 无法进入塔防");
                return;
            }
            td.gameObject.SetActive(true);
            td.enabled = true;
            td.LevelIndex = Mathf.Clamp(levelIndex, 1, Mathf.Max(1, ArknightsTD.Data.TDLevels.All.Count));
            td.BeginBattle();                               // 显式开局（可重复调用）
            SnakeModeMenu.Probe($"StartTowerDefense: done, controller={(td.Controller != null)} " +
                                $"sim={(td.Controller != null && td.Controller.Sim != null)}");
        }

        // ==================================================================
        //  暂停菜单（对齐原版 DrawPauseMenu：暂停 / 重新开始 / 返回开始界面 / 退出游戏）
        // ==================================================================

        private int _pauseSelection;

        private static readonly string[] PauseOptions = { "重新开始", "返回开始界面", "退出游戏" };

        /// <summary>呼出暂停菜单（由 Bootstrap 在 ESC 时调用）。</summary>
        public void ShowPause()
        {
            if (_boot != null && _boot.Controller != null && _boot.State == BootstrapState.Playing)
            {
                // 暂停菜单 = 模拟暂停，两者必须绑定（否则菜单开着游戏还在跑）
                _boot.Controller.SetPaused(true);
                Current = UiScreen.Paused;
                _pauseSelection = 0;
            }
        }

        /// <summary>继续游戏。</summary>
        public void ResumeGame()
        {
            Current = UiScreen.Playing;
            if (_boot != null && _boot.Controller != null) _boot.Controller.SetPaused(false);
        }

        /// <summary>暂停菜单：重新开始。</summary>
        private void PauseRestart()
        {
            if (_boot != null && _boot.Controller != null)
            {
                _boot.Controller.Restart();
                _boot.Controller.SetPaused(false);
            }
            Current = UiScreen.Playing;
        }

        /// <summary>暂停菜单：返回开始界面。</summary>
        private void PauseBackToStart()
        {
            if (_boot != null) _boot.ShowStartScreen();
        }

        /// <summary>暂停菜单：退出游戏。</summary>
        private void PauseQuit() => _quitRequested = true;
        /// <summary>测试入口：执行"返回开始界面"选项（供自动化验证调用）。</summary>
        public void PauseBackToStartTest() => PauseBackToStart();


        private void UpdatePauseInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                _pauseSelection = (_pauseSelection + PauseOptions.Length - 1) % PauseOptions.Length;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                _pauseSelection = (_pauseSelection + 1) % PauseOptions.Length;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                ActivatePauseSelection();
            // ★ ESC 不在这里处理：Bootstrap.Update 同一帧也监听 ESC（切换暂停）。
            //   两边都处理时，一次按键会在同一帧内"开菜单 → 立即关菜单"（实测菜单闪现即消失）。
        }

        private void ActivatePauseSelection()
        {
            switch (_pauseSelection)
            {
                case 0: PauseRestart(); break;
                case 1: PauseBackToStart(); break;
                default: PauseQuit(); break;
            }
        }

        /// <summary>绘制暂停菜单。原版是 Win11 圆角卡 + 三行按钮，全部自绘。</summary>
        private void DrawPause()
        {
            float w = Screen.width, h = Screen.height;
            // 半透明遮罩：透过它能看到后面的游戏画面
            Win11Ui.Panel(new Rect(0, 0, w, h), new Color(0.02f, 0.03f, 0.05f, 0.55f), 0);

            var card = new Rect(w * 0.5f - 250f, 200f, 500f, 300f);
            Win11Ui.Acrylic(card, new Color(1f, 1f, 1f, 1f), Win11Ui.Radius + 4, 0.9f);
            Win11Ui.AccentBar(new Rect(card.x - 12, card.y, card.width, card.height), Win11Ui.Accent);

            var title = new GUIStyle(GUI.skin.label)
            { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = C(UiTheme.TextPrimary);
            GUI.Label(new Rect(card.x, card.y + 28f, card.width, 60f), "暂停", title);

            float oy = card.y + 120f;
            for (int i = 0; i < PauseOptions.Length; i++)
            {
                var r = new Rect(card.x + 50f, oy + i * 60f, 400f, 42f);
                bool hover = r.Contains(Event.current.mousePosition);
                bool sel = i == _pauseSelection;

                Win11Ui.Panel(r, (hover || sel) ? Win11Ui.CardHover : Win11Ui.Subtle, Win11Ui.Radius + 2);
                if (sel) Win11Ui.Panel(new Rect(r.x, r.y + 4, 4f, r.height - 8), Win11Ui.Accent, 2);

                var st = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                st.normal.textColor = C(UiTheme.TextPrimary);
                GUI.Label(r, PauseOptions[i], st);

                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    _pauseSelection = i;
                    ActivatePauseSelection();
                    return;
                }
            }

            UpdatePauseInput();
        }

        private void DrawHeader(string title, string subtitle)
        {
            float w = Screen.width;
            GUI.Label(new Rect(0, Screen.height * 0.06f, w, 46), title, _sectionTitle);
            if (!string.IsNullOrEmpty(subtitle))
                GUI.Label(new Rect(0, Screen.height * 0.06f + 48f, w, 22), subtitle, _subtitle);
        }

        private void DrawBackButton()
        {
            var r = new Rect(28, Screen.height - 68f, 130f, 42f);
            bool hover = r.Contains(Event.current.mousePosition);
            Win11Ui.Panel(r, hover ? Win11Ui.CardHover : Win11Ui.CardFill, Win11Ui.Radius + 2);
            Win11Ui.Panel(new Rect(r.x, r.y, 4f, r.height), Win11Ui.Accent, 2);
            GUI.DrawTexture(new Rect(r.x, r.y, 4f, r.height), _accentTex);
            GUI.Label(r, "← 返回 (Esc)", new GUIStyle(_label)
            { alignment = TextAnchor.MiddleCenter });

            if ((Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)))
            {
                Event.current.Use();
                GoStart();
            }
        }
    }
}
