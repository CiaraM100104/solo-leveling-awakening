using UnityEngine;

namespace SLA.Data
{
    /// <summary>
    /// PLACEHOLDER scoring for the Scores board. The GDD has no score system yet; these weights need a spec
    /// (a new SCR section in docs/design) before they are tuned.
    /// </summary>
    [CreateAssetMenu(menuName = "SLA/Config/Score Config")]
    public class ScoreConfig : ScriptableObject
    {
        public int pointsPerKill = 100;
        public int pointsPerXp = 1;
        public int pointsPerGold = 2;
        [Tooltip("Clearing faster than this earns a time bonus.")]
        public float parSeconds = 600f;
        public int pointsPerSecondUnderPar = 5;
        public int deathPenalty = 500;
        [Tooltip("Bonus for clearing without taking damage (the Unbroken title's condition, PRG-16).")]
        public int noDamageBonus = 1000;
        public int boardSize = 10;
    }
}
