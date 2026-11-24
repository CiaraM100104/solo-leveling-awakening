using SLA.Core;
using SLA.Data;
using UnityEngine;

namespace SLA.Items
{
    /// <summary>
    /// Placeholder hub shop (GDD Must/Should list: "shop"): buy from <see cref="ShopData"/> stock, sell monster cores.
    /// The shop's final home (hub window or Association merchant) is still an open question in items-and-economy.md.
    /// </summary>
    public class ShopService : MonoBehaviour
    {
        [SerializeField] private ShopData _shop;
        [SerializeField] private InventoryService _inventory;

        [Header("Channels")]
        [Tooltip("Index into the shop's stock.")]
        [SerializeField] private IntEventChannel _onBuyRequested;
        [SerializeField] private VoidEventChannel _onSellRequested;
        [SerializeField] private SystemMessageChannel _messages;

        private void OnEnable()
        {
            _onBuyRequested.Raised += Buy;
            _onSellRequested.Raised += Sell;
        }

        private void OnDisable()
        {
            _onBuyRequested.Raised -= Buy;
            _onSellRequested.Raised -= Sell;
        }

        private void Buy(int index)
        {
            if (index < 0 || index >= _shop.stock.Length)
            {
                return;
            }
            var item = _shop.stock[index];
            if (_inventory.TryBuy(item))
            {
                _messages.Raise(new SystemMessage(SystemMessageKind.Reward, $"Purchased {item.displayName} for {item.buyPrice} gold."));
            }
            else if (_inventory.Inventory.SpaceFor(item.id) < 1)
            {
                _messages.Raise(new SystemMessage(SystemMessageKind.Warning, "Inventory full."));
            }
            else
            {
                _messages.Raise(new SystemMessage(SystemMessageKind.Warning, $"Not enough gold. {item.displayName} costs {item.buyPrice}."));
            }
        }

        private void Sell()
        {
            int earned = _inventory.SellAll(_shop.buysKinds);
            _messages.Raise(earned > 0
                ? new SystemMessage(SystemMessageKind.Reward, $"Sold materials for {earned} gold.")
                : new SystemMessage(SystemMessageKind.Info, "Nothing to sell."));
        }
    }
}
