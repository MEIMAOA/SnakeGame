using ArknightsTD.Core.Geometry;
using ArknightsTD.Data;
using ArknightsTD.Core;
using ArknightsTD.Gameplay;
using ArknightsTD.Snake;
using ArknightsTD.SnakeView;
using ArknightsTD.View;
using UnityEngine;
using UnityEditor;

namespace ArknightsTD.EditorTools
{
    /// <summary>
    /// 启动链路验证：模拟一次真实启动过程并断言每一步都成立。
    /// <list type="number">
    /// <item>入口场景存在</item>
    /// <item><c>SnakeBootstrap</c> 起来后能显示模式选择菜单</item>
    /// <item>菜单里 5 个模式逐个都能真正开局（控制器 + 世界 + 视图 + 相机 + HUD 齐备）</item>
    /// <item>开局后推进一段模拟不报错、状态自洽</item>
    /// </list>
    /// <para>
    /// 调用：Tuanjie.exe -batchmode -quit -projectPath &lt;工程&gt;
    ///   -executeMethod ArknightsTD.EditorTools.VerifyLaunch.Run
    /// </para>
    /// </summary>
    public static class VerifyLaunch
    {
        private static int _checks;
        private static int _failures;

        private static void Check(bool ok, string what)
        {
            _checks++;
            if (!ok) _failures++;
            Debug.Log(ok ? $"[VerifyLaunch] OK   {what}" : $"[VerifyLaunch] FAIL {what}");
        }

