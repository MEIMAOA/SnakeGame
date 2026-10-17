using UnityEngine;
using ArknightsTD.Core;
using ArknightsTD.Snake;

namespace ArknightsTD.SnakeView
{
    /// <summary>启动器状态。</summary>
    public enum BootstrapState { Menu, Playing }

    /// <summary>
    /// 贪吃蛇运行时入口：先出模式选择菜单，选定后创建对应模式的控制器。
    /// <para>
    /// 用法：打开 <c>Assets/Scenes/Game.unity</c> → Play，或直接运行打包好的 exe。
    /// </para>
    /// <para>
    /// 状态机只有两态（<see cref="BootstrapState.Menu"/> / <see cref="BootstrapState.Playing"/>），
    /// 且菜单是否绘制<b>由"有没有正在跑的对局"决定</b>，不依赖组件 enabled——
    /// 之前用 enabled 开关时，一旦开局链路中途抛异常，菜单就会和游戏画面叠在一起。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SnakeBootstrap : MonoBehaviour
    {
        [Header("启动")]
        [Tooltip("启动时显示模式选择菜单")]
        public bool ShowMenuOnStart = true;

        [Tooltip("不显示菜单时，直接以该模式开局")]
        public SnakeMode StartMode = SnakeMode.Classic;

        [Tooltip("随机种子；相同种子 + 相同操作 = 相同战局")]
        public int RandomSeed = 20240;

        [Tooltip("搜打撤模式地图（0=废弃工厂 1=边境森林 2=航天基地）")]
        public int ExtractionMapIndex = 0;

        private SnakeController _controller;
        private SnakeModeMenu _menu;
        private SnakeUiRoot _ui;

        /// <summary>界面总控（开始界面 / 模式选择 / 设置 / 成就 / 排行）。</summary>
        public SnakeUiRoot Ui
        {
            get
            {
                if (_ui == null) _ui = GetComponent<SnakeUiRoot>();
                if (_ui == null)
                {
                    _ui = gameObject.AddComponent<SnakeUiRoot>();
                    _ui.Bind(this);
                }
                return _ui;
            }
        }

        /// <summary>当前状态。</summary>
        public BootstrapState State { get; private set; } = BootstrapState.Menu;

        /// <summary>当前控制器（未开局时为 null）。</summary>
        public SnakeController Controller => _controller;

        /// <summary>是否应绘制菜单：只有"菜单态且没有对局"才画。</summary>
        public bool ShouldDrawMenu => State == BootstrapState.Menu && _controller == null;

        /// <summary>
        /// 模式选择菜单。按需创建——编辑器里 AddComponent 不会触发 Awake，
        /// 只靠 Awake 建菜单的话，非 Play 模式的调用方（含自动化验证）会拿到 null。
        /// </summary>
        public SnakeModeMenu Menu => EnsureMenu();

        private void Awake()
        {
            Probe("Awake");
            EnsureMenu();
            _ = Ui;                       // 建立界面总控

            // 启动时载入设置并应用到美术系统（原版在 MainWindow 构造里做同样的事）
            Settings = AppSettings.Load();
            SnakeSettingsMenu.ApplyToSkin(Settings);
            ApplyRuntimeSettings(Settings);
        }

        /// <summary>当前设置。</summary>
        public AppSettings Settings { get; private set; }

        /// <summary>设置界面（按需创建）。</summary>
        public SnakeSettingsMenu SettingsMenu
        {
            get
            {
                if (_settingsMenu == null)
                {
                    _settingsMenu = GetComponent<SnakeSettingsMenu>();
                    if (_settingsMenu == null)
                        _settingsMenu = gameObject.AddComponent<SnakeSettingsMenu>();
                    _settingsMenu.OnClosed = () =>
                    {
                        // 关掉设置后回到菜单态，重新应用一次存档里的设置
                        var s = AppSettings.Load();
                        SnakeSettingsMenu.ApplyToSkin(s);
                        ApplyRuntimeSettings(s);
                        ShowMenu();
                    };
                }
                return _settingsMenu;
            }
        }

        private SnakeSettingsMenu _settingsMenu;

        /// <summary>把设置里能立刻生效的项应用到引擎。</summary>
        public static void ApplyRuntimeSettings(AppSettings s)
        {
            if (s == null) return;
            QualitySettings.vSyncCount = s.Vsync ? 1 : 0;
            Application.targetFrameRate = s.FpsTarget;
            QualitySettings.antiAliasing = s.AntiAlias ? 4 : 0;
        }

        private void Start()
        {
            Probe("Start");
            // 命令行启动参数（打包后的 exe 也支持）：
            //   -menu            强制显示模式选择菜单
            //   -play            跳过菜单，直接用 StartMode 模式开局
            //   -mode <名称>     classic / timed / team / extreme / extraction / td
            //   -td              直接进入塔防模式（等价 -mode td）
            var args = System.Environment.GetCommandLineArgs();
            bool forceMenu = HasArg(args, "-menu");
            bool skipMenu = HasArg(args, "-play");

            // 塔防模式（正式内容）：直接开局
            if (!forceMenu && (HasArg(args, "-td") || IsTowerDefenseModeArg(args)))
            {
                Probe("Start: 直接进入塔防");
                Ui.StartTowerDefense();
                return;
            }

            var byArg = ParseModeArg(args);
            if (byArg.HasValue)
            {
                StartMode = byArg.Value;
                // ★ 指定了 -mode 就意味着"直接进这个模式"，必须同时跳过菜单。
                //   先前只设置 StartMode 而没有置 skipMenu，结果即使传了 -mode classic
                //   仍然走 ShowMenuOnStart=true 的分支显示菜单（实测确认）。
                skipMenu = true;
            }

            if (forceMenu || (ShowMenuOnStart && !skipMenu)) ShowStartScreen();
            else StartModeNow(StartMode);
        }

        private static bool HasArg(string[] args, string flag)
        {
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], flag, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary><c>-mode td</c> / <c>-mode towerdefense</c> 表示进入塔防模式。</summary>
        private static bool IsTowerDefenseModeArg(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], "-mode", System.StringComparison.OrdinalIgnoreCase)) continue;
                string v = args[i + 1].ToLowerInvariant();
                return v == "td" || v == "towerdefense" || v == "tower_defense";
            }
            return false;
        }

        /// <summary>解析 <c>-mode &lt;名称&gt;</c>；无法识别时返回 null。</summary>
        private static SnakeMode? ParseModeArg(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], "-mode", System.StringComparison.OrdinalIgnoreCase)) continue;
                switch (args[i + 1].ToLowerInvariant())
                {
                    case "classic": return SnakeMode.Classic;
                    case "timed": return SnakeMode.Timed;
                    case "team": case "team4v4": return SnakeMode.Team4v4;
                    case "extreme": return SnakeMode.Extreme;
                    case "extraction": return SnakeMode.Extraction;
                }
            }
            return null;
        }

        /// <summary>
        /// 启动探针：往 persistentDataPath 写一行。
        /// Debug.Log 在打包版里未必能可靠取到（实测 Player.log 为空），
        /// 因此用文件探针确认生命周期回调真的执行了。
        /// </summary>
        private static void Probe(string where)
        {
            try
            {
                var path = System.IO.Path.Combine(Application.persistentDataPath, "bootstrap_probe.log");
                System.IO.File.AppendAllText(path,
                    $"[{System.DateTime.Now:HH:mm:ss}] {where}\r\n",
                    System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private SnakeModeMenu EnsureMenu()
        {
            if (_menu == null) _menu = GetComponent<SnakeModeMenu>();
            if (_menu == null) _menu = gameObject.AddComponent<SnakeModeMenu>();
            return _menu;
        }

        /// <summary>回到开始界面（会先停掉当前对局）。</summary>
        public void ShowStartScreen()
        {
            TearDownGame();
            State = BootstrapState.Menu;
            if (_settingsMenu != null && _settingsMenu.IsVisible) return;
            Ui.GoStart();

            var cam = Camera.main;
            if (cam != null) cam.enabled = false;
        }

        /// <summary>兼容旧调用：等同于回到开始界面。</summary>
        public void ShowMenu() => ShowStartScreen();

        /// <summary>进入对局后回到开始界面（Esc 用）。</summary>
        private void BackToStartFromGame()
        {
            TearDownGame();
            State = BootstrapState.Menu;
            var cam = Camera.main;
            if (cam != null) cam.enabled = false;
            Ui.GoStart();
        }


        /// <summary>以指定模式开局。</summary>
        public void StartModeNow(SnakeMode mode)
        {
            // 先清场，避免上一次对局的视图/状态残留
            TearDownGame();
            State = BootstrapState.Playing;

            var cam = Camera.main;
            if (cam != null) cam.enabled = true;

            try
            {
                _controller = GetComponent<SnakeController>();
                if (_controller == null) _controller = gameObject.AddComponent<SnakeController>();
                _controller.Mode = mode;
                _controller.RandomSeed = RandomSeed;
                _controller.ExtractionMapIndex = ExtractionMapIndex;
                _controller.SetupField();
            }
            catch (System.Exception ex)
            {
                // 开局链路任何一步失败都要整体回退到菜单，不能停在"菜单关了、画面空白"的半坏状态
                Debug.LogError($"[SnakeBootstrap] {mode} 开局异常：{ex.GetType().Name}: {ex.Message}，退回菜单");
                ShowStartScreen();
                return;
            }

            // ★ 兜底：世界【和视图】都必须建立成功才算开局；
            //   只查 World 不够——曾出现过 World 已建、视图因异常缺失、
            //   结果菜单关了画面却一片空白的情况（打包版实测）。
            if (_controller.World == null || _controller.View == null)
            {
                Debug.LogError($"[SnakeBootstrap] {mode} 开局失败：世界或视图未建立，退回菜单");
                ShowStartScreen();
                return;
            }

            // 通知界面总控切到对局（它会在对局期间停止绘制所有界面）
            if (_ui != null) _ui.SetPlayingScreen();

            Debug.Log($"[SnakeBootstrap] 开局：{mode}（{SnakeModeConfig.For(mode).DisplayName}）" +
                      $" 状态={State} 菜单应绘制={ShouldDrawMenu}");
        }

        private void Update()
        {
            // 设置界面打开时：Esc 关闭设置
            if (_settingsMenu != null && _settingsMenu.IsVisible)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) _settingsMenu.Close();
                return;
            }

            if (State != BootstrapState.Playing) return;
            if (_controller == null) return;

            // ESC：切换暂停菜单（对齐原版 Key.Escape → Paused = !Paused; PauseSelection = 0）
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_ui != null && _ui.Current == UiScreen.Paused) _ui.ResumeGame();
                else
                {
                    if (_controller != null) _controller.SetPaused(true);
                    if (_ui != null) _ui.ShowPause();
                }
            }
        }

        private void TearDownGame()
        {
            if (_controller != null) _controller.Teardown();
            _controller = null;
        }

        /// <summary>退出游戏时保存统计。</summary>
        private void OnDestroy() => SnakeProgress.Save();
    }
}
