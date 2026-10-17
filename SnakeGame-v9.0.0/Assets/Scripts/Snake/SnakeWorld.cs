using System;
using System.Collections.Generic;
using ArknightsTD.Core;
using ArknightsTD.Core.Geometry;

namespace ArknightsTD.Snake
{
    /// <summary>游戏模式。</summary>
    public enum SnakeMode { Classic, Timed, Team4v4, Extreme, Extraction }

    /// <summary>对局结果。</summary>
    public enum MatchOutcome { Running, Won, Lost }

    /// <summary>道具类型。</summary>
    public enum PowerUpKind { SpeedBoost, Shield, ScoreGem, Magnet }

    /// <summary>蛇（玩家 / AI 敌蛇）。位置与方向均为世界坐标（像素，与 WPF 一致）。</summary>
    public sealed class SnakeBody
    {
        /// <summary>蛇身路径点，索引 0 = 头。</summary>
        public readonly List<Vec2> Points = new List<Vec2>();
        public Vec2 Direction = new Vec2(1, 0);
        public string Name = "";
        public int Score;
        public bool Alive = true;
        public string Team = "red";
        public int Lives = 1;
        public double SpeedMultiplier = 1.0;
        public double InvincibleTimer;

        /// <summary>
        /// 稳定的 AI 相位种子。原版用 <c>enemy.GetHashCode()*0.001</c>（进程相关、
        /// 不可复现），这里在出生时赋一个确定性值，使 AI 行为可复现。
        /// </summary>
        public double PhaseSeed;

        /// <summary>稳定整数 ID。原版计时赛把 GetHashCode() 当 id 参与伪随机公式，这里用 AiId。</summary>
        public int AiId;

        public Vec2 Head => Points.Count > 0 ? Points[0] : new Vec2(0, 0);

        public SnakeBody() { }

        /// <summary>初始 3 段身体，段距 16px（原版 Enemy 构造）。</summary>
        public SnakeBody(Vec2 start, string name)
        {
            Name = name;
            Points.Add(start);
            Points.Add(new Vec2(start.X - 16, start.Y));
            Points.Add(new Vec2(start.X - 32, start.Y));
        }
    }

    /// <summary>食物（原版 Food）。</summary>
    public sealed class FoodItem
    {
        public Vec2 Position;
        public Rgb Color;
        public double Radius;
        /// <summary>分值（可能为小数，击杀掉落精确分配 AI 分数用）。</summary>
        public double ScoreValue = 1.0;
        /// <summary>击杀掉落标记：吃完不再生成新食物。</summary>
        public bool IsFromKill;

        public FoodItem() { }
        public FoodItem(Vec2 pos, Rgb color, double radius)
        { Position = pos; Color = color; Radius = radius; }
    }

    /// <summary>场上道具（原版 PowerUp）。</summary>
    public sealed class PowerUpItem
    {
        public Vec2 Position;
        public PowerUpKind Kind;
        public double PulsePhase;
        public double Life = 20.0;
        public double MaxLife = 20.0;

        /// <summary>道具颜色（原版 GetColor）。</summary>
        public static Rgb ColorOf(PowerUpKind k)
        {
            switch (k)
            {
                case PowerUpKind.SpeedBoost: return Rgb.FromHex(0xFFC80A);
                case PowerUpKind.Shield: return Rgb.FromHex(0x50B4FF);
                case PowerUpKind.ScoreGem: return Rgb.FromHex(0x00DC78);
                default: return Rgb.FromHex(0xFF64C8);
            }
        }
    }

    /// <summary>飘字（原版 ScorePopup）。</summary>
    public sealed class ScorePopup
    {
        public Vec2 Position;
        public string Text = "";
        public Rgb Color = Rgb.FromHex(0xFFFFFF);
        public double Life = 1.2;
        public double MaxLife = 1.2;
    }

    /// <summary>粒子（原版 Particle）。</summary>
    public sealed class Particle
    {
        public Vec2 Position;
        public Vec2 Velocity;
        public Rgb Color;
        public double Life;
        public double MaxLife = 1.0;
        public double Radius;
    }

    /// <summary>
    /// 贪吃蛇核心模拟。忠实移植 WPF 版 <c>MainWindow.xaml.cs</c> 的宿主逻辑
    /// （主循环 / 玩家蛇 / 敌蛇 / 食物 / 道具 / 碰撞 / 结算 / 慈悲·惩罚局），
    /// 模式专属规则由 <see cref="SnakeGameModeBase"/> 的 5 个派生类负责
    /// （对应原版 ClassicMode / TimedMode / TeamMode / ExtremeMode / ExtractionMode）。
    /// <para>
    /// 坐标与数值：世界 5000×4000 像素，速度 = 每帧（1/60s）步进的像素数，
    /// 与原版完全同数；所有计时用固定步长 <see cref="FixedStep"/>（原版 60Hz 定时器）。
    /// </para>
    /// </summary>
    public sealed class SnakeWorld
    {
        // ==================== 常量（原版 MainWindow 常量区）====================

        /// <summary>固定逻辑步长（原版 60Hz DispatcherTimer）。</summary>
        public const double FixedStep = 1.0 / 60.0;

        /// <summary>单次 Advance 最多执行的步数（失控保护）。</summary>
        public const int MaxStepsPerAdvance = 1200;

        /// <summary>玩家/敌蛇基础速度（px/帧，原版 BASE_SPEED_BASE）。</summary>
        public const double BaseSpeedPerStep = 4.5;

        /// <summary>蛇身段半径（px，原版 BASE_SEGMENT_RADIUS_BASE）。</summary>
        public const double SegmentRadius = 8;

        /// <summary>蛇身段间距（px，原版 Enemy 构造的 16px）。</summary>
        public const double SegmentSpacing = 16;

        /// <summary>食物半径（原版 FOOD_RADIUS_BASE）。</summary>
        public const double FoodRadius = 6;

        /// <summary>击杀掉落食物半径（原版 LARGE_FOOD_RADIUS_BASE）。</summary>
        public const double LargeFoodRadius = 10;

        /// <summary>默认世界尺寸（原版 5000×4000）。</summary>
        public const double DefaultWorldWidth = 5000;
        public const double DefaultWorldHeight = 4000;

        /// <summary>经典模式胜利分数。</summary>
        public const double ClassicWinScore = 3000;

        /// <summary>极限模式胜利时间（秒）。</summary>
        public const double ExtremeWinTime = 180.0;

        /// <summary>普通食物数 / 极限模式食物数。</summary>
        public const int FoodCountNormal = 125;
        public const int FoodCountExtreme = 60;

        /// <summary>敌人数（classic / extreme）。</summary>
        public const int EnemyCountClassic = 10;
        public const int EnemyCountExtreme = 20;

        /// <summary>动态补充敌人上限与间隔。</summary>
        public const int MaxEnemies = 50;
        public const double SpawnDelay = 3.0;

        /// <summary>Ctrl 加速持续窗口（秒）；极限模式减半。</summary>
        public const double BoostDuration = 5.0;
        public const double BoostDurationExtreme = 2.5;
        public const double BoostMultiplier = 2.0;

        /// <summary>连击窗口（秒）与单次得分上限。</summary>
        public const double ComboWindow = 3.0;
        public const int ComboMaxGain = 10;

        // ---- 道具 ----
        public const double PowerUpSpawnInterval = 8.0;
        public const double PowerUpLifetime = 20.0;
        public const int MaxPowerUps = 3;
        public const double SpeedBoostDuration = 8.0;
        public const double SpeedBoostMultiplier = 1.5;
        public const double ShieldDuration = 15.0;
        public const double MagnetDuration = 10.0;
        public const double GemValue = 50;
        public const double MagnetRange = 280;
        public const double MagnetPullPerSecond = 260;

