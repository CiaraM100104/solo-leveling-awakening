using SLA.Core;
using TMPro;
using UnityEngine;

namespace SLA.UI
{
    /// <summary>
    /// The Results screen (screen flow: "Results: XP, loot, level-ups"), shown in the boss room once the boss falls.
    /// Hidden again when the player leaves the gate.
    /// </summary>
    public class ResultsWindow : MonoBehaviour
    {
        [SerializeField] private RunRecordChannel _onResultsReady;
        [SerializeField] private TravelRequestChannel _arrived;
        [SerializeField] private int _gateAreaId = 1;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _text;

        private void Awake()
        {
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            _onResultsReady.Raised += Show;
            _arrived.Raised += OnArrived;
        }

        private void OnDisable()
        {
            _onResultsReady.Raised -= Show;
            _arrived.Raised -= OnArrived;
        }

        private void Show(RunRecord run)
        {
            string placement = run.placement == 1
                ? "<color=#FFC94A>New best score!</color>"
                : run.placement > 0 ? $"#{run.placement} on the Scores board" : "";
            _text.text = string.Format(
                "<color=#FFC94A><b>GATE CLEARED</b></color>\n{0}\n\nTime  {1}\nKills  {2}\nXP  +{3}\nGold  +{4}\nDamage taken  {5}\nFalls  {6}\n\n<b>Score  {7:N0}</b>\n{8}\n\nStep through the gate to return home.",
                run.gateName, FormatTime(run.timeSeconds), run.kills, run.xp, run.gold, run.damageTaken, run.deaths,
                run.score, placement);
            _panel.SetActive(true);
        }

        private void OnArrived(TravelRequest request)
        {
            if (request.AreaId != _gateAreaId)
            {
                _panel.SetActive(false);
            }
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return string.Format("{0}:{1:00}", total / 60, total % 60);
        }
    }
}
