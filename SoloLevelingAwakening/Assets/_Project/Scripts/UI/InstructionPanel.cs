using SLA.Core;
using TMPro;
using UnityEngine;

namespace SLA.UI
{
    /// <summary>Shows the latest instruction string, such as the tutorial's current step.</summary>
    public class InstructionPanel : MonoBehaviour
    {
        [SerializeField] private StringEventChannel _instruction;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private string _heading = "TRAINING";

        private void OnEnable()
        {
            _instruction.Raised += Show;
            Show(_instruction.HasValue ? _instruction.Last : "");
        }

        private void OnDisable()
        {
            _instruction.Raised -= Show;
        }

        private void Show(string text)
        {
            _text.text = $"<color=#3FB8FF><b>{_heading}</b></color>\n{text}";
        }
    }
}
