using SLA.AI;
using SLA.Core;
using SLA.Data;
using Unity.AI.Navigation;
using UnityEngine;

namespace SLA.Gates
{
    /// <summary>
    /// One per gate (GTE-8): builds its NavMesh, announces entry, sends the player back to the entrance on death
    /// (GTE-4), opens the exit portal when the boss falls (GTE-5) and resets for a replay when the player leaves.
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

        private bool _playerInside;

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
            foreach (var room in _rooms)
            {
                room.Cleared += OnRoomCleared;
            }
        }

        private void OnDisable()
        {
            _arrived.Raised -= OnArrived;
            _onPlayerDied.Raised -= OnPlayerDied;
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
            }
            else if (!_playerInside && wasInside)
            {
                ResetRun();
            }
        }

        private void OnRoomCleared(RoomEncounter room)
        {
            if (!room.IsBossRoom)
            {
                return;
            }
            _exitPortal.SetActive(true);
            _onGateCleared.Raise();
            _messages.Raise(new SystemMessage(SystemMessageKind.Reward,
                "The dungeon's boss has been defeated. The gate will close shortly.")); // GTE-5
        }

        private void OnPlayerDied()
        {
            if (_playerInside)
            {
                _requests.Raise(new TravelRequest(_entranceSpawn, _areaId, true));
            }
        }

        private void ResetRun()
        {
            foreach (var room in _rooms)
            {
                room.ResetRoom();
            }
            _exitPortal.SetActive(false);
        }
    }
}
