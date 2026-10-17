using System;
using System.Collections.Generic;
using ArknightsTD.Core;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Data;

namespace ArknightsTD.Gameplay
{
    /// <summary>关卡进程状态。</summary>
    public enum BattlePhase
    {
        /// <summary>进行中。</summary>
        Running,
        /// <summary>全部波次清完，胜利。</summary>
        Won,
        /// <summary>生命耗尽，失败。</summary>
        Lost,
    }

    /// <summary>场上敌人运行时状态（世界坐标，单位=格）。</summary>
    public sealed class EnemyUnit
    {
        public EnemyKind Kind;
        public int Hp;
        public int MaxHp;
        public double Speed;

        /// <summary>沿行进路径的进度 [0,1]；>=1 表示抵达终点。</summary>
        public double Progress;

        /// <summary>当前位置（世界坐标，单位=格）。</summary>
        public double Col;
        public double Row;

        /// <summary>上一帧位置，供表现层插值/朝向。</summary>
        public double PrevCol;
        public double PrevRow;

        /// <summary>当前行进方向（单位向量，网格坐标）。</summary>
        public double DirCol;
        public double DirRow;

        /// <summary>受击闪光剩余时间（秒），表现层使用。</summary>
        public double HitFlash;

        /// <summary>稳定唯一 id，表现层用来对应 GameObject。</summary>
        public int Id;

        public bool Dead;

        // ---- 明日方舟式战斗属性 ----
        /// <summary>对阻挡干员的攻击力。</summary>
        public int Atk;
        /// <summary>攻击间隔（秒）。</summary>
        public double AtkInterval;
        /// <summary>攻击冷却。</summary>
        public double AtkCooldown;
        /// <summary>当前阻挡它的干员 Id（-1 = 未被阻挡）。</summary>
        public int BlockedByOpId = -1;
    }

    /// <summary>场上干员（部署单位）运行时状态。</summary>
    public sealed class OperatorUnit
    {
        public OperatorKind Kind;
        public int Col;
        public int Row;
        public Facing Facing;

        /// <summary>攻击冷却剩余（秒），&lt;=0 表示可攻击。</summary>
        public double Cooldown;

        /// <summary>表现层使用的脉冲相位。</summary>
        public double PulsePhase;

        // ---- 明日方舟式战斗/技能属性 ----
        /// <summary>生命上限 / 当前生命。</summary>
        public int MaxHp;
        public int Hp;
        /// <summary>防御力（每次受击减免）。</summary>
        public int Def;
        /// <summary>阻挡数（0 = 不阻挡，如远程职业）。</summary>
        public int BlockCount;
        /// <summary>当前阻挡的敌人数量（每帧统计）。</summary>
        public int BlockedCount;

        /// <summary>是否存活（被敌人打死后置 false，随后从场上移除）。</summary>
        public bool Alive = true;
        /// <summary>受击闪光剩余（表现层）。</summary>
        public double HitFlash;

        /// <summary>技能技力：当前 / 上限。</summary>
        public double Sp;
        public double SpMax;
        /// <summary>技力回复方式与每次回复量。</summary>
        public SkillRecovery Recovery;
        public double SpPerTick;
        /// <summary>技能持续时间 / 剩余生效时间。</summary>
        public double SkillDuration;
        public double SkillActiveTimer;
        /// <summary>技能效果与强度。</summary>
        public SkillEffect SkillEffect;
        public double SkillMagnitude;

        /// <summary>技能是否已就绪（可释放）。</summary>
        public bool SkillReady => SpMax > 0 && Sp >= SpMax - 1e-6 && SkillActiveTimer <= 0;
        /// <summary>技能是否正在生效。</summary>
        public bool SkillActive => SkillActiveTimer > 0;
        /// <summary>技力进度 [0,1]（HUD 用）。</summary>
        public double SpProgress => SpMax > 0 ? Math.Max(0, Math.Min(1, Sp / SpMax)) : 0;

        /// <summary>
        /// 当前锁定的目标。开火后保持锁定直到目标死亡或离开射程。
        /// <para>
        /// 为什么需要锁定：若每发都重新选"当前最近"的敌人，多敌人同时进入射程时
        /// 每发都会打到不同的目标，伤害被永久分散，导致任何干员都打不死人（实测确认）。
        /// 锁定后单个干员的伤害才能累积成击杀。
        /// </para>
        /// </summary>
        public EnemyUnit Target;

        public int Id;
    }

    /// <summary>飞行中的弹丸。命中时结算伤害（延迟结算，与 WPF 版一致）。</summary>
    public sealed class Projectile
    {
        public double Col;
        public double Row;
        public EnemyUnit Target;
        public double Speed;
        public int Damage;
        public int Id;
    }

