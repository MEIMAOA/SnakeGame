using System;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Gameplay;
using UnityEngine;

namespace ArknightsTD.View
{
    /// <summary>
    /// 静态战场构建：把 <see cref="BattleSim.Grid"/> 渲染成明日方舟风格的地形。
    /// <para>
    /// 视觉要点（对齐明日方舟）：
    /// <list type="bullet">
    /// <item>可部署高台：双色棋盘格 + 内嵌边框（高台感），格间留凹槽</item>
    /// <item>敌方行进路径：更暗的石板 + 中间虚线引导</item>
    /// <item>细网格线：只有部署时才该出现的参考线，这里做得很轻，不抢戏</item>
    /// <item>入口/终点：青色与红色地贴箭头 + 光圈</item>
    /// <item>障碍：不规则岩体；外围：错落钢墙 + 深色地板底</item>
    /// </list>
    /// </para>
    /// <para>
    /// 性能：同类地形合并成少数网格（地面/路径/网格/装饰/障碍/墙体各一个），
    /// 单关静态三角面约 1 万，DrawCall 很少。
    /// </para>
    /// </summary>
    public sealed class BattlefieldBuilder
    {
        /// <summary>高台顶面高度。</summary>
        public const double GroundTop = 0.06;
        /// <summary>路径板顶面高度（略低，形成下沉的沟槽感）。</summary>
        public const double PathTop = 0.05;
        /// <summary>贴地装饰层高度，必须高于 GroundTop/PathTop，避免 z-fighting。</summary>
        public const double DecalHeight = 0.075;

        // ---- 明日方舟配色 ----
        private static readonly Rgb FloorBase = Rgb.FromHex(0x0C1116);   // 战场外底
        private static readonly Rgb TileA = Rgb.FromHex(0x27313F);       // 高台棋盘 A
        private static readonly Rgb TileB = Rgb.FromHex(0x2E3948);       // 高台棋盘 B
        private static readonly Rgb TileRim = Rgb.FromHex(0x17202A);     // 高台凹槽/边框
        private static readonly Rgb PathA = Rgb.FromHex(0x141B23);       // 路径 A
        private static readonly Rgb PathB = Rgb.FromHex(0x18202A);       // 路径 B
        private static readonly Rgb GridLine = Rgb.FromHex(0x44556A);    // 网格线
        private static readonly Rgb Rock = Rgb.FromHex(0x3A454F);        // 岩体
        private static readonly Rgb RockTop = Rgb.FromHex(0x4A5763);     // 岩体顶
        private static readonly Rgb WallBody = Rgb.FromHex(0x212932);    // 外墙
        private static readonly Rgb WallTop = Rgb.FromHex(0x39454F);     // 外墙顶
        private static readonly Rgb EntryCyan = Rgb.FromHex(0x3FD8E8);   // 入口
        private static readonly Rgb ExitRed = Rgb.FromHex(0xD83A3A);     // 终点
        private static readonly Rgb GoalGold = Rgb.FromHex(0xE8B23C);    // 终点光圈

        public GameObject Root { get; private set; }

        public int GroundTriangles { get; private set; }
        public int PathTriangles { get; private set; }
        public int ObstacleTriangles { get; private set; }
        public int WallTriangles { get; private set; }
        public int DecoBracesTriangles { get; private set; }

