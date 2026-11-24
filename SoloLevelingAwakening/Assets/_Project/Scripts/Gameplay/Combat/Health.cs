using System;
using UnityEngine;

namespace SLA.Combat
{
    /// <summary>Hit points for the player or an enemy. Raises Damaged, Changed and Died.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private int _maxHp = 100;

        /// <summary>Damage dealt and the world hit point.</summary>
        public event Action<int, Vector3> Damaged;
        public event Action Died;
        /// <summary>Any change to current or max HP.</summary>
        public event Action Changed;

        public int Current { get; private set; }
        public int Max => _maxHp;
        public bool IsDead => Current <= 0;
        public float Armor { get; set; }
        public bool IsStaggered { get; set; }

        private void Awake()
        {
            Current = _maxHp;
        }

        public void SetMax(int maxHp, bool refill)
        {
            _maxHp = Mathf.Max(1, maxHp);
            Current = refill ? _maxHp : Mathf.Min(Current, _maxHp);
            Changed?.Invoke();
        }

        public void RestoreFull()
        {
            Current = _maxHp;
            Changed?.Invoke();
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0)
            {
                return;
            }
            Current = Mathf.Min(_maxHp, Current + amount);
            Changed?.Invoke();
        }

        /// <summary>Applies damage and returns the whole points actually dealt.</summary>
        public int TakeDamage(float amount, Vector3 hitPoint)
        {
            if (IsDead)
            {
                return 0;
            }
            int dealt = Mathf.Min(Mathf.RoundToInt(amount), Current);
            if (dealt <= 0)
            {
                return 0;
            }

            Current -= dealt;
            Damaged?.Invoke(dealt, hitPoint);
            Changed?.Invoke();
            if (Current <= 0)
            {
                Died?.Invoke();
            }
            return dealt;
        }
    }
}
