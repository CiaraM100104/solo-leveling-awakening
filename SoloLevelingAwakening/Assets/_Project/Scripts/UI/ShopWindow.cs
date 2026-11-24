using SLA.Core;
using SLA.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SLA.UI
{
    /// <summary>
    /// Placeholder shop window: one row per stocked item with a Buy button, a sell-materials button, and gold.
    /// Buy buttons are greyed out when the player can't afford the item.
    /// </summary>
    public class ShopWindow : MonoBehaviour
    {
        [SerializeField] private ShopData _shop;
        [SerializeField] private InventoryStatusChannel _inventory;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _gold;
        [Tooltip("One label and one Buy button per stocked item, in stock order.")]
        [SerializeField] private TMP_Text[] _rows = new TMP_Text[0];
        [SerializeField] private Button[] _buyButtons = new Button[0];

        private void Awake()
        {
            _title.text = $"<color=#3FB8FF><b>{_shop.shopName.ToUpperInvariant()}</b></color>";
            for (int i = 0; i < _rows.Length; i++)
            {
                var item = i < _shop.stock.Length ? _shop.stock[i] : null;
                _rows[i].text = item != null ? $"{item.displayName}  <color=#FFC94A>{item.buyPrice} G</color>" : "";
                _buyButtons[i].gameObject.SetActive(item != null);
            }
        }

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
            _gold.text = string.Format("Gold: <color=#FFC94A>{0:N0} G</color>", status.Gold);
            for (int i = 0; i < _buyButtons.Length && i < _shop.stock.Length; i++)
            {
                _buyButtons[i].interactable = status.Gold >= _shop.stock[i].buyPrice;
            }
        }
    }
}
