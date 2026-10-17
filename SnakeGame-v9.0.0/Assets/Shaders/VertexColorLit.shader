Shader "ArknightsTD/VertexColorLit"
{
    // ==================================================================================
    //  明日方舟风程序化网格专用着色器
    //
    //  为什么需要自定义：内置 Standard 着色器【不会】把顶点色乘进反照率
    //  （实测：地面顶点色为 #FF1EFF 时，画面中洋红像素占比 0.00%，
    //   渲染出来的是材质基色，导致整个战场发灰、路径与地面无法区分）。
    //
    //  本着色器做两件事：
    //    1. 反照率 = 材质基色 × 顶点色   （逐面配色靠顶点色表达）
    //    2. 一点简单的 Lambert + 半球环境光，保持冷灰工业风的体积感
    //
    //  为兼容起见使用 CG/表面着色器语法（内置管线与 URP 均可编译）。
    // ==================================================================================

    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _AmbientBoost ("Ambient Boost", Range(0,1)) = 0.35
        _KeyBoost ("Key Boost", Range(0,2)) = 0.85
        _Emission ("Emission", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0

        struct Input
        {
            float4 vcolor;
        };

        fixed4 _Color;
        half _AmbientBoost;
        half _KeyBoost;
        fixed4 _Emission;

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcolor = v.color;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed3 tint = IN.vcolor.rgb * _Color.rgb;
            o.Albedo = tint * (1.0 + _AmbientBoost);
            o.Emission = _Emission.rgb;
            o.Alpha = 1.0;

            // 用顶点色亮度轻微影响自发光，让亮面更"发光"一点（低多边形硬边风格）
            o.Emission += tint * _KeyBoost * 0.06;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
