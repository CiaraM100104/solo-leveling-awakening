using UnityEngine;

namespace SLA.Data
{
    /// <summary>
    /// One enemy type's stats (docs/design/enemies-and-ai.md). Fields marked PLACEHOLDER are not in the spec yet.
    /// </summary>
    [CreateAssetMenu(menuName = "SLA/Data/Enemy")]
    public class EnemyData : ScriptableObject
    {
        public string displayName = "Goblin";
        public int maxHp = 40;
        public int damagePerHit = 8;
        public float armor;

        [Tooltip("PLACEHOLDER: base XP before the gate-rank multiplier (PRG-2).")]
        public int baseXp = 20;
        [Tooltip("Gold on kill instead of the rank's random range (bosses). 0 = use the range.")]
        public int fixedGold;

        [Header("Boss (ENM-7)")]
        public bool isBoss;
        [Tooltip("Phase 2 starts at this fraction of max HP.")]
        [Range(0f, 1f)] public float phase2HpFraction = 0.5f;

        [Header("Timings (ENM-3)")]
        [Range(0.6f, 1f)] public float windupSeconds = 0.8f;
        [Tooltip("PLACEHOLDER: the swing's active frames; parries land in the first part (CMB-14).")]
        public float attackSeconds = 0.25f;
        [Tooltip("PLACEHOLDER: the punish window after an attack.")]
        public float recoverSeconds = 1f;

        [Header("Movement (PLACEHOLDERS)")]
        public float moveSpeedMetersPerSecond = 1.6f;
        public float sightRangeMeters = 20f;
        public float circleRadiusMeters = 2f;
        public float attackRangeMeters = 1.1f;
        public float corpseSeconds = 3f;
    }
}
