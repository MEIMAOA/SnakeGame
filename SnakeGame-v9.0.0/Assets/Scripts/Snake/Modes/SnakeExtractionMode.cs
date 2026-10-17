using System;
using System.Collections.Generic;
using ArknightsTD.Core.Geometry;

namespace ArknightsTD.Snake
{
    /// <summary>撤离模式容器。</summary>
    public sealed class SnakeContainer
    {
        public Vec2 Position;
        public bool IsSearched;
    }

    /// <summary>撤离模式地图定义（原版 MapData）。</summary>
    public sealed class ExtractionMapData
    {
        public string Name;
        public double WorldWidth;
        public double WorldHeight;
        public Vec2 ExtractionPoint;
        public List<Vec2> ContainerPositions;
        public int EnemyCount;
        public double EnemySpeedMultiplier = 1;
        public int EnemyLives = 1;
        public bool HasFirebombs;
    }

    /// <summary>火炸弹（原版 Firebomb）。</summary>
    public sealed class SnakeFirebomb
    {
        public Vec2 Position;
        public double Radius;
        public double Duration;
        public double Elapsed;
    }

    /// <summary>
    /// 搜打撤模式。忠实移植 WPF 版 ExtractionMode.cs（472 行）：
    /// 三张地图（废弃工厂 / 边境森林 / 航天基地），容器 30/99/999，
    /// 敌人 5/10/20，速度 1.0/1.5/5.0，生命 1/1/3，仅航天基地有火炸弹；
    /// 搜索：靠近容器按 F 开始读条（时长 0.02×(分数+1) 秒，实时重算），
    /// 读满后再按 F 领取分数；搜索期间玩家蛇冻结（宿主处理）；
    /// 撤离：在撤离点半径 120px 内停留 9 秒即胜利（离开清零）；
    /// 火炸弹：敌蛇每帧 2% 概率向玩家附近投掷（半径 100、持续 10s），
    /// 站火中累计 5s 判负；
    /// AI：经典加强版——触发 600px、攻击阈值 2.8/3.4/2.0、逃跑混合 0.7，
    /// 距离外追玩家，但食物距离² &lt; 玩家距离²×0.25 时先吃食物。
    /// </summary>
    public sealed class SnakeExtractionMode : SnakeGameModeBase
    {
        public override string Name => "extraction";
        public override int FoodCount => 40;
        public override int EnemyCount => SelectedMap != null ? SelectedMap.EnemyCount : 0;
        public override double AiDifficulty => 2.5;

        // ==================== 撤离 ====================

        public Vec2 ExtractionPoint { get; set; }
        public double ExtractionRadius { get; set; } = 120;
        public double ExtractionTimer { get; set; } = 0;
        public const double RequiredExtractionTime = 9.0;
        public bool ExtractionComplete { get; set; } = false;

        // ==================== 搜索 ====================

        public List<SnakeContainer> Containers { get; set; } = new List<SnakeContainer>();
        public SnakeContainer CurrentNearContainer { get; set; } = null;
        public bool IsPlayerSearching { get; set; } = false;
        public double SearchProgress { get; set; } = 0;
        public int SearchScore { get; set; } = 0;
        public bool SearchComplete { get; set; } = false;
        public Vec2 SearchCirclePos { get; set; }

        // ==================== 火炸弹 ====================

        public List<SnakeFirebomb> Firebombs { get; set; } = new List<SnakeFirebomb>();
        public double FireExposureTimer { get; set; } = 0;
        private readonly Random _rand = new Random();

        // ==================== 地图表（原版 Maps，进程级生成一次）====================

        public static readonly ExtractionMapData[] Maps =
        {
            new ExtractionMapData
            {
                Name = "废弃工厂",
                WorldWidth = 4000, WorldHeight = 3500,
                ExtractionPoint = new Vec2(800, 600),
                ContainerPositions = GenerateRandomPositions(30, 4000, 3500),
                EnemyCount = 5,
                EnemySpeedMultiplier = 1.0,
                EnemyLives = 1,
                HasFirebombs = false
            },
            new ExtractionMapData
            {
                Name = "边境森林",
                WorldWidth = 5000, WorldHeight = 4000,
                ExtractionPoint = new Vec2(1500, -1000),
                ContainerPositions = GenerateRandomPositions(99, 5000, 4000),
                EnemyCount = 10,
                EnemySpeedMultiplier = 1.5,
                EnemyLives = 1,
                HasFirebombs = false
            },
            new ExtractionMapData
            {
                Name = "航天基地",
                WorldWidth = 8000, WorldHeight = 6000,
                ExtractionPoint = new Vec2(3000, -2000),
                ContainerPositions = GenerateRandomPositions(999, 8000, 6000),
                EnemyCount = 20,
                EnemySpeedMultiplier = 5.0,
                EnemyLives = 3,
                HasFirebombs = true
            }
        };

