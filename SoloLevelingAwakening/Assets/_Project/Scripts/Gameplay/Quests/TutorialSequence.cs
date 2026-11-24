using SLA.AI;
using SLA.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SLA.Quests
{
    /// <summary>
    /// The Training Grounds, a stand-in for the Prologue's tutorial path (gates.md, gate 1): pick up the daggers,
    /// strike a sparring goblin, block its club, parry it, then step through the gate home. Teaches the GDD's
    /// core fight loop ("read the tell, dodge or parry, strike") before the first real gate.
    /// </summary>
    public class TutorialSequence : MonoBehaviour
    {
        public enum Step
        {
            Grab,
            Strike,
            Block,
            Parry,
            Done,
        }

        private const string CompletedId = "tutorial";
        private const int StrikesNeeded = 3;

        [SerializeField] private int _areaId = 3;
        [SerializeField] private NavMeshSurface _navMesh;
        [SerializeField] private EnemyDirector _director;
        [SerializeField] private GameObject _sparringPrefab;
        [SerializeField] private Transform _sparringSpawn;
        [SerializeField] private Transform _head;
        [SerializeField] private XRGrabInteractable[] _daggers = new XRGrabInteractable[0];
        [SerializeField] private GameObject _exitPortal;

        [Header("Channels")]
        [SerializeField] private TravelRequestChannel _arrived;
        [SerializeField] private HitEventChannel _hits;
        [Tooltip("The instruction shown on the training panel.")]
        [SerializeField] private StringEventChannel _instruction;
        [SerializeField] private SystemMessageChannel _messages;
        [SerializeField] private VoidEventChannel _saveRequested;
        [SerializeField] private VoidEventChannel _onNewGame;
        [SerializeField] private SaveDataChannel _onCollectSave;
        [SerializeField] private SaveDataChannel _onApplySave;

        private bool _active;
        private bool _completedEver;
        private int _strikes;
        private EnemyBrain _sparring;

        public Step Current { get; private set; } = Step.Grab;
        public bool CompletedEver => _completedEver;

        private void Awake()
        {
            _navMesh.BuildNavMesh();
            _exitPortal.SetActive(false);
        }

        private void OnEnable()
        {
            _arrived.Raised += OnArrived;
            _hits.Raised += OnHit;
            _onNewGame.Raised += OnNewGame;
            _onCollectSave.Raised += Collect;
            _onApplySave.Raised += Apply;
        }

        private void OnDisable()
        {
            _arrived.Raised -= OnArrived;
            _hits.Raised -= OnHit;
            _onNewGame.Raised -= OnNewGame;
            _onCollectSave.Raised -= Collect;
            _onApplySave.Raised -= Apply;
        }

        private void OnArrived(TravelRequest request)
        {
            bool inside = request.AreaId == _areaId;
            if (inside && !_active)
            {
                _active = true;
                Enter(Step.Grab);
            }
            else if (!inside && _active)
            {
                _active = false;
                RemoveSparring();
                _exitPortal.SetActive(false);
            }
        }

        private void Update()
        {
            if (_active && Current == Step.Grab && HeldDaggers() >= _daggers.Length)
            {
                Enter(Step.Strike);
            }
        }

        private void OnHit(HitEvent hit)
        {
            if (!_active)
            {
                return;
            }
            switch (Current)
            {
                case Step.Strike when hit.Kind == HitKind.Damage:
                    _strikes++;
                    if (_strikes >= StrikesNeeded)
                    {
                        Enter(Step.Block);
                    }
                    break;
                case Step.Block when hit.Kind == HitKind.Block:
                    Enter(Step.Parry);
                    break;
                case Step.Parry when hit.Kind == HitKind.Parry:
                    Enter(Step.Done);
                    break;
            }
        }

        /// <summary>Moves on one step. For editor testing.</summary>
        public void SkipStep()
        {
            if (_active && Current != Step.Done)
            {
                Enter(Current + 1);
            }
        }

        private void Enter(Step step)
        {
            Current = step;
            string text;
            switch (step)
            {
                case Step.Grab:
                    _strikes = 0;
                    RemoveSparring();
                    _exitPortal.SetActive(false);
                    text = "Pick up both daggers from the rack with the grip buttons.";
                    break;
                case Step.Strike:
                    SpawnSparring();
                    text = "Strike the sparring goblin. Swing hard: a slow tap does nothing.";
                    break;
                case Step.Block:
                    text = "When its club glows red, hold a dagger between the club and your face to block.";
                    break;
                case Step.Parry:
                    text = "Now strike the club as it swings down to parry. A parry staggers the enemy.";
                    break;
                default:
                    RemoveSparring();
                    _exitPortal.SetActive(true);
                    _completedEver = true;
                    _saveRequested.Raise();
                    text = "Training complete. Step through the gate to go home.";
                    break;
            }
            _instruction.Raise(text);
            _messages.Raise(new SystemMessage(step == Step.Done ? SystemMessageKind.Reward : SystemMessageKind.Info, text));
        }

        private int HeldDaggers()
        {
            int held = 0;
            foreach (var dagger in _daggers)
            {
                if (dagger.isSelected)
                {
                    held++;
                }
            }
            return held;
        }

        private void SpawnSparring()
        {
            if (_sparring != null)
            {
                return;
            }
            Vector3 position = _sparringSpawn.position;
            float angle = Mathf.Atan2(position.z - _head.position.z, position.x - _head.position.x);
            _sparring = _director.Spawn(_sparringPrefab, position, angle, null);
        }

        private void RemoveSparring()
        {
            if (_sparring != null)
            {
                _director.Despawn(_sparring);
                _sparring = null;
            }
        }

        private void OnNewGame()
        {
            _completedEver = false;
        }

        private void Collect(SaveData save)
        {
            if (_completedEver && !save.quests.completed.Contains(CompletedId))
            {
                save.quests.completed.Add(CompletedId);
            }
        }

        private void Apply(SaveData save)
        {
            _completedEver = save.quests.completed.Contains(CompletedId);
        }
    }
}
