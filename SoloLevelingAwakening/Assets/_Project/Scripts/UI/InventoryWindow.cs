using SLA.Core;
using SLA.Data;
using TMPro;
using UnityEngine;

namespace SLA.UI
{
    /// <summary>The System inventory window in the hub (ITM-9): 20 slots and gold.</summary>
    public class InventoryWindow : MonoBehaviour
    {
        [SerializeField] private ItemDatabase _items;
        [SerializeField] private InventoryStatusChannel _inventory;
        [SerializeField] private TMP_Text _gold;
        [Tooltip("One text per slot, in order.")]
        [SerializeField] private TMP_Text[] _slots = new TMP_Text[0];

        private void OnEnable()
        {
            _inventory.Raised += Show;
            if (_inventory.HasValue)
            {
                Show(_inventory.Last);
            }
        }

        private void OnDisable()
        {
            _inventory.Raised -= Show;
        }

        private void Show(InventoryStatus status)
        {
            _gold.text = string.Format("<color=#FFC94A>{0:N0} G</color>", status.Gold);
            for (int i = 0; i < _slots.Length; i++)
            {
                var stack = status.Slots != null && i < status.Slots.Length ? status.Slots[i] : default;
                if (stack.IsEmpty)
                {
                    _slots[i].text = "<color=#5A6A80>—</color>";
                    continue;
                }
                var item = _items.Find(stack.id);
                string name = item != null ? item.displayName : stack.id;
                _slots[i].text = stack.count > 1 ? $"{name}\n×{stack.count}" : name;
            }
        }
    }
}
