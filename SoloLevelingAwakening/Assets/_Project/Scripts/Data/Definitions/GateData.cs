using UnityEngine;

namespace SLA.Data
{
    /// <summary>One gate's facts, shown on its portal panel (UI-6) and used for rewards (docs/design/gates.md).</summary>
    [CreateAssetMenu(menuName = "SLA/Data/Gate")]
    public class GateData : ScriptableObject
    {
        public string displayName = "Goblin Warren";
        public HunterRank rank = HunterRank.E;
        public int recommendedMinLevel = 1;
        public int recommendedMaxLevel = 9;
        [Tooltip("Shown on the gate panel.")]
        public string enemyTypes = "Goblins, kobolds";
        [Tooltip("Red gates cannot be left until the boss is defeated (GTE-6).")]
        public bool isRed;
    }
}
