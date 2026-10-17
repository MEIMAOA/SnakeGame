using System.Collections.Generic;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Data;
using ArknightsTD.Gameplay;
using UnityEngine;

namespace ArknightsTD.View
{
    /// <summary>
    /// 敌人视图：把 <see cref="BattleSim.Enemies"/> 同步到场景。
    /// <para>
    /// 用<b>对象池</b>：每波敌人数量不小，创建/销毁 GameObject + Mesh 会造成明显卡顿，
    /// 因此按兵种预建模型、按需要复用。
    /// </para>
    /// </summary>
    public sealed class EnemyViews
    {
        /// <summary>每种兵种一个预制网格与材质（同兵种共享，不重复生成）。</summary>
        private readonly Dictionary<EnemyKind, Mesh> _meshes = new Dictionary<EnemyKind, Mesh>();
        private readonly Dictionary<EnemyKind, Material> _mats = new Dictionary<EnemyKind, Material>();
        private readonly List<GameObject> _pool = new List<GameObject>();
        private readonly Dictionary<int, GameObject> _active = new Dictionary<int, GameObject>();

        private readonly Transform _root;

        /// <summary>同时存在的敌人上限（超出则复用或忽略，防止异常情况爆场景）。</summary>
        public const int MaxVisible = 64;

        public EnemyViews(Transform parent)
        {
            var go = new GameObject("Enemies");
            go.transform.SetParent(parent, false);
            _root = go.transform;
        }

        /// <summary>当前可见敌人数。</summary>
        public int VisibleCount => _active.Count;

        public void Sync(BattleSim sim)
        {
            if (sim == null) return;

            // 1) 回收已消失的（死亡或走到终点被移除的）
            _toRelease.Clear();
            foreach (var kv in _active)
            {
                bool stillAlive = false;
                for (int i = 0; i < sim.Enemies.Count; i++)
                {
                    var e = sim.Enemies[i];
                    if (e.Id == kv.Key && !e.Dead) { stillAlive = true; break; }
                }
                if (!stillAlive) _toRelease.Add(kv.Key);
            }
            for (int i = 0; i < _toRelease.Count; i++) Release(_toRelease[i]);

            // 2) 更新/新增
            for (int i = 0; i < sim.Enemies.Count; i++)
            {
                var e = sim.Enemies[i];
                if (e.Dead) continue;

                GameObject go;
                if (!_active.TryGetValue(e.Id, out go))
                {
                    if (_active.Count >= MaxVisible) continue;
                    go = Acquire(e.Kind);
                    _active[e.Id] = go;
                }

                var t = go.transform;
                // 世界坐标 ↔ Unity 坐标：网格 (col,row) → Unity (x, y, z) = (col, 高度, row)
                t.position = new Vector3((float)e.Col, TdMaterials.UnitBaseY, (float)e.Row);

                // 朝向：把模型的 +Z 前方对齐行进方向
                float yaw = Mathf.Atan2((float)e.DirCol, (float)e.DirRow) * Mathf.Rad2Deg;
                t.rotation = Quaternion.Euler(0f, yaw, 0f);

                // 受击闪光：命中时整体提亮
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var baseMat = _mats[e.Kind];
                    if (e.HitFlash > 0 && _hitMat != null) mr.sharedMaterial = _hitMat;
                    else mr.sharedMaterial = baseMat;
                }
            }
        }

        private readonly List<int> _toRelease = new List<int>();
        private Material _hitMat;

        // ---- 血条（明日方舟式：敌人头顶血条，按剩余血量变色）----
        private GameObject _hpGo;
        private MeshFilter _hpFilter;
        private Mesh _hpMesh;

        private void Release(int id)
        {
            GameObject go;
            if (!_active.TryGetValue(id, out go)) return;
            go.SetActive(false);
            _pool.Add(go);
            _active.Remove(id);
        }

