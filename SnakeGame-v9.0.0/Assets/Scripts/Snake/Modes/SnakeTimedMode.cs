using System;
using System.Collections.Generic;
using ArknightsTD.Core.Geometry;

namespace ArknightsTD.Snake
{
    /// <summary>
    /// 淘汰之王（计时赛·大逃杀）。忠实移植 WPF 版 TimedMode.cs（531 行）：
    /// 49 条带等级（1~10）敌蛇，等级决定速度与击杀掉落分数；
    /// 毒圈半径 5000 起、每 300s 缩 1000、共 3 次，圈外累计 15s 判负；
    /// 按 F 挑战 600px 内最近敌蛇竞速（先到 80px 者胜，25s 超时平局），
    /// 胜者等级 +1、对手被移除，败者出局；第三次缩圈后 180s 禁止决斗。
    /// 本模式玩家不会被碰撞杀死——只会死于毒圈或决斗失败；
    /// 分数来自食物与 AI 互杀掉落的食物（敌蛇等级即掉落分数）。
    /// </summary>
    public sealed class SnakeTimedMode : SnakeGameModeBase
    {
        public override string Name => "timed";
        public override int FoodCount => 0;
        public override int EnemyCount => 49;
        public override double AiDifficulty => 1.0;
        public override bool HasTimedRanking => true;

        public int PlayerLevel = 1;
        public const int MaxLevel = 10;

        private readonly Random _rand = new Random();
        private readonly List<int> _enemyLevels = new List<int>();

        public double PoisonRadius = 5000;
        public Vec2 PoisonCenter = new Vec2(0, 0);
        public double PoisonShrinkTimer = 300;
        public int ShrinkCount = 0;
        public const double PoisonTimeout = 15;
        private double _outsideTimer = 0;

        public bool IsRacing = false;
        public int RaceOpponentIndex = -1;
        public Vec2 RaceDestination = new Vec2(0, 0);
        public double RaceTimer = 0;
        public const double RaceTimeout = 25.0;
        public const double RaceArriveRadius = 80.0;
        public string RaceResultMessage = "";
        public double RaceResultTimer = 0;

        public bool RaceDisabled = false;
        public double DisableTimer = 180;

        /// <summary>道路宽度（原版十字道路，绘制在最底层）。</summary>
        public const double RoadWidth = 100;

        /// <summary>世界尺寸（原版固定 5000×4000）。</summary>
        public const double WorldWidth = 5000;
        public const double WorldHeight = 4000;

        /// <summary>敌蛇名字池（原版 GenerateRandomName）。</summary>
        private static readonly string[] Names =
        { "MMA", "ACE", "VIP", "GOD", "FOX", "WOLF", "BEAR", "TIGER", "LION", "EAGLE" };

        public override void Initialize()
        {
            var w = World;
            if (w == null) return;
            w.SetWorldSizeInternal(WorldWidth, WorldHeight);
            PlayerLevel = 1;
            PoisonRadius = 5000;
            PoisonCenter = new Vec2(0, 0);
            ShrinkCount = 0;
            PoisonShrinkTimer = 300;
            RaceDisabled = false;
            DisableTimer = 180;
            IsRacing = false;
            RaceOpponentIndex = -1;
            RaceTimer = 0;
            RaceResultMessage = "";
            RaceResultTimer = 0;
            _enemyLevels.Clear();
            _outsideTimer = 0;
            _aiTime = 0;
        }

        /// <summary>
        /// 开局：生成 49 条带等级敌蛇（原版 StartGameAfterSelection）。
        /// 等级写入敌蛇 Score（原版把 Score 兼作等级），速度 = 0.85 + 等级×0.03。
        /// </summary>
        public void StartGameAfterSelection()
        {
            var w = World;
            w.SetWorldSizeInternal(WorldWidth, WorldHeight);
            w.Enemies.Clear();
            _enemyLevels.Clear();
            for (int i = 0; i < EnemyCount; i++)
            {
                Vec2 spawn = w.RandomPosition(100);
                var enemy = new SnakeBody(spawn, Names[_rand.Next(Names.Length)])
                {
                    AiId = i,
                    PhaseSeed = i * 1.37 % 4.0,
                };
                int level = _rand.Next(1, MaxLevel + 1);
                _enemyLevels.Add(level);
                enemy.Score = level;
                enemy.SpeedMultiplier = 0.85 + level * 0.03;
                enemy.Points.Clear();
                enemy.Points.Add(spawn);
                enemy.Points.Add(new Vec2(spawn.X - 16 * w.Scale, spawn.Y));
                enemy.Points.Add(new Vec2(spawn.X - 32 * w.Scale, spawn.Y));
                w.Enemies.Add(enemy);
            }
            PlayerLevel = 1;
        }

