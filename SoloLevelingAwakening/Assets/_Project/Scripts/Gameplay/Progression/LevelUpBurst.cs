using SLA.Core.Events;
using UnityEngine;

namespace SLA.Progression
{
    /// <summary>
    /// Gold sparks rising from a glowing ring on the floor around the player when they level up (PRG-3,
    /// gold for level-ups per UI-1). Kept at floor and body height and never in front of the eyes, so it is
    /// not a full-screen flash (CMF-12). One reused set of particle systems, never spawned per level-up.
    /// </summary>
    public class LevelUpBurst : MonoBehaviour
    {
        [SerializeField] private IntEventChannel _onLevelUp;
        [Tooltip("Plays with its child systems.")]
        [SerializeField] private ParticleSystem _burst;

        private Transform _player;
        private Transform _head;

        private void OnEnable()
        {
            if (_onLevelUp != null)
            {
                _onLevelUp.Raised += OnLevelUp;
            }
        }

        private void OnDisable()
        {
            if (_onLevelUp != null)
            {
                _onLevelUp.Raised -= OnLevelUp;
            }
        }

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            _player = player != null ? player.transform : null;
            var cam = Camera.main;
            _head = cam != null ? cam.transform : null;
        }

        private void OnLevelUp(int newLevel)
        {
            if (_burst == null || _player == null || _head == null)
            {
                return;
            }

            // Centered under the head, on the floor the player stands on.
            Vector3 head = _head.position;
            transform.position = new Vector3(head.x, _player.position.y, head.z);
            _burst.Play(true);
        }

        [ContextMenu("Play (test in Play mode)")]
        private void PlayForTesting() => OnLevelUp(0);
    }
}
