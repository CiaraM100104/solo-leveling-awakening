using SLA.Data;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SLA.Combat
{
    /// <summary>
    /// A glowing ribbon behind a held blade, drawn only while the blade moves fast enough to deal damage
    /// (CombatConfig.minHitSpeed), so the trail doubles as a "this swing counts" cue and slow waving draws nothing.
    /// Sits partway up the blade, facing across its flat, so the ribbon's width runs along the blade.
    /// </summary>
    [RequireComponent(typeof(TrailRenderer))]
    public class SlashTrail : MonoBehaviour
    {
        [SerializeField] private CombatConfig _config;
        [SerializeField] private XRGrabInteractable _grab;

        private TrailRenderer _trail;
        private Vector3 _lastPosition;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            _trail.emitting = false;
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
        }

        // Visual only, so it tracks rendered frames rather than physics steps.
        private void Update()
        {
            Vector3 position = transform.position;
            float speed = Time.deltaTime > 0f ? (position - _lastPosition).magnitude / Time.deltaTime : 0f;
            _lastPosition = position;

            bool held = _grab != null && _grab.isSelected;
            _trail.emitting = held && _config != null && speed >= _config.minHitSpeed;
        }
    }
}
