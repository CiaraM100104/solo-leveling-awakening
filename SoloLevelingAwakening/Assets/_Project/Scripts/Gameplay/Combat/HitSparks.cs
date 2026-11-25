using SLA.Core;
using SLA.Data;
using UnityEngine;

namespace SLA.Combat
{
    /// <summary>
    /// CMB-20: a cyan spark burst at every damaging hit, more sparks for harder hits. Uses a small pool of
    /// particle systems (CODING_STANDARDS rule 8), so nothing is created mid-fight and nothing allocates per hit.
    /// Sparks stay at the hit point; nothing flashes the whole view (CMF-12).
    /// </summary>
    public class HitSparks : MonoBehaviour
    {
        [SerializeField] private CombatConfig _combat;
        [SerializeField] private HitEventChannel _hits;
        [Tooltip("Copies of the VFX_HitSpark_Mana prefab, used in turn.")]
        [SerializeField] private ParticleSystem[] _pool = new ParticleSystem[0];

        private int _next;

        private void OnEnable()
        {
            _hits.Raised += OnHit;
        }

        private void OnDisable()
        {
            _hits.Raised -= OnHit;
        }

        private void OnHit(HitEvent hit)
        {
            if (hit.Kind != HitKind.Damage || _pool.Length == 0)
            {
                return;
            }
            var sparks = _pool[_next];
            _next = (_next + 1) % _pool.Length;

            float t = Mathf.Clamp01(hit.Amount / _combat.feedbackFullScaleDamage);
            int count = Mathf.RoundToInt(Mathf.Lerp(_combat.hitSparkMinCount, _combat.hitSparkMaxCount, t));
            sparks.transform.position = hit.Position;
            sparks.Emit(count);
        }
    }
}
