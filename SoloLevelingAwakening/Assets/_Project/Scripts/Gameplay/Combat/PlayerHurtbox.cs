using SLA.Core;
using SLA.Core.Events;
using SLA.Data;
using SLA.Gates;
using UnityEngine;

namespace SLA.Combat
{
    /// <summary>
    /// The player's body in a fight: a capsule from the headset down to the floor (CMB-15), so leaning or
    /// stepping out of a strike dodges it, plus the player's HP. At 0 HP the view fades out, the player
    /// respawns at the entrance with XP kept (GTE-4), and the view fades back in (never a hard cut, CMF-1).
    /// A level-up restores HP (PRG-3). Lives in the gate scene, not on the rig, so it unloads with the gate.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody), typeof(Health))]
    public class PlayerHurtbox : MonoBehaviour
    {
        private const int CapsuleAxisY = 1;
        // The capsule reaches this far above the eyes to cover the top of the head.
        private const float AboveEyesMeters = 0.1f;
        // Never shorter than this, even crouched on the floor.
        private const float MinHeightMeters = 0.6f;
        // Wait after the respawn move before clearing the view, so the move lands while it's still dark.
        private const float RespawnSettleSeconds = 0.1f;
        // Used only if no ProgressionConfig is assigned: PRG-6's base HP.
        private const int FallbackMaxHp = 100;

        [SerializeField] private ProgressionConfig _progression;
        [Tooltip("Raised with the damage taken by each hit.")]
        [SerializeField] private FloatEventChannel _onDamageTaken;
        [Tooltip("Raised with the player's HP after every change.")]
        [SerializeField] private FloatEventChannel _onPlayerHpChanged;
        [SerializeField] private VoidEventChannel _onPlayerDied;
        [SerializeField] private IntEventChannel _onLevelUp;
        [SerializeField] private float _radiusMeters = 0.2f;
        [SerializeField] private float _fadeSeconds = 0.4f;
        [SerializeField] private float _blackSeconds = 1.2f;

        private Health _health;
        private CapsuleCollider _capsule;
        private Transform _player;
        private Transform _head;
        private ScreenFader _fader;
        private float _respawnAt = -1f;
        private float _fadeInAt = -1f;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _capsule = GetComponent<CapsuleCollider>();
            _capsule.isTrigger = true;
            _capsule.direction = CapsuleAxisY;
            _capsule.radius = _radiusMeters;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            if (_onLevelUp != null)
            {
                _onLevelUp.Raised += OnLevelUp;
            }
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            if (_onLevelUp != null)
            {
                _onLevelUp.Raised -= OnLevelUp;
            }
        }

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            _player = player != null ? player.transform : null;
            var cam = Camera.main;
            _head = cam != null ? cam.transform : null;
            _fader = player != null ? player.GetComponentInChildren<ScreenFader>(true) : null;
            RefillHp();
        }

        // The capsule is a physics trigger that enemy strikes test against in their FixedUpdate.
        private void FixedUpdate()
        {
            FollowHead();
        }

        private void Update()
        {
            if (_respawnAt >= 0f && Time.time >= _respawnAt)
            {
                _respawnAt = -1f;
                if (PlayerSpawn.Current != null)
                {
                    PlayerSpawn.Current.MovePlayerHere();
                }
                RefillHp();
                _fadeInAt = Time.time + RespawnSettleSeconds;
            }

            if (_fadeInAt >= 0f && Time.time >= _fadeInAt)
            {
                _fadeInAt = -1f;
                if (_fader != null)
                {
                    _fader.FadeOverlay(0f, _fadeSeconds);
                }
            }
        }

        private void FollowHead()
        {
            if (_player == null || _head == null)
            {
                return;
            }

            Vector3 head = _head.position;
            float floorY = _player.position.y;
            float height = Mathf.Max(MinHeightMeters, head.y - floorY + AboveEyesMeters);
            transform.SetPositionAndRotation(new Vector3(head.x, floorY + height * 0.5f, head.z), Quaternion.identity);
            _capsule.height = height;
        }

        private int MaxHp()
        {
            int vitality = GameSession.Data.player.stats.vit;
            return _progression != null ? _progression.MaxHp(vitality) : FallbackMaxHp;
        }

        private void RefillHp()
        {
            _health.Initialize(MaxHp(), 0f);
            if (_onPlayerHpChanged != null)
            {
                _onPlayerHpChanged.Raise(_health.Current);
            }
        }

        private void OnDamaged(HitInfo hit)
        {
            if (_onDamageTaken != null)
            {
                _onDamageTaken.Raise(hit.Damage);
            }
            if (_onPlayerHpChanged != null)
            {
                _onPlayerHpChanged.Raise(_health.Current);
            }
        }

        private void OnLevelUp(int newLevel)
        {
            if (!_health.IsDead)
            {
                RefillHp();
            }
        }

        private void OnDied()
        {
            if (_onPlayerDied != null)
            {
                _onPlayerDied.Raise();
            }
            if (_fader != null)
            {
                _fader.FadeOverlay(1f, _fadeSeconds);
            }
            _respawnAt = Time.time + _fadeSeconds + _blackSeconds;
        }
    }
}
