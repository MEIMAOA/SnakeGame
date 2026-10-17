using System;

namespace ArknightsTD.Core
{
    /// <summary>
    /// 玩家进度存档。
    /// 从 WPF 版的 TDProgress.cs 移植，区别是持久化改为 Unity 的外置存档目录
    /// （Application.persistentDataPath），不再依赖 WPF 的 SecureStorage。
    /// </summary>
    public static class PlayerProgress
    {
        private const string FileName = "td_progress.txt";
        private const string PrefsKey = "ArknightsTD.TD.MaxUnlocked";

        /// <summary>已解锁的最高关卡号（1-based）。1 表示只解锁第 1 关。</summary>
        public static int MaxUnlocked { get; private set; } = 1;

        /// <summary>关卡总数（由外部注入，避免 Core 层直接依赖关卡表）。</summary>
        public static int LevelCount { get; set; } = 10;

        static PlayerProgress() { Load(); }

        public static void Load()
        {
            try
            {
                string raw = Store.Load(FileName, PrefsKey);
                if (!string.IsNullOrEmpty(raw) && int.TryParse(raw.Trim(), out int v))
                    MaxUnlocked = Clamp(v);
                else
                    MaxUnlocked = 1;
            }
            catch { MaxUnlocked = 1; }
        }

        public static void Save()
        {
            try { Store.Save(FileName, PrefsKey, MaxUnlocked.ToString()); } catch { }
        }

        /// <summary>通关 levelIndex 关卡，解锁下一关。</summary>
        public static void MarkComplete(int levelIndex)
        {
            int next = levelIndex + 1;
            if (next > MaxUnlocked && next <= LevelCount)
            {
                MaxUnlocked = next;
                Save();
            }
        }

        public static bool IsUnlocked(int levelIndex) => levelIndex <= MaxUnlocked;

        /// <summary>仅用于测试/调试：直接设置进度。</summary>
        public static void SetMaxUnlockedForTest(int value)
        {
            MaxUnlocked = Clamp(value);
            Save();
        }

        private static int Clamp(int v) => Math.Max(1, Math.Min(v, Math.Max(1, LevelCount)));
    }

    /// <summary>
    /// 存档后端。优先用 Unity PlayerPrefs；在没有 UnityEngine 的环境（独立测试工程）
    /// 下退化为普通文件，从而让存档逻辑可以脱离编辑器被实测。
    /// </summary>
    internal static class Store
    {
        private static readonly bool HasUnity = ProbeUnity();
        private static string _dir;

        private static bool ProbeUnity()
        {
            // 不直接引用 UnityEngine，用反射探测，保证本文件可独立编译与测试
            try
            {
                var t = Type.GetType("UnityEngine.Application, UnityEngine");
                return t != null;
            }
            catch { return false; }
        }

        private static string Dir()
        {
            if (_dir != null) return _dir;
            try
            {
                var t = Type.GetType("UnityEngine.Application, UnityEngine");
                if (t != null)
                {
                    var p = t.GetProperty("persistentDataPath");
                    if (p != null) _dir = p.GetValue(null) as string;
                }
            }
            catch { }
            if (string.IsNullOrEmpty(_dir))
                _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ArknightsTD_Progress");
            return _dir;
        }

        public static string Load(string fileName, string prefsKey)
        {
            try
            {
                var path = System.IO.Path.Combine(Dir(), fileName);
                if (System.IO.File.Exists(path))
                    return System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
            }
            catch { }
            return null;
        }

        public static void Save(string fileName, string prefsKey, string value)
        {
            try
            {
                var dir = Dir();
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(dir, fileName), value, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }
}
