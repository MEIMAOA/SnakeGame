using System;

namespace ArknightsTD.Core.Geometry
{
    /// <summary>
    /// 程序化基础几何体工厂（移植自 WPF 版 TdPrim）。
    /// 所有形状以原点为中心或底面贴 y=0，由调用方自行平移/旋转。
    /// </summary>
    public static class Primitives
    {
        /// <summary>轴对齐长方体。center 为中心，half 为半尺寸。</summary>
        public static void Box(MeshBuilder mb, Vec3 center, Vec3 half)
        {
            double x0 = center.X - half.X, x1 = center.X + half.X;
            double y0 = center.Y - half.Y, y1 = center.Y + half.Y;
            double z0 = center.Z - half.Z, z1 = center.Z + half.Z;

            var p000 = new Vec3(x0, y0, z0); var p100 = new Vec3(x1, y0, z0);
            var p110 = new Vec3(x1, y1, z0); var p010 = new Vec3(x0, y1, z0);
            var p001 = new Vec3(x0, y0, z1); var p101 = new Vec3(x1, y0, z1);
            var p111 = new Vec3(x1, y1, z1); var p011 = new Vec3(x0, y1, z1);

            mb.AddQuad(p001, p101, p111, p011, new Vec3(0, 0, 1));   // +Z
            mb.AddQuad(p100, p000, p010, p110, new Vec3(0, 0, -1));  // -Z
            mb.AddQuad(p101, p100, p110, p111, new Vec3(1, 0, 0));   // +X
            mb.AddQuad(p000, p001, p011, p010, new Vec3(-1, 0, 0));  // -X
            mb.AddQuad(p010, p011, p111, p110, new Vec3(0, 1, 0));   // +Y
            mb.AddQuad(p000, p100, p101, p001, new Vec3(0, -1, 0));  // -Y
        }

        /// <summary>底面贴 y=0 的长方体（地形/建筑常用）。</summary>
        public static void BoxOnGround(MeshBuilder mb, double cx, double cz,
            double sizeX, double height, double sizeZ)
        {
            Box(mb, new Vec3(cx, height * 0.5, cz), new Vec3(sizeX * 0.5, height * 0.5, sizeZ * 0.5));
        }

        /// <summary>水平地面片（法线 +Y）。</summary>
        public static void GroundQuad(MeshBuilder mb, double x0, double z0, double x1, double z1, double y)
        {
            mb.AddQuad(new Vec3(x0, y, z0), new Vec3(x1, y, z0),
                       new Vec3(x1, y, z1), new Vec3(x0, y, z1), new Vec3(0, 1, 0));
        }

        /// <summary>水平环带（地面描边/发光带）。</summary>
        public static void GroundRing(MeshBuilder mb, double cx, double cz,
            double inner, double outer, double y, int seg = 20)
        {
            if (seg < 3) seg = 3;
            for (int i = 0; i < seg; i++)
            {
                double a0 = i * 2 * Math.PI / seg;
                double a1 = (i + 1) * 2 * Math.PI / seg;
                var i0 = new Vec3(cx + Math.Cos(a0) * inner, y, cz + Math.Sin(a0) * inner);
                var o0 = new Vec3(cx + Math.Cos(a0) * outer, y, cz + Math.Sin(a0) * outer);
                var i1 = new Vec3(cx + Math.Cos(a1) * inner, y, cz + Math.Sin(a1) * inner);
                var o1 = new Vec3(cx + Math.Cos(a1) * outer, y, cz + Math.Sin(a1) * outer);
                mb.AddQuad(i0, o0, o1, i1, new Vec3(0, 1, 0));
            }
        }

        /// <summary>四棱锥（尖端朝上）。</summary>
        public static void Pyramid(MeshBuilder mb, double cx, double cz,
            double baseSize, double baseY, double tipY)
        {
            double h = baseSize * 0.5;
            var a = new Vec3(cx - h, baseY, cz - h);
            var b = new Vec3(cx + h, baseY, cz - h);
            var c = new Vec3(cx + h, baseY, cz + h);
            var d = new Vec3(cx - h, baseY, cz + h);
            var tip = new Vec3(cx, tipY, cz);

            mb.AddQuad(a, d, c, b, new Vec3(0, -1, 0));   // 底面朝下
            mb.AddTriangle(a, b, tip);
            mb.AddTriangle(b, c, tip);
            mb.AddTriangle(c, d, tip);
            mb.AddTriangle(d, a, tip);
        }