        /// <summary>构建整个静态战场。</summary>
        public void Build(BattleSim sim, Transform parent = null)
        {
            if (sim == null) return;

            Root = new GameObject("Battlefield");
            if (parent != null) Root.transform.SetParent(parent, false);

            var floor = new MeshBuilder();     // 场外深色底板
            var ground = new MeshBuilder();    // 可部署高台
            var path = new MeshBuilder();      // 路径石板
            var grid = new MeshBuilder();      // 网格线
            var deco = new MeshBuilder();      // 入口/终点标记
            var obstacle = new MeshBuilder();  // 岩体
            var wall = new MeshBuilder();      // 外围钢墙

            // ---------- 场外底板 ----------
            floor.FaceTint = ArkPalette.FaceShader(FloorBase);
            Primitives.GroundQuad(floor, -1.6, -1.6, sim.GridW + 1.6, sim.GridH + 1.6, -0.02);

            for (int c = 0; c < sim.GridW; c++)
                for (int r = 0; r < sim.GridH; r++)
                {
                    var cell = sim.Grid[c, r];

                    if (cell == BattleSim.CellType.Obstacle)
                    {
                        // 岩体：不规则棱柱 + 偏心尖顶，顶面略亮
                        obstacle.FaceTint = ArkPalette.FaceShader(Rock);
                        double rot = c * 0.41 + r * 0.27;
                        Primitives.Frustum(obstacle, new Vec3(c + 0.5, 0.02, r + 0.5),
                            0.46, 0.46, 0.32, 0.32, 0.40, 7, rot);
                        obstacle.FaceTint = ArkPalette.GradientFaceShader(RockTop);
                        Primitives.Pyramid(obstacle, c + 0.5, r + 0.5, 0.44, 0.40, 0.66);
                        continue;
                    }

                    bool isPath = cell == BattleSim.CellType.Path;
                    bool checker = ((c + r) & 1) == 0;

                    if (isPath)
                    {
                        // 路径石板：双色棋盘 + 略低高度（下沉感）
                        path.FaceTint = ArkPalette.FaceShader(checker ? PathA : PathB);
                        Primitives.BoxOnGround(path, c + 0.5, r + 0.5, 0.984, PathTop, 0.984);
                    }
                    else
                    {
                        // 高台：先铺一层凹槽底（更暗、略大），再叠棋盘砖（内缩）→ 形成边框
                        ground.FaceTint = ArkPalette.FaceShader(TileRim);
                        Primitives.BoxOnGround(ground, c + 0.5, r + 0.5, 0.998, GroundTop - 0.012, 0.998);
                        ground.FaceTint = ArkPalette.FaceShader(checker ? TileA : TileB);
                        Primitives.BoxOnGround(ground, c + 0.5, r + 0.5, 0.90, GroundTop, 0.90);
                    }
                }

            // ---------- 轻度网格线（只覆盖可部署高台，明日方舟部署时的参考线） ----------
            grid.FaceTint = ArkPalette.FaceShader(GridLine);
            const double lineW = 0.02;
            for (int c = 0; c < sim.GridW; c++)
                for (int r = 0; r < sim.GridH; r++)
                {
                    if (sim.Grid[c, r] != BattleSim.CellType.Deployable) continue;
                    Primitives.GroundQuad(grid, c, r + 0.5 - lineW, c + 1, r + 0.5 + lineW, DecalHeight);
                    Primitives.GroundQuad(grid, c + 0.5 - lineW, r, c + 0.5 + lineW, r + 1, DecalHeight);
                }

            // ---------- 入口 / 终点地贴 ----------
            BuildEntryExit(sim, deco);

            // ---------- 外围钢墙：错落高度 + 顶部亮边 ----------
            for (int c = -1; c <= sim.GridW; c++)
            {
                double h1 = 0.62 + 0.05 * (((c + 100) * 7) % 5);
                double h2 = 0.58 + 0.06 * (((c + 100) * 3) % 4);
                wall.FaceTint = ArkPalette.FaceShader(WallBody);
                Primitives.BeveledBox(wall, new Vec3(c + 0.5, h1 * 0.5, -0.5),
                    new Vec3(0.46, h1 * 0.5, 0.46), 0.10);
                Primitives.BeveledBox(wall, new Vec3(c + 0.5, h2 * 0.5, sim.GridH + 0.5),
                    new Vec3(0.46, h2 * 0.5, 0.46), 0.10);
                wall.FaceTint = ArkPalette.FaceShader(WallTop);
                Primitives.BoxOnGround(wall, c + 0.5, -0.5, 0.80, h1 + 0.008, 0.80);
                Primitives.BoxOnGround(wall, c + 0.5, sim.GridH + 0.5, 0.80, h2 + 0.008, 0.80);
            }
            for (int r = -1; r <= sim.GridH; r++)
            {
                double h1 = 0.60 + 0.05 * (((r + 100) * 5) % 4);
                double h2 = 0.64 + 0.04 * (((r + 100) * 9) % 5);
                wall.FaceTint = ArkPalette.FaceShader(WallBody);
                Primitives.BeveledBox(wall, new Vec3(-0.5, h1 * 0.5, r + 0.5),
                    new Vec3(0.46, h1 * 0.5, 0.46), 0.10);
                Primitives.BeveledBox(wall, new Vec3(sim.GridW + 0.5, h2 * 0.5, r + 0.5),
                    new Vec3(0.46, h2 * 0.5, 0.46), 0.10);
                wall.FaceTint = ArkPalette.FaceShader(WallTop);
                Primitives.BoxOnGround(wall, -0.5, r + 0.5, 0.80, h1 + 0.008, 0.80);
                Primitives.BoxOnGround(wall, sim.GridW + 0.5, r + 0.5, 0.80, h2 + 0.008, 0.80);
            }

            GroundTriangles = ground.TriangleCount;
            PathTriangles = path.TriangleCount + grid.TriangleCount;
            ObstacleTriangles = obstacle.TriangleCount;
            WallTriangles = wall.TriangleCount;
            DecoBracesTriangles = deco.TriangleCount + floor.TriangleCount;

            var solid = TdMaterials.Solid();
            TdMaterials.CreateMeshObject("Floor", floor, solid, Root.transform);
            TdMaterials.CreateMeshObject("Ground", ground, solid, Root.transform);
            TdMaterials.CreateMeshObject("Path", path, solid, Root.transform);
            TdMaterials.CreateMeshObject("GridLines", grid, solid, Root.transform);
            TdMaterials.CreateMeshObject("EntryExit", deco, TdMaterials.Solid(), Root.transform);
            TdMaterials.CreateMeshObject("Obstacles", obstacle, solid, Root.transform);
            TdMaterials.CreateMeshObject("Walls", wall, solid, Root.transform);
        }

