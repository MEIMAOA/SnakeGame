using System;
using ArknightsTD.Core.Geometry;

namespace ArknightsTD.Snake
{
    /// <summary>
    /// 占领模式（4v4）。忠实移植 WPF 版 TeamMode.cs：
    /// 优先冲向占领点（世界原点，宿主从不改）；
    /// 占领点内发现敌方（红队额外锁定圈内玩家）→ 攻击，速度 1.2；
    /// 顺路吃食物：食物距离² &lt; 到占点距离²×0.35 才去吃，速度 1.6；
    /// 补偿局：只慢慢去占领点，不攻击（速度 0.4）。
    /// </summary>
    public sealed class SnakeTeamMode : SnakeGameModeBase
    {
        public override string Name => "team4v4";
        public override int FoodCount => 125;
        public override int EnemyCount => 7;
        public override double AiDifficulty => 0.9;
        public override bool HasCaptureZone => true;

        public Vec2 CaptureZone { get; set; } = new Vec2(0, 0);

        public override void UpdateAI()
        {
            var w = World;
            if (w == null || w.Player.Points.Count == 0) return;
            _aiTime += SnakeWorld.FixedStep;

            bool mercy = w.IsMercyRound;
            var captureZone = CaptureZone;
            var playerHead = w.Player.Head;
            double captureRadius = 150 * w.Scale;
            double captureRadiusSq = captureRadius * captureRadius;

            var enemies = w.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.Points.Count == 0) continue;
                var head = enemy.Head;

                // 补偿局：只慢慢去占领点，不攻击
                if (mercy)
                {
                    enemy.Direction = Norm(captureZone.X - head.X,
                                           captureZone.Y - head.Y);
                    enemy.SpeedMultiplier = 0.4;
                    continue;
                }

                // 找占领点内的敌方目标
                Vec2? targetPoint = null;
                double nearestSq = double.MaxValue;

                for (int j = 0; j < enemies.Count; j++)
                {
                    var other = enemies[j];
                    if (other == enemy) continue;
                    if (other.Points.Count == 0) continue;
                    if (other.Team == enemy.Team) continue;

                    double odx = other.Head.X - captureZone.X;
                    double ody = other.Head.Y - captureZone.Y;
                    if (odx * odx + ody * ody > captureRadiusSq) continue;

                    double edx = other.Head.X - head.X;
                    double edy = other.Head.Y - head.Y;
                    double edSq = edx * edx + edy * edy;
                    if (edSq < nearestSq)
                    {
                        nearestSq = edSq;
                        targetPoint = other.Head;
                    }
                }

                if (enemy.Team == "red")
                {
                    double pdx = playerHead.X - captureZone.X;
                    double pdy = playerHead.Y - captureZone.Y;
                    if (pdx * pdx + pdy * pdy < captureRadiusSq)
                    {
                        double edx = playerHead.X - head.X;
                        double edy = playerHead.Y - head.Y;
                        double edSq = edx * edx + edy * edy;
                        if (edSq < nearestSq)
                        {
                            nearestSq = edSq;
                            targetPoint = playerHead;
                        }
                    }
                }

                if (targetPoint.HasValue)
                {
                    enemy.Direction = Norm(targetPoint.Value.X - head.X,
                                           targetPoint.Value.Y - head.Y);
                    enemy.SpeedMultiplier = 1.2;
                }
                else
                {
                    // 顺路吃食物：如果最近的比占领点更近，先吃
                    double ddx = captureZone.X - head.X;
                    double ddy = captureZone.Y - head.Y;
                    double distToZoneSq = ddx * ddx + ddy * ddy;

                    var nearestFood = FindNearestFood(head);
                    bool goEatFood = false;
                    if (nearestFood != null)
                    {
                        double fdx = nearestFood.Position.X - head.X;
                        double fdy = nearestFood.Position.Y - head.Y;
                        double foodSq = fdx * fdx + fdy * fdy;
                        if (foodSq < distToZoneSq * 0.35)   // 食物明显更近才顺路
                            goEatFood = true;
                    }

                    if (goEatFood)
                    {
                        enemy.Direction = Norm(nearestFood.Position.X - head.X,
                                               nearestFood.Position.Y - head.Y);
                    }
                    else
                    {
                        enemy.Direction = Norm(captureZone.X - head.X,
                                               captureZone.Y - head.Y);
                    }
                    enemy.SpeedMultiplier = 1.6;
                }
            }
        }
    }
}
