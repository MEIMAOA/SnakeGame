using System.IO;
using ArknightsTD.Core.Geometry;
using UnityEditor;
using UnityEngine;

namespace ArknightsTD.EditorTools
{
    /// <summary>
    /// headless 场景预览：把程序化生成的几何体真正渲染成 PNG，用于自验 3D 建模效果。
    /// <para>
    /// 调用方式（团结/Unity 命令行）：
    /// <code>
    /// Tuanjie.exe -batchmode -nographics -quit -projectPath &lt;工程&gt; \
    ///   -executeMethod ArknightsTD.EditorTools.ScenePreview.Run -logFile &lt;日志&gt;
    /// </code>
    /// 输出：&lt;工程&gt;/Artifacts/scene_preview.png
    /// </para>
    /// <para>
    /// 说明：预验证用。正式游戏场景会由运行时的 BattlefieldBuilder 构建，
    /// 这里只是为了能在没有人工操作编辑器的情况下看到几何与配色的实际效果。
    /// </para>
    /// </summary>
    public static class ScenePreview
    {
        private const int Width = 1280;
        private const int Height = 720;

        public static void Run()
        {
            // ---------- 渲染环境探针 ----------
            // 先用一个正对镜头、覆盖全屏的大红面验证"这个渲染环境到底出不出图"。
            // 若连红面都出不来，后续几何分析就没有意义，必须先隔离环境问题。
            bool probeOk = RenderProbe();
            Debug.Log($"[ScenePreview] RENDERPROBE fillRedWorked={probeOk}");

            var mb = new MeshBuilder();

            // ---------- 地面：逐格板 + 凹槽 ----------
            const int GW = 12, GH = 8;
            const double cell = 1.0;
            var shader = Shader.Find("Unified Standard") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[ScenePreview] 找不到可用着色器，无法渲染");
                EditorApplication.Exit(3);
                return;
            }

            // 地面分为两类，用两种颜色桶
            var ground = new MeshBuilder();
            ground.FaceTint = ArkPalette.FaceShader(ArkPalette.StoneCool);
            var path = new MeshBuilder();
            path.FaceTint = ArkPalette.FaceShader(ArkPalette.Asphalt);
            var rock = new MeshBuilder();
            rock.FaceTint = ArkPalette.FaceShader(ArkPalette.RockLight);
            var wall = new MeshBuilder();
            wall.FaceTint = ArkPalette.GradientFaceShader(ArkPalette.WallLight);
            var glow = new MeshBuilder();

            // 简化的路径（与第 1 关一致的折线形状）
            var pathCells = new System.Collections.Generic.HashSet<(int, int)>();
            AddPathRun(pathCells, -1, 3, 2, 3);
            AddPathRun(pathCells, 2, 3, 2, 5);
            AddPathRun(pathCells, 2, 5, 9, 5);
            AddPathRun(pathCells, 9, 5, 9, 4);
            AddPathRun(pathCells, 9, 4, 12, 4);

            var obstacleCells = new System.Collections.Generic.HashSet<(int, int)>
            {
                (0, 0), (11, 0), (0, 7), (11, 7),
            };

            for (int c = 0; c < GW; c++)
                for (int r = 0; r < GH; r++)
                {
                    bool isPath = pathCells.Contains((c, r));
                    bool isObstacle = obstacleCells.Contains((c, r));

                    if (isObstacle)
                    {
                        // 岩体：棱柱 + 尖顶，不用立方体
                        Primitives.Frustum(rock, new Vec3(c + 0.5, 0.02, r + 0.5),
                            0.46, 0.46, 0.30, 0.30, 0.42, 7, c * 0.4);
                        Primitives.Pyramid(rock, c + 0.5, r + 0.5, 0.44, 0.44, 0.68);
                        continue;
                    }

                    var target = isPath ? path : ground;
                    double top = isPath ? 0.055 : 0.06;

                    // 板体 + 裙边：让地面有厚度而不是纸片
                    Primitives.BoxOnGround(target, c + 0.5, r + 0.5, cell * 0.984, top, cell * 0.984);
                }

