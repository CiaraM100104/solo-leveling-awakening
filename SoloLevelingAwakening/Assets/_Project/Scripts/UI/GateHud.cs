using System.Collections.Generic;
using SLA.Core;
using SLA.Core.Events;
using SLA.Data;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SLA.UI
{
    /// <summary>
    /// Placeholder in-gate UI until the System UI pass (UI rules) replaces it: a System message banner
    /// that trails the head (Lazy Follow, never head-locked) for the objective, level-ups, death and the
    /// gate clearing, and a small readout over the left controller with level, HP and XP.
    /// Uses built-in UI Text so it needs no TextMesh Pro resources.
    /// </summary>
    public class GateHud : MonoBehaviour
    {
        // The gate's intro and cleared banners stay up longer than the default System message.
        private const float LongMessageSeconds = 8f;
        // Used only if no ProgressionConfig is assigned: PRG-6's base HP.
        private const int FallbackMaxHp = 100;
        private const float MinDirectionSqrMagnitude = 0.0001f;
        private const string LeftControllerPath = "Camera Offset/Left Controller";

        [Header("Banner")]
        [SerializeField] private GameObject _bannerRoot;
        [SerializeField] private Text _bannerTitle;
        [SerializeField] private Text _bannerBody;
        [SerializeField] private Graphic _bannerBorder;
        [SerializeField] private Color _infoColor = new Color(0.247f, 0.722f, 1f);
        [SerializeField] private Color _rewardColor = new Color(1f, 0.788f, 0.290f);
        [SerializeField] private Color _warningColor = new Color(0.898f, 0.220f, 0.231f);
        [TextArea] [SerializeField] private string _objective = "Defeat every goblin.";

        [Header("Wrist readout")]
        [SerializeField] private Transform _wristRoot;
        [SerializeField] private Text _wristText;
        [Tooltip("Readout position above the left controller.")]
        [FormerlySerializedAs("_wristOffset")]
        [SerializeField] private Vector3 _wristOffsetMeters = new Vector3(0f, 0.09f, 0f);

        [Header("Data")]
        [SerializeField] private ProgressionConfig _progression;
        [SerializeField] private IntEventChannel _onLevelUp;
        [SerializeField] private VoidEventChannel _onGateCleared;
        [SerializeField] private VoidEventChannel _onPlayerDied;
        [SerializeField] private FloatEventChannel _onPlayerHpChanged;
        [SerializeField] private StringEventChannel _onEnemyKilled;

        private readonly Queue<SystemMessage> _queue = new Queue<SystemMessage>();
        private float _hideAt = -1f;
        private Transform _leftHand;
        private Transform _head;
        private float _hp;
        private bool _wristDirty = true;

        private void OnEnable()
        {
            if (_onLevelUp != null)
            {
                _onLevelUp.Raised += OnLevelUp;
            }
            if (_onGateCleared != null)
            {
                _onGateCleared.Raised += OnGateCleared;
            }
            if (_onPlayerDied != null)
            {
                _onPlayerDied.Raised += OnPlayerDied;
            }
            if (_onPlayerHpChanged != null)
            {
                _onPlayerHpChanged.Raised += OnHpChanged;
            }
            if (_onEnemyKilled != null)
            {
                _onEnemyKilled.Raised += OnEnemyKilled;
            }
        }

        private void OnDisable()
        {
            if (_onLevelUp != null)
            {
                _onLevelUp.Raised -= OnLevelUp;
            }
            if (_onGateCleared != null)
            {
                _onGateCleared.Raised -= OnGateCleared;
            }
            if (_onPlayerDied != null)
            {
                _onPlayerDied.Raised -= OnPlayerDied;
            }
            if (_onPlayerHpChanged != null)
            {
                _onPlayerHpChanged.Raised -= OnHpChanged;
            }
            if (_onEnemyKilled != null)
            {
                _onEnemyKilled.Raised -= OnEnemyKilled;
            }
        }

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            _leftHand = player != null ? player.transform.Find(LeftControllerPath) : null;
            var cam = Camera.main;
            _head = cam != null ? cam.transform : null;
            if (_wristRoot != null)
            {
                _wristRoot.gameObject.SetActive(_leftHand != null);
            }
            if (_bannerRoot != null)
            {
                _bannerRoot.SetActive(false);
            }

            Post(new SystemMessage(SystemMessageKind.Info, SystemMessage.Bracket("You have entered the Goblin Warren."), _objective, LongMessageSeconds));
            _wristDirty = true;
        }

        private void Update()
        {
            if (_bannerRoot == null)
            {
                return;
            }

            if (_hideAt > 0f && Time.unscaledTime >= _hideAt)
            {
                _hideAt = -1f;
                _bannerRoot.SetActive(false);
            }

            if (!_bannerRoot.activeSelf && _queue.Count > 0)
            {
                Show(_queue.Dequeue());
            }
        }

        private void LateUpdate()
        {
            // Refreshed here, a frame after the kill or level-up, so XP awarded by other listeners is in.
            if (_wristDirty)
            {
                _wristDirty = false;
                RefreshWrist();
            }

            if (_wristRoot == null || _leftHand == null || _head == null)
            {
                return;
            }

            Vector3 position = _leftHand.position + _wristOffsetMeters;
            Vector3 away = position - _head.position;
            _wristRoot.SetPositionAndRotation(position, away.sqrMagnitude > MinDirectionSqrMagnitude ? Quaternion.LookRotation(away) : Quaternion.identity);
        }

        private void Post(in SystemMessage message) => _queue.Enqueue(message);

        private void Show(in SystemMessage message)
        {
            if (_bannerTitle != null)
            {
                _bannerTitle.text = message.Title;
            }
            if (_bannerBody != null)
            {
                _bannerBody.text = message.Body;
            }
            if (_bannerBorder != null)
            {
                _bannerBorder.color = ColorFor(message.Kind);
            }
            _bannerRoot.SetActive(true);
            _hideAt = message.Seconds > 0f ? Time.unscaledTime + message.Seconds : -1f;
        }

        private Color ColorFor(SystemMessageKind kind)
        {
            switch (kind)
            {
                case SystemMessageKind.Reward: return _rewardColor;
                case SystemMessageKind.Warning: return _warningColor;
                default: return _infoColor;
            }
        }

        private void OnLevelUp(int newLevel)
        {
            Post(SystemMessage.LevelUp(newLevel));
            _wristDirty = true;
        }

        private void OnGateCleared()
        {
            // GTE-5's wording, until the Goblin Chieftain is what clears the gate.
            Post(new SystemMessage(SystemMessageKind.Reward,
                SystemMessage.Bracket("All enemies have been defeated. The gate will close shortly."),
                "The exit portal has opened in the far chamber.", LongMessageSeconds));
        }

        private void OnPlayerDied()
        {
            Post(SystemMessage.Warning("You have died. Returning to the entrance. Your experience is kept."));
        }

        private void OnHpChanged(float hp)
        {
            _hp = hp;
            _wristDirty = true;
        }

        private void OnEnemyKilled(string enemyId) => _wristDirty = true;

        // Runs only when something changed, never every frame, so its string building is fine.
        private void RefreshWrist()
        {
            if (_wristText == null)
            {
                return;
            }

            var player = GameSession.Data.player;
            int maxHp = _progression != null ? _progression.MaxHp(player.stats.vit) : FallbackMaxHp;
            int toNext = _progression != null ? _progression.XpToNext(player.level) : 0;
            _wristText.text = $"Lv {player.level}\nHP {Mathf.CeilToInt(_hp)} / {maxHp}\nXP {player.xp} / {toNext}";
        }
    }
}
