using UnityEngine;
using ArknightsTD.Core;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Snake;

namespace ArknightsTD.SnakeView
{
    /// <summary>
    /// 贪吃蛇模式选择界面。
    /// <para>
    /// 用 IMGUI 绘制，避免依赖 Canvas/Prefab 与场景文件。
    /// 展示全部 5 个模式及其目标、以及累计统计与成就进度。
    /// </para>
    /// </summary>
    public sealed class SnakeModeMenu : MonoBehaviour
    {
        /// <summary>可选模式顺序。</summary>
        public static readonly SnakeMode[] Modes =
        {
            SnakeMode.Classic, SnakeMode.Timed, SnakeMode.Team4v4,
            SnakeMode.Extreme, SnakeMode.Extraction
        };

        /// <summary>选择回调（由 Bootstrap 注入）。</summary>
        public System.Action<SnakeMode> OnSelected;

        /// <summary>
        /// 是否应该绘制。由 Bootstrap 依据"是否处于菜单态且没有对局"回答。
        /// <para>
        /// 为什么不只看组件 enabled：一旦开局链路中途失败，
        /// enabled 可能没被关掉，菜单就会和游戏画面叠在一起（实测出现过）。
        /// 改成向状态机查询后，只要对局已经建立就绝不会再画菜单。
        /// </para>
        /// </summary>
        private SnakeBootstrap _boot;

        private void Awake()
        {
            if (_boot == null) _boot = GetComponent<SnakeBootstrap>();
        }

        /// <summary>菜单是否可见。</summary>
        public bool IsVisible
        {
            get
            {
                if (_boot == null) _boot = GetComponent<SnakeBootstrap>();
                // 没有 Bootstrap（单独挂菜单的场景）时，退化为看 enabled
                return _boot == null ? enabled : _boot.ShouldDrawMenu;
            }
        }

        private GUIStyle _title, _label, _small, _big;
        private Texture2D _panelTex, _accentTex, _cardTex, _cardHoverTex, _cyanTex;

        // 主题动态颜色（Windows11 / 深色 / 浅色 / 纯白），与主界面一致
        private static Color32 Amber => C32(UiTheme.Accent);
        private static Color32 Cyan => C32(Rgb.FromHex(0x00B2D2));
        private static Color32 PanelBg => C32(UiTheme.Background);
        private static Color32 CardBg => C32(UiTheme.Card);
        private static Color32 CardHover => C32(UiTheme.CardHover);
        private static Color32 TextMain => C32(UiTheme.TextPrimary);
        private static Color32 TextDim => C32(UiTheme.TextSecondary);

        private static Color32 C32(Rgb c) => new Color32(c.R, c.G, c.B, 255);

        /// <summary>模式卡片尺寸（UiRoot 计算 6 卡整体居中时复用）。</summary>
        public const float CardW = 236f;
        public const float CardH = 150f;
        public const float CardGap = 16f;

        private int _hover = -1;

