using SLA.Core;
using SLA.Data;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SLA.Combat
{
    /// <summary>
    /// On a held weapon: turns collisions into damage (CMB-4 to CMB-10), parries enemy weapons (CMB-14)
    /// and plays controller haptics (CMB-20). Enemies check <see cref="IsHeld"/> to see if it blocks (CMB-13).
    /// Not built yet: edge/blunt detection (CMB-11), contact drag (CMB-12), crits (CMB-7 chance is TBD).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class WeaponHitDetector : MonoBehaviour
    {
        private const float MinHapticAmplitude = 0.3f;

        [SerializeField] private WeaponData _weapon;
        [SerializeField] private CombatConfig _combat;
        [SerializeField] private ProgressionConfig _progression;
        [SerializeField] private PlayerStatusChannel _playerStatus;
        [Tooltip("The blade tip; its path decides whether a hit was a real swing (CMB-9).")]
        [SerializeField] private Transform _tip;
        [Tooltip("Colliders this weapon never touches, such as the player's body.")]
        [SerializeField] private Collider[] _ignoredColliders = new Collider[0];

        private readonly SwingTracker _swing = new SwingTracker();
        private readonly HitCooldownTracker _cooldowns = new HitCooldownTracker();
        private XRGrabInteractable _grab;
        private HapticImpulsePlayer _haptics;

        public bool IsHeld { get; private set; }

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            GetComponent<Rigidbody>().mass = _weapon.massKg;
        }

        private void Start()
        {
            var own = GetComponentsInChildren<Collider>();
            foreach (var ignored in _ignoredColliders)
            {
                if (ignored == null)
                {
                    continue;
                }
                foreach (var mine in own)
                {
                    Physics.IgnoreCollision(mine, ignored);
                }
            }
        }

        private void OnEnable()
        {
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            IsHeld = true;
            _haptics = args.interactorObject.transform.GetComponentInParent<HapticImpulsePlayer>();
            _swing.Clear();
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            IsHeld = _grab.isSelected;
            if (!IsHeld)
            {
                _haptics = null;
            }
        }

        private void FixedUpdate()
        {
            if (IsHeld)
            {
                _swing.Record(Time.fixedTime, _tip.position);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsHeld)
            {
                return;
            }

            float now = Time.fixedTime;
            float speed = collision.relativeVelocity.magnitude;
            Vector3 contact = collision.GetContact(0).point;

            // CMB-14: blade meets the enemy's weapon during its swing.
            if (collision.collider.TryGetComponent(out EnemyWeaponHitbox enemyWeapon))
            {
                if (enemyWeapon.Owner != null && enemyWeapon.Owner.TryParry(speed, contact))
                {
                    PlayHaptics(_combat.feedbackFullScaleDamage);
                }
                return;
            }

            var target = collision.collider.GetComponentInParent<Health>();
            if (target == null || target.IsDead)
            {
                return;
            }

            int targetId = target.GetHashCode(); // Unique per UnityEngine.Object.
            if (!_cooldowns.CanHit(targetId, now, _combat.perTargetCooldown))
            {
                return;
            }
            if (!_swing.HasSwung(now, _combat.swingArcWindow, _combat.minSwingArc, _tip.position))
            {
                return;
            }

            var zone = collision.collider.TryGetComponent(out HitZoneMarker marker) ? marker.Zone : HitZone.Torso;
            int strengthBonus = _playerStatus.HasValue ? _playerStatus.Last.Strength - _progression.baseStat : 0;
            var input = new DamageInput(_weapon.baseDamage, speed, strengthBonus, zone, false, target.Armor,
                target.IsStaggered);
            float damage = DamageCalculator.Calculate(input, _combat);

            int dealt = target.TakeDamage(damage, contact);
            if (dealt <= 0)
            {
                return;
            }
            _cooldowns.RegisterHit(targetId, now);
            PlayHaptics(dealt);
        }

        private void PlayHaptics(float damage)
        {
            if (_haptics == null)
            {
                return;
            }
            float t = Mathf.Clamp01(damage / _combat.feedbackFullScaleDamage);
            float amplitude = Mathf.Lerp(MinHapticAmplitude, 1f, t);
            float duration = Mathf.Lerp(_combat.hapticMinSeconds, _combat.hapticMaxSeconds, t);
            _haptics.SendHapticImpulse(amplitude, duration);
        }
    }
}
