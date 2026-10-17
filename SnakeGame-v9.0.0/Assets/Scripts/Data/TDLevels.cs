using System.Collections.Generic;

// ==================================================================================
//  TOWER DEFENSE LEVEL DATA
//
//  10 个全新关卡，逐一解锁（通关第 N 关解锁第 N+1 关，进度存 PlayerProgress）。
//  数据驱动：新增关卡只需向 TDLevels.All 追加一条。
//
//  路径约定（与 BattleSim 一致）：
//    · 路径点是【格索引】，入口从网格左侧之外进来（x = -1），出口到右侧之外（x = GridW）
//    · 相邻两点必须水平或垂直对齐，且不能重合
//    · 障碍格不能落在路径格上
// ==================================================================================

namespace ArknightsTD.Data
{
    /// <summary>关卡静态数据（数据驱动，新增关卡只需在此追加一条）</summary>
    public class TDLevelData
    {
        public int Index;              // 关卡号 1-based
        public string Name;            // 关卡名
        public string Description;     // 简短描述
        public int GridW;              // 网格宽（格）
        public int GridH;              // 网格高（格）
        public int StartingCost;       // 初始费用
        public int StartingLives;      // 初始生命
        public int TotalWaves;         // 总波次
        public int Difficulty;         // 难度星级 1-5
        public int[][] Obstacles;      // 障碍格子 [col, row]
        public double[][] Path;        // 路径点 [col, row]（可超出网格，表示入口/出口）
    }

