using System;
using System.Collections.Generic;
using ArknightsTD.Core.Geometry;

namespace ArknightsTD.Snake
{
    /// <summary>
    /// AI 通用向量工具。敌蛇决策逻辑已随各模式类移植
    /// （<see cref="SnakeGameModeBase"/> 的 5 个派生类），这里只留
    /// 输入/测试/预览层需要的归一化、随机方向与最近食物查询。
    /// </summary>
    public static class SnakeAi
    {
        public static Vec2 Norm(double dx, double dy)
        {
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return new Vec2(1, 0);
            return new Vec2(dx / len, dy / len);
        }

        public static Vec2 RandomDir(Random rand)
        {
            double a = rand.NextDouble() * Math.PI * 2;
            return new Vec2(Math.Cos(a), Math.Sin(a));
        }

        /// <summary>最近的食物（无则 null）。</summary>
        public static FoodItem FindNearestFood(Vec2 from, List<FoodItem> foods)
        {
            if (foods == null || foods.Count == 0) return null;

            FoodItem best = null;
            double bestSq = double.MaxValue;
            for (int i = 0; i < foods.Count; i++)
            {
                var f = foods[i];
                double dx = f.Position.X - from.X;
                double dy = f.Position.Y - from.Y;
                double sq = dx * dx + dy * dy;
                if (sq < bestSq) { bestSq = sq; best = f; }
            }
            return best;
        }
    }
}