    /// <summary>
    /// 塔防战斗模拟 —— 纯逻辑，不含任何引擎依赖。
    /// <para>
    /// 移植自 WPF 版 TowerDefenseMode 的更新循环，但把渲染/输入彻底剥离：
    /// <list type="bullet">
    /// <item>固定时间步推进，保证不同帧率下结果一致（<see cref="Advance"/&gt;</item>
    /// <item>随机数由外部注入种子，使战局可复现、可测试</item>
    /// <item>伤害在弹丸命中时结算，击杀按兵种回费（Boss 6 / 重装 3 / 重盾 2 / 其他 1）</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class BattleSim
    {
        /// <summary>固定逻辑步长（秒）。60Hz。</summary>
        public const double FixedStep = 1.0 / 60.0;

        /// <summary>单次 Advance 最多执行的逻辑步数（失控保护，约 20 秒游戏时间）。
        /// 早期版本误设为 8，导致 Advance(5.0) 只推进 8/60 秒就丢弃剩余时间，模拟几乎不前进。</summary>
        public const int MaxStepsPerAdvance = 1200;

        /// <summary>弹丸速度（格/秒）。WPF 版为 14×cellScale 像素/秒，此处按格换算。</summary>
        public const double ProjectileSpeed = 14.0;

        /// <summary>受击闪光持续时间（秒）。</summary>
        public const double HitFlashDuration = 0.12;

        // ---------------- 静态配置 ----------------
        public TDLevelData Level { get; private set; }

        /// <summary>网格类型（与 WPF 版一致的 Deployable / Path / Obstacle）。</summary>
        public enum CellType { Deployable, Path, Obstacle }

        public CellType[,] Grid { get; private set; }
        public int GridW { get; private set; }
        public int GridH { get; private set; }

        // ---------------- 动态状态 ----------------
        public BattlePhase Phase { get; private set; } = BattlePhase.Running;

        public int Lives { get; private set; }
        public int MaxLives { get; private set; }
        public int Cost { get; private set; }
        public int Score { get; private set; }

        public int CurrentWave { get; private set; }
        public double NextWaveTimer { get; private set; }

        public readonly List<EnemyUnit> Enemies = new List<EnemyUnit>();
        public readonly List<OperatorUnit> Operators = new List<OperatorUnit>();
        public readonly List<Projectile> Projectiles = new List<Projectile>();

        /// <summary>累计游戏时间（秒），表现层用于动画相位。</summary>
        public double Time { get; private set; }

        /// <summary>本次推进中被击杀的敌人数量（供表现层播特效）。</summary>
        public int LastFrameKills { get; private set; }
        /// <summary>本次推进中抵达终点（扣血）的敌人数量。</summary>
        public int LastFrameLeaks { get; private set; }

        // ---------------- 内部 ----------------
        private readonly List<double> _pathX = new List<double>();
        private readonly List<double> _pathY = new List<double>();
        private readonly List<double> _segLen = new List<double>();
        private double _pathTotalLen;
        private double _costTimer;
        private int _enemiesRemaining;
        private double _spawnTimer;
        private double _spawnInterval;
        private double _stepAccumulator;
        private int _nextId = 1;
        private Random _rand;

        /// <summary>关卡是否已通过（用于解锁进度）。</summary>
        public bool LevelCleared => Phase == BattlePhase.Won;

        /// <summary>
        /// 准备阶段（明日方舟"部署阶段"）：此时不生成敌人、显示敌人动线，
        /// 玩家可先部署干员并观察路线，按"开始行动"后进入战斗。
        /// </summary>
        public bool PrepPhase { get; private set; }

        // ==================================================================
        //  初始化
        // ==================================================================

        public void Initialize(TDLevelData level, int randomSeed = 12345, bool prepPhase = false)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            Level = level;
            GridW = Math.Max(1, level.GridW);
            GridH = Math.Max(1, level.GridH);

            _rand = new Random(randomSeed);
            _nextId = 1;

            BuildGrid();
            BuildPath();

            MaxLives = Math.Max(1, level.StartingLives);
            Lives = MaxLives;
            Cost = Math.Max(0, level.StartingCost);
            Score = 0;

            CurrentWave = 0;
            NextWaveTimer = TDBalance.FirstWaveDelay;
            _enemiesRemaining = 0;
            _spawnTimer = 0;
            _spawnInterval = 1.0;
            _costTimer = 0;
            _stepAccumulator = 0;
            Time = 0;
            LastFrameKills = 0;
            LastFrameLeaks = 0;
            Phase = BattlePhase.Running;
            PrepPhase = prepPhase;

            Enemies.Clear();
            Operators.Clear();
            Projectiles.Clear();

            // 让进度解锁逻辑知道关卡总数（PlayerProgress 不认识关卡表）
            PlayerProgress.LevelCount = TDLevels.All.Count;
        }

