using System;

namespace ArknightsTD.Snake
{
    /// <summary>
    /// 经典模式。忠实移植 WPF 版 ClassicMode.cs：
    /// 见到玩家 400px 内 → 体型和时间决定进攻/躲避；否则寻找最近食物（无食物则随机方向）；
    /// 补偿局变蠢（只在 120px 内逃跑，其余缓慢随机游走/偶尔吃食物）。
    /// </summary>
    public sealed class SnakeClassicMode : SnakeGameModeBase
    {
        public override string Name => "classic";
        public override int FoodCount => 125;
        public override int EnemyCount => 10;
        public override double AiDifficulty => 0.6;

        private readonly Random _rand = new Random();

        public override void UpdateAI()
        {
            var w = World;
            if (w == null || w.Player.Points.Count == 0) return;
            _aiTime += SnakeWorld.FixedStep;

            bool mercy = w.IsMercyRound;
            var playerHead = w.Player.Head;
            double triggerDist = 400 * w.Scale;
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

                // 补偿局：蠢 AI —— 只在极近距离（120px）才反应，且永远躲避
                if (mercy)
                {
                    double fearDist = 120 * w.Scale;
                    if (distSq < fearDist * fearDist)
                    {
                        enemy.Direction = Norm(-dxp, -dyp);
                    }
                    else
                    {
                        // 缓慢随机游走 + 偶尔吃食物（两次独立掷骰，与 WPF 一致）
                        if (_rand.NextDouble() < 0.3) TrySteerToFood(enemy);
                        else if (_rand.NextDouble() < 0.05)
                            enemy.Direction = RandomDir(_rand);
                    }
                    continue;
                }

                if (distSq < triggerSq)
                {
                    // 相位：WPF 用 enemy.GetHashCode()*0.001（进程相关、不可复现），
                    // 这里改用敌蛇自身的稳定相位种子（出生时赋值），保证行为可复现。
                    double phase = (_aiTime + enemy.PhaseSeed * 0.001) % 4.0;
                    double attackThreshold = 2.0;
                    if (enemy.Points.Count > playerLen)
                        attackThreshold = 2.8;
                    else if (enemy.Points.Count < playerLen / 2)
                        attackThreshold = 1.0;

                    if (phase < attackThreshold)
                    {
                        enemy.Direction = Norm(dxp, dyp);
                    }
                    else
                    {
                        double len = Math.Sqrt(distSq);
                        if (len < 1e-6) len = 1;
                        double awayX = -dxp / len;
                        double awayY = -dyp / len;
                        double perpX = -awayY;
                        double perpY = awayX;
                        const double mix = 0.75;
                        enemy.Direction = Norm(awayX * mix + perpX * (1 - mix),
                                               awayY * mix + perpY * (1 - mix));
                    }
                }
                else
                {
                    if (!TrySteerToFood(enemy))
                        enemy.Direction = RandomDir(_rand);
                }
            }
        }
    }
}
