using UnityEngine;
using ArknightsTD.SnakeView;

namespace ArknightsTD.View
{
    /// <summary>
    /// 塔防模式运行时入口（正式模式，不再是实验性开关）。
    /// <para>
    /// 本组件会自动创建 <see cref="BattleController"/>（含相机、光照、战场与 HUD），
    /// 因此场景里只需要一个挂本组件的空 GameObject。
    /// </para>
    /// <para>
    /// ★ 启动不走 <c>Start()</c>：场景里的塔防对象默认是<b>未激活</b>的，
    /// <c>SetActive(true)</c> 会触发 Awake/OnEnable/Start，但 <c>Start()</c> 只执行一次——
    /// 一旦退出塔防再进入，Start 不会重跑，战斗就永远起不来（这正是"无法开始游戏"的根因）。
    /// 现改为由 <see cref="BeginBattle"/> 显式驱动，可重复调用；二次进入自动重置关卡。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleBootstrap : MonoBehaviour
    {
        [Tooltip("关卡号（1-based），范围 1..10")]
        public int LevelIndex = 1;

        [Tooltip("随机种子；相同种子 + 相同操作 = 相同战局")]
        public int RandomSeed = 20240;

        [Tooltip("按 Esc 退出塔防返回主菜单")]
        public bool EscapeReturnsToMenu = true;

        private BattleController _controller;

        /// <summary>本局是否已经开打（避免 SetActive 触发 Start() 时把刚建好的关卡又重置一遍）。</summary>
        private bool _started;

        public BattleController Controller => _controller;

        private void Start() => BeginBattle();

        /// <summary>
        /// 显式开始一局塔防（可重复调用）。
        /// 首次进入：创建控制器并初始化关卡；
        /// 再次进入：重置当前关卡（清场重建），保证"退出再进入"也能正常开打。
        /// </summary>
        public void BeginBattle()
        {
            if (_started && _controller != null && _controller.Sim != null) return;   // 已开局

            SnakeModeMenuProbe("BattleBootstrap.BeginBattle");

            if (_controller == null) _controller = GetComponent<BattleController>();
            if (_controller == null) _controller = gameObject.AddComponent<BattleController>();

            int maxLevel = Mathf.Max(1, Data.TDLevels.All.Count);
            _controller.LevelIndex = Mathf.Clamp(LevelIndex, 1, maxLevel);
            _controller.RandomSeed = RandomSeed;

            if (_controller.Sim != null) _controller.Restart();   // 二次进入 → 重置
            else _controller.SetupField();

            _started = true;
            SnakeModeMenuProbe($"BattleBootstrap.BeginBattle done: Sim={_controller.Sim != null} " +
                               $"Phase={_controller.Phase} BattlePhase={_controller.BattlePhase}");
        }

        /// <summary>结束当前塔防并返回贪吃蛇主菜单。</summary>
        public void ExitToMenu()
        {
            SnakeModeMenuProbe("BattleBootstrap.ExitToMenu");
            _started = false;
            if (_controller != null) _controller.Teardown();
            gameObject.SetActive(false);

            var boot = FindObjectOfType<SnakeBootstrap>(true);
            if (boot != null)
            {
                boot.ShowStartScreen();
                var ui = boot.Ui;
                if (ui != null) ui.GoTDLevelSelect();   // 明日方舟：作战结束回到关卡列表
            }
        }

        private void Update()
        {
            if (!EscapeReturnsToMenu) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // 正在选干员 / 选朝向 / 放置 / 选中已部署干员时，ESC 先交给控制器取消，
            // 只有"完全空闲"时按 ESC 才退出塔防——避免误触退出。
            if (_controller != null && _controller.Phase != DeployPhase.Idle) return;
            if (_controller != null && _controller.SelectedOperator != null) return;
            // 同一帧内控制器已消费 ESC（刚取消选择）→ 不要再退出
            if (_controller != null && _controller.EscConsumedFrame == Time.frameCount) return;

            ExitToMenu();
        }

        /// <summary>探针：走 SnakeModeMenu 的日志通道（打包版 Debug.Log 可能不可靠）。</summary>
        private static void SnakeModeMenuProbe(string msg) => SnakeModeMenu.Probe(msg);
    }
}
