using System;

namespace ArknightsTD.Core.Geometry
{
    /// <summary>
    /// 2D（俯视）图元构建器。
    /// <para>
    /// 与 <see cref="Primitives"/> 的区别：这套图元全部建在 <b>XY 平面</b>上（z = 0），
    /// 法线朝 +Z，供正交俯视相机观看。
    /// </para>
    /// <para>
    /// 为什么需要两套：塔防是 3D 斜视（XZ 地面 + Y 高度），
    /// 而贪吃蛇的 5 个模式要求俯视 2D——两者坐标系与绕序都不同，不能混用。
    /// </para>
    /// </summary>
    public static class Primitives2D
    {
        /// <summary>俯视图元的法线（朝 +Z，即朝向相机）。</summary>
        public static readonly Vec3 Normal = new Vec3(0, 0, 1);

        /// <summary>
        /// 圆（三角扇）。顶点按逆时针排列，从 +Z 看为正面。
        /// </summary>
        public static void Disc(MeshBuilder mb, double cx, double cy, double radius, int segments = 16)
        {
            if (radius <= 0) return;
            if (segments < 3) segments = 3;

            for (int i = 0; i < segments; i++)
            {
                double a0 = i * 2.0 * Math.PI / segments;
                double a1 = (i + 1) * 2.0 * Math.PI / segments;
                // 绕序：从 +Z 看为逆时针（法线才朝 +Z）。
                // 先前写成 (中心, a0, a1)，从 +Z 看是顺时针，法线朝 -Z，
                // 结果整个面被背面剔除（实测只剩网格线可见）。
                mb.AddTriangleWithNormal(
                    new Vec3(cx, cy, 0),
                    new Vec3(cx + Math.Cos(a1) * radius, cy + Math.Sin(a1) * radius, 0),
                    new Vec3(cx + Math.Cos(a0) * radius, cy + Math.Sin(a0) * radius, 0),
                    Normal);
            }
        }

        /// <summary>圆环（内径到外径）。</summary>
        public static void Ring(MeshBuilder mb, double cx, double cy,
            double inner, double outer, int segments = 24)
        {
            if (outer <= inner || outer <= 0) return;
            if (segments < 3) segments = 3;

            for (int i = 0; i < segments; i++)
            {
                double a0 = i * 2.0 * Math.PI / segments;
                double a1 = (i + 1) * 2.0 * Math.PI / segments;
                var i0 = new Vec3(cx + Math.Cos(a0) * inner, cy + Math.Sin(a0) * inner, 0);
                var o0 = new Vec3(cx + Math.Cos(a0) * outer, cy + Math.Sin(a0) * outer, 0);
                var i1 = new Vec3(cx + Math.Cos(a1) * inner, cy + Math.Sin(a1) * inner, 0);
                var o1 = new Vec3(cx + Math.Cos(a1) * outer, cy + Math.Sin(a1) * outer, 0);
                mb.AddQuad(i0, i1, o1, o0, Normal);
            }
        }

        /// <summary>
        /// 凸多边形（用于蛇头眼睛、碎片等）。点数需 &gt;= 3。
        /// </summary>
        public static void Polygon(MeshBuilder mb, Vec3[] pts)
        {
            if (pts == null || pts.Length < 3) return;
            // 绕序反转，保证从 +Z 看是逆时针
            for (int i = 1; i < pts.Length - 1; i++)
                mb.AddTriangleWithNormal(pts[0], pts[i + 1], pts[i], Normal);
        }

        /// <summary>正多边形（如六边形食物）。</summary>
        public static void RegularPolygon(MeshBuilder mb, double cx, double cy,
            double radius, int sides, double rotation = 0)
        {
            if (sides < 3) sides = 3;
            var pts = new Vec3[sides];
            for (int i = 0; i < sides; i++)
            {
                double a = rotation + i * 2.0 * Math.PI / sides;
                pts[i] = new Vec3(cx + Math.Cos(a) * radius, cy + Math.Sin(a) * radius, 0);
            }
            Polygon(mb, pts);
        }

        /// <summary>轴对齐矩形。</summary>
        public static void Rect(MeshBuilder mb, double minX, double minY, double maxX, double maxY)
        {
            if (maxX <= minX || maxY <= minY) return;
            mb.AddQuad(
                new Vec3(minX, minY, 0), new Vec3(minX, maxY, 0),
                new Vec3(maxX, maxY, 0), new Vec3(maxX, minY, 0), Normal);
        }

        /// <summary>线段（厚度由 halfWidth 给出），用于藤蔓、轨迹线。</summary>
        public static void Segment(MeshBuilder mb, double ax, double ay, double bx, double by,
            double halfWidth)
        {
            double dx = bx - ax, dy = by - ay;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9 || halfWidth <= 0) return;
            double nx = -dy / len * halfWidth, ny = dx / len * halfWidth;
            mb.AddQuad(
                new Vec3(ax + nx, ay + ny, 0), new Vec3(ax - nx, ay - ny, 0),
                new Vec3(bx - nx, by - ny, 0), new Vec3(bx + nx, by + ny, 0), Normal);
        }
    }
}
