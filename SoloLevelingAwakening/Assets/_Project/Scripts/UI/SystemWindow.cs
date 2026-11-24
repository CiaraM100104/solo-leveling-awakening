using SLA.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SLA.UI
{
    /// <summary>
    /// Shows one <see cref="SystemMessage"/> at a time, coloured by kind (UI-1), with the System chime (UI-2),
    /// then hides itself. Placement comes from a Lazy Follow on the same object (UI-3).
    /// </summary>
    public class SystemWindow : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [Tooltip("Four thin edge strips, coloured by message kind (UI-1).")]
        [SerializeField] private Image[] _border = new Image[0];
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _chime;
        [Tooltip("PLACEHOLDER (not in spec): how long each message stays up.")]
        [SerializeField] private float _visibleSeconds = 3.5f;
        [SerializeField] private float _fadeSeconds = 0.3f;

        private float _remainingSeconds;

        public bool IsShowing => _remainingSeconds > 0f;

        private void Awake()
        {
            _background.color = SystemPalette.Background;
            _text.color = SystemPalette.Text;
            _group.alpha = 0f;
        }

        public void Show(in SystemMessage message)
        {
            Color edge = SystemPalette.For(message.Kind);
            foreach (var strip in _border)
            {
                strip.color = edge;
            }
            _text.text = SystemMessage.Bracket(message.Text);
            _remainingSeconds = _visibleSeconds;
            _group.alpha = 1f;
            if (_chime != null)
            {
                _audio.PlayOneShot(_chime);
            }
        }

        private void Update()
        {
            if (_remainingSeconds <= 0f)
            {
                return;
            }
            _remainingSeconds -= Time.deltaTime;
            _group.alpha = Mathf.Clamp01(_remainingSeconds / _fadeSeconds);
        }
    }
}
