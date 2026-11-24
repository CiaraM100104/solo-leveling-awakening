using SLA.Combat;
using SLA.Data;
using UnityEngine;

namespace SLA.Items
{
    /// <summary>
    /// Applies used consumables to the player. ITM-4: a health potion heals 50 HP over 2 s.
    /// MP restore waits for an MP system (skills); Fatigue for the Fatigue system.
    /// </summary>
    public class ConsumableEffects : MonoBehaviour
    {
        [SerializeField] private Health _health;
        [SerializeField] private ItemDataEventChannel _onItemUsed;

        private float _healPerSecond;
        private float _healSecondsLeft;
        private float _healCarry;
        private int _healOwed;

        private void OnEnable()
        {
            _onItemUsed.Raised += OnItemUsed;
        }

        private void OnDisable()
        {
            _onItemUsed.Raised -= OnItemUsed;
        }

        private void OnItemUsed(ItemData item)
        {
            if (item.healHp <= 0)
            {
                return;
            }
            if (item.effectSeconds <= 0f)
            {
                _health.Heal(item.healHp);
                return;
            }
            // Stacking potions adds to whatever healing is still to come.
            _healOwed += item.healHp;
            _healSecondsLeft = Mathf.Max(_healSecondsLeft, item.effectSeconds);
            _healPerSecond = _healOwed / _healSecondsLeft;
        }

        private void Update()
        {
            if (_healSecondsLeft <= 0f)
            {
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, _healSecondsLeft);
            _healSecondsLeft -= dt;
            _healCarry += _healPerSecond * dt;
            int whole = Mathf.Min(Mathf.FloorToInt(_healCarry), _healOwed);
            if (_healSecondsLeft <= 0f)
            {
                whole = _healOwed; // The last frame pays whatever rounding left behind.
            }
            if (whole > 0)
            {
                _healCarry -= whole;
                _healOwed -= whole;
                _health.Heal(whole);
            }
            if (_healSecondsLeft <= 0f)
            {
                _healCarry = 0f;
            }
        }
    }
}
