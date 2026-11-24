using SLA.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SLA.UI
{
    /// <summary>The daily quest's System window: reps and round, never calories (QST-7), with Start and Rest (QST-17).</summary>
    public class DailyQuestWindow : MonoBehaviour
    {
        [SerializeField] private DailyQuestStatusChannel _progress;
        [SerializeField] private VoidEventChannel _startRequested;
        [SerializeField] private VoidEventChannel _restRequested;
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Button _startButton;
        [SerializeField] private TMP_Text _startLabel;
        [SerializeField] private Button _restButton;

        private void Awake()
        {
            _background.color = SystemPalette.Background;
            _text.color = SystemPalette.Text;
        }

        private void OnEnable()
        {
            _progress.Raised += Show;
            if (_progress.HasValue)
            {
                Show(_progress.Last);
            }
        }

        private void OnDisable()
        {
            _progress.Raised -= Show;
        }

        /// <summary>Wired to the Start / Resume button.</summary>
        public void StartQuest() => _startRequested.Raise();

        /// <summary>Wired to the Rest button.</summary>
        public void Rest() => _restRequested.Raise();

        private void Show(DailyQuestStatus s)
        {
            string state;
            switch (s.State)
            {
                case DailyQuestState.Calibrating: state = "Stand still..."; break;
                case DailyQuestState.InProgress:
                    state = string.Format("Round {0} / {1}: {2}", s.Round + 1, s.RoundCount, s.IsSquatRound ? "Squats" : "Punches");
                    break;
                case DailyQuestState.Resting: state = "Resting. Resume when ready."; break;
                case DailyQuestState.Complete: state = "<color=#FFC94A>Complete!</color>"; break;
                default: state = "Ready"; break;
            }

            _text.text = string.Format(
                "<color=#3FB8FF><b>DAILY QUEST: STRENGTH TRAINING</b></color>\nSquats  {0} / {1}\nPunches  {2} / {3}\n{4}",
                s.SquatsDone, s.SquatsTarget, s.PunchesDone, s.PunchesTarget, state);

            bool canStart = s.State == DailyQuestState.Available || s.State == DailyQuestState.Resting;
            _startButton.interactable = canStart;
            _startLabel.text = s.State == DailyQuestState.Resting ? "Resume" : "Start";
            _restButton.interactable = s.State == DailyQuestState.InProgress || s.State == DailyQuestState.Calibrating;
        }
    }
}