        private void EnsureStyles()
        {
            if (_title != null) return;
            _panelTex = Solid(PanelBg);
            _accentTex = Solid(Amber);
            _cardTex = Solid(CardBg);
            _cardHoverTex = Solid(CardHover);
            _cyanTex = Solid(Cyan);

            _title = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = TextMain;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _label.normal.textColor = TextMain;
            _small = new GUIStyle(_label) { fontSize = 12, wordWrap = true };
            _small.normal.textColor = TextDim;
            _big = new GUIStyle(_label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
            _big.normal.textColor = Amber;
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        /// <summary>模式说明（标题 + 目标 + 操作，文案对齐 WPF 原版 GetAvailableModes）。</summary>
        public static (string name, string goal, string keys) Describe(SnakeMode mode)
        {
            switch (mode)
            {
                case SnakeMode.Classic:
                    return ("经典模式", "自由移动，达到 3000 分胜利",
                            "鼠标转向    Ctrl 加速");
                case SnakeMode.Timed:
                    return ("淘汰之王", "49 人决斗，按 F 挑战附近对手",
                            "鼠标转向    F 发起决斗（靠近对手）");
                case SnakeMode.Team4v4:
                    return ("占领模式 (4v4)", "与蓝队 AI 合作占领中央区域",
                            "鼠标转向    留在圈内");
                case SnakeMode.Extreme:
                    return ("极限模式", "20 个敌人，存活 3 分钟胜利",
                            "鼠标转向    Ctrl 加速");
                default:
                    return ("搜打撤", "收集分数，搜索容器撤离",
                            "靠近容器按 F 搜索、再按 F 领取    撤离点停留 9 秒");
            }
        }

        /// <summary>
        /// 独立绘制（仅在没有 <see cref="SnakeUiRoot"/> 接管时使用）。
        /// 有 UiRoot 时由它的界面状态机决定何时调用 <see cref="DrawCards"/>。
        /// </summary>
        private void OnGUI()
        {
            if (!IsVisible) return;
            // ★ 只要存在 UiRoot，就【绝不】自己画——所有界面绘制统一由 UiRoot 调度。
            //   先前写成 "root.Current != Playing 才 return"，方向反了：
            //   塔防/对局期间 Current==Playing，条件不成立 → 继续画模式菜单，
            //   把 3D 战场整个盖住（实测塔防只剩一片背景色，看不到战场）。
            var root = GetComponent<SnakeUiRoot>();
            if (root != null) return;

            EnsureStyles();
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _panelTex);
            GUI.Label(new Rect(0, Screen.height * 0.06f, Screen.width, 44), "贪吃蛇", _title);
            DrawCards(null);
        }

        /// <summary>
        /// 绘制 5 张模式卡片。供 <see cref="SnakeUiRoot"/> 在"模式选择"界面里调用。
        /// <paramref name="root"/> 非空时，点击卡片走 UiRoot 的导航；为空则走 <see cref="OnSelected"/>。
        /// </summary>
        /// <summary>绘制模式卡片；startX 为 NaN 时只按本菜单居中，否则用调用方给的左边界（含塔防卡整体居中）。</summary>
        public void DrawCards(SnakeUiRoot root, float startX = float.NaN)
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            float cw = CardW, ch = CardH, gap = CardGap;
            float total = Modes.Length * cw + (Modes.Length - 1) * gap;
            float x0 = float.IsNaN(startX) ? w * 0.5f - total * 0.5f : startX;
            float y0 = h * 0.28f;

            _hover = -1;
            for (int i = 0; i < Modes.Length; i++)
            {
                var mode = Modes[i];
                var info = Describe(mode);
                var r = new Rect(x0 + i * (cw + gap), y0, cw, ch);

                bool over = r.Contains(Event.current.mousePosition);
                if (over) _hover = i;

                // Windows 11 Fluent 圆角卡片 + 细描边 + 顶部强调条
                Win11Ui.Card(r, Win11Ui.Radius + 2, over);
                Win11Ui.Panel(new Rect(r.x + 12, r.y, r.width - 24, 3), over ? Win11Ui.Accent : new Color(0.21f, 0.71f, 0.91f), 2);

                GUI.Label(new Rect(r.x + 12, r.y + 12, cw - 24, 26), $"{i + 1}. {info.name}", _big);
                GUI.Label(new Rect(r.x + 12, r.y + 44, cw - 24, 44), info.goal, _small);
                GUI.Label(new Rect(r.x + 12, r.y + ch - 34, cw - 24, 30), info.keys, _small);

                if (over && Event.current.type == EventType.MouseDown)
                {
                    Event.current.Use();
                    BeginMode(root, mode);
                    return;
                }
            }

            // 数字键选择
            for (int i = 0; i < Modes.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { BeginMode(root, Modes[i]); return; }
            }

            DrawStatsPanel(y0 + ch + 24f);
        }

