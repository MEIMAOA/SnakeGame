using System.Collections.Generic;
using ArknightsTD.Core;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Data;
using ArknightsTD.Gameplay;
using UnityEngine;

namespace ArknightsTD.View
{
    /// <summary>玩家交互阶段。</summary>
    public enum DeployPhase
    {
        /// <summary>空闲：点击干员卡，或点击已部署干员查看。</summary>
        Idle,
        /// <summary>已选干员卡，等待点击可部署格。</summary>
        Placing,
        /// <summary>已选好格子，等待选择朝向（术师跳过此阶段）。</summary>
        ChoosingFacing,
    }

    /// <summary>
    /// 塔防战斗控制器：唯一的运行时入口。
    /// <para>
    /// 职责划分：<see cref="BattleSim"/> 负责全部游戏逻辑（纯 C#、可脱离引擎测试），
    /// 本类只负责「驱动它」与「把它的状态画出来」：
    /// 输入 → 部署/撤下 → 推进模拟 → 同步视图。
    /// </para>
    /// <para>
    /// 相机放在网格中心斜上方，朝向下俯视，视野包含整个战场。
    /// 鼠标拾取用 y=0 地面平面的射线求交，与逻辑层的格坐标 (col, row) 一对一。
    /// </para>
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        // ---------------- 战斗配置 ----------------
        [Header("关卡")]
        public int LevelIndex = 1;
        public int RandomSeed = 20240;

        [Header("相机")]
        [Tooltip("俯仰角（度）：越大越接近俯视")]
        public float CameraPitchDegrees = 52f;
        [Tooltip("偏航角（度）：形成斜向构图")]
        public float CameraYawDegrees = 22f;
        [Tooltip("垂直视场角")]
        public float CameraFieldOfView = 44f;
        [Tooltip("取景留白系数：1.0 = 恰好装下战场")]
        public float CameraDistancePadding = 1.06f;
        /// <summary>目标点相对网格中心的 Z 偏移（格）。</summary>
        public float CameraTargetZOffset = 0f;
        /// <summary>目标点高度偏移。</summary>
        public float CameraTargetYOffset = 0f;

        // ---------------- 运行时状态 ----------------
        public BattleSim Sim { get; private set; }
        public DeployPhase Phase { get; private set; } = DeployPhase.Idle;
        public int SelectedOperatorIndex { get; private set; } = -1;
        public int HoverCol { get; private set; } = -1;
        public int HoverRow { get; private set; } = -1;
        public int PendingCol { get; private set; } = -1;
        public int PendingRow { get; private set; } = -1;

        /// <summary>当前选中的<b>已部署</b>干员（HUD 撤退面板用）；null 表示没有选中。</summary>
        public OperatorUnit SelectedOperator { get; private set; }

        /// <summary>本次 ESC 被控制器消费的帧号；BattleBootstrap 据此避免同帧"取消+退出"双重响应。</summary>
        public int EscConsumedFrame { get; private set; } = -1;

        /// <summary>正在从卡片拖拽的索引（-1 表示没有拖拽）。</summary>
        private int _dragCard = -1;

        /// <summary>1 / 2 / 4 倍速。</summary>
        public int SpeedIndex { get; private set; }
        public bool Paused { get; private set; }

        /// <summary>最近一次操作失败的原因（HUD 显示）。</summary>
        public string LastMessage { get; private set; } = "";
        private double _messageUntil;

        public static readonly double[] SpeedOptions = { 1.0, 2.0, 4.0 };
        public double SpeedMultiplier => SpeedOptions[Mathf.Clamp(SpeedIndex, 0, SpeedOptions.Length - 1)];

        // ---------------- 组件 ----------------
        private Camera _cam;
        private BattlefieldBuilder _field;
        private EnemyViews _enemies;
        private OperatorViews _operators;
        private ProjectileViews _projectiles;
        private Transform _root;

        public BattlefieldBuilder Field => _field;
        public EnemyViews Enemies => _enemies;
        public OperatorViews Operators => _operators;
        public ProjectileViews Projectiles => _projectiles;

        // ==================================================================
        //  生命周期
        // ==================================================================

