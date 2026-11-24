using UnityEngine;

namespace SLA.Core
{
    /// <summary>An event carrying a string (an item id, an instruction).</summary>
    [CreateAssetMenu(menuName = "SLA/Events/String Event Channel")]
    public class StringEventChannel : EventChannel<string>
    {
    }
}