            // 路径发光带：贴地薄片
            foreach (var cellPos in pathCells)
            {
                double c = cellPos.Item1, r = cellPos.Item2;
                if (c < 0 || c >= GW || r < 0 || r >= GH) continue;
                Primitives.GroundQuad(glow, c + 0.06, r + 0.06, c + 0.94, r + 0.94, 0.058);
            }

            // 战场外围墙体：切角块，高低错落
            for (int c = -1; c <= GW; c++)
            {
                double h = 0.60 + 0.05 * ((c * 7) % 5);
                Primitives.BeveledBox(wall, new Vec3(c + 0.5, h * 0.5, -0.5),
                    new Vec3(0.46, h * 0.5, 0.46), 0.10);
                Primitives.BeveledBox(wall, new Vec3(c + 0.5, h * 0.5, GH + 0.5),
                    new Vec3(0.46, h * 0.5, 0.46), 0.10);
            }
            for (int r = -1; r <= GH; r++)
            {
                double h = 0.60 + 0.05 * ((r * 5) % 4);
                Primitives.BeveledBox(wall, new Vec3(-0.5, h * 0.5, r + 0.5),
                    new Vec3(0.46, h * 0.5, 0.46), 0.10);
                Primitives.BeveledBox(wall, new Vec3(GW + 0.5, h * 0.5, r + 0.5),
                    new Vec3(0.46, h * 0.5, 0.46), 0.10);
            }