        private void BuildGrid()
        {
            Grid = new CellType[GridW, GridH];
            for (int c = 0; c < GridW; c++)
                for (int r = 0; r < GridH; r++)
                    Grid[c, r] = CellType.Deployable;

            if (Level.Obstacles != null)
                foreach (var ob in Level.Obstacles)
                {
                    if (ob == null || ob.Length < 2) continue;
                    int c = ob[0], r = ob[1];
                    if (c >= 0 && c < GridW && r >= 0 && r < GridH)
                        Grid[c, r] = CellType.Obstacle;
                }

            // 路径格覆盖：把折线经过的格标记为 Path。
            // 关卡数据里的路径坐标是【格索引】（例如 {-1,3} 表示第 3 行），
            // 因此这里直接按格索引标记，不做 +0.5 偏移（世界坐标偏移见 BuildPath）。
            if (Level.Path != null)
                for (int i = 0; i + 1 < Level.Path.Length; i++)
                {
                    var a = Level.Path[i];
                    var b = Level.Path[i + 1];
                    if (a == null || b == null || a.Length < 2 || b.Length < 2) continue;

                    if (Math.Abs(a[1] - b[1]) < 1e-9)
                    {
                        int row = (int)Math.Floor(a[1] + 1e-9);
                        int c0 = (int)Math.Floor(Math.Min(a[0], b[0]) + 1e-9);
                        int c1 = (int)Math.Floor(Math.Max(a[0], b[0]) + 1e-9);
                        for (int c = c0; c <= c1; c++)
                            if (c >= 0 && c < GridW && row >= 0 && row < GridH)
                                Grid[c, row] = CellType.Path;
                    }
                    else if (Math.Abs(a[0] - b[0]) < 1e-9)
                    {
                        int col = (int)Math.Floor(a[0] + 1e-9);
                        int r0 = (int)Math.Floor(Math.Min(a[1], b[1]) + 1e-9);
                        int r1 = (int)Math.Floor(Math.Max(a[1], b[1]) + 1e-9);
                        for (int r = r0; r <= r1; r++)
                            if (col >= 0 && col < GridW && r >= 0 && r < GridH)
                                Grid[col, r] = CellType.Path;
                    }
                }
        }

        private void BuildPath()
        {
            _pathX.Clear(); _pathY.Clear(); _segLen.Clear();
            _pathTotalLen = 0;

            if (Level.Path != null)
                foreach (var p in Level.Path)
                {
                    if (p == null || p.Length < 2) continue;
                    // ★ 格索引 → 格中心世界坐标：+0.5。
                    //   这一步是关键：射程判定（WaveDirector.InRange）以格中心为基准
                    //   （格 (c,r) 中心 = c+0.5, r+0.5），若路径直接用格索引当世界坐标，
                    //   敌人就会走在格边界上，任何干员都打不到它。
                    double wx = p[0] + 0.5;
                    double wy = p[1] + 0.5;

                    // 去掉连续重复点，避免零长段造成方向计算出 NaN
                    if (_pathX.Count > 0)
                    {
                        double lx = _pathX[_pathX.Count - 1], ly = _pathY[_pathY.Count - 1];
                        if (Math.Abs(lx - wx) < 1e-12 && Math.Abs(ly - wy) < 1e-12) continue;
                    }
                    _pathX.Add(wx);
                    _pathY.Add(wy);
                }

            for (int i = 0; i + 1 < _pathX.Count; i++)
            {
                double dx = _pathX[i + 1] - _pathX[i];
                double dy = _pathY[i + 1] - _pathY[i];
                double len = Math.Sqrt(dx * dx + dy * dy);
                _segLen.Add(len);
                _pathTotalLen += len;
            }
        }

        /// <summary>路径总长度（格）。</summary>
        public double PathLength => _pathTotalLen;

        /// <summary>路径折线点数。</summary>
        public int PathPointCount => _pathX.Count;

        /// <summary>取路径上第 i 个点（供表现层铺路）。</summary>
        public bool TryGetPathPoint(int i, out double x, out double y)
        {
            x = y = 0;
            if (i < 0 || i >= _pathX.Count) return false;
            x = _pathX[i];
            y = _pathY[i];
            return true;
        }

