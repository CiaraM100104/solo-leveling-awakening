using System;
using System.Collections.Generic;
using SLA.Core;

namespace SLA.Items
{
    /// <summary>
    /// Pure inventory (ITM-9): a fixed number of slots, no weight limit, stacks up to each item's max, plus gold.
    /// </summary>
    public sealed class Inventory
    {
        private readonly ItemStack[] _slots;
        private readonly Func<string, int> _maxStack;

        public Inventory(int slotCount, Func<string, int> maxStack)
        {
            _slots = new ItemStack[slotCount];
            _maxStack = maxStack;
        }

        public int SlotCount => _slots.Length;
        public int Gold { get; private set; }
        public IReadOnlyList<ItemStack> Slots => _slots;

        /// <summary>Copy of the slots, for publishing to the UI.</summary>
        public ItemStack[] Snapshot() => (ItemStack[])_slots.Clone();

        public int Count(string id)
        {
            int total = 0;
            foreach (var slot in _slots)
            {
                if (slot.id == id)
                {
                    total += slot.count;
                }
            }
            return total;
        }

        /// <summary>How many of <paramref name="id"/> would fit right now.</summary>
        public int SpaceFor(string id)
        {
            int max = Math.Max(1, _maxStack(id));
            int space = 0;
            foreach (var slot in _slots)
            {
                if (slot.IsEmpty)
                {
                    space += max;
                }
                else if (slot.id == id)
                {
                    space += Math.Max(0, max - slot.count);
                }
            }
            return space;
        }

        /// <summary>Adds items, topping up existing stacks first. Returns how many did not fit.</summary>
        public int Add(string id, int count)
        {
            if (string.IsNullOrEmpty(id) || count <= 0)
            {
                return 0;
            }
            int max = Math.Max(1, _maxStack(id));
            int left = count;

            for (int i = 0; i < _slots.Length && left > 0; i++)
            {
                if (_slots[i].id == id && _slots[i].count < max)
                {
                    int moved = Math.Min(left, max - _slots[i].count);
                    _slots[i].count += moved;
                    left -= moved;
                }
            }
            for (int i = 0; i < _slots.Length && left > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    int moved = Math.Min(left, max);
                    _slots[i] = new ItemStack(id, moved);
                    left -= moved;
                }
            }
            return left;
        }

        /// <summary>Removes <paramref name="count"/> of an item, all or nothing.</summary>
        public bool TryRemove(string id, int count)
        {
            if (count <= 0 || Count(id) < count)
            {
                return false;
            }
            int left = count;
            // Take from the last stacks first so the first slots stay full.
            for (int i = _slots.Length - 1; i >= 0 && left > 0; i--)
            {
                if (_slots[i].id != id)
                {
                    continue;
                }
                int taken = Math.Min(left, _slots[i].count);
                _slots[i].count -= taken;
                left -= taken;
                if (_slots[i].count <= 0)
                {
                    _slots[i] = default;
                }
            }
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount > 0)
            {
                Gold += amount;
            }
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Gold < amount)
            {
                return false;
            }
            Gold -= amount;
            return true;
        }

        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
            Gold = 0;
        }

        /// <summary>Restores from a save. Returns how many items no longer fit (slot count shrank).</summary>
        public int Load(IEnumerable<ItemStack> stacks, int gold)
        {
            Clear();
            Gold = Math.Max(0, gold);
            int lost = 0;
            foreach (var stack in stacks)
            {
                lost += Add(stack.id, stack.count);
            }
            return lost;
        }
    }
}
