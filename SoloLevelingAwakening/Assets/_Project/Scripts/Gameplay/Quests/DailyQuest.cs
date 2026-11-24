using SLA.Core;
using SLA.Data;
using SLA.Exercise;
using UnityEngine;

namespace SLA.Quests
{
    /// <summary>
    /// The daily quest in the hub (QST-1, QST-5 to QST-13): calibrate standing height, then alternate rounds of
    /// squats (counted from the headset) and punches (on glowing target pads). Pays stat points and full recovery.
    /// Rest pauses with no penalty (QST-15, QST-17). Not built yet: the time bonus (QST-6), Abandon and the
    /// Penalty Zone (QST-14), the play-space check (QST-16) and the low-impact option (QST-18).
    /// </summary>
    public class DailyQuest : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private DailyQuestConfig _config;
        [SerializeField] private int _hubAreaId;

        [Header("Player")]
        [SerializeField] private Transform _head;
        [Tooltip("The XR Origin: its height is the floor.")]
        [SerializeField] private Transform _floor;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;

        [Header("Target pads (jab, cross, hook, uppercut)")]
        [SerializeField] private Transform _padRoot;
        [SerializeField] private Transform[] _pads = new Transform[4];
        [SerializeField] private Renderer[] _padRenderers = new Renderer[4];
        [SerializeField] private Color _padIdleColor = new Color(0.25f, 0.72f, 1f) * 2f;
        [SerializeField] private Color _padHitColor = new Color(1f, 0.79f, 0.29f) * 3f;

        [Header("Feedback")]
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _repClip;

        [Header("Channels")]
        [SerializeField] private PlayerStatusChannel _playerStatus;
        [SerializeField] private DailyQuestStatusChannel _progress;
        [SerializeField] private VoidEventChannel _startRequested;
        [SerializeField] private VoidEventChannel _restRequested;
        [SerializeField] private IntEventChannel _onCompleted;
        [SerializeField] private SystemMessageChannel _messages;
        [SerializeField] private TravelRequestChannel _arrived;

        private SquatCounter _squats;
        private MaterialPropertyBlock _block;
        private float[] _padCooldowns;
        private Vector3 _lastLeft;
        private Vector3 _lastRight;
        private DailyQuestState _state;
        private float _calibrationSeconds;
        private int _round;
        private int _roundCount;
        private int _roundDone;
        private int _squatsDone;
        private int _squatsTarget;
        private int _punchesDone;
        private int _punchesTarget;

        public DailyQuestState State => _state;

        private void Awake()
        {
            _squats = new SquatCounter(_config.squatDownFraction, _config.squatUpFraction, _config.maxPitchDownDegrees,
                _config.maxDriftMeters, _config.minRepSeconds);
            _block = new MaterialPropertyBlock();
            _padCooldowns = new float[_pads.Length];
            _roundCount = Mathf.Max(1, _config.roundsPerExercise) * 2;
            _padRoot.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _startRequested.Raised += OnStartRequested;
            _restRequested.Raised += OnRestRequested;
            _arrived.Raised += OnArrived;
        }

        private void OnDisable()
        {
            _startRequested.Raised -= OnStartRequested;
            _restRequested.Raised -= OnRestRequested;
            _arrived.Raised -= OnArrived;
        }

        private void Start()
        {
            RefreshTargets();
            _messages.Raise(new SystemMessage(SystemMessageKind.Info,
                "Daily Quest: Strength Training has arrived. Use the quest window to begin."));
            Publish();
        }

        private void OnStartRequested()
        {
            if (_state == DailyQuestState.Available)
            {
                RefreshTargets();
                _state = DailyQuestState.Calibrating;
                _calibrationSeconds = _config.calibrationSeconds;
                _squats.BeginCalibration();
                _messages.Raise(new SystemMessage(SystemMessageKind.Info, "Stand still.")); // QST-8
                Publish();
            }
            else if (_state == DailyQuestState.Resting)
            {
                _state = DailyQuestState.InProgress;
                ShowPads(!DailyQuestRules.IsSquatRound(_round));
                Publish();
            }
        }

        private void OnRestRequested()
        {
            if (_state == DailyQuestState.InProgress || _state == DailyQuestState.Calibrating)
            {
                // QST-17: resting is always free. Calibration restarts on resume.
                _state = _squats.IsCalibrated ? DailyQuestState.Resting : DailyQuestState.Available;
                ShowPads(false);
                Publish();
            }
        }

        private void OnArrived(TravelRequest request)
        {
            if (request.AreaId != _hubAreaId)
            {
                OnRestRequested(); // QST-15: leaving is a pause, not abandoning.
            }
        }