        /// <summary>按进度取路径上的世界位置（进度会被钳制到 [0,1]）。</summary>
        public void PositionAt(double progress, out double x, out double y)
        {
            x = y = 0;
            if (_pathX.Count == 0) return;
            if (_pathX.Count == 1) { x = _pathX[0]; y = _pathY[0]; return; }

            if (progress <= 0) { x = _pathX[0]; y = _pathY[0]; return; }
            if (progress >= 1) { x = _pathX[_pathX.Count - 1]; y = _pathY[_pathY.Count - 1]; return; }

            double target = progress * _pathTotalLen;
            double acc = 0;
            for (int i = 0; i < _segLen.Count; i++)
            {
                double seg = _segLen[i];
                if (acc + seg >= target)
                {
                    double t = seg < 1e-9 ? 0 : (target - acc) / seg;
                    x = _pathX[i] + (_pathX[i + 1] - _pathX[i]) * t;
                    y = _pathY[i] + (_pathY[i + 1] - _pathY[i]) * t;
                    return;
                }
                acc += seg;
            }
            x = _pathX[_pathX.Count - 1];
            y = _pathY[_pathY.Count - 1];
        }

        /// <summary>按进度取路径方向（单位向量）。</summary>
        public void DirectionAt(double progress, out double dx, out double dy)
        {
            dx = 1; dy = 0;
            if (_pathX.Count < 2) return;

            if (progress < 0) progress = 0;
            if (progress > 1) progress = 1;

            double target = progress * _pathTotalLen;
            double acc = 0;
            for (int i = 0; i < _segLen.Count; i++)
            {
                double seg = _segLen[i];
                if (acc + seg >= target)
                {
                    double ax = _pathX[i + 1] - _pathX[i];
                    double ay = _pathY[i + 1] - _pathY[i];
                    double len = Math.Sqrt(ax * ax + ay * ay);
                    if (len < 1e-9) { dx = 1; dy = 0; return; }
                    dx = ax / len; dy = ay / len;
                    return;
                }
                acc += seg;
            }
        }

        // ==================================================================
        //  推进
        // ==================================================================

        /// <summary>
        /// 按固定步长推进模拟。<paramref name="dt"/> 为真实经过时间，
        /// <paramref name="speedMultiplier"/> 为倍速（1/2/4 倍，与 UI 的加速按钮对应）。
        /// 未消耗的余量会留到下次，保证长时间运行不漂移。
        /// </summary>
        public void Advance(double dt, double speedMultiplier = 1.0)
        {
            if (Phase != BattlePhase.Running) return;
            if (dt <= 0) return;
            if (double.IsNaN(speedMultiplier) || speedMultiplier <= 0) speedMultiplier = 1.0;

            LastFrameKills = 0;
            LastFrameLeaks = 0;

            _stepAccumulator += dt * speedMultiplier;

            // 用 1e-9 的容差判断是否够一步：60 次累加 1/60 会得到 0.9999999999999999，
            // 严格比较会少算最后一步（表现为"推进 1 秒实际只走了 59 帧"）。
            const double Eps = 1e-9;

            int steps = 0;
            while (_stepAccumulator >= FixedStep - Eps && steps < MaxStepsPerAdvance)
            {
                Step(FixedStep);
                _stepAccumulator -= FixedStep;
                if (_stepAccumulator < Eps) _stepAccumulator = 0;
                steps++;
                if (Phase != BattlePhase.Running) break;
            }

            // 仅当远超保护上限时才丢弃积压（正常大 dt 会被完整消费，不会走到这里）
            if (_stepAccumulator > FixedStep * MaxStepsPerAdvance)
                _stepAccumulator = 0;
        }

        private void Step(double dt)
        {
            Time += dt;

            UpdateCostRegen(dt);
            UpdateWaves(dt);
            UpdateEnemies(dt);
            UpdateOperators(dt);
            UpdateProjectiles(dt);
            CheckEnd();
        }

        private void UpdateCostRegen(double dt)
        {
            _costTimer += dt;
            while (_costTimer >= TDBalance.CostRegenSeconds)
            {
                _costTimer -= TDBalance.CostRegenSeconds;
                Cost++;
            }
        }

        /// <summary>开始行动：结束准备阶段，开始波次计时（明日方舟「开始行动」按钮）。</summary>
        public bool StartBattle()
        {
            if (!PrepPhase) return false;
            PrepPhase = false;
            NextWaveTimer = TDBalance.FirstWaveDelay;
            return true;
        }

