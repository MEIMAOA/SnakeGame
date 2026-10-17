using System;
using System.Collections.Generic;

namespace ArknightsTD.Data
{
    /// <summary>干员职业。</summary>
    public enum OperatorKind { Guard, Sniper, Caster }

    /// <summary>敌人种类。</summary>
    public enum EnemyKind { Infantry, Shield, Runner, Heavy, Boss }

    /// <summary>攻击朝向（与 WPF 版 TDDirection 一致）。</summary>
    public enum Facing { Up, Down, Left, Right }

    /// <summary>技能技力回复方式（明日方舟：自动回复 / 攻击回复）。</summary>
    public enum SkillRecovery { Auto, OnAttack }

    /// <summary>技能效果类型。</summary>
    public enum SkillEffect
    {
        /// <summary>攻击力提升（持续期间伤害 ×(1+Magnitude)）。</summary>
        AtkBoost,
        /// <summary>攻速提升（持续期间攻击间隔 ×(1-Magnitude)）。</summary>
        FastAttack,
        /// <summary>范围爆发（持续期间每次攻击对射程内全部敌人造成伤害）。</summary>
        AoE,
    }

    /// <summary>干员技能定义。</summary>
    public sealed class SkillDef
    {
        public string Name;
        public string Description;
        /// <summary>技力上限。</summary>
        public double SpMax;
        /// <summary>技力回复方式。</summary>
        public SkillRecovery Recovery;
        /// <summary>自动回复时每秒回复量；攻击回复时为每次攻击回复量。</summary>
        public double SpPerTick;
        /// <summary>技能持续时间（秒）。</summary>
        public double Duration;
        public SkillEffect Effect;
        /// <summary>效果强度（含义随 Effect 变化）。</summary>
        public double Magnitude;
    }

    /// <summary>
    /// 干员数值定义。数值逐条移植自 WPF 版 TowerDefenseMode.OpDefs：
    /// (kind, name, cost, dmg, interval, color)。
    /// </summary>
    public sealed class OperatorDef
    {
        public OperatorKind Kind;
        public string Name;
        /// <summary>职业全称（明日方舟式：近卫 / 狙击 / 术师）。</summary>
        public string ClassName;
        public int Cost;
        public int Damage;
        /// <summary>攻击间隔（秒）。</summary>
        public double Interval;
        /// <summary>生命上限（被敌人攻击会掉血，归零即阵亡）。</summary>
        public int MaxHp;
        /// <summary>防御力（每次受击减免）。</summary>
        public int Def;
        /// <summary>阻挡数（近战挡人；远程为 0）。</summary>
        public int BlockCount;
        /// <summary>职业主色（用于程序化生成材质）。</summary>
        public (byte R, byte G, byte B) Color;
        /// <summary>技能。</summary>
        public SkillDef Skill;
    }

    /// <summary>
    /// 敌人数值定义，移植自 WPF 版 TowerDefenseMode.SpawnEnemy：
    /// 每类敌人有基础血量 / 速度 / 旧屏幕尺寸 / 配色，血量与速度再按波次和关卡成长。
    /// </summary>
    public sealed class EnemyDef
    {
        public EnemyKind Kind;
        public int BaseHp;
        public double BaseSpeed;
        /// <summary>WPF 版遗留的"屏幕像素基准尺寸"。3D 中改用 HalfSize 作为世界半尺寸。</summary>
        public double LegacySize;
        /// <summary>世界半尺寸（格）。3D 建模使用，不再依赖屏幕像素。</summary>
        public double HalfSize;
        public (byte R, byte G, byte B) Color;
        /// <summary>对阻挡干员的攻击力。</summary>
        public int Atk;
        /// <summary>攻击间隔（秒）。</summary>
        public double AtkInterval;
        /// <summary>是否会被阻挡（高速兵/Boss 依然可被阻挡；保留字段便于扩展）。</summary>
        public bool Blockable;
    }

    /// <summary>全部静态数值表。</summary>
    public static class TDBalance
    {
        /// <summary>费用自然回复：每多少秒 +1。</summary>
        public const double CostRegenSeconds = 1.0;

        /// <summary>首波等待时间（秒）。</summary>
        public const double FirstWaveDelay = 8.0;

        /// <summary>一波清完到下一波的间隔（秒）。</summary>
        public const double BetweenWaveDelay = 8.0;

