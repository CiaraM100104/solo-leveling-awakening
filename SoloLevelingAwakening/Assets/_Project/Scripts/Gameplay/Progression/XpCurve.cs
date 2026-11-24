using System;
using SLA.Data;

namespace SLA.Progression
{
    /// <summary>Pure XP math (PRG-1, PRG-2, PRG-12).</summary>
    public static class XpCurve
    {
        private const int LevelsPerRank = 10;

        /// <summary>PRG-1: round(xpBase × L^xpExponent), rounding halves up.</summary>
        public static int XpToNext(int level, ProgressionConfig config)
        {
            double xp = config.xpBase * Math.Pow(level, config.xpExponent);
            return (int)Math.Round(xp, MidpointRounding.AwayFromZero);
        }

        /// <summary>PRG-2: base XP × the gate rank's multiplier.</summary>
        public static int EnemyXp(int baseXp, HunterRank gateRank, ProgressionConfig config)
        {
            return baseXp * config.rankXpMultipliers[(int)gateRank];
        }

        /// <summary>PRG-12: E 1–9, D 10–19, C 20–29, B 30–39, A 40–49, S 50.</summary>
        public static HunterRank RankForLevel(int level)
        {
            int index = Math.Max(0, Math.Min(level / LevelsPerRank, (int)HunterRank.S));
            return (HunterRank)index;
        }
    }
}
