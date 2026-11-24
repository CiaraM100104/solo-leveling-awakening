using SLA.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SLA.UI
{
    /// <summary>UI-6: the panel above a gate portal: rank, recommended level, enemy types, Blue or Red.</summary>
    public class GatePanel : MonoBehaviour
    {
        [SerializeField] private GateData _gate;
        [Tooltip("Four thin edge strips: blue for Blue gates, red for Red (UI-1).")]
        [SerializeField] private Image[] _border = new Image[0];
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _text;

        private void Awake()
        {
            _background.color = SystemPalette.Background;
            foreach (var strip in _border)
            {
                strip.color = _gate.isRed ? SystemPalette.Warning : SystemPalette.Info;
            }
            _text.color = SystemPalette.Text;
            _text.text = string.Format("<b>{0}</b>\n{1}-Rank {2} Gate\nRecommended Lv {3}–{4}\n{5}",
                _gate.displayName.ToUpperInvariant(), _gate.rank, _gate.isRed ? "Red" : "Blue",
                _gate.recommendedMinLevel, _gate.recommendedMaxLevel, _gate.enemyTypes);
        }
    }
}
