using SLA.Data;
using UnityEngine;

namespace SLA.Progression
{
    /// <summary>Pure loot rules (docs/design/items-and-economy.md).</summary>
    public static class Loot
    {
        /// <summary>
        /// Gold for one kill: the enemy's fixed amount if it has one (bosses), otherwise a roll in the
        /// gate rank's range. <paramref name="random01"/> is in [0, 1).
        /// </summary>
        public static int GoldForKill(EnemyData enemy, HunterRank gateRank, EconomyConfig config, float random01)
        {
            if (enemy.fixedGold > 0)
            {
                return enemy.fixedGold;
            }
            int min = config.goldPerKillMin[(int)gateRank];
            int max = config.goldPerKillMax[(int)gateRank];
            int roll = min + Mathf.FloorToInt(Mathf.Clamp01(random01) * (max - min + 1));
            return Mathf.Min(roll, max);
        }

        /// <summary>Whether a regular kill drops a potion (drops table). Bosses don't roll potions.</summary>
        public static bool DropsPotion(EnemyData enemy, HunterRank gateRank, EconomyConfig config, float random01)
        {
            return !enemy.isBoss && random01 < config.potionDropChance[(int)gateRank];
        }
    }
}
