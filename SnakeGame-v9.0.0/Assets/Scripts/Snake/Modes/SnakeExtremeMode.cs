using System;

namespace ArknightsTD.Snake
{
    /// <summary>
    /// 极限模式。忠实移植 WPF 版 ExtremeMode.cs：
    /// AI 与经典模式同构，触发半径更大（500px）、进攻性更强。
    /// </summary>
    public sealed class SnakeExtremeMode : SnakeGameModeBase
    {
        public override string Name => "extreme";
        public override int FoodCount => 60;
        public override int EnemyCount => 20;
        public override double AiDifficulty => 1.5;

        private readonly Random _rand = new Random();

        public override void UpdateAI()
        {
            var w = World;
            if (w == null || w.Player.Points.Count == 0) return;
            _aiTime += SnakeWorld.FixedStep;

            bool mercy = w.IsMercyRound;
            var playerHead = w.Player.Head;
            double triggerDist = 500 * w.Scale;
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

                if (mercy)
                {
                    double fearDist = 120 * w.Scale;
                    if (distSq < fearDist * fearDist)
                        enemy.Direction = Norm(-dxp, -dyp);
                    else
                    {
                        if (_rand.NextDouble() < 0.3) TrySteerToFood(enemy);
                        else if (_rand.NextDouble() < 0.05)
                            enemy.Direction = RandomDir(_rand);
                    }
                    continue;
                }

                if (distSq < triggerSq)
                {
                    double phase = (_aiTime + enemy.PhaseSeed * 0.001) % 4.0;
                    double attackThreshold = 2.5;
                    if (enemy.Points.Count > playerLen)
                        attackThreshold = 3.2;
                    else if (enemy.Points.Count < playerLen / 2)
                        attackThreshold = 1.5;

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
