using System;

namespace ArknightsTD.Core.Geometry
{

    /// <summary>
    /// 明日方舟式工业风配色 + 逐面着色器。
    /// <para>
    /// 设计意图：冷灰石材/金属打底，琥珀金作强调色，青色作能量光，
    /// 配合偏冷的定向主光（左上前方）营造高对比、低饱和的工业感。
    /// </para>
    /// <para>
    /// <see cref="FaceShader"/> 用<b>朝向</b>而非实时光照来决定每个面的明暗：
    /// 顶面最亮、底面最暗（近似 AO）、迎主光侧略亮。这样即使在没有实时光照的
    /// 预览/工具链里，模型也保持可读的立体感与统一的硬边面差。
    /// </para>
    /// </summary>
    public static class ArkPalette
    {
        // ---------- 地形 / 结构 ----------
        public static readonly Rgb Abyss = Rgb.FromHex(0x0B0D11);        // 深渊底色
        public static readonly Rgb StoneCool = Rgb.FromHex(0x3A404A);    // 冷灰石材
        public static readonly Rgb StoneLight = Rgb.FromHex(0x555D69);
        public static readonly Rgb Asphalt = Rgb.FromHex(0x272B31);      // 沥青路面
        public static readonly Rgb AsphaltWorn = Rgb.FromHex(0x463E33);
        public static readonly Rgb MetalDark = Rgb.FromHex(0x1E2228);
        public static readonly Rgb MetalMid = Rgb.FromHex(0x4A525C);
        public static readonly Rgb RockDark = Rgb.FromHex(0x2A2E35);
        public static readonly Rgb RockLight = Rgb.FromHex(0x454B55);
        public static readonly Rgb WallDark = Rgb.FromHex(0x16191E);
        public static readonly Rgb WallLight = Rgb.FromHex(0x33393F);

        // ---------- 强调色 ----------
        public static readonly Rgb Amber = Rgb.FromHex(0xE8A83C);        // ★ 明日方舟主强调色
        public static readonly Rgb AmberDim = Rgb.FromHex(0x8A6322);
        public static readonly Rgb Cyan = Rgb.FromHex(0x4AD8E8);         // 能量光
        public static readonly Rgb Crimson = Rgb.FromHex(0xD83A3A);      // 危险/生命
        public static readonly Rgb AllyBlue = Rgb.FromHex(0x6E9AC8);
        public static readonly Rgb HotWhite = Rgb.FromHex(0xFFF4DC);

        // ---------- 阵营色 ----------
        public static readonly Rgb EnemyInfantry = Rgb.FromHex(0xE07040);
        public static readonly Rgb EnemyShield = Rgb.FromHex(0x5080C0);
        public static readonly Rgb EnemyRunner = Rgb.FromHex(0xE8C030);
        public static readonly Rgb EnemyHeavy = Rgb.FromHex(0x705090);
        public static readonly Rgb EnemyBoss = Rgb.FromHex(0xC03040);

        public static Rgb OperatorColor(Data.OperatorKind kind)
        {
            switch (kind)
            {
                case Data.OperatorKind.Guard: return Rgb.FromHex(0xD88C3C);
                case Data.OperatorKind.Sniper: return Rgb.FromHex(0x5C9ED8);
                default: return Rgb.FromHex(0x9C6CD8);
            }
        }

        public static Rgb EnemyColor(Data.EnemyKind kind)
        {
            switch (kind)
            {
                case Data.EnemyKind.Shield: return EnemyShield;
                case Data.EnemyKind.Runner: return EnemyRunner;
                case Data.EnemyKind.Heavy: return EnemyHeavy;
                case Data.EnemyKind.Boss: return EnemyBoss;
                default: return EnemyInfantry;
            }
        }

        // ---------- 颜色运算 ----------
        public static Rgb Lighten(Rgb c, double f)
        {
            Clamp01(ref f);
            return new Rgb((byte)(c.R + (255 - c.R) * f),
                           (byte)(c.G + (255 - c.G) * f),
                           (byte)(c.B + (255 - c.B) * f));
        }

        public static Rgb Darken(Rgb c, double f)
        {
            Clamp01(ref f);
            return new Rgb((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));
        }

        public static Rgb Lerp(Rgb a, Rgb b, double t)
        {
            Clamp01(ref t);
            return new Rgb((byte)(a.R + (b.R - a.R) * t),
                           (byte)(a.G + (b.G - a.G) * t),
                           (byte)(a.B + (b.B - a.B) * t));
        }

        private static void Clamp01(ref double v)
        {
            if (v < 0) v = 0; else if (v > 1) v = 1;
        }

        // ---------- 方向光（与 WPF 版一致，供实时光照与面着色共用）----------
        /// <summary>主光方向（指向光源）：从画面左上前方照入。</summary>
        public static readonly Vec3 KeyLightDir = new Vec3(-0.45, 0.80, -0.40).Normalized();
        /// <summary>补光方向（指向光源）：偏暖、偏右，用于提亮暗面。</summary>
        public static readonly Vec3 FillLightDir = new Vec3(0.55, 0.35, 0.75).Normalized();

        /// <summary>朗伯着色，用于需要按真实光照预计算颜色的场合。</summary>
        public static Rgb Shade(Rgb baseColor, Vec3 normal, double ambient = 0.42)
        {
            var n = normal.Normalized();
            double key = Math.Max(0, Vec3.Dot(n, KeyLightDir));
            double fill = Math.Max(0, Vec3.Dot(n, FillLightDir));
            double k = ambient + 0.62 * key + 0.18 * fill;
            if (k > 1.35) k = 1.35;
            return new Rgb(
                (byte)Math.Min(255, baseColor.R * k),
                (byte)Math.Min(255, baseColor.G * k),
                (byte)Math.Min(255, baseColor.B * k));
        }

        /// <summary>
        /// 生成"逐面着色器"：给定基色，返回可直接赋给 <see cref="MeshBuilder.FaceTint"/> 的回调。
        /// 顶面 +12%、底面 -45%、迎主光侧 +6%、背光侧 -12%，形成统一的硬边面差。
        /// </summary>
        public static Func<double, double, int, Rgb> FaceShader(Rgb baseColor)
        {
            return (u, v, kind) =>
            {
                switch (kind)
                {
                    case 0: return Lighten(baseColor, 0.12);   // 顶面：最亮
                    case 1: return Darken(baseColor, 0.55);    // 底面：最暗（近似 AO）
                    case 2: return Darken(baseColor, 0.86);    // +X：主光 X 为负 -> 背光
                    case 3: return Lighten(baseColor, 0.05);   // -X：迎光
                    case 4: return Darken(baseColor, 0.90);    // +Z：主光 Z 为负 -> 背光
                    default: return Lighten(baseColor, 0.03);  // -Z：迎光
                }
            };
        }

        /// <summary>
        /// 带轻微渐变的面着色（顶亮底暗的纵向渐变），用于需要体积感的主体。
        /// </summary>
        public static Func<double, double, int, Rgb> GradientFaceShader(Rgb baseColor, double strength = 0.10)
        {
            var bright = Lighten(baseColor, 0.12 + strength);
            var dark = Darken(baseColor, 0.55);
            return (u, v, kind) =>
            {
                if (kind == 0) return bright;
                if (kind == 1) return dark;
                double t = 1.0 - v;                 // v=1 在面顶部
                var c = Lerp(dark, bright, t);
                return kind == 2 || kind == 4 ? Darken(c, 0.90) : c;
            };
        }
    }
}