        private void Update()
        {
            Vector3 left = _leftHand.position;
            Vector3 right = _rightHand.position;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 leftVelocity = (left - _lastLeft) / dt;
            Vector3 rightVelocity = (right - _lastRight) / dt;
            _lastLeft = left;
            _lastRight = right;

            switch (_state)
            {
                case DailyQuestState.Calibrating:
                    _squats.AddCalibrationSample(HeadHeight());
                    _calibrationSeconds -= Time.deltaTime;
                    if (_calibrationSeconds <= 0f)
                    {
                        Vector3 head = _head.position;
                        _squats.EndCalibration(new Vector2(head.x, head.z));
                        PlacePads();
                        BeginRound(0);
                    }
                    break;

                case DailyQuestState.InProgress:
                    if (DailyQuestRules.IsSquatRound(_round))
                    {
                        TickSquats();
                    }
                    else
                    {
                        TickPunches(left, leftVelocity, right, rightVelocity);
                    }
                    break;
            }
        }

        /// <summary>Finishes the quest at once. For editor testing.</summary>
        public void CompleteNow()
        {
            if (_state != DailyQuestState.Complete)
            {
                _squatsDone = _squatsTarget;
                _punchesDone = _punchesTarget;
                Complete();
            }
        }

        private void TickSquats()
        {
            Vector3 head = _head.position;
            float pitch = SquatCounter.PitchDownDegrees(_head.forward);
            if (_squats.Tick(Time.time, HeadHeight(), new Vector2(head.x, head.z), pitch))
            {
                _squatsDone++;
                CountRep();
            }
        }

        private void TickPunches(Vector3 left, Vector3 leftVelocity, Vector3 right, Vector3 rightVelocity)
        {
            Vector3 head = _head.position;
            for (int i = 0; i < _pads.Length; i++)
            {
                if (_padCooldowns[i] > 0f)
                {
                    _padCooldowns[i] -= Time.deltaTime;
                    if (_padCooldowns[i] <= 0f)
                    {
                        SetPadColor(i, _padIdleColor);
                    }
                    continue;
                }

                Vector3 pad = _pads[i].position;
                bool hit = (PunchDetector.IsOnPad(left, pad, _config.padRadiusMeters)
                            && PunchDetector.IsValidPunch(leftVelocity, left, head, _config.punchMinSpeed))
                           || (PunchDetector.IsOnPad(right, pad, _config.padRadiusMeters)
                               && PunchDetector.IsValidPunch(rightVelocity, right, head, _config.punchMinSpeed));
                if (!hit)
                {
                    continue;
                }

                _padCooldowns[i] = _config.padCooldownSeconds;
                SetPadColor(i, _padHitColor);
                _punchesDone++;
                CountRep();
                if (_state != DailyQuestState.InProgress)
                {
                    return;
                }
            }
        }

        private void CountRep()
        {
            _roundDone++;
            if (_repClip != null)
            {
                _audio.PlayOneShot(_repClip);
            }

            bool squats = DailyQuestRules.IsSquatRound(_round);
            int target = DailyQuestRules.RepsInRound(_round, squats ? _squatsTarget : _punchesTarget,
                _config.roundsPerExercise);
            if (_roundDone >= target)
            {
                if (_round + 1 >= _roundCount)
                {
                    Complete();
                    return;
                }
                BeginRound(_round + 1);
                return;
            }
            Publish();
        }

        private void BeginRound(int round)
        {
            _round = round;
            _roundDone = 0;
            _state = DailyQuestState.InProgress;
            bool squats = DailyQuestRules.IsSquatRound(round);
            ShowPads(!squats);
            _messages.Raise(new SystemMessage(SystemMessageKind.Info,
                squats ? $"Round {round + 1}: Squats." : $"Round {round + 1}: Punch the glowing targets."));
            Publish();
        }

        private void Complete()
        {
            _state = DailyQuestState.Complete;
            ShowPads(false);
            _onCompleted.Raise(_config.statPointReward);
            _messages.Raise(new SystemMessage(SystemMessageKind.Reward,
                $"Daily Quest complete. Reward: {_config.statPointReward} stat points and full recovery."));
            Publish();
        }

        private void RefreshTargets()
        {
            int level = _playerStatus.HasValue ? _playerStatus.Last.Level : 1;
            DailyQuestRules.RepsForLevel(level, _config, out _squatsTarget, out _punchesTarget);
        }

        private void PlacePads()
        {
            // QST-13 pads around the calibrated head, facing the squat spot's forward (toward the gate, MRH-5).
            Vector3 head = _head.position;
            Transform frame = _padRoot.parent;
            for (int i = 0; i < _pads.Length && i < _config.padOffsets.Length; i++)
            {
                Vector3 offset = _config.padOffsets[i];
                _pads[i].position = head + frame.right * offset.x + Vector3.up * offset.y + frame.forward * offset.z;
                SetPadColor(i, _padIdleColor);
            }
        }

        private void ShowPads(bool visible)
        {
            _padRoot.gameObject.SetActive(visible);
        }

        private void SetPadColor(int index, Color color)
        {
            _block.Clear();
            _block.SetColor(EmissionColorId, color);
            _padRenderers[index].SetPropertyBlock(_block);
        }

        private float HeadHeight() => _head.position.y - _floor.position.y;

        private void Publish()
        {
            _progress.Raise(new DailyQuestStatus(_state, _round, _roundCount, DailyQuestRules.IsSquatRound(_round),
                _squatsDone, _squatsTarget, _punchesDone, _punchesTarget));
        }
    }
}