        private void UpdateWaves(double dt)
        {
            if (PrepPhase) return;   // 准备阶段不出兵

            int totalWaves = Math.Max(1, Level.TotalWaves);

            // ★ 顺序很重要：先处理"本波还有待放敌人"，再判断整体是否结束。
            //   早期版本把"CurrentWave >= totalWaves"放在最前面，导致 CurrentWave 刚自增到
            //   totalWaves（第一波还没来得及放兵）时就判定胜利，整局瞬间结束。
            //   现在用 _enemiesRemaining 区分「本波进行中」与「全部波次已放完」。
            if (_enemiesRemaining > 0)
            {
                _spawnTimer -= dt;
                if (_spawnTimer <= 0)
                {
                    _spawnTimer = _spawnInterval;
                    SpawnEnemy();
                    _enemiesRemaining--;
                    if (_enemiesRemaining <= 0) NextWaveTimer = TDBalance.BetweenWaveDelay;
                }
                return;
            }

            // 全部波次已放完：等场上清空即胜利
            if (CurrentWave >= totalWaves)
            {
                if (Enemies.Count == 0) Phase = BattlePhase.Won;
                return;
            }

            // 等待下一波开始
            NextWaveTimer -= dt;
            if (NextWaveTimer <= 0)
            {
                CurrentWave++;
                _enemiesRemaining = WaveDirector.EnemiesInWave(CurrentWave, Level.Index);
                _spawnTimer = 0;   // 下一 tick 立即放第一个兵
                _spawnInterval = WaveDirector.SpawnInterval(CurrentWave, Level.Index);
            }
        }

        private void SpawnEnemy()
        {
            var kind = WaveDirector.PickKind(Level.Index, CurrentWave, _rand.NextDouble());
            var def = TDBalance.Enemy(kind);

            var e = new EnemyUnit
            {
                Kind = kind,
                MaxHp = WaveDirector.EnemyHp(kind, CurrentWave, Level.Index),
                Speed = WaveDirector.EnemySpeed(kind, CurrentWave, Level.Index),
                Progress = 0,
                HitFlash = 0,
                Id = _nextId++,
                Dead = false,
                Atk = def.Atk,
                AtkInterval = def.AtkInterval,
                AtkCooldown = 0,
                BlockedByOpId = -1,
            };
            e.Hp = e.MaxHp;

            PositionAt(0, out double x, out double y);
            e.Col = e.PrevCol = x;
            e.Row = e.PrevRow = y;
            DirectionAt(0, out double dx, out double dy);
            e.DirCol = dx; e.DirRow = dy;

            Enemies.Add(e);
        }