        // ---- 占点 ----
        public const double CaptureRadius = 150;
        public const double CaptureRate = 1.0;
        public const double CaptureWinProgress = 30;

        // ---- 极限模式 ----
        public const double ExtremeScoreGrowthPerSec = 0.005;
        public const double ExtremeScoreMaxMult = 2.0;
        public const double FoodRainInterval = 30.0;
        public const int FoodRainCount = 25;

        // ---- 慈悲 / 惩罚局 ----
        public const int MercyThreshold = 5;
        public const int WinStreakThreshold = 10;
        public const double PunishSpeedFactor = 3.0;
        public const double PunishTeleportInterval = 3.0;
        public const double PunishTeleportChance = 0.20;
        public const double PunishTeleportRange = 200.0;
        public const double PunishTeleportMinDist = 100.0;
        public const double PunishScoreBonus = 2.0;

        // ==================== 世界状态 ====================

        public SnakeMode Mode { get; private set; }
        public MatchOutcome Outcome { get; private set; } = MatchOutcome.Running;

        /// <summary>UI 缩放（原版 Scale=min(w/1600,h/900)）。宿主每帧按窗口尺寸更新。</summary>
        public double Scale { get; private set; } = 1.0;

        /// <summary>更新 UI 缩放（原版 RecalculateScale）。影响速度、碰撞半径与绘制尺寸。</summary>
        public void SetScale(double scale)
            => Scale = Math.Max(0.1, Math.Min(4.0, scale));

        /// <summary>把待生成的敌人出生点与进度（0..1）复制出来（原版出生预告红圈）。</summary>
        public void GetPendingSpawns(List<Vec2> positions, List<double> progress)
        {
            positions.Clear();
            progress.Clear();
            for (int i = 0; i < _spawnPositions.Count; i++)
            {
                positions.Add(_spawnPositions[i]);
                progress.Add(Math.Max(0, Math.Min(1, _spawnTimers[i] / SpawnDelay)));
            }
        }

        /// <summary>世界尺寸（撤离模式随地图变化）。</summary>
        public double WorldWidth => _worldW;
        public double WorldHeight => _worldH;
        public double WorldHalfW => _worldW * 0.5;
        public double WorldHalfH => _worldH * 0.5;
        private double _worldW = DefaultWorldWidth;
        private double _worldH = DefaultWorldHeight;

        /// <summary>本局累计存活时间（秒）。</summary>
        public double Time { get; private set; }

        /// <summary>玩家蛇。</summary>
        public readonly SnakeBody Player = new SnakeBody();
        /// <summary>AI 敌蛇。</summary>
        public readonly List<SnakeBody> Enemies = new List<SnakeBody>();
        /// <summary>食物。</summary>
        public readonly List<FoodItem> Foods = new List<FoodItem>();
        /// <summary>道具。</summary>
        public readonly List<PowerUpItem> PowerUps = new List<PowerUpItem>();
        /// <summary>飘字。</summary>
        public readonly List<ScorePopup> Popups = new List<ScorePopup>();
        /// <summary>粒子。</summary>
        public readonly List<Particle> Particles = new List<Particle>();

        /// <summary>分数的小数部分（原版 _fractionalScore，击杀掉落按体积精确分配）。</summary>
        public double FractionalScore { get; private set; }

        /// <summary>连击。</summary>
        public int ComboCount { get; private set; }
        public double ComboTimer { get; private set; }

        /// <summary>分数倍率（极限模式随时间提升）。</summary>
        public double ScoreMultiplier { get; private set; } = 1.0;

        // ---- 效果计时器（原版 UpdateEffects）----
        public double SpeedBoostTimer { get; private set; }
        public double ShieldTimer { get; private set; }
        public double MagnetTimer { get; private set; }
        public double InvincibleTimer { get; private set; }

        // ---- 加速（Ctrl，原版 Boosting + BoostStartTime）----
        public bool Boosting { get; private set; }
        public double BoostStartTime { get; private set; }

        /// <summary>玩家速度倍率（计时赛按等级写入，原版 SpeedMultiplier 字段）。</summary>
        public double PlayerSpeedMultiplier = 1.0;

        // ---- 慈悲 / 惩罚局 ----
        public bool IsMercyRound { get; private set; }
        public bool IsPunishRound { get; private set; }

        /// <summary>开发者无敌（原版 _devInvincible，测试用）。</summary>
        public bool DevInvincible { get; set; }

        // ---- 统计（原版 GameStats + 连死/连胜）----
        public int TotalKills { get; private set; }
        public int CurrentGameKills { get; private set; }
        public int WonGames { get; private set; }
        public int CaptureWins { get; private set; }

        // ---- 占点 ----
        public double CaptureProgress { get; private set; }
        public Vec2 CaptureZone { get; private set; } = new Vec2(0, 0);
        public bool InCaptureZone { get; private set; }

        /// <summary>场上蓝队 / 红队（占点模式用，与 Enemies 同对象）。</summary>
        public readonly List<SnakeBody> BlueTeam = new List<SnakeBody>();
        public readonly List<SnakeBody> RedTeam = new List<SnakeBody>();

        // ==================== 模式 ====================

        /// <summary>当前模式对象（对应原版 _currentMode）。</summary>
        public SnakeGameModeBase ActiveMode { get; private set; }

        public SnakeTimedMode Timed => ActiveMode as SnakeTimedMode;
        public SnakeExtractionMode Extraction => ActiveMode as SnakeExtractionMode;

        // ==================== 输入 ====================

        /// <summary>本帧 F 键是否按下（原版 IsKeyPressed(Key.F) 轮询）。</summary>
        public bool FKeyDown { get; set; }

        // ==================== 内部 ====================

        private readonly Random _rand;
        private double _stepAccumulator;

        private readonly List<Vec2> _spawnPositions = new List<Vec2>();
        private readonly List<double> _spawnTimers = new List<double>();
        private double _powerUpSpawnTimer = 5.0;
        private double _foodRainTimer = FoodRainInterval;
        private double _punishTeleportTimer;

        /// <summary>食物色板（原版 FOOD_COLORS：红/橙/黄/粉/紫/青/绿）。</summary>
        private static readonly Rgb[] FoodPalette =
        {
            Rgb.FromHex(0xFF0000), Rgb.FromHex(0xFFA500), Rgb.FromHex(0xFFFF00),
            Rgb.FromHex(0xFFC0CB), Rgb.FromHex(0x800080), Rgb.FromHex(0x00FFFF),
            Rgb.FromHex(0x008000),
        };

        public SnakeWorld(SnakeMode mode, int seed = 20240)
        {
            Mode = mode;
            _rand = new Random(seed);
            // 模式对象在构造时即创建（原版 StartGame 里 new），
            // 这样开赛前就能访问模式状态（如撤离模式选图）。
            ActiveMode = CreateMode(Mode);
            ActiveMode.Bind(this);
        }

        // ==================================================================
        //  生命周期（对应原版 InitializeGame / StartGame）
        // ==================================================================

