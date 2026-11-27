using SLA.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace SLA.AI
{
    /// <summary>
    /// Placeholder enemy feedback until the tech-art pass: the windup tell (red weapon glow and a cue,
    /// CMB-17), a white flash and HP bar on hits, a block clang, and a fall-and-sink on death in place of
    /// the ragdoll (CMB-22). Never touches the camera (CMB-21).
    /// </summary>
    [RequireComponent(typeof(EnemyBrain), typeof(Health))]
    public class EnemyFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private const float TellVolume = 0.9f;
        private const float BlockVolume = 1f;
        // Hit sound: this loud at 0 damage, plus 1/HitVolumeDamageScale per point of damage, capped at 1.
        private const float HitVolumeBase = 0.4f;
        private const float HitVolumeDamageScale = 20f;
        // Death: tip over backwards this far, then sink for the last SinkSeconds of the corpse's life.
        private const float FallDegrees = 85f;
        private const float SinkSeconds = 0.8f;
        private const float SinkMetersPerSecond = 0.6f;
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [SerializeField] private Renderer[] _bodyRenderers;
        [SerializeField] private Renderer _weaponRenderer;
        [Tooltip("Scaled on x from 1 (full HP) to 0; its pivot is the bar's left edge.")]
        [SerializeField] private Transform _hpFill;
        [Tooltip("Turned to face the player each frame.")]
        [SerializeField] private Transform _hpBar;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private Color _tellColor = new Color(1f, 0.08f, 0.05f);
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField] private float _flashSeconds = 0.08f;
        [SerializeField] private float _fallSeconds = 0.35f;
        [SerializeField] private float _corpseSeconds = 3f;

        private EnemyBrain _brain;
        private Health _health;
        private EnemyMeleeAttack _attack;
        private NavMeshAgent _agent;
        private Collider[] _colliders;
        private MaterialPropertyBlock _block;
        private Color[] _bodyColors;
        private Color _weaponColor;
        private Transform _camera;
        private float _flashUntil;
        private bool _flashing;
        private float _diedAt = -1f;
        private Quaternion _aliveRotation;

        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _health = GetComponent<Health>();
            _attack = GetComponent<EnemyMeleeAttack>();
            _agent = GetComponent<NavMeshAgent>();
            _colliders = GetComponentsInChildren<Collider>();
            _block = new MaterialPropertyBlock();

            _bodyColors = new Color[_bodyRenderers.Length];
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                _bodyColors[i] = BaseColor(_bodyRenderers[i]);
            }
            _weaponColor = BaseColor(_weaponRenderer);
        }

        private void OnEnable()
        {
            _brain.StateChanged += OnStateChanged;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            if (_attack != null)
            {
                _attack.Struck += OnStruck;
            }
        }

        private void OnDisable()
        {
            _brain.StateChanged -= OnStateChanged;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            if (_attack != null)
            {
                _attack.Struck -= OnStruck;
            }
        }

        private void Start()
        {
            var cam = Camera.main;
            _camera = cam != null ? cam.transform : null;
            UpdateHpBar();
        }

        private void OnStateChanged(EnemyState state)
        {
            bool telling = state == EnemyState.Windup;
            SetColor(_weaponRenderer, telling ? _tellColor : _weaponColor);
            if (telling)
            {
                Play(PlaceholderSounds.Tell, TellVolume);
            }
            if (state == EnemyState.Idle)
            {
                UpdateHpBar(); // healed after the player respawned
            }
        }

        private void OnDamaged(HitInfo hit)
        {
            _flashUntil = Time.time + _flashSeconds;
            _flashing = true;
            SetBodyColor(true);
            UpdateHpBar();
            Play(PlaceholderSounds.Impact, Mathf.Clamp01(HitVolumeBase + hit.Damage / HitVolumeDamageScale));
        }

        private void OnStruck(bool blocked)
        {
            if (blocked)
            {
                Play(PlaceholderSounds.Block, BlockVolume);
            }
        }

        private void OnDied()
        {
            _diedAt = Time.time;
            _aliveRotation = transform.rotation;
            SetColor(_weaponRenderer, _weaponColor);
            foreach (Collider c in _colliders)
            {
                c.enabled = false;
            }
            if (_agent != null)
            {
                _agent.enabled = false;
            }
            if (_hpBar != null)
            {
                _hpBar.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_flashing && Time.time >= _flashUntil)
            {
                _flashing = false;
                SetBodyColor(false);
            }

            if (_diedAt >= 0f)
            {
                AnimateDeath(Time.time - _diedAt);
                return;
            }

            if (_hpBar != null && _camera != null)
            {
                Vector3 away = _hpBar.position - _camera.position;
                away.y = 0f;
                if (away.sqrMagnitude > MinDirectionSqrMagnitude)
                {
                    _hpBar.rotation = Quaternion.LookRotation(away);
                }
            }
        }

        // Tip over backwards, wait, then sink into the floor and go away.
        private void AnimateDeath(float elapsed)
        {
            float fall = Mathf.Clamp01(elapsed / _fallSeconds);
            transform.rotation = _aliveRotation * Quaternion.Euler(-FallDegrees * fall * fall, 0f, 0f);

            if (elapsed > _corpseSeconds - SinkSeconds)
            {
                transform.position += Vector3.down * (SinkMetersPerSecond * Time.deltaTime);
            }
            if (elapsed > _corpseSeconds)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateHpBar()
        {
            if (_hpFill == null || _health.Max <= 0f)
            {
                return;
            }
            _hpFill.localScale = new Vector3(Mathf.Clamp01(_health.Current / _health.Max), 1f, 1f);
        }

        private void SetBodyColor(bool flash)
        {
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                SetColor(_bodyRenderers[i], flash ? _flashColor : _bodyColors[i]);
            }
        }

        private void SetColor(Renderer target, Color color)
        {
            if (target == null)
            {
                return;
            }
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }

        private static Color BaseColor(Renderer target)
        {
            if (target == null || target.sharedMaterial == null || !target.sharedMaterial.HasProperty(BaseColorId))
            {
                return Color.white;
            }
            return target.sharedMaterial.GetColor(BaseColorId);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (_audio != null)
            {
                _audio.PlayOneShot(clip, volume);
            }
        }
    }
}
