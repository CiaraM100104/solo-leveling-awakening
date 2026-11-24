using UnityEngine;

namespace SLA.Core
{
    /// <summary>The high-score board, best first.</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Scores Channel")]
    public class ScoresChannel : EventChannel<RunRecord[]>
    {
    }
}
