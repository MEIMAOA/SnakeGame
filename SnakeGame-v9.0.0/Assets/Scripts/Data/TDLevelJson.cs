using System;
using System.Collections.Generic;
using System.Text;

namespace ArknightsTD.Data
{
    /// <summary>
    /// 关卡数据的外部存储格式（纯 C#，便于脱离引擎读写）。
    /// 关卡表在 <see cref="TDLevels.All"/> 中维护，本类负责它与 JSON 之间的转换。
    /// </summary>
    public static class TDLevelJson
    {
        /// <summary>
        /// 把全部关卡序列化为 JSON。
        /// 手写而非依赖 JsonUtility，是为了让数据可被独立工程读出来做校验。
        /// </summary>
        public static string ToJson(List<TDLevelData> levels)
        {
            var sb = new StringBuilder();
            sb.Append("{\"levels\":[");
            for (int i = 0; i < levels.Count; i++)
            {
                var lv = levels[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                sb.Append("\"index\":").Append(lv.Index).Append(',');
                sb.Append("\"name\":\"").Append(Escape(lv.Name)).Append("\",");
                sb.Append("\"description\":\"").Append(Escape(lv.Description)).Append("\",");
                sb.Append("\"gridW\":").Append(lv.GridW).Append(',');
                sb.Append("\"gridH\":").Append(lv.GridH).Append(',');
                sb.Append("\"startingCost\":").Append(lv.StartingCost).Append(',');
                sb.Append("\"startingLives\":").Append(lv.StartingLives).Append(',');
                sb.Append("\"totalWaves\":").Append(lv.TotalWaves).Append(',');
                sb.Append("\"difficulty\":").Append(lv.Difficulty).Append(',');

                sb.Append("\"obstacles\":[");
                if (lv.Obstacles != null)
                    for (int k = 0; k < lv.Obstacles.Length; k++)
                    {
                        if (k > 0) sb.Append(',');
                        sb.Append('[').Append(lv.Obstacles[k][0]).Append(',').Append(lv.Obstacles[k][1]).Append(']');
                    }
                sb.Append("],");

                sb.Append("\"path\":[");
                if (lv.Path != null)
                    for (int k = 0; k < lv.Path.Length; k++)
                    {
                        if (k > 0) sb.Append(',');
                        sb.Append('[').Append(Num(lv.Path[k][0])).Append(',').Append(Num(lv.Path[k][1])).Append(']');
                    }
                sb.Append(']');

                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string Num(double d)
        {
            // 路径坐标都是整数值的格坐标，用不变文化输出，避免小数点/千分位差异
            if (Math.Abs(d - Math.Round(d)) < 1e-9)
                return ((long)Math.Round(d)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