        public ExtractionMapData SelectedMap { get; set; }

        public SnakeExtractionMode()
        {
            SelectedMap = Maps[0];
        }

        private static List<Vec2> GenerateRandomPositions(int count, double worldW, double worldH)
        {
            var list = new List<Vec2>();
            var rng = new Random();
            for (int i = 0; i < count; i++)
            {
                list.Add(new Vec2(
                    rng.NextDouble() * worldW - worldW / 2,
                    rng.NextDouble() * worldH - worldH / 2));
            }
            return list;
        }

        /// <summary>选择地图（开赛前选图）。</summary>
        public void SelectMap(int index)
        {
            if (index < 0 || index >= Maps.Length) index = 0;
            SelectedMap = Maps[index];
        }

        public override void Initialize()
        {
            var w = World;
            if (w == null) return;
            w.SetWorldSizeInternal(SelectedMap.WorldWidth, SelectedMap.WorldHeight);
            ExtractionPoint = SelectedMap.ExtractionPoint;
            ExtractionTimer = 0;
            ExtractionComplete = false;
            Firebombs.Clear();
            FireExposureTimer = 0;
            IsPlayerSearching = false;
            SearchProgress = 0;
            SearchComplete = false;
            _aiTime = 0;

            Containers.Clear();
            var positions = SelectedMap.ContainerPositions;
            for (int i = 0; i < positions.Count; i++)
                Containers.Add(new SnakeContainer { Position = positions[i], IsSearched = false });

            w.SpawnExtractionEnemies(SelectedMap.EnemyCount,
                SelectedMap.EnemySpeedMultiplier, SelectedMap.EnemyLives);
        }

        public override void UpdateBeforeMove()
        {
            var w = World;
            if (w == null || w.Player.Points.Count == 0) return;
            double dt = SnakeWorld.FixedStep;
            var playerHead = w.Player.Head;
            double distToExtract = Dist(playerHead, ExtractionPoint);

            // 撤离读条（离开立刻清零）
            if (distToExtract <= ExtractionRadius * w.Scale)
            {
                ExtractionTimer += dt;
                if (ExtractionTimer >= RequiredExtractionTime)
                {
                    ExtractionComplete = true;
                    w.GameOver(true);
                }
            }
            else ExtractionTimer = 0;

            // 最近容器（100px 内）
            CurrentNearContainer = null;
            for (int i = 0; i < Containers.Count; i++)
            {
                var c = Containers[i];
                if (c.IsSearched) continue;
                if (Dist(playerHead, c.Position) < 100 * w.Scale)
                { CurrentNearContainer = c; break; }
            }

            // 搜索状态机
            bool fPressed = w.FKeyDown;
            if (CurrentNearContainer != null && !IsPlayerSearching && fPressed)
            {
                IsPlayerSearching = true;
                SearchProgress = 0;
                SearchComplete = false;
            }
            else if (IsPlayerSearching)
            {
                double searchTime = 0.02 * (w.Player.Score + 1);
                SearchProgress += dt;
                if (SearchProgress >= searchTime && !SearchComplete)
                {
                    SearchComplete = true;
                    SearchScore = GenerateSearchScore();
                    SearchCirclePos = new Vec2(
                        CurrentNearContainer.Position.X,
                        CurrentNearContainer.Position.Y - 40 * w.Scale);
                }
                if (SearchComplete && fPressed)
                {
                    w.AddScore(SearchScore);
                    CurrentNearContainer.IsSearched = true;
                    IsPlayerSearching = false;
                    SearchComplete = false;
                }
            }

            // 火炸弹（仅航天基地）
            if (SelectedMap.HasFirebombs)
            {
                var enemies = w.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (_rand.NextDouble() < 0.02)
                    {
                        var target = new Vec2(
                            playerHead.X + (_rand.NextDouble() - 0.5) * 300,
                            playerHead.Y + (_rand.NextDouble() - 0.5) * 300);
                        Firebombs.Add(new SnakeFirebomb
                        {
                            Position = target,
                            Radius = 100,
                            Duration = 10,
                            Elapsed = 0
                        });
                    }
                }
                for (int i = Firebombs.Count - 1; i >= 0; i--)
                {
                    Firebombs[i].Elapsed += dt;
                    if (Firebombs[i].Elapsed >= Firebombs[i].Duration) Firebombs.RemoveAt(i);
                }

                bool inFire = false;
                for (int i = 0; i < Firebombs.Count; i++)
                    if (Dist(playerHead, Firebombs[i].Position) <= Firebombs[i].Radius * w.Scale)
                    { inFire = true; break; }

                if (inFire)
                {
                    FireExposureTimer += dt;
                    if (FireExposureTimer >= 5) w.GameOver(false);
                }
                else FireExposureTimer = 0;
            }
        }

