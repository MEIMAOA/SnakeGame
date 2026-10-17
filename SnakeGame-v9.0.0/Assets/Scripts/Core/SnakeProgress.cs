using System;
using System.Collections.Generic;
using ArknightsTD.Snake;

namespace ArknightsTD.Core
{
    /// <summary>成就定义。</summary>
    public sealed class AchievementDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public string Icon = "";

        public AchievementDef(string id, string name, string desc, string icon)
        { Id = id; Name = name; Description = desc; Icon = icon; }
    }

    /// <summary>
    /// 贪吃蛇的统计与成就。
    /// <para>
    /// 移植自 WPF 版 <c>MainWindow.Achievements</c> / <c>CheckAchievements</c> /
    /// <c>achievements.json</c> 那套机制，并把统计口径固定下来：
    /// 累计击杀、总场次、胜场、占点胜场、单局最长存活、当前局击杀。
    /// </para>
    /// <para>
    /// 与塔防的 <see cref="PlayerProgress"/> 一致，持久化走 <see cref="Store"/>，
    /// 因此本类可脱离引擎被断言测试。
    /// </para>
    /// </summary>
    public static class SnakeProgress
    {
        private const string FileName = "snake_progress.txt";
        private const string PrefsKey = "ArknightsTD.Snake.Progress";

        // ==================== 成就表（原版 6 个）====================

        public static readonly AchievementDef[] Achievements =
        {
            new AchievementDef("first_kill",     "初次击杀", "第一次击杀敌人", "1"),
            new AchievementDef("kill_10",        "十人斩",   "累计击杀 10 个敌人", "2"),
            new AchievementDef("kill_50",        "五十人斩", "累计击杀 50 个敌人", "3"),
            new AchievementDef("win_10",         "常胜将军", "赢得 10 场游戏", "4"),
            new AchievementDef("survival_5min",  "生存专家", "单局存活超过 5 分钟", "5"),
            new AchievementDef("capture_win",    "占领专家", "在占点模式中获胜", "6"),
        };

        // ==================== 统计 ====================

        /// <summary>累计击杀。</summary>
        public static int TotalKills { get; private set; }
        /// <summary>总场次。</summary>
        public static int TotalGames { get; private set; }
        /// <summary>胜场。</summary>
        public static int WonGames { get; private set; }
        /// <summary>占点模式胜场。</summary>
        public static int CaptureWins { get; private set; }
        /// <summary>单局最长存活（秒）。</summary>
        public static double BestSurvivalSeconds { get; private set; }

        /// <summary>当前局击杀。</summary>
        public static int CurrentGameKills { get; private set; }
        /// <summary>当前局时长（秒）。</summary>
        public static double CurrentGameDuration { get; private set; }

        /// <summary>
        /// 连续失败场次（原版 death_count.txt）。≥5 时下一局为慈悲局并清零。
        /// </summary>
        public static int ConsecutiveDeaths { get; private set; }

        /// <summary>
        /// 连续胜利场次（原版 win_streak.txt）。≥10 时下一局为惩罚局并清零。
        /// </summary>
        public static int ConsecutiveWins { get; private set; }

        private static readonly Dictionary<string, bool> Unlocked = new Dictionary<string, bool>();

        /// <summary>是否已解锁某成就。</summary>
        public static bool IsUnlocked(string id)
            => Unlocked.TryGetValue(id, out bool v) && v;

        /// <summary>已解锁数量。</summary>
        public static int UnlockedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Achievements.Length; i++)
                    if (IsUnlocked(Achievements[i].Id)) n++;
                return n;
            }
        }

        static SnakeProgress() { Load(); }

        // ==================== 对局生命周期 ====================

        /// <summary>开始一局：重置本局计数。</summary>
        public static void BeginGame()
        {
            TotalGames++;
            CurrentGameKills = 0;
            CurrentGameDuration = 0;
            Save();
        }

        /// <summary>每帧/每步上报本局进度。</summary>
        public static void ReportGameProgress(double durationSeconds, int kills)
        {
            CurrentGameDuration = durationSeconds;
            if (kills > CurrentGameKills) CurrentGameKills = kills;
        }

        /// <summary>
        /// 结束一局：结算统计并检查成就。
        /// </summary>
        /// <param name="mode">本局模式。</param>
        /// <param name="won">是否胜利。</param>
        /// <param name="kills">本局击杀。</param>
        /// <param name="duration">本局时长（秒）。</param>
        /// <returns>本局新解锁的成就列表（可能为空）。</returns>
        public static List<AchievementDef> EndGame(SnakeMode mode, bool won, int kills, double duration)
        {
            if (kills > 0) TotalKills += kills;
            if (won)
            {
                WonGames++;
                if (mode == SnakeMode.Team4v4) CaptureWins++;
            }
            if (duration > BestSurvivalSeconds) BestSurvivalSeconds = duration;
            CurrentGameKills = kills;
            CurrentGameDuration = duration;

            var newly = CheckAchievements();
            Save();
            return newly;
        }

        /// <summary>按当前统计检查成就，返回本次新解锁的。
        /// 生存成就按<b>单局时长</b>判定（原版 CurrentGameDuration &gt; 300）。</summary>
        public static List<AchievementDef> CheckAchievements()
        {
            var newly = new List<AchievementDef>();

            UnlockIf("first_kill", TotalKills >= 1, newly);
            UnlockIf("kill_10", TotalKills >= 10, newly);
            UnlockIf("kill_50", TotalKills >= 50, newly);
            UnlockIf("win_10", WonGames >= 10, newly);
            UnlockIf("survival_5min", CurrentGameDuration > 300.0, newly);
            UnlockIf("capture_win", CaptureWins >= 1, newly);

            return newly;
        }

        /// <summary>一局失败：连死 +1、连胜清零（原版 OnPlayerDeath）。</summary>
        public static void RegisterLoss()
        {
            ConsecutiveDeaths = Math.Min(99, ConsecutiveDeaths + 1);
            ConsecutiveWins = 0;
            Save();
        }

        /// <summary>一局胜利：连胜 +1、连死清零（原版 OnPlayerWin）。</summary>
        public static void RegisterWin()
        {
            ConsecutiveDeaths = 0;
            ConsecutiveWins = Math.Min(99, ConsecutiveWins + 1);
            Save();
        }

        /// <summary>消耗慈悲局资格（原版 StartGame 里清零连死计数）。</summary>
        public static void ConsumeDeathStreak()
        {
            ConsecutiveDeaths = 0;
            Save();
        }

        /// <summary>消耗惩罚局资格（原版 StartGame 里清零连胜计数）。</summary>
        public static void ConsumeWinStreak()
        {
            ConsecutiveWins = 0;
            Save();
        }

        private static void UnlockIf(string id, bool condition, List<AchievementDef> newly)
        {
            if (!condition || IsUnlocked(id)) return;
            Unlocked[id] = true;
            for (int i = 0; i < Achievements.Length; i++)
                if (Achievements[i].Id == id) { newly.Add(Achievements[i]); break; }
        }

        // ==================== 持久化 ====================

        /// <summary>把统计与已解锁成就序列化成行文本。</summary>
        public static string Serialize()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("v1");
            sb.AppendLine($"kills={TotalKills}");
            sb.AppendLine($"games={TotalGames}");
            sb.AppendLine($"wins={WonGames}");
            sb.AppendLine($"captures={CaptureWins}");
            sb.AppendLine($"best={BestSurvivalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.AppendLine($"deaths={ConsecutiveDeaths}");
            sb.AppendLine($"streak={ConsecutiveWins}");
            for (int i = 0; i < Achievements.Length; i++)
                sb.AppendLine($"ach.{Achievements[i].Id}={(IsUnlocked(Achievements[i].Id) ? 1 : 0)}");
            return sb.ToString();
        }

        /// <summary>从行文本恢复（容错：任何坏行都跳过）。</summary>
        public static void Deserialize(string raw)
        {
            TotalKills = TotalGames = WonGames = CaptureWins = 0;
            BestSurvivalSeconds = 0;
            ConsecutiveDeaths = 0;
            ConsecutiveWins = 0;
            Unlocked.Clear();
            if (string.IsNullOrEmpty(raw)) return;

            var lines = raw.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq);
                string val = line.Substring(eq + 1);

                if (key == "kills")
                {
                    if (int.TryParse(val, out int k)) TotalKills = k;
                }
                else if (key == "games")
                {
                    if (int.TryParse(val, out int g)) TotalGames = g;
                }
                else if (key == "wins")
                {
                    if (int.TryParse(val, out int w)) WonGames = w;
                }
                else if (key == "captures")
                {
                    if (int.TryParse(val, out int c)) CaptureWins = c;
                }
                else if (key == "best")
                {
                    if (double.TryParse(val, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double b))
                        BestSurvivalSeconds = b;
                }
                else if (key == "deaths")
                {
                    if (int.TryParse(val, out int d)) ConsecutiveDeaths = Math.Max(0, Math.Min(d, 99));
                }
                else if (key == "streak")
                {
                    if (int.TryParse(val, out int w)) ConsecutiveWins = Math.Max(0, Math.Min(w, 99));
                }
                else if (key.StartsWith("ach."))
                {
                    Unlocked[key.Substring(4)] = val == "1";
                }
            }
        }

        public static void Save()
        {
            try { Store.Save(FileName, PrefsKey, Serialize()); } catch { }
        }

        public static void Load()
        {
            try { Deserialize(Store.Load(FileName, PrefsKey)); }
            catch { }
        }

        /// <summary>仅用于测试/调试：清空全部统计与成就。</summary>
        public static void ResetForTest()
        {
            TotalKills = TotalGames = WonGames = CaptureWins = 0;
            BestSurvivalSeconds = 0;
            ConsecutiveDeaths = 0;
            ConsecutiveWins = 0;
            CurrentGameKills = 0;
            CurrentGameDuration = 0;
            Unlocked.Clear();
        }

        /// <summary>仅用于测试/调试：直接设置统计值。</summary>
        public static void SetStatsForTest(int kills, int games, int wins, int captures, double best)
        {
            TotalKills = kills;
            TotalGames = games;
            WonGames = wins;
            CaptureWins = captures;
            BestSurvivalSeconds = best;
        }

        /// <summary>仅用于测试/调试：直接设置连死/连胜计数。</summary>
        public static void SetStreaksForTest(int deaths, int wins)
        {
            ConsecutiveDeaths = Math.Max(0, Math.Min(99, deaths));
            ConsecutiveWins = Math.Max(0, Math.Min(99, wins));
        }
    }
}
