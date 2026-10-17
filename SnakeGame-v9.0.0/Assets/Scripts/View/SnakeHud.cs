using System;
using System.Collections.Generic;
using ArknightsTD.Snake;
using UnityEngine;

namespace ArknightsTD.SnakeView
{
    /// <summary>
    /// 贪吃蛇 HUD。布局与内容逐项对齐 WPF 8.0.0 的 DrawHUD：
    /// 右上分数卡 / 左上击杀卡 / 右下 FPS / 经典进度条 / 极限倍率与生存进度 /
    /// 占领双向条 / 淘汰之王等级卡·实时排行·敌头标签·小地图·决斗卡 /
    /// 搜打撤搜索遮罩·敌头血条 / 连击卡 / 效果卡 / 慈悲·惩罚徽章 / 飘分文字。
    /// </summary>
    public sealed class SnakeHud : MonoBehaviour
    {
        private SnakeController _ctrl;

        private GUIStyle _label, _small, _big, _tiny, _popup;
        private Texture2D _panelTex, _accentTex, _cyanTex, _crimsonTex, _barBgTex,
                          _blueTex, _redTex, _yellowTex, _blackTex, _whiteTex, _greenTex;
        private Texture2D _hoverTex, _borderTex;

        private static readonly Color32 Amber = new Color32(0xFF, 0x8C, 0x00, 0xFF);
        private static readonly Color32 Cyan = new Color32(0x00, 0xB2, 0xD2, 0xFF);
        private static readonly Color32 Crimson = new Color32(0xE8, 0x11, 0x23, 0xFF);
        private static readonly Color32 Win11Card = new Color32(0xF3, 0xF3, 0xF3, 0xFF);
        private static readonly Color32 Win11Text = new Color32(0x16, 0x16, 0x16, 0xFF);
        private static readonly Color32 Win11Text2 = new Color32(0x60, 0x60, 0x60, 0xFF);
        private static readonly Color32 DarkCard = new Color32(0x14, 0x14, 0x14, 0xDC);
        private static readonly Color32 Green = new Color32(0x00, 0xA0, 0x50, 0xFF);
        private static readonly Color32 RedBar = new Color32(0xC8, 0x3C, 0x3C, 0xFF);

        private int _rankTick;