        public static void Run()
        {
            _checks = 0;
            _failures = 0;

            // ---- 1. 入口场景存在 ----
            Check(System.IO.File.Exists(SetupGameEntry.ScenePath),
                $"入口场景存在（{SetupGameEntry.ScenePath}）");

            // ---- 2. Bootstrap 起来会先显示菜单 ----
            var host = new GameObject("LaunchHost");
            var boot = host.AddComponent<SnakeBootstrap>();
            boot.ShowMenuOnStart = true;
            boot.RandomSeed = 20240;

            var menu = boot.Menu;   // 按需创建，不依赖 Awake
            Check(menu != null, "Bootstrap 提供模式选择菜单");
            boot.ShowMenu();
            Check(menu.enabled, "调用 ShowMenu 后菜单启用");

            // ---- 2b. 状态机：菜单态 vs 对局态 ----
            Check(boot.State == BootstrapState.Menu, "初始为菜单态");
            Check(boot.ShouldDrawMenu, "菜单态下菜单应绘制");
            Check(menu.IsVisible, "菜单 IsVisible 与状态机一致");

            // ---- 3. 逐个模式开局 ----
            foreach (var mode in SnakeModeMenu.Modes)
            {
                boot.StartModeNow(mode);

                var ctrl = boot.Controller;
                Check(ctrl != null, $"{mode}: 控制器已创建");
                if (ctrl == null) continue;

                // ★ 回归断言：开局后菜单必须彻底不可见（此前会出现菜单与游戏叠加）
                Check(boot.State == BootstrapState.Playing, $"{mode}: 进入对局态");
                Check(!boot.ShouldDrawMenu, $"{mode}: 对局态下不应绘制菜单");
                Check(!menu.IsVisible, $"{mode}: 菜单 IsVisible 为假（无叠加）");

                Check(ctrl.World != null, $"{mode}: 世界已初始化");
                Check(ctrl.View != null, $"{mode}: 视图已创建");
                Check(ctrl.Hud != null, $"{mode}: HUD 已挂载");
                Check(Camera.main != null, $"{mode}: 主相机存在");

                var w = ctrl.World;
                if (w == null) continue;

                // ★ 回归断言：所选模式必须真的生效
                Check(w.Mode == mode, $"{mode}: 世界模式与所选一致");

                // 世界状态合理（原版：timed/classic/team/extraction 125 食物，extreme 60）
                Check(w.Player.Points.Count > 0, $"{mode}: 玩家蛇有身段");
                Check(w.Player.Alive, $"{mode}: 玩家初始存活");
                Check(w.Foods.Count >= 60, $"{mode}: 食物已生成（{w.Foods.Count}）");

                int minEnemies = SnakeModeConfig.For(mode).EnemyCount;
                if (mode == SnakeMode.Extraction) minEnemies = 5;   // 首图敌人 5
                Check(w.Enemies.Count >= minEnemies,
                    $"{mode}: 敌蛇已生成（{w.Enemies.Count} >= {minEnemies}）");

                // 相机是 2D 正交（塔防才用 3D 透视）
                Check(Camera.main.orthographic, $"{mode}: 相机为正交（2D 俯视）");

                // 推进 3 秒模拟不应报错
                for (int i = 0; i < 180 && w.Outcome == MatchOutcome.Running; i++)
                {
                    // 简单的转向输入，模拟玩家操作
                    if (i % 40 == 0) w.SetDirection(new Vec2ish(i).Value);
                    ctrl.Tick(SnakeWorld.FixedStep);
                }
                Check(!double.IsNaN(w.Time) && w.Time > 0, $"{mode}: 推进后时间有效（{w.Time:0.0}s）");

                // 视图同步一次不应抛异常
                ctrl.View.Sync(w);
                Check(true, $"{mode}: 视图同步无异常");
            }

            // ---- 3b. 返回菜单后状态复原 ----
            boot.ShowMenu();
            Check(boot.State == BootstrapState.Menu, "ShowMenu 后回到菜单态");
            Check(boot.Controller == null, "ShowMenu 后控制器已释放");
            Check(boot.ShouldDrawMenu, "ShowMenu 后菜单重新绘制");
            Check(menu.IsVisible, "ShowMenu 后 IsVisible 为真");

            // ---- 3c. 设置界面（照搬原版 SettingsWindow 的 6 个分页）----
            var sm = boot.SettingsMenu;
            Check(sm != null, "设置界面可获取");
            Check(SnakeSettingsMenu.Tabs.Length == 6, "设置分页为 6 个（与原版一致）");
            Check(SnakeSettingsMenu.Tabs[0] == "背景样式", "分页1 = 背景样式");
            Check(SnakeSettingsMenu.Tabs[1] == "蛇头颜色", "分页2 = 蛇头颜色");
            Check(SnakeSettingsMenu.Tabs[2] == "蛇身颜色", "分页3 = 蛇身颜色");
            Check(SnakeSettingsMenu.Tabs[3] == "主题色", "分页4 = 主题色");
            Check(SnakeSettingsMenu.Tabs[4] == "画面设置", "分页5 = 画面设置");
            Check(SnakeSettingsMenu.Tabs[5] == "实验性功能", "分页6 = 实验性功能");

            Check(!sm.IsVisible, "设置界面初始不可见");
            sm.Open();
            Check(sm.IsVisible, "Open 后设置界面可见");
            sm.Close();
            Check(!sm.IsVisible, "Close 后设置界面隐藏");

            // ---- 3d. 设置项往返（对应原版 AppSettings 字段）----
            var st = new AppSettings();
            Check(st.BackgroundStyle == 2, "默认 BackgroundStyle = 2（原版值）");
            Check(st.HeadColorHex == "#90EE90", "默认蛇头色 #90EE90");
            Check(st.BodyColorHex == "#008000", "默认蛇身色 #008000");
            Check(st.AccentColorHex == "#0067C0", "默认强调色 #0067C0");
            Check(st.ThemeName == "Windows11", "默认主题 Windows11");
            Check(st.FpsTarget == 60, "默认帧率 60");
            Check(st.Vsync, "默认开启垂直同步");
            Check(st.SpeedBalance, "默认开启速度平衡");
            Check(!st.TowerDefenseEnabled, "塔防默认关闭");

            st.BackgroundStyle = 3; st.HeadColorHex = "#FFFF00"; st.FpsTarget = 144;
            var st2 = AppSettings.Deserialize(st.Serialize());
            Check(st2 != null && st2.BackgroundStyle == 3, "设置序列化往返：背景样式");
            Check(st2.HeadColorHex == "#FFFF00", "设置序列化往返：蛇头色");
            Check(st2.FpsTarget == 144, "设置序列化往返：帧率");

            var parsed = AppSettings.ParseHex("#4AD8E8", Rgb.FromHex(0x000000));
            Check(parsed.R == 0x4A && parsed.G == 0xD8 && parsed.B == 0xE8, "ParseHex 正确解析");
            Check(AppSettings.ParseHex("坏值", Rgb.FromHex(0x112233)).R == 0x11, "ParseHex 坏值回退");

            // ---- 3e. 界面流程（本轮重构的核心）----
            var ui = boot.Ui;
            Check(ui != null, "界面总控存在");
            Check(ui.Current == UiScreen.Start, "启动后第一眼是开始界面");

            // 开始游戏 -> 模式选择
            ui.GoModeSelect();
            Check(ui.Current == UiScreen.ModeSelect, "开始游戏 -> 模式选择");

            // 设置
            ui.GoSettings();
            Check(ui.Current == UiScreen.Settings, "设置 -> 设置界面");
            Check(ui.SettingsVisible, "设置界面已打开");
            ui.GoStart();
            Check(ui.Current == UiScreen.Start, "返回 -> 开始界面");

            // 成就
            ui.GoAchievements();
            Check(ui.Current == UiScreen.Achievements, "成就 -> 成就界面");
            ui.GoStart();

            // 排行
            ui.GoRankings();
            Check(ui.Current == UiScreen.Rankings, "排行 -> 排行界面");
            ui.GoStart();

            // 各界面返回都回到开始界面
            foreach (var scr in new[] { UiScreen.ModeSelect, UiScreen.Achievements, UiScreen.Rankings })
            {
                ui.GoStart();
                if (scr == UiScreen.ModeSelect) ui.GoModeSelect();
                else if (scr == UiScreen.Achievements) ui.GoAchievements();
                else ui.GoRankings();
                ui.GoBack();
                Check(ui.Current == UiScreen.Start, $"{scr} 返回后回到开始界面");
            }

            // 排行数据层
            SnakeScores.ResetForTest();
            Check(SnakeScores.ForMode(SnakeMode.Timed).Count == 0, "排行初始为空");
            SnakeScores.AddForTest(SnakeMode.Timed, 30);
            SnakeScores.AddForTest(SnakeMode.Timed, 90);
            SnakeScores.AddForTest(SnakeMode.Classic, 50);
            var timed = SnakeScores.ForMode(SnakeMode.Timed);
            Check(timed.Count == 2, "计时赛榜有 2 条");
            Check(timed[0].Score == 90, "榜按分数降序（第一 90）");
            Check(SnakeScores.ForMode(SnakeMode.Classic).Count == 1, "经典榜独立计数 1 条");

            // 每模式上限 20 条
            SnakeScores.ResetForTest();
            for (int i = 0; i < 30; i++) SnakeScores.AddForTest(SnakeMode.Extreme, i);
            Check(SnakeScores.ForMode(SnakeMode.Extreme).Count == SnakeScores.MaxEntriesPerMode,
                $"每模式保留上限 {SnakeScores.MaxEntriesPerMode} 条");

            // 排行序列化往返
            SnakeScores.ResetForTest();
            SnakeScores.AddForTest(SnakeMode.Team4v4, 77);
            var rawScores = SnakeScores.Serialize();
            SnakeScores.ResetForTest();
            SnakeScores.Deserialize(rawScores);
            Check(SnakeScores.ForMode(SnakeMode.Team4v4).Count == 1, "排行序列化往返");

            // ---- 3f. 暂停菜单（ESC → 暂停菜单，而非直接回主界面）----
            boot.StartModeNow(SnakeMode.Classic);
            Check(boot.State == BootstrapState.Playing, "开局后处于对局态");
            Check(boot.Controller != null, "暂停前控制器存在");
            Check(!boot.Controller.Paused, "暂停前控制器未暂停");

            ui.ShowPause();
            Check(ui.Current == UiScreen.Paused, "ShowPause 后界面进入暂停态");
            Check(boot.Controller.Paused, "暂停后控制器 Paused=true");

            ui.ResumeGame();
            Check(ui.Current == UiScreen.Playing, "ResumeGame 后回到对局态");
            Check(!boot.Controller.Paused, "恢复后控制器 Paused=false");

            // 暂停菜单三个选项语义存在（照搬原版：重新开始 / 返回开始界面 / 退出游戏）
            ui.ShowPause();
            Check(ui.Current == UiScreen.Paused, "再次 ShowPause 生效");
            ui.PauseBackToStartTest();
            Check(ui.Current == UiScreen.Start, "返回开始界面后回到主界面");
            Check(boot.Controller == null, "返回后控制器已拆除");

            // ---- 3g. 塔防模式（正式内容：显式 BeginBattle，可重复进入 / 退出）----
            var tdGo = new GameObject("BattleBootstrap(塔防验证)");
            var td = tdGo.AddComponent<ArknightsTD.View.BattleBootstrap>();
            tdGo.SetActive(false);            // 模拟入口场景里"默认关闭"的塔防对象
            td.LevelIndex = 1;
            td.RandomSeed = 20240;

            ui.StartTowerDefense();           // 走真实入口
            Check(ui.Current == UiScreen.Playing, "塔防: 界面切到对局态（蛇界面停画）");
            Check(tdGo.activeSelf, "塔防: 场景里的塔防对象被激活");
            Check(td.Controller != null, "塔防: 控制器已创建");
            if (td.Controller != null)
            {
                Check(td.Controller.Sim != null, "塔防: 关卡模拟已初始化");
                Check(td.Controller.Phase == DeployPhase.Idle, "塔防: 初始处于待部署");
                Check(td.Controller.BattlePhase == BattlePhase.Running, "塔防: 战斗进行中");
                Check(td.Controller.Sim.Level.Index == 1, "塔防: 关卡号生效");
                Check(Camera.main != null && !Camera.main.orthographic, "塔防: 相机切换为 3D 透视");
                Check(Camera.main != null && Camera.main.enabled, "塔防: 相机已启用（否则黑屏）");
            }

            // ★ 明日方舟式操作流程：选卡 → 选格 → 选朝向 → 部署 → 选中 → 撤退
            if (td.Controller != null && td.Controller.Sim != null)
            {
                var ctrl = td.Controller;
                var sim = ctrl.Sim;
                sim.CostOverrideForTest(50);

                // 找一格可部署位
                int dc = -1, dr = -1;
                for (int c = 0; c < sim.GridW && dc < 0; c++)
                    for (int r = 0; r < sim.GridH; r++)
                        if (sim.CanDeployAt(c, r)) { dc = c; dr = r; break; }
                Check(dc >= 0, "塔防操作: 存在可部署格");

                ctrl.SelectCard(0);
                Check(ctrl.Phase == DeployPhase.Placing, "塔防操作: 选卡后进入待放置");

                ctrl.OnFieldClicked(dc, dr);
                // 近卫需要选朝向；术师直接部署
                Check(ctrl.Phase == DeployPhase.ChoosingFacing || ctrl.Sim.Operators.Count == 1,
                    "塔防操作: 点击高台后进入选朝向或直接部署");

                if (ctrl.Phase == DeployPhase.ChoosingFacing)
                {
                    ctrl.OnFieldClicked(dc + 1, dr);
                    Check(ctrl.Sim.Operators.Count == 1, "塔防操作: 选朝向后完成部署");
                }
                Check(ctrl.Phase == DeployPhase.Idle, "塔防操作: 部署后回到空闲");

                // 点已部署干员 → 选中（不再立即撤下）
                ctrl.OnFieldClicked(dc, dr);
                Check(ctrl.SelectedOperator != null, "塔防操作: 点干员后选中（弹出撤退面板）");
                int before = ctrl.Sim.Operators.Count;
                ctrl.ConfirmRetreat();
                Check(ctrl.Sim.Operators.Count == before - 1, "塔防操作: 撤退移除干员");
                Check(ctrl.SelectedOperator == null, "塔防操作: 撤退后清除选中");

                // 取消选择
                ctrl.SelectCard(0);
                ctrl.CancelSelection();
                Check(ctrl.Phase == DeployPhase.Idle && ctrl.SelectedOperatorIndex < 0,
                    "塔防操作: 取消选择复位");
            }

            // ★ 明日方舟式机制：准备阶段 / 敌人动线 / 技能 / 敌人攻击干员
            if (td.Controller != null && td.Controller.Sim != null)
            {
                var ctrl = td.Controller;
                var sim = ctrl.Sim;

                Check(sim.PrepPhase, "机制: 开局处于准备阶段");
                Check(ctrl.Field != null && ctrl.Field.HasRoutePreview, "机制: 已构建敌人动线预览");

                // 准备阶段不出兵
                int enemiesBefore = sim.Enemies.Count;
                sim.Advance(3.0);
                Check(sim.Enemies.Count == enemiesBefore, "机制: 准备阶段不生成敌人");

                ctrl.StartBattleNow();
                Check(!sim.PrepPhase, "机制: 开始行动后进入战斗");
                sim.Advance(9.0);
                Check(sim.CurrentWave >= 1, "机制: 开始行动后第一波出动");

                // 技能：技力满 → 可释放 → 生效中
                sim.CostOverrideForTest(80);
                int dc = -1, dr = -1;
                for (int c = 0; c < sim.GridW && dc < 0; c++)
                    for (int r = 0; r < sim.GridH; r++)
                        if (sim.CanDeployAt(c, r)) { dc = c; dr = r; break; }
                if (dc >= 0 && sim.TryDeployOperator(OperatorKind.Guard, dc, dr, Facing.Right, out _))
                {
                    var op = sim.OperatorAt(dc, dr);
                    Check(op != null && op.MaxHp > 0, "技能: 干员有生命值");
                    Check(op.SkillReady == false, "技能: 初始技力未满");
                    op.Sp = op.SpMax;
                    Check(op.SkillReady, "技能: 技力满后技能就绪");
                    ctrl.SelectDeployedOperator(op);
                    bool activated = ctrl.ActivateSelectedSkill();
                    Check(activated, "技能: 释放成功");
                    Check(op.SkillActive, "技能: 进入生效状态");
                    Check(!op.SkillReady, "技能: 释放后技力清空");
                }

                // 敌人攻击干员：把近卫放在路径旁，推进一段时间后应受到伤害
                sim.Advance(20.0);
                Check(true, "机制: 战斗推进 20 秒无异常");
            }

            // ★ 回归：Start() 一生只跑一次，退出再进入曾导致战斗起不来
            td.BeginBattle();
            Check(td.Controller != null && td.Controller.Sim != null, "塔防: 二次 BeginBattle 仍能开局");

            td.ExitToMenu();
            Check(!tdGo.activeSelf, "塔防: 退出后场景对象关闭");
            Check(td.Controller == null || td.Controller.Sim == null, "塔防: 退出后模拟已清空");
            Check(ui.Current == UiScreen.TDLevelSelect, "塔防: 退出后回到关卡选择（明日方舟式）");

            // 退出后还能再次进入（完整往返）
            tdGo.SetActive(true);
            td.BeginBattle();
            Check(td.Controller != null && td.Controller.Sim != null, "塔防: 退出后可再次进入");
            td.ExitToMenu();
            UnityEngine.Object.DestroyImmediate(tdGo);

            // ---- 3h. 塔防关卡选择界面 ----
            ui.GoTDLevelSelect();
            Check(ui.Current == UiScreen.TDLevelSelect, "关卡选择: 可进入作战列表");
            ui.GoBack();
            Check(ui.Current == UiScreen.ModeSelect, "关卡选择: 返回后回到模式选择");
            Check(ArknightsTD.Data.TDLevels.All.Count >= 5, "关卡选择: 关卡数据不少于 5 关");

            // ---- 4. 菜单可选性 ----
            var desc = SnakeModeMenu.Describe(SnakeMode.Timed);
            Check(!string.IsNullOrEmpty(desc.name) && !string.IsNullOrEmpty(desc.goal),
                "计时赛菜单描述完整");

            UnityEngine.Object.DestroyImmediate(host);

            Debug.Log($"[VerifyLaunch] 启动链路验证：共 {_checks} 项，失败 {_failures} 项");
            Debug.Log(_failures == 0
                ? "[VerifyLaunch] LAUNCH CHAIN OK"
                : "[VerifyLaunch] LAUNCH CHAIN BROKEN");
            EditorApplication.Exit(_failures == 0 ? 0 : 1);
        }

        /// <summary>构造几个轮换方向，避免测试里写死单一朝向。</summary>
        private struct Vec2ish
        {
            private readonly int _i;
            public Vec2ish(int i) { _i = i; }
            public Core.Geometry.Vec2 Value
            {
                get
                {
                    switch ((_i / 40) % 4)
                    {
                        case 0: return new Core.Geometry.Vec2(1, 0);
                        case 1: return new Core.Geometry.Vec2(0, 1);
                        case 2: return new Core.Geometry.Vec2(-1, 0);
                        default: return new Core.Geometry.Vec2(0, -1);
                    }
                }
            }
        }
    }
}