    /// <summary>所有塔防关卡（10 关，逐一解锁）</summary>
    public static class TDLevels
    {
        public static readonly List<TDLevelData> All = new List<TDLevelData>
        {
            // ==================== 第 1 关 ====================
            new TDLevelData
            {
                Index = 1,
                Name = "边境哨所",
                Description = "敌军沿谷道缓慢推进，熟悉部署与技能",
                GridW = 12, GridH = 8,
                StartingCost = 45, StartingLives = 12,
                TotalWaves = 4, Difficulty = 1,
                Obstacles = new int[][]
                {
                    new int[] { 0, 0 }, new int[] { 11, 0 },
                    new int[] { 0, 7 }, new int[] { 11, 7 },
                    new int[] { 6, 1 }, new int[] { 10, 6 },
                },
                Path = new double[][]
                {
                    new double[] { -1, 2 }, new double[] { 3, 2 },
                    new double[] { 3, 5 }, new double[] { 8, 5 },
                    new double[] { 8, 3 }, new double[] { 12, 3 },
                },
            },

            // ==================== 第 2 关 ====================
            new TDLevelData
            {
                Index = 2,
                Name = "迂回山谷",
                Description = "折返地形，注意照顾两段长直路",
                GridW = 14, GridH = 9,
                StartingCost = 40, StartingLives = 12,
                TotalWaves = 5, Difficulty = 1,
                Obstacles = new int[][]
                {
                    new int[] { 0, 0 }, new int[] { 2, 0 }, new int[] { 13, 0 },
                    new int[] { 6, 3 }, new int[] { 6, 6 },
                    new int[] { 11, 3 }, new int[] { 12, 5 },
                },
                Path = new double[][]
                {
                    new double[] { -1, 4 }, new double[] { 4, 4 },
                    new double[] { 4, 1 }, new double[] { 9, 1 },
                    new double[] { 9, 7 }, new double[] { 14, 7 },
                },
            },

            // ==================== 第 3 关 ====================
            new TDLevelData
            {
                Index = 3,
                Name = "裂谷走廊",
                Description = "长直走廊 + 收窄出口，重装敌人初次登场",
                GridW = 16, GridH = 9,
                StartingCost = 38, StartingLives = 10,
                TotalWaves = 5, Difficulty = 2,
                Obstacles = new int[][]
                {
                    new int[] { 1, 4 }, new int[] { 3, 5 }, new int[] { 2, 7 },
                    new int[] { 8, 3 }, new int[] { 9, 1 }, new int[] { 14, 2 },
                    new int[] { 13, 6 }, new int[] { 15, 7 },
                },
                Path = new double[][]
                {
                    new double[] { -1, 1 }, new double[] { 6, 1 },
                    new double[] { 6, 7 }, new double[] { 12, 7 },
                    new double[] { 12, 4 }, new double[] { 16, 4 },
                },
            },

            // ==================== 第 4 关 ====================
            new TDLevelData
            {
                Index = 4,
                Name = "环形废墟",
                Description = "绕行废墟一周，狙击需要覆盖拐角",
                GridW = 14, GridH = 10,
                StartingCost = 36, StartingLives = 10,
                TotalWaves = 6, Difficulty = 2,
                Obstacles = new int[][]
                {
                    new int[] { 0, 0 }, new int[] { 13, 1 },
                    new int[] { 6, 5 }, new int[] { 7, 6 },
                    new int[] { 5, 8 }, new int[] { 6, 8 },
                    new int[] { 12, 9 },
                },
                Path = new double[][]
                {
                    new double[] { -1, 5 }, new double[] { 3, 5 },
                    new double[] { 3, 2 }, new double[] { 10, 2 },
                    new double[] { 10, 8 }, new double[] { 14, 8 },
                },
            },

            // ==================== 第 5 关 ====================
            new TDLevelData
            {
                Index = 5,
                Name = "双塔防线",
                Description = "高低两段路，建议一近战一术师分工",
                GridW = 16, GridH = 10,
                StartingCost = 34, StartingLives = 10,
                TotalWaves = 6, Difficulty = 3,
                Obstacles = new int[][]
                {
                    new int[] { 2, 6 }, new int[] { 7, 5 }, new int[] { 8, 6 },
                    new int[] { 13, 6 }, new int[] { 8, 1 }, new int[] { 14, 1 },
                    new int[] { 0, 9 }, new int[] { 15, 9 },
                },
                Path = new double[][]
                {
                    new double[] { -1, 3 }, new double[] { 5, 3 },
                    new double[] { 5, 8 }, new double[] { 11, 8 },
                    new double[] { 11, 2 }, new double[] { 16, 2 },
                },
            },

            // ==================== 第 6 关 ====================
            new TDLevelData
            {
                Index = 6,
                Name = "狭窄高原",
                Description = "多段阶梯回廊，射程覆盖成为关键",
                GridW = 18, GridH = 9,
                StartingCost = 32, StartingLives = 9,
                TotalWaves = 7, Difficulty = 3,
                Obstacles = new int[][]
                {
                    new int[] { 2, 1 }, new int[] { 3, 6 }, new int[] { 6, 8 },
                    new int[] { 7, 4 }, new int[] { 11, 1 }, new int[] { 12, 5 },
                    new int[] { 15, 8 }, new int[] { 16, 3 },
                },
                Path = new double[][]
                {
                    new double[] { -1, 7 }, new double[] { 4, 7 },
                    new double[] { 4, 3 }, new double[] { 9, 3 },
                    new double[] { 9, 6 }, new double[] { 14, 6 },
                    new double[] { 14, 1 }, new double[] { 18, 1 },
                },
            },

            // ==================== 第 7 关 ====================
            new TDLevelData
            {
                Index = 7,
                Name = "深渊回廊",
                Description = "敌潮密集，护盾兵成群推进",
                GridW = 18, GridH = 10,
                StartingCost = 30, StartingLives = 9,
                TotalWaves = 7, Difficulty = 4,
                Obstacles = new int[][]
                {
                    new int[] { 1, 0 }, new int[] { 2, 5 }, new int[] { 3, 8 },
                    new int[] { 9, 1 }, new int[] { 10, 8 }, new int[] { 14, 5 },
                    new int[] { 15, 0 }, new int[] { 17, 7 },
                },
                Path = new double[][]
                {
                    new double[] { -1, 2 }, new double[] { 6, 2 },
                    new double[] { 6, 6 }, new double[] { 12, 6 },
                    new double[] { 12, 3 }, new double[] { 18, 3 },
                },
            },

            // ==================== 第 8 关 ====================
            new TDLevelData
            {
                Index = 8,
                Name = "要塞外围",
                Description = "长距离突入，漏一个就掉很多血",
                GridW = 16, GridH = 12,
                StartingCost = 28, StartingLives = 8,
                TotalWaves = 8, Difficulty = 4,
                Obstacles = new int[][]
                {
                    new int[] { 0, 0 }, new int[] { 1, 8 }, new int[] { 2, 3 },
                    new int[] { 7, 2 }, new int[] { 8, 7 }, new int[] { 9, 0 },
                    new int[] { 13, 9 }, new int[] { 14, 5 },
                },
                Path = new double[][]
                {
                    new double[] { -1, 6 }, new double[] { 4, 6 },
                    new double[] { 4, 10 }, new double[] { 10, 10 },
                    new double[] { 10, 4 }, new double[] { 16, 4 },
                },
            },

            // ==================== 第 9 关 ====================
            new TDLevelData
            {
                Index = 9,
                Name = "黑铁矿区",
                Description = "重装与 Boss 混编，考验技能时机",
                GridW = 20, GridH = 10,
                StartingCost = 26, StartingLives = 8,
                TotalWaves = 8, Difficulty = 5,
                Obstacles = new int[][]
                {
                    new int[] { 2, 2 }, new int[] { 3, 7 }, new int[] { 8, 1 },
                    new int[] { 9, 5 }, new int[] { 13, 9 }, new int[] { 14, 3 },
                    new int[] { 18, 8 }, new int[] { 19, 2 },
                },
                Path = new double[][]
                {
                    new double[] { -1, 8 }, new double[] { 5, 8 },
                    new double[] { 5, 4 }, new double[] { 11, 4 },
                    new double[] { 11, 7 }, new double[] { 16, 7 },
                    new double[] { 16, 2 }, new double[] { 20, 2 },
                },
            },

            // ==================== 第 10 关 ====================
            new TDLevelData
            {
                Index = 10,
                Name = "天穹裂隙",
                Description = "终局作战：全兵种混合，10 波敌潮",
                GridW = 20, GridH = 12,
                StartingCost = 25, StartingLives = 6,
                TotalWaves = 10, Difficulty = 5,
                Obstacles = new int[][]
                {
                    new int[] { 2, 1 }, new int[] { 3, 6 }, new int[] { 8, 2 },
                    new int[] { 9, 9 }, new int[] { 13, 4 }, new int[] { 14, 10 },
                    new int[] { 18, 6 }, new int[] { 19, 1 },
                },
                Path = new double[][]
                {
                    new double[] { -1, 10 }, new double[] { 6, 10 },
                    new double[] { 6, 6 }, new double[] { 12, 6 },
                    new double[] { 12, 2 }, new double[] { 17, 2 },
                    new double[] { 17, 9 }, new double[] { 20, 9 },
                },
            },
        };

        /// <summary>按 1-based 关卡号取数据（越界则夹到合法范围）。</summary>
        public static TDLevelData Get(int index)
        {
            if (All.Count == 0) return new TDLevelData();
            int i = index - 1;
            // 越界一律回退到第 1 关（与既有测试契约一致）
            if (i < 0 || i >= All.Count) return All[0];
            return All[i];
        }
    }
}
