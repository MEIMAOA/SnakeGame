using System;
using ArknightsTD.Data;
using ArknightsTD.Gameplay;
using UnityEngine;

namespace ArknightsTD.View
{
    /// <summary>
    /// 战斗 HUD —— 明日方舟风格。
    /// <para>
    /// 布局（对齐明日方舟）：
    /// <list type="bullet">
    /// <item>左上：关卡编号/名称 + 生命菱形 + 分数</item>
    /// <item>顶部中间：波次圆点 + 波次进度 + 场上敌人 + 下一波倒计时</item>
    /// <item>右上：暂停 / 倍速（1× 2× 4×）</item>
    /// <item>左下：费用面板（大号数字）</item>
    /// <item>右下：干员卡（职业标 + 名称 + 费用 + 可用性遮罩 + 数字键）</item>
    /// <item>底部中间：当前操作提示 + 取消按钮</item>
    /// <item>选中已部署干员：左上弹出「撤退」面板</item>
    /// </list>
    /// 全部 IMGUI 自绘，不依赖 Canvas/Prefab。
    /// </para>
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        private BattleController _ctrl;

        // ---- 供控制器消费的点击状态（OnGUI 期间记录，Update 期间消费）----
        private int _cardClick = -1;
        private int _speedClick = -1;
        private bool _pauseClick;
        private bool _retreatClick;
        private bool _cancelClick;
        private bool _menuClick;
        private bool _startBattleClick;
        private bool _skillClick;
        private int _clickFrame = -1;

        // 卡片区域（给控制器做拖拽命中判定）
        private readonly Rect[] _cardRects = new Rect[8];
        private int _cardRectCount;

        // ---- 样式 ----
        private GUIStyle _label, _small, _tiny, _big, _num, _cardTitle;
        private Texture2D _panelTex, _panelDeepTex, _cardTex, _cardSelTex;
        private Texture2D _accentTex, _goldTex, _cyanTex, _redTex, _greenTex, _dimTex;

        // ---- 明日方舟配色 ----
        private static readonly Color32 PanelBg = new Color32(0x11, 0x16, 0x1D, 0xF2);
        private static readonly Color32 PanelDeep = new Color32(0x0A, 0x0D, 0x12, 0xF6);
        private static readonly Color32 CardBg = new Color32(0x1B, 0x22, 0x2B, 0xF4);
        private static readonly Color32 CardSel = new Color32(0x2C, 0x36, 0x44, 0xFA);
        private static readonly Color32 Accent = new Color32(0x35, 0xB6, 0xE8, 0xFF);   // 蓝
        private static readonly Color32 Gold = new Color32(0xF2, 0xC1, 0x4E, 0xFF);     // 金
        private static readonly Color32 Cyan = new Color32(0x5F, 0xE3, 0xC8, 0xFF);     // 青绿
        private static readonly Color32 Red = new Color32(0xE4, 0x48, 0x3F, 0xFF);
        private static readonly Color32 Green = new Color32(0x54, 0xD0, 0x8A, 0xFF);
        private static readonly Color32 TextMain = new Color32(0xEC, 0xF1, 0xF6, 0xFF);
        private static readonly Color32 TextDim = new Color32(0x8E, 0x9A, 0xA8, 0xFF);

        private static readonly string[] ClassGlyph = { "近", "狙", "术" };
        private static readonly string[] ClassName = { "近卫", "狙击", "术师" };

        public void Bind(BattleController ctrl) { _ctrl = ctrl; }

        // ==================================================================
        //  点击消费
        // ==================================================================

        private bool Fresh => Time.frameCount == _clickFrame;

        public bool TryConsumeCardClick(out int index)
        {
            index = Fresh ? _cardClick : -1;
            _cardClick = -1;
            return index >= 0;
        }

        public bool TryConsumeSpeedClick(out int index)
        {
            index = Fresh ? _speedClick : -1;
            _speedClick = -1;
            return index >= 0;
        }

        public bool TryConsumePauseClick()
        {
            bool v = Fresh && _pauseClick;
            _pauseClick = false;
            return v;
        }

        public bool TryConsumeRetreatClick()
        {
            bool v = Fresh && _retreatClick;
            _retreatClick = false;
            return v;
        }

        public bool TryConsumeCancelClick()
        {
            bool v = Fresh && _cancelClick;
            _cancelClick = false;
            return v;
        }

