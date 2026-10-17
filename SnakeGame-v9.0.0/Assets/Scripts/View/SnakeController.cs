using ArknightsTD.Core;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Snake;
using UnityEngine;

namespace ArknightsTD.SnakeView
{
    /// <summary>
    /// 贪吃蛇运行时控制器：采集输入 → 推进世界（<see cref="SnakeWorld"/>）→ 同步视图。
    /// <para>
    /// 操作对齐 WPF 原版：<b>鼠标指向转向</b>（蛇头朝鼠标方向）、按住 <b>Ctrl</b> 加速
    /// （5s/2.5s 窗口）、<b>F</b> 模式动作（淘汰之王决斗 / 搜打撤搜索），
    /// 另保留 WASD/方向键作为键盘转向的便捷补充。
    /// </para>
    /// </summary>
    public sealed class SnakeController : MonoBehaviour
    {
        [Header("对局")]
        public SnakeMode Mode = SnakeMode.Classic;
        public int RandomSeed = 20240;

        [Header("撤离模式地图（0=废弃工厂 1=边境森林 2=航天基地）")]
        public int ExtractionMapIndex = 0;

        [Header("相机（2D 正交俯视）")]
        [Tooltip("取景留白系数：1.0 = 恰好装下整个战场")]
        public float CameraZoomPadding = 1.06f;
        [Tooltip("纵横比超过该值时才按宽度取景（超宽屏）")]
        public float UltraWideAspect = 2.4f;

        // ---------------- 运行时状态 ----------------
        public SnakeWorld World { get; private set; }
        public bool Paused { get; private set; }
        /// <summary>设置暂停状态（暂停菜单/ESC 用）。</summary>
        public void SetPaused(bool paused) => Paused = paused;

        private SnakeWorldView _view;
        private SnakeHud _hud;
        private Camera _cam;
        private Transform _root;

        /// <summary>临时提示（如决斗发起结果）。</summary>
        public string StatusMessage { get; private set; } = "";
        public double StatusMessageTimer { get; private set; }

        public SnakeWorldView View => _view;
        public SnakeHud Hud => _hud;

        /// <summary>1 / 2 / 4 倍速。</summary>
        public static readonly double[] SpeedOptions = { 1.0, 2.0, 4.0 };
        public int SpeedIndex { get; private set; }
        public double SpeedMultiplier => SpeedOptions[Mathf.Clamp(SpeedIndex, 0, SpeedOptions.Length - 1)];

        /// <summary>本局新解锁的成就（供 HUD 展示）。</summary>
        public System.Collections.Generic.List<AchievementDef> NewAchievements { get; private set; }
            = new System.Collections.Generic.List<AchievementDef>();

        private void Awake()
        {
            EnsureHud();
            // 世界由 Bootstrap 设置好 Mode/RandomSeed 后显式调用 SetupField 建立。
        }

        private void EnsureHud()
        {
            if (_hud != null) return;
            _hud = GetComponent<SnakeHud>();
            if (_hud == null) _hud = gameObject.AddComponent<SnakeHud>();
            _hud.Bind(this);
        }

        /// <summary>初始化世界、视图与相机。编辑器里可直接调用（无需 Play 模式）做渲染验证。</summary>
        public void SetupField()
        {
            if (World != null) return;
            EnsureHud();

            try
            {
                World = new SnakeWorld(Mode, RandomSeed);
                if (Mode == SnakeMode.Extraction)
                    World.Extraction.SelectMap(ExtractionMapIndex);
                World.Initialize();
                World.StartGame();

                _root = new GameObject("SnakeRoot").transform;
                _view = new SnakeWorldView(_root, World);

                SetupCamera();
                SetupLights();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SnakeController] SetupField 失败：{ex.GetType().Name}: {ex.Message}");
                if (_root != null) { Destroy(_root.gameObject); _root = null; }
                _view = null;
                World = null;
                throw;
            }

