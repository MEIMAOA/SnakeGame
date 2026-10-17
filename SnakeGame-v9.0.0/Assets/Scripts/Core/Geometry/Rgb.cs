namespace ArknightsTD.Core.Geometry
{
    /// <summary>
    /// RGB 颜色（非引擎类型，保证几何/配色层可独立测试）。
    /// <para>
    /// 从 <c>ArkPalette.cs</c> 提取为独立文件：它是通用值类型，
    /// 而 ArkPalette 依赖塔防的 OperatorKind/EnemyKind 与 Vec3，
    /// 提取后只引用 Rgb 的代码（如贪吃蛇世界）不必再拖入整个配色表。
    /// </para>
    /// </summary>
    public struct Rgb
    {
        public byte R, G, B;
        public Rgb(byte r, byte g, byte b) { R = r; G = g; B = b; }

        public static Rgb FromHex(uint hex)
            => new Rgb((byte)((hex >> 16) & 0xFF), (byte)((hex >> 8) & 0xFF), (byte)(hex & 0xFF));

        public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
        public bool Equals(Rgb o) => R == o.R && G == o.G && B == o.B;
    }
}
