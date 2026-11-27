using System;
using SLA.Combat;
using SLA.Core.Events;
using SLA.Data;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace SLA.AI
{
    public enum EnemyState
    {
        Idle,
        Approach,
        Circle,
        Windup,
        Attack,
        Recover,
        Stagger,
        Dead,
    }

    /// <summary>
    /// The shared enemy state machine from the GDD's enemy AI diagram (ENM-1). A minimal C# version so
    /// enemies work on day one; richer behavior moves into Unity Behavior graphs later.
    /// Attacks, the tell and hit feedback listen to <see cref="StateChanged"/>.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public class EnemyBrain : MonoBehaviour
    {
        // Stop this far into attack range (fraction of it), so the enemy doesn't walk into the player.
        private const float StoppingDistanceFraction = 0.75f;
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [SerializeField] private EnemyData _data;
        [SerializeField] private float _sightRange = 12f;
        [Tooltip("Degrees per second the enemy turns to face the player while it circles.")]
        [FormerlySerializedAs("_turnSpeedDegrees")]
        [SerializeField] private float _turnSpeedDegreesPerSecond = 360f;
        [Tooltip("How long the Attack state lasts (the active frames).")]
        [SerializeField] private float _attackSeconds = 0.2f;
        [Tooltip("Raised with EnemyData.id on death (ENM-6).")]
        [SerializeField] private StringEventChannel _onEnemyKilled;
        [Tooltip("When the player dies, enemies go back to where they started (GTE-4 respawn).")]
        [SerializeField] private VoidEventChannel _onPlayerDied;

        private NavMeshAgent _agent;
        private Health _health;
        private Transform _player;
        private float _stateTimer;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        public event Action<EnemyState> StateChanged;

        public EnemyState State { get; private set; } = EnemyState.Idle;
        public EnemyData Data => _data;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            if (_data != null)
            {
                _health.Initialize(_data.maxHp, _data.armor);
                _agent.speed = _data.moveSpeed;
                _agent.stoppingDistance = _data.attackRange * StoppingDistanceFraction;
            }
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
            if (_onPlayerDied != null)
            {
                _onPlayerDied.Raised += ReturnHome;
            }
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
            if (_onPlayerDied != null)
            {
                _onPlayerDied.Raised -= ReturnHome;
            }
            ReleaseToken();
        }

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            _player = player != null ? player.transform : null;
        }

        private void Update()
        {
            if (State == EnemyState.Dead || _player == null || _data == null || !_agent.isOnNavMesh)
            {
                return;
            }

            if (_health.IsStaggered && State != EnemyState.Stagger)
            {
                Enter(EnemyState.Stagger);
            }

            _stateTimer += Time.deltaTime;
            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            switch (State)
            {
                case EnemyState.Idle:
                    if (distance <= _sightRange) Enter(EnemyState.Approach);
                    break;

                case EnemyState.Approach:
                    _agent.SetDestination(_player.position);
                    if (distance <= _data.attackRange * 1.5f) Enter(EnemyState.Circle);
                    break;

                case EnemyState.Circle:
                    // TODO: strafe around the player at attack range.
                    _agent.SetDestination(_player.position);
                    Face(toPlayer);
                    if (distance <= _data.attackRange && TryGetToken()) Enter(EnemyState.Windup);
                    else if (distance > _data.attackRange * 2f) Enter(EnemyState.Approach);
                    break;

                case EnemyState.Windup:
                    // The tell (ENM-3): EnemyFeedback glows the weapon and plays the cue while in this state.
                    // Facing is locked here, so the player can step out of the attack (CMB-15).
                    _agent.isStopped = true;
                    if (_stateTimer >= _data.windupSeconds) Enter(EnemyState.Attack);
                    break;

                case EnemyState.Attack:
                    // EnemyMeleeAttack strikes on entering this state.
                    if (_stateTimer >= _attackSeconds) Enter(EnemyState.Recover);
                    break;

                case EnemyState.Recover:
                    if (_stateTimer >= _data.recoverSeconds)
                    {
                        ReleaseToken();
                        _agent.isStopped = false;
                        Enter(EnemyState.Circle);
                    }
                    break;

                case EnemyState.Stagger:
                    _agent.isStopped = true;
                    if (!_health.IsStaggered)
                    {
                        _agent.isStopped = false;
                        Enter(EnemyState.Recover);
                    }
                    break;
            }
        }

        private void Face(Vector3 flatDirection)
        {
            if (flatDirection.sqrMagnitude < MinDirectionSqrMagnitude)
            {
                return;
            }
            var target = Quaternion.LookRotation(flatDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeedDegreesPerSecond * Time.deltaTime);
        }

        private void Enter(EnemyState next)
        {
            State = next;
            _stateTimer = 0f;
            StateChanged?.Invoke(next);
        }

        private bool TryGetToken()
        {
            var director = EnemyDirector.Current;
            return director == null || director.Tokens.TryAcquire(GetInstanceID());
        }

        private void ReleaseToken()
        {
            EnemyDirector.Current?.Tokens.Release(GetInstanceID());
        }

        private void ReturnHome()
        {
            if (State == EnemyState.Dead || !_agent.isOnNavMesh)
            {
                return;
            }

            ReleaseToken();
            _agent.Warp(_homePosition);
            transform.rotation = _homeRotation;
            _agent.isStopped = false;
            _agent.ResetPath();
            _health.Heal(_health.Max);
            Enter(EnemyState.Idle);
        }

        private void OnDied()
        {
            ReleaseToken();
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
            }
            Enter(EnemyState.Dead);
            if (_onEnemyKilled != null && _data != null)
            {
                _onEnemyKilled.Raise(_data.id);
            }
            // TODO: switch to ragdoll and drop loot (ENM-6). EnemyFeedback plays a placeholder fall for now.
        }
    }
}