        private int GenerateSearchScore()
        {
            double baseChance = 0.5;
            if (SelectedMap.Name == "边境森林") baseChance = 0.7;
            else if (SelectedMap.Name == "航天基地") baseChance = 0.9;
            return _rand.NextDouble() < baseChance ? _rand.Next(80, 100) : _rand.Next(20, 60);
        }

        /// <summary>搜索得分的颜色圈（原版 GetScoreColor）。</summary>
        public static Rgb ScoreColor(int score)
        {
            if (score >= 90) return Rgb.FromHex(0xFFD700);
            if (score >= 70) return Rgb.FromHex(0xFFA500);
            if (score >= 50) return Rgb.FromHex(0x800080);
            return Rgb.FromHex(0x6495ED);
        }

        public override void UpdateAI()
        {
            var w = World;
            if (w == null || w.Player.Points.Count == 0) return;
            _aiTime += SnakeWorld.FixedStep;

            bool mercy = w.IsMercyRound;
            var playerHead = w.Player.Head;
            double triggerDist = 600 * w.Scale;
            double triggerSq = triggerDist * triggerDist;
            int playerLen = w.Player.Points.Count;

            var enemies = w.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.Points.Count == 0) continue;
                var head = enemy.Head;
                double dxp = playerHead.X - head.X;
                double dyp = playerHead.Y - head.Y;
                double distSq = dxp * dxp + dyp * dyp;

                // 补偿局：慢慢靠近食物，不攻击
                if (mercy)
                {
                    if (!TrySteerToFood(enemy))
                        enemy.Direction = Norm(dxp, dyp);
                    continue;
                }

                if (distSq < triggerSq)
                {
                    double phase = (_aiTime + enemy.PhaseSeed * 0.001) % 4.0;
                    double attackThreshold = 2.8;
                    if (enemy.Points.Count > playerLen) attackThreshold = 3.4;
                    else if (enemy.Points.Count < playerLen / 2) attackThreshold = 2.0;

                    if (phase < attackThreshold)
                        enemy.Direction = Norm(dxp, dyp);
                    else
                    {
                        double len = Math.Sqrt(distSq);
                        if (len < 1e-6) len = 1;
                        double awayX = -dxp / len;
                        double awayY = -dyp / len;
                        double perpX = -awayY;
                        double perpY = awayX;
                        const double mix = 0.7;
                        enemy.Direction = Norm(awayX * mix + perpX * (1 - mix),
                                               awayY * mix + perpY * (1 - mix));
                    }
                }
                else
                {
                    // 追踪玩家时，若附近有食物先吃
                    var nearestFood = FindNearestFood(head);
                    bool goEat = false;
                    if (nearestFood != null)
                    {
                        double fdx = nearestFood.Position.X - head.X;
                        double fdy = nearestFood.Position.Y - head.Y;
                        double foodSq = fdx * fdx + fdy * fdy;
                        if (foodSq < distSq * 0.25)
                            goEat = true;
                    }
                    if (goEat)
                        enemy.Direction = Norm(nearestFood.Position.X - head.X,
                                               nearestFood.Position.Y - head.Y);
                    else
                        enemy.Direction = Norm(dxp, dyp);
                }
            }
        }
    }
}