        private void UpdateEnemies(double dt)
        {
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var e = Enemies[i];

                if (e.Dead)
                {
                    Enemies.RemoveAt(i);
                    continue;
                }

                e.PrevCol = e.Col;
                e.PrevRow = e.Row;

                // ---- 阻挡判定（明日方舟：近战干员挡住敌人，被挡的敌人停下来打干员）----
                var blocker = FindBlocker(e);
                if (blocker != null)
                {
                    e.BlockedByOpId = blocker.Id;
                    e.AtkCooldown -= dt;
                    if (e.AtkCooldown <= 0)
                    {
                        e.AtkCooldown = e.AtkInterval > 0 ? e.AtkInterval : 1.0;
                        int dmg = e.Atk - blocker.Def;
                        if (dmg < 1) dmg = 1;
                        blocker.Hp -= dmg;
                        blocker.HitFlash = HitFlashDuration;
                    }
                    // 被阻挡：不推进进度，只刷新受击表现
                    if (e.HitFlash > 0) { e.HitFlash -= dt; if (e.HitFlash < 0) e.HitFlash = 0; }
                    continue;
                }

                e.BlockedByOpId = -1;

                // WPF 版：progressDelta = speed * dt / max(pathTotalLen, 1)
                if (_pathTotalLen > 1e-9)
                    e.Progress += e.Speed * dt / _pathTotalLen;

                PositionAt(e.Progress, out double x, out double y);
                e.Col = x;
                e.Row = y;
                DirectionAt(e.Progress, out double dx, out double dy);
                e.DirCol = dx; e.DirRow = dy;

                if (e.HitFlash > 0)
                {
                    e.HitFlash -= dt;
                    if (e.HitFlash < 0) e.HitFlash = 0;
                }

                // 抵达终点：扣命并移除
                if (e.Progress >= 1.0)
                {
                    Lives--;
                    LastFrameLeaks++;
                    Enemies.RemoveAt(i);
                }
            }
        }

        /// <summary>查找能阻挡该敌人的干员（近战、阻挡数未满、距离够近）。</summary>
        private OperatorUnit FindBlocker(EnemyUnit e)
        {
            const double blockRange = 0.78;   // 约 3/4 格
            OperatorUnit best = null;
            double bestSq = blockRange * blockRange;

            for (int i = 0; i < Operators.Count; i++)
            {
                var op = Operators[i];
                if (!op.Alive || op.BlockCount <= 0) continue;

                // 已经在挡这个敌人的，优先继续挡（避免抖动）
                double ox = op.Col + 0.5, oy = op.Row + 0.5;
                double dx = e.Col - ox, dy = e.Row - oy;
                double sq = dx * dx + dy * dy;
                if (sq > blockRange * blockRange) continue;

                if (e.BlockedByOpId == op.Id) return op;

                int used = 0;
                for (int k = 0; k < Enemies.Count; k++)
                    if (Enemies[k].BlockedByOpId == op.Id && !Enemies[k].Dead) used++;
                if (used >= op.BlockCount) continue;

                if (sq < bestSq) { bestSq = sq; best = op; }
            }
            return best;
        }

        /// <summary>统计每个干员当前阻挡的敌人数（HUD/表现用）。</summary>
        private void RefreshBlockedCounts()
        {
            for (int i = 0; i < Operators.Count; i++) Operators[i].BlockedCount = 0;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Dead || e.BlockedByOpId < 0) continue;
                for (int k = 0; k < Operators.Count; k++)
                    if (Operators[k].Id == e.BlockedByOpId) { Operators[k].BlockedCount++; break; }
            }
        }

        /// <summary>统一伤害结算（弹丸命中 / 范围技能都用它，保证击杀统计一致）。</summary>
        private void DamageEnemy(EnemyUnit t, int dmg)
        {
            if (t == null || t.Dead) return;
            t.Hp -= dmg;
            t.HitFlash = HitFlashDuration;
            if (t.Hp <= 0 && !t.Dead)
            {
                t.Dead = true;
                Cost += KillReward(t.Kind);
                Score += 1;
                LastFrameKills++;
            }
        }

        /// <summary>干员阵亡清理（Hp<=0）。</summary>
        private void RemoveDeadOperators()
        {
            for (int i = Operators.Count - 1; i >= 0; i--)
            {
                var op = Operators[i];
                if (op.Hp > 0) { op.Alive = true; continue; }
                op.Alive = false;
                Operators.RemoveAt(i);
                LastFrameOperatorDeaths++;
            }
        }

        /// <summary>本帧阵亡干员数（表现层/HUD 用）。</summary>
        public int LastFrameOperatorDeaths { get; private set; }

        private void UpdateOperators(double dt)
        {
            LastFrameOperatorDeaths = 0;
            RefreshBlockedCounts();
            RemoveDeadOperators();

            if (Enemies.Count == 0)
            {
                // 没敌人时仍然推进冷却与脉冲，但不发射
                for (int i = 0; i < Operators.Count; i++)
                {
                    var op = Operators[i];
                    if (op.Cooldown > 0) op.Cooldown -= dt;
                    op.PulsePhase += dt * 3;
                }
                return;
            }

            for (int i = 0; i < Operators.Count; i++)
            {
                var op = Operators[i];
                if (op.HitFlash > 0) { op.HitFlash -= dt; if (op.HitFlash < 0) op.HitFlash = 0; }

                // ---- 技能计时与技力 ----
                if (op.SkillActiveTimer > 0)
                {
                    op.SkillActiveTimer -= dt;
                    if (op.SkillActiveTimer < 0) op.SkillActiveTimer = 0;
                }
                else if (op.SpMax > 0 && op.Recovery == SkillRecovery.Auto)
                {
                    op.Sp = Math.Min(op.SpMax, op.Sp + op.SpPerTick * dt);
                }

                if (op.Cooldown > 0) op.Cooldown -= dt;
                op.PulsePhase += dt * 3;
                if (op.Cooldown > 0) continue;

                // 目标选择：优先沿用已锁定的目标，锁定失效时才重新选"射程内最近"。
                // 这样伤害才能在单个目标上累积成击杀（否则每发都换目标，永远打不死人）。
                EnemyUnit target = op.Target;
                if (!IsTargetValid(op, target))
                {
                    target = FindNearestInRange(op);
                    op.Target = target;
                }

                if (target == null) continue;

                double ox = op.Col + 0.5, oy = op.Row + 0.5;
                var def = TDBalance.Operator(op.Kind);

                // 技能加成
                double dmgMul = 1.0, intervalMul = 1.0;
                if (op.SkillActive)
                {
                    if (op.SkillEffect == SkillEffect.AtkBoost) dmgMul += op.SkillMagnitude;
                    else if (op.SkillEffect == SkillEffect.FastAttack) intervalMul -= op.SkillMagnitude;
                }
                int dmg = (int)Math.Round(def.Damage * dmgMul);
                if (dmg < 1) dmg = 1;

                // 范围爆发：直接对射程内全部敌人结算
                if (op.SkillActive && op.SkillEffect == SkillEffect.AoE)
                {
                    for (int k = 0; k < Enemies.Count; k++)
                    {
                        var e = Enemies[k];
                        if (e.Dead) continue;
                        if (!WaveDirector.InRange(op.Kind, op.Facing, op.Col, op.Row, e.Col, e.Row))
                            continue;
                        DamageEnemy(e, dmg);
                    }
                }
                else
                {
                    Projectiles.Add(new Projectile
                    {
                        Col = ox,
                        Row = oy,
                        Target = target,
                        Speed = ProjectileSpeed,
                        Damage = dmg,
                        Id = _nextId++,
                    });
                }

                double interval = def.Interval * Math.Max(0.2, intervalMul);
                op.Cooldown = interval;

                // 攻击回复技力
                if (op.SpMax > 0 && op.Recovery == SkillRecovery.OnAttack && !op.SkillActive)
                    op.Sp = Math.Min(op.SpMax, op.Sp + op.SpPerTick);
            }
        }

        /// <summary>目标是否仍然有效（存活、且在射程内）。</summary>
        private static bool IsTargetValid(OperatorUnit op, EnemyUnit target)
        {
            if (target == null || target.Dead) return false;
            if (target.Progress >= 1.0) return false;
            return WaveDirector.InRange(op.Kind, op.Facing, op.Col, op.Row, target.Col, target.Row);
        }

        /// <summary>在射程内挑选离干员最近的敌人（按格距）。</summary>
        private EnemyUnit FindNearestInRange(OperatorUnit op)
        {
            EnemyUnit best = null;
            double bestSq = double.MaxValue;
            double ox = op.Col + 0.5, oy = op.Row + 0.5;

            for (int k = 0; k < Enemies.Count; k++)
            {
                var e = Enemies[k];
                if (e.Dead) continue;
                if (e.Progress >= 1.0) continue;
                if (!WaveDirector.InRange(op.Kind, op.Facing, op.Col, op.Row, e.Col, e.Row)) continue;

                double ddx = e.Col - ox, ddy = e.Row - oy;
                double sq = ddx * ddx + ddy * ddy;
                if (sq < bestSq) { bestSq = sq; best = e; }
            }
            return best;
        }

        private void UpdateProjectiles(double dt)
        {
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var p = Projectiles[i];

                // 目标已消失即作废。
                // 只判 Dead 即可：击杀会立刻置 Dead，抵达终点则直接从列表移除；
                // 两种情况下已移除的对象都不会再被标记为存活。
                // （早先版本每颗弹丸都做 Enemies.IndexOf，是 O(n²)，且目标泄漏后会永远追击。）
                if (p.Target == null || p.Target.Dead)
                {
                    Projectiles.RemoveAt(i);
                    continue;
                }

                double dx = p.Target.Col - p.Col;
                double dy = p.Target.Row - p.Row;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                double step = p.Speed * dt;

                if (dist <= step || dist < 1e-9)
                {
                    // 命中结算（统一走 DamageEnemy）
                    DamageEnemy(p.Target, p.Damage);
                    Projectiles.RemoveAt(i);
                }
                else
                {
                    p.Col += dx / dist * step;
                    p.Row += dy / dist * step;
                }
            }
        }

        /// <summary>击杀回费：Boss 6 / 重装 3 / 重盾 2 / 其他 1（与 WPF 版一致）。</summary>
        public static int KillReward(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Boss: return 6;
                case EnemyKind.Heavy: return 3;
                case EnemyKind.Shield: return 2;
                default: return 1;
            }
        }

        private void CheckEnd()
        {
            if (Phase != BattlePhase.Running) return;
            if (Lives <= 0)
            {
                Lives = 0;
                Phase = BattlePhase.Lost;
            }
        }

        // ==================================================================
        //  玩家操作
        // ==================================================================

        /// <summary>测试专用：直接设定费用，避免测试依赖自然回费节奏。</summary>
        public void CostOverrideForTest(int value) => Cost = value < 0 ? 0 : value;

        /// <summary>
        /// 部署干员。失败原因通过 <paramref name="reason"/> 返回，便于 UI 显示。
        /// 术师不需要选朝向（固定为 Down，与 WPF 版一致）。
        /// </summary>
        public bool TryDeployOperator(OperatorKind kind, int col, int row, Facing facing,
            out string reason)
        {
            reason = null;

            if (Phase != BattlePhase.Running) { reason = "对战已结束"; return false; }
            if (col < 0 || col >= GridW || row < 0 || row >= GridH) { reason = "超出战场范围"; return false; }
            if (Grid[col, row] != CellType.Deployable) { reason = "该格不可部署"; return false; }

            for (int i = 0; i < Operators.Count; i++)
                if (Operators[i].Col == col && Operators[i].Row == row) { reason = "该格已有干员"; return false; }

            var def = TDBalance.Operator(kind);
            if (Cost < def.Cost) { reason = "费用不足"; return false; }

            Cost -= def.Cost;
            var skill = def.Skill;
            Operators.Add(new OperatorUnit
            {
                Kind = kind,
                Col = col,
                Row = row,
                Facing = kind == OperatorKind.Caster ? Facing.Down : facing,
                Cooldown = 0,
                PulsePhase = 0,
                Id = _nextId++,

                // 明日方舟式战斗属性
                MaxHp = def.MaxHp,
                Hp = def.MaxHp,
                Def = def.Def,
                BlockCount = def.BlockCount,
                Alive = true,
                HitFlash = 0,

                // 技能
                Sp = 0,
                SpMax = skill != null ? skill.SpMax : 0,
                Recovery = skill != null ? skill.Recovery : SkillRecovery.Auto,
                SpPerTick = skill != null ? skill.SpPerTick : 0,
                SkillDuration = skill != null ? skill.Duration : 0,
                SkillActiveTimer = 0,
                SkillEffect = skill != null ? skill.Effect : SkillEffect.AtkBoost,
                SkillMagnitude = skill != null ? skill.Magnitude : 0,
            });
            return true;
        }

        /// <summary>释放指定干员的技能（技力满且未在生效中）。返回是否成功。</summary>
        public bool ActivateSkill(int operatorId)
        {
            for (int i = 0; i < Operators.Count; i++)
            {
                var op = Operators[i];
                if (op.Id != operatorId) continue;
                if (!op.SkillReady) return false;
                op.Sp = 0;
                op.SkillActiveTimer = op.SkillDuration;
                return true;
            }
            return false;
        }

        /// <summary>按格释放技能（HUD/键盘用）。</summary>
        public bool ActivateSkillAt(int col, int row)
        {
            var op = OperatorAt(col, row);
            return op != null && ActivateSkill(op.Id);
        }

        /// <summary>按 id 取干员（HUD 用）。</summary>
        public OperatorUnit OperatorById(int id)
        {
            for (int i = 0; i < Operators.Count; i++)
                if (Operators[i].Id == id) return Operators[i];
            return null;
        }

        /// <summary>撤下干员，返还一半费用（与 WPF 版一致）。</summary>
        public bool TryRemoveOperatorAt(int col, int row)
        {
            for (int i = 0; i < Operators.Count; i++)
            {
                var op = Operators[i];
                if (op.Col != col || op.Row != row) continue;

                var def = TDBalance.Operator(op.Kind);
                Cost += def.Cost / 2;
                Operators.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>取指定格上的干员（没有则返回 null）。</summary>
        public OperatorUnit OperatorAt(int col, int row)
        {
            for (int i = 0; i < Operators.Count; i++)
            {
                var op = Operators[i];
                if (op.Col == col && op.Row == row) return op;
            }
            return null;
        }

        /// <summary>某格是否可部署（空格、无干员、且对战进行中）。</summary>
        public bool CanDeployAt(int col, int row)
        {
            if (Phase != BattlePhase.Running) return false;
            if (col < 0 || col >= GridW || row < 0 || row >= GridH) return false;
            if (Grid[col, row] != CellType.Deployable) return false;
            return OperatorAt(col, row) == null;
        }

        /// <summary>可部署格数量（自检/UI 用）。</summary>
        public int CountDeployableCells()
        {
            int n = 0;
            for (int c = 0; c < GridW; c++)
                for (int r = 0; r < GridH; r++)
                    if (Grid[c, r] == CellType.Deployable) n++;
            return n;
        }

        /// <summary>指定类型格的坐标列表（自检用）。</summary>
        public List<(int col, int row)> CellsOfType(CellType type)
        {
            var list = new List<(int, int)>();
            for (int c = 0; c < GridW; c++)
                for (int r = 0; r < GridH; r++)
                    if (Grid[c, r] == type) list.Add((c, r));
            return list;
        }

        /// <summary>
        /// 结算并解锁进度。只在胜利时生效；返回 true 表示解锁了新关卡。
        /// </summary>
        public bool CommitProgress()
        {
            if (Phase != BattlePhase.Won) return false;
            int before = PlayerProgress.MaxUnlocked;
            PlayerProgress.MarkComplete(Level.Index);
            return PlayerProgress.MaxUnlocked > before;
        }
    }
}
