using UnityEngine;

namespace SLA.Core
{
    /// <summary>An event carrying one int (an amount, a level, a stat index).</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Int Event Channel")]
    public class IntEventChannel : EventChannel<int>
    {
    }
}
