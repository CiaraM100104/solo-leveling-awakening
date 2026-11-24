using SLA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SLA.UI
{
    /// <summary>
    /// The title screen: Continue (only with a save), New Game, Tutorial, Scores and Quit. The buttons raise
    /// title commands through <see cref="ChannelButton"/>s; this only keeps Continue honest and toggles Scores.
    /// The disclaimer text (GDD Legal &amp; IP, item 4) and the standing-play note (CMF-13) are set by the builder.
    /// </summary>
    public class TitleMenu : MonoBehaviour
    {
        [SerializeField] private IntEventChannel _saveExists;
        [SerializeField] private Button _continueButton;
        [SerializeField] private GameObject _scoresPanel;

        private void OnEnable()
        {
            _saveExists.Raised += OnSaveExists;
            OnSaveExists(_saveExists.HasValue ? _saveExists.Last : 0);
        }

        private void OnDisable()
        {
            _saveExists.Raised -= OnSaveExists;
        }

        private void OnSaveExists(int exists)
        {
            _continueButton.interactable = exists != 0;
        }

        /// <summary>Wired to the Scores button.</summary>
        public void ToggleScores()
        {
            _scoresPanel.SetActive(!_scoresPanel.activeSelf);
        }
    }
}
