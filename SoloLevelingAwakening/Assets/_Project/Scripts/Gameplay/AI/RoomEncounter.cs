using System;
using SLA.Core;
using SLA.Data;
using UnityEngine;

namespace SLA.AI
{
    /// <summary>
    /// The fight in one gate room (GTE-1). Starts when the player's head enters the room, runs its waves,
    /// opens the exit barrier when cleared. A boss room calls reinforcements at phase 2 (ENM-7)
    /// and is cleared when the boss dies. Resets when the player dies mid-fight (GTE-4).
    /// </summary>
    public class RoomEncounter : MonoBehaviour
    {
        private enum Phase
        {
            Idle,
            Waiting,
            Fighting,
            Cleared,
        }

        private const float SpawnRetrySeconds = 1f;

        [SerializeField] private EncounterData _data;
        [SerializeField] private EnemyDirector _director;
        [SerializeField] private Transform _playerHead;
        [Tooltip("The fight starts once the head is inside this box (local to this transform).")]
        [SerializeField] private Vector3 _triggerCenter;
        [SerializeField] private Vector3 _triggerSize = new Vector3(7f, 4f, 6f);
        [Tooltip("ENM-5: put these ahead of where the player walks in, never behind.")]
        [SerializeField] private Transform[] _spawnPoints = new Transform[0];
        [Tooltip("Blocks the way on until the room is cleared. Optional.")]
        [SerializeField] private GameObject _exitBarrier;

        [Header("Channels")]
        [SerializeField] private SystemMessageChannel _messages;
        [SerializeField] private VoidEventChannel _onPlayerDied;

        private Bounds _trigger;
        private Phase _phase;
        private int _waveIndex;
        private int _alive;
        private float _timerSeconds;
        private int _spawnCursor;
        private EnemyBrain _boss;
        private bool _phase2;

        /// <summary>Raised once when the room is cleared.</summary>
        public event Action<RoomEncounter> Cleared;

        public bool IsBossRoom => _data.isBossRoom;
        public bool IsCleared => _phase == Phase.Cleared;
        public bool IsFighting => _phase == Phase.Waiting || _phase == Phase.Fighting;

        private void Awake()
        {
            _trigger = new Bounds(transform.TransformPoint(_triggerCenter), _triggerSize);
            ResetRoom();
        }

        private void OnEnable()
        {
            _onPlayerDied.Raised += OnPlayerDied;
        }

        private void OnDisable()
        {
            _onPlayerDied.Raised -= OnPlayerDied;
            UnhookBoss();
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Idle:
                    if (_trigger.Contains(_playerHead.position))
                    {
                        _phase = Phase.Waiting;
                        _timerSeconds = _data.startDelaySeconds;
                    }
                    break;

                case Phase.Waiting:
                    _timerSeconds -= Time.deltaTime;
                    if (_timerSeconds <= 0f)
                    {
                        _phase = Phase.Fighting;
                        SpawnWave(_data.waves[_waveIndex]);
                    }
                    break;
            }
        }

        public void HandleEnemyDied(EnemyBrain enemy)
        {
            _alive--;

            if (enemy == _boss)
            {
                UnhookBoss();
                _director.DespawnOwnedBy(this, true); // The boss's followers vanish with it.
                _alive = 0;
                SetCleared();
                return;
            }
            if (_alive > 0 || _data.isBossRoom)
            {
                return;
            }

            _waveIndex++;
            if (_waveIndex >= _data.waves.Length)
            {
                SetCleared();
            }
            else
            {
                _phase = Phase.Waiting;
                _timerSeconds = _data.secondsBetweenWaves;
            }
        }

        /// <summary>Back to untouched: enemies gone, barrier closed.</summary>
        public void ResetRoom()
        {
            if (_director != null)
            {
                _director.DespawnOwnedBy(this, false);
            }
            UnhookBoss();
            _phase = Phase.Idle;
            _waveIndex = 0;
            _alive = 0;
            _spawnCursor = 0;
            _phase2 = false;
            if (_exitBarrier != null)
            {
                _exitBarrier.SetActive(true);
            }
        }

        private void SpawnWave(Wave wave)
        {
            Vector3 head = _playerHead.position;
            foreach (var group in wave.groups)
            {
                for (int i = 0; i < group.count; i++)
                {
                    var point = _spawnPoints[_spawnCursor % _spawnPoints.Length];
                    _spawnCursor++;
                    Vector3 position = point.position;
                    // Each enemy circles the player on the side it spawned.
                    float angle = Mathf.Atan2(position.z - head.z, position.x - head.x);
                    var enemy = _director.Spawn(group.enemyPrefab, position, angle, this);
                    if (enemy == null)
                    {
                        continue;
                    }
                    _alive++;
                    if (enemy.Data.isBoss && _boss == null)
                    {
                        _boss = enemy;
                        _boss.Health.Changed += OnBossHealthChanged;
                    }
                }
            }

            if (_alive == 0)
            {
                // The gate-wide cap (ENM-4) or an empty pool: try again shortly rather than skipping the wave.
                _phase = Phase.Waiting;
                _timerSeconds = SpawnRetrySeconds;
            }
        }

        private void OnBossHealthChanged()
        {
            if (_phase2 || _boss.Health.IsDead || _boss.Health.Current > _boss.Health.Max * _boss.Data.phase2HpFraction)
            {
                return;
            }
            _phase2 = true;
            if (!string.IsNullOrEmpty(_data.phase2Message))
            {
                _messages.Raise(new SystemMessage(SystemMessageKind.Warning, _data.phase2Message));
            }
            SpawnWave(_data.phase2Reinforcements);
        }

        private void UnhookBoss()
        {
            if (_boss != null)
            {
                _boss.Health.Changed -= OnBossHealthChanged;
                _boss = null;
            }
        }

        private void SetCleared()
        {
            _phase = Phase.Cleared;
            if (_exitBarrier != null)
            {
                _exitBarrier.SetActive(false);
            }
            if (!string.IsNullOrEmpty(_data.clearedMessage))
            {
                _messages.Raise(new SystemMessage(SystemMessageKind.Info, _data.clearedMessage));
            }
            Cleared?.Invoke(this);
        }

        private void OnPlayerDied()
        {
            if (IsFighting)
            {
                ResetRoom();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.TransformPoint(_triggerCenter), _triggerSize);
        }
    }
}
