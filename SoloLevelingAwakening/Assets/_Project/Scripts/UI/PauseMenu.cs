using SLA.Core;
using TMPro;
using UnityEngine;

namespace SLA.UI
{
    /// <summary>
    /// Pause and settings (GDD input table: Menu button): resume, comfort preset, UI mode, return to title.
    /// Appears in front of the player when paused and stays put; the camera itself never moves (CMF-1).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private const int PresetCount = 3;
        private static readonly string[] PresetNames = { "Recommended", "Comfortable", "Advanced" };

        [SerializeField] private Transform _head;
        [SerializeField] private GameObject _panel;
        [SerializeField] private float _distanceMeters = 1.2f;
        [SerializeField] private TMP_Text _presetLabel;
        [SerializeField] private TMP_Text _uiModeLabel;
        [SerializeField] private TMP_Text _returnLabel;

        [Header("Channels")]
        [SerializeField] private GameStateChannel _state;
        [SerializeField] private SettingsChannel _settings;
        [SerializeField] private IntEventChannel _setPreset;
        [SerializeField] private IntEventChannel _setUiMode;

        private GameState _underlying;
        private int _preset;
        private int _uiMode;

        private void Awake()
        {
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            _state.Raised += OnState;
            _settings.Raised += OnSettings;
            if (_settings.HasValue)
            {
                OnSettings(_settings.Last);
            }
        }

        private void OnDisable()
        {
            _state.Raised -= OnState;
            _settings.Raised -= OnSettings;
        }

        private void OnState(GameState state)
        {
            bool paused = state == GameState.Paused;
            if (!paused)
            {
                _underlying = state;
            }
            if (paused && !_panel.activeSelf)
            {
                PlaceInFront();
                _returnLabel.text = _underlying == GameState.Gate || _underlying == GameState.Results
                    ? "Return to Title (gate progress not saved)"
                    : "Save and Return to Title";
            }
            _panel.SetActive(paused);
        }

        private void OnSettings(SettingsStatus settings)
        {
            _preset = System.Array.IndexOf(PresetNames, settings.ComfortPreset);
            _uiMode = settings.UiMode == "HUD" ? 1 : 0;
            _presetLabel.text = "Comfort: " + settings.ComfortPreset;
            _uiModeLabel.text = "UI: " + settings.UiMode;
        }

        /// <summary>Wired to the comfort button: Recommended → Comfortable → Advanced.</summary>
        public void CyclePreset()
        {
            _setPreset.Raise((Mathf.Max(0, _preset) + 1) % PresetCount);
        }

        /// <summary>Wired to the UI mode button: Diegetic ↔ HUD (UI-15).</summary>
        public void ToggleUiMode()
        {
            _setUiMode.Raise(1 - _uiMode);
        }

        private void PlaceInFront()
        {
            Vector3 forward = _head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();
            transform.SetPositionAndRotation(_head.position + forward * _distanceMeters, Quaternion.LookRotation(forward));
        }
    }
}
