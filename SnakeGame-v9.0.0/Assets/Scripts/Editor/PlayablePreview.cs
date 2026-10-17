using System.IO;
using ArknightsTD.Gameplay;
using ArknightsTD.Data;
using ArknightsTD.View;
using UnityEngine;
using UnityEditor;

namespace ArknightsTD.EditorTools
{
    /// <summary>
    /// 渲染"可玩关卡"的真实运行状态：创建 <see cref="View.BattleController"/> →
    /// 用真实部署接口铺开局 → 推进模拟 → 渲染 3D 战场 → 复合 HUD → 落盘 PNG。
    /// <para>
    /// 意义：<b>无需进入 Play 模式</b>即可真实执行并验证整个运行时链路
    /// （战场构建、干员/敌人/弹丸视图、控制器状态机、HUD 布局）。
    /// </para>
    /// <para>
    /// 调用：Tuanjie.exe -batchmode -quit -projectPath &lt;工程&gt;
    ///       -executeMethod ArknightsTD.EditorTools.PlayablePreview.Run
    /// 注意不要加 -nographics。输出：Artifacts/playable_preview.png
    /// </para>
    /// </summary>
    public static class PlayablePreview
    {
        private const int W = 1600;
        private const int H = 900;

        public static void Run()
        {
            // ---------- 1. 创建真实运行时控制器 ----------
            var host = new GameObject("BattleHost");
            var ctrl = host.AddComponent<View.BattleController>();
            ctrl.LevelIndex = 1;
            ctrl.RandomSeed = 20240;
            ctrl.SetupField();

            var sim = ctrl.Sim;
            Debug.Log($"[PlayablePreview] battlefield ready {sim.GridW}x{sim.GridH} " +
                      $"staticTris={ctrl.Field.TotalTriangles} cost={sim.Cost} lives={sim.Lives}");

            // ---------- 2. 用真实部署接口铺开局 ----------
            // 起始费用只够少量干员，因此边推进边部署 —— 这就是真实约束下的玩法。
            var plan = new (OperatorKind kind, int col, int row, Facing facing)[]
            {
                (OperatorKind.Guard,  0, 4, Facing.Right),
                (OperatorKind.Guard,  0, 2, Facing.Right),
                (OperatorKind.Sniper, 3, 2, Facing.Down),
                (OperatorKind.Sniper, 8, 6, Facing.Up),
                (OperatorKind.Caster, 5, 1, Facing.Down),
                (OperatorKind.Caster, 9, 1, Facing.Down),
                (OperatorKind.Sniper, 11, 1, Facing.Down),
            };

            int deployed = 0, planIndex = 0;
            for (int i = 0; i < (int)(26.0 / BattleSim.FixedStep); i++)
            {
                ctrl.Tick(BattleSim.FixedStep);
                while (planIndex < plan.Length)
                {
                    var p = plan[planIndex];
                    if (sim.Cost < TDBalance.Operator(p.kind).Cost) break;
                    string reason;
                    if (sim.TryDeployOperator(p.kind, p.col, p.row, p.facing, out reason)) deployed++;
                    else Debug.Log($"[PlayablePreview] deploy {p.kind}@({p.col},{p.row}) failed: {reason}");
                    planIndex++;
                }
            }
            Debug.Log($"[PlayablePreview] deployed {deployed} operators, cost={sim.Cost}");

            // ---------- 3. 推进到有战斗画面的时刻 ----------
            int guard = 0;
            while (sim.Time < 46.0 && sim.Phase == BattlePhase.Running && guard++ < 60 * 900)
                ctrl.Tick(BattleSim.FixedStep);

            ctrl.SyncViews();
            Debug.Log($"[PlayablePreview] sim state: {ctrl.DescribeState()}");
            Debug.Log($"[PlayablePreview] view counts: enemies={ctrl.Enemies.VisibleCount} " +
                      $"operators={ctrl.Operators.VisibleCount} projectiles={ctrl.Projectiles.VisibleCount}");

            // ---------- 4. 渲染 3D ----------
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[PlayablePreview] no main camera"); EditorApplication.Exit(3); return; }

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

            // ---------- 5. 复合 HUD 到像素（不用 IMGUI，保证一定出现在图里）----------
            CompositeHud(tex, ctrl, deployed);

            var outDir = Path.Combine(Directory.GetCurrentDirectory(), "Artifacts");
            Directory.CreateDirectory(outDir);
            var outPath = Path.Combine(outDir, "playable_preview.png");
            var png = tex.EncodeToPNG();
            File.WriteAllBytes(outPath, png);

            var px = tex.GetPixels32();
            int nonBg = 0;
            for (int i = 0; i < px.Length; i++)
                if (px[i].r > 18 || px[i].g > 18 || px[i].b > 22) nonBg++;
            double pct = 100.0 * nonBg / px.Length;

            // ---- 颜色直方图：客观判断画面里到底有哪些颜色 ----
            Histogram(tex, sim);

            Debug.Log($"[PlayablePreview] wrote {W}x{H} -> {outPath} nonBackground={pct:0.0}%");
            Debug.Log($"[PlayablePreview] SELFCHECK fileExists={File.Exists(outPath)} bytes={png.Length} " +
                      $"deployed={deployed} visibleEnemies={ctrl.Enemies.VisibleCount} " +
                      $"visibleOperators={ctrl.Operators.VisibleCount}");

            Object.DestroyImmediate(host);
            EditorApplication.Exit(0);
        }