        public void Bind(SnakeController ctrl) { _ctrl = ctrl; }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _panelTex = Solid(Win11Card);
            _accentTex = Solid(Amber);
            _cyanTex = Solid(Cyan);
            _crimsonTex = Solid(Crimson);
            _barBgTex = Solid(new Color32(0xDD, 0xDD, 0xDD, 0xFF));
            _blueTex = Solid(new Color32(0x00, 0x00, 0xFF, 0xFF));
            _redTex = Solid(new Color32(0xFF, 0x00, 0x00, 0xFF));
            _yellowTex = Solid(new Color32(0xFF, 0xD7, 0x00, 0xFF));
            _blackTex = Solid(new Color32(0, 0, 0, 0xCC));
            _hoverTex = Solid(new Color32(0xFF, 0xE0, 0xB3, 0xFF));     // 悬停浅琥珀
            _borderTex = Solid(new Color32(0xC8, 0xC8, 0xC8, 0xFF));    // 浅灰边框
            _whiteTex = Solid(new Color32(0xFF, 0xFF, 0xFF, 0xFF));
            _greenTex = Solid(Green);

            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            _label.normal.textColor = Win11Text;
            _small = new GUIStyle(_label) { fontSize = 12 };
            _tiny = new GUIStyle(_label) { fontSize = 11 };
            _big = new GUIStyle(_label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _popup = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static GUIStyle Col(GUIStyle baseStyle, Color c)
        {
            var s = new GUIStyle(baseStyle);
            s.normal.textColor = c;
            return s;
        }

        private void OnGUI()
        {
            var boot = GetComponent<SnakeBootstrap>();
            if (boot == null) boot = FindObjectOfType<SnakeBootstrap>();
            if (boot == null || boot.State != BootstrapState.Playing) return;

            if (_ctrl == null) return;
            if (boot.Controller != _ctrl) return;
            if (_ctrl.World == null) return;
            EnsureStyles();
            var w = _ctrl.World;
            float s = (float)w.Scale;

            DrawScorePopups(w);

            if (w.Mode == SnakeMode.Timed)
            {
                DrawTimedInfoCard(w, s);
                DrawTimedRanking(w, s);
                DrawEnemyLevelLabels(w, s);
                DrawRaceCard(w, s);
                DrawMiniMap(w);
                DrawBadges(w, s);
                DrawFpsCard(s);
                DrawEffects(w, s, top: 200);
            }
            else
            {
                DrawScoreCard(w, s);
                DrawKillsCard(w, s);
                if (w.Mode == SnakeMode.Classic) DrawClassicProgress(w, s);
                else if (w.Mode == SnakeMode.Extreme)
                {
                    DrawClassicProgress(w, s);   // 极速用生存进度条
                    DrawExtremeMultiplier(w, s);
                }
                else if (w.Mode == SnakeMode.Team4v4) DrawCaptureUI(w, s);
                DrawBadges(w, s);
                DrawFpsCard(s);
                DrawEffects(w, s, top: (w.Mode == SnakeMode.Classic || w.Mode == SnakeMode.Extreme) ? 104 : 70);
            }

            DrawComboDisplay(w, s);
            if (w.Mode == SnakeMode.Extraction)
            {
                DrawEnemyHealthBars(w, s);
                DrawSearchOverlay(w, s);
                DrawMiniMap(w);
            }

            DrawSpeedButtons();
            DrawHint(w);
            DrawEndScreen(w);
        }

        // ---------------- 世界坐标 → 屏幕坐标 ----------------
        private Vector2 WorldToGui(SnakeWorld w, double x, double y)
        {
            var cam = Camera.main;
            if (cam == null) return new Vector2(float.NaN, float.NaN);
            var sp = cam.WorldToScreenPoint(new Vector3((float)x, (float)y, -cam.transform.position.z));
            return new Vector2(sp.x, Screen.height - sp.y);
        }

        private bool OnScreen(Vector2 p, float padX = 100, float padY = 80)
            => !float.IsNaN(p.x) && p.x > -padX && p.x < Screen.width + padX
               && p.y > -padY && p.y < Screen.height + padY;

        // ---------------- 飘分文字（原版 DrawScorePopups：18px 加粗，按生命淡出）----------------
        private void DrawScorePopups(SnakeWorld w)
        {
            for (int i = 0; i < w.Popups.Count; i++)
            {
                var p = w.Popups[i];
                float alpha = Mathf.Max(0, Mathf.Min(1, (float)(p.Life / p.MaxLife)));
                if (alpha < 0.02f) continue;
                var sp = WorldToGui(w, p.Position.X, p.Position.Y);
                if (!OnScreen(sp, 80, 40)) continue;

                var prev = GUI.color;
                GUI.color = new Color(p.Color.R / 255f, p.Color.G / 255f, p.Color.B / 255f, alpha);
                GUI.Label(new Rect(sp.x - 60, sp.y - 14, 120, 28), p.Text, _popup);
                GUI.color = prev;
            }
        }

        // ---------------- 通用卡片（原版 DrawHUD）----------------
        private void DrawScoreCard(SnakeWorld w, float s)
        {
            var r = new Rect(Screen.width - 220 * s, 20 * s, 200 * s, 40 * s);
            GUI.DrawTexture(r, _panelTex);
            string text = $"分数: {w.Score + w.FractionalScore:F1}";
            GUI.Label(new Rect(r.x + 10 * s, r.y + 10 * s, r.width - 20, 20), text, Col(_label, Win11Text));
        }

        private void DrawKillsCard(SnakeWorld w, float s)
        {
            var r = new Rect(20 * s, 20 * s, 140 * s, 40 * s);
            GUI.DrawTexture(r, _panelTex);
            GUI.Label(new Rect(r.x + 10 * s, r.y + 10 * s, r.width - 20, 20),
                $"击杀: {w.CurrentGameKills}", Col(_label, Win11Text));
        }

        private void DrawFpsCard(float s)
        {
            var r = new Rect(Screen.width - 120 * s, Screen.height - 50 * s, 100 * s, 30 * s);
            GUI.DrawTexture(r, _panelTex);
            int fps = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.deltaTime));
            GUI.Label(new Rect(r.x + 5 * s, r.y + 5 * s, 90, 20), $"FPS: {fps}", Col(_small, Win11Text));
        }

