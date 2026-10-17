using System;
using System.Collections.Generic;
using ArknightsTD.Core.Geometry;

namespace ArknightsTD.Snake
{
    /// <summary>
    /// 贪吃蛇模式基类。忠实移植 WPF 版 <c>GameModeBase.cs</c>：
    /// 模式对象只负责「自己的规则与 AI」，宿主（<see cref="SnakeWorld"/>，
    /// 对应 WPF 的 MainWindow）负责通用移动 / 碰撞 / 食物 / 道具 / 结算。
    /// </summary>
    public abstract class SnakeGameModeBase
    {
        /// <summary>模式标识（对应 WPF 的 GameMode 字符串）。</summary>
        public abstract string Name { get; }

        /// <summary>食物数（宿主按此生成；与 WPF 一致，部分模式宿主另有硬编码）。</summary>
        public abstract int FoodCount { get; }

        /// <summary>敌人数。</summary>
        public abstract int EnemyCount { get; }

        /// <summary>AI 难度系数（参与敌蛇速度计算）。</summary>
        public abstract double AiDifficulty { get; }

        public virtual bool HasCaptureZone => false;
        public virtual bool HasTimedRanking => false;

        /// <summary>宿主世界（对应 WPF 的 MainWindow Form）。</summary>
        public SnakeWorld World { get; private set; }

        public void Bind(SnakeWorld world) => World = world;

        /// <summary>通用 AI 时间累加（各模式共享，WPF 原版同名字段）。</summary>
        protected double _aiTime = 0;

        public virtual void Initialize() { }
        public virtual void UpdateBeforeMove() { }
        public virtual void UpdateAfterMove() { }
        public virtual void UpdateAI() { }

        // ==================== AI 通用工具方法（原版 GameModeBase）====================

        protected static double Dist(Vec2 a, Vec2 b)
            => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        protected static Vec2 Norm(double dx, double dy)
        {
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) return new Vec2(1, 0);
            return new Vec2(dx / len, dy / len);
        }

        protected static Vec2 RandomDir(Random r)
        {
            double angle = r.NextDouble() * Math.PI * 2;
            return new Vec2(Math.Cos(angle), Math.Sin(angle));
        }

        /// <summary>寻找离指定点最近的食物（原版 FindNearestFood）。</summary>
        protected FoodItem FindNearestFood(Vec2 head)
        {
            if (World == null || World.Foods == null) return null;
            FoodItem nearest = null;
            double nearestSq = double.MaxValue;
            var foods = World.Foods;
            for (int i = 0; i < foods.Count; i++)
            {
                var f = foods[i];
                double dx = f.Position.X - head.X;
                double dy = f.Position.Y - head.Y;
                double sq = dx * dx + dy * dy;
                if (sq < nearestSq) { nearestSq = sq; nearest = f; }
            }
            return nearest;
        }

        /// <summary>让敌人朝最近食物移动，返回是否成功（原版 TrySteerToFood）。</summary>
        protected bool TrySteerToFood(SnakeBody enemy)
        {
            if (enemy == null || enemy.Points.Count == 0) return false;
            var nearest = FindNearestFood(enemy.Head);
            if (nearest == null) return false;
            enemy.Direction = Norm(nearest.Position.X - enemy.Head.X,
                                   nearest.Position.Y - enemy.Head.Y);
            return true;
        }
    }
}