        public override void UpdateBeforeMove()
        {
            var w = World;
            if (w == null || w.Outcome != MatchOutcome.Running) return;

            // 玩家速度随等级提升（原版：1.0 + (等级-1)*0.03）
            w.PlayerSpeedMultiplier = 1.0 + (PlayerLevel - 1) * 0.03;

            if (IsRacing)
            {
                UpdateRace();
                return;
            }

            UpdatePoison();
            CheckPlayerRaceRequest();
        }

        private void CheckPlayerRaceRequest()
        {
            var w = World;
            if (RaceDisabled) return;
            if (w.Player.Points.Count == 0) return;
            if (!w.FKeyDown) return;

            var head = w.Player.Head;
            int nearestIndex = -1;
            double nearestDist = 600 * w.Scale;
            var enemies = w.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.Points.Count == 0) continue;
                double d = Dist(head, e.Head);
                if (d < nearestDist) { nearestDist = d; nearestIndex = i; }
            }
            if (nearestIndex >= 0) StartRace(nearestIndex);
        }

        private void StartRace(int opponentIndex)
        {
            IsRacing = true;
            RaceOpponentIndex = opponentIndex;
            RaceTimer = 0;

            double angle = _rand.NextDouble() * Math.PI * 2;
            double maxByPoison = PoisonRadius * 0.6;
            double maxByWorld = Math.Min(WorldWidth, WorldHeight) * 0.35;
            double maxRadius = Math.Min(maxByPoison, maxByWorld);
            if (maxRadius < 300) maxRadius = 300;
            double radius = maxRadius * (0.5 + _rand.NextDouble() * 0.5);

            RaceDestination = new Vec2(
                PoisonCenter.X + Math.Cos(angle) * radius,
                PoisonCenter.Y + Math.Sin(angle) * radius);
            RaceResultMessage = "";
        }

        private void UpdateRace()
        {
            var w = World;
            if (w.Player.Points.Count == 0 || RaceOpponentIndex < 0
                || RaceOpponentIndex >= w.Enemies.Count
                || w.Enemies[RaceOpponentIndex].Points.Count == 0)
            {
                EndRace(0);
                return;
            }
            double dt = SnakeWorld.FixedStep;
            RaceTimer += dt;

            var head = w.Player.Head;
            double dxp = RaceDestination.X - head.X;
            double dyp = RaceDestination.Y - head.Y;
            double lenP = Math.Sqrt(dxp * dxp + dyp * dyp);
            if (lenP > 1) w.Player.Direction = new Vec2(dxp / lenP, dyp / lenP);

            var opp = w.Enemies[RaceOpponentIndex];
            var oh = opp.Head;
            double dxo = RaceDestination.X - oh.X;
            double dyo = RaceDestination.Y - oh.Y;
            double lenO = Math.Sqrt(dxo * dxo + dyo * dyo);
            if (lenO > 1) opp.Direction = new Vec2(dxo / lenO, dyo / lenO);

            if (lenP < RaceArriveRadius * w.Scale) { EndRace(1); return; }
            if (lenO < RaceArriveRadius * w.Scale) { EndRace(-1); return; }
            if (RaceTimer >= RaceTimeout) EndRace(0);
        }

        private void EndRace(int result)
        {
            var w = World;
            IsRacing = false;
            if (result == 1)
            {
                int idx = RaceOpponentIndex;
                if (idx >= 0 && idx < w.Enemies.Count)
                {
                    w.Enemies.RemoveAt(idx);
                    if (idx < _enemyLevels.Count) _enemyLevels.RemoveAt(idx);
                }
                if (PlayerLevel < MaxLevel) PlayerLevel++;
                RaceResultMessage = $"决斗胜利！等级 {PlayerLevel}";
                RaceResultTimer = 3.0;
            }
            else if (result == -1)
            {
                RaceResultMessage = "决斗失败！";
                RaceResultTimer = 3.0;
                w.GameOver(false);
            }
            else
            {
                RaceResultMessage = "决斗超时，平局";
                RaceResultTimer = 3.0;
            }
            RaceOpponentIndex = -1;
        }

        private void UpdatePoison()
        {
            var w = World;
            double dt = SnakeWorld.FixedStep;
            if (ShrinkCount < 3)
            {
                PoisonShrinkTimer -= dt;
                if (PoisonShrinkTimer <= 0)
                {
                    PoisonRadius -= 1000;
                    ShrinkCount++;
                    PoisonShrinkTimer = 300;
                    if (ShrinkCount == 3) DisableTimer = 180;
                }
            }
            if (DisableTimer > 0 && ShrinkCount == 3)
            {
                DisableTimer -= dt;
                if (DisableTimer <= 0) RaceDisabled = true;
            }

            if (w.Player.Points.Count == 0) return;
            double playerDist = Dist(w.Player.Head, PoisonCenter);
            if (playerDist > PoisonRadius)
            {
                _outsideTimer += dt;
                if (_outsideTimer >= PoisonTimeout) w.GameOver(false);
            }
            else _outsideTimer = 0;
        }

        /// <summary>圈外已停留时间（HUD 用）。</summary>
        public double OutsideTimer => _outsideTimer;

        /// <summary>场上总存活数（玩家 + 敌蛇，原版 UI 显示 Enemies.Count + 1）。</summary>
        public int AliveCount => (World != null ? World.Enemies.Count : 0) + 1;

        public override void UpdateAI()
        {
            var w = World;
            if (IsRacing) return;
            if (w.Player.Points.Count == 0) return;

            _aiTime += SnakeWorld.FixedStep;
            bool mercy = w.IsMercyRound;
            var playerHead = w.Player.Head;
            double triggerDist = 400 * w.Scale;
            double triggerSq = triggerDist * triggerDist;

            double dodgeChance = 0.75 + (PlayerLevel - 1) * 0.02;
            if (dodgeChance > 0.95) dodgeChance = 0.95;

            var enemies = w.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.Points.Count == 0) continue;
                var head = enemy.Head;

                if (mercy)
                {
                    if (_rand.NextDouble() < 0.02)
                        enemy.Direction = RandomDir(_rand);
                    continue;
                }

                // 1. 避开过近的其他敌蛇（100px）
                bool dodgedAI = false;
                double aiAvoidDist = 100 * w.Scale;
                double aiAvoidSq = aiAvoidDist * aiAvoidDist;
                for (int j = 0; j < enemies.Count; j++)
                {
                    if (j == i) continue;
                    var other = enemies[j];
                    if (other.Points.Count == 0) continue;
                    double ddx = head.X - other.Head.X;
                    double ddy = head.Y - other.Head.Y;
                    if (ddx * ddx + ddy * ddy < aiAvoidSq)
                    {
                        enemy.Direction = Norm(ddx, ddy);
                        dodgedAI = true;
                        break;
                    }
                }
                if (dodgedAI) continue;

                // 2. 回到毒圈内
                double dxc = head.X - PoisonCenter.X;
                double dyc = head.Y - PoisonCenter.Y;
                double distToCenterSq = dxc * dxc + dyc * dyc;
                double poisonThreshold = PoisonRadius * 0.9;
                if (distToCenterSq > poisonThreshold * poisonThreshold)
                {
                    enemy.Direction = Norm(PoisonCenter.X - head.X, PoisonCenter.Y - head.Y);
                    continue;
                }

                // 3. 面对玩家：按等级闪避或逃离
                double dxp = head.X - playerHead.X;
                double dyp = head.Y - playerHead.Y;
                double distToPlayerSq = dxp * dxp + dyp * dyp;

                if (distToPlayerSq < triggerSq)
                {
                    // 原版用 enemy.GetHashCode() 作 id（进程相关）；这里用稳定 AiId
                    int id = enemy.AiId;
                    double seed = id * 0.6180339887 + Math.Floor(_aiTime * 2.0);
                    double roll = Math.Abs(seed * 9301 + 49297) % 233280 / 233280.0;

                    if (roll < dodgeChance)
                    {
                        double angle = Math.Atan2(dyp, dxp) + Math.Sin(_aiTime + id) * 0.4;
                        enemy.Direction = new Vec2(Math.Cos(angle), Math.Sin(angle));
                    }
                    else
                    {
                        enemy.Direction = Norm(-dxp, -dyp);
                    }
                }
                else
                {
                    // 4. 找食物，否则在毒圈内绕圈游走
                    if (!TrySteerToFood(enemy))
                    {
                        int id = enemy.AiId;
                        double angle = _aiTime * 0.5 + id * 0.7;
                        double radius = 1200 + Math.Sin(_aiTime * 0.3 + id) * 600;
                        double tx = PoisonCenter.X + Math.Cos(angle) * radius;
                        double ty = PoisonCenter.Y + Math.Sin(angle) * radius;
                        enemy.Direction = Norm(tx - head.X, ty - head.Y);
                    }
                }
            }
        }

        public override void UpdateAfterMove()
        {
            var w = World;
            if (RaceResultTimer > 0)
            {
                RaceResultTimer -= SnakeWorld.FixedStep;
                if (RaceResultTimer < 0) RaceResultTimer = 0;
            }
        }

        /// <summary>读取某条敌蛇的等级。</summary>
        public int EnemyLevel(int index)
            => index >= 0 && index < _enemyLevels.Count ? _enemyLevels[index] : 1;

        /// <summary>等级对应的颜色（原版 GetLevelColor）。</summary>
        public static Rgb LevelColor(int level)
        {
            if (level >= 10) return Rgb.FromHex(0xFFD700);   // 金
            if (level >= 7) return Rgb.FromHex(0x800080);    // 紫
            if (level >= 4) return Rgb.FromHex(0x0000FF);    // 蓝
            return Rgb.FromHex(0x008000);                    // 绿
        }
    }
}