        private void Awake()
        {
            EnsureHud();
            // ★ 不在 Awake 里建关卡（原因同 SnakeController.Awake）：
            //   AddComponent 触发 Awake 时 LevelIndex/RandomSeed 还没被调用方设置，
            //   Awake 抛异常会被引擎吞掉，留下"Sim 已建、视图缺失"的半初始化状态。
            //   关卡一律由调用方设置好参数后显式调用 SetupField 建立。
        }

        /// <summary>确保场上有 HUD 并绑定到本控制器。</summary>
        private void EnsureHud()
        {
            if (_hud != null) return;
            _hud = GetComponent<BattleHud>();
            if (_hud == null) _hud = gameObject.AddComponent<BattleHud>();
            _hud.Bind(this);
        }

        private BattleHud _hud;
        public BattleHud Hud => _hud;

        /// <summary>
        /// 初始化关卡、场景与相机。可在编辑器中直接调用（无需进入 Play 模式）以便渲染验证。
        /// </summary>
        public void SetupField()
        {
            if (Sim != null) return;   // 已初始化
            EnsureHud();

            try
            {
                var level = TDLevels.Get(LevelIndex);
                Sim = new BattleSim();
                // 明日方舟式：先进入准备阶段（不动兵，先看动线、摆干员），按"开始行动"开打
                Sim.Initialize(level, RandomSeed, prepPhase: true);

                _root = new GameObject("BattleRoot").transform;

                _field = new BattlefieldBuilder();
                _field.Build(Sim, _root);
                _field.BuildRoutePreview(Sim);   // 敌人动线预览

                _enemies = new EnemyViews(_root);
                _enemies.PrepareHitFlash();
                _operators = new OperatorViews(_root);
                _projectiles = new ProjectileViews(_root);

                SetupCamera();
                SetupLights();
            }
            catch (System.Exception ex)
            {
                // 清理半初始化状态，保证失败后可以重新开局
                Debug.LogError($"[BattleController] SetupField 失败：{ex.GetType().Name}: {ex.Message}");
                if (_root != null) { Destroy(_root.gameObject); _root = null; }
                _field = null;
                _enemies = null;
                _operators = null;
                _projectiles = null;
                Sim = null;
                throw;
            }

            Debug.Log($"[BattleController] 关卡 {Sim.Level.Index} {Sim.Level.Name} 就绪：" +
                      $"{Sim.GridW}x{Sim.GridH}，静态三角面 {_field.TotalTriangles}，" +
                      $"费用 {Sim.Cost}，生命 {Sim.Lives}，波次 {Sim.Level.TotalWaves}");
        }

        private void SetupCamera()
        {
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null) camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";

            _cam = camGo.GetComponent<Camera>();
            if (_cam == null) _cam = camGo.AddComponent<Camera>();

            // ★ 菜单态会关掉相机；且入口场景的相机默认是正交（贪吃蛇用），
            //   塔防是 3D 斜视，必须显式切回透视并重新启用，否则黑屏或画面错误。
            _cam.enabled = true;
            _cam.orthographic = false;

