using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SLA.Items
{
    /// <summary>
    /// Shows that something can be picked up: a soft System-blue pulse while it waits, a brighter glow while a hand
    /// or ray hovers it, and a floating grab icon above it that always faces the player. All of it hides while held.
    /// Danger is never signalled by this colour (CMF-11); this is the System's "interactable" blue (UI-1).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GrabHint : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Renderer[] _renderers = new Renderer[0];
        [Tooltip("Root of the icon (a world-space canvas). Placed above the object every frame.")]
        [SerializeField] private Transform _icon;
        [SerializeField] private CanvasGroup _iconGroup;

        [Header("Look (PLACEHOLDERS)")]
        [SerializeField] private Color _idleGlow = new Color(0.25f, 0.72f, 1f) * 0.35f;
        [SerializeField] private Color _hoverGlow = new Color(0.25f, 0.72f, 1f) * 1.6f;
        [SerializeField] private float _pulseRadiansPerSecond = 3f;
        [SerializeField] private float _iconHeightMeters = 0.16f;
        [SerializeField] private float _hoverIconScale = 1.35f;
        [Tooltip("The icon fades out beyond this distance so the room isn't covered in markers.")]
        [SerializeField] private float _iconRangeMeters = 4f;

        private XRGrabInteractable _grab;
        private MaterialPropertyBlock _block;
        private Transform _head;
        private Vector3 _iconScale;
        private bool _wasHeld;

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _block = new MaterialPropertyBlock();
            if (_icon != null)
            {
                _iconScale = _icon.localScale;
            }
        }

        private void Start()
        {
            var cam = Camera.main;
            _head = cam != null ? cam.transform : null;
        }

        private void OnDisable()
        {
            SetGlow(Color.black);
            if (_iconGroup != null)
            {
                _iconGroup.alpha = 0f;
            }
        }

        private void LateUpdate()
        {
            bool held = _grab.isSelected;
            if (held)
            {
                if (!_wasHeld)
                {
                    SetGlow(Color.black);
                    _iconGroup.alpha = 0f;
                }
                _wasHeld = true;
                return;
            }
            _wasHeld = false;

            bool hovered = _grab.isHovered;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * _pulseRadiansPerSecond);
            SetGlow(hovered ? _hoverGlow : _idleGlow * pulse);
            UpdateIcon(hovered);
        }

        private void UpdateIcon(bool hovered)
        {
            if (_icon == null || _head == null)
            {
                return;
            }
            Vector3 position = transform.position + Vector3.up * _iconHeightMeters;
            Vector3 toIcon = position - _head.position;
            float distance = toIcon.magnitude;
            _iconGroup.alpha = hovered ? 1f : Mathf.Clamp01((_iconRangeMeters - distance) / 0.5f);

            // World-space billboard: the icon turns to face the player, the camera never moves (CMF-1).
            toIcon.y = 0f;
            var facing = toIcon.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toIcon) : _icon.rotation;
            _icon.SetPositionAndRotation(position, facing);
            _icon.localScale = hovered ? _iconScale * _hoverIconScale : _iconScale;
        }

        private void SetGlow(Color color)
        {
            _block.Clear();
            _block.SetColor(EmissionColorId, color);
            foreach (var r in _renderers)
            {
                if (r != null)
                {
                    r.SetPropertyBlock(_block);
                }
            }
        }
    }
}
