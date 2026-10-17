using System;

namespace ArknightsTD.Core.Geometry
{
    /// <summary>
    /// 轻量 2D 向量，供贪吃蛇世界使用（世界坐标，单位≈1 格）。
    /// 与 <see cref="Vec3"/> 一样保持不依赖引擎，便于脱离编辑器测试。
    /// </summary>
    public struct Vec2
    {
        public double X, Y;
        public Vec2(double x, double y) { X = x; Y = y; }

        public static Vec2 Zero => new Vec2(0, 0);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(double s, Vec2 a) => new Vec2(a.X * s, a.Y * s);

        public double Length => Math.Sqrt(X * X + Y * Y);

        /// <summary>归一化；零向量退化为 (1,0) 而不是 NaN。</summary>
        public Vec2 Normalized()
        {
            double l = Length;
            if (l < 1e-9) return new Vec2(1, 0);
            return new Vec2(X / l, Y / l);
        }

        public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        /// <summary>2D 叉积（标量），用于判断左右侧。</summary>
        public static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

        public static double Distance(Vec2 a, Vec2 b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static Vec2 Lerp(Vec2 a, Vec2 b, double t)
        {
            if (t < 0) t = 0; else if (t > 1) t = 1;
            return new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        public override string ToString() => $"({X:0.###},{Y:0.###})";
    }
}
