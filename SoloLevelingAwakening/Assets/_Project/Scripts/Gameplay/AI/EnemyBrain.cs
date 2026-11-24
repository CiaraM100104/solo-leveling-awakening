using SLA.Combat;
using SLA.Core;
using SLA.Data;
using UnityEngine;
using UnityEngine.AI;

namespace SLA.AI
{
    /// <summary>
    /// ENM-1 state machine for a melee enemy: Idle → Approach → Circle → Windup (with token) → Attack → Recover → Circle,
    /// Stagger after a parry, Dead at 0 HP. Moves on the gate's NavMesh. The weapon visibly winds up and swings;
    /// parries land early in the swing (CMB-14), held weapons in the swing's path block (CMB-13).
    /// </summary>
    [RequireComponent(typeof(Health), typeof(Rigidbody), typeof(NavMeshAgent))]
    public class EnemyBrain : MonoBehaviour, IParryable
    {
        public enum State
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

        private const float RepathSeconds = 0.25f;
        private const float DeathPushImpulse = 1.5f;
        private const float RestSwingDegrees = 30f;
        private const float RaisedSwingDegrees = -70f;
        private const float StruckSwingDegrees = 75f;
        private const float ApproachToCircleMargin = 1f;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private EnemyData _data;
        [SerializeField] private CombatConfig _combat;
        [SerializeField] private ProgressionConfig _progression;
        [SerializeField] private PlayerStatusChannel _playerStatus;
        [SerializeField] private HitEventChannel _hits;

        [Header("Body")]
        [SerializeField] private Renderer[] _bodyRenderers = new Renderer[0];
        [SerializeField] private Renderer _weaponRenderer;
        [Tooltip("Rotates around its X axis to swing the weapon.")]
        [SerializeField] private Transform _weaponPivot;
        [SerializeField] private Transform _weaponTip;
        [Tooltip("Layers that can block an attack (PlayerWeapon).")]
        [SerializeField] private LayerMask _blockMask;

        [Header("Audio")]
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _tellClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private AudioClip _parryClip;
        [SerializeField] private AudioClip _blockClip;

        private Health _health;
        private Rigidbody _body;
        private NavMeshAgent _agent;
        private MaterialPropertyBlock _block;
        private EnemyDirector _director;
        private Transform _playerHead;
        private Health _playerHealth;
        private float _angleRadians;
        private float _stateSeconds;
        private float _hitstopSeconds;
        private float _flashSeconds;
        private float _tokenRetrySeconds;
        private float _repathSeconds;
        private int _id;

        public State CurrentState { get; private set; }
        public EnemyData Data => _data;
        public Health Health => _health;
        public int Id => _id;
        /// <summary>The room that spawned this enemy.</summary>
        public RoomEncounter Owner { get; private set; }
        public bool IsDead => CurrentState == State.Dead;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _body = GetComponent<Rigidbody>();
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = false;
            _block = new MaterialPropertyBlock();
            _id = GetHashCode(); // Unique per UnityEngine.Object.
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        /// <summary>Called by the director when taking this enemy from the pool.</summary>
        public void Spawn(EnemyDirector director, RoomEncounter owner, Transform playerHead, Health playerHealth,
            Vector3 position, float angleRadians)
        {
            _director = director;
            Owner = owner;
            _playerHead = playerHead;
            _playerHealth = playerHealth;
            _angleRadians = angleRadians;

            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
            _body.isKinematic = true;

            Vector3 toPlayer = Flat(playerHead.position - position);
            var rotation = toPlayer.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);

            _agent.enabled = true;
            _agent.Warp(position);
            _agent.speed = _data.moveSpeedMetersPerSecond;

            _health.SetMax(_data.maxHp, true);
            _health.Armor = _data.armor;
            _health.IsStaggered = false;
            _hitstopSeconds = 0f;
            _flashSeconds = 0f;
            _tokenRetrySeconds = 0f;
            _repathSeconds = 0f;
            SetEmission(_bodyRenderers, Color.black);
            SetWeaponGlow(false);
            SetSwing(RestSwingDegrees);
            Enter(State.Idle);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _stateSeconds += dt;
            _tokenRetrySeconds -= dt;
            _repathSeconds -= dt;

            if (CurrentState == State.Dead)
            {
                if (_stateSeconds >= _data.corpseSeconds)
                {
                    _director.Despawn(this);
                }
                return;
            }