        /// <summary>重置一局：世界尺寸、玩家蛇、食物、计时器（原版 InitializeGame）。</summary>
        public void Initialize()
        {
            SetWorldSizeInternal(DefaultWorldWidth, DefaultWorldHeight);
            Outcome = MatchOutcome.Running;
            Time = 0;

            Player.Points.Clear();
            Player.Points.Add(new Vec2(0, 0));
            Player.Points.Add(new Vec2(-16, 0));
            Player.Points.Add(new Vec2(-32, 0));
            Player.Direction = new Vec2(1, 0);
            Player.Score = 0;
            Player.Alive = true;
            Player.Name = "玩家";
            Player.Team = "blue";
            Player.Lives = 1;
            Player.SpeedMultiplier = 1.0;
            Player.InvincibleTimer = 0;

            Enemies.Clear();
            BlueTeam.Clear();
            RedTeam.Clear();
            Foods.Clear();
            PowerUps.Clear();
            Popups.Clear();
            Particles.Clear();

            CaptureZone = new Vec2(0, 0);
            CaptureProgress = 0;

            _spawnPositions.Clear();
            _spawnTimers.Clear();

            ComboCount = 0;
            ComboTimer = 0;
            SpeedBoostTimer = ShieldTimer = MagnetTimer = InvincibleTimer = 0;
            FractionalScore = 0;
            ScoreMultiplier = 1.0;
            _foodRainTimer = FoodRainInterval;
            _punishTeleportTimer = 0;
            PlayerSpeedMultiplier = 1.0;
            Boosting = false;
            CurrentGameKills = 0;
            FKeyDown = false;
            _stepAccumulator = 0;

            // 重置模式对象状态（对象在构造时已创建）
            ActiveMode.Initialize();

            GenerateFoods(Mode == SnakeMode.Extreme ? FoodCountExtreme : FoodCountNormal);
        }

        /// <summary>开始一局：结算慈悲/惩罚局状态并生成敌人（原版 StartGame 后半段）。</summary>
        public void StartGame()
        {
            Outcome = MatchOutcome.Running;
            Player.Score = 0;
            FractionalScore = 0;
            Time = 0;
            CurrentGameKills = 0;

            // ---- 慈悲 / 惩罚局判定（原版：连胜 ≥10 → 惩罚局；连死 ≥5 → 慈悲局）----
            if (SnakeProgress.ConsecutiveWins >= WinStreakThreshold)
            {
                IsPunishRound = true;
                IsMercyRound = false;
                SnakeProgress.ConsumeWinStreak();
            }
            else if (SnakeProgress.ConsecutiveDeaths >= MercyThreshold)
            {
                IsPunishRound = false;
                IsMercyRound = true;
                SnakeProgress.ConsumeDeathStreak();
            }
            else
            {
                IsPunishRound = false;
                IsMercyRound = false;
            }

            if (Mode == SnakeMode.Timed && Timed != null)
            {
                Timed.StartGameAfterSelection();
            }
            else if (Mode == SnakeMode.Extraction)
            {
                // ★ 搜打撤的敌人由模式自己按地图生成（原版 ExtractionMode.Initialize →
                //   SpawnExtractionEnemies）。原版宿主随后调用的 SpawnInitialEnemies
                //   会先 Enemies.Clear() 再因 extraction 提前返回，把刚生成的敌人清掉
                //   （v8.0.0 的宿主排序 bug，会导致搜打撤一个敌人都没有）。
                //   这里按模式设计意图保留地图敌人，不做那次清除。
            }
            else
            {
                SpawnInitialEnemies();
            }
        }

        private static SnakeGameModeBase CreateMode(SnakeMode mode)
        {
            switch (mode)
            {
                case SnakeMode.Timed: return new SnakeTimedMode();
                case SnakeMode.Team4v4: return new SnakeTeamMode();
                case SnakeMode.Extreme: return new SnakeExtremeMode();
                case SnakeMode.Extraction: return new SnakeExtractionMode();
                default: return new SnakeClassicMode();
            }
        }

        /// <summary>切换撤离地图（菜单选图 / 测试用；原版在开局前选图）。</summary>
        public void SelectExtractionMap(int mapIndex)
        {
            var ext = Extraction;
            if (ext == null) return;
            ext.SelectMap(mapIndex);
            Initialize();
            StartGame();
        }

        // ==================================================================
        //  输入
        // ==================================================================

        /// <summary>提交一个方向输入（测试 / 预览用；游戏内方向由鼠标控制）。</summary>
        public void SetDirection(Vec2 dir)
        {
            // 零向量输入必须忽略（原版鼠标输入 len>1 才转向）
            if (Math.Abs(dir.X) < 1e-9 && Math.Abs(dir.Y) < 1e-9) return;
            var n = dir.Normalized();
            // 禁止 180° 反向（防瞬间自杀）
            if (Math.Abs(n.X + Player.Direction.X) < 1e-9 &&
                Math.Abs(n.Y + Player.Direction.Y) < 1e-9) return;
            Player.Direction = n;
        }

        /// <summary>
        /// 鼠标瞄准（原版 OnMouseMove）：蛇头指向鼠标世界坐标，
        /// 距离 &gt; 1 才转向，避免原地抖动。
        /// </summary>
        public void SetMouseAim(Vec2 worldPoint)
        {
            var head = Player.Head;
            double dx = worldPoint.X - head.X;
            double dy = worldPoint.Y - head.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 1) Player.Direction = new Vec2(dx / len, dy / len);
        }

        /// <summary>Ctrl 加速按下 / 松开（原版 OnKeyDown/OnKeyUp）。</summary>
        public void SetBoosting(bool boosting)
        {
            if (boosting && !Boosting) BoostStartTime = Time;
            if (!boosting) BoostStartTime = 0;
            Boosting = boosting;
        }

        // ==================================================================
        //  推进（原版 GameTimer_Tick）
        // ==================================================================

        /// <summary>按固定步长推进。<paramref name="dt"/> 为真实秒数。</summary>
        public void Advance(double dt)
        {
            if (Outcome != MatchOutcome.Running) return;
            if (dt <= 0 || double.IsNaN(dt)) return;

            const double Eps = 1e-9;
            _stepAccumulator += dt;
            int steps = 0;
            while (_stepAccumulator >= FixedStep - Eps && steps < MaxStepsPerAdvance)
            {
                Step();
                _stepAccumulator -= FixedStep;
                if (_stepAccumulator < Eps) _stepAccumulator = 0;
                steps++;
                if (Outcome != MatchOutcome.Running) break;
            }
        }

        /// <summary>一个逻辑帧（原版 GameTimer_Tick 顺序）。</summary>
        private void Step()
        {
            double dt = FixedStep;
            Time += dt;

            ActiveMode?.UpdateBeforeMove();

            UpdateSnake();

            UpdateEffects(dt);
            UpdatePowerUps(dt);
            UpdateParticles(dt);
            UpdatePopups(dt);

            if (Mode == SnakeMode.Classic || Mode == SnakeMode.Extreme)
                HandleEnemySpawning();

            UpdateEnemies();
            ActiveMode?.UpdateAI();

            TryPickupPowerUp();

            ActiveMode?.UpdateAfterMove();
            if (Mode == SnakeMode.Team4v4) UpdateCaptureProgress();

            CheckFoodCollision();
            CheckEnemyCollision();

            if (Mode == SnakeMode.Extreme)
            {
                ScoreMultiplier = Math.Min(ExtremeScoreMaxMult,
                    1.0 + Time * ExtremeScoreGrowthPerSec);

                _foodRainTimer -= dt;
                if (_foodRainTimer <= 0)
                {
                    _foodRainTimer = FoodRainInterval;
                    SpawnFoodRain();
                }
            }

            if (IsPunishRound)
            {
                _punishTeleportTimer -= dt;
                if (_punishTeleportTimer <= 0)
                {
                    _punishTeleportTimer = PunishTeleportInterval;
                    ApplyPunishTeleport();
                }
            }

            if (Mode == SnakeMode.Classic && Player.Score >= ClassicWinScore)
            {
                GameOver(true);
                return;
            }
            if (Mode == SnakeMode.Extreme && Time >= ExtremeWinTime)
            {
                GameOver(true);
                return;
            }
        }

