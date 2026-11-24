using UnityEngine;

namespace SLA.Core
{
    /// <summary>Items given to the player: loot drops, purchases, rewards.</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Item Grant Channel")]
    public class ItemGrantChannel : EventChannel<ItemStack>
    {
    }
}
