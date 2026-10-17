using System;
using System.Collections.Generic;

namespace ArknightsTD.Core.Geometry
{
    /// <summary>轻量 3D 向量，保持几何层不依赖引擎，便于脱离编辑器实测。</summary>
    public struct Vec3
    {
        public double X, Y, Z;
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(double s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vec3 Normalized()
        {
            double l = Length;
            if (l < 1e-9) return new Vec3(0, 1, 0);
            return new Vec3(X / l, Y / l, Z / l);
        }

        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y,
                     a.Z * b.X - a.X * b.Z,
                     a.X * b.Y - a.Y * b.X);

        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public override string ToString() => $"({X:0.###},{Y:0.###},{Z:0.###})";
    }

    /// <summary>
    /// 程序化网格累加器。
    /// <para>
    /// 关键设计：<b>顶点不共享</b>（每个三角面写 3 个独立顶点），因此每个面的法线严格属于该面，
    /// 天然得到硬边低多边形（flat shading）外观 —— 这正是明日方舟那种工业风建模的基础。
    /// 代价是顶点数 = 三角数 × 3，对塔防这种小规模场景完全可接受。
    /// </para>
    /// <para>
    /// 逐面颜色通过 <see cref="FaceTint"/> 回调记录为"每面一个颜色"，
    /// 由 Unity 适配层写进顶点色（见 MeshFactory），从而在<b>单个网格、单个材质</b>下
    /// 获得逐面配色，不需要拆分 DrawCall，也不需要烘焙贴图。
    /// </para>
    /// </summary>
    public class MeshBuilder
    {
        private readonly List<Vec3> _pos = new List<Vec3>(256);
        private readonly List<Vec3> _nrm = new List<Vec3>(256);
        private readonly List<(double u, double v)> _uv = new List<(double, double)>(256);
        private readonly List<int> _tris = new List<int>(384);
        /// <summary>每个三角面一个颜色索引（指向 <see cref="FaceColors"/>）。</summary>
        private readonly List<int> _faceColorIdx = new List<int>(128);
        private readonly List<Rgb> _faceColors = new List<Rgb>(8);
        private readonly Dictionary<Rgb, int> _colorLookup = new Dictionary<Rgb, int>();

        public int VertexCount => _pos.Count;
        public int TriangleCount => _tris.Count / 3;
        /// <summary>累计写入的三角面数，用于自检（应恒等于 <see cref="TriangleCount"/>）。</summary>
        public int EmittedTriangles { get; private set; }
        public bool IsEmpty => _tris.Count == 0;

        public IReadOnlyList<Vec3> Positions => _pos;
        public IReadOnlyList<Vec3> Normals => _nrm;
        public IReadOnlyList<(double u, double v)> Uvs => _uv;
        public IReadOnlyList<int> Triangles => _tris;
        public IReadOnlyList<Rgb> FaceColors => _faceColors;
        /// <summary>第 i 个三角面使用的颜色。</summary>
        public Rgb TriangleColor(int triIndex) => _faceColors[_faceColorIdx[triIndex]];

        /// <summary>
        /// 逐面着色回调：(面内 UV, 朝向码) → 颜色。
        /// 朝向码：0=顶面 1=底面 2=+X 3=-X 4=+Z 5=-Z。
        /// 若为 null 则使用 <see cref="DefaultColor"/> 单色。
        /// </summary>
        public Func<double, double, int, Rgb> FaceTint;

        /// <summary>未设置 FaceTint 时使用的单色。</summary>
        public Rgb DefaultColor = Rgb.FromHex(0x9AA4B2);

        public void Clear()
        {
            _pos.Clear(); _nrm.Clear(); _uv.Clear(); _tris.Clear();
            _faceColorIdx.Clear(); _faceColors.Clear(); _colorLookup.Clear();
        }

        private int ColorIndex(Rgb c)
        {
            if (_colorLookup.TryGetValue(c, out int i)) return i;
            i = _faceColors.Count;
            _faceColors.Add(c);
            _colorLookup[c] = i;
            return i;
        }