            // CMB-20: hitstop freezes the enemy only, never the camera (CMB-21).
            if (_hitstopSeconds > 0f)
            {
                _hitstopSeconds -= dt;
                _stateSeconds -= dt;
                if (_hitstopSeconds <= 0f)
                {
                    SetAgentStopped(IsMovingState(CurrentState) == false);
                }
                return;
            }

            Vector3 toPlayer = Flat(_playerHead.position - transform.position);
            float distance = toPlayer.magnitude;

            switch (CurrentState)
            {
                case State.Idle:
                    if (distance <= _data.sightRangeMeters)
                    {
                        Enter(State.Approach);
                    }
                    break;

                case State.Approach:
                case State.Circle:
                    TickMove(toPlayer, distance);
                    break;

                case State.Windup:
                    Face(toPlayer);
                    if (_stateSeconds >= _data.windupSeconds)
                    {
                        Enter(State.Attack);
                    }
                    break;

                case State.Attack:
                    Face(toPlayer);
                    if (_stateSeconds >= _data.attackSeconds)
                    {
                        ResolveAttack();
                        Enter(State.Recover);
                    }
                    break;

                case State.Recover:
                    if (_stateSeconds >= _data.recoverSeconds)
                    {
                        ReleaseToken();
                        Enter(State.Circle);
                    }
                    break;

                case State.Stagger:
                    if (_stateSeconds >= _combat.staggerSeconds)
                    {
                        _health.IsStaggered = false;
                        SetEmission(_bodyRenderers, Color.black);
                        Enter(State.Circle);
                    }
                    break;
            }
        }

        private void Update()
        {
            if (_flashSeconds > 0f)
            {
                _flashSeconds -= Time.deltaTime;
                if (_flashSeconds <= 0f)
                {
                    SetEmission(_bodyRenderers, CurrentState == State.Stagger ? _combat.staggerColor : Color.black);
                }
            }

            // The swing is the visible tell (CMB-17): raise during windup, strike during attack, lower during recover.
            switch (CurrentState)
            {
                case State.Windup:
                    SetSwing(Mathf.Lerp(RestSwingDegrees, RaisedSwingDegrees, _stateSeconds / _data.windupSeconds));
                    break;
                case State.Attack:
                    SetSwing(Mathf.Lerp(RaisedSwingDegrees, StruckSwingDegrees, _stateSeconds / _data.attackSeconds));
                    break;
                case State.Recover:
                    SetSwing(Mathf.Lerp(StruckSwingDegrees, RestSwingDegrees, _stateSeconds / _data.recoverSeconds));
                    break;
            }
        }

        private void TickMove(Vector3 toPlayer, float distance)
        {
            // Waiting out the retry delay lets other enemies take the token instead of the last attacker.
            bool hasToken = _director.Tokens.Holds(_id)
                            || (_tokenRetrySeconds <= 0f && _director.Tokens.TryAcquire(_id));
            Vector3 head = _playerHead.position;
            Vector3 target;
            if (hasToken)
            {
                if (distance <= _data.attackRangeMeters)
                {
                    Face(toPlayer);
                    Enter(State.Windup);
                    return;
                }
                target = new Vector3(head.x, transform.position.y, head.z);
                _agent.stoppingDistance = _data.attackRangeMeters * 0.8f;
            }
            else
            {
                var offset = new Vector3(Mathf.Cos(_angleRadians), 0f, Mathf.Sin(_angleRadians)) * _data.circleRadiusMeters;
                target = new Vector3(head.x, transform.position.y, head.z) + offset;
                _agent.stoppingDistance = 0f;
            }

            if (_repathSeconds <= 0f && _agent.isOnNavMesh)
            {
                _repathSeconds = RepathSeconds;
                _agent.SetDestination(target);
            }
            Face(toPlayer);

            if (CurrentState == State.Approach && distance <= _data.circleRadiusMeters + ApproachToCircleMargin)
            {
                Enter(State.Circle);
            }
        }

        private void Enter(State state)
        {
            CurrentState = state;
            _stateSeconds = 0f;
            SetAgentStopped(!IsMovingState(state));

            switch (state)
            {
                case State.Windup:
                    // ENM-3 / CMB-17: the tell is a red glow, a raised weapon and a sound, never colour alone (CMF-11).
                    SetWeaponGlow(true);
                    Play(_tellClip, 1f);
                    break;

                case State.Attack:
                case State.Stagger:
                case State.Dead:
                    SetWeaponGlow(false);
                    break;
            }
        }