            float midX = Sim.GridW * 0.5f;
            float midZ = Sim.GridH * 0.5f + CameraTargetZOffset;
            var target = new Vector3(midX, CameraTargetYOffset, midZ);

            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.035f, 0.046f, 0.064f, 1f);
            _cam.fieldOfView = CameraFieldOfView;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 500f;

            // ★ 相机距离由【视场角 + 俯仰角 + 战场包围盒】反解，而不是手调系数。
            //   推法：以网格中心为参考点，相机空间里的竖直坐标差 δv = -Δx·sin(yaw) + Δz·cos(yaw)·sin(pitch)，
            //   屏幕竖直半高 = distance·tan(fov/2)。要装下整图即需 distance ≥ max|δv| / tan(fov/2)。
            //   早先用"高度系数 × 网格高"估算，参数之间互相耦合，试几次都取不准（画面时近时远、角落被裁）。
            float pitch = CameraPitchDegrees * Mathf.Deg2Rad;
            float yaw = CameraYawDegrees * Mathf.Deg2Rad;
            float halfW = Sim.GridW * 0.5f + 1.2f;   // 含外围墙体
            float halfD = Sim.GridH * 0.5f + 1.2f;

            float sinP = Mathf.Sin(pitch), cosP = Mathf.Cos(pitch);
            float sinY = Mathf.Sin(yaw), cosY = Mathf.Cos(yaw);

            // 地平面上包围盒四个角相对中心的竖直偏移极值
            float dv = Mathf.Abs(halfW * sinY) + Mathf.Abs(halfD * cosY) * sinP;
            // 水平方向同理
            float du = Mathf.Abs(halfW * cosY) + Mathf.Abs(halfD * sinY);

            float tanHalfV = Mathf.Tan(CameraFieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanHalfH = tanHalfV * ((float)Screen.width / Mathf.Max(1, Screen.height));

            float distV = dv / Mathf.Max(0.01f, tanHalfV);
            float distU = du / Mathf.Max(0.01f, tanHalfH);
            float dist = Mathf.Max(distV, distU) * CameraDistancePadding;

            // 由距离与俯仰角得到相机位置（沿视线方向后退）
            var dir = new Vector3(-sinY * cosP, -sinP, -cosY * cosP).normalized;
            camGo.transform.position = target - dir * dist;
            camGo.transform.LookAt(target);
        }

        private void SetupLights()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // 明日方舟战场基调：足够亮以看清地形，但保留深色工业感
            RenderSettings.ambientLight = new Color(0.205f, 0.225f, 0.262f);

            if (GameObject.Find("KeyLight") == null)
            {
                var keyGo = new GameObject("KeyLight");
                var key = keyGo.AddComponent<Light>();
                key.type = LightType.Directional;
                key.color = new Color(1.0f, 0.985f, 0.96f);
                key.intensity = 1.25f;
                keyGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            }

            if (GameObject.Find("FillLight") == null)
            {
                var fillGo = new GameObject("FillLight");
                var fill = fillGo.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.color = new Color(0.40f, 0.52f, 0.70f);
                fill.intensity = 0.62f;
                fillGo.transform.rotation = Quaternion.Euler(28f, 145f, 0f);
            }
        }

        private void Update()
        {
            if (Sim == null) return;

            HandleKeyboard();
            if (!Paused) Tick(Time.deltaTime);
            UpdateHover();
            SyncViews();
        }

        /// <summary>
        /// 推进一帧。<paramref name="dt"/> 为真实秒数。抽成公开方法，
        /// 使编辑器（无需 Play 模式）也能驱动同样的逻辑做渲染验证。
        /// </summary>
        public void Tick(double dt)
        {
            if (Sim == null) return;
            Sim.Advance(dt, SpeedMultiplier);

            if (Sim.Phase != BattlePhase.Running && !_progressCommitted)
            {
                _progressCommitted = true;
                if (Sim.Phase == BattlePhase.Won)
                {
                    Sim.CommitProgress();
                    Debug.Log($"[BattleController] 通关，已解锁至第 {PlayerProgress.MaxUnlocked} 关");
                }
                else
                {
                    Debug.Log("[BattleController] 战斗失败");
                }
            }
        }

        private bool _progressCommitted;

        /// <summary>把模拟状态同步到场景。</summary>
        public void SyncViews()
        {
            if (Sim == null) return;
            _enemies.Sync(Sim);
            _operators.Sync(Sim);
            _projectiles.Sync(Sim);
            _field.SetRouteVisible(Sim.PrepPhase);   // 准备阶段显示敌人动线

            // 地面提示：部署 > 选朝向 >（选中干员范围 / 悬停光标）
            _operators.HideOverlays();
            if (Phase == DeployPhase.Placing && SelectedOperatorIndex >= 0)
            {
                _operators.DrawDeployPreview(Sim, SelectedKind(), HoverCol, HoverRow);
            }
            else if (Phase == DeployPhase.ChoosingFacing)
            {
                _operators.DrawFacingPreview(Sim, PendingCol, PendingRow);
            }
            else if (SelectedOperator != null)
            {
                _operators.DrawSelection(Sim, SelectedOperator);
            }
            else if (HoverCol >= 0 && HoverRow >= 0)
            {
                _operators.DrawHover(Sim, HoverCol, HoverRow);
            }
        }

        // ==================================================================
        //  输入
        // ==================================================================

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Space)) SpeedIndex = (SpeedIndex + 1) % SpeedOptions.Length;
            if (Input.GetKeyDown(KeyCode.P)) Paused = !Paused;
            if (Input.GetKeyDown(KeyCode.R)) Restart();

            // 明日方舟：「开始行动」与技能释放
            if (Sim != null && Sim.PrepPhase &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                StartBattleNow();
            if (Input.GetKeyDown(KeyCode.Q)) ActivateSelectedSkill();

            // 明日方舟手感：数字键 1/2/3 直接选干员
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectCard(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectCard(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectCard(2);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EscConsumedFrame = Time.frameCount;
                if (Phase != DeployPhase.Idle || SelectedOperator != null) CancelSelection();
            }
        }

        private void UpdateHover()
        {
            int c, r;
            if (ScreenToCell(Input.mousePosition, out c, out r)) { HoverCol = c; HoverRow = r; }
            else { HoverCol = -1; HoverRow = -1; }

            // HUD 交互优先于战场
            var guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (_hud != null)
            {
                if (_hud.TryConsumePauseClick()) { Paused = !Paused; return; }
                if (_hud.TryConsumeRetreatClick()) { ConfirmRetreat(); return; }
                if (_hud.TryConsumeCancelClick()) { CancelSelection(); return; }
                if (_hud.TryConsumeMenuClick()) { ExitToMenu(); return; }
                if (_hud.TryConsumeStartBattleClick()) { StartBattleNow(); return; }
                if (_hud.TryConsumeSkillClick()) { ActivateSelectedSkill(); return; }

                if (Input.GetMouseButtonDown(0) && _hud.TryConsumeSpeedClick(out int speedIndex))
                {
                    SpeedIndex = speedIndex;
                    return;
                }
                if (Input.GetMouseButtonDown(0) && _hud.TryConsumeCardClick(out int cardIndex))
                {
                    SelectCard(cardIndex);
                    _dragCard = cardIndex;      // 支持"从卡片拖到高台"直接部署
                    return;
                }
            }

            // 拖拽部署：按住期间不响应战场点击，松开时按松手位置结算
            if (_dragCard >= 0)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    _dragCard = -1;
                    if (Phase != DeployPhase.Idle && HoverCol >= 0 && HoverRow >= 0)
                        OnFieldClicked(HoverCol, HoverRow);
                }
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                // 落在 HUD 上的点击不穿透到战场
                if (_hud != null && _hud.PointerOverHud(guiMouse)) return;
                OnFieldClicked(HoverCol, HoverRow);
            }

            if (Input.GetMouseButtonDown(1)) CancelSelection();
        }

        /// <summary>撤退当前选中的干员（返还一半费用）。</summary>
        public void ConfirmRetreat()
        {
            if (Sim == null || SelectedOperator == null) return;
            var op = SelectedOperator;
            var def = TDBalance.Operator(op.Kind);
            if (Sim.TryRemoveOperatorAt(op.Col, op.Row))
                ShowMessage($"已撤退 {def.Name}，返还 {def.Cost / 2} 费用");
            SelectedOperator = null;
        }

        /// <summary>返回主菜单（通知塔防入口拆除本局）。</summary>
        public void ExitToMenu()
        {
            var td = GetComponent<BattleBootstrap>();
            if (td != null) td.ExitToMenu();
        }

        /// <summary>屏幕坐标 → 网格坐标。用 y=0 地面平面射线求交，与逻辑层格坐标一致。</summary>
        public bool ScreenToCell(Vector3 screenPos, out int col, out int row)
        {
            col = row = -1;
            if (_cam == null) return false;

            var ray = _cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            float dist;
            if (!plane.Raycast(ray, out dist)) return false;

            var hit = ray.GetPoint(dist);
            col = Mathf.FloorToInt(hit.x);
            row = Mathf.FloorToInt(hit.z);
            return col >= 0 && col < Sim.GridW && row >= 0 && row < Sim.GridH;
        }

        /// <summary>战场点击：按当前阶段决定行为。</summary>
        public void OnFieldClicked(int col, int row)
        {
            if (Sim == null || Sim.Phase != BattlePhase.Running) return;
            if (col < 0 || row < 0) { CancelSelection(); return; }

            switch (Phase)
            {
                case DeployPhase.Placing:
                    if (Sim.CanDeployAt(col, row))
                    {
                        var kind = SelectedKind();
                        if (kind == OperatorKind.Caster)
                        {
                            // 术师无需选朝向，直接部署（与 WPF 版一致）
                            TryDeploy(kind, col, row, Facing.Down);
                            CancelSelection();
                        }
                        else
                        {
                            PendingCol = col; PendingRow = row;
                            Phase = DeployPhase.ChoosingFacing;
                        }
                    }
                    else
                    {
                        ShowMessage(Sim.Grid[col, row] != BattleSim.CellType.Deployable
                            ? "该格不可部署" : "该格已有干员");
                    }
                    break;

                case DeployPhase.ChoosingFacing:
                    // 点自己所在格附近：用相对位置判断朝向
                    {
                        int dc = col - PendingCol;
                        int dr = row - PendingRow;
                        Facing facing;
                        if (Mathf.Abs(dc) >= Mathf.Abs(dr))
                            facing = dc >= 0 ? Facing.Right : Facing.Left;
                        else
                            facing = dr >= 0 ? Facing.Down : Facing.Up;
                        TryDeploy(SelectedKind(), PendingCol, PendingRow, facing);
                        CancelSelection();
                    }
                    break;

                default:
                    // 空闲：点已部署干员 → 选中（HUD 弹出撤退面板），点空地 → 取消选中
                    var op = Sim.OperatorAt(col, row);
                    if (op != null)
                    {
                        SelectedOperator = op;
                        var d = TDBalance.Operator(op.Kind);
                        ShowMessage($"已选中 {d.Name}：可撤退（返还 {d.Cost / 2}）");
                    }
                    else
                    {
                        SelectedOperator = null;
                    }
                    break;
            }
        }

        private bool TryDeploy(OperatorKind kind, int col, int row, Facing facing)
        {
            string reason;
            if (Sim.TryDeployOperator(kind, col, row, facing, out reason))
            {
                ShowMessage($"已部署 {TDBalance.Operator(kind).Name}");
                return true;
            }
            ShowMessage(reason ?? "部署失败");
            return false;
        }

        private static OperatorKind KindFromCard(int cardIndex)
        {
            var defs = TDBalance.Operators;
            if (cardIndex < 0 || cardIndex >= defs.Length) return OperatorKind.Guard;
            return defs[cardIndex].Kind;
        }

        /// <summary>选择干员卡。</summary>
        public void SelectCard(int cardIndex)
        {
            if (Sim == null || Sim.Phase != BattlePhase.Running) return;

            var kind = KindFromCard(cardIndex);
            if (Sim.Cost < TDBalance.Operator(kind).Cost)
            {
                ShowMessage($"{TDBalance.Operator(kind).Name} 费用不足");
                return;
            }

            if (SelectedOperatorIndex == cardIndex && Phase != DeployPhase.Idle)
            {
                CancelSelection();
                return;
            }

            SelectedOperatorIndex = cardIndex;
            SelectedOperator = null;      // 选卡时取消"已部署干员"选中，避免两套选中并存
            Phase = DeployPhase.Placing;
            PendingCol = PendingRow = -1;
            ShowMessage($"选择部署位置（{TDBalance.Operator(kind).Name}）");
        }

        /// <summary>取消当前选择。</summary>
        public void CancelSelection()
        {
            Phase = DeployPhase.Idle;
            SelectedOperatorIndex = -1;
            SelectedOperator = null;
            _dragCard = -1;
            PendingCol = PendingRow = -1;
            if (_operators != null) _operators.HideOverlays();
        }

        private OperatorKind SelectedKind()
        {
            if (Sim == null || SelectedOperatorIndex < 0) return OperatorKind.Guard;
            return KindFromCard(SelectedOperatorIndex);
        }

        /// <summary>切换暂停。</summary>
        public void TogglePause() => Paused = !Paused;

        /// <summary>是否处于准备阶段（显示动线、等待「开始行动」）。</summary>
        public bool PrepPhase => Sim != null && Sim.PrepPhase;

        /// <summary>开始行动（结束准备阶段，开始波次推进）。</summary>
        public void StartBattleNow()
        {
            if (Sim == null) return;
            if (!Sim.StartBattle()) return;
            ShowMessage("作战开始");
        }

        /// <summary>释放当前选中干员的技能（明日方舟：点干员 → 技能就绪时点技能按钮 / 按 Q）。</summary>
        public bool ActivateSelectedSkill()
        {
            if (Sim == null || SelectedOperator == null) return false;
            var op = SelectedOperator;
            var def = TDBalance.Operator(op.Kind);
            string skillName = def.Skill != null ? def.Skill.Name : "技能";
            if (!op.SkillReady) { ShowMessage($"{skillName}：技力未满"); return false; }
            if (!Sim.ActivateSkill(op.Id)) return false;
            ShowMessage($"{skillName} 发动！");
            return true;
        }

        /// <summary>选中一个已部署干员（HUD 点击列表/战场点击都会走这里）。</summary>
        public void SelectDeployedOperator(OperatorUnit op) => SelectedOperator = op;

        /// <summary>Restart：重置关卡。</summary>
        public void Restart()
        {
            if (_root != null) Destroy(_root.gameObject);
            Sim = null;
            _progressCommitted = false;
            Phase = DeployPhase.Idle;
            SelectedOperatorIndex = -1;
            SelectedOperator = null;
            _dragCard = -1;
            SpeedIndex = 0;
            Paused = false;
            SetupField();
        }

        /// <summary>
        /// 彻底拆除本局：清掉战场根节点、模拟与 HUD，回到未初始化状态。
        /// 供塔防退出回主菜单时调用；之后再次 <see cref="SetupField"/> 等同于全新开局。
        /// </summary>
        public void Teardown()
        {
            if (_root != null) { SafeDestroy(_root.gameObject); _root = null; }
            if (_hud != null) { SafeDestroy(_hud); _hud = null; }
            Sim = null;
            _progressCommitted = false;
            Phase = DeployPhase.Idle;
            SelectedOperatorIndex = -1;
            SelectedOperator = null;
            _dragCard = -1;
            Paused = false;
        }

        /// <summary>编辑器非播放态用 DestroyImmediate（Destroy 在编辑模式下不生效），运行时用 Destroy。</summary>
        private static void SafeDestroy(UnityEngine.Object o)
        {
            if (o == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(o); return; }
#endif
            UnityEngine.Object.Destroy(o);
        }

        private void ShowMessage(string msg)
        {
            LastMessage = msg;
            _messageUntil = Time.timeAsDouble + 2.5;
        }

        /// <summary>当前应显示的消息（过期返回空串）。</summary>
        public string CurrentMessage()
            => Time.timeAsDouble <= _messageUntil ? LastMessage : "";

        // ==================================================================
        //  只读查询（供 HUD 与验证使用）
        // ==================================================================

        public BattlePhase BattlePhase => Sim != null ? Sim.Phase : BattlePhase.Running;
        public int Cost => Sim != null ? Sim.Cost : 0;
        public int Lives => Sim != null ? Sim.Lives : 0;
        public int MaxLives => Sim != null ? Sim.MaxLives : 0;
        public int Score => Sim != null ? Sim.Score : 0;
        public int CurrentWave => Sim != null ? Sim.CurrentWave : 0;

        /// <summary>自检摘要（编辑器渲染验证用）。</summary>
        public string DescribeState()
            => Sim == null ? "not initialized"
             : $"phase={Sim.Phase} wave={Sim.CurrentWave}/{Sim.Level.TotalWaves} " +
               $"lives={Sim.Lives}/{Sim.MaxLives} cost={Sim.Cost} score={Sim.Score} " +
               $"enemies={Sim.Enemies.Count} operators={Sim.Operators.Count} " +
               $"projectiles={Sim.Projectiles.Count} time={Sim.Time:0.0}s";
    }
}
