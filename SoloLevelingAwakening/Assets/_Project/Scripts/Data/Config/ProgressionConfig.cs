using UnityEngine;

namespace SLA.Data
{
    /// <summary>
    /// Every tunable progression number (docs/design/progression.md, Data). Defaults are the spec values.
    /// </summary>
    [CreateAssetMenu(menuName = "SLA/Config/Progression Config")]
    public class ProgressionConfig : ScriptableObject
    {
        [Header("XP (PRG-1, PRG-2)")]
        public float xpBase = 50f;
        public float xpExponent = 1.6f;
        public int levelCap = 50;
        [Tooltip("XP multiplier per gate rank, E to S.")]
        public int[] rankXpMultipliers = { 1, 3, 8, 20, 45, 100 };

        [Header("Rewards (PRG-3, PRG-4)")]
        public int statPointsPerLevel = 5;
        public int skillPointsAfterLevel = 10;

        [Header("Stats (PRG-5 to PRG-9)")]
        public int baseStat = 10;
        public int baseHp = 100;
        public int hpPerVitality = 10;
        public int baseMp = 20;
        public int mpPerIntelligence = 2;
    }
}