        public static readonly OperatorDef[] Operators =
        {
            new OperatorDef
            {
                Kind = OperatorKind.Guard, Name = "近卫", ClassName = "近卫",
                Cost = 12, Damage = 4, Interval = 0.70,
                MaxHp = 240, Def = 3, BlockCount = 2,
                Color = (0xD0, 0x50, 0x50),
                Skill = new SkillDef
                {
                    Name = "强力击", Description = "攻击力 +80%，持续 8 秒",
                    SpMax = 5, Recovery = SkillRecovery.OnAttack, SpPerTick = 1,
                    Duration = 8, Effect = SkillEffect.AtkBoost, Magnitude = 0.80,
                },
            },
            new OperatorDef
            {
                Kind = OperatorKind.Sniper, Name = "狙击", ClassName = "狙击",
                Cost = 15, Damage = 3, Interval = 0.45,
                MaxHp = 130, Def = 0, BlockCount = 0,
                Color = (0x4A, 0x90, 0xD0),
                Skill = new SkillDef
                {
                    Name = "连射", Description = "攻击速度 +60%，持续 8 秒",
                    SpMax = 12, Recovery = SkillRecovery.Auto, SpPerTick = 1.6,
                    Duration = 8, Effect = SkillEffect.FastAttack, Magnitude = 0.60,
                },
            },
            new OperatorDef
            {
                Kind = OperatorKind.Caster, Name = "术师", ClassName = "术师",
                Cost = 18, Damage = 6, Interval = 1.10,
                MaxHp = 140, Def = 0, BlockCount = 0,
                Color = (0x90, 0x50, 0xC8),
                Skill = new SkillDef
                {
                    Name = "奥术爆发", Description = "持续 8 秒：每次攻击对范围内全部敌人造成伤害",
                    SpMax = 15, Recovery = SkillRecovery.Auto, SpPerTick = 2.0,
                    Duration = 8, Effect = SkillEffect.AoE, Magnitude = 1.0,
                },
            },
        };

        public static readonly EnemyDef[] Enemies =
        {
            new EnemyDef { Kind = EnemyKind.Infantry, BaseHp = 3,  BaseSpeed = 0.85, LegacySize = 0.26, HalfSize = 0.24, Color = (0xE0, 0x70, 0x40), Atk = 18, AtkInterval = 1.30, Blockable = true },
            new EnemyDef { Kind = EnemyKind.Shield,   BaseHp = 10, BaseSpeed = 0.55, LegacySize = 0.32, HalfSize = 0.30, Color = (0x50, 0x80, 0xC0), Atk = 14, AtkInterval = 1.50, Blockable = true },
            new EnemyDef { Kind = EnemyKind.Runner,   BaseHp = 2,  BaseSpeed = 1.30, LegacySize = 0.24, HalfSize = 0.20, Color = (0xE8, 0xC0, 0x30), Atk = 10, AtkInterval = 1.20, Blockable = true },
            new EnemyDef { Kind = EnemyKind.Heavy,    BaseHp = 18, BaseSpeed = 0.50, LegacySize = 0.36, HalfSize = 0.34, Color = (0x70, 0x50, 0x90), Atk = 34, AtkInterval = 1.60, Blockable = true },
            new EnemyDef { Kind = EnemyKind.Boss,     BaseHp = 64, BaseSpeed = 0.42, LegacySize = 0.46, HalfSize = 0.44, Color = (0xC0, 0x30, 0x40), Atk = 60, AtkInterval = 1.80, Blockable = true },
        };

        public static OperatorDef Operator(OperatorKind k)
        {
            for (int i = 0; i < Operators.Length; i++)
                if (Operators[i].Kind == k) return Operators[i];
            return Operators[0];
        }

        public static EnemyDef Enemy(EnemyKind k)
        {
            for (int i = 0; i < Enemies.Length; i++)
                if (Enemies[i].Kind == k) return Enemies[i];
            return Enemies[0];
        }
    }

    /// <summary>
    /// 波次生成与敌人生成规则。
    /// <para>
    /// 逐条移植自 WPF 版：
    /// <list type="bullet">
    /// <item>波次兵力：<c>4 + wave*2 + levelIndex</c></item>
    /// <item>出兵间隔：<c>max(0.30, 0.9 - wave*0.05 - levelIndex*0.01)</c></item>
    /// <item>血量成长：<c>baseHp + wave*3 + levelIndex*2</c>，速度成长：<c>baseSpeed + wave*0.03 + levelIndex*0.01</c></item>
    /// <item>兵种抽取：按关卡档位与波次的概率表（见 <see cref="PickKind"/>）</item>
    /// </list>
    /// 这些公式原先散落在 MonoBehaviour 里，抽成纯函数后可以脱离引擎直接验证。
    /// </para>
    /// </summary>
    public static class WaveDirector
    {
        /// <summary>该波次的敌人总数。</summary>
        public static int EnemiesInWave(int wave, int levelIndex)
            => 4 + wave * 2 + levelIndex;

