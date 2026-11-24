using SLA.Core;
using SLA.Data;
using UnityEngine;

namespace SLA.Items
{
    /// <summary>
    /// The System inventory (ITM-9): loot goes straight in, gold is kept here, potions are used from it,
    /// and it is saved and loaded with the rest of the player.
    /// </summary>
    public class InventoryService : MonoBehaviour
    {
        [SerializeField] private ItemDatabase _items;
        [SerializeField] private EconomyConfig _economy;

        [Header("Channels in")]
        [SerializeField] private ItemGrantChannel _onItemGranted;
        [SerializeField] private IntEventChannel _onGoldGained;
        [Tooltip("An item id the player is trying to use (a potion raised to the mouth).")]
        [SerializeField] private StringEventChannel _onUseRequested;
        [SerializeField] private VoidEventChannel _onNewGame;
        [SerializeField] private SaveDataChannel _onCollectSave;
        [SerializeField] private SaveDataChannel _onApplySave;

        [Header("Channels out")]
        [SerializeField] private InventoryStatusChannel _status;
        [SerializeField] private ItemDataEventChannel _onItemUsed;
        [SerializeField] private SystemMessageChannel _messages;

        private Inventory _inventory;

        public Inventory Inventory => _inventory;
        public ItemDatabase Items => _items;

        private void Awake()
        {
            _inventory = new Inventory(_economy.inventorySlots, _items.MaxStack);
        }

        private void OnEnable()
        {
            _onItemGranted.Raised += Grant;
            _onGoldGained.Raised += AddGold;
            _onUseRequested.Raised += Use;
            _onNewGame.Raised += ResetForNewGame;
            _onCollectSave.Raised += Collect;
            _onApplySave.Raised += Apply;
        }

        private void OnDisable()
        {
            _onItemGranted.Raised -= Grant;
            _onGoldGained.Raised -= AddGold;
            _onUseRequested.Raised -= Use;
            _onNewGame.Raised -= ResetForNewGame;
            _onCollectSave.Raised -= Collect;
            _onApplySave.Raised -= Apply;
        }

        private void Start()
        {
            Publish();
        }

        private void Grant(ItemStack stack)
        {
            var item = _items.Find(stack.id);
            int notAdded = _inventory.Add(stack.id, stack.count);
            int added = stack.count - notAdded;
            string name = item != null ? item.displayName : stack.id;
            if (added > 0)
            {
                _messages.Raise(new SystemMessage(SystemMessageKind.Reward,
                    added > 1 ? $"You have acquired {name} ×{added}." : $"You have acquired {name}."));
            }
            if (notAdded > 0)
            {
                _messages.Raise(new SystemMessage(SystemMessageKind.Warning, $"Inventory full. {name} was lost."));
            }
            Publish();
        }

        private void AddGold(int gold)
        {
            _inventory.AddGold(gold);
            Publish();
        }

        private void Use(string id)
        {
            var item = _items.Find(id);
            if (item == null || !_inventory.TryRemove(id, 1))
            {
                return;
            }
            _onItemUsed.Raise(item);
            Publish();
        }

        /// <summary>For the shop: spend gold and add the item together, or do neither.</summary>
        public bool TryBuy(ItemData item)
        {
            if (_inventory.SpaceFor(item.id) < 1 || !_inventory.TrySpend(item.buyPrice))
            {
                return false;
            }
            _inventory.Add(item.id, 1);
            Publish();
            return true;
        }

        /// <summary>For the shop: sells every item of the given kinds. Returns the gold earned.</summary>
        public int SellAll(ItemKind[] kinds)
        {
            int earned = 0;
            foreach (var item in _items.items)
            {
                if (item == null || item.sellPrice <= 0 || System.Array.IndexOf(kinds, item.kind) < 0)
                {
                    continue;
                }
                int count = _inventory.Count(item.id);
                if (count > 0 && _inventory.TryRemove(item.id, count))
                {
                    earned += count * item.sellPrice;
                }
            }
            _inventory.AddGold(earned);
            Publish();
            return earned;
        }

        private void ResetForNewGame()
        {
            _inventory.Clear();
            Publish();
        }

        private void Collect(SaveData save)
        {
            save.gold = _inventory.Gold;
            save.inventory.Clear();
            foreach (var stack in _inventory.Slots)
            {
                if (!stack.IsEmpty)
                {
                    save.inventory.Add(stack);
                }
            }
        }

        private void Apply(SaveData save)
        {
            int lost = _inventory.Load(save.inventory, save.gold);
            if (lost > 0)
            {
                Debug.LogWarning($"[SLA] {lost} saved items did not fit in the inventory.");
            }
            Publish();
        }

        private void Publish()
        {
            _status.Raise(new InventoryStatus(_inventory.Snapshot(), _inventory.Gold));
        }
    }
}
