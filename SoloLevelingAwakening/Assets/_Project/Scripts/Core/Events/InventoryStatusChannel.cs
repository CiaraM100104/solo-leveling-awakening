using UnityEngine;

namespace SLA.Core
{
    /// <summary>What the inventory holds, for the UI.</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Inventory Status Channel")]
    public class InventoryStatusChannel : EventChannel<InventoryStatus>
    {
    }
}