        private GameObject Acquire(EnemyKind kind)
        {
            // 优先复用池里同兵种的
            for (int i = 0; i < _pool.Count; i++)
            {
                var cand = _pool[i];
                if (cand.name.StartsWith("Enemy_" + kind))
                {
                    _pool.RemoveAt(i);
                    cand.SetActive(true);
                    return cand;
                }
            }

            var mb = BuildEnemyMesh(kind);
            var go = TdMaterials.CreateMeshObject("Enemy_" + kind, mb, MaterialFor(kind), _root);
            return go;
        }

        private Material MaterialFor(EnemyKind kind)
        {
            Material m;
            if (_mats.TryGetValue(kind, out m)) return m;
            m = TdMaterials.Solid();
            _mats[kind] = m;
            return m;
        }

        /// <summary>受击闪光材质（全白自发光）。</summary>
        public void PrepareHitFlash()
        {
            _hitMat = TdMaterials.Emissive(ArknightsTD.Core.Geometry.ArkPalette.HotWhite, 1.6f);
        }

        /// <summary>按兵种程序化生成低多边形模型（剪影与体型可区分）。</summary>
        public static MeshBuilder BuildEnemyMesh(EnemyKind kind)
        {
            var def = TDBalance.Enemy(kind);
            var color = ArkPalette.EnemyColor(kind);
            double half = def.HalfSize;

            var mb = new MeshBuilder();
            mb.FaceTint = ArkPalette.FaceShader(color);

            // 身体：下宽上窄的棱柱（低多边形躯干）
            Primitives.Frustum(mb, new Vec3(0, 0.02, 0),
                half, half, half * 0.68, half * 0.68, half * 1.45, 5, 0.4);

            // 头/肩：按兵种做差异化剪影
            switch (kind)
            {
                case EnemyKind.Infantry:
                    Primitives.Pyramid(mb, 0, 0, half * 1.05, 0.02 + half * 1.45, 0.02 + half * 2.25);
                    break;
                case EnemyKind.Shield:
                    // 重盾：正面加一块厚盾牌
                    Primitives.Box(mb, new Vec3(0, half * 1.0, half * 0.9),
                        new Vec3(half * 1.0, half * 0.95, half * 0.18));
                    Primitives.Pyramid(mb, 0, 0, half * 0.95, 0.02 + half * 1.45, 0.02 + half * 2.1);
                    break;
                case EnemyKind.Runner:
                    // 疾行：低伏前倾 + 后掠尾
                    Primitives.Pyramid(mb, 0, 0, half * 0.9, 0.02 + half * 1.1, 0.02 + half * 1.8);
                    Primitives.Box(mb, new Vec3(0, half * 0.7, -half * 1.2),
                        new Vec3(half * 0.18, half * 0.5, half * 0.9));
                    break;
                case EnemyKind.Heavy:
                    // 重装：矮胖 + 巨型切角肩甲
                    Primitives.BeveledBox(mb, new Vec3(0, half * 1.5, 0),
                        new Vec3(half * 1.15, half * 0.55, half * 1.0), half * 0.3);
                    Primitives.Pyramid(mb, 0, 0, half * 1.1, 0.02 + half * 1.95, 0.02 + half * 2.6);
                    break;
                case EnemyKind.Boss:
                    // 首领：最高 + 头冠四角 + 背鳍
                    Primitives.BeveledBox(mb, new Vec3(0, half * 1.2, 0),
                        new Vec3(half * 0.95, half * 0.5, half * 0.85), half * 0.25);
                    Primitives.Pyramid(mb, 0, 0, half * 1.2, 0.02 + half * 1.7, 0.02 + half * 3.1);
                    for (int i = 0; i < 4; i++)
                    {
                        double ax = (i % 2 == 0 ? 1 : -1) * half * 0.75;
                        double az = (i < 2 ? 1 : -1) * half * 0.75;
                        Primitives.Pyramid(mb, ax, az, half * 0.34,
                            0.02 + half * 2.5, 0.02 + half * 3.2);
                    }
                    break;
            }

            return mb;
        }
    }
}