            SnakeProgress.BeginGame();
            _lastReportedKills = 0;

            Debug.Log($"[SnakeController] 模式 {Mode}（{SnakeModeConfig.For(Mode).DisplayName}）就绪：" +
                      $"食物 {World.Foods.Count}，敌蛇 {World.Enemies.Count}，" +
                      $"地图 {World.WorldWidth:0}×{World.WorldHeight:0}");
        }

        /// <summary>
        /// 建立 <b>跟随玩家的 2D 正交俯视</b>相机（原版 WPF 的相机始终居中于蛇头，
        /// 世界像素与屏幕像素 1:1，所有尺寸按 <c>Scale</c> 缩放）。
        /// </summary>
        private void SetupCamera()
        {
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null) camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";

            _cam = camGo.GetComponent<Camera>();
            if (_cam == null) _cam = camGo.AddComponent<Camera>();

            // ★ 菜单态会关掉相机，必须显式重新启用（否则黑屏）。
            _cam.enabled = true;

            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.043f, 0.051f, 0.066f, 1f);
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 3000f;

            // 1 世界像素 = 1 屏幕像素（原版 WorldToScreen 不做缩放）
            _cam.orthographicSize = Mathf.Max(1, Screen.height) * 0.5f;
            SnapCameraToPlayer();
        }

        /// <summary>按指定纵横比计算正交尺寸（跟随相机：恒为视口半高）。</summary>
        public float ComputeOrthoSizeFor(float aspect)
            => Mathf.Max(1, Screen.height) * 0.5f;

        /// <summary>相机立即居中到玩家蛇头。</summary>
        public void SnapCameraToPlayer()
        {
            if (_cam == null || World == null) return;
            var head = World.Player.Head;
            _cam.transform.position = new Vector3((float)head.X, (float)head.Y, -10f);
            _cam.transform.rotation = Quaternion.identity;
        }

        /// <summary>纯环境光：亮度由顶点色决定（2D 俯视无立体感贡献）。</summary>
        private void SetupLights()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.42f);

            var key = GameObject.Find("KeyLight");
            if (key != null)
            {
                var l = key.GetComponent<Light>();
                if (l != null) l.intensity = 0f;
            }
        }

        private void Update()
        {
            if (World == null) return;

            // 暂停时冻结一切游戏输入（原版 Paused 时只处理暂停菜单）
            if (Paused) return;

            HandleKeyboard();
            Tick(Time.deltaTime * SpeedMultiplier);
            _view.Sync(World);
            UpdateCamera();
            ReportProgress();
        }
        private int _lastReportedKills;
        private bool _resultReported;


        /// <summary>把本局进度同步给统计系统；一旦分出胜负就结算并检查成就。</summary>
        private void ReportProgress()
        {
            if (World == null) return;

            int kills = World.CurrentGameKills;
            if (kills != _lastReportedKills)
            {
                _lastReportedKills = kills;
                SnakeProgress.ReportGameProgress(World.Time, kills);
            }

            if (!_resultReported && World.Outcome != MatchOutcome.Running)
            {
                _resultReported = true;
                bool won = World.Outcome == MatchOutcome.Won;
                NewAchievements = SnakeProgress.EndGame(World.Mode, won, kills, World.Time);

                // 记入历史排行榜（按模式分别记录；原版只有淘汰之王记录）
                SnakeScores.Record(World.Mode, World.Score,
                    World.PlayerBodyCells, World.Time, won);

                for (int i = 0; i < NewAchievements.Count; i++)
                    Debug.Log($"[SnakeController] 解锁成就：{NewAchievements[i].Name}（{NewAchievements[i].Description}）");
            }
        }

        /// <summary>推进给定秒数（内部按固定步长消化）。</summary>
        public void Tick(double dt)
        {
            if (World == null) return;
            World.Advance(dt);
        }

        // ==================================================================
        //  输入
        // ==================================================================

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
            if (Input.GetKeyDown(KeyCode.P)) Paused = !Paused;
            if (Input.GetKeyDown(KeyCode.Space)) SpeedIndex = (SpeedIndex + 1) % SpeedOptions.Length;

            // F 键轮询（原版 IsKeyPressed(Key.F)：淘汰之王决斗 / 搜打撤搜索）
            World.FKeyDown = Input.GetKey(KeyCode.F);

            // Ctrl 加速（原版）；Shift 作为便捷补充
            bool boosting = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                            || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            World.SetBoosting(boosting);

            if (!World.IsRacing)
            {
                // 键盘转向（便捷补充）；鼠标转向为原版操作方式
                double dx = 0, dy = 0;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dx -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dx += 1;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dy += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dy -= 1;

                if (dx != 0 || dy != 0)
                {
                    World.SetDirection(new Vec2(dx, dy));
                }
                else if (_cam != null)
                {
                    // 鼠标转向（原版 OnMouseMove）：蛇头指向鼠标世界坐标
                    var sp = Input.mousePosition;
                    sp.z = Mathf.Abs(_cam.transform.position.z);
                    var hit = _cam.ScreenToWorldPoint(sp);
                    World.SetMouseAim(new Vec2(hit.x, hit.y));
                }
            }

            if (StatusMessageTimer > 0)
            {
                StatusMessageTimer -= Time.deltaTime;
                if (StatusMessageTimer <= 0) StatusMessage = "";
            }
        }

        /// <summary>跟随玩家 + 窗口尺寸变化时重算取景与 UI 缩放（原版 UpdateCamera / RecalculateScale）。</summary>
        private void UpdateCamera()
        {
            if (_cam == null || World == null) return;
            if (SuppressAutoCamera) return;

            // UI 缩放（原版 min(w/1600, h/900)，影响速度/碰撞/绘制尺寸）
            World.SetScale(Mathf.Min(
                Screen.width / 1600f, Screen.height / 900f));

            int w = Screen.width, h = Screen.height;
            if (w == _lastScreenW && h == _lastScreenH)
            {
                // 尺寸没变也要跟随玩家
                var head = World.Player.Head;
                _cam.transform.position = new Vector3((float)head.X, (float)head.Y, -10f);
                return;
            }
            _lastScreenW = w; _lastScreenH = h;

            _cam.orthographicSize = Mathf.Max(1, h) * 0.5f;
            var hp = World.Player.Head;
            _cam.transform.position = new Vector3((float)hp.X, (float)hp.Y, -10f);
        }

        private int _lastScreenW = -1, _lastScreenH = -1;

        /// <summary>抑制自动相机调整（批量渲染用）。</summary>
        public bool SuppressAutoCamera { get; set; }

        /// <summary>切换暂停。</summary>
        public void TogglePause() => Paused = !Paused;

        /// <summary>设置倍速档位（HUD 调用）。</summary>
        public void SetSpeedIndex(int index)
            => SpeedIndex = Mathf.Clamp(index, 0, SpeedOptions.Length - 1);

        /// <summary>拆掉当前对局（保留本组件以便重新开局）。</summary>
        public void Teardown()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            _view = null;
            World = null;
            Paused = false;
            _resultReported = false;
            _lastReportedKills = 0;
            NewAchievements.Clear();
            SnakeProgress.Save();
        }

        /// <summary>重开当前模式。</summary>
        public void Restart()
        {
            if (_root != null) Destroy(_root.gameObject);
            World = null;
            Paused = false;
            SpeedIndex = 0;
            _resultReported = false;
            _lastReportedKills = 0;
            NewAchievements.Clear();
            SetupField();
        }

        /// <summary>切换模式并重开。</summary>
        public void SwitchMode(SnakeMode mode)
        {
            Mode = mode;
            Restart();
        }

        /// <summary>自检摘要。</summary>
        public string DescribeState() => World != null ? World.DescribeState() : "not initialized";
    }
}
