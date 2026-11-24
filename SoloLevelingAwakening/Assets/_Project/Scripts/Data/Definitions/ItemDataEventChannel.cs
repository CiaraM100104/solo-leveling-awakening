using SLA.Core;
using UnityEngine;

namespace SLA.Data
{
    /// <summary>An item that was just used (a potion drunk).</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Item Data Channel")]
    public class ItemDataEventChannel : EventChannel<ItemData>
    {
    }
}