        /// <summary>棱柱（侧面数可配）—— 低多边形身体/圆柱部件。</summary>
        public static void Prism(MeshBuilder mb, Vec3 baseCenter,
            double radiusX, double radiusZ, double height, int sides = 8, double rotation = 0)
        {
            if (sides < 3) sides = 3;
            var bottom = new Vec3[sides];
            var top = new Vec3[sides];
            for (int i = 0; i < sides; i++)
            {
                double a = rotation + i * 2 * Math.PI / sides;
                double x = baseCenter.X + Math.Cos(a) * radiusX;
                double z = baseCenter.Z + Math.Sin(a) * radiusZ;
                bottom[i] = new Vec3(x, baseCenter.Y, z);
                top[i] = new Vec3(x, baseCenter.Y + height, z);
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                mb.AddQuad(bottom[i], bottom[j], top[j], top[i]);
            }

            var cTop = new Vec3(baseCenter.X, baseCenter.Y + height, baseCenter.Z);
            var cBot = new Vec3(baseCenter.X, baseCenter.Y, baseCenter.Z);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                mb.AddTriangle(cTop, top[i], top[j]);
                mb.AddTriangle(cBot, bottom[j], bottom[i]);
            }
        }

        /// <summary>棱锥台（上窄下宽）—— 重装单位/机械腿。</summary>
        public static void Frustum(MeshBuilder mb, Vec3 baseCenter,
            double baseRadiusX, double baseRadiusZ,
            double topRadiusX, double topRadiusZ,
            double height, int sides = 8, double rotation = 0)
        {
            if (sides < 3) sides = 3;
            var bottom = new Vec3[sides];
            var top = new Vec3[sides];
            for (int i = 0; i < sides; i++)
            {
                double a = rotation + i * 2 * Math.PI / sides;
                double ca = Math.Cos(a), sa = Math.Sin(a);
                bottom[i] = new Vec3(baseCenter.X + ca * baseRadiusX, baseCenter.Y, baseCenter.Z + sa * baseRadiusZ);
                top[i] = new Vec3(baseCenter.X + ca * topRadiusX, baseCenter.Y + height, baseCenter.Z + sa * topRadiusZ);
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                mb.AddQuad(bottom[i], bottom[j], top[j], top[i]);
            }

            var cTop = new Vec3(baseCenter.X, baseCenter.Y + height, baseCenter.Z);
            var cBot = new Vec3(baseCenter.X, baseCenter.Y, baseCenter.Z);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                mb.AddTriangle(cTop, top[i], top[j]);
                mb.AddTriangle(cBot, bottom[j], bottom[i]);
            }
        }

        /// <summary>
        /// 带倒角的长方体（明日方舟式切角面板/机械件）。
        /// <para>
        /// 规范倒角盒 = 26 个面：<b>6 个轴向主面 + 12 个棱面 + 8 个角面</b>。
        /// 棱面法线是 (1,1,0) 这类二分量方向，角面法线是 (1,1,1) 这类三分量方向 ——
        /// 这些斜向面正是"切角"外观的来源。
        /// </para>
        /// <para>
        /// 实现要点：每个面都用<b>显式顶点 + 显式法线</b>写入，不依赖绕序反推法线，
        /// 也不依赖任何中间状态（早期版本用运行时标志推导法线，导致棱面/角面的法线
        /// 退化成轴向，倒角外观丢失）。
        /// 倒角大小会被钳到各半尺寸的 0.49，保证内层尺寸恒为正、不产生退化面。
        /// </para>
        /// <para>三角数：6×2 + 12×2 + 8×1 = 40。</para>
        /// </summary>
        public static void BeveledBox(MeshBuilder mb, Vec3 center, Vec3 half, double bevel)
        {
            // 负数倒角是无效输入：明确退化为普通方块，而不是取绝对值静默当成正倒角
            if (bevel <= 0) { Box(mb, center, half); return; }

            double bx = Math.Min(bevel, half.X * 0.49);
            double by = Math.Min(Math.Abs(bevel), half.Y * 0.49);
            double bz = Math.Min(Math.Abs(bevel), half.Z * 0.49);
            if (bx <= 1e-9 || by <= 1e-9 || bz <= 1e-9) { Box(mb, center, half); return; }

            double hx = half.X, hy = half.Y, hz = half.Z;
            double cx = center.X, cy = center.Y, cz = center.Z;

            // 内层（切角后）半尺寸
            double ix = hx - bx, iy = hy - by, iz = hz - bz;

            // ---- 6 个轴向主面（内缩到切角处）----
            // +X / -X
            AddPlanarFace(mb,
                new Vec3(cx + hx, cy - iy, cz - iz), new Vec3(cx + hx, cy + iy, cz - iz),
                new Vec3(cx + hx, cy + iy, cz + iz), new Vec3(cx + hx, cy - iy, cz + iz),
                new Vec3(1, 0, 0));
            AddPlanarFace(mb,
                new Vec3(cx - hx, cy - iy, cz + iz), new Vec3(cx - hx, cy + iy, cz + iz),
                new Vec3(cx - hx, cy + iy, cz - iz), new Vec3(cx - hx, cy - iy, cz - iz),
                new Vec3(-1, 0, 0));
            // +Y / -Y
            AddPlanarFace(mb,
                new Vec3(cx - ix, cy + hy, cz + iz), new Vec3(cx + ix, cy + hy, cz + iz),
                new Vec3(cx + ix, cy + hy, cz - iz), new Vec3(cx - ix, cy + hy, cz - iz),
                new Vec3(0, 1, 0));
            AddPlanarFace(mb,
                new Vec3(cx - ix, cy - hy, cz - iz), new Vec3(cx + ix, cy - hy, cz - iz),
                new Vec3(cx + ix, cy - hy, cz + iz), new Vec3(cx - ix, cy - hy, cz + iz),
                new Vec3(0, -1, 0));
            // +Z / -Z
            AddPlanarFace(mb,
                new Vec3(cx - ix, cy - iy, cz + hz), new Vec3(cx + ix, cy - iy, cz + hz),
                new Vec3(cx + ix, cy + iy, cz + hz), new Vec3(cx - ix, cy + iy, cz + hz),
                new Vec3(0, 0, 1));
            AddPlanarFace(mb,
                new Vec3(cx + ix, cy - iy, cz - hz), new Vec3(cx - ix, cy - iy, cz - hz),
                new Vec3(cx - ix, cy + iy, cz - hz), new Vec3(cx + ix, cy + iy, cz - hz),
                new Vec3(0, 0, -1));

            // ---- 12 个棱面：法线为二分量方向 ----
            // 沿 Z 的四条棱（法线 ±X±Y，恒在 z 方向铺满内层 iz）
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    double xo = cx + sx * hx, xi = cx + sx * ix;
                    double yo = cy + sy * hy, yi = cy + sy * iy;
                    var n = new Vec3(sx, sy, 0).Normalized();

                    Vec3 a, b, c, d;
                    if (sx > 0 && sy > 0)
                    {
                        a = new Vec3(xi, yo, cz - iz); b = new Vec3(xo, yi, cz - iz);
                        c = new Vec3(xo, yi, cz + iz); d = new Vec3(xi, yo, cz + iz);
                    }
                    else if (sx < 0 && sy > 0)
                    {
                        a = new Vec3(xo, yi, cz - iz); b = new Vec3(xi, yo, cz - iz);
                        c = new Vec3(xi, yo, cz + iz); d = new Vec3(xo, yi, cz + iz);
                    }
                    else if (sx > 0 && sy < 0)
                    {
                        a = new Vec3(xo, yi, cz - iz); b = new Vec3(xi, yo, cz - iz);
                        c = new Vec3(xi, yo, cz + iz); d = new Vec3(xo, yi, cz + iz);
                    }
                    else
                    {
                        a = new Vec3(xi, yo, cz - iz); b = new Vec3(xo, yi, cz - iz);
                        c = new Vec3(xo, yi, cz + iz); d = new Vec3(xi, yo, cz + iz);
                    }
                    mb.AddQuad(a, b, c, d, n);
                }

            // 沿 X 的四条棱（法线 ±Y±Z）
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    double yo = cy + sy * hy, yi = cy + sy * iy;
                    double zo = cz + sz * hz, zi = cz + sz * iz;
                    var n = new Vec3(0, sy, sz).Normalized();

                    Vec3 a, b, c, d;
                    if (sy > 0 && sz > 0)
                    {
                        a = new Vec3(cx - ix, yi, zo); b = new Vec3(cx - ix, yo, zi);
                        c = new Vec3(cx + ix, yo, zi); d = new Vec3(cx + ix, yi, zo);
                    }
                    else if (sy > 0 && sz < 0)
                    {
                        a = new Vec3(cx - ix, yo, zi); b = new Vec3(cx - ix, yi, zo);
                        c = new Vec3(cx + ix, yi, zo); d = new Vec3(cx + ix, yo, zi);
                    }
                    else if (sy < 0 && sz > 0)
                    {
                        a = new Vec3(cx - ix, yo, zi); b = new Vec3(cx - ix, yi, zo);
                        c = new Vec3(cx + ix, yi, zo); d = new Vec3(cx + ix, yo, zi);
                    }
                    else
                    {
                        a = new Vec3(cx - ix, yi, zo); b = new Vec3(cx - ix, yo, zi);
                        c = new Vec3(cx + ix, yo, zi); d = new Vec3(cx + ix, yi, zo);
                    }
                    mb.AddQuad(a, b, c, d, n);
                }

            // 沿 Y 的四条棱（法线 ±X±Z）
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    double xo = cx + sx * hx, xi = cx + sx * ix;
                    double zo = cz + sz * hz, zi = cz + sz * iz;
                    var n = new Vec3(sx, 0, sz).Normalized();

                    Vec3 a, b, c, d;
                    if (sx > 0 && sz > 0)
                    {
                        a = new Vec3(xo, cy - iy, zi); b = new Vec3(xi, cy - iy, zo);
                        c = new Vec3(xi, cy + iy, zo); d = new Vec3(xo, cy + iy, zi);
                    }
                    else if (sx > 0 && sz < 0)
                    {
                        a = new Vec3(xi, cy - iy, zo); b = new Vec3(xo, cy - iy, zi);
                        c = new Vec3(xo, cy + iy, zi); d = new Vec3(xi, cy + iy, zo);
                    }
                    else if (sx < 0 && sz > 0)
                    {
                        a = new Vec3(xi, cy - iy, zo); b = new Vec3(xo, cy - iy, zi);
                        c = new Vec3(xo, cy + iy, zi); d = new Vec3(xi, cy + iy, zo);
                    }
                    else
                    {
                        a = new Vec3(xo, cy - iy, zi); b = new Vec3(xi, cy - iy, zo);
                        c = new Vec3(xi, cy + iy, zo); d = new Vec3(xo, cy + iy, zi);
                    }
                    mb.AddQuad(a, b, c, d, n);
                }

            // ---- 8 个角面：法线为三分量方向 (sx,sy,sz) ----
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        double xo = cx + sx * hx, xi = cx + sx * ix;
                        double yo = cy + sy * hy, yi = cy + sy * iy;
                        double zo = cz + sz * hz, zi = cz + sz * iz;

                        var n = new Vec3(sx, sy, sz).Normalized();
                        var p0 = new Vec3(xi, yo, zo);   // 跨 ±X
                        var p1 = new Vec3(xo, yi, zo);   // 跨 ±Y
                        var p2 = new Vec3(xo, yo, zi);   // 跨 ±Z

                        // 三角形绕序须使 cross(p1-p0, p2-p0) 与法线同向
                        var cross = Vec3.Cross(p1 - p0, p2 - p0);
                        if (Vec3.Dot(cross, n) >= 0) mb.AddTriangleWithNormal(p0, p1, p2, n);
                        else mb.AddTriangleWithNormal(p0, p2, p1, n);
                    }
        }

        /// <summary>
        /// 写入一个平面四边形（拆 2 三角），法线显式给定。
        /// 若顶点绕序与给定法线相反会自动纠正，避免因手写顶点顺序出错而出现"黑面"。
        /// </summary>
        private static void AddPlanarFace(MeshBuilder mb, Vec3 a, Vec3 b, Vec3 c, Vec3 d, Vec3 normal)
        {
            var n = normal.Normalized();
            var cross = Vec3.Cross(b - a, c - a);
            if (Vec3.Dot(cross, n) >= 0)
            {
                mb.AddTriangleWithNormal(a, b, c, n);
                mb.AddTriangleWithNormal(a, c, d, n);
            }
            else
            {
                mb.AddTriangleWithNormal(a, c, b, n);
                mb.AddTriangleWithNormal(a, d, c, n);
            }
        }
    }
}
