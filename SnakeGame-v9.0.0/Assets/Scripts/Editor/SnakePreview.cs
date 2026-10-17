using System;
using System.IO;
using ArknightsTD.Core.Geometry;
using ArknightsTD.Snake;
using UnityEngine;
using UnityEditor;

namespace ArknightsTD.EditorTools
{
    /// <summary>
    /// 渲染贪吃蛇各模式的真实运行状态：
    /// 创建 SnakeController → 用脚本策略驾驶 → 推进模拟 → 渲染 3D 世界 → 复合 HUD → 落盘 PNG。
    /// <para>
    /// 用途：无需进入 Play 模式即可真实执行并验证整条运行时链路。
    /// </para>
    /// <para>
    /// 调用：Tuanjie.exe -batchmode -quit -projectPath &lt;工程&gt;
    ///       -executeMethod ArknightsTD.EditorTools.SnakePreview.Run
    /// 注意不要加 -nographics。输出：Artifacts/snake_&lt;mode&gt;.png
    /// </para>
    /// </summary>
    public static class SnakePreview
    {
        private const int W = 1280;
        private const int H = 720;

        public static void Run()
        {
            RunMode(SnakeMode.Classic, "classic", 20.0);
            RunMode(SnakeMode.Extreme, "extreme", 14.0);
            RunMode(SnakeMode.Team4v4, "team", 10.0);
            RunMode(SnakeMode.Extraction, "extraction", 12.0);
            RunMode(SnakeMode.Timed, "timed", 24.0);

            Debug.Log("[SnakePreview] all modes rendered");
            EditorApplication.Exit(0);
        }

