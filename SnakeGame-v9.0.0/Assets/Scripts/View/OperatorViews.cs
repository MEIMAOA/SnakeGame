using System.Collections.Generic;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Data;
using ArknightsTD.Gameplay;
using UnityEngine;

namespace ArknightsTD.View
{
    /// <summary>
    /// 干员视图：同步 <see cref="BattleSim.Operators"/> 到场景，并绘制部署/朝向/选中/悬停等地面提示。
    /// <para>
    /// 明日方舟式表现：
    /// <list type="bullet">
    /// <item>干员脚下职业色圆环 + 正面朝向楔形指示</item>
    /// <item>部署预览：可部署=青绿、不可部署=红，同时预览攻击范围</item>
    /// <item>选朝向：四个方向箭头高亮</item>
    /// <item>选中已部署干员：范围常亮 + 方格描边</item>
    /// <item>待机轻微起伏、开火瞬间模型脉冲</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class OperatorViews
    {
        private readonly Transform _root;
        private readonly Dictionary<int, GameObject> _active = new Dictionary<int, GameObject>();

        /// <summary>每个干员上一次的冷却值，用于侦测"这一帧开火了"。</summary>
        private readonly Dictionary<int, double> _lastCooldown = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _fireFlash = new Dictionary<int, double>();
        private readonly List<int> _toRemove = new List<int>();

        // ---- 干员血条 / 技力条（世界空间，每帧重建）----
        private GameObject _barsGo;
        private MeshFilter _barsFilter;
        private Mesh _barsMesh;

        private readonly Overlay _deploy;
        private readonly Overlay _facing;
        private readonly Overlay _select;
        private readonly Overlay _hover;

        private readonly Material _okMat;
        private readonly Material _badMat;
        private readonly Material _rangeMat;
        private readonly Material _facingMat;
        private readonly Material _selectMat;
        private readonly Material _hoverMat;

        public OperatorViews(Transform parent)
        {
            var go = new GameObject("Operators");
            go.transform.SetParent(parent, false);
            _root = go.transform;

            _okMat = TdMaterials.Transparent(Rgb.FromHex(0x4FE0B0), 0.34f);
            _badMat = TdMaterials.Transparent(Rgb.FromHex(0xE4483F), 0.42f);
            _rangeMat = TdMaterials.Transparent(Rgb.FromHex(0x4FD8FF), 0.16f);
            _facingMat = TdMaterials.Transparent(Rgb.FromHex(0xF2C14E), 0.34f);
            _selectMat = TdMaterials.Transparent(Rgb.FromHex(0xF2C14E), 0.18f);
            _hoverMat = TdMaterials.Transparent(Rgb.FromHex(0xEAF6FF), 0.12f);

            _deploy = new Overlay("DeployPreview", parent, _okMat);
            _facing = new Overlay("FacingPreview", parent, _facingMat);
            _select = new Overlay("SelectionOverlay", parent, _selectMat);
            _hover = new Overlay("HoverOverlay", parent, _hoverMat);
        }

        public int VisibleCount => _active.Count;

        // ==================================================================
        //  同步
        // ==================================================================

        public void Sync(BattleSim sim)
        {
            if (sim == null) return;

            // 回收已撤下的
            _toRemove.Clear();
            foreach (var kv in _active)
            {
                bool alive = false;
                for (int i = 0; i < sim.Operators.Count; i++)
                    if (sim.Operators[i].Id == kv.Key) { alive = true; break; }
                if (!alive) _toRemove.Add(kv.Key);
            }
            for (int i = 0; i < _toRemove.Count; i++)
            {
                Object.Destroy(_active[_toRemove[i]]);
                _active.Remove(_toRemove[i]);
                _lastCooldown.Remove(_toRemove[i]);
                _fireFlash.Remove(_toRemove[i]);
            }

            SyncStatusBars(sim);

            // 新增 + 更新
            for (int i = 0; i < sim.Operators.Count; i++)
            {
                var op = sim.Operators[i];
                GameObject go;
                if (!_active.TryGetValue(op.Id, out go))
                {
                    var mb = BuildOperatorMesh(op.Kind);
                    go = TdMaterials.CreateMeshObject("Op_" + op.Kind + "_" + op.Id, mb, TdMaterials.Solid(), _root);
                    if (go == null) continue;
                    _active[op.Id] = go;
                    _lastCooldown[op.Id] = op.Cooldown;
                    _fireFlash[op.Id] = 0;
                }

                var t = go.transform;
                // 待机轻微起伏（明日方舟小人也有呼吸感）
                float bob = Mathf.Sin(Time.time * 2.4f + op.Id * 1.7f) * 0.015f;
                t.position = new Vector3(op.Col + 0.5f, TdMaterials.UnitBaseY + bob, op.Row + 0.5f);

                float yaw = op.Facing == Facing.Up ? 180f
                          : op.Facing == Facing.Left ? 270f
                          : op.Facing == Facing.Right ? 90f : 0f;
                t.rotation = Quaternion.Euler(0f, yaw, 0f);

                // 开火侦测：冷却被重置 = 刚刚开火 → 模型脉冲
                double last = _lastCooldown[op.Id];
                double flash = _fireFlash[op.Id];
                if (op.Cooldown > last + 1e-6) flash = 0.12;
                else if (flash > 0) flash -= Time.deltaTime;
                _lastCooldown[op.Id] = op.Cooldown;
                _fireFlash[op.Id] = Mathf.Max(0f, (float)flash);

                float k = 1f + (float)flash / 0.12f * 0.14f;
                t.localScale = new Vector3(k, k, k);
            }
        }

        /// <summary>
        /// 干员头顶状态条（明日方舟式）：
        /// 绿色血条 + 下方技力条（充满变金色并闪烁 = 技能就绪），技能生效时底座加金环。
        /// </summary>
        private void SyncStatusBars(BattleSim sim)
        {
            if (_barsGo == null)
            {
                _barsGo = new GameObject("OperatorBars");
                _barsGo.transform.SetParent(_root, false);
                _barsFilter = _barsGo.AddComponent<MeshFilter>();
                var mr = _barsGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = TdMaterials.Solid();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            var cam = Camera.main;
            if (cam == null || sim.Operators.Count == 0)
            {
                if (_barsGo.activeSelf) _barsGo.SetActive(false);
                return;
            }

            var right = cam.transform.right; right.y = 0f; right.Normalize();
            var up = Vector3.up;
            var mb = new MeshBuilder();
            float puls = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);

            for (int i = 0; i < sim.Operators.Count; i++)
            {
                var op = sim.Operators[i];
                float hpRatio = op.MaxHp > 0 ? Mathf.Clamp01((float)op.Hp / op.MaxHp) : 0f;
                float spRatio = (float)op.SpProgress;
                bool ready = op.SkillReady;
                bool active = op.SkillActive;

                double bx = op.Col + 0.5, bz = op.Row + 0.5;
                const float halfW = 0.30f, halfH = 0.028f;
                float yHp = 1.30f, ySp = 1.22f;

                // 血条
                mb.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0x0A0D12));
                BarQuad(mb, right, up, bx, bz, yHp, halfW, halfH, 1f, -halfW);
                mb.FaceTint = ArkPalette.FaceShader(
                    hpRatio < 0.3f ? Rgb.FromHex(0xE4483F) : Rgb.FromHex(0x54D08A));
                BarQuad(mb, right, up, bx, bz, yHp + 0.0004f, halfW - 0.004f, halfH - 0.004f, hpRatio, -halfW + 0.004f);

                // 技力条（就绪金色闪烁）
                mb.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0x0A0D12));
                BarQuad(mb, right, up, bx, bz, ySp, halfW, halfH * 0.75f, 1f, -halfW);
                var spColor = ready
                    ? Rgb.FromHex(0xF2C14E)
                    : (active ? Rgb.FromHex(0xFFE08A) : Rgb.FromHex(0x35B6E8));
                mb.FaceTint = ArkPalette.FaceShader(spColor);
                float spFill = ready ? 1f : spRatio;
                BarQuad(mb, right, up, bx, bz, ySp + 0.0004f, halfW - 0.004f, halfH * 0.75f - 0.003f, spFill, -halfW + 0.004f);

                // 技能生效：底座金环
                if (active)
                {
                    mb.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0xF2C14E));
                    Primitives.GroundRing(mb, bx, bz, 0.40f + 0.05f * puls, 0.46f + 0.05f * puls,
                        BattlefieldBuilder.DecalHeight + 0.004);
                }
            }

            if (_barsMesh != null) Object.Destroy(_barsMesh);
            _barsMesh = TdMaterials.BuildMesh(mb, "OperatorBars");
            _barsFilter.sharedMesh = _barsMesh;
            if (!_barsGo.activeSelf) _barsGo.SetActive(true);
        }

        /// <summary>面向相机的矩形条（双绕序，避免背面剔除）。</summary>
        private static void BarQuad(MeshBuilder mb, Vector3 right, Vector3 up,
            double bx, double bz, float y, float halfW, float halfH, float ratio, float leftOffset)
        {
            if (ratio <= 0f) return;
            float w = halfW * 2f * ratio;
            Vector3 c = new Vector3((float)bx, y, (float)bz);
            Vector3 a = c + right * leftOffset + up * (-halfH);
            Vector3 b = c + right * (leftOffset + w) + up * (-halfH);
            Vector3 cc = c + right * (leftOffset + w) + up * halfH;
            Vector3 d = c + right * leftOffset + up * halfH;
            mb.AddQuad(new Vec3(a.x, a.y, a.z), new Vec3(b.x, b.y, b.z),
                       new Vec3(cc.x, cc.y, cc.z), new Vec3(d.x, d.y, d.z), new Vec3(0, 1, 0));
            mb.AddQuad(new Vec3(a.x, a.y, a.z), new Vec3(d.x, d.y, d.z),
                       new Vec3(cc.x, cc.y, cc.z), new Vec3(b.x, b.y, b.z), new Vec3(0, -1, 0));
        }

        // ==================================================================
        //  地面提示
        // ==================================================================

        /// <summary>部署预览：目标格 + 攻击范围；可部署青绿、不可部署红。</summary>
        public void DrawDeployPreview(BattleSim sim, OperatorKind kind, int hoverCol, int hoverRow)
        {
            if (!ValidCell(sim, hoverCol, hoverRow)) { _deploy.Hide(); return; }

            bool canDeploy = sim.CanDeployAt(hoverCol, hoverRow);
            _deploy.SetMaterial(canDeploy ? _okMat : _badMat);

            var mb = new MeshBuilder();
            double top = BattlefieldBuilder.DecalHeight;

            // 目标格（比格线略高，避免闪面）
            FillCell(mb, hoverCol, hoverRow, top + 0.004);

            // 攻击范围（默认朝向；选朝向阶段另有箭头提示）
            var cells = WaveDirector.RangeCells(kind, Facing.Down);
            for (int i = 0; i < cells.Count; i++)
            {
                int cc = hoverCol + cells[i].dc;
                int rr = hoverRow + cells[i].dr;
                if (!ValidCell(sim, cc, rr)) continue;
                FillCell(mb, cc, rr, top + 0.006, 0.16);
            }

            _deploy.Show(mb, "DeployPreviewMesh");
        }

        /// <summary>选朝向阶段：四方向箭头 + 待部署格高亮。</summary>
        public void DrawFacingPreview(BattleSim sim, int col, int row)
        {
            if (!ValidCell(sim, col, row)) { _facing.Hide(); return; }

            var mb = new MeshBuilder();
            double top = BattlefieldBuilder.DecalHeight + 0.010;
            FillCell(mb, col, row, top);

            var up = new Vec3(0, 1, 0);
            const double len = 0.62, halfW = 0.22;
            // 四个方向的箭头（中心 → 外）
            ArrowQuad(mb, col + 0.5, row + 0.5, 0, 1, len, halfW, top, up);
            ArrowQuad(mb, col + 0.5, row + 0.5, 0, -1, len, halfW, top, up);
            ArrowQuad(mb, col + 0.5, row + 0.5, -1, 0, len, halfW, top, up);
            ArrowQuad(mb, col + 0.5, row + 0.5, 1, 0, len, halfW, top, up);

            _facing.Show(mb, "FacingPreviewMesh");
        }

        /// <summary>选中已部署干员：其攻击范围常亮 + 所在格描边。</summary>
        public void DrawSelection(BattleSim sim, OperatorUnit op)
        {
            if (sim == null || op == null) { _select.Hide(); return; }

            _select.SetMaterial(_selectMat);
            var mb = new MeshBuilder();
            double top = BattlefieldBuilder.DecalHeight + 0.008;
            FillCell(mb, op.Col, op.Row, top);

            var cells = WaveDirector.RangeCells(op.Kind, op.Facing);
            for (int i = 0; i < cells.Count; i++)
            {
                int cc = op.Col + cells[i].dc;
                int rr = op.Row + cells[i].dr;
                if (!ValidCell(sim, cc, rr)) continue;
                FillCell(mb, cc, rr, top + 0.001, 0.12);
            }
            _select.Show(mb, "SelectionMesh");
        }

        /// <summary>空闲状态下的鼠标悬停光标（四角括号）。</summary>
        public void DrawHover(BattleSim sim, int col, int row)
        {
            if (!ValidCell(sim, col, row)) { _hover.Hide(); return; }
            var mb = new MeshBuilder();
            double top = BattlefieldBuilder.DecalHeight + 0.012;
            double a = 0.10, l = 0.28;
            double x0 = col + a, x1 = col + 1 - a, z0 = row + a, z1 = row + 1 - a;
            const double t = 0.035;
            // 四角 L 形
            Corner(mb, x0, z0, 1, 1, l, t, top);
            Corner(mb, x1, z0, -1, 1, l, t, top);
            Corner(mb, x0, z1, 1, -1, l, t, top);
            Corner(mb, x1, z1, -1, -1, l, t, top);
            _hover.Show(mb, "HoverMesh");
        }

        public void HideOverlays()
        {
            _deploy.Hide();
            _facing.Hide();
            _select.Hide();
            _hover.Hide();
        }

        public void HidePreview() => HideOverlays();

        // ==================================================================
        //  提示绘制工具
        // ==================================================================

        private static bool ValidCell(BattleSim sim, int col, int row)
            => sim != null && col >= 0 && row >= 0 && col < sim.GridW && row < sim.GridH;

        private static void FillCell(MeshBuilder mb, int col, int row, double y, double inset = 0.04)
            => Primitives.GroundQuad(mb, col + inset, row + inset,
                col + 1 - inset, row + 1 - inset, y);

        private static void Corner(MeshBuilder mb, double x, double z, int dx, int dz,
            double len, double t, double y)
        {
            var up = new Vec3(0, 1, 0);
            mb.AddQuad(new Vec3(x, y, z), new Vec3(x + dx * len, y, z),
                       new Vec3(x + dx * len, y, z + dz * t), new Vec3(x, y, z + dz * t), up);
            mb.AddQuad(new Vec3(x, y, z), new Vec3(x, y, z + dz * len),
                       new Vec3(x + dx * t, y, z + dz * len), new Vec3(x + dx * t, y, z), up);
        }

        private static void ArrowQuad(MeshBuilder mb, double cx, double cz,
            double dx, double dz, double len, double halfW, double y, Vec3 up)
        {
            double px = -dz, pz = dx;
            double tipX = cx + dx * len, tipZ = cz + dz * len;
            double baseX = cx - dx * len * 0.25, baseZ = cz - dz * len * 0.25;
            mb.AddTriangleWithNormal(
                new Vec3(tipX, y, tipZ),
                new Vec3(baseX + px * halfW, y, baseZ + pz * halfW),
                new Vec3(baseX - px * halfW, y, baseZ - pz * halfW), up);
        }

        // ==================================================================
        //  干员模型
        // ==================================================================

        /// <summary>按职业程序化生成干员模型：职业色底座环 + 身体剪影 + 朝向楔形。</summary>
        public static MeshBuilder BuildOperatorMesh(OperatorKind kind)
        {
            var color = ArkPalette.OperatorColor(kind);
            var dark = ArkPalette.Darken(color, 0.45);
            var light = ArkPalette.Lighten(color, 0.35);
            var mb = new MeshBuilder();

            // 底座圆环（职业色）
            mb.FaceTint = ArkPalette.FaceShader(dark);
            Primitives.Frustum(mb, new Vec3(0, 0.004, 0), 0.36, 0.36, 0.30, 0.30, 0.05, 8);
            mb.FaceTint = ArkPalette.FaceShader(light);
            Primitives.Frustum(mb, new Vec3(0, 0.02, 0), 0.28, 0.28, 0.26, 0.26, 0.045, 8);

            // 朝向楔形（+Z 前方，锚白/金）
            mb.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0xF2C14E));
            mb.AddTriangleWithNormal(
                new Vec3(0, 0.085, 0.58),
                new Vec3(-0.11, 0.085, 0.30),
                new Vec3(0.11, 0.085, 0.30), new Vec3(0, 1, 0));

            // 身体（职业剪影区分）
            mb.FaceTint = ArkPalette.FaceShader(color);
            switch (kind)
            {
                case OperatorKind.Guard:
                    // 近卫：厚甲 + 肩甲 + 大刃
                    Primitives.Frustum(mb, new Vec3(0, 0.06, 0), 0.26, 0.26, 0.18, 0.18, 0.50, 6);
                    Primitives.Box(mb, new Vec3(0, 0.70, 0), new Vec3(0.20, 0.17, 0.15));
                    mb.FaceTint = ArkPalette.FaceShader(light);
                    Primitives.Box(mb, new Vec3(0.26, 0.76, 0), new Vec3(0.10, 0.13, 0.13));
                    Primitives.Box(mb, new Vec3(-0.26, 0.76, 0), new Vec3(0.10, 0.13, 0.13));
                    mb.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0xE9EEF4));
                    Primitives.Box(mb, new Vec3(0, 0.88, 0.36), new Vec3(0.05, 0.44, 0.09));
                    break;

                case OperatorKind.Sniper:
                    // 狙击：细高 + 长枪管
                    Primitives.Frustum(mb, new Vec3(0, 0.06, 0), 0.22, 0.22, 0.15, 0.15, 0.62, 6);
                    Primitives.Box(mb, new Vec3(0, 0.86, 0), new Vec3(0.16, 0.15, 0.14));
                    mb.FaceTint = ArkPalette.FaceShader(light);
                    Primitives.Box(mb, new Vec3(0, 0.66, 0.34), new Vec3(0.045, 0.045, 0.36));
                    break;

                case OperatorKind.Caster:
                    // 术师：飘逸下摆 + 悬浮核心
                    Primitives.Frustum(mb, new Vec3(0, 0.06, 0), 0.30, 0.30, 0.16, 0.16, 0.34, 6, 0.5);
                    Primitives.Frustum(mb, new Vec3(0, 0.32, 0), 0.17, 0.17, 0.13, 0.13, 0.46, 6);
                    mb.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0xD9B3FF));
                    Primitives.Frustum(mb, new Vec3(0, 1.06, 0), 0.13, 0.13, 0.04, 0.04, 0.20, 6);
                    break;
            }

            return mb;
        }

        /// <summary>可复用的地面提示网格对象。</summary>
        private sealed class Overlay
        {
            private readonly GameObject _go;
            private readonly MeshFilter _mf;
            private readonly MeshRenderer _mr;
            private Mesh _mesh;

            public Overlay(string name, Transform parent, Material mat)
            {
                _go = new GameObject(name);
                if (parent != null) _go.transform.SetParent(parent, false);
                _mf = _go.AddComponent<MeshFilter>();
                _mr = _go.AddComponent<MeshRenderer>();
                _mr.sharedMaterial = mat;
                _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _mr.receiveShadows = false;
                _go.SetActive(false);
            }

            public void SetMaterial(Material m)
            {
                if (m != null && _mr != null) _mr.sharedMaterial = m;
            }

            public void Show(MeshBuilder mb, string name)
            {
                if (mb == null || mb.IsEmpty) { Hide(); return; }
                if (_mesh != null) Object.Destroy(_mesh);
                _mesh = TdMaterials.BuildMesh(mb, name);
                _mf.sharedMesh = _mesh;
                if (!_go.activeSelf) _go.SetActive(true);
            }

            public void Hide()
            {
                if (_go != null && _go.activeSelf) _go.SetActive(false);
            }
        }
    }

    /// <summary>弹丸视图：数量少、生命周期短，直接创建销毁。</summary>
    public sealed class ProjectileViews
    {
        private readonly Transform _root;
        private readonly Dictionary<int, GameObject> _active = new Dictionary<int, GameObject>();
        private Material _mat;

        public ProjectileViews(Transform parent)
        {
            var go = new GameObject("Projectiles");
            go.transform.SetParent(parent, false);
            _root = go.transform;
            _mat = TdMaterials.Emissive(ArkPalette.HotWhite, 1.2f);
        }

        public int VisibleCount => _active.Count;

        public void Sync(BattleSim sim)
        {
            if (sim == null) return;

            _gone.Clear();
            foreach (var kv in _active)
            {
                bool alive = false;
                for (int i = 0; i < sim.Projectiles.Count; i++)
                    if (sim.Projectiles[i].Id == kv.Key) { alive = true; break; }
                if (!alive) _gone.Add(kv.Key);
            }
            for (int i = 0; i < _gone.Count; i++)
            {
                Object.Destroy(_active[_gone[i]]);
                _active.Remove(_gone[i]);
            }

            for (int i = 0; i < sim.Projectiles.Count; i++)
            {
                var p = sim.Projectiles[i];
                GameObject go;
                if (!_active.TryGetValue(p.Id, out go))
                {
                    var mb = new MeshBuilder();
                    // 小曳光：细长棱柱，+Z 朝前
                    Primitives.Prism(mb, new Vec3(0, 0, -0.06), 0.035, 0.035, 0.14, 4,
                        Mathf.PI * 0.25f);
                    go = TdMaterials.CreateMeshObject("Bullet_" + p.Id, mb, _mat, _root);
                    if (go == null) continue;
                    _active[p.Id] = go;
                }
                go.transform.position = new Vector3((float)p.Col, 0.42f, (float)p.Row);
            }
        }

        private readonly List<int> _gone = new List<int>();
    }
}