        /// <summary>
        /// 统一的卡片正面绘制（标题 / 说明 / 操作键），蛇模式卡与塔防卡共用，
        /// 保证文字颜色、字号、留白完全一致；文字用自适应缩小，绝不溢出卡片。
        /// </summary>
        public static void DrawCardFace(Rect r, bool hovered, int index, string title,
            string body, string keys)
        {
            Win11Ui.Card(r, Win11Ui.Radius + 2, hovered);
            var accent = hovered ? Win11Ui.Accent : new Color(0.21f, 0.71f, 0.91f);
            Win11Ui.Panel(new Rect(r.x + 12, r.y, r.width - 24, 3), accent, 2);

            // 序号角标
            Win11Ui.Panel(new Rect(r.x + 12, r.y + 12, 26, 24), accent, 6);
            Win11Ui.FittedLabel(new Rect(r.x + 12, r.y + 12, 26, 24), $"{index + 1}",
                14, 11, Color.white, true, TextAnchor.MiddleCenter, false);

            // 标题
            Win11Ui.FittedLabel(new Rect(r.x + 46, r.y + 12, r.width - 60, 28), title,
                19, 13, Win11Ui.TextMain, true, TextAnchor.MiddleLeft, false);

            // 说明（可换行，占中部）
            Win11Ui.FittedLabel(new Rect(r.x + 14, r.y + 46, r.width - 28, r.height - 118), body,
                12, 10, Win11Ui.TextDim, false, TextAnchor.UpperLeft, true);

            // 操作键（底部一行，超长自动缩小）
            Win11Ui.FittedLabel(new Rect(r.x + 14, r.y + r.height - 56, r.width - 28, 42), keys,
                11, 9, Win11Ui.TextDim, false, TextAnchor.LowerLeft, true);
        }

        /// <summary>进入某个模式：有 UiRoot 时走它的导航，否则直接回调。</summary>
        private void BeginMode(SnakeUiRoot root, SnakeMode mode)
        {
            if (root != null) root.GoPlaying(mode);
            else Select(mode);
        }

        /// <summary>底部统计面板（保留原版的累计数据展示）。</summary>
        private void DrawStatsPanel(float y)
        {
            float w = Screen.width;
            var panel = new Rect(w * 0.5f - 340f, y, 680f, 92f);
            GUI.DrawTexture(panel, _panelTex);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), _accentTex);

            GUI.Label(new Rect(panel.x + 16, panel.y + 10, 640, 24),
                $"累计：击杀 {SnakeProgress.TotalKills}    场次 {SnakeProgress.TotalGames}    " +
                $"胜场 {SnakeProgress.WonGames}    占点胜 {SnakeProgress.CaptureWins}    " +
                $"最长存活 {SnakeProgress.BestSurvivalSeconds:0}s", _label);

            var defs = SnakeProgress.Achievements;
            float ax = panel.x + 16, ay = panel.y + 40;
            for (int i = 0; i < defs.Length; i++)
            {
                bool got = SnakeProgress.IsUnlocked(defs[i].Id);
                var st = new GUIStyle(_small);
                st.normal.textColor = got ? Amber : TextDim;
                GUI.Label(new Rect(ax, ay, 210, 20), (got ? "✔ " : "○ ") + defs[i].Name, st);
                ax += 216;
                if (i == 2) { ax = panel.x + 16; ay += 20; }
            }
        }

        private void Select(SnakeMode mode)
        {
            if (OnSelected != null) OnSelected(mode);
        }

        /// <summary>打开设置界面（由 Bootstrap 提供）。</summary>
        private void OpenSettings()
        {
            if (_boot == null) _boot = GetComponent<SnakeBootstrap>();
            if (_boot == null) return;
            _boot.SettingsMenu.Open();
        }
        /// <summary>诊断探针：往 persistentDataPath 写一行，确认点击链路是否执行。</summary>
        public static void Probe(string msg)
        {
            try
            {
                var path = System.IO.Path.Combine(Application.persistentDataPath, "ui_probe.log");
                System.IO.File.AppendAllText(path,
                    $"[{System.DateTime.Now:HH:mm:ss.fff}] {msg}\r\n", System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