        private void ResolveAttack()
        {
            // CMB-15: the player's hurtbox follows the headset, so stepping or leaning away dodges.
            float distance = Flat(_playerHead.position - transform.position).magnitude;
            if (distance > _data.attackRangeMeters || _playerHealth.IsDead)
            {
                return;
            }

            float damage = _data.damagePerHit;
            if (IsBlocked(out Vector3 blockPoint))
            {
                // CMB-13: a held weapon in the path takes most of the hit. Fatigue cost waits for the Fatigue system.
                damage *= 1f - _combat.blockDamageReduction;
                _hits.Raise(new HitEvent(blockPoint, 0, HitKind.Block));
                Play(_blockClip, 1f);
            }
            _playerHealth.TakeDamage(damage, _playerHead.position);
        }

        private bool IsBlocked(out Vector3 point)
        {
            Vector3 from = _weaponPivot.position;
            Vector3 toHead = _playerHead.position - from;
            if (Physics.SphereCast(from, _combat.blockProbeRadiusMeters, toHead.normalized, out RaycastHit hit,
                    toHead.magnitude, _blockMask, QueryTriggerInteraction.Ignore))
            {
                var weapon = hit.collider.GetComponentInParent<WeaponHitDetector>();
                if (weapon != null && weapon.IsHeld)
                {
                    point = hit.point;
                    return true;
                }
            }
            point = default;
            return false;
        }

        /// <summary>CMB-14: a fast hit on the weapon early in its swing negates the attack and staggers.</summary>
        public bool TryParry(float relativeSpeed, Vector3 contactPoint)
        {
            if (CurrentState != State.Attack || _stateSeconds > ParryWindowSeconds() || relativeSpeed < _combat.parryMinSpeed)
            {
                return false;
            }

            ReleaseToken();
            Enter(State.Stagger);
            _health.IsStaggered = true; // CMB-8: the next hits ignore armor.
            SetEmission(_bodyRenderers, _combat.staggerColor);
            SetSwing(RestSwingDegrees);
            _hits.Raise(new HitEvent(contactPoint, 0, HitKind.Parry));
            Play(_parryClip, 1f);
            return true;
        }

        private float ParryWindowSeconds()
        {
            int perceptionBonus = _playerStatus.HasValue ? _playerStatus.Last.Perception - _progression.baseStat : 0;
            return DamageCalculator.ParryWindowSeconds(perceptionBonus, _combat);
        }

        private void OnDamaged(int amount, Vector3 hitPoint)
        {
            float t = Mathf.Clamp01(amount / _combat.feedbackFullScaleDamage);
            _hitstopSeconds = Mathf.Lerp(_combat.hitstopMinSeconds, _combat.hitstopMaxSeconds, t);
            SetAgentStopped(true);
            _flashSeconds = _combat.hitFlashSeconds;
            SetEmission(_bodyRenderers, _combat.hitFlashColor);
            _hits.Raise(new HitEvent(hitPoint, amount, HitKind.Damage));
            Play(_hitClip, Mathf.Lerp(0.5f, 1f, t));
            if (CurrentState == State.Idle)
            {
                Enter(State.Approach);
            }
        }

        private void OnDied()
        {
            ReleaseToken();
            Enter(State.Dead);
            _agent.enabled = false;

            // Placeholder for the CMB-22 ragdoll: the body tips over under physics.
            _body.isKinematic = false;
            Vector3 away = Flat(transform.position - _playerHead.position).normalized;
            _body.AddForceAtPosition(away * DeathPushImpulse, transform.position + Vector3.up, ForceMode.Impulse);

            _director.HandleEnemyDied(this);
        }

        private void ReleaseToken()
        {
            if (_director.Tokens.Holds(_id))
            {
                _director.Tokens.Release(_id);
                _tokenRetrySeconds = _data.recoverSeconds;
            }
        }

        private static bool IsMovingState(State state) => state == State.Approach || state == State.Circle;

        private void SetAgentStopped(bool stopped)
        {
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = stopped;
            }
        }

        private void Face(Vector3 toPlayer)
        {
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(toPlayer);
            }
        }

        private void SetSwing(float degrees)
        {
            _weaponPivot.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null)
            {
                _audio.PlayOneShot(clip, volume);
            }
        }

        private void SetWeaponGlow(bool on)
        {
            if (_weaponRenderer == null)
            {
                return;
            }
            _block.Clear();
            _block.SetColor(EmissionColorId, on ? _combat.tellGlowColor : Color.black);
            _weaponRenderer.SetPropertyBlock(_block);
        }

        private void SetEmission(Renderer[] renderers, Color color)
        {
            _block.Clear();
            _block.SetColor(EmissionColorId, color);
            foreach (var r in renderers)
            {
                r.SetPropertyBlock(_block);
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
