using UnityEngine;

namespace SLA.Data
{
    /// <summary>What kind of item, for sorting and for what the shop buys back.</summary>
    public enum ItemKind
    {
        Consumable,
        Material,
        Weapon,
        Gloves,
        Key,
    }

    /// <summary>
    /// One item (docs/design/items-and-economy.md). <see cref="id"/> is what the save file stores; never rename it.
    /// </summary>
    [CreateAssetMenu(menuName = "SLA/Data/Item")]
    public class ItemData : ScriptableObject
    {
        public string id = "health_potion";
        public string displayName = "Health Potion";
        public ItemKind kind = ItemKind.Consumable;
        public HunterRank rank = HunterRank.E;
        [TextArea] public string description = "";

        [Header("Economy")]
        [Tooltip("Shop price in gold. 0 = not sold.")]
        public int buyPrice;
        [Tooltip("Gold when sold back. 0 = can't be sold.")]
        public int sellPrice;
        [Tooltip("PLACEHOLDER: how many fit in one inventory slot.")]
        public int maxStack = 10;

        [Header("Consumable effect (ITM-4)")]
        public int healHp;
        public int restoreMp;
        [Tooltip("Heal and restore spread over this many seconds.")]
        public float effectSeconds;
    }
}