        public bool TryConsumeMenuClick()
        {
            bool v = Fresh && _menuClick;
            _menuClick = false;
            return v;
        }

        public bool TryConsumeStartBattleClick()
        {
            bool v = Fresh && _startBattleClick;
            _startBattleClick = false;
            return v;
        }

        public bool TryConsumeSkillClick()
        {
            bool v = Fresh && _skillClick;
            _skillClick = false;
            return v;
        }

        /// <summary>鼠标位置是否落在某张干员卡上（拖拽部署用），返回卡片序号或 -1。</summary>
        public int CardIndexAt(Vector2 guiPos)
        {
            for (int i = 0; i < _cardRectCount; i++)
                if (_cardRects[i].Contains(guiPos)) return i;
            return -1;
        }

        /// <summary>鼠标是否压在 HUD 的可交互区域上（避免穿透到战场）。</summary>
        public bool PointerOverHud(Vector2 guiPos)
        {
            if (CardIndexAt(guiPos) >= 0) return true;
            return false;
        }

        // ==================================================================
        //  样式
        // ==================================================================

        private void EnsureStyles()
        {
            if (_label != null) return;

            _panelTex = MakeTex(PanelBg);
            _panelDeepTex = MakeTex(PanelDeep);
            _cardTex = MakeTex(CardBg);
            _cardSelTex = MakeTex(CardSel);
            _accentTex = MakeTex(Accent);
            _goldTex = MakeTex(Gold);
            _cyanTex = MakeTex(Cyan);
            _redTex = MakeTex(Red);
            _greenTex = MakeTex(Green);
            _dimTex = MakeTex(new Color(0f, 0f, 0f, 0.55f));

            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            _label.normal.textColor = TextMain;
            _small = new GUIStyle(_label) { fontSize = 12 };
            _tiny = new GUIStyle(_label) { fontSize = 10 };
            _big = new GUIStyle(_label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _num = new GUIStyle(_label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _cardTitle = new GUIStyle(_label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private float S => Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.2f);

        // ==================================================================
        //  主绘制
        // ==================================================================

        private void OnGUI()
        {
            if (_ctrl == null || _ctrl.Sim == null) return;
            EnsureStyles();

            DrawTopBar();
            DrawWaveBar();
            DrawControlButtons();
            DrawCostPanel();
            DrawOperatorCards();
            DrawRetreatPanel();
            DrawSkillPanel();
            DrawPrepPanel();
            DrawHintBar();
            DrawMessage();
            DrawDeployRibbon();
            DrawPausedOverlay();
            DrawEndScreen();
        }

        // ---------------- 左上：关卡 / 生命 / 分数 ----------------
        private void DrawTopBar()
        {
            float s = S;
            var sim = _ctrl.Sim;
            var r = new Rect(16 * s, 16 * s, 320 * s, 74 * s);
            GUI.DrawTexture(r, _panelTex);
            GUI.DrawTexture(new Rect(r.x, r.y, 3 * s, r.height), _accentTex);

            GUI.Label(new Rect(r.x + 12 * s, r.y + 6 * s, r.width, 22 * s),
                $"第 {sim.Level.Index} 关   {sim.Level.Name}", WithColor(_label, TextMain));

            // 生命菱形
            int max = Mathf.Clamp(sim.MaxLives, 1, 24);
            float dx = r.x + 12 * s, dy = r.y + 34 * s, size = 11 * s;
            for (int i = 0; i < max; i++)
            {
                var d = new Rect(dx + i * (size + 4 * s), dy, size, size);
                bool alive = i < sim.Lives;
                GUI.DrawTexture(d, alive ? _cyanTex : _dimTex);
                GUI.DrawTexture(new Rect(d.x + size * 0.28f, d.y + size * 0.28f, size * 0.44f, size * 0.44f),
                    alive ? _panelDeepTex : _dimTex);
            }
            GUI.Label(new Rect(r.x + 12 * s, r.y + 50 * s, r.width, 20 * s),
                $"生命 {sim.Lives}/{sim.MaxLives}    击破 {sim.Score}", WithColor(_small, TextDim));
        }

        // ---------------- 顶中：波次 ----------------
        private void DrawWaveBar()
        {
            float s = S;
            var sim = _ctrl.Sim;
            float w = 300 * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, 16 * s, w, 54 * s);
            GUI.DrawTexture(r, _panelTex);

            int total = Mathf.Max(1, sim.Level.TotalWaves);
            float dotY = r.y + 10 * s;
            float dw = Mathf.Min(18 * s, (r.width - 24 * s) / total);
            float startX = r.x + (r.width - dw * total) * 0.5f;
            for (int i = 0; i < total; i++)
            {
                var d = new Rect(startX + i * dw, dotY, dw * 0.62f, 8 * s);
                bool done = i < sim.CurrentWave - 1;
                bool cur = i == sim.CurrentWave - 1;
                GUI.DrawTexture(d, done ? _cyanTex : (cur ? _goldTex : _dimTex));
            }

            if (sim.PrepPhase)
            {
                GUI.Label(new Rect(r.x, r.y + 24 * s, r.width, 22 * s),
                    "准备阶段 · 观察动线并部署干员",
                    new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Cyan } });
            }
            else
            {
            string waveText = $"波次 {sim.CurrentWave}/{total}    场上 {sim.Enemies.Count}";
            if (sim.CurrentWave < total && sim.NextWaveTimer > 0)
                waveText += $"    下一波 {sim.NextWaveTimer:0.0}s";
            else if (sim.CurrentWave >= total && sim.Enemies.Count == 0)
                waveText = "敌军已清空";
            GUI.Label(new Rect(r.x, r.y + 24 * s, r.width, 22 * s),
                waveText, new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            }
        }

        // ---------------- 右上：暂停 / 倍速 ----------------
        private void DrawControlButtons()
        {
            float s = S;
            float bh = 30 * s, bw = 46 * s, gap = 6 * s;
            float y = 16 * s;
            float x = Screen.width - 16 * s;

            // 倍速（倒序绘制，从右往左）
            var names = new[] { "1×", "2×", "4×" };
            for (int i = 2; i >= 0; i--)
            {
                x -= bw;
                var r = new Rect(x, y, bw, bh);
                bool active = _ctrl.SpeedIndex == i;
                GUI.DrawTexture(r, active ? _cardSelTex : _panelTex);
                if (active) GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2 * s), _goldTex);
                GUI.Label(r, names[i], new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
                if (Click(r)) { _speedClick = i; Stamp(); }
                x -= gap;
            }

            // 暂停
            x -= bw;
            var pr = new Rect(x, y, bw, bh);
            GUI.DrawTexture(pr, _ctrl.Paused ? _cardSelTex : _panelTex);
            GUI.Label(pr, _ctrl.Paused ? "▶" : "Ⅱ",
                new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            if (Click(pr)) { _pauseClick = true; Stamp(); }
        }

        // ---------------- 左下：费用 ----------------
        private void DrawCostPanel()
        {
            float s = S;
            var sim = _ctrl.Sim;
            var r = new Rect(16 * s, Screen.height - 96 * s, 150 * s, 80 * s);
            GUI.DrawTexture(r, _panelTex);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3 * s), _goldTex);

