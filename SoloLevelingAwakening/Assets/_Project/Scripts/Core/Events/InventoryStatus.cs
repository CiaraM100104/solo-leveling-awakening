namespace SLA.Core
{
    /// <summary>What the inventory holds: 20 slots (ITM-9) and gold.</summary>
    public readonly struct InventoryStatus
    {
        public readonly ItemStack[] Slots;
        public readonly int Gold;

        public InventoryStatus(ItemStack[] slots, int gold)
        {
            Slots = slots;
            Gold = gold;
        }

        /// <summary>Total of one item across all slots.</summary>
        public int Count(string id)
        {
            int total = 0;
            if (Slots == null)
            {
                return 0;
            }
            foreach (var stack in Slots)
            {
                if (stack.id == id)
                {
                    total += stack.count;
                }
            }
            return total;
        }
    }
}
