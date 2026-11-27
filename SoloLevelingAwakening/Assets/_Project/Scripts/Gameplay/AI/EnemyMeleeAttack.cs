using System;
using SLA.Combat;
using SLA.Data;
using UnityEngine;

namespace SLA.AI
{
    /// <summary>
    /// The enemy's hit: when the brain enters Attack, one strike lands in a sphere in front of the enemy.
    /// It hurts the player's hurtbox if it is still there (step or lean out to dodge, CMB-15), and a player
    /// weapon inside the sphere blocks it (CMB-13, block fatigue not built yet).
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class EnemyMeleeAttack : MonoBehaviour
    {
        [SerializeField] private CombatConfig _config;
        [Tooltip("Strike center: this far in front of the enemy (m)...")]
        [SerializeField] private float _reachMeters = 0.9f;
        [Tooltip("...at this height above its feet (m).")]
        [SerializeField] private float _strikeHeightMeters = 1.1f;
        [SerializeField] private float _strikeRadiusMeters = 0.45f;
        [Tooltip("Layer of the player's hurtbox.")]
        [SerializeField] private LayerMask _playerMask;
        [Tooltip("Layer of the player's weapons; one inside the strike blocks it.")]
        [SerializeField] private LayerMask _playerWeaponMask;

        private const int MaxOverlaps = 16;

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private EnemyBrain _brain;
        private bool _strikePending;

        /// <summary>Raised when a strike reaches the player; true if it was blocked.</summary>
        public event Action<bool> Struck;

        public Vector3 StrikeCenter => transform.position + Vector3.up * _strikeHeightMeters + transform.forward * _reachMeters;

        private void Awake() => _brain = GetComponent<EnemyBrain>();

        private void OnEnable() => _brain.StateChanged += OnStateChanged;

        private void OnDisable() => _brain.StateChanged -= OnStateChanged;

        // The brain changes state in Update; the hit test itself runs on the next physics step.
        private void OnStateChanged(EnemyState state)
        {
            if (state == EnemyState.Attack && _brain.Data != null)
            {
                _strikePending = true;
            }
        }

        private void FixedUpdate()
        {
            if (_strikePending)
            {
                _strikePending = false;
                Strike();
            }
        }

        private void Strike()
        {
            Vector3 center = StrikeCenter;
            int count = Physics.OverlapSphereNonAlloc(center, _strikeRadiusMeters, _overlaps,
                _playerMask | _playerWeaponMask, QueryTriggerInteraction.Collide);

            IDamageable target = null;
            Collider targetCollider = null;
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                Collider other = _overlaps[i];
                if ((_playerWeaponMask.value & (1 << other.gameObject.layer)) != 0)
                {
                    blocked = true;
                }
                else if (target == null)
                {
                    target = other.GetComponentInParent<IDamageable>();
                    targetCollider = other;
                }
            }

            if (target == null || target.IsDead)
            {
                return;
            }

            float reduction = blocked && _config != null ? _config.blockDamageReduction : 0f;
            float damage = _brain.Data.damage * (1f - reduction);
            var hit = new HitInfo(damage, targetCollider.ClosestPoint(center), -transform.forward, HitZone.Torso, 0f);
            target.ApplyDamage(hit);
            Struck?.Invoke(blocked);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(StrikeCenter, _strikeRadiusMeters);
        }
    }
}
