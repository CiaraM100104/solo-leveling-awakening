using SLA.Core;
using TMPro;
using UnityEngine;

namespace SLA.UI
{
    /// <summary>
    /// UI-7: pooled numbers at the hit point in System blue, fading over 0.6 s. Also shows PARRY, BLOCK and gold.
    /// Allocation-free after Awake.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        [SerializeField] private HitEventChannel _hits;
        [SerializeField] private Transform _head;
        [SerializeField] private TextMeshPro[] _pool = new TextMeshPro[0];
        [SerializeField] private float _lifetimeSeconds = 0.6f;
        [Tooltip("PLACEHOLDER.")]
        [SerializeField] private float _riseMetersPerSecond = 0.6f;

        private float[] _age;
        private int _next;

        private void Awake()
        {
            _age = new float[_pool.Length];
            for (int i = 0; i < _pool.Length; i++)
            {
                _age[i] = _lifetimeSeconds;
                _pool[i].gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            _hits.Raised += Show;
        }

        private void OnDisable()
        {
            _hits.Raised -= Show;
        }

        private void Show(HitEvent hit)
        {
            var text = _pool[_next];
            _age[_next] = 0f;
            _next = (_next + 1) % _pool.Length;

            text.transform.position = hit.Position;
            text.gameObject.SetActive(true);
            switch (hit.Kind)
            {
                case HitKind.Parry:
                    text.SetText("PARRY");
                    text.color = SystemPalette.Reward;
                    break;
                case HitKind.Block:
                    text.SetText("BLOCK");
                    text.color = SystemPalette.Text;
                    break;
                case HitKind.Gold:
                    text.SetText("+{0} G", hit.Amount);
                    text.color = SystemPalette.Reward;
                    break;
                default:
                    text.SetText("{0}", hit.Amount);
                    text.color = SystemPalette.Info;
                    break;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 head = _head.position;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_age[i] >= _lifetimeSeconds)
                {
                    continue;
                }
                _age[i] += dt;
                var text = _pool[i];
                if (_age[i] >= _lifetimeSeconds)
                {
                    text.gameObject.SetActive(false);
                    continue;
                }
                Transform t = text.transform;
                t.position += Vector3.up * (_riseMetersPerSecond * dt);
                t.rotation = Quaternion.LookRotation(t.position - head);
                text.alpha = 1f - _age[i] / _lifetimeSeconds;
            }
        }
    }
}
