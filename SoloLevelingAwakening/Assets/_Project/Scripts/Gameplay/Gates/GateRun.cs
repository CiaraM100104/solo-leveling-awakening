using System;
using SLA.AI;
using SLA.Core;
using SLA.Data;
using Unity.AI.Navigation;
using UnityEngine;

namespace SLA.Gates
{
    /// <summary>
    /// One per gate (GTE-8): builds its NavMesh, announces entry, sends the player back to the entrance on death
    /// (GTE-4), opens the exit portal when the boss falls (GTE-5), reports the run for the Results screen,
    /// remembers that the gate was cleared, and resets for a replay when the player leaves.
    /// </summary>
    public class GateRun : MonoBehaviour
    {
        [SerializeField] private GateData _gate;
        [SerializeField] private int _areaId = 1;
        [SerializeField] private NavMeshSurface _navMesh;
        [SerializeField] private RoomEncounter[] _rooms = new RoomEncounter[0];
        [SerializeField] private GameObject _exitPortal;
        [SerializeField] private Transform _entranceSpawn;

        [Header("Channels")]
        [SerializeField] private TravelRequestChannel _requests;
        [SerializeField] private TravelRequestChannel _arrived;
        [SerializeField] private VoidEventChannel _onPlayerDied;
        [SerializeField] private VoidEventChannel _onGateCleared;
        [SerializeField] private SystemMessageChannel _messages;

        [Header("Run results")]
        [SerializeField] private VoidEventChannel _onEnemyKilled;
        [SerializeField] private IntEventChannel _onXpGained;
        [SerializeField] private IntEventChannel _onGoldGained;
        [SerializeField] private IntEventChannel _onPlayerDamaged;
        [Tooltip("Raised when the boss falls, with the run's numbers (score is filled in by the Scores board).")]
        [SerializeField] private RunRecordChannel _onRunCompleted;

        [Header("Save")]
        [SerializeField] private VoidEventChannel _onNewGame;
        [SerializeField] private SaveDataChannel _onCollectSave;
        [SerializeField] private SaveDataChannel _onApplySave;

        private bool _playerInside;
        private bool _clearedEver;
        private bool _runOver;
        private float _runStartTime;
        private RunRecord _run;

        public bool PlayerInside => _playerInside;

        private void Awake()
        {
            // Built at load for now; GTE-8 wants each gate scene to ship its own baked NavMesh.
            _navMesh.BuildNavMesh();
            _exitPortal.SetActive(false);
        }

        private void OnEnable()
        {
            _arrived.Raised += OnArrived;
            _onPlayerDied.Raised += OnPlayerDied;
            _onEnemyKilled.Raised += OnEnemyKilled;
            _onXpGained.Raised += OnXp;
            _onGoldGained.Raised += OnGold;
            _onPlayerDamaged.Raised += OnDamaged;
            _onNewGame.Raised += OnNewGame;
            _onCollectSave.Raised += Collect;
            _onApplySave.Raised += Apply;
            foreach (var room in _rooms)
            {
                room.Cleared += OnRoomCleared;
            }
        }

        private void OnDisable()
        {
            _arrived.Raised -= OnArrived;
            _onPlayerDied.Raised -= OnPlayerDied;
            _onEnemyKilled.Raised -= OnEnemyKilled;
            _onXpGained.Raised -= OnXp;
            _onGoldGained.Raised -= OnGold;
            _onPlayerDamaged.Raised -= OnDamaged;
            _onNewGame.Raised -= OnNewGame;
            _onCollectSave.Raised -= Collect;
            _onApplySave.Raised -= Apply;
            foreach (var room in _rooms)
            {
                room.Cleared -= OnRoomCleared;
            }
        }

        private void OnArrived(TravelRequest request)
        {
            bool wasInside = _playerInside;
            _playerInside = request.AreaId == _areaId;

            if (_playerInside && !wasInside)
            {
                string kind = _gate.isRed ? "Red" : "Blue";
                _messages.Raise(new SystemMessage(_gate.isRed ? SystemMessageKind.Warning : SystemMessageKind.Info,
                    $"You have entered the {_gate.displayName}. Rank {_gate.rank}, {kind} Gate."));
                StartRun();
            }
            else if (!_playerInside && wasInside)
            {
                ResetRun();
            }
        }

        private void StartRun()
        {
            _runOver = false;
            _runStartTime = Time.time;
            _run = new RunRecord { gateId = _gate.id, gateName = _gate.displayName };
        }

        private bool Counting => _playerInside && !_runOver && _run != null;

        private void OnEnemyKilled()
        {
            if (Counting)
            {
                _run.kills++;
            }
        }

        private void OnXp(int xp)
        {
            if (Counting)
            {
                _run.xp += xp;
            }
        }

        private void OnGold(int gold)
        {
            if (Counting)
            {
                _run.gold += gold;
            }
        }

        private void OnDamaged(int damage)
        {
            if (Counting)
            {
                _run.damageTaken += damage;
            }
        }

        private void OnRoomCleared(RoomEncounter room)
        {
            if (!room.IsBossRoom)
            {
                return;
            }
            _exitPortal.SetActive(true);
            _clearedEver = true;
            _onGateCleared.Raise();
            _messages.Raise(new SystemMessage(SystemMessageKind.Reward,
                "The dungeon's boss has been defeated. The gate will close shortly.")); // GTE-5

            if (_run != null && !_runOver)
            {
                _runOver = true;
                _run.timeSeconds = Time.time - _runStartTime;
                _run.dateUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                _onRunCompleted.Raise(_run);
            }
        }

        private void OnPlayerDied()
        {
            if (!_playerInside)
            {
                return;
            }
            if (Counting)
            {
                _run.deaths++;
            }
            _requests.Raise(new TravelRequest(_entranceSpawn, _areaId, true));
        }

        private void ResetRun()
        {
            foreach (var room in _rooms)
            {
                room.ResetRoom();
            }
            _exitPortal.SetActive(false);
            _run = null;
        }

        private void OnNewGame()
        {
            _clearedEver = false;
        }

        private void Collect(SaveData save)
        {
            if (_clearedEver && !save.quests.completed.Contains(_gate.id))
            {
                save.quests.completed.Add(_gate.id);
            }
        }

        private void Apply(SaveData save)
        {
            _clearedEver = save.quests.completed.Contains(_gate.id);
        }
    }
}
