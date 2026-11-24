using System.Collections.Generic;

namespace SLA.AI
{
    /// <summary>ENM-2 / CMB-18: an enemy needs a token before it winds up; at most <see cref="Capacity"/> are out.</summary>
    public sealed class AttackTokenPool
    {
        private readonly HashSet<int> _holders = new HashSet<int>();

        public AttackTokenPool(int capacity)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }

        public int InUse => _holders.Count;

        public bool Holds(int holderId) => _holders.Contains(holderId);

        public bool TryAcquire(int holderId)
        {
            if (_holders.Contains(holderId))
            {
                return true;
            }
            if (_holders.Count >= Capacity)
            {
                return false;
            }
            _holders.Add(holderId);
            return true;
        }

        public void Release(int holderId)
        {
            _holders.Remove(holderId);
        }

        public void Clear()
        {
            _holders.Clear();
        }
    }
}
