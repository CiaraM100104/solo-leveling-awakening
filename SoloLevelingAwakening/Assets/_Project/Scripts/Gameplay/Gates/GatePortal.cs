using SLA.Core;
using UnityEngine;

namespace SLA.Gates
{
    /// <summary>
    /// A gate portal (MRH-3) or exit portal. Walking the head into it requests travel (MRH-8).
    /// The portal faces into the room along its local +Z.
    /// </summary>
    public class GatePortal : MonoBehaviour
    {
        private const float ArrivalCooldownSeconds = 1f;

        [SerializeField] private Transform _playerHead;
        [SerializeField] private Transform _destination;
        [SerializeField] private int _destinationAreaId;
        [Tooltip("Pulses gently so the gate reads as alive.")]
        [SerializeField] private Transform _swirl;
        [SerializeField] private float _pulseAmount = 0.03f;
        [SerializeField] private float _pulseRadiansPerSecond = 2f;
        [Tooltip("PLACEHOLDER: how close the head must get, and how wide the opening is.")]
        [SerializeField] private float _triggerDepthMeters = 0.45f;
        [SerializeField] private float _halfWidthMeters = 0.6f;

        [Header("Channels")]
        [SerializeField] private TravelRequestChannel _requests;
        [SerializeField] private TravelRequestChannel _arrived;

        private float _readyAt;
        private Vector3 _swirlScale;
        private bool _travelling;

        private void Awake()
        {
            if (_swirl != null)
            {
                _swirlScale = _swirl.localScale;
            }
        }

        private void OnEnable()
        {
            _arrived.Raised += OnArrived;
            _readyAt = Time.time + ArrivalCooldownSeconds;
            _travelling = false;
        }

        private void OnDisable()
        {
            _arrived.Raised -= OnArrived;
        }

        private void OnArrived(TravelRequest request)
        {
            _travelling = false;
            _readyAt = Time.time + ArrivalCooldownSeconds;
        }

        private void Update()
        {
            if (_swirl != null)
            {
                float pulse = 1f + _pulseAmount * Mathf.Sin(Time.time * _pulseRadiansPerSecond);
                _swirl.localScale = new Vector3(_swirlScale.x * pulse, _swirlScale.y, _swirlScale.z * pulse);
            }
            if (_travelling || Time.time < _readyAt)
            {
                return;
            }

            Vector3 local = transform.InverseTransformPoint(_playerHead.position);
            if (local.z > -0.1f && local.z < _triggerDepthMeters && Mathf.Abs(local.x) < _halfWidthMeters)
            {
                _travelling = true;
                _requests.Raise(new TravelRequest(_destination, _destinationAreaId, false));
            }
        }
    }
}