        // ---------------- 经典进度（原版 240×26 卡 + 绿色进度条）----------------
        private void DrawClassicProgress(SnakeWorld w, float s)
        {
            var card = new Rect(20 * s, 70 * s, 240 * s, 26 * s);
            GUI.DrawTexture(card, _panelTex);

            bool classic = w.Mode == SnakeMode.Classic;
            double pct = classic
                ? Math.Min(1.0, w.Score / SnakeWorld.ClassicWinScore)
                : Math.Min(1.0, w.Time / SnakeWorld.ExtremeWinTime);

            var barBg = new Rect(card.x + 6 * s, card.y + 10 * s, card.width - 12 * s, 6 * s);
            GUI.DrawTexture(barBg, _barBgTex);
            GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * (float)pct, barBg.height),
                classic ? _greenTex : Solid(RedBar));

            string text = classic
                ? $"{w.Score} / {SnakeWorld.ClassicWinScore:0}"
                : $"存活 {FmtTime(w.Time)} / {FmtTime(SnakeWorld.ExtremeWinTime)}";
            GUI.Label(new Rect(card.x, card.y + 3 * s, card.width, 20),
                text, new GUIStyle(_tiny) { alignment = TextAnchor.MiddleCenter });
        }

        private void DrawExtremeMultiplier(SnakeWorld w, float s)
        {
            var r = new Rect(20 * s, 100 * s, 260 * s, 26 * s);
            GUI.DrawTexture(r, _panelTex);
            GUI.Label(new Rect(r.x + 10 * s, r.y + 5 * s, 240, 18),
                $"倍率 x{w.ScoreMultiplier:F1}   食物雨 {Math.Max(0, (int)w.FoodRainCountdown)}s",
                Col(_tiny, Win11Text));
        }

        private static string FmtTime(double seconds)
        {
            int sec = Math.Max(0, (int)seconds);
            return $"{sec / 60:D2}:{sec % 60:D2}";
        }

        // ---------------- 占领 UI（原版 DrawCaptureUI：双向条 + 人数）----------------
        private void DrawCaptureUI(SnakeWorld w, float s)
        {
            var card = new Rect(Screen.width * 0.5f - 250 * s, 20 * s, 500 * s, 60 * s);
            GUI.DrawTexture(card, _panelTex);

            float barW = 400 * s, barH = 20 * s;
            var bar = new Rect(card.x + (card.width - barW) / 2f, card.y + 10 * s, barW, barH);
            GUI.DrawTexture(bar, _barBgTex);
            // 双向条：蓝色从左边、红色从右边（原版 blueW=(30+p)/60、redW=(30-p)/60）
            float blueW = (float)((30 + w.CaptureProgress) / 60.0 * barW);
            float redW = (float)((30 - w.CaptureProgress) / 60.0 * barW);
            GUI.DrawTexture(new Rect(bar.x, bar.y, blueW, bar.height), _blueTex);
            GUI.DrawTexture(new Rect(bar.x + barW - redW, bar.y, redW, bar.height), _redTex);

            GUI.Label(new Rect(card.x + 10 * s, card.y + 35 * s, 120, 20),
                $"蓝队: {w.BlueAliveCount}人", Col(_small, new Color32(0x00, 0x00, 0xFF, 0xFF)));
            GUI.Label(new Rect(card.x + card.width - 120 * s, card.y + 35 * s, 110, 20),
                $"红队: {w.RedAliveCount}人", Col(_small, new Color32(0xFF, 0x00, 0x00, 0xFF)));
        }

        // ---------------- 淘汰之王（原版 TimedMode.DrawUI + DrawTimedRanking）----------------
        private void DrawTimedInfoCard(SnakeWorld w, float s)
        {
            var card = new Rect(20 * s, 20 * s, 360 * s, 36 * s);
            GUI.DrawTexture(card, _blackTex);

            var levelStyle = new GUIStyle(_small) { fontStyle = FontStyle.Bold };
            levelStyle.normal.textColor = LevelGuiColor(w.PlayerLevel);
            GUI.Label(new Rect(card.x + 12 * s, card.y + 9 * s, 90, 20),
                $"等级 {w.PlayerLevel}", levelStyle);

            string info;
            if (w.IsRacing)
                info = $"剩余 {w.TimedAliveCount}   决斗中";
            else
                info = $"剩余 {w.TimedAliveCount}   缩圈 {Math.Max(0, (int)w.PoisonShrinkTimer)}s   按F决斗";
            GUI.Label(new Rect(card.x + 100 * s, card.y + 9 * s, card.width - 110, 20),
                info, Col(_small, Color.white));
        }

        private static Color LevelGuiColor(int level)
        {
            if (level >= 10) return new Color32(0xFF, 0xD7, 0x00, 0xFF);
            if (level >= 7) return new Color32(0xA8, 0x55, 0xF7, 0xFF);
            if (level >= 4) return new Color32(0x4A, 0x90, 0xD0, 0xFF);
            return new Color32(0x4A, 0xC8, 0x6A, 0xFF);
        }

        private void DrawRaceCard(SnakeWorld w, float s)
        {
            if (!w.IsRacing)
            {
                if (w.RaceResultTimer > 0 && !string.IsNullOrEmpty(w.RaceResultMessage))
                {
                    var st = new GUIStyle(_label) { fontSize = 28, fontStyle = FontStyle.Bold };
                    st.normal.textColor = w.RaceResultMessage.Contains("胜") ? Amber : Color.white;
                    GUI.Label(new Rect(0, Screen.height * 0.2f - 20, Screen.width, 40),
                        w.RaceResultMessage, new GUIStyle(st) { alignment = TextAnchor.MiddleCenter });
                }
                return;
            }

            var card = new Rect(Screen.width * 0.5f - 250 * s, 70 * s, 500 * s, 60 * s);
            GUI.DrawTexture(card, _panelTex);

            var title = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color32(0x8B, 0x00, 0x00, 0xFF);
            GUI.Label(new Rect(card.x + 20 * s, card.y + 8 * s, card.width - 40, 22),
                $"决斗中！{Math.Max(0, SnakeTimedMode.RaceTimeout - w.RaceTimer):F1}s", title);

            int oi = w.RaceOpponentIndex;
            if (oi >= 0 && oi < w.Enemies.Count && w.Enemies[oi].Points.Count > 0)
            {
                double pd = SnakeWorld.Dist(w.Player.Head, w.RaceDestination);
                double od = SnakeWorld.Dist(w.Enemies[oi].Head, w.RaceDestination);
                GUI.Label(new Rect(card.x + 20 * s, card.y + 32 * s, card.width - 40, 20),
                    $"你: {pd:F0}    对手: {od:F0}", Col(_small, Win11Text));
            }
        }

        private readonly List<(int score, string name)> _rankCache = new List<(int, string)>();

        private void DrawTimedRanking(SnakeWorld w, float s)
        {
            // 每 10 帧重排一次（原版 _rankingCacheTick % 10）
            if (_rankTick % 10 == 0)
            {
                _rankTick = 0;
                _rankCache.Clear();
                _rankCache.Add((w.Score, "玩家"));
                for (int i = 0; i < w.Enemies.Count; i++)
                    _rankCache.Add((w.Enemies[i].Score, w.Enemies[i].Name));
                _rankCache.Sort((a, b) => b.score.CompareTo(a.score));
            }
            _rankTick++;

            var card = new Rect(Screen.width - 220 * s, 70 * s, 200 * s, 200 * s);
            GUI.DrawTexture(card, _panelTex);
            GUI.Label(new Rect(card.x + 10 * s, card.y + 10 * s, card.width - 20, 20),
                $"实时排行 (共{_rankCache.Count}人)", Col(_small, Win11Text));

            int n = Math.Min(5, _rankCache.Count);
            for (int i = 0; i < n; i++)
            {
                bool isMe = _rankCache[i].name == "玩家";
                var st = new GUIStyle(_small);
                st.normal.textColor = isMe ? new Color32(0x00, 0x8A, 0x00, 0xFF) : Win11Text;
                if (isMe) st.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(card.x + 10 * s, card.y + 40 * s + i * 25 * s, card.width - 20, 20),
                    $"{i + 1}. {_rankCache[i].name}: {_rankCache[i].score}", st);
            }
        }

        private void DrawEnemyLevelLabels(SnakeWorld w, float s)
        {
            int shown = 0;
            for (int i = 0; i < w.Enemies.Count && shown < 30; i++)
            {
                var enemy = w.Enemies[i];
                if (enemy.Points.Count == 0) continue;
                var p = WorldToGui(w, enemy.Head.X, enemy.Head.Y);
                if (!OnScreen(p, 80, 40)) continue;

                int level = w.EnemyLevel(i);
                var st = new GUIStyle(_small) { fontStyle = FontStyle.Bold };
                st.normal.textColor = LevelGuiColor(level);

                var content = new GUIContent($"{level} {enemy.Name}");
                var size = st.CalcSize(content);
                GUI.DrawTexture(new Rect(p.x - size.x / 2 - 4 * s, p.y - 34 * s, size.x + 8 * s, size.y + 4 * s), _blackTex);
                GUI.Label(new Rect(p.x - size.x / 2, p.y - 32 * s, size.x + 8 * s, size.y + 4 * s), content, st);
                shown++;
            }
        }

        // ---------------- 搜打撤：敌头血条 + 搜索遮罩 ----------------
        private void DrawEnemyHealthBars(SnakeWorld w, float s)
        {
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var enemy = w.Enemies[i];
                if (enemy.Points.Count == 0) continue;
                var p = WorldToGui(w, enemy.Head.X, enemy.Head.Y);
                if (!OnScreen(p, 100, 80)) continue;

                var bg = enemy.Lives >= 3 ? new Color32(0x1E, 0x82, 0x1E, 0xDC)
                    : enemy.Lives == 2 ? new Color32(0xC8, 0x82, 0x1E, 0xDC)
                    : new Color32(0xC8, 0x1E, 0x1E, 0xDC);

                var st = new GUIStyle(_small) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                st.normal.textColor = Color.white;
                var content = new GUIContent($"♥ {enemy.Lives}");
                var size = st.CalcSize(content);
                var r = new Rect(p.x - size.x / 2 - 6, p.y - 42 * s, size.x + 12, size.y + 6);
                GUI.DrawTexture(r, Solid(bg));
                GUI.Label(r, content, st);
            }
        }

        private void DrawSearchOverlay(SnakeWorld w, float s)
        {
            if (!w.IsPlayerSearching) return;

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Solid(new Color32(0, 0, 0, 0xB4)));

            float barW = 300 * s, barH = 30 * s;
            var bar = new Rect(Screen.width * 0.5f - barW * 0.5f, Screen.height * 0.5f + 80 * s, barW, barH);
            GUI.DrawTexture(bar, _barBgTex);
            double progress = Math.Min(1, w.SearchProgress / Math.Max(0.001, w.CurrentSearchTime));
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * (float)progress, bar.height),
                Solid(new Color32(0x00, 0xFF, 0x00, 0xFF)));

            string text = w.SearchReady ? $"搜索完成！再按 F 领取 +{w.SearchScore}"
                                         : $"搜索中... {(int)(progress * 100)}%";
            var st = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            st.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width * 0.5f - barW * 0.5f, Screen.height * 0.5f + 30 * s, barW, 34), text, st);

            // 搜索完成：容器上方显示分数颜色圈
            if (w.SearchReady)
            {
                var p = WorldToGui(w, w.SearchCirclePos.X, w.SearchCirclePos.Y);
                if (OnScreen(p, 200, 200))
                {
                    var col = SnakeExtractionMode.ScoreColor(w.SearchScore);
                    GUI.DrawTexture(new Rect(p.x - 25, p.y - 25, 50, 50), Solid(new Color32(col.R, col.G, col.B, 0xFF)));
                    var st2 = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter };
                    st2.normal.textColor = Color.white;
                    GUI.Label(new Rect(p.x - 25, p.y - 25, 50, 50), $"+{w.SearchScore}", st2);
                }
            }
        }

        // ---------------- 小地图（原版：左下 150×150）----------------
        private void DrawMiniMap(SnakeWorld w)
        {
            float size = 150f;
            var r = new Rect(20, Screen.height - size - 20, size, size);
            GUI.DrawTexture(r, _blackTex);

            double worldW = w.WorldWidth, worldH = w.WorldHeight;
            float MapX(double x) => r.x + (float)((x + worldW / 2) / worldW * size);
            float MapY(double y) => r.y + (float)((y + worldH / 2) / worldH * size);

            void Dot(double x, double y, float rad, Color32 c)
            {
                var p = new Vector2(MapX(x), MapY(y));
                GUI.DrawTexture(new Rect(p.x - rad, p.y - rad, rad * 2, rad * 2), Solid(c));
            }

            if (w.Mode == SnakeMode.Timed)
            {
                // 毒圈
                var center = new Vector2(MapX(w.PoisonCenter.X), MapY(w.PoisonCenter.Y));
                float pr = (float)(w.PoisonRadius / worldW * size);
                DrawCircleOutline(center, pr, 1f, Crimson);
                if (w.IsRacing)
                    Dot(w.RaceDestination.X, w.RaceDestination.Y, 3f, new Color32(0xFF, 0xD7, 0x00, 0xFF));
            }

            if (w.Mode == SnakeMode.Extraction)
            {
                Dot(w.ExtractionPoint.X, w.ExtractionPoint.Y, 3f, new Color32(0x00, 0x8A, 0x00, 0xFF));
                var containers = w.Containers;
                if (containers != null)
                    for (int i = 0; i < containers.Count; i++)
                        if (!containers[i].IsSearched)
                            Dot(containers[i].Position.X, containers[i].Position.Y, 1f, new Color32(0xFF, 0xD7, 0x00, 0xFF));
            }

            if (w.Player.Points.Count > 0)
                Dot(w.Player.Head.X, w.Player.Head.Y, 2f, new Color32(0x00, 0x00, 0xFF, 0xFF));

            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                if (e.Points.Count == 0) continue;
                Dot(e.Head.X, e.Head.Y, 1f, new Color32(0xFF, 0x00, 0x00, 0xFF));
            }
        }

        /// <summary>用细矩形拼一个圆环（IMGUI 无圆弧）。</summary>
        private void DrawCircleOutline(Vector2 center, float radius, float thickness, Color32 c)
        {
            int segs = 48;
            for (int i = 0; i < segs; i++)
            {
                float a = i * 2f * Mathf.PI / segs;
                var p = new Vector2(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius);
                GUI.DrawTexture(new Rect(p.x - thickness * 0.5f, p.y - thickness * 0.5f, thickness, thickness), Solid(c));
            }
        }

        // ---------------- 连击（原版 DrawComboDisplay：彩色卡片 + 白条）----------------
        private void DrawComboDisplay(SnakeWorld w, float s)
        {
            if (w.ComboCount < 2 || w.ComboTimer <= 0) return;

            float cw = 180 * s, ch = 44 * s;
            var r = new Rect(Screen.width * 0.5f - cw * 0.5f, 90 * s, cw, ch);

            var bg = w.ComboCount >= 5 ? new Color32(0xFF, 0x5A, 0x00, 0xFF)
                : w.ComboCount >= 3 ? new Color32(0xFF, 0xA5, 0x00, 0xFF)
                : new Color32(0xFF, 0xC8, 0x0A, 0xFF);
            GUI.DrawTexture(r, Solid(bg));

            var st = new GUIStyle(_label) { fontSize = 22, fontStyle = FontStyle.Bold };
            st.normal.textColor = Color.white;
            GUI.Label(new Rect(r.x, r.y + 5 * s, r.width, 24), $"连击 ×{w.ComboCount}",
                new GUIStyle(st) { alignment = TextAnchor.MiddleCenter });

            float barW = cw - 10 * s;
            var barBg = new Rect(r.x + 5 * s, r.y + ch - 7 * s, barW, 4 * s);
            GUI.DrawTexture(barBg, Solid(new Color32(0, 0, 0, 0x5A)));
            GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * (float)(w.ComboTimer / SnakeWorld.ComboWindow), barBg.height),
                _whiteTex);
        }

        // ---------------- 效果卡（原版 DrawActiveEffects：深色卡 + 竖条进度）----------------
        private void DrawEffects(SnakeWorld w, float s, float top)
        {
            float y = top * s;
            float cw = 150 * s, ch = 30 * s, gap = 6 * s;

            if (w.SpeedBoostTimer > 0)
            { DrawEffectCard(20 * s, y, cw, ch, "加速", w.SpeedBoostTimer, SnakeWorld.SpeedBoostDuration, new Color32(0xFF, 0xC8, 0x0A, 0xFF)); y += ch + gap; }
            if (w.ShieldRemaining > 0)
            { DrawEffectCard(20 * s, y, cw, ch, "护盾", w.ShieldRemaining, SnakeWorld.ShieldDuration, new Color32(0x50, 0xB4, 0xFF, 0xFF)); y += ch + gap; }
            if (w.MagnetTimer > 0)
            { DrawEffectCard(20 * s, y, cw, ch, "磁铁", w.MagnetTimer, SnakeWorld.MagnetDuration, new Color32(0xFF, 0x64, 0xC8, 0xFF)); y += ch + gap; }
            if (w.InvincibleTimer > 0)
            { DrawEffectCard(20 * s, y, cw, ch, "无敌", w.InvincibleTimer, 1.5, new Color32(0xFF, 0xDC, 0x64, 0xFF)); }

            if (w.Mode == SnakeMode.Timed)
            {
                if (w.OutsideTimer > 0)
                    GUI.Label(new Rect(20 * s, y, 260, 20),
                        $"⚠ 圈外！{Math.Max(0, SnakeTimedMode.PoisonTimeout - w.OutsideTimer):0.0}s",
                        Col(_small, Crimson));
            }
            if (w.Mode == SnakeMode.Extraction && w.FireExposureTimer > 0)
                GUI.Label(new Rect(20 * s, y, 260, 20),
                    $"🔥 火中！{Math.Max(0, 5 - w.FireExposureTimer):0.0}s",
                    Col(_small, Crimson));

            if (_ctrl.Paused)
                GUI.Label(new Rect(20 * s, y + 24, 200, 20), "已暂停（P 继续）", Col(_small, Crimson));
        }

        private void DrawEffectCard(float x, float y, float w, float h,
            string label, double remaining, double total, Color32 accent)
        {
            GUI.DrawTexture(new Rect(x, y, w, h), Solid(DarkCard));
            double pct = Math.Max(0, Math.Min(1, remaining / total));
            GUI.DrawTexture(new Rect(x + 3, y + 4, 4, (h - 8) * (float)pct), Solid(accent));
            var st = new GUIStyle(_small);
            st.normal.textColor = Color.white;
            GUI.Label(new Rect(x + 14, y + (h - 18) / 2f, w - 16, 18), $"{label}  {remaining:F1}s", st);
        }

        // ---------------- 慈悲 / 惩罚徽章（原版 DrawMercyBadge / DrawPunishBadge）----------------
        private void DrawBadges(SnakeWorld w, float s)
        {
            if (w.IsMercyRound)
            {
                var card = new Rect(Screen.width * 0.5f - 90 * s, 20 * s, 180 * s, 34 * s);
                GUI.DrawTexture(card, Solid(new Color32(0xFF, 0x64, 0x64, 0xDC)));
                var st = new GUIStyle(_label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                st.normal.textColor = Color.white;
                GUI.Label(card, "💖 补偿局", st);
            }
            else if (w.IsPunishRound)
            {
                float pulse = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
                var card = new Rect(Screen.width * 0.5f - 110 * s, 20 * s, 220 * s, 38 * s);
                GUI.DrawTexture(card, Solid(new Color32(0xC8, 0x14, 0x14, (byte)(255 * pulse))));
                var st = new GUIStyle(_label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                st.normal.textColor = Color.white;
                GUI.Label(card, "⚡ 神仙局 ⚡", st);
            }
        }

        // ---------------- 倍速 ----------------
        private void DrawSpeedButtons()
        {
            float bw = 52f, bh = 30f, gap = 6f;
            float x0 = Screen.width - (bw * 3 + gap * 2) - 16f;
            float y = Screen.height - bh - 56f;
            var names = new[] { "1x", "2x", "4x" };

            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(x0 + i * (bw + gap), y, bw, bh);
                bool active = _ctrl.SpeedIndex == i;
                GUI.DrawTexture(r, active ? Solid(new Color32(0xD6, 0xE4, 0xF7, 0xF6)) : _panelTex);
                if (active) GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), _accentTex);
                if (GUI.Button(r, names[i], new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter }))
                    _ctrl.SetSpeedIndex(i);
            }
        }

        // ---------------- 操作提示 ----------------
        private void DrawHint(SnakeWorld w)
        {
            var r = new Rect(Screen.width * 0.5f - 300f, Screen.height - 42f, 600f, 26f);
            GUI.DrawTexture(r, _panelTex);
            string hint = "鼠标转向    Ctrl 加速    Space 倍速    P 暂停    R 重开";
            if (w.Mode == SnakeMode.Timed)
                hint = "按 F 挑战 600px 内最近对手决斗    " + hint;
            else if (w.Mode == SnakeMode.Extraction)
                hint = "靠近容器按 F 搜索，读满后再按 F 领取；到撤离点（绿圈）停留 9 秒撤离    " + hint;
            GUI.Label(r, hint, new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
        }

        // ---------------- 结算 ----------------
        /// <summary>
        /// 自绘按钮：贴图 + 文字 + 悬停高亮 + 点击（不依赖引擎默认 GUI.Button 皮肤）。
        /// </summary>
        private bool SelfButton(Rect r, string text)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            GUI.DrawTexture(r, hover ? _hoverTex : _panelTex);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), hover ? _accentTex : _borderTex);
            GUI.Label(r, text, new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter });
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        private void DrawEndScreen(SnakeWorld w)
        {
            if (w.Outcome == MatchOutcome.Running) return;

            bool won = w.Outcome == MatchOutcome.Won;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                Solid(new Color32(0, 0, 0, 0x8C)));

            var box = new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.5f - 130f, 480, 260);
            GUI.DrawTexture(box, _panelTex);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 3f), won ? _accentTex : _crimsonTex);

            string winReason = "";
            if (won)
            {
                if (w.Mode == SnakeMode.Classic) winReason = "达成 3000 分";
                else if (w.Mode == SnakeMode.Extreme) winReason = "存活 3 分钟";
                else if (w.Mode == SnakeMode.Team4v4) winReason = "占领区域成功";
                else if (w.Mode == SnakeMode.Extraction) winReason = "撤离成功";
            }

            GUI.Label(new Rect(box.x, box.y + 24, box.width, 40),
                won ? "胜利" : "阵亡",
                new GUIStyle(_big)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = won ? Amber : Crimson }
                });

            GUI.Label(new Rect(box.x, box.y + 68, box.width, 22),
                $"分数 {w.Score:0.#}    击杀 {w.CurrentGameKills}    存活 {w.Time:0.0}s",
                new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter });
            if (won && !string.IsNullOrEmpty(winReason))
                GUI.Label(new Rect(box.x, box.y + 90, box.width, 20),
                    winReason, new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });

            // 连胜 / 慈悲进度（原版结算画面）
            int streak = ArknightsTD.Core.SnakeProgress.ConsecutiveWins;
            if (!won && streak > 0)
                GUI.Label(new Rect(box.x, box.y + 112, box.width, 20),
                    $"连胜 {streak} 场已中断", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            if (won && streak >= 9)
                GUI.Label(new Rect(box.x, box.y + 112, box.width, 20),
                    "🔥 下一局是神仙局！", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Crimson } });

            var newly = _ctrl.NewAchievements;
            if (newly != null && newly.Count > 0)
            {
                string achText = "解锁成就：" + newly[0].Name;
                for (int i = 1; i < newly.Count; i++) achText += "、" + newly[i].Name;
                GUI.Label(new Rect(box.x, box.y + 134, box.width, 20),
                    achText, new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            }

            // 三个自绘按钮（照搬原版结算界面：重新开始 / 切换模式；新增返回主菜单）
            if (SelfButton(new Rect(box.x + 16, box.y + 196, 140, 38), "重开 (R)"))
                _ctrl.Restart();
            if (SelfButton(new Rect(box.x + 168, box.y + 196, 140, 38), "切换模式"))
                _ctrl.SwitchMode(NextMode(w.Mode));
            if (SelfButton(new Rect(box.x + 320, box.y + 196, 140, 38), "返回主菜单"))
            {
                var boot = GetComponent<SnakeBootstrap>();
                if (boot == null) boot = FindObjectOfType<SnakeBootstrap>();
                if (boot != null) boot.ShowStartScreen();
            }
        }

        private static SnakeMode NextMode(SnakeMode m)
        {
            switch (m)
            {
                case SnakeMode.Classic: return SnakeMode.Timed;
                case SnakeMode.Timed: return SnakeMode.Team4v4;
                case SnakeMode.Team4v4: return SnakeMode.Extreme;
                case SnakeMode.Extreme: return SnakeMode.Extraction;
                default: return SnakeMode.Classic;
            }
        }
    }
}
