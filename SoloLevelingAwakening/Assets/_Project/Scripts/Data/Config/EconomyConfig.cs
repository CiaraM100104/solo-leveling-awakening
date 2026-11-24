using UnityEngine;

namespace SLA.Data
{
    /// <summary>Drop numbers from docs/design/items-and-economy.md (drops and income table, ITM-10).</summary>
    [CreateAssetMenu(menuName = "SLA/Config/Economy Config")]
    public class EconomyConfig : ScriptableObject
    {
        [Tooltip("Lowest gold per regular kill by gate rank, E to S. A and S follow ITM-10 (B ×1.5, ×2).")]
        public int[] goldPerKillMin = { 5, 15, 40, 100, 150, 200 };
        [Tooltip("Highest gold per regular kill by gate rank, E to S.")]
        public int[] goldPerKillMax = { 10, 25, 60, 150, 225, 300 };
    }
}