        /// <summary>该波次的出兵间隔（秒）。</summary>
        public static double SpawnInterval(int wave, int levelIndex)
        {
            double v = 0.9 - wave * 0.05 - levelIndex * 0.01;
            return Math.Max(0.30, v);
        }

        /// <summary>该波次下某类敌人的实际血量。</summary>
        public static int EnemyHp(EnemyKind kind, int wave, int levelIndex)
            => TDBalance.Enemy(kind).BaseHp + wave * 3 + levelIndex * 2;

        /// <summary>该波次下某类敌人的实际速度（格/秒）。</summary>
        public static double EnemySpeed(EnemyKind kind, int wave, int levelIndex)
            => TDBalance.Enemy(kind).BaseSpeed + wave * 0.03 + levelIndex * 0.01;

        /// <summary>
        /// 按关卡档位与波次抽取兵种。<paramref name="roll"/> 取 [0,1)。
        /// 移植自 WPF 版的 PickEnemyKind，判定顺序与阈值完全一致。
        /// </summary>
        public static EnemyKind PickKind(int levelIndex, int wave, double roll)
        {
            if (levelIndex <= 2)
            {
                if (wave >= 4 && roll < 0.30) return EnemyKind.Runner;
                return EnemyKind.Infantry;
            }
            if (levelIndex <= 4)
            {
                if (wave >= 5 && roll < 0.20) return EnemyKind.Shield;
                if (wave >= 3 && roll < 0.50) return EnemyKind.Runner;
                return EnemyKind.Infantry;
            }
            if (levelIndex <= 6)
            {
                if (wave >= 7 && roll < 0.15) return EnemyKind.Heavy;
                if (wave >= 5 && roll < 0.40) return EnemyKind.Shield;
                if (wave >= 3 && roll < 0.60) return EnemyKind.Runner;
                return EnemyKind.Infantry;
            }
            if (levelIndex <= 8)
            {
                if (wave >= 9 && roll < 0.10) return EnemyKind.Boss;
                if (wave >= 7 && roll < 0.30) return EnemyKind.Heavy;
                if (wave >= 5 && roll < 0.50) return EnemyKind.Shield;
                if (wave >= 2 && roll < 0.70) return EnemyKind.Runner;
                return EnemyKind.Infantry;
            }
            if (wave >= 8 && roll < 0.15) return EnemyKind.Boss;
            if (wave >= 6 && roll < 0.35) return EnemyKind.Heavy;
            if (wave >= 4 && roll < 0.55) return EnemyKind.Shield;
            if (wave >= 2 && roll < 0.75) return EnemyKind.Runner;
            return EnemyKind.Infantry;
        }

        /// <summary>
        /// 攻击范围（相对干员所在格的偏移格集合）。
        /// 移植自 WPF 版 GetRangeCells：术师为周围 8 格；近卫/狙击为朝向直线 3 / 5 格。
        /// </summary>
        public static List<(int dc, int dr)> RangeCells(OperatorKind kind, Facing facing, int reachBonus = 0)
        {
            var list = new List<(int, int)>();

            if (kind == OperatorKind.Caster)
            {
                for (int dc = -1; dc <= 1; dc++)
                    for (int dr = -1; dr <= 1; dr++)
                        if (dc != 0 || dr != 0) list.Add((dc, dr));
                return list;
            }

            int fx, fy;
            switch (facing)
            {
                case Facing.Up: fx = 0; fy = -1; break;
                case Facing.Down: fx = 0; fy = 1; break;
                case Facing.Left: fx = -1; fy = 0; break;
                default: fx = 1; fy = 0; break;
            }

            int reach = (kind == OperatorKind.Guard ? 3 : 5) + Math.Max(0, reachBonus);
            for (int i = 1; i <= reach; i++) list.Add((fx * i, fy * i));
            return list;
        }

        /// <summary>
        /// 敌人是否落在干员攻击范围内。
        /// 移植自 WPF 版 InRange：干员占格中心为 (col+0.5, row+0.5)，判定半径半格。
        /// </summary>
        public static bool InRange(OperatorKind kind, Facing facing, int opCol, int opRow,
            double enemyCol, double enemyRow, int reachBonus = 0)
        {
            double relC = enemyCol - (opCol + 0.5);
            double relR = enemyRow - (opRow + 0.5);
            var cells = RangeCells(kind, facing, reachBonus);
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (relC >= cell.dc - 0.5 && relC < cell.dc + 0.5 &&
                    relR >= cell.dr - 0.5 && relR < cell.dr + 0.5)
                    return true;
            }
            return false;
        }
    }
}
