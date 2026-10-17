using System.Collections.Generic;
using System.IO;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Data;
using ArknightsTD.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ArknightsTD.EditorTools
{
    /// <summary>
    /// 把【真实战斗模拟】跑起来并渲染成 PNG：不只是摆模型，而是让 BattleSim 推进一段
    /// 游戏时间后，按当前真实状态（费用、波次、干员、敌人位置、血条）画出来。
    /// <para>
    /// 调用：
    /// <code>
    /// Tuanjie.exe -batchmode -quit -projectPath &lt;工程&gt; \
    ///   -executeMethod ArknightsTD.EditorTools.BattlePreview.Run -logFile &lt;日志&gt;
    /// </code>
    /// 注意：不要加 -nographics，否则读不到真实渲染结果。
    /// 输出：&lt;工程&gt;/Artifacts/battle_preview.png
    /// </para>
    /// </summary>
    public static class BattlePreview
    {
        private const int Width = 1600;
        private const int Height = 900;
        private const int LevelIndex = 1;
        private const double SimSeconds = 42.0;

        public static void Run()
        {
            var level = TDLevels.Get(LevelIndex);
            var sim = new BattleSim();
            sim.Initialize(level, 2024);

            // ---- 部署几个干员（用真实接口，会扣费并校验格） ----
            string reason;
            var deployments = new (OperatorKind kind, int col, int row, Facing facing)[]
            {
                (OperatorKind.Guard,  1, 4, Facing.Right),
                (OperatorKind.Sniper, 2, 2, Facing.Down),
                (OperatorKind.Sniper, 6, 6, Facing.Up),
                (OperatorKind.Caster, 8, 2, Facing.Down),
            };
            int deployed = 0;
            foreach (var d in deployments)
                if (sim.TryDeployOperator(d.kind, d.col, d.row, d.facing, out reason)) deployed++;
            Debug.Log($"[BattlePreview] deployed {deployed}/{deployments.Length} operators, cost={sim.Cost}");

            // ---- 推进真实模拟 ----
            const double step = BattleSim.FixedStep;
            int steps = (int)(SimSeconds / step);
            for (int i = 0; i < steps && sim.Phase == BattlePhase.Running; i++)
                sim.Advance(step);

            Debug.Log($"[BattlePreview] after {sim.Time:0.0}s: phase={sim.Phase} wave={sim.CurrentWave} " +
                      $"lives={sim.Lives}/{sim.MaxLives} cost={sim.Cost} score={sim.Score} " +
                      $"enemies={sim.Enemies.Count} operators={sim.Operators.Count} projectiles={sim.Projectiles.Count}");

            // ---- 构建场景 ----
            var shader = Shader.Find("Standard");
            if (shader == null) { Debug.LogError("[BattlePreview] no usable shader"); EditorApplication.Exit(3); return; }

            var root = new GameObject("BattlePreviewRoot");

            // 地形：可部署 / 路径 / 障碍 三种颜色
            var deploy = new MeshBuilder();
            deploy.FaceTint = ArkPalette.FaceShader(ArkPalette.StoneCool);
            var path = new MeshBuilder();
            path.FaceTint = ArkPalette.FaceShader(ArkPalette.Asphalt);
            var rock = new MeshBuilder();
            rock.FaceTint = ArkPalette.FaceShader(ArkPalette.RockLight);

            for (int c = 0; c < sim.GridW; c++)
                for (int r = 0; r < sim.GridH; r++)
                {
                    var ct = sim.Grid[c, r];
                    if (ct == BattleSim.CellType.Obstacle)
                    {
                        Primitives.Frustum(rock, new Vec3(c + 0.5, 0.02, r + 0.5),
                            0.46, 0.46, 0.30, 0.30, 0.45, 7, c * 0.4 + r * 0.3);
                        Primitives.Pyramid(rock, c + 0.5, r + 0.5, 0.44, 0.47, 0.72);
                        continue;
                    }
                    var target = ct == BattleSim.CellType.Path ? path : deploy;
                    double top = ct == BattleSim.CellType.Path ? 0.05 : 0.06;
                    Primitives.BoxOnGround(target, c + 0.5, r + 0.5, 0.984, top, 0.984);
                }

            // 外围墙体
            var wall = new MeshBuilder();
            wall.FaceTint = ArkPalette.GradientFaceShader(ArkPalette.WallLight);
            for (int c = -1; c <= sim.GridW; c++)
            {
                double h = 0.62 + 0.05 * ((c * 7) % 5);
                Primitives.BeveledBox(wall, new Vec3(c + 0.5, h * 0.5, -0.5), new Vec3(0.46, h * 0.5, 0.46), 0.10);
                Primitives.BeveledBox(wall, new Vec3(c + 0.5, h * 0.5, sim.GridH + 0.5), new Vec3(0.46, h * 0.5, 0.46), 0.10);
            }
            for (int r = -1; r <= sim.GridH; r++)
            {
                double h = 0.62 + 0.05 * ((r * 5) % 4);
                Primitives.BeveledBox(wall, new Vec3(-0.5, h * 0.5, r + 0.5), new Vec3(0.46, h * 0.5, 0.46), 0.10);
                Primitives.BeveledBox(wall, new Vec3(sim.GridW + 0.5, h * 0.5, r + 0.5), new Vec3(0.46, h * 0.5, 0.46), 0.10);
            }

            AddPart(root, "Ground", deploy, shader, 0x3A404A);
            AddPart(root, "Path", path, shader, 0x272B31);
            AddPart(root, "Obstacles", rock, shader, 0x454B55);
            AddPart(root, "Walls", wall, shader, 0x33393F);

            // 干员：按职业上色，位置来自模拟
            foreach (var op in sim.Operators)
            {
                var col = ArkPalette.OperatorColor(op.Kind);
                var mb = new MeshBuilder();
                mb.FaceTint = ArkPalette.FaceShader(col);
                double cx = op.Col + 0.5, cz = op.Row + 0.5;
                Primitives.Frustum(mb, new Vec3(cx, 0.06, cz), 0.26, 0.26, 0.17, 0.17, 0.55, 6);
                Primitives.Box(mb, new Vec3(cx, 0.78, cz), new Vec3(0.20, 0.16, 0.14));
                if (op.Kind == OperatorKind.Sniper)
                    Primitives.Box(mb, new Vec3(cx, 1.00, cz), new Vec3(0.10, 0.08, 0.28));
                else if (op.Kind == OperatorKind.Caster)
                    Primitives.Pyramid(mb, cx, cz, 0.34, 0.68, 0.95);
                else
                    Primitives.Pyramid(mb, cx, cz, 0.28, 0.92, 1.12);
                AddPart(root, $"Op_{op.Kind}_{op.Id}", mb, shader, ColorOf(col));
            }

            // 敌人：按兵种上色与体型，位置来自模拟（真实推进后的位置）
            foreach (var e in sim.Enemies)
            {
                var col = ArkPalette.EnemyColor(e.Kind);
                var half = TDBalance.Enemy(e.Kind).HalfSize;
                var mb = new MeshBuilder();
                mb.FaceTint = ArkPalette.FaceShader(col);
                Primitives.Frustum(mb, new Vec3(e.Col, 0.06, e.Row),
                    half, half, half * 0.68, half * 0.68, half * 1.45, 5, e.Id * 0.7);
                Primitives.Pyramid(mb, e.Col, e.Row, half * 1.1, 0.06 + half * 1.45, 0.06 + half * 2.3);
                AddPart(root, $"Enemy_{e.Kind}_{e.Id}", mb, shader, ColorOf(col));
            }

            // 弹丸：自发光小球
            foreach (var p in sim.Projectiles)
            {
                var mb = new MeshBuilder();
                Primitives.Frustum(mb, new Vec3(p.Col, 0.42, p.Row), 0.05, 0.05, 0.02, 0.02, 0.10, 6);
                AddPart(root, $"Bullet_{p.Id}", mb, shader, 0xFFF4DC, true);
            }

            // ---- 相机：斜俯视，覆盖整个战场 ----
            var camGo = new GameObject("PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.043f, 0.051f, 0.066f, 1f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            float midX = sim.GridW * 0.5f, midZ = sim.GridH * 0.5f;
            camGo.transform.position = new Vector3(midX, sim.GridH * 1.35f, midZ - sim.GridH * 0.85f);
            camGo.transform.LookAt(new Vector3(midX, 0f, midZ));

            // ---- 光照 ----
            var keyGo = new GameObject("KeyLight");
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1.0f, 0.93f, 0.82f);
            key.intensity = 1.15f;
            keyGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var fillGo = new GameObject("FillLight");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.43f, 0.53f, 0.66f);
            fill.intensity = 0.55f;
            fillGo.transform.rotation = Quaternion.Euler(28f, 145f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.28f, 0.32f);

            // ---- 渲染落盘 ----
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            rt.Release();

            var outDir = Path.Combine(Directory.GetCurrentDirectory(), "Artifacts");
            Directory.CreateDirectory(outDir);
            var outPath = Path.Combine(outDir, "battle_preview.png");
            var png = tex.EncodeToPNG();
            File.WriteAllBytes(outPath, png);

            var px = tex.GetPixels32();
            int nonBg = 0;
            for (int i = 0; i < px.Length; i++)
                if (px[i].r > 18 || px[i].g > 18 || px[i].b > 22) nonBg++;
            double pct = 100.0 * nonBg / px.Length;

            Debug.Log($"[BattlePreview] wrote {Width}x{Height} -> {outPath} nonBackground={pct:0.0}% " +
                      $"operators={sim.Operators.Count} enemies={sim.Enemies.Count} bullets={sim.Projectiles.Count}");
            Debug.Log($"[BattlePreview] SELFCHECK fileExists={File.Exists(outPath)} bytes={png.Length} " +
                      $"phase={sim.Phase} wave={sim.CurrentWave} lives={sim.Lives} score={sim.Score}");

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(keyGo);
            Object.DestroyImmediate(fillGo);
            EditorApplication.Exit(0);
        }

        private static uint ColorOf(Rgb c) => (uint)((c.R << 16) | (c.G << 8) | c.B);

        private static void AddPart(GameObject parent, string name, MeshBuilder mb,
            Shader shader, uint color, bool emissive = false)
        {
            if (mb == null || mb.IsEmpty) return;

            var mesh = new Mesh { name = name };
            int vc = mb.VertexCount;
            var verts = new Vector3[vc];
            var norms = new Vector3[vc];
            var uvs = new Vector2[vc];
            var cols = new Color32[vc];
            for (int i = 0; i < vc; i++)
            {
                var p = mb.Positions[i];
                var n = mb.Normals[i];
                var uv = mb.Uvs[i];
                verts[i] = new Vector3((float)p.X, (float)p.Y, (float)p.Z);
                norms[i] = new Vector3((float)n.X, (float)n.Y, (float)n.Z);
                uvs[i] = new Vector2((float)uv.u, (float)uv.v);
                var c = mb.TriangleColor(i / 3);
                cols[i] = new Color32(c.R, c.G, c.B, 255);
            }
            var tris = new int[mb.Triangles.Count];
            for (int i = 0; i < tris.Length; i++) tris[i] = mb.Triangles[i];
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.colors32 = cols;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mat = new Material(shader);
            var col = new Color32((byte)((color >> 16) & 0xFF), (byte)((color >> 8) & 0xFF), (byte)(color & 0xFF), 255);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            if (emissive && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", (Color)col * 0.7f);
            }
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