            GUI.Label(new Rect(r.x + 12 * s, r.y + 6 * s, r.width, 18 * s),
                "COST", WithColor(_tiny, TextDim));
            GUI.Label(new Rect(r.x + 10 * s, r.y + 20 * s, 70 * s, 44 * s),
                $"{sim.Cost}", WithColor(_num, Gold));
            GUI.Label(new Rect(r.x + 78 * s, r.y + 30 * s, r.width - 86 * s, 34 * s),
                $"+{1}/{TDBalance.CostRegenSeconds:0}s", WithColor(_tiny, TextDim));
        }

        // ---------------- 右下：干员卡 ----------------
        private void DrawOperatorCards()
        {
            float s = S;
            var defs = TDBalance.Operators;
            float cw = 104 * s, ch = 128 * s, gap = 10 * s;
            float totalW = defs.Length * cw + (defs.Length - 1) * gap;
            float x0 = Screen.width - totalW - 16 * s;
            float y0 = Screen.height - ch - 16 * s;

            _cardRectCount = 0;
            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                bool selected = _ctrl.SelectedOperatorIndex == i && _ctrl.Phase != DeployPhase.Idle;
                bool affordable = _ctrl.Cost >= def.Cost;
                bool usable = affordable && _ctrl.Sim.Phase == BattlePhase.Running;

                float lift = selected ? 8 * s : 0f;
                var r = new Rect(x0 + i * (cw + gap), y0 - lift, cw, ch);
                if (i < _cardRects.Length) { _cardRects[i] = r; _cardRectCount = i + 1; }

                GUI.DrawTexture(r, selected ? _cardSelTex : _cardTex);
                if (selected)
                {
                    GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3 * s), _goldTex);
                    GUI.DrawTexture(new Rect(r.x, r.y + r.height - 2 * s, r.width, 2 * s), _goldTex);
                }

                var ac = new Color32(def.Color.R, def.Color.G, def.Color.B, 255);
                var classColor = MakeTex(ac);

                // 数字键角标
                var badge = new Rect(r.x + 4 * s, r.y + 4 * s, 16 * s, 16 * s);
                GUI.DrawTexture(badge, _panelDeepTex);
                GUI.Label(badge, $"{i + 1}", new GUIStyle(_tiny) { alignment = TextAnchor.MiddleCenter });

                // 职业块（代替立绘）
                var face = new Rect(r.x + 10 * s, r.y + 24 * s, cw - 20 * s, 48 * s);
                GUI.DrawTexture(face, classColor);
                GUI.Label(face, ClassGlyph[Mathf.Clamp(i, 0, ClassGlyph.Length - 1)],
                    new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });

                GUI.Label(new Rect(r.x, r.y + 74 * s, cw, 18 * s), ClassName[Mathf.Clamp(i, 0, ClassName.Length - 1)] + " · " + def.Name, _cardTitle);

                // 费用条
                var costBg = new Rect(r.x + 8 * s, r.y + 94 * s, cw - 16 * s, 22 * s);
                GUI.DrawTexture(costBg, _panelDeepTex);
                GUI.DrawTexture(new Rect(costBg.x, costBg.y, 3 * s, costBg.height), usable ? _goldTex : _redTex);
                GUI.Label(costBg, $"{def.Cost}", WithColor(
                    new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter }, usable ? Gold : Red));
                GUI.Label(new Rect(r.x, r.y + 112 * s, cw, 16 * s),
                    $"伤害 {def.Damage} · 阻挡 {def.BlockCount}", WithColor(_tiny, TextDim));

                if (!usable) GUI.DrawTexture(r, _dimTex);

                if (Click(r)) { _cardClick = i; Stamp(); }
            }
        }

        // ---------------- 撤退面板 ----------------
        private void DrawRetreatPanel()
        {
            var op = _ctrl.SelectedOperator;
            if (op == null) return;

            float s = S;
            var def = TDBalance.Operator(op.Kind);
            var r = new Rect(16 * s, Screen.height - 200 * s, 236 * s, 92 * s);
            GUI.DrawTexture(r, _panelTex);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3 * s), _redTex);

            GUI.Label(new Rect(r.x + 12 * s, r.y + 8 * s, r.width - 20 * s, 20 * s),
                $"{def.Name}（已部署）", WithColor(_label, TextMain));
            GUI.Label(new Rect(r.x + 12 * s, r.y + 30 * s, r.width - 20 * s, 18 * s),
                $"朝向 {FacingName(op.Facing)}", WithColor(_tiny, TextDim));

            var btn = new Rect(r.x + 12 * s, r.y + 52 * s, r.width - 24 * s, 30 * s);
            GUI.DrawTexture(btn, _cardSelTex);
            GUI.DrawTexture(new Rect(btn.x, btn.y, btn.width, 2 * s), _redTex);
            GUI.Label(btn, $"撤退（返还 {def.Cost / 2} 费用）",
                new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            if (Click(btn)) { _retreatClick = true; Stamp(); }
        }

        /// <summary>选中干员的技能面板：生命、技力、技能名与释放按钮（明日方舟式）。</summary>
        private void DrawSkillPanel()
        {
            var op = _ctrl.SelectedOperator;
            if (op == null) return;

            float s = S;
            var def = TDBalance.Operator(op.Kind);
            var skill = def.Skill;
            var r = new Rect(16 * s, Screen.height - 316 * s, 236 * s, 104 * s);
            GUI.DrawTexture(r, _panelTex);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3 * s), skill != null && op.SkillReady ? _goldTex : _accentTex);

            // 生命
            float hp = op.MaxHp > 0 ? Mathf.Clamp01((float)op.Hp / op.MaxHp) : 0f;
            GUI.Label(new Rect(r.x + 12 * s, r.y + 6 * s, r.width - 20 * s, 18 * s),
                $"生命 {op.Hp}/{op.MaxHp}", WithColor(_tiny, TextDim));
            var hb = new Rect(r.x + 12 * s, r.y + 24 * s, r.width - 24 * s, 6 * s);
            GUI.DrawTexture(hb, _panelDeepTex);
            GUI.DrawTexture(new Rect(hb.x, hb.y, hb.width * hp, hb.height),
                MakeTex(hp < 0.3f ? Red : Green));

            // 技力
            float sp = (float)op.SpProgress;
            GUI.Label(new Rect(r.x + 12 * s, r.y + 34 * s, r.width - 20 * s, 16 * s),
                skill != null ? $"{skill.Name} · 技力 {op.Sp:0}/{op.SpMax:0}" : "无技能",
                WithColor(_tiny, op.SkillReady ? Gold : TextDim));
            var sb = new Rect(r.x + 12 * s, r.y + 52 * s, r.width - 24 * s, 6 * s);
            GUI.DrawTexture(sb, _panelDeepTex);
            GUI.DrawTexture(new Rect(sb.x, sb.y, sb.width * (op.SkillReady ? 1f : sp), sb.height),
                MakeTex(op.SkillReady ? Gold : Accent));

            // 技能按钮
            var btn = new Rect(r.x + 12 * s, r.y + 64 * s, r.width - 24 * s, 30 * s);
            GUI.DrawTexture(btn, op.SkillReady ? _cardSelTex : _cardTex);
            if (op.SkillReady) GUI.DrawTexture(new Rect(btn.x, btn.y, btn.width, 2 * s), _goldTex);
            string labelText = op.SkillActive ? "技能生效中…"
                             : op.SkillReady ? "释放技能（Q）"
                             : "技力蓄积中…";
            GUI.Label(btn, labelText,
                new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            if (op.SkillReady && Click(btn)) { _skillClick = true; Stamp(); }
        }

        /// <summary>准备阶段：开始行动按钮 + 动线说明。</summary>
        private void DrawPrepPanel()
        {
            if (!_ctrl.PrepPhase) return;
            float s = S;

            var btn = new Rect(Screen.width * 0.5f - 110 * s, Screen.height - 132 * s, 220 * s, 46 * s);
            GUI.DrawTexture(btn, _cardSelTex);
            GUI.DrawTexture(new Rect(btn.x, btn.y, btn.width, 3 * s), _goldTex);
            GUI.Label(btn, "开始行动 (Enter)",
                new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Gold } });
            if (Click(btn)) { _startBattleClick = true; Stamp(); }

            var tip = new Rect(Screen.width * 0.5f - 250 * s, Screen.height - 82 * s, 500 * s, 24 * s);
            GUI.Label(tip, "青色箭头 = 敌人动线　·　先部署干员再开始行动",
                new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Cyan } });
        }

        private static string FacingName(Facing f)
            => f == Facing.Up ? "上" : f == Facing.Down ? "下" : f == Facing.Left ? "左" : "右";

        // ---------------- 底部提示条 ----------------
        private void DrawHintBar()
        {
            float s = S;
            string hint;
            Color col = TextDim;
            switch (_ctrl.Phase)
            {
                case DeployPhase.Placing:
                    hint = "选择部署位置 · 点击高台格放置"; col = Cyan; break;
                case DeployPhase.ChoosingFacing:
                    hint = "拖/点击选择朝向"; col = Gold; break;
                default:
                    if (_ctrl.SelectedOperator != null) hint = "已选中干员：可撤退，或点其它位置取消";
                    else hint = "拖拽干员卡到高台部署 · 点数字键 1/2/3 选卡 · 空格倍速 · P 暂停 · R 重开 · ESC 返回";
                    break;
            }

            var st = WithColor(_small, col);
            var size = st.CalcSize(new GUIContent(hint));
            float w = size.x + 24 * s;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height - 52 * s, w, 28 * s);
            GUI.DrawTexture(r, _panelTex);
            GUI.Label(r, hint, new GUIStyle(st) { alignment = TextAnchor.MiddleCenter });

            // 取消按钮（部署/朝向阶段）
            if (_ctrl.Phase != DeployPhase.Idle)
            {
                var cancel = new Rect(r.xMax + 10 * s, r.y, 68 * s, 28 * s);
                GUI.DrawTexture(cancel, _cardSelTex);
                GUI.Label(cancel, "取消", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
                if (Click(cancel)) { _cancelClick = true; Stamp(); }
            }
        }

        // ---------------- 部署状态条 ----------------
        private void DrawDeployRibbon()
        {
            if (_ctrl.Phase == DeployPhase.Idle) return;
            float s = S;
            var defs = TDBalance.Operators;
            int idx = Mathf.Clamp(_ctrl.SelectedOperatorIndex, 0, defs.Length - 1);
            var def = defs[idx];

            string text = _ctrl.Phase == DeployPhase.Placing
                ? $"部署中：{def.Name}"
                : $"选择朝向：{def.Name}";

            var st = WithColor(_label, Gold);
            float w = st.CalcSize(new GUIContent(text)).x + 30 * s;
            var r = new Rect((Screen.width - w) * 0.5f, 78 * s, w, 30 * s);
            GUI.DrawTexture(r, _panelDeepTex);
            GUI.DrawTexture(new Rect(r.x, r.y, 3 * s, r.height), _goldTex);
            GUI.Label(r, text, new GUIStyle(st) { alignment = TextAnchor.MiddleCenter });
        }

        // ---------------- 消息 ----------------
        private void DrawMessage()
        {
            string msg = _ctrl.CurrentMessage();
            if (string.IsNullOrEmpty(msg)) return;
            float s = S;
            var st = WithColor(_small, Gold);
            var size = st.CalcSize(new GUIContent(msg));
            var r = new Rect((Screen.width - size.x) * 0.5f - 12 * s,
                Screen.height - 150 * s, size.x + 24 * s, 26 * s);
            GUI.DrawTexture(r, _panelDeepTex);
            GUI.Label(r, msg, new GUIStyle(st) { alignment = TextAnchor.MiddleCenter });
        }

        // ---------------- 暂停遮罩 ----------------
        private void DrawPausedOverlay()
        {
            if (!_ctrl.Paused || _ctrl.Sim.Phase != BattlePhase.Running) return;
            float s = S;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                MakeTex(new Color(0f, 0f, 0f, 0.45f)));
            var st = WithColor(_big, TextMain);
            string text = "已暂停";
            var size = st.CalcSize(new GUIContent(text));
            GUI.Label(new Rect((Screen.width - size.x) * 0.5f, Screen.height * 0.42f, size.x, 40 * s), text, st);
            GUI.Label(new Rect(0, Screen.height * 0.42f + 44 * s, Screen.width, 24 * s),
                "P / Ⅱ 继续    ·    R 重开    ·    ESC 返回主菜单",
                new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
        }

        // ---------------- 结算 ----------------
        private void DrawEndScreen()
        {
            var sim = _ctrl.Sim;
            if (sim.Phase == BattlePhase.Running) return;

            float s = S;
            bool won = sim.Phase == BattlePhase.Won;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                MakeTex(new Color(0f, 0f, 0f, 0.62f)));

            var box = new Rect(Screen.width * 0.5f - 250 * s, Screen.height * 0.5f - 120 * s, 500 * s, 240 * s);
            GUI.DrawTexture(box, _panelTex);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 4 * s), won ? _goldTex : _redTex);

            GUI.Label(new Rect(box.x, box.y + 22 * s, box.width, 46 * s),
                won ? "作战成功" : "作战失败",
                new GUIStyle(_big)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = won ? Gold : Red }
                });

            GUI.Label(new Rect(box.x, box.y + 74 * s, box.width, 22 * s),
                $"击破 {sim.Score}    剩余生命 {sim.Lives}/{sim.MaxLives}    用时 {sim.Time:0.0}s",
                new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });

            var again = new Rect(box.x + 44 * s, box.y + 170 * s, 180 * s, 40 * s);
            GUI.DrawTexture(again, _cardSelTex);
            GUI.DrawTexture(new Rect(again.x, again.y, again.width, 2 * s), _goldTex);
            GUI.Label(again, "重打 (R)", new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter });
            if (Click(again)) _ctrl.Restart();

            var menu = new Rect(box.x + 276 * s, box.y + 170 * s, 180 * s, 40 * s);
            GUI.DrawTexture(menu, _cardSelTex);
            GUI.Label(menu, "返回主菜单", new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter });
            if (Click(menu)) { _menuClick = true; Stamp(); }
        }

        // ==================================================================
        //  工具
        // ==================================================================

        private void Stamp() { _clickFrame = Time.frameCount; Event.current.Use(); }

        private static bool Click(Rect r)
            => Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition);

        private static GUIStyle WithColor(GUIStyle baseStyle, Color c)
        {
            var s = new GUIStyle(baseStyle);
            s.normal.textColor = c;
            return s;
        }
    }
}
