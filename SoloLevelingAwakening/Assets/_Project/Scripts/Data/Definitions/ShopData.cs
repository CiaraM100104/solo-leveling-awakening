using UnityEngine;

namespace SLA.Data
{
    /// <summary>What a shop sells. Placeholder stock until the shop spec settles (items-and-economy.md open question).</summary>
    [CreateAssetMenu(menuName = "SLA/Data/Shop")]
    public class ShopData : ScriptableObject
    {
        public string shopName = "Hunter Supply";
        public ItemData[] stock = new ItemData[0];
        [Tooltip("Item kinds the shop buys back.")]
        public ItemKind[] buysKinds = { ItemKind.Material };
    }
}
