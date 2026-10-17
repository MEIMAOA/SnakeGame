using ArknightsTD.Core.Geometry;

namespace ArknightsTD.Snake
{
    /// <summary>
    /// 各模式的数值视图。数值<b>全部来自模式类本身</b>（WPF 各 Mode 类的
    /// FoodCount / EnemyCount / AiDifficulty），本类只是 UI/测试访问的薄包装，
    /// 避免数值在多处硬编码漂移。
    /// </summary>
    public sealed class SnakeModeConfig
    {
        /// <summary>本配置对应的模式。</summary>
        public SnakeMode Mode;

        /// <summary>模式对象（数值真源）。</summary>
        public SnakeGameModeBase ModeInstance;

        /// <summary>模式显示名（原版 GetAvailableModes 卡片名）。</summary>
        public string DisplayName;

        public int FoodCount => ModeInstance != null ? ModeInstance.FoodCount : 0;
        public int EnemyCount => ModeInstance != null ? ModeInstance.EnemyCount : 0;
        public double AiDifficulty => ModeInstance != null ? ModeInstance.AiDifficulty : 0;

        /// <summary>经典模式胜利分数（原版 CLASSIC_WIN_SCORE）。</summary>
        public double ScoreToWin = SnakeWorld.ClassicWinScore;

        /// <summary>极限模式胜利存活时间（原版 EXTREME_WIN_TIME）。</summary>
        public double SurviveSeconds = SnakeWorld.ExtremeWinTime;

        public static SnakeModeConfig For(SnakeMode mode)
        {
            SnakeGameModeBase instance;
            string display;
            switch (mode)
            {
                case SnakeMode.Timed:
                    instance = new SnakeTimedMode();
                    display = "淘汰之王";
                    break;
                case SnakeMode.Team4v4:
                    instance = new SnakeTeamMode();
                    display = "占领模式 (4v4)";
                    break;
                case SnakeMode.Extreme:
                    instance = new SnakeExtremeMode();
                    display = "极限模式";
                    break;
                case SnakeMode.Extraction:
                    instance = new SnakeExtractionMode();
                    display = "搜打撤";
                    break;
                default:
                    instance = new SnakeClassicMode();
                    display = "经典模式";
                    break;
            }
            return new SnakeModeConfig { Mode = mode, ModeInstance = instance, DisplayName = display };
        }
    }
}
