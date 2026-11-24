using SLA.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SLA.UI
{
    /// <summary>
    /// UI-4 (first cut): level, rank, gold and HP / MP bars on the left wrist. Always visible for now;
    /// showing it only when the wrist faces the eyes, Fatigue and cooldown rings come later.
    /// </summary>
    public class WristPanel : MonoBehaviour
    {
        private static readonly string[] RankNames = { "E", "D", "C", "B", "A", "S" };

        [SerializeField] private PlayerStatusChannel _status;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Image _background;
        [Tooltip("Filled by scaling its right anchor.")]
        [SerializeField] private RectTransform _hpFill;
        [SerializeField] private RectTransform _mpFill;

        private void Awake()
        {
            _background.color = SystemPalette.Background;
            _text.color = SystemPalette.Text;
        }

        private void OnEnable()
        {
            _status.Raised += Show;
            if (_status.HasValue)
            {
                Show(_status.Last);
            }
        }

        private void OnDisable()
        {
            _status.Raised -= Show;
        }

        // Runs only when the status changes, never per frame, so the string work is fine.
        private void Show(PlayerStatus s)
        {
            string rank = RankNames[Mathf.Clamp(s.RankIndex, 0, RankNames.Length - 1)];
            string points = s.UnspentStatPoints > 0 ? string.Format("   <color=#FFC94A>+{0} pts</color>", s.UnspentStatPoints) : "";
            _text.text = string.Format("Lv {0}  {1}-Rank   {2} G{3}\nHP {4}/{5}\nMP {6}/{7}",
                s.Level, rank, s.Gold, points, s.Hp, s.MaxHp, s.Mp, s.MaxMp);
            SetFill(_hpFill, s.MaxHp > 0 ? s.Hp / (float)s.MaxHp : 0f);
            SetFill(_mpFill, s.MaxMp > 0 ? s.Mp / (float)s.MaxMp : 0f);
        }

        private static void SetFill(RectTransform fill, float fraction)
        {
            var max = fill.anchorMax;
            max.x = Mathf.Clamp01(fraction);
            fill.anchorMax = max;
        }
    }
}