            // ---------- 干员与敌人（程序化低多边形）----------
            var operatorMesh = new MeshBuilder();
            operatorMesh.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0xD88C3C));
            Primitives.Frustum(operatorMesh, new Vec3(3.5, 0.06, 2.5), 0.26, 0.26, 0.17, 0.17, 0.55, 6);
            Primitives.Box(operatorMesh, new Vec3(3.5, 0.78, 2.5), new Vec3(0.20, 0.16, 0.14));
            Primitives.Pyramid(operatorMesh, 3.5, 2.5, 0.30, 0.94, 1.12);

            var operatorMesh2 = new MeshBuilder();
            operatorMesh2.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0x5C9ED8));
            Primitives.Frustum(operatorMesh2, new Vec3(7.5, 0.06, 3.5), 0.22, 0.22, 0.15, 0.15, 0.70, 6);
            Primitives.Box(operatorMesh2, new Vec3(7.5, 0.98, 3.5), new Vec3(0.12, 0.10, 0.30));

            var enemyMesh = new MeshBuilder();
            enemyMesh.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0xE07040));
            for (int i = 0; i < 5; i++)
            {
                double cx = 4.5 + i * 1.1, cz = 5.5;
                Primitives.Frustum(enemyMesh, new Vec3(cx, 0.06, cz), 0.24, 0.24, 0.16, 0.16, 0.34, 5, i);
                Primitives.Pyramid(enemyMesh, cx, cz, 0.26, 0.40, 0.58);
            }

            var bossMesh = new MeshBuilder();
            bossMesh.FaceTint = ArkPalette.FaceShader(Rgb.FromHex(0xC03040));
            Primitives.Frustum(bossMesh, new Vec3(9.5, 0.06, 6.5), 0.44, 0.44, 0.30, 0.30, 0.62, 6);
            Primitives.BeveledBox(bossMesh, new Vec3(9.5, 0.92, 6.5), new Vec3(0.26, 0.20, 0.26), 0.08);

            // ---------- 组装 GameObject ----------
            var root = new GameObject("ScenePreviewRoot");
            AddPart(root, "Ground", ground, shader, 0x3A404A);
            AddPart(root, "Path", path, shader, 0x272B31);
            AddPart(root, "Rock", rock, shader, 0x454B55);
            AddPart(root, "Wall", wall, shader, 0x33393F);
            AddPart(root, "Operator1", operatorMesh, shader, 0xD88C3C);
            AddPart(root, "Operator2", operatorMesh2, shader, 0x5C9ED8);
            AddPart(root, "Enemies", enemyMesh, shader, 0xE07040);
            AddPart(root, "Boss", bossMesh, shader, 0xC03040);
            AddPart(root, "PathGlow", glow, shader, 0x4AD8E8, true);

            // ---------- 相机：斜俯视，取景覆盖整个战场 ----------
            var camGo = new GameObject("PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.043f, 0.051f, 0.066f, 1f);   // 深渊底色
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            camGo.transform.position = new Vector3(6f, 13.5f, -4.5f);
            camGo.transform.LookAt(new Vector3(6f, 0f, 4f));

            // ---------- 光照：冷灰主光 + 暖色补光 + 环境 ----------
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
            RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.28f);

            // ---------- 渲染到 PNG ----------
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
            var outPath = Path.Combine(outDir, "scene_preview.png");
            var png = tex.EncodeToPNG();
            File.WriteAllBytes(outPath, png);

            // 统计非背景像素，判断是否真的画出了东西（而不是全黑/空白）
            var px = tex.GetPixels32();
            int nonBg = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].r > 18 || px[i].g > 18 || px[i].b > 22) nonBg++;
            }
            double pct = 100.0 * nonBg / px.Length;

            Debug.Log($"[ScenePreview] wrote {Width}x{Height} -> {outPath}  " +
                      $"nonBackground={pct:0.0}%  triangles(gnd/path/rock/wall/ops/enemy)=" +
                      $"{ground.TriangleCount}/{path.TriangleCount}/{rock.TriangleCount}/" +
                      $"{wall.TriangleCount}/{(operatorMesh.TriangleCount + operatorMesh2.TriangleCount)}/" +
                      $"{(enemyMesh.TriangleCount + bossMesh.TriangleCount)}");
            Debug.Log($"[ScenePreview] SELFCHECK nonBackgroundPct={pct:0.0} fileExists={File.Exists(outPath)} " +
                      $"bytes={png.Length}");

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(keyGo);
            Object.DestroyImmediate(fillGo);

            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 渲染环境探针：一个正对镜头、覆盖全屏的纯红面。
        /// 用来判定"渲染不出图"到底是环境问题（例如 -nographics 下没有真实 GPU 输出），
        /// 还是我的几何/材质问题。返回 true 表示红面确实渲染出来了。
        /// </summary>
        private static bool RenderProbe()
        {
            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            if (shader == null) return false;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ProbeQuad";
            // 放在相机正前方并放大到覆盖全屏
            go.transform.position = new Vector3(0f, 0f, 5f);
            go.transform.localScale = new Vector3(40f, 40f, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 面朝相机

            var mat = new Material(shader);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.red);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.red);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var camGo = new GameObject("ProbeCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.transform.position = Vector3.zero;
            camGo.transform.rotation = Quaternion.identity;   // 朝 +Z

            var rt = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            cam.targetTexture = null;
            rt.Release();

            var c = tex.GetPixel(32, 32);
            bool ok = c.r > 0.5f && c.g < 0.5f && c.b < 0.5f;
            Debug.Log($"[ScenePreview] probe centre pixel = ({c.r:0.000},{c.g:0.000},{c.b:0.000})");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(tex);
            return ok;
        }

        private static void AddPathRun(System.Collections.Generic.HashSet<(int, int)> set,
            int x0, int y0, int x1, int y1)
        {
            if (x0 == x1)
            {
                int lo = System.Math.Min(y0, y1), hi = System.Math.Max(y0, y1);
                for (int y = lo; y <= hi; y++) set.Add((x0, y));
            }
            else
            {
                int lo = System.Math.Min(x0, x1), hi = System.Math.Max(x0, x1);
                for (int x = lo; x <= hi; x++) set.Add((x, y0));
            }
        }

        private static void AddPart(GameObject parent, string name, MeshBuilder mb,
            Shader shader, uint fallbackColor, bool emissive = false)
        {
            if (mb == null || mb.IsEmpty) return;

            var mesh = BuildUnityMesh(mb, name);
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mat = new Material(shader);
            // 顶点色：单材质即可表现逐面配色
            var col = new Color32(
                (byte)((fallbackColor >> 16) & 0xFF),
                (byte)((fallbackColor >> 8) & 0xFF),
                (byte)(fallbackColor & 0xFF), 255);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            if (emissive && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", (Color)col * 0.6f);
            }

            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>把引擎无关的 MeshBuilder 转成 Unity Mesh（含顶点色）。</summary>
        private static Mesh BuildUnityMesh(MeshBuilder mb, string name)
        {
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

            // 顶点数在 65535 以内可用 16 位索引，无需额外设置
            return mesh;
        }
    }
}