        // ==================================================================
        //  可读性采样
        // ==================================================================

        /// <summary>
        /// 把每个格子的中心投影到屏幕并采样渲染后的颜色，
        /// 按格类型汇总亮度。用来客观判断"路径与地面是否分得清"，
        /// 而不是靠肉眼看图下结论。
        /// </summary>
        private static void Histogram(Texture2D tex, BattleSim sim)
        {
            var px = tex.GetPixels32();
            var counts = new System.Collections.Generic.Dictionary<int, int>();
            int magenta = 0;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                int key = (c.r >> 3 << 10) | (c.g >> 3 << 5) | (c.b >> 3);
                int n;
                counts.TryGetValue(key, out n);
                counts[key] = n + 1;
                if (c.r > 60 && c.b > 60 && c.r > c.g + 25 && c.b > c.g + 25) magenta++;
            }

            var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>>(counts);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 8 && i < list.Count; i++)
            {
                int k = list[i].Key;
                int r = ((k >> 10) & 31) << 3, g = ((k >> 5) & 31) << 3, b = (k & 31) << 3;
                sb.Append($"#{r:X2}{g:X2}{b:X2}={100.0 * list[i].Value / px.Length:0.0}% ");
            }
            Debug.Log($"[PlayablePreview] 颜色直方图 Top8: {sb}");
            Debug.Log($"[PlayablePreview] 洋红像素占比 = {100.0 * magenta / px.Length:0.00}% （>5% 说明地面顶点色真的生效）");
        }

        private static void ProbeCellColors(Texture2D tex, Camera cam, BattleSim sim)
        {
            double sumDeploy = 0, sumPath = 0, sumObstacle = 0;
            int nDeploy = 0, nPath = 0, nObstacle = 0;
            float minDeploy = 999, maxDeploy = -1, minPath = 999, maxPath = -1;

            for (int c = 0; c < sim.GridW; c++)
                for (int r = 0; r < sim.GridH; r++)
                {
                    var world = new Vector3(c + 0.5f, (float)BattlefieldBuilder.GroundTop, r + 0.5f);
                    var sp = cam.WorldToScreenPoint(world);
                    if (sp.z <= 0) continue;

                    int x = Mathf.RoundToInt(sp.x);
                    int y = Mathf.RoundToInt(sp.y);
                    if (x < 0 || x >= W || y < 0 || y >= H) continue;

                    var col = tex.GetPixel(x, y);
                    float lum = 0.2126f * col.r + 0.7152f * col.g + 0.0722f * col.b;

                    switch (sim.Grid[c, r])
                    {
                        case BattleSim.CellType.Path:
                            sumPath += lum; nPath++;
                            if (lum < minPath) minPath = lum;
                            if (lum > maxPath) maxPath = lum;
                            break;
                        case BattleSim.CellType.Obstacle:
                            sumObstacle += lum; nObstacle++;
                            break;
                        default:
                            sumDeploy += lum; nDeploy++;
                            if (lum < minDeploy) minDeploy = lum;
                            if (lum > maxDeploy) maxDeploy = lum;
                            break;
                    }
                }

            double avgDeploy = nDeploy > 0 ? sumDeploy / nDeploy : 0;
            double avgPath = nPath > 0 ? sumPath / nPath : 0;
            double avgObs = nObstacle > 0 ? sumObstacle / nObstacle : 0;

            Debug.Log($"[PlayablePreview] 可读性采样: 可部署格亮度={avgDeploy:0.000}({nDeploy}格, " +
                      $"{minDeploy:0.000}~{maxDeploy:0.000})  路径格亮度={avgPath:0.000}({nPath}格, " +
                      $"{minPath:0.000}~{maxPath:0.000})  障碍格亮度={avgObs:0.000}({nObstacle}格)");
            Debug.Log($"[PlayablePreview] 路径/地面亮度比 = " +
                      $"{(avgDeploy > 1e-6 ? avgPath / avgDeploy : 0):0.000} " +
                      $"（<0.75 才算路线清晰可辨）");
        }

        // ==================================================================
        //  HUD 像素复合
        // ==================================================================

        private static readonly Color32 Amber = new Color32(0xE8, 0xA8, 0x3C, 0xFF);
        private static readonly Color32 Cyan = new Color32(0x4A, 0xD8, 0xE8, 0xFF);
        private static readonly Color32 Crimson = new Color32(0xD8, 0x3A, 0x3A, 0xFF);
        private static readonly Color32 PanelBg = new Color32(0x16, 0x1A, 0x21, 0xEC);
        private static readonly Color32 CardBg = new Color32(0x20, 0x26, 0x2F, 0xF2);
        private static readonly Color32 CardSel = new Color32(0x4A, 0x36, 0x14, 0xF6);
        private static readonly Color32 BarBg = new Color32(0x0C, 0x0F, 0x14, 0xD8);

        private static Color32[] _px;

        private static void CompositeHud(Texture2D tex, View.BattleController ctrl, int deployed)
        {
            _px = tex.GetPixels32();
            var sim = ctrl.Sim;

            // 画面坐标原点在左下，而 HUD 习惯从左上都：转换 y
            // 注意 GetPixels32 的行序是自下而上，因此 yFromTop -> row = H-1-y

            // ---- 左上：关卡 / 生命 / 费用 ----
            Rect(16, 16, 268, 84, PanelBg);
            Rect(18, 16, 268 - 16, 3, Amber);
            Bar(28, 16 + 84 - 12, 268 - 24, 4, BarBg);
            float lpct = sim.MaxLives > 0 ? Mathf.Clamp01((float)sim.Lives / sim.MaxLives) : 0f;
            Bar(28, 16 + 84 - 12, (int)((268 - 24) * lpct), 4, Crimson);
            // 费用/生命刻度：用小块表示数值，避免依赖字体
            Segments(28, 16 + 30, sim.Lives, Mathf.Max(1, sim.MaxLives), 6, 10, Crimson);
            Segments(180, 16 + 26, sim.Cost, 60, 5, 10, Amber);
            Border(16, 16, 268, 84, new Color32(0x3A, 0x42, 0x50, 0xFF));

            // ---- 顶部中间：波次进度 ----
            int waveX = W / 2 - 160, waveY = 16;
            Rect(waveX, waveY, 320, 34, PanelBg);
            Border(waveX, waveY, 320, 34, new Color32(0x3A, 0x42, 0x50, 0xFF));
            int totalWaves = Mathf.Max(1, sim.Level.TotalWaves);
            Bar(waveX + 12, waveY + 24, (int)((320 - 24) * Mathf.Clamp01((float)sim.CurrentWave / totalWaves)), 4, Cyan);

            // ---- 击破数 ----
            Rect(waveX + 332, waveY, 140, 34, PanelBg);
            Border(waveX + 332, waveY, 140, 34, new Color32(0x3A, 0x42, 0x50, 0xFF));
            Segments(waveX + 344, waveY + 12, sim.Score, 40, 5, 12, Amber);

            // ---- 操作提示条 ----
            Rect(W / 2 - 200, 58, 400, 26, PanelBg);
            Bar(W / 2 - 190, 70, 380, 2, Cyan);

            // ---- 右上：倍速按钮 ----
            int bw = 52, bh = 30, gap = 6;
            int x0 = W - (bw * 3 + gap * 2) - 16;
            string[] names = { "1x", "2x", "4x" };
            for (int i = 0; i < 3; i++)
            {
                int bx = x0 + i * (bw + gap);
                bool active = ctrl.SpeedIndex == i;
                Rect(bx, 16, bw, bh, active ? CardSel : PanelBg);
                Border(bx, 16, bw, bh, active ? Amber : new Color32(0x3A, 0x42, 0x50, 0xFF));
                if (active) Bar(bx, 16, bw, 3, Amber);
                // 用竖条数表示倍速（1/2/4 倍 -> 1/2/4 条）
                for (int k = 0; k < (i == 0 ? 1 : i == 1 ? 2 : 4); k++)
                    Bar(bx + 8 + k * 7, 26, 4, 12, active ? Amber : new Color32(0x93, 0x9E, 0xAE, 0xFF));
            }

            // ---- 底部右侧：干员卡 ----
            var defs = TDBalance.Operators;
            int cw = 108, ch = 124, cgap = 10;
            int totalWid = defs.Length * cw + (defs.Length - 1) * cgap;
            int cx0 = W - totalWid - 20;
            int cy0 = H - ch - 20;
            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                int x = cx0 + i * (cw + cgap);
                bool affordable = sim.Cost >= def.Cost;
                bool selected = ctrl.SelectedOperatorIndex == i && ctrl.Phase != DeployPhase.Idle;

                Rect(x, cy0, cw, ch, selected ? CardSel : CardBg);
                Border(x, cy0, cw, ch, selected ? Amber : new Color32(0x3A, 0x42, 0x50, 0xFF));
                if (affordable) Bar(x, cy0, cw, 3, Amber);
                if (selected) Bar(x, cy0, cw, 3, Cyan);

                // 职业色块代替立绘
                Rect(x + 14, cy0 + 10, cw - 28, 48, new Color32(def.Color.R, def.Color.G, def.Color.B, 255));
                Border(x + 14, cy0 + 10, cw - 28, 48, new Color32(0xFF, 0xFF, 0xFF, 0x60));

                // 费用与伤害用小条表示
                Segments(x + 14, cy0 + 76, def.Cost, 30, 4, 8, affordable ? Amber : new Color32(0x60, 0x69, 0x76, 0xFF));
                Segments(x + 14, cy0 + 96, def.Damage, 10, 4, 8, Cyan);

                if (!affordable)
                    Rect(x, cy0, cw, ch, new Color32(0x00, 0x00, 0x00, 0x70));
            }

            // ---- 已部署干员数（左上角下方）----
            Segments(28, 120, deployed, 10, 6, 9, Cyan);

            // ---- 结算 ----
            if (sim.Phase != BattlePhase.Running)
            {
                bool won = sim.Phase == BattlePhase.Won;
                int bx = W / 2 - 220, by = H / 2 - 60;
                Rect(0, 0, W, H, new Color32(0x00, 0x00, 0x00, 0x8C));
                Rect(bx, by, 440, 120, PanelBg);
                Bar(bx, by, 440, 4, won ? Amber : Crimson);
                Border(bx, by, 440, 120, won ? Amber : Crimson);
                // 用大色块表示胜负：胜=琥珀金，败=赤红
                Rect(bx + 20, by + 22, 400, 34, won ? Amber : Crimson);
                Segments(bx + 20, by + 74, sim.Score, 60, 6, 12, Amber);
                Segments(bx + 240, by + 74, sim.Lives, Mathf.Max(1, sim.MaxLives), 6, 12, Crimson);
            }

            tex.SetPixels32(_px);
            tex.Apply();

            Debug.Log($"[PlayablePreview] HUD composited: lives={sim.Lives}/{sim.MaxLives} cost={sim.Cost} " +
                      $"wave={sim.CurrentWave}/{sim.Level.TotalWaves} score={sim.Score} " +
                      $"phase={sim.Phase} cards={defs.Length} deployed={deployed}");
        }

        // 画面 y 自上而下；像素数组自下而上
        private static int Row(int yFromTop) => H - 1 - yFromTop;

        private static void Rect(int x, int yTop, int w, int h, Color32 c)
        {
            if (w <= 0 || h <= 0) return;
            int x0 = Mathf.Clamp(x, 0, W - 1);
            int x1 = Mathf.Clamp(x + w, 0, W);
            int y0 = Mathf.Clamp(yTop, 0, H);
            int y1 = Mathf.Clamp(yTop + h, 0, H);

            for (int yy = y0; yy < y1; yy++)
            {
                int row = Row(yy) * W;
                for (int xx = x0; xx < x1; xx++)
                {
                    int i = row + xx;
                    _px[i] = Blend(_px[i], c);
                }
            }
        }

        /// <summary>细边框。</summary>
        private static void Border(int x, int yTop, int w, int h, Color32 c)
        {
            Rect(x, yTop, w, 1, c);
            Rect(x, yTop + h - 1, w, 1, c);
            Rect(x, yTop, 1, h, c);
            Rect(x + w - 1, yTop, 1, h, c);
        }

        /// <summary>进度条（只画填充部分）。</summary>
        private static void Bar(int x, int yTop, int w, int h, Color32 c)
            => Rect(x, yTop, Mathf.Max(0, w), h, c);

        /// <summary>用若干小方块表示数值（免字体）。</summary>
        private static void Segments(int x, int yTop, int value, int max, int segW, int segH, Color32 c)
        {
            if (max <= 0) return;
            int n = Mathf.Clamp(value, 0, max);
            for (int i = 0; i < n; i++)
                Rect(x + i * (segW + 2), yTop, segW, segH, c);
        }

        private static Color32 Blend(Color32 dst, Color32 src)
        {
            if (src.a == 255) return src;
            int a = src.a;
            int ia = 255 - a;
            return new Color32(
                (byte)((src.r * a + dst.r * ia) / 255),
                (byte)((src.g * a + dst.g * ia) / 255),
                (byte)((src.b * a + dst.b * ia) / 255),
                255);
        }
    }
}
