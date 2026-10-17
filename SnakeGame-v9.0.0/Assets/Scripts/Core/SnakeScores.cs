using System;
using System.Collections.Generic;
using ArknightsTD.Snake;

namespace ArknightsTD.Core
{
    /// <summary>
    /// 历史排行榜。对应原版的 <c>timed_scores.json</c> 与
    /// <c>RankingsWindow</c>（Rank / Name / Score 三列）。
    /// <para>
    /// 原版只在计时赛记录成绩；这里扩展为<b>按模式分别记录</b>，
    /// 因为本工程有 5 个模式，混在一张榜里没有可比性。
    /// </para>
    /// </summary>
    public static class SnakeScores
    {
        /// <summary>一条成绩。</summary>
        public struct Entry
        {
            public string Mode;
            public int Score;
            public int BodyCells;
            public double Seconds;
            public bool Won;
            public string Date;

            public override string ToString()
                => $"{Mode} {Score} {BodyCells} {Seconds:0.0} {(Won ? 1 : 0)} {Date}";
        }

        /// <summary>每个模式保留的条目数。</summary>
        public const int MaxEntriesPerMode = 20;

        private const string FileName = "snake_scores.txt";
        private const string PrefsKey = "ArknightsTD.Snake.Scores";

        private static readonly List<Entry> Entries = new List<Entry>();

        static SnakeScores() { Load(); }

        /// <summary>全部成绩（按分数降序）。</summary>
        public static IReadOnlyList<Entry> All => Entries;

        /// <summary>取某模式的榜单（已按分数降序，最多 <see cref="MaxEntriesPerMode"/> 条）。</summary>
        public static List<Entry> ForMode(SnakeMode mode)
        {
            string tag = TagOf(mode);
            var list = new List<Entry>();
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].Mode == tag) list.Add(Entries[i]);
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (list.Count > MaxEntriesPerMode) list.RemoveRange(MaxEntriesPerMode, list.Count - MaxEntriesPerMode);
            return list;
        }

        /// <summary>把一条成绩记入榜单。</summary>
        public static void Record(SnakeMode mode, int score, int bodyCells, double seconds, bool won)
        {
            Entries.Add(new Entry
            {
                Mode = TagOf(mode),
                Score = score,
                BodyCells = bodyCells,
                Seconds = seconds,
                Won = won,
                Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            });

            // 每个模式只留前 N 名，避免文件无限增长
            Prune();
            Save();
        }

        private static void Prune()
        {
            foreach (SnakeMode m in Enum.GetValues(typeof(SnakeMode)))
            {
                string tag = TagOf(m);
                var idx = new List<int>();
                for (int i = 0; i < Entries.Count; i++)
                    if (Entries[i].Mode == tag) idx.Add(i);

                if (idx.Count <= MaxEntriesPerMode) continue;

                // 按分数降序，删掉尾部
                idx.Sort((a, b) => Entries[b].Score.CompareTo(Entries[a].Score));
                var drop = new List<int>();
                for (int k = MaxEntriesPerMode; k < idx.Count; k++) drop.Add(idx[k]);
                drop.Sort((a, b) => b.CompareTo(a));      // 从后往前删
                for (int k = 0; k < drop.Count; k++) Entries.RemoveAt(drop[k]);
            }
        }

        /// <summary>模式对应的榜单标签（用英文名，便于存档可读）。</summary>
        public static string TagOf(SnakeMode m)
        {
            switch (m)
            {
                case SnakeMode.Classic: return "classic";
                case SnakeMode.Timed: return "timed";
                case SnakeMode.Team4v4: return "team4v4";
                case SnakeMode.Extreme: return "extreme";
                default: return "extraction";
            }
        }

        public static string NameOf(SnakeMode m) => SnakeModeConfig.For(m).DisplayName;

        // ==================== 持久化 ====================

        public static string Serialize()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("v1");
            for (int i = 0; i < Entries.Count; i++)
                sb.AppendLine(Entries[i].ToString());
            return sb.ToString();
        }

        public static void Deserialize(string raw)
        {
            Entries.Clear();
            if (string.IsNullOrEmpty(raw)) return;

            var lines = raw.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var parts = lines[i].Trim().Split(' ');
                if (parts.Length < 6) continue;                       // 跳过表头与坏行
                if (!int.TryParse(parts[1], out int score)) continue;
                if (!int.TryParse(parts[2], out int cells)) continue;
                if (!double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double secs)) continue;

                Entries.Add(new Entry
                {
                    Mode = parts[0],
                    Score = score,
                    BodyCells = cells,
                    Seconds = secs,
                    Won = parts[4] == "1",
                    Date = parts.Length >= 6 ? parts[5] + " " + (parts.Length >= 7 ? parts[6] : "") : "",
                });
            }
        }

        public static void Save()
        {
            try { Store.Save(FileName, PrefsKey, Serialize()); } catch { }
        }

        public static void Load()
        {
            try { Deserialize(Store.Load(FileName, PrefsKey)); } catch { }
        }

        /// <summary>仅用于测试/调试：清空榜单。</summary>
        public static void ResetForTest() => Entries.Clear();

        /// <summary>仅用于测试/调试：直接注入一条。</summary>
        public static void AddForTest(SnakeMode mode, int score)
            => Entries.Add(new Entry
            {
                Mode = TagOf(mode), Score = score, BodyCells = 0,
                Seconds = 0, Won = false, Date = "test",
            });
    }
}