        /// <summary>玩家蛇移动（原版 UpdateSnake）。搜索期间冻结；撞墙即死。</summary>
        private void UpdateSnake()
        {
            if (Player.Points.Count == 0) return;
            // ★ 撤离模式搜索期间玩家蛇冻结（原版）
            if (Mode == SnakeMode.Extraction && Extraction != null && Extraction.IsPlayerSearching)
                return;

            double speed = GetCurrentSpeed();
            var head = Player.Head;
            var next = new Vec2(head.X + Player.Direction.X * speed,
                                head.Y + Player.Direction.Y * speed);
            double margin = SegmentRadius * 2;

            if (next.X < -WorldHalfW + margin || next.X > WorldHalfW - margin ||
                next.Y < -WorldHalfH + margin || next.Y > WorldHalfH - margin)
            {
                if (DevInvincible)
                {
                    next = new Vec2(
                        Math.Max(-WorldHalfW + margin, Math.Min(WorldHalfW - margin, next.X)),
                        Math.Max(-WorldHalfH + margin, Math.Min(WorldHalfH - margin, next.Y)));
                }
                else
                {
                    GameOver(false);
                    return;
                }
            }
            Player.Points.Insert(0, next);
        }

        /// <summary>玩家速度（原版 GetCurrentSpeed）。</summary>
        public double GetCurrentSpeed()
        {
            double speed = BaseSpeedPerStep * Scale;
            double boostDur = Mode == SnakeMode.Extreme ? BoostDurationExtreme : BoostDuration;
            if (Boosting && (Time - BoostStartTime) < boostDur)
                speed *= BoostMultiplier;
            if (SpeedBoostTimer > 0) speed *= SpeedBoostMultiplier;
            return speed * PlayerSpeedMultiplier;
        }

        /// <summary>敌蛇动态补充（原版 HandleEnemySpawning，仅 classic/extreme）。</summary>
        private void HandleEnemySpawning()
        {
            if (Enemies.Count < MaxEnemies && _rand.NextDouble() < 0.02)
            {
                _spawnPositions.Add(RandomPosition());
                _spawnTimers.Add(0);
            }
            for (int i = _spawnTimers.Count - 1; i >= 0; i--)
            {
                _spawnTimers[i] += FixedStep;
                if (_spawnTimers[i] >= SpawnDelay)
                {
                    if (Enemies.Count < MaxEnemies)
                        Enemies.Add(MakeEnemy(_spawnPositions[i], "AI" + _rand.Next(1000), Enemies.Count));
                    _spawnTimers.RemoveAt(i);
                    _spawnPositions.RemoveAt(i);
                }
            }
        }