        /// <summary>入口青色箭头 + 终点红色箭头与金色光圈（明日方舟的“蓝门/红门”）。</summary>
        private static void BuildEntryExit(BattleSim sim, MeshBuilder deco)
        {
            double sx, sy, ex, ey;
            double sdx = 1, sdy = 0, edx = 1, edy = 0;

            if (sim.PathPointCount >= 2 && sim.TryGetPathPoint(0, out sx, out sy))
            {
                double nx, ny;
                if (sim.TryGetPathPoint(1, out nx, out ny))
                {
                    double l = System.Math.Sqrt((nx - sx) * (nx - sx) + (ny - sy) * (ny - sy));
                    if (l > 1e-6) { sdx = (nx - sx) / l; sdy = (ny - sy) / l; }
                }

                // 入口：青色三角箭头 + 外圈
                deco.FaceTint = ArkPalette.FaceShader(EntryCyan);
                ArrowOnGround(deco, sx, sy, sdx, sdy, 0.34, 0.18, DecalHeight + 0.004);
                Primitives.GroundRing(deco, sx, sy, 0.42, 0.48, DecalHeight + 0.002);
                // 起点标记条
                Primitives.GroundQuad(deco, sx - 0.5 + 0.06, sy - 0.5, sx - 0.5 + 0.16, sy + 0.5, DecalHeight + 0.002);

                if (sim.TryGetPathPoint(sim.PathPointCount - 1, out ex, out ey))
                {
                    if (sim.PathPointCount >= 3)
                    {
                        double px, py;
                        sim.TryGetPathPoint(sim.PathPointCount - 2, out px, out py);
                        double l = System.Math.Sqrt((ex - px) * (ex - px) + (ey - py) * (ey - py));
                        if (l > 1e-6) { edx = (ex - px) / l; edy = (ey - py) / l; }
                    }
                    // 终点：红色箭头 + 金色光圈
                    deco.FaceTint = ArkPalette.FaceShader(ExitRed);
                    ArrowOnGround(deco, ex, ey, edx, edy, 0.36, 0.20, DecalHeight + 0.006);
                    deco.FaceTint = ArkPalette.FaceShader(GoalGold);
                    Primitives.GroundRing(deco, ex, ey, 0.52, 0.60, DecalHeight + 0.003);
                    Primitives.GroundQuad(deco, ex - 0.5 + 0.06, ey - 0.5, ex - 0.5 + 0.16, ey + 0.5, DecalHeight + 0.003);
                }
            }
        }