        private static void RunMode(SnakeMode mode, string tag, double seconds)
        {
            var host = new GameObject("SnakeHost_" + tag);
            var ctrl = host.AddComponent<SnakeView.SnakeController>();
            ctrl.Mode = mode;
            ctrl.RandomSeed = 20240;
            ctrl.SetupField();

            var w = ctrl.World;
            Debug.Log($"[SnakePreview] {tag}: initialized foods={w.Foods.Count} enemies={w.Enemies.Count} " +
                      $"mode={w.Mode} display={SnakeModeConfig.For(w.Mode).DisplayName}");

            // ---- 用脚本策略驾驶 ----
            int steps = (int)(seconds / SnakeWorld.FixedStep);
            for (int i = 0; i < steps && w.Outcome == MatchOutcome.Running; i++)
            {
                DriveForPreview(w, mode);
                ctrl.Tick(SnakeWorld.FixedStep);
            }

            // ---- 手动同步视图与相机（批量模式没有帧循环，不能依赖 Update）----
            ctrl.View.Sync(w);
            PositionCamera(ctrl, w);

            // ---- 渲染 ----
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[SnakePreview] no main camera"); return; }

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            rt.Release();

            CompositeHud(tex, ctrl);

            var outDir = Path.Combine(Directory.GetCurrentDirectory(), "Artifacts");
            Directory.CreateDirectory(outDir);
            var outPath = Path.Combine(outDir, $"snake_{tag}.png");
            var png = tex.EncodeToPNG();
            File.WriteAllBytes(outPath, png);

            var px = tex.GetPixels32();
            int nonBg = 0;
            for (int i = 0; i < px.Length; i++)
                if (px[i].r > 18 || px[i].g > 18 || px[i].b > 22) nonBg++;
            double pct = 100.0 * nonBg / px.Length;

            Debug.Log($"[SnakePreview] {tag}: state={ctrl.DescribeState()}");
            Debug.Log($"[SnakePreview] {tag}: wrote {W}x{H} -> {outPath} " +
                      $"nonBackground={pct:0.0}% bytes={png.Length}");

            UnityEngine.Object.DestroyImmediate(host);
            var root = GameObject.Find("SnakeRoot");
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        /// <summary>
        /// 预览用驾驶策略。引擎拒绝 180° 反向输入（防自杀），
        /// 靠近边界时改用切线绕行，永不触发反向拒绝。
        /// </summary>
        private static void DriveForPreview(SnakeWorld w, SnakeMode mode)
        {
            var head = w.Player.Head;

            // 搜打撤：优先最近的未搜容器，按 F 搜索；之后去撤离点
            if (mode == SnakeMode.Extraction)
            {
                Vec2 target = w.ExtractionPoint;
                var near = w.Extraction != null ? w.Extraction.CurrentNearContainer : null;
                if (near != null && w.ContainersSearched == 0)
                {
                    // 靠近容器时按 F 搜索（引擎按轮询 F 状态推进读条）
                    w.FKeyDown = true;
                    return;
                }
                w.FKeyDown = false;

                if (w.ContainersSearched < w.ContainersTotal)
                {
                    double bestSq = double.MaxValue;
                    var containers = w.Containers;
                    for (int i = 0; i < containers.Count; i++)
                    {
                        var c = containers[i];
                        if (c.IsSearched) continue;
                        double dx = c.Position.X - head.X, dy = c.Position.Y - head.Y;
                        double sq = dx * dx + dy * dy;
                        if (sq < bestSq) { bestSq = sq; target = c.Position; }
                    }
                }

                // 到撤离点附近就停下读条（不再乱走）
                if (w.ContainersSearched > 0 &&
                    SnakeWorld.Dist(head, w.ExtractionPoint) <= w.ExtractionRadius * 0.5)
                    return;

                if (NearWall(w, head, 60.0))
                {
                    var radial = SnakeAi.Norm(head.X, head.Y);
                    w.SetDirection(new Vec2(-radial.Y * 0.9 - radial.X * 0.5,
                                            radial.X * 0.9 - radial.Y * 0.5));
                    return;
                }

                w.SetDirection(SnakeAi.Norm(target.X - head.X, target.Y - head.Y));
                return;
            }

            // 占点：留在中心附近绕圈
            if (mode == SnakeMode.Team4v4)
            {
                var radial = SnakeAi.Norm(head.X - w.CaptureZone.X, head.Y - w.CaptureZone.Y);
                var tangent = new Vec2(-radial.Y, radial.X);
                var inward = new Vec2(-radial.X, -radial.Y);
                w.SetDirection(new Vec2(tangent.X * 0.85 + inward.X * 0.15,
                                        tangent.Y * 0.85 + inward.Y * 0.15));
                return;
            }

            // 淘汰之王：毒圈内游走 + 按 F 发起决斗
            if (mode == SnakeMode.Timed)
            {
                // 决斗中方向由决斗逻辑接管，不要覆盖
                if (w.IsRacing) { w.FKeyDown = false; return; }

                // 先试着发起决斗（按 F）
                w.FKeyDown = !w.RaceDisabled;

                // 留在毒圈内：朝圈心 60% 处游走，靠近边界时切线绕行
                var toCenter = new Vec2(w.PoisonCenter.X - head.X, w.PoisonCenter.Y - head.Y);
                double distToCenter = toCenter.Length;
                double safeRadius = w.PoisonRadius * 0.6;
                if (distToCenter > safeRadius || NearWall(w, head, 60.0))
                {
                    var radial = SnakeAi.Norm(head.X - w.PoisonCenter.X, head.Y - w.PoisonCenter.Y);
                    var tangent = new Vec2(-radial.Y, radial.X);
                    w.SetDirection(new Vec2(tangent.X * 0.8 - radial.X * 0.6,
                                            tangent.Y * 0.8 - radial.Y * 0.6));
                }
                else
                {
                    // 圈内绕圈
                    var radial = SnakeAi.Norm(head.X - w.PoisonCenter.X, head.Y - w.PoisonCenter.Y);
                    w.SetDirection(new Vec2(-radial.Y, radial.X));
                }
                return;
            }

            // 其余模式：找最近食物；靠近边界时切线绕行
            if (NearWall(w, head, 60.0))
            {
                var radial = SnakeAi.Norm(head.X, head.Y);
                w.SetDirection(new Vec2(-radial.Y * 0.9 - radial.X * 0.5,
                                        radial.X * 0.9 - radial.Y * 0.5));
                return;
            }

            var food = SnakeAi.FindNearestFood(head, w.Foods);
            if (food != null)
                w.SetDirection(SnakeAi.Norm(food.Position.X - head.X, food.Position.Y - head.Y));
        }

        private static bool NearWall(SnakeWorld w, Vec2 head, double safe)
        {
            double halfW = w.WorldHalfW, halfH = w.WorldHalfH;
            return Math.Abs(head.X) > halfW - safe || Math.Abs(head.Y) > halfH - safe;
        }

        /// <summary>
        /// 跟随玩家的 2D 正交相机（与运行时一致）：世界像素与屏幕像素 1:1，
        /// 相机中心 = 玩家蛇头，取景半高 = 预览分辨率半高。
        /// </summary>
        private static void PositionCamera(SnakeView.SnakeController ctrl, SnakeWorld w)
        {
            var cam = Camera.main;
            if (cam == null) return;

            // 抑制控制器的自动取景：批量模式下 Screen 尺寸与 RT 不一致，会覆盖这里的设定
            ctrl.SuppressAutoCamera = true;
            cam.orthographic = true;
            cam.orthographicSize = H * 0.5f;
            var head = w.Player.Head;
            cam.transform.position = new Vector3((float)head.X, (float)head.Y, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 3000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.043f, 0.051f, 0.066f, 1f);
        }

        // ==================================================================
        //  HUD 像素复合
        // ==================================================================
        private static readonly Color32 Amber = new Color32(0xE8, 0xA8, 0x3C, 0xFF);
        private static readonly Color32 Cyan = new Color32(0x4A, 0xD8, 0xE8, 0xFF);
        private static readonly Color32 Crimson = new Color32(0xD8, 0x3A, 0x3A, 0xFF);
        private static readonly Color32 PanelBg = new Color32(0x16, 0x1A, 0x21, 0xEC);
        private static readonly Color32 BarBg = new Color32(0x0C, 0x0F, 0x14, 0xD8);
        private static readonly Color32 Edge = new Color32(0x3A, 0x42, 0x50, 0xFF);
        private static readonly Color32 Dim = new Color32(0x93, 0x9E, 0xAE, 0xFF);
        private static readonly Color32 Magenta = new Color32(0xFF, 0x64, 0xC8, 0xFF);

        private static Color32[] _px;

        private static void CompositeHud(Texture2D tex, SnakeView.SnakeController ctrl)
        {
            _px = tex.GetPixels32();
            var w = ctrl.World;

            // 左上：分数 / 长度 / 存活时间 / 模式附加数值。
            // 用 7 段数码管绘制——批量渲染里 OnGUI 不会画进 RenderTexture，
            // 没有字体可用，因此数字必须自己画。
            Rect(16, 16, 300, 108, PanelBg);
            Rect(18, 16, 284, 3, Amber);
            Border(16, 16, 300, 108, Edge);

            Number(28, 34, w.Player.Score, 22, Amber);          // 分数
            Number(150, 40, w.PlayerBodyCells, 14, Cyan);       // 体长（格）
            Number(28, 70, (int)w.Time, 12, Dim);               // 存活秒数

            if (w.Mode == SnakeMode.Timed)
            {
                Number(150, 70, w.PlayerLevel, 12, Amber);      // 等级
                Number(215, 70, w.TimedAliveCount, 12, Crimson);// 存活数
            }
            else if (w.Mode == SnakeMode.Extraction)
            {
                Number(150, 70, w.ContainersSearched, 12, Cyan);        // 已搜容器
                Number(215, 70, w.ContainersTotal, 12, Dim);            // 容器总数
            }

            // 顶部中：模式目标进度
            int pw = 340, px0 = W / 2 - pw / 2;
            Rect(px0, 16, pw, 44, PanelBg);
            Border(px0, 16, pw, 44, Edge);

            float pct; Color32 col;
            switch (w.Mode)
            {
                case SnakeMode.Classic:
                    pct = Mathf.Clamp01((float)(w.Player.Score / SnakeWorld.ClassicWinScore));
                    col = Amber; break;
                case SnakeMode.Extreme:
                    pct = Mathf.Clamp01((float)(w.Time / SnakeWorld.ExtremeWinTime));
                    col = Cyan; break;
                case SnakeMode.Timed:
                    pct = Mathf.Clamp01(w.ShrinkCount / 3f);
                    col = Crimson; break;
                case SnakeMode.Team4v4:
                    pct = Mathf.Clamp01((float)((w.CaptureProgress + SnakeWorld.CaptureWinProgress)
                        / (2 * SnakeWorld.CaptureWinProgress)));
                    col = w.InCaptureZone ? Cyan : Amber; break;
                default:
                    pct = w.ContainersTotal > 0
                        ? Mathf.Clamp01((float)w.ContainersSearched / w.ContainersTotal) : 0f;
                    col = w.IsPlayerSearching ? Cyan : Amber; break;
            }
            Rect(px0 + 14, 32, pw - 28, 8, BarBg);
            Rect(px0 + 14, 32, (int)((pw - 28) * pct), 8, col);

            // 右上：倍速
            int bw = 52, bh = 30, gap = 6;
            int bx0 = W - (bw * 3 + gap * 2) - 16;
            for (int i = 0; i < 3; i++)
            {
                int bx = bx0 + i * (bw + gap);
                bool active = ctrl.SpeedIndex == i;
                Rect(bx, 16, bw, bh, active ? new Color32(0x4A, 0x36, 0x14, 0xF6) : PanelBg);
                Border(bx, 16, bw, bh, active ? Amber : Edge);
                if (active) Rect(bx, 16, bw, 3, Amber);
                int bars = i == 0 ? 1 : i == 1 ? 2 : 4;
                for (int k = 0; k < bars; k++)
                    Rect(bx + 8 + k * 8, 24, 5, 14, active ? Amber : Dim);
            }

            // 左下：生效中的效果
            int ey = H - 40;
            if (w.ShieldRemaining > 0)
            { Rect(16, ey, 130, 20, PanelBg); Segments(24, ey + 6, (int)(w.ShieldRemaining * 10), 200, 4, 8, Cyan); ey -= 24; }
            if (w.SpeedBoostTimer > 0)
            { Rect(16, ey, 130, 20, PanelBg); Segments(24, ey + 6, (int)(w.SpeedBoostTimer * 10), 200, 4, 8, Amber); ey -= 24; }
            if (w.MagnetTimer > 0)
            { Rect(16, ey, 130, 20, PanelBg); Segments(24, ey + 6, (int)(w.MagnetTimer * 10), 200, 4, 8, Magenta); }

            // 底部中：操作提示条
            Rect(W / 2 - 260, H - 42, 520, 26, PanelBg);
            Rect(W / 2 - 250, H - 30, 500, 2, Cyan);

            // 结算
            if (w.Outcome != MatchOutcome.Running)
            {
                bool won = w.Outcome == MatchOutcome.Won;
                Rect(0, 0, W, H, new Color32(0x00, 0x00, 0x00, 0x8C));
                int ex = W / 2 - 220, eyy = H / 2 - 60;
                Rect(ex, eyy, 440, 120, PanelBg);
                Rect(ex, eyy, 440, 4, won ? Amber : Crimson);
                Border(ex, eyy, 440, 120, won ? Amber : Crimson);
                Rect(ex + 20, eyy + 20, 400, 32, won ? Amber : Crimson);
                Segments(ex + 20, eyy + 74, w.Player.Score, 100, 6, 12, Amber);
                Segments(ex + 240, eyy + 74, w.PlayerBodyCells, 40, 6, 12, Cyan);
            }

            tex.SetPixels32(_px);
            tex.Apply();
        }

        private static int Row(int yFromTop) => H - 1 - yFromTop;

        private static void Rect(int x, int yTop, int w, int h, Color32 c)
        {
            if (w <= 0 || h <= 0 || c.a == 0) return;
            int x0 = Mathf.Clamp(x, 0, W - 1), x1 = Mathf.Clamp(x + w, 0, W);
            int y0 = Mathf.Clamp(yTop, 0, H), y1 = Mathf.Clamp(yTop + h, 0, H);
            for (int yy = y0; yy < y1; yy++)
            {
                int row = Row(yy) * W;
                for (int xx = x0; xx < x1; xx++)
                    _px[row + xx] = Blend(_px[row + xx], c);
            }
        }

        private static void Border(int x, int yTop, int w, int h, Color32 c)
        {
            Rect(x, yTop, w, 1, c);
            Rect(x, yTop + h - 1, w, 1, c);
            Rect(x, yTop, 1, h, c);
            Rect(x + w - 1, yTop, 1, h, c);
        }

        /// <summary>
        /// 用 7 段数码管绘制一个非负整数。
        /// 批量渲染里没有字体可用（IMGUI 的 OnGUI 不会画进 RenderTexture），
        /// 因此 HUD 上的数字必须自己画。数码管只需 7 个矩形即可表示 0-9，
        /// 比逐像素描字简单得多，且在小尺寸下依然清晰。
        /// </summary>
        /// <param name="digitHeight">单个数码管的高度（宽度自动取其 0.58 倍）。</param>
        private static void Number(int x, int yTop, int value, int digitHeight, Color32 c)
        {
            if (value < 0) value = 0;
            string s = value.ToString();
            int dw = Mathf.Max(4, (int)(digitHeight * 0.58f));
            int thick = Mathf.Max(2, digitHeight / 7);
            int gap = Mathf.Max(2, dw / 4);
            int step = dw + gap;

            for (int i = 0; i < s.Length; i++)
                SevenSegment(x + i * step, yTop, dw, digitHeight, thick, s[i] - '0', c);
        }

        /// <summary>七段数码管：段位 a(上) b(右上) c(右下) d(下) e(左下) f(左上) g(中)。</summary>
        private static void SevenSegment(int x, int yTop, int w, int h, int t, int digit, Color32 c)
        {
            bool[][] map =
            {
                new[]{true, true, true, true, true, true, false},    // 0
                new[]{false,true, true, false,false,false,false},    // 1
                new[]{true, true, false,true, true, false,true },    // 2
                new[]{true, true, true, true, false,false,true },    // 3
                new[]{false,true, true, false,false,true, true },    // 4
                new[]{true, false,true, true, false,true, true },    // 5
                new[]{true, false,true, true, true, true, true },    // 6
                new[]{true, true, true, false,false,false,false},    // 7
                new[]{true, true, true, true, true, true, true },    // 8
                new[]{true, true, true, true, false,true, true },    // 9
            };
            if (digit < 0 || digit > 9) return;
            var on = map[digit];

            int midY = yTop + h / 2;
            int halfH = h / 2 - t;

            if (on[0]) Rect(x + t, yTop, w - 2 * t, t, c);              // a
            if (on[1]) Rect(x + w - t, yTop + t, t, halfH, c);          // b
            if (on[2]) Rect(x + w - t, midY, t, halfH, c);              // c
            if (on[3]) Rect(x + t, yTop + h - t, w - 2 * t, t, c);      // d
            if (on[4]) Rect(x, midY, t, halfH, c);                      // e
            if (on[5]) Rect(x, yTop + t, t, halfH, c);                  // f
            if (on[6]) Rect(x + t, midY - t / 2, w - 2 * t, t, c);      // g
        }

        private static void Segments(int x, int yTop, int value, int max, int segW, int segH, Color32 c)
        {
            if (max <= 0) return;
            int n = Mathf.Clamp(value, 0, max);
            int perRow = Mathf.Max(1, 200 / (segW + 2));
            for (int i = 0; i < n; i++)
                Rect(x + (i % perRow) * (segW + 2), yTop + (i / perRow) * (segH + 2), segW, segH, c);
        }

        private static Color32 Blend(Color32 dst, Color32 src)
        {
            if (src.a == 255) return src;
            int a = src.a, ia = 255 - a;
            return new Color32(
                (byte)((src.r * a + dst.r * ia) / 255),
                (byte)((src.g * a + dst.g * ia) / 255),
                (byte)((src.b * a + dst.b * ia) / 255),
                255);
        }
    }
}
