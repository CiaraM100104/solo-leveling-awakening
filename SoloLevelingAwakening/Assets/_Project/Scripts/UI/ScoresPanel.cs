using System.Text;
using SLA.Core;
using TMPro;
using UnityEngine;

namespace SLA.UI
{
    /// <summary>The Scores board on the title screen: the best gate runs, best first.</summary>
    public class ScoresPanel : MonoBehaviour
    {
        private const int Shown = 5;

        [SerializeField] private ScoresChannel _scores;
        [SerializeField] private TMP_Text _text;

        private void OnEnable()
        {
            _scores.Raised += Show;
            Show(_scores.HasValue ? _scores.Last : new RunRecord[0]);
        }

        private void OnDisable()
        {
            _scores.Raised -= Show;
        }

        private void Show(RunRecord[] board)
        {
            var text = new StringBuilder("<color=#3FB8FF><b>SCORES</b></color>\n");
            if (board.Length == 0)
            {
                text.Append("\nNo gates cleared yet.\nClear a gate to set a score.");
            }
            for (int i = 0; i < board.Length && i < Shown; i++)
            {
                var run = board[i];
                text.AppendFormat("\n{0}. <b>{1:N0}</b>  {2}  {3}  {4} kills", i + 1, run.score, run.gateName,
                    ResultsWindow.FormatTime(run.timeSeconds), run.kills);
            }
            _text.text = text.ToString();
        }
    }
}