        /// <summary>在地面上画一个指向 (dx,dy) 的箭头（三角 + 尾巴），法线强制朝上。</summary>
        private static void ArrowOnGround(MeshBuilder mb, double cx, double cy,
            double dx, double dy, double len, double halfW, double y)
        {
            double px = -dy, py = dx;   // 垂直方向
            var up = new Vec3(0, 1, 0);

            double tipX = cx + dx * len, tipY = cy + dy * len;
            double baseX = cx - dx * len * 0.35, baseY = cy - dy * len * 0.35;

            // 三角箭头
            mb.AddTriangleWithNormal(
                new Vec3(tipX, y, tipY),
                new Vec3(baseX + px * halfW, y, baseY + py * halfW),
                new Vec3(baseX - px * halfW, y, baseY - py * halfW), up);

            // 尾巴（窄条四边形）
            double tx0 = cx - dx * len * 0.35 + px * halfW * 0.30;
            double ty0 = cy - dy * len * 0.35 + py * halfW * 0.30;
            double tx1 = cx - dx * len * 1.15 + px * halfW * 0.30;
            double ty1 = cy - dy * len * 1.15 + py * halfW * 0.30;
            double tx2 = cx - dx * len * 1.15 - px * halfW * 0.30;
            double ty2 = cy - dy * len * 1.15 - py * halfW * 0.30;
            double tx3 = cx - dx * len * 0.35 - px * halfW * 0.30;
            double ty3 = cy - dy * len * 0.35 - py * halfW * 0.30;
            mb.AddQuad(new Vec3(tx0, y, ty0), new Vec3(tx1, y, ty1),
                       new Vec3(tx2, y, ty2), new Vec3(tx3, y, ty3), up);
        }

        // ==================================================================
        //  敌人动线预览（明日方舟：开始行动前显示进攻路线）
        // ==================================================================

        private GameObject _routeGo;

        /// <summary>
        /// 构建敌人动线：沿路径每隔一定距离画一个青色箭头（朝向行军方向），
        /// 起点/终点各加一个光圈。默认隐藏，由控制器在准备阶段打开。
        /// </summary>
        public void BuildRoutePreview(BattleSim sim)
        {
            if (sim == null || Root == null) return;
            if (_routeGo != null) return;

            var mb = new MeshBuilder();
            mb.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0x7FE7FF));

            double y = DecalHeight + 0.020;
            double total = Math.Max(1e-6, sim.PathLength);

            // 沿路径采样箭头：间距 1.15 格，尺寸随采样密度调整
            const double spacing = 1.15;
            int steps = Math.Max(2, (int)(total / spacing));
            var up = new Vec3(0, 1, 0);

            for (int i = 0; i < steps; i++)
            {
                double t = (i + 0.5) / steps;
                if (t <= 0.02 || t >= 0.98) continue;

                sim.PositionAt(t, out double px, out double py);
                sim.DirectionAt(t, out double dx, out double dy);
                double len = 0.34, halfW = 0.19;

                double tipX = px + dx * len, tipY = py + dy * len;
                double baseX = px - dx * len * 0.35, baseY = py - dy * len * 0.35;
                double perpX = -dy, perpY = dx;

                mb.AddTriangleWithNormal(
                    new Vec3(tipX, y, tipY),
                    new Vec3(baseX + perpX * halfW, y, baseY + perpY * halfW),
                    new Vec3(baseX - perpX * halfW, y, baseY - perpY * halfW), up);
            }

            // 终点光圈
            sim.PositionAt(1.0, out double ex, out double ey);
            mb.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0xFF7A7A));
            Primitives.GroundRing(mb, ex, ey, 0.42, 0.52, y);

            // 起点光圈
            sim.PositionAt(0.0, out double sx, out double sy);
            mb.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0x7FE7FF));
            Primitives.GroundRing(mb, sx, sy, 0.42, 0.52, y);

            var mat = TdMaterials.Transparent(Rgb.FromHex(0x7FE7FF), 0.62f);
            _routeGo = TdMaterials.CreateMeshObject("RoutePreview", mb, mat, Root.transform);
            if (_routeGo != null) _routeGo.SetActive(false);
        }

        /// <summary>动线预览是否已构建（验证用）。</summary>
        public bool HasRoutePreview => _routeGo != null;

        /// <summary>显示/隐藏动线预览。</summary>
        public void SetRouteVisible(bool visible)
        {
            if (_routeGo != null && _routeGo.activeSelf != visible) _routeGo.SetActive(visible);
        }

        /// <summary>总静态三角面数（性能自检用）。</summary>
        public int TotalTriangles
            => GroundTriangles + PathTriangles + ObstacleTriangles + WallTriangles + DecoBracesTriangles;
    }
}
