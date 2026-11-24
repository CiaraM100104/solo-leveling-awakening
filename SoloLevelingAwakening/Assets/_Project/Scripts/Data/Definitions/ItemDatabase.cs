using UnityEngine;

namespace SLA.Data
{
    /// <summary>Every item the game knows, looked up by id when loading a save.</summary>
    [CreateAssetMenu(menuName = "SLA/Data/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        public ItemData[] items = new ItemData[0];

        /// <summary>The item with this id, or null.</summary>
        public ItemData Find(string id)
        {
            foreach (var item in items)
            {
                if (item != null && item.id == id)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>Stack size for an id; unknown ids stack to 1.</summary>
        public int MaxStack(string id)
        {
            var item = Find(id);
            return item != null ? Mathf.Max(1, item.maxStack) : 1;
        }
    }
}