        /// <summary>敌蛇移动 / 吃食物 / 长度裁剪（原版 UpdateEnemies）。</summary>
        private void UpdateEnemies()
        {
            double aiDiff;
            switch (Mode)
            {
                case SnakeMode.Classic: aiDiff = 0.6; break;
                case SnakeMode.Timed: aiDiff = 1.3; break;
                case SnakeMode.Extreme: aiDiff = 1.5; break;
                case SnakeMode.Team4v4: aiDiff = 0.9; break;
                default: aiDiff = 2.5; break;
            }

            double mercySpeedFactor = IsMercyRound ? 0.55 : 1.0;

            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = Enemies[i];
                if (enemy.Points.Count == 0) continue;

                double punishSpeed = IsPunishRound ? PunishSpeedFactor : 1.0;
                double speed = BaseSpeedPerStep * Scale * aiDiff * enemy.SpeedMultiplier
                               * mercySpeedFactor * punishSpeed;
                if (enemy.InvincibleTimer > 0) enemy.InvincibleTimer -= FixedStep;

                double segR = SegmentRadius;
                double boundaryMargin = segR * 2;

                var newHead = new Vec2(
                    enemy.Head.X + enemy.Direction.X * speed,
                    enemy.Head.Y + enemy.Direction.Y * speed);

                // 惩罚局跳过边界检查（穿墙）；否则撞界反弹（反转 180° ± 抖动）
                if (!IsPunishRound)
                {
                    bool hitBoundary = false;
                    if (newHead.X < -WorldHalfW + boundaryMargin)
                    { newHead = new Vec2(-WorldHalfW + boundaryMargin, newHead.Y); hitBoundary = true; }
                    else if (newHead.X > WorldHalfW - boundaryMargin)
                    { newHead = new Vec2(WorldHalfW - boundaryMargin, newHead.Y); hitBoundary = true; }
                    if (newHead.Y < -WorldHalfH + boundaryMargin)
                    { newHead = new Vec2(newHead.X, -WorldHalfH + boundaryMargin); hitBoundary = true; }
                    else if (newHead.Y > WorldHalfH - boundaryMargin)
                    { newHead = new Vec2(newHead.X, WorldHalfH - boundaryMargin); hitBoundary = true; }

                    if (hitBoundary)
                    {
                        double jitter = (_rand.NextDouble() - 0.5) * 0.4;
                        double angle = Math.Atan2(enemy.Direction.Y, enemy.Direction.X) + Math.PI + jitter;
                        enemy.Direction = new Vec2(Math.Cos(angle), Math.Sin(angle));
                    }
                }

                enemy.Points.Insert(0, newHead);

                // 敌蛇吃食物（原版 UpdateEnemies 内嵌逻辑）
                for (int j = Foods.Count - 1; j >= 0; j--)
                {
                    var food = Foods[j];
                    double dx = newHead.X - food.Position.X;
                    double dy = newHead.Y - food.Position.Y;
                    double rr = segR + food.Radius;
                    if (dx * dx + dy * dy < rr * rr)
                    {
                        enemy.Score++;
                        enemy.Points.Add(enemy.Points[enemy.Points.Count - 1]);

                        bool isFromKill = food.IsFromKill;
                        Foods.RemoveAt(j);
                        if (!isFromKill)
                            Foods.Add(CreateFood(FoodPalette[_rand.Next(FoodPalette.Length)]));
                        break;
                    }
                }

                // 目标长度 = 3 + 分数，超长裁尾（保底 3 段）
                int targetLen = 3 + enemy.Score;
                while (enemy.Points.Count > targetLen && enemy.Points.Count > 3)
                    enemy.Points.RemoveAt(enemy.Points.Count - 1);
            }
        }

        /// <summary>玩家吃食物 / 连击计分 / 裁尾（原版 CheckFoodCollision）。</summary>
        private void CheckFoodCollision()
        {
            if (Player.Points.Count == 0) return;
            var head = Player.Head;
            double segR = SegmentRadius;
            for (int i = Foods.Count - 1; i >= 0; i--)
            {
                var food = Foods[i];
                double dx = head.X - food.Position.X;
                double dy = head.Y - food.Position.Y;
                double rr = segR + food.Radius;
                if (dx * dx + dy * dy < rr * rr)
                {
                    bool isFromKill = food.IsFromKill;
                    double scoreValue = food.ScoreValue;
                    var foodColor = food.Color;
                    var foodPos = food.Position;

                    Foods.RemoveAt(i);

                    if (!isFromKill)
                        Foods.Add(CreateFood(FoodPalette[_rand.Next(FoodPalette.Length)]));

                    double roundBonus = IsPunishRound ? PunishScoreBonus : 1.0;

                    if (isFromKill)
                    {
                        double finalScore = scoreValue * ScoreMultiplier * roundBonus;
                        GrantScore(finalScore);
                        AddPopup(foodPos, "+" + finalScore.ToString("0.#"), foodColor);
                    }
                    else
                    {
                        if (ComboTimer > 0) ComboCount++;
                        else ComboCount = 1;
                        ComboTimer = ComboWindow;

                        int baseGain = Math.Min(ComboCount + 1, ComboMaxGain);
                        int scoreGain = (int)Math.Round(baseGain * ScoreMultiplier * roundBonus);
                        if (scoreGain < 1) scoreGain = 1;
                        Player.Score += scoreGain;
                        AddPopup(foodPos, "+" + scoreGain,
                            ComboCount >= 3 ? Rgb.FromHex(0xFFC80A) : foodColor);
                    }

                    SpawnParticles(foodPos, foodColor, 6);
                    return;   // 该帧不裁尾 → 蛇长 +1
                }
            }
            // 没吃到 → 裁尾（长度恒定）
            if (Player.Points.Count > 1) Player.Points.RemoveAt(Player.Points.Count - 1);
        }

        /// <summary>碰撞判定（原版 CheckEnemyCollision；timed 只做 AI 互杀）。</summary>
        private void CheckEnemyCollision()
        {
            if (Player.Points.Count == 0) return;
            if (DevInvincible) return;
            if (InvincibleTimer > 0) return;

            if (Mode == SnakeMode.Timed)
            {
                CheckAIvsAICollision();
                return;
            }

            var playerHead = Player.Head;
            double segR = SegmentRadius;
            double thr = (segR * 2) * (segR * 2);
            var dead = new HashSet<SnakeBody>();

            // 1. 玩家头撞敌蛇身体 → 护盾抵挡或死亡
            for (int i = 0; i < Enemies.Count; i++)
            {
                var enemy = Enemies[i];
                if (Mode == SnakeMode.Team4v4 && enemy.Team == "blue") continue;
                for (int j = 1; j < enemy.Points.Count; j++)
                {
                    double dx = playerHead.X - enemy.Points[j].X;
                    double dy = playerHead.Y - enemy.Points[j].Y;
                    if (dx * dx + dy * dy < thr)
                    {
                        if (TryBlockWithShield(playerHead)) return;
                        GameOver(false);
                        return;
                    }
                }
            }

            // 2. 玩家头撞敌头 → 敌扣命（0.5s 无敌帧），命尽击杀
            for (int i = 0; i < Enemies.Count; i++)
            {
                var enemy = Enemies[i];
                if (Mode == SnakeMode.Team4v4 && enemy.Team == "blue") continue;
                if (enemy.Points.Count == 0) continue;
                double dx = playerHead.X - enemy.Head.X;
                double dy = playerHead.Y - enemy.Head.Y;
                if (dx * dx + dy * dy < thr)
                {
                    if (enemy.InvincibleTimer > 0) continue;
                    enemy.Lives--;
                    enemy.InvincibleTimer = 0.5;
                    if (enemy.Lives <= 0 && !dead.Contains(enemy)) KillEnemy(enemy, dead);
                }
            }

            // 3. 敌头撞玩家身体 → 敌死
            for (int i = 0; i < Enemies.Count; i++)
            {
                var enemy = Enemies[i];
                if (Mode == SnakeMode.Team4v4 && enemy.Team == "blue") continue;
                if (enemy.Points.Count == 0) continue;
                var eh = enemy.Head;
                for (int j = 1; j < Player.Points.Count; j++)
                {
                    double dx = eh.X - Player.Points[j].X;
                    double dy = eh.Y - Player.Points[j].Y;
                    if (dx * dx + dy * dy < thr)
                    {
                        if (!dead.Contains(enemy)) KillEnemy(enemy, dead);
                        break;
                    }
                }
            }

            // 4. classic / extreme：AI 互杀（头撞他敌身体）
            if (Mode == SnakeMode.Classic || Mode == SnakeMode.Extreme)
            {
                for (int i = 0; i < Enemies.Count; i++)
                {
                    var e1 = Enemies[i];
                    if (e1.Points.Count == 0 || dead.Contains(e1)) continue;
                    for (int k = i + 1; k < Enemies.Count; k++)
                    {
                        var e2 = Enemies[k];
                        if (e2.Points.Count == 0 || dead.Contains(e2)) continue;
                        for (int m = 0; m < e2.Points.Count; m++)
                        {
                            double dx = e1.Head.X - e2.Points[m].X;
                            double dy = e1.Head.Y - e2.Points[m].Y;
                            if (dx * dx + dy * dy < thr)
                            {
                                e2.Lives--;
                                if (e2.Lives <= 0 && !dead.Contains(e2)) KillEnemy(e2, dead);
                                break;
                            }
                        }
                        if (!dead.Contains(e2))
                        {
                            for (int m = 0; m < e1.Points.Count; m++)
                            {
                                double dx = e2.Head.X - e1.Points[m].X;
                                double dy = e2.Head.Y - e1.Points[m].Y;
                                if (dx * dx + dy * dy < thr)
                                {
                                    e1.Lives--;
                                    if (e1.Lives <= 0 && !dead.Contains(e1)) KillEnemy(e1, dead);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            RemoveDead(dead);
        }

        /// <summary>计时赛：AI 头-头互撞各扣一命（原版 CheckAIvsAICollision）。</summary>
        private void CheckAIvsAICollision()
        {
            double segR = SegmentRadius;
            double thr = (segR * 2) * (segR * 2);
            var dead = new HashSet<SnakeBody>();

            for (int i = 0; i < Enemies.Count; i++)
            {
                var e1 = Enemies[i];
                if (e1.Points.Count == 0 || dead.Contains(e1)) continue;
                for (int k = i + 1; k < Enemies.Count; k++)
                {
                    var e2 = Enemies[k];
                    if (e2.Points.Count == 0 || dead.Contains(e2)) continue;
                    double dx = e1.Head.X - e2.Head.X;
                    double dy = e1.Head.Y - e2.Head.Y;
                    if (dx * dx + dy * dy < thr)
                    {
                        e1.Lives--;
                        e2.Lives--;
                        if (e1.Lives <= 0 && !dead.Contains(e1)) KillEnemy(e1, dead);
                        if (e2.Lives <= 0 && !dead.Contains(e2)) KillEnemy(e2, dead);
                    }
                }
            }

            RemoveDead(dead);
        }

        private void RemoveDead(HashSet<SnakeBody> dead)
        {
            if (dead.Count == 0) return;
            for (int i = Enemies.Count - 1; i >= 0; i--)
                if (dead.Contains(Enemies[i])) Enemies.RemoveAt(i);
            BlueTeam.RemoveAll(e => dead.Contains(e));
            RedTeam.RemoveAll(e => dead.Contains(e));
        }

        /// <summary>击杀一名敌人：统计 + 掉落（原版 KillEnemy）。</summary>
        public void KillEnemy(SnakeBody enemy, HashSet<SnakeBody> dead)
        {
            if (enemy == null || dead.Contains(enemy)) return;
            dead.Add(enemy);
            TotalKills++;
            CurrentGameKills++;
            SpawnFoodsFromEnemy(enemy);
        }

        /// <summary>按敌人身体段掉落食物，吃完全部等于其总分（原版 SpawnFoodsFromEnemy）。</summary>
        private void SpawnFoodsFromEnemy(SnakeBody enemy)
        {
            if (enemy == null || enemy.Points.Count == 0) return;

            int bodyLen = enemy.Points.Count;
            int totalScore = Math.Max(enemy.Score, 1);
            double perFoodScore = (double)totalScore / bodyLen;

            for (int i = 0; i < bodyLen; i++)
            {
                var seg = enemy.Points[i];
                var food = new FoodItem(
                    new Vec2(seg.X + (_rand.NextDouble() - 0.5) * 40 * Scale,
                             seg.Y + (_rand.NextDouble() - 0.5) * 40 * Scale),
                    Rgb.FromHex(0xFF0000),
                    LargeFoodRadius * Scale)
                {
                    ScoreValue = perFoodScore,
                    IsFromKill = true,
                };
                Foods.Add(food);
            }
        }

        // ==================================================================
        //  占点（原版 UpdateCaptureProgress）
        // ==================================================================

        private void UpdateCaptureProgress()
        {
            if (Player.Points.Count == 0) return;
            int blue = 0, red = 0;
            double r2 = CaptureRadius * Scale * (CaptureRadius * Scale);

            double dxp = Player.Head.X - CaptureZone.X;
            double dyp = Player.Head.Y - CaptureZone.Y;
            if (dxp * dxp + dyp * dyp < r2) blue++;
            InCaptureZone = blue > 0;

            for (int i = 0; i < BlueTeam.Count; i++)
            {
                var e = BlueTeam[i];
                if (!Enemies.Contains(e) || e.Points.Count == 0) continue;
                double dx = e.Head.X - CaptureZone.X;
                double dy = e.Head.Y - CaptureZone.Y;
                if (dx * dx + dy * dy < r2) blue++;
            }
            for (int i = 0; i < RedTeam.Count; i++)
            {
                var e = RedTeam[i];
                if (!Enemies.Contains(e) || e.Points.Count == 0) continue;
                double dx = e.Head.X - CaptureZone.X;
                double dy = e.Head.Y - CaptureZone.Y;
                if (dx * dx + dy * dy < r2) red++;
            }

            CaptureProgress += (blue - red) * CaptureRate * FixedStep;
            CaptureProgress = Math.Max(-30, Math.Min(30, CaptureProgress));

            if (CaptureProgress >= CaptureWinProgress)
            {
                CaptureWins++;
                GameOver(true);
            }
            else if (CaptureProgress <= -CaptureWinProgress)
            {
                GameOver(false);
            }
        }

        /// <summary>蓝队存活人数 / 红队存活人数（HUD 用，玩家算蓝队）。</summary>
        public int BlueAliveCount
        {
            get
            {
                int n = Player.Points.Count > 0 ? 1 : 0;
                for (int i = 0; i < BlueTeam.Count; i++)
                    if (Enemies.Contains(BlueTeam[i]) && BlueTeam[i].Points.Count > 0) n++;
                return n;
            }
        }

        public int RedAliveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < RedTeam.Count; i++)
                    if (Enemies.Contains(RedTeam[i]) && RedTeam[i].Points.Count > 0) n++;
                return n;
            }
        }

        // ==================================================================
        //  食物 / 敌人生成
        // ==================================================================

        /// <summary>创建食物：随机位置，避开玩家与敌蛇（50 次重试，原版 CreateFood）。</summary>
        public FoodItem CreateFood(Rgb color, double radius = -1)
        {
            if (radius < 0) radius = FoodRadius * Scale;
            double segR = SegmentRadius;
            for (int i = 0; i < 50; i++)
            {
                var pos = RandomPosition();
                if (pos.X < -WorldHalfW + radius || pos.X > WorldHalfW - radius ||
                    pos.Y < -WorldHalfH + radius || pos.Y > WorldHalfH - radius) continue;
                bool bad = false;
                for (int s = 0; s < Player.Points.Count; s++)
                {
                    double dx = Player.Points[s].X - pos.X, dy = Player.Points[s].Y - pos.Y;
                    double rr = segR + radius;
                    if (dx * dx + dy * dy < rr * rr) { bad = true; break; }
                }
                if (bad) continue;
                for (int e = 0; e < Enemies.Count && !bad; e++)
                    for (int s = 0; s < Enemies[e].Points.Count; s++)
                    {
                        double dx = Enemies[e].Points[s].X - pos.X, dy = Enemies[e].Points[s].Y - pos.Y;
                        double rr = segR + radius;
                        if (dx * dx + dy * dy < rr * rr) { bad = true; break; }
                    }
                if (!bad) return new FoodItem(pos, color, radius);
            }
            return new FoodItem(RandomPosition(), color, radius);
        }

        public void GenerateFoods(int count)
        {
            Foods.Clear();
            for (int i = 0; i < count; i++)
                Foods.Add(CreateFood(FoodPalette[_rand.Next(FoodPalette.Length)]));
        }

        private void SpawnFoodRain()
        {
            for (int i = 0; i < FoodRainCount; i++)
                Foods.Add(CreateFood(FoodPalette[_rand.Next(FoodPalette.Length)]));
            var center = Player.Points.Count > 0 ? Player.Head : new Vec2(0, 0);
            AddPopup(center, "食物雨！", Rgb.FromHex(0x50C8FF));
        }

        private SnakeBody MakeEnemy(Vec2 pos, string name, int id)
        {
            var e = new SnakeBody(pos, name)
            {
                AiId = id,
                PhaseSeed = id * 1.37 % 4.0,
                Team = "red",
                Lives = 1,
                SpeedMultiplier = 1.0,
                Score = 0,
            };
            return e;
        }

        /// <summary>开局敌人生成（原版 SpawnInitialEnemies）。</summary>
        private void SpawnInitialEnemies()
        {
            Enemies.Clear();
            BlueTeam.Clear();
            RedTeam.Clear();
            double minDistSq = Math.Pow(SegmentRadius * 4, 2);

            int enemyCount = Mode == SnakeMode.Extreme ? EnemyCountExtreme : EnemyCountClassic;

            if (Mode == SnakeMode.Team4v4)
            {
                CaptureZone = new Vec2(0, 0);
                // 玩家出生在右边缘，朝左（原版阵型）
                Player.Points.Clear();
                Player.Points.Add(new Vec2(WorldHalfW - 200 * Scale, 0));
                Player.Points.Add(new Vec2(WorldHalfW - 216 * Scale, 0));
                Player.Points.Add(new Vec2(WorldHalfW - 232 * Scale, 0));
                Player.Direction = new Vec2(-1, 0);

                for (int i = 0; i < 4; i++)
                {
                    var pos = new Vec2(-WorldHalfW + 200 * Scale, -WorldHalfH + 100 * Scale + i * 200 * Scale);
                    var e = new SnakeBody(pos, "R" + i) { Team = "red", Direction = new Vec2(1, 0) };
                    e.AiId = i; e.PhaseSeed = i * 1.37 % 4.0;
                    Enemies.Add(e); RedTeam.Add(e);
                }
                for (int i = 0; i < 3; i++)
                {
                    var pos = new Vec2(WorldHalfW - 200 * Scale, -WorldHalfH + 150 * Scale + i * 200 * Scale);
                    var e = new SnakeBody(pos, "B" + i) { Team = "blue", Direction = new Vec2(-1, 0) };
                    e.AiId = 4 + i; e.PhaseSeed = (4 + i) * 1.37 % 4.0;
                    Enemies.Add(e); BlueTeam.Add(e);
                }
                return;
            }

            if (Mode == SnakeMode.Extraction) return;

            for (int i = 0; i < enemyCount; i++)
            {
                Vec2 pos = RandomPosition();
                for (int j = 0; j < 50; j++)
                {
                    pos = RandomPosition();
                    bool bad = false;
                    for (int s = 0; s < Player.Points.Count; s++)
                    {
                        double dx = Player.Points[s].X - pos.X, dy = Player.Points[s].Y - pos.Y;
                        if (dx * dx + dy * dy < minDistSq) { bad = true; break; }
                    }
                    if (bad) continue;
                    for (int e = 0; e < Enemies.Count; e++)
                    {
                        if (Enemies[e].Points.Count == 0) continue;
                        double dx = Enemies[e].Head.X - pos.X, dy = Enemies[e].Head.Y - pos.Y;
                        if (dx * dx + dy * dy < minDistSq) { bad = true; break; }
                    }
                    if (!bad) break;
                }
                Enemies.Add(MakeEnemy(pos, "AI" + i, i));
            }
        }

        /// <summary>撤离模式敌人（原版 SpawnExtractionEnemies）。</summary>
        public void SpawnExtractionEnemies(int count, double speedMultiplier, int lives)
        {
            Enemies.Clear();
            BlueTeam.Clear();
            RedTeam.Clear();
            for (int i = 0; i < count; i++)
            {
                var spawn = RandomPosition();
                Enemies.Add(new SnakeBody(spawn, "无敌AI")
                {
                    SpeedMultiplier = speedMultiplier,
                    Lives = lives,
                    AiId = i,
                    PhaseSeed = i * 1.37 % 4.0,
                });
            }
        }

        public Vec2 RandomPosition(double margin = 50)
        {
            margin *= Scale;
            double minX = -WorldHalfW + margin, maxX = WorldHalfW - margin;
            double minY = -WorldHalfH + margin, maxY = WorldHalfH - margin;
            if (maxX <= minX) maxX = minX + 1;
            if (maxY <= minY) maxY = minY + 1;
            return new Vec2(
                minX + _rand.NextDouble() * (maxX - minX),
                minY + _rand.NextDouble() * (maxY - minY));
        }

        /// <summary>设置世界尺寸（原版 SetWorldSizeInternal，模式 Initialize 调用）。</summary>
        public void SetWorldSizeInternal(double w, double h)
        {
            _worldW = w;
            _worldH = h;
        }

        // ==================================================================
        //  计分
        // ==================================================================

        /// <summary>直接加分（原版 AddScore，撤离模式搜索领取用）。</summary>
        public void AddScore(int amount) => Player.Score += amount;

        /// <summary>小数分数累积（原版 GrantScore）。</summary>
        private void GrantScore(double amount)
        {
            FractionalScore += amount;
            while (FractionalScore >= 1.0)
            {
                Player.Score++;
                FractionalScore -= 1.0;
            }
        }

        // ==================================================================
        //  道具 / 效果 / 粒子 / 飘字
        // ==================================================================

        private void UpdateEffects(double dt)
        {
            if (SpeedBoostTimer > 0) SpeedBoostTimer -= dt;
            if (ShieldTimer > 0) ShieldTimer -= dt;
            if (MagnetTimer > 0) MagnetTimer -= dt;
            if (InvincibleTimer > 0) InvincibleTimer -= dt;

            if (ComboTimer > 0)
            {
                ComboTimer -= dt;
                if (ComboTimer <= 0) ComboCount = 0;
            }

            if (MagnetTimer > 0 && Player.Points.Count > 0) ApplyMagnet(dt);
        }

        private void ApplyMagnet(double dt)
        {
            var head = Player.Head;
            double range = MagnetRange * Scale;
            double rangeSq = range * range;
            double pullSpeed = MagnetPullPerSecond * Scale * dt;
            for (int i = 0; i < Foods.Count; i++)
            {
                var food = Foods[i];
                double dx = head.X - food.Position.X;
                double dy = head.Y - food.Position.Y;
                double distSq = dx * dx + dy * dy;
                if (distSq < rangeSq && distSq > 4)
                {
                    double dist = Math.Sqrt(distSq);
                    double step = Math.Min(pullSpeed, dist * 0.5);
                    food.Position = new Vec2(
                        food.Position.X + dx / dist * step,
                        food.Position.Y + dy / dist * step);
                }
            }
        }

        private void UpdatePowerUps(double dt)
        {
            _powerUpSpawnTimer -= dt;
            if (_powerUpSpawnTimer <= 0
                && (Mode == SnakeMode.Classic || Mode == SnakeMode.Extreme))
            {
                _powerUpSpawnTimer = PowerUpSpawnInterval;
                if (PowerUps.Count < MaxPowerUps) SpawnPowerUp();
            }

            for (int i = PowerUps.Count - 1; i >= 0; i--)
            {
                var p = PowerUps[i];
                p.Life -= dt;
                p.PulsePhase += dt * 4.5;
                if (p.Life <= 0) PowerUps.RemoveAt(i);
            }
        }

        private void SpawnPowerUp()
        {
            var kinds = new[]
            {
                PowerUpKind.SpeedBoost, PowerUpKind.Shield,
                PowerUpKind.ScoreGem, PowerUpKind.Magnet
            };
            PowerUps.Add(new PowerUpItem
            {
                Position = RandomPosition(80),
                Kind = kinds[_rand.Next(kinds.Length)],
                Life = PowerUpLifetime,
                MaxLife = PowerUpLifetime,
            });
        }

        /// <summary>道具拾取（原版 TryPickupPowerUp）。</summary>
        private void TryPickupPowerUp()
        {
            if (Player.Points.Count == 0) return;
            var head = Player.Head;
            double pickupR = SegmentRadius + 18 * Scale;
            double pickupSq = pickupR * pickupR;

            for (int i = PowerUps.Count - 1; i >= 0; i--)
            {
                var p = PowerUps[i];
                double dx = head.X - p.Position.X;
                double dy = head.Y - p.Position.Y;
                if (dx * dx + dy * dy < pickupSq)
                {
                    ActivatePowerUp(p);
                    PowerUps.RemoveAt(i);
                }
            }
        }

        private void ActivatePowerUp(PowerUpItem p)
        {
            var color = PowerUpItem.ColorOf(p.Kind);
            switch (p.Kind)
            {
                case PowerUpKind.SpeedBoost:
                    SpeedBoostTimer = Math.Max(SpeedBoostTimer, SpeedBoostDuration);
                    AddPopup(p.Position, "加速！", color);
                    break;
                case PowerUpKind.Shield:
                    ShieldTimer = Math.Max(ShieldTimer, ShieldDuration);
                    AddPopup(p.Position, "护盾！", color);
                    break;
                case PowerUpKind.ScoreGem:
                    int gemValue = (int)(GemValue * (IsPunishRound ? PunishScoreBonus : 1.0));
                    Player.Score += gemValue;
                    AddPopup(p.Position, "+" + gemValue, color);
                    break;
                case PowerUpKind.Magnet:
                    MagnetTimer = Math.Max(MagnetTimer, MagnetDuration);
                    AddPopup(p.Position, "磁铁！", color);
                    break;
            }
            SpawnParticles(p.Position, color, 24);
        }

        /// <summary>测试钩子：直接激活道具效果（不走拾取判定）。</summary>
        public void ApplyPowerUpForTest(PowerUpKind kind)
            => ActivatePowerUp(new PowerUpItem { Position = Player.Head, Kind = kind });

        /// <summary>护盾抵挡一次致死碰撞（原版 TryBlockWithShield）。</summary>
        private bool TryBlockWithShield(Vec2 collisionPos)
        {
            if (ShieldTimer <= 0) return false;
            ShieldTimer = 0;
            InvincibleTimer = 1.5;
            SpawnParticles(collisionPos, Rgb.FromHex(0x50B4FF), 32);
            AddPopup(collisionPos, "护盾抵挡！", Rgb.FromHex(0x50B4FF));
            return true;
        }

        private void UpdateParticles(double dt)
        {
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                var p = Particles[i];
                p.Life -= dt;
                if (p.Life <= 0) { Particles.RemoveAt(i); continue; }
                p.Position = new Vec2(p.Position.X + p.Velocity.X * dt,
                                      p.Position.Y + p.Velocity.Y * dt);
                p.Velocity = new Vec2(p.Velocity.X * 0.94, p.Velocity.Y * 0.94);
            }
            if (Particles.Count > 300)
                Particles.RemoveRange(0, Particles.Count - 300);
        }

        private void UpdatePopups(double dt)
        {
            for (int i = Popups.Count - 1; i >= 0; i--)
            {
                var s = Popups[i];
                s.Life -= dt;
                if (s.Life <= 0) { Popups.RemoveAt(i); continue; }
                s.Position = new Vec2(s.Position.X, s.Position.Y - 55 * Scale * dt);
            }
        }

        public void SpawnParticles(Vec2 worldPos, Rgb color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                double angle = _rand.NextDouble() * Math.PI * 2;
                double speed = (70 + _rand.NextDouble() * 220) * Scale;
                Particles.Add(new Particle
                {
                    Position = worldPos,
                    Velocity = new Vec2(Math.Cos(angle) * speed, Math.Sin(angle) * speed),
                    Color = color,
                    Life = 0.4 + _rand.NextDouble() * 0.5,
                    MaxLife = 0.9,
                    Radius = (2 + _rand.NextDouble() * 3) * Scale
                });
            }
        }

        public void AddPopup(Vec2 worldPos, string text, Rgb color)
        {
            Popups.Add(new ScorePopup
            {
                Position = worldPos,
                Text = text,
                Color = color,
                Life = 1.2,
                MaxLife = 1.2
            });
            if (Popups.Count > 30) Popups.RemoveAt(0);
        }

        /// <summary>惩罚局瞬移（原版 ApplyPunishTeleport）。</summary>
        private void ApplyPunishTeleport()
        {
            if (Player.Points.Count == 0) return;
            var playerHead = Player.Head;
            double segR = SegmentRadius;

            for (int i = 0; i < Enemies.Count; i++)
            {
                var enemy = Enemies[i];
                if (enemy.Points.Count == 0) continue;
                if (_rand.NextDouble() > PunishTeleportChance) continue;

                double angle = _rand.NextDouble() * Math.PI * 2;
                double dist = PunishTeleportMinDist + _rand.NextDouble() * PunishTeleportRange * Scale;
                var newPos = new Vec2(
                    playerHead.X + Math.Cos(angle) * dist,
                    playerHead.Y + Math.Sin(angle) * dist);

                SpawnParticles(newPos, Rgb.FromHex(0xFF5050), 15);

                enemy.Points.Clear();
                for (int s = 0; s < 3; s++)
                    enemy.Points.Add(new Vec2(newPos.X - s * segR * 2, newPos.Y));
            }
        }

        // ==================================================================
        //  结算（原版 GameOver）
        // ==================================================================

        /// <summary>对局结束。won=true 为胜利。</summary>
        public void GameOver(bool won)
        {
            if (Outcome != MatchOutcome.Running) return;
            Outcome = won ? MatchOutcome.Won : MatchOutcome.Lost;

            if (won)
            {
                WonGames++;
                SnakeProgress.RegisterWin();
            }
            else
            {
                Player.Alive = false;   // 失败=蛇死亡；先前未置位，测试发现死后 Alive 仍为 true
                SnakeProgress.RegisterLoss();
            }

            // 淘汰之王：结算时记录成绩（原版 SaveScore）
            if (Mode == SnakeMode.Timed && Player.Score > 0)
            {
                SnakeScores.Record(SnakeMode.Timed, Player.Score,
                    Player.Points.Count, Time, false);
            }

            IsMercyRound = false;
            IsPunishRound = false;
        }

        /// <summary>食物雨倒计时（极限模式 HUD 用）。</summary>
        public double FoodRainCountdown => _foodRainTimer;

        // ==================================================================
        //  只读查询（视图 / HUD / 测试）
        // ==================================================================

        public int Score => Player.Score;

        /// <summary>玩家体长（原版无此概念，蛇身点数即长度）。</summary>
        public int PlayerBodyCells => Player.Points.Count;

        public bool PlayerAlive => Player.Points.Count > 0;

        /// <summary>护盾剩余时间（HUD 用，原版 _shieldTimer）。</summary>
        public double ShieldRemaining => ShieldTimer;

        // ---- 计时赛转发（原版 TimedMode 状态）----
        public int PlayerLevel => Timed != null ? Timed.PlayerLevel : 1;
        public Vec2 PoisonCenter => Timed != null ? Timed.PoisonCenter : new Vec2(0, 0);
        public double PoisonRadius => Timed != null ? Timed.PoisonRadius : 0;
        public double PoisonShrinkTimer => Timed != null ? Timed.PoisonShrinkTimer : 0;
        public int ShrinkCount => Timed != null ? Timed.ShrinkCount : 0;
        public double OutsideTimer => Timed != null ? Timed.OutsideTimer : 0;
        public bool IsRacing => Timed != null && Timed.IsRacing;
        public bool RaceDisabled => Timed != null && Timed.RaceDisabled;
        public Vec2 RaceDestination => Timed != null ? Timed.RaceDestination : new Vec2(0, 0);
        public double RaceTimer => Timed != null ? Timed.RaceTimer : 0;
        public int RaceOpponentIndex => Timed != null ? Timed.RaceOpponentIndex : -1;
        public string RaceResultMessage => Timed != null ? Timed.RaceResultMessage : "";
        public double RaceResultTimer => Timed != null ? Timed.RaceResultTimer : 0;
        public int TimedAliveCount => Timed != null ? Timed.AliveCount : 0;
        public int EnemyLevel(int index) => Timed != null ? Timed.EnemyLevel(index) : 1;

        // ---- 撤离模式转发（原版 ExtractionMode 状态）----
        public Vec2 ExtractionPoint => Extraction != null ? Extraction.ExtractionPoint : new Vec2(0, 0);
        public double ExtractionRadius => Extraction != null ? Extraction.ExtractionRadius : 120;
        public double ExtractionTimer => Extraction != null ? Extraction.ExtractionTimer : 0;
        public bool ExtractionComplete => Extraction != null && Extraction.ExtractionComplete;
        public IReadOnlyList<SnakeContainer> Containers
            => Extraction != null ? Extraction.Containers : null;
        public bool IsPlayerSearching => Extraction != null && Extraction.IsPlayerSearching;
        public double SearchProgress => Extraction != null ? Extraction.SearchProgress : 0;
        public int SearchScore => Extraction != null ? Extraction.SearchScore : 0;
        public bool SearchReady => Extraction != null && Extraction.SearchComplete;
        public Vec2 SearchCirclePos => Extraction != null ? Extraction.SearchCirclePos : new Vec2(0, 0);
        public IReadOnlyList<SnakeFirebomb> Firebombs
            => Extraction != null ? Extraction.Firebombs : null;
        public double FireExposureTimer => Extraction != null ? Extraction.FireExposureTimer : 0;
        public ExtractionMapData CurrentMap => Extraction != null ? Extraction.SelectedMap : null;
        public double CurrentSearchTime => 0.02 * (Player.Score + 1);
        public int ContainersSearched
        {
            get
            {
                if (Extraction == null) return 0;
                int n = 0;
                for (int i = 0; i < Extraction.Containers.Count; i++)
                    if (Extraction.Containers[i].IsSearched) n++;
                return n;
            }
        }
        public int ContainersTotal => Extraction != null ? Extraction.Containers.Count : 0;

        public static double Dist(Vec2 a, Vec2 b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>描述状态（自检 / 调试）。</summary>
        public string DescribeState()
            => $"mode={Mode} outcome={Outcome} score={Score} len={Player.Points.Count} " +
               $"foods={Foods.Count} enemies={Enemies.Count} time={Time:0.0}s " +
               $"combo={ComboCount} mercy={IsMercyRound} punish={IsPunishRound}";
    }
}