        /// <summary>按逆时针顺序添加一个三角面，法线自动计算。</summary>
        public virtual void AddTriangle(Vec3 a, Vec3 b, Vec3 c)
            => AddTriangleWithNormal(a, b, c, Vec3.Cross(b - a, c - a).Normalized());

        /// <summary>添加四边形（a→b→c→d 逆时针），拆成两个三角面，法线自动计算。</summary>
        public virtual void AddQuad(Vec3 a, Vec3 b, Vec3 c, Vec3 d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        /// <summary>按指定公共法线添加四边形（用于朝向已知的面，避免依赖绕序推导）。</summary>
        public virtual void AddQuad(Vec3 a, Vec3 b, Vec3 c, Vec3 d, Vec3 normalHint)
        {
            var n = normalHint.Normalized();
            AddTriangleWithNormal(a, b, c, n);
            AddTriangleWithNormal(a, c, d, n);
        }

        /// <summary>添加一个三角面并显式指定三个顶点的法线。</summary>
        public void AddTriangleWithNormal(Vec3 a, Vec3 b, Vec3 c, Vec3 n)
        {
            EmittedTriangles++;
            int i0 = _pos.Count;
            _pos.Add(a); _pos.Add(b); _pos.Add(c);
            _nrm.Add(n); _nrm.Add(n); _nrm.Add(n);

            ComputeUv(a, b, c, n, out var ua, out var ub, out var uc);
            _uv.Add(ua); _uv.Add(ub); _uv.Add(uc);

            _tris.Add(i0); _tris.Add(i0 + 1); _tris.Add(i0 + 2);

            Rgb col = DefaultColor;
            if (FaceTint != null)
            {
                int kind = FaceKind(n);
                double mu = (ua.u + ub.u + uc.u) / 3.0;
                double mv = (ua.v + ub.v + uc.v) / 3.0;
                try { col = FaceTint(mu, mv, kind); } catch { col = DefaultColor; }
            }
            _faceColorIdx.Add(ColorIndex(col));
        }

        /// <summary>由法线推断朝向码。</summary>
        public static int FaceKind(Vec3 normal)
        {
            double ax = Math.Abs(normal.X), ay = Math.Abs(normal.Y), az = Math.Abs(normal.Z);
            if (ay >= ax && ay >= az) return normal.Y >= 0 ? 0 : 1;
            if (ax >= az) return normal.X >= 0 ? 2 : 3;
            return normal.Z >= 0 ? 4 : 5;
        }

        /// <summary>
        /// 把一个三角面投影到与法线垂直的主轴平面上并归一化到 [0,1]，
        /// 使多面体的每个面都占用完整纹理空间（供程序化贴图/渐变使用）。
        /// </summary>
        private static void ComputeUv(Vec3 a, Vec3 b, Vec3 c, Vec3 n,
            out (double u, double v) ua, out (double u, double v) ub, out (double u, double v) uc)
        {
            double ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
            if (ay >= ax && ay >= az)
            {
                ua = (a.X, a.Z); ub = (b.X, b.Z); uc = (c.X, c.Z);
            }
            else if (ax >= az)
            {
                ua = (a.Z, a.Y); ub = (b.Z, b.Y); uc = (c.Z, c.Y);
            }
            else
            {
                ua = (a.X, a.Y); ub = (b.X, b.Y); uc = (c.X, c.Y);
            }

            Normalize2(ref ua, ref ub, ref uc);
        }

        private static void Normalize2(ref (double u, double v) a, ref (double u, double v) b,
            ref (double u, double v) c)
        {
            double minU = Math.Min(a.u, Math.Min(b.u, c.u));
            double maxU = Math.Max(a.u, Math.Max(b.u, c.u));
            double minV = Math.Min(a.v, Math.Min(b.v, c.v));
            double maxV = Math.Max(a.v, Math.Max(b.v, c.v));
            double su = maxU - minU, sv = maxV - minV;
            if (su < 1e-9) su = 1;
            if (sv < 1e-9) sv = 1;
            a = ((a.u - minU) / su, (a.v - minV) / sv);
            b = ((b.u - minU) / su, (b.v - minV) / sv);
            c = ((c.u - minU) / su, (c.v - minV) / sv);
        }
    }
}
