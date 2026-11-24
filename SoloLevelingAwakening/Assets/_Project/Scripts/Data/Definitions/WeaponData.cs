using UnityEngine;

namespace SLA.Data
{
    /// <summary>One weapon's stats (docs/design/items-and-economy.md).</summary>
    [CreateAssetMenu(menuName = "SLA/Data/Weapon")]
    public class WeaponData : ScriptableObject
    {
        public string displayName = "Rusty Dagger";
        public HunterRank rank = HunterRank.E;
        public float baseDamage = 10f;
        public int strengthRequirement;
        [Tooltip("PLACEHOLDER (not in spec yet).")]
        public float massKg = 0.4f;
        public bool twoHanded;
    }
}
