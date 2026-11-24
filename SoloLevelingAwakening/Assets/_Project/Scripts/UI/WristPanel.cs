using SLA.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SLA.UI
{
    /// <summary>
    /// UI-4 (first cut): level, rank, gold, potions and HP / MP / XP bars. Used on the left wrist (diegetic mode)
    /// and, as a second copy at the screen edge, for HUD mode (UI-15). Showing it only when the wrist faces the
    /// eyes, Fatigue and cooldown rings come later.
    /// </summary>
    public class WristPanel : MonoBehaviour
    {
        private static readonly string[] RankNames = { "E", "D", "C", "B", "A", "S" };

        [SerializeField] private PlayerStatusChannel _status;
        [SerializeField] private InventoryStatusChannel _inventory;
        [SerializeField] private string _potionId = "health_potion";
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Image _background;
        [Tooltip("Filled by scaling its right anchor.")]
        [SerializeField] private RectTransform _hpFill;
        [SerializeField] private RectTransform _mpFill;
        [SerializeField] private RectTransform _xpFill;

        private PlayerStatus _player;
        private int _gold;
        private int _potions;

        private void Awake()
        {
            _background.color = SystemPalette.Background;
            _text.color = SystemPalette.Text;
        }

        private void OnEnable()
        {
            _status.Raised += OnStatus;
            _inventory.Raised += OnInventory;
            if (_inventory.HasValue)
            {
                OnInventory(_inventory.Last);
            }
            if (_status.HasValue)
            {
                OnStatus(_status.Last);
            }
        }

        private void OnDisable()
        {
            _status.Raised -= OnStatus;
            _inventory.Raised -= OnInventory;
        }

        private void OnStatus(PlayerStatus status)
        {
            _player = status;
            Refresh();
        }

        private void OnInventory(InventoryStatus inventory)
        {
            _gold = inventory.Gold;
            _potions = inventory.Count(_potionId);
            Refresh();
        }

        // Runs only when something changes, never per frame, so the string work is fine.
        private void Refresh()
        {
            var s = _player;
            string rank = RankNames[Mathf.Clamp(s.RankIndex, 0, RankNames.Length - 1)];
            string points = s.UnspentStatPoints > 0 ? string.Format("  <color=#FFC94A>+{0} pts</color>", s.UnspentStatPoints) : "";
            string xp = s.XpToNext > 0 ? string.Format("{0}/{1}", s.Xp, s.XpToNext) : "MAX";
            _text.text = string.Format("Lv {0}  {1}-Rank  {2} G  Potions {3}{4}\nHP {5}/{6}   MP {7}/{8}   XP {9}",
                s.Level, rank, _gold, _potions, points, s.Hp, s.MaxHp, s.Mp, s.MaxMp, xp);
            SetFill(_hpFill, s.MaxHp > 0 ? s.Hp / (float)s.MaxHp : 0f);
            SetFill(_mpFill, s.MaxMp > 0 ? s.Mp / (float)s.MaxMp : 0f);
            SetFill(_xpFill, s.XpToNext > 0 ? s.Xp / (float)s.XpToNext : 1f);
        }

        private static void SetFill(RectTransform fill, float fraction)
        {
            if (fill == null)
            {
                return;
            }
            var max = fill.anchorMax;
            max.x = Mathf.Clamp01(fraction);
            fill.anchorMax = max;
        }
    }
}
