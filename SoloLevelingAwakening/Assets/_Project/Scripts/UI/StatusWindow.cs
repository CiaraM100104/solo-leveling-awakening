using SLA.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SLA.UI
{
    /// <summary>
    /// UI-5 Status window: level, job, rank, title, HP / MP, the five stats with "+" buttons and unspent points.
    /// Pinned to a hub wall for now (MRH-7); opening it with X comes later. Fatigue joins when it exists.
    /// </summary>
    public class StatusWindow : MonoBehaviour
    {
        private static readonly string[] RankNames = { "E", "D", "C", "B", "A", "S" };
        private static readonly string[] StatNames = { "Strength", "Vitality", "Agility", "Perception", "Intelligence" };

        [SerializeField] private PlayerStatusChannel _status;
        [Tooltip("Raised with the stat index when a + button is pressed.")]
        [SerializeField] private IntEventChannel _allocateStat;
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _summary;
        [SerializeField] private TMP_Text[] _statTexts = new TMP_Text[5];
        [SerializeField] private Button[] _plusButtons = new Button[5];
        [SerializeField] private TMP_Text _points;

        private void Awake()
        {
            _background.color = SystemPalette.Background;
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

        /// <summary>Wired to each + button with its stat index.</summary>
        public void Allocate(int statIndex)
        {
            _allocateStat.Raise(statIndex);
        }

        private void Show(PlayerStatus s)
        {
            string rank = RankNames[Mathf.Clamp(s.RankIndex, 0, RankNames.Length - 1)];
            string xp = s.XpToNext > 0 ? string.Format("{0} / {1}", s.Xp, s.XpToNext) : "MAX";
            _summary.text = string.Format(
                "Level <b>{0}</b>     Rank <b>{1}</b>\nJob: None     Title: None\nHP {2} / {3}     MP {4} / {5}\nXP {6}     Gold {7}",
                s.Level, rank, s.Hp, s.MaxHp, s.Mp, s.MaxMp, xp, s.Gold);

            for (int i = 0; i < _statTexts.Length; i++)
            {
                _statTexts[i].text = string.Format("{0}  <b>{1}</b>", StatNames[i], s.Stat(i));
                _plusButtons[i].interactable = s.UnspentStatPoints > 0;
            }

            _points.text = s.UnspentStatPoints > 0
                ? string.Format("<color=#FFC94A>Available points: {0}</color>", s.UnspentStatPoints)
                : "Available points: 0";
        }
    }
}
