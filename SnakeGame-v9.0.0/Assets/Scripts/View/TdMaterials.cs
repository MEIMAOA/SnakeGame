using ArknightsTD.Core.Geometry;
using UnityEngine;

namespace ArknightsTD.View
{
    /// <summary>
    /// 材质与网格工厂。
    /// <para>
    /// 所有程序化网格都把逐面配色写进<b>顶点色</b>，因此材质基色保持白色，
    /// 否则会与顶点色重复相乘，整片偏暗。
    /// </para>
    /// </summary>
    public static class TdMaterials
    {
        /// <summary>
        /// 单位模型的站立基准高度 = 地形板顶面高度。
        /// 程序化单位网格的本地原点在 y=0，若不抬起就会陷进地面（实测表现为"贴地小黑片"）。
        /// 这里与 <see cref="BattlefieldBuilder.GroundTop"/> 保持一致，避免两处硬编码漂移。
        /// </summary>
        public const float UnitBaseY = 0.06f;

        private static Shader _shader;

        /// <summary>当前工程用内置渲染管线；URP 启用后会优先命中 Lit。</summary>
        public static Shader SolidShader
        {
            get
            {
                if (_shader != null) return _shader;

                // ★ 必须优先用本项目自定义的顶点色着色器：
                //   内置 Standard 不会把顶点色乘进反照率（实测洋红顶点色的画面占比 0.00%），
                //   会导致所有程序化网格都渲染成材质基色 —— 整片发灰、路径与地面无法区分。
                _shader = Shader.Find("ArknightsTD/VertexColorLit")
                       ?? Shader.Find("Standard")
                       ?? Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Sprites/Default")
                       ?? Shader.Find("Unlit/Color");
                if (_shader == null)
                    Debug.LogError("[TdMaterials] 找不到可用着色器（构建可能剥离了着色器）");
                else
                    Debug.Log($"[TdMaterials] 使用着色器: {_shader.name}");
                return _shader;
            }
        }

        /// <summary>
        /// 创建材质。着色器缺失时给出明确的异常信息，
        /// 由调用方（Bootstrap）捕获后退回菜单，而不是留下"菜单已关、画面空白"的半初始化状态。
        /// </summary>
        private static Material CreateMaterial()
        {
            if (SolidShader == null)
                throw new System.InvalidOperationException(
                    "[TdMaterials] 没有可用着色器，无法创建材质。" +
                    "请确认 ArknightsTD/VertexColorLit 已加入 Project Settings → Graphics " +
                    "→ Always Included Shaders（本项目已默认配置）。");
            return new Material(SolidShader);
        }

        public static Material Solid()
        {
            var m = CreateMaterial();
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            return m;
        }

        /// <summary>自发光材质（弹丸 / 能量件）。</summary>
        public static Material Emissive(Rgb color, float intensity = 0.8f)
        {
            var m = CreateMaterial();
            var c = new Color32(color.R, color.G, color.B, 255);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", (Color)c * intensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        /// <summary>半透明贴地材质（部署预览 / 范围提示）。</summary>
        public static Material Transparent(Rgb color, float alpha = 0.45f)
        {
            var m = CreateMaterial();
            var c = new Color32(color.R, color.G, color.B,
                (byte)Mathf.Clamp(alpha * 255f, 0f, 255f));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);

            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        /// <summary>把引擎无关的 MeshBuilder 转成 Unity Mesh（含顶点色与法线）。</summary>
        public static Mesh BuildMesh(MeshBuilder mb, string name)
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
            return mesh;
        }

        /// <summary>创建带网格的 GameObject。</summary>
        public static GameObject CreateMeshObject(string name, MeshBuilder mb, Material mat,
            Transform parent = null)
        {
            if (mb == null || mb.IsEmpty) return null;

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildMesh(mb, name);

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat ?? Solid();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }
    }
}
