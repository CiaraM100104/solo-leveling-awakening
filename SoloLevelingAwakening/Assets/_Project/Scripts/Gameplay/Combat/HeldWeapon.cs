using SLA.Core;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace SLA.Combat
{
    /// <summary>
    /// The player side of a melee weapon: hit feedback (haptics, spark, sound; CMB-20, damage numbers
    /// not built yet), the wielder's STR from the save, and a hip holster. A weapon nobody holds floats at
    /// the player's hip after a moment, so a dropped dagger is never lost (a placeholder for the
    /// hipL/hipR equipment slots).
    /// </summary>
    [RequireComponent(typeof(WeaponHitDetector), typeof(XRGrabInteractable), typeof(Rigidbody))]
    public class HeldWeapon : MonoBehaviour
    {
        // Feedback scales between these for the weakest and strongest hit.
        private const float EnemyHitHapticMin = 0.4f;
        private const float EnemyHitHapticMax = 1f;
        private const float EnemyHitSparksMin = 6f;
        private const float EnemyHitSparksMax = 20f;
        private const float EnemyHitVolumeMin = 0.5f;
        private const float EnemyHitVolumeMax = 1f;
        private const float SurfaceHapticMin = 0.25f;
        private const float SurfaceHapticMax = 0.7f;
        private const float SurfaceSparksMin = 8f;
        private const float SurfaceSparksMax = 28f;
        private const float SurfaceVolumeMin = 0.3f;
        private const float SurfaceVolumeMax = 0.8f;
        // Sparks and glow start this far off the struck surface, so they aren't buried in it.
        private const float SurfaceSparkLiftMeters = 0.01f;
        // The glow flash behind the sparks: this wide for the weakest and strongest hit.
        private const float GlowSizeMinMeters = 0.08f;
        private const float GlowSizeMaxMeters = 0.22f;
        // Holstered blade points down and a little forward.
        private const float HolsterPitchDegrees = 100f;
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [Header("Hit feedback (CMB-20)")]
        [Tooltip("Cyan sparks; their color and fade come from the particle system.")]
        [SerializeField] private ParticleSystem _spark;
        [Tooltip("One soft glow flash per hit, behind the sparks.")]
        [SerializeField] private ParticleSystem _glow;
        [SerializeField] private AudioSource _audio;
        [Tooltip("Haptic pulse length for the weakest and strongest hits (s).")]
        [SerializeField] private Vector2 _hapticSeconds = new Vector2(0.04f, 0.12f);
        [Tooltip("A hit of this much damage gets the strongest feedback.")]
        [SerializeField] private float _fullFeedbackDamage = 15f;

        [Header("Striking walls and other solids")]
        [Tooltip("An impact this fast gets the most sparks.")]
        [FormerlySerializedAs("_fullSurfaceSparkSpeed")]
        [SerializeField] private float _fullSurfaceSparkSpeedMetersPerSecond = 5f;

        [Header("Holster")]
        [Tooltip("Hip position relative to the head's facing: x right, z forward.")]
        [FormerlySerializedAs("_holsterOffset")]
        [SerializeField] private Vector2 _holsterOffsetMeters = new Vector2(0.22f, 0.12f);
        [Tooltip("Hip height as a fraction of head height.")]
        [SerializeField] private float _hipHeightFraction = 0.58f;
        [Tooltip("Seconds after letting go before the weapon returns to the hip.")]
        [SerializeField] private float _holsterDelaySeconds = 1f;
        [Tooltip("Stays where it was placed (e.g. on a rack) until first grabbed; holsters after that.")]
        [SerializeField] private bool _waitUntilFirstGrab;

        private WeaponHitDetector _detector;
        private XRGrabInteractable _grab;
        private Rigidbody _body;
        private Transform _player;
        private Transform _head;
        private float _releasedAt;
        private bool _onRack;

        private bool Holstered =>
            !_onRack && !_grab.isSelected && _player != null && _head != null && Time.time - _releasedAt >= _holsterDelaySeconds;

        private void Awake()
        {
            _detector = GetComponent<WeaponHitDetector>();
            _grab = GetComponent<XRGrabInteractable>();
            _body = GetComponent<Rigidbody>();
            _releasedAt = -_holsterDelaySeconds; // start holstered
            _onRack = _waitUntilFirstGrab;
        }

        private void OnEnable()
        {
            _detector.Hit += OnHit;
            _detector.SurfaceStruck += OnSurfaceStruck;
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            _detector.Hit -= OnHit;
            _detector.SurfaceStruck -= OnSurfaceStruck;
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
        }

        private void Start()
        {
            _detector.Strength = GameSession.Data.player.stats.str;

            var player = GameObject.FindWithTag("Player");
            _player = player != null ? player.transform : null;
            var cam = Camera.main;
            _head = cam != null ? cam.transform : null;

            // The blade must not knock against the player's own body capsule.
            var body = player != null ? player.GetComponent<CharacterController>() : null;
            if (body != null)
            {
                foreach (var c in GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(c, body);
                }
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            _onRack = false;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            _releasedAt = Time.time;
        }

        private void FixedUpdate()
        {
            if (Holstered && !_body.isKinematic)
            {
                _body.isKinematic = true;
            }
        }

        // The holster follows the head every rendered frame, so the dagger doesn't trail behind the hip.
        private void LateUpdate()
        {
            if (!Holstered)
            {
                return;
            }

            float floorY = _player.position.y;
            Vector3 forward = _head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < MinDirectionSqrMagnitude)
            {
                forward = _player.forward;
            }
            var yaw = Quaternion.LookRotation(forward.normalized);

            Vector3 head = _head.position;
            Vector3 hip = new Vector3(head.x, floorY + (head.y - floorY) * _hipHeightFraction, head.z)
                          + yaw * new Vector3(_holsterOffsetMeters.x, 0f, _holsterOffsetMeters.y);

            transform.SetPositionAndRotation(hip, yaw * Quaternion.Euler(HolsterPitchDegrees, 0f, 0f));
        }

        private void OnSurfaceStruck(Vector3 point, Vector3 normal, float speed)
        {
            if (!_grab.isSelected)
            {
                return; // a dropped or racked dagger clattering down stays quiet
            }

            float strength = Mathf.Clamp01(speed / _fullSurfaceSparkSpeedMetersPerSecond);
            Pulse(Mathf.Lerp(SurfaceHapticMin, SurfaceHapticMax, strength), Mathf.Lerp(_hapticSeconds.x, _hapticSeconds.y, strength));

            Burst(point + normal * SurfaceSparkLiftMeters, Mathf.Lerp(SurfaceSparksMin, SurfaceSparksMax, strength), strength);

            if (_audio != null)
            {
                _audio.transform.position = point;
                _audio.PlayOneShot(PlaceholderSounds.Block, Mathf.Lerp(SurfaceVolumeMin, SurfaceVolumeMax, strength));
            }
        }

        private void OnHit(HitInfo hit)
        {
            float strength = Mathf.Clamp01(hit.Damage / _fullFeedbackDamage);
            Pulse(Mathf.Lerp(EnemyHitHapticMin, EnemyHitHapticMax, strength), Mathf.Lerp(_hapticSeconds.x, _hapticSeconds.y, strength));

            Burst(hit.Point + hit.Normal * SurfaceSparkLiftMeters, Mathf.Lerp(EnemyHitSparksMin, EnemyHitSparksMax, strength), strength);

            if (_audio != null)
            {
                _audio.transform.position = hit.Point;
                _audio.PlayOneShot(PlaceholderSounds.Impact, Mathf.Lerp(EnemyHitVolumeMin, EnemyHitVolumeMax, strength));
            }
        }

        // Both systems simulate in world space and are reused for every hit (pooled, never spawned).
        private void Burst(Vector3 point, float sparkCount, float strength)
        {
            if (_spark != null)
            {
                var sparks = new ParticleSystem.EmitParams { position = point, applyShapeToPosition = true };
                _spark.Emit(sparks, Mathf.RoundToInt(sparkCount));
            }

            if (_glow != null)
            {
                var glow = new ParticleSystem.EmitParams
                {
                    position = point,
                    startSize = Mathf.Lerp(GlowSizeMinMeters, GlowSizeMaxMeters, strength),
                };
                _glow.Emit(glow, 1);
            }
        }

        private void Pulse(float amplitude, float seconds)
        {
            if (_grab.interactorsSelecting.Count > 0 && _grab.interactorsSelecting[0] is XRBaseInputInteractor hand)
            {
                hand.SendHapticImpulse(amplitude, seconds);
            }
        }
    }
}
