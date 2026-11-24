using System;
using SLA.Core;
using SLA.Data;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace SLA.MR
{
    /// <summary>The three comfort presets (docs/design/comfort-and-accessibility.md, presets table).</summary>
    public enum ComfortPreset
    {
        Recommended,
        Comfortable,
        Advanced,
    }

    /// <summary>UI-15: diegetic windows and wrist panel, or a HUD for the user study.</summary>
    public enum UiMode
    {
        Diegetic,
        Hud,
    }

    /// <summary>
    /// Applies the comfort preset to the player rig and switches between the wrist panel and the HUD.
    /// Recommended: smooth 1.5 m/s, snap 45°, medium vignette. Comfortable: teleport, snap 45°, strong vignette.
    /// Advanced: smooth movement and turning, no vignette. Settings are saved with the profile.
    /// Not built yet: left-handed mode (CMF-8) and the Dash fade options.
    /// </summary>
    public class ComfortSettings : MonoBehaviour
    {
        [SerializeField] private ComfortConfig _config;

        [Header("Rig")]
        [Tooltip("The starter rig's per-hand managers: they switch stick movement between smooth and teleport.")]
        [SerializeField] private ControllerInputActionManager[] _controllers = new ControllerInputActionManager[0];
        [SerializeField] private ContinuousMoveProvider _move;
        [SerializeField] private TunnelingVignetteController _vignette;

        [Header("UI mode")]
        [SerializeField] private GameObject _wristPanel;
        [SerializeField] private GameObject _hud;

        [Header("Channels")]
        [Tooltip("A ComfortPreset as an int.")]
        [SerializeField] private IntEventChannel _setPreset;
        [Tooltip("A UiMode as an int.")]
        [SerializeField] private IntEventChannel _setUiMode;
        [SerializeField] private SettingsChannel _settings;
        [SerializeField] private SaveDataChannel _onCollectSave;
        [SerializeField] private SaveDataChannel _onApplySave;

        public ComfortPreset Preset { get; private set; } = ComfortPreset.Recommended;
        public UiMode Mode { get; private set; } = UiMode.Diegetic;

        private void OnEnable()
        {
            _setPreset.Raised += OnSetPreset;
            _setUiMode.Raised += OnSetUiMode;
            _onCollectSave.Raised += Collect;
            _onApplySave.Raised += Apply;
        }

        private void OnDisable()
        {
            _setPreset.Raised -= OnSetPreset;
            _setUiMode.Raised -= OnSetUiMode;
            _onCollectSave.Raised -= Collect;
            _onApplySave.Raised -= Apply;
        }

        private void Start()
        {
            ApplyAll();
        }

        private void OnSetPreset(int preset)
        {
            Preset = (ComfortPreset)Mathf.Clamp(preset, 0, 2);
            ApplyAll();
        }

        private void OnSetUiMode(int mode)
        {
            Mode = (UiMode)Mathf.Clamp(mode, 0, 1);
            ApplyAll();
        }

        private void ApplyAll()
        {
            bool smoothMove = Preset != ComfortPreset.Comfortable;
            bool smoothTurn = Preset == ComfortPreset.Advanced;
            foreach (var controller in _controllers)
            {
                if (controller != null)
                {
                    controller.smoothMotionEnabled = smoothMove;
                    controller.smoothTurnEnabled = smoothTurn;
                }
            }
            if (_move != null)
            {
                _move.moveSpeed = _config.walkSpeedMetersPerSecond; // CMF-2: one walk speed, no stat changes it.
            }
            if (_vignette != null)
            {
                _vignette.enabled = Preset != ComfortPreset.Advanced;
                var parameters = _vignette.defaultParameters;
                parameters.apertureSize = Preset == ComfortPreset.Comfortable
                    ? _config.strongVignetteAperture
                    : _config.mediumVignetteAperture;
                _vignette.defaultParameters = parameters;
            }

            if (_wristPanel != null)
            {
                _wristPanel.SetActive(Mode == UiMode.Diegetic);
            }
            if (_hud != null)
            {
                _hud.SetActive(Mode == UiMode.Hud);
            }
            _settings.Raise(new SettingsStatus(Preset.ToString(), Mode == UiMode.Hud ? "HUD" : "Diegetic"));
        }

        private void Collect(SaveData save)
        {
            save.settings.comfortPreset = Preset.ToString();
            save.settings.uiMode = Mode.ToString();
        }

        private void Apply(SaveData save)
        {
            Preset = Enum.TryParse(save.settings.comfortPreset, out ComfortPreset preset) ? preset : ComfortPreset.Recommended;
            Mode = Enum.TryParse(save.settings.uiMode, true, out UiMode mode) ? mode : UiMode.Diegetic;
            ApplyAll();
        }
    }
}
