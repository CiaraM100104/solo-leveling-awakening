using System.Collections.Generic;
using SLA.Core;
using UnityEngine;

namespace SLA.UI
{
    /// <summary>Queues System messages from every system and shows them one after another.</summary>
    public class SystemMessageService : MonoBehaviour
    {
        [SerializeField] private SystemWindow _window;

        [Header("Channels")]
        [SerializeField] private SystemMessageChannel _messages;
        [SerializeField] private IntEventChannel _onLevelUp;
        [SerializeField] private VoidEventChannel _onPlayerDied;

        [Header("Text")]
        [SerializeField] private string _welcome = "Welcome, Player.";

        private readonly Queue<SystemMessage> _queue = new Queue<SystemMessage>();

        private void Awake()
        {
            // Before other systems' Start, so the welcome comes first.
            Enqueue(new SystemMessage(SystemMessageKind.Info, _welcome));
        }

        private void OnEnable()
        {
            _messages.Raised += Enqueue;
            _onLevelUp.Raised += OnLevelUp;
            _onPlayerDied.Raised += OnPlayerDied;
        }

        private void OnDisable()
        {
            _messages.Raised -= Enqueue;
            _onLevelUp.Raised -= OnLevelUp;
            _onPlayerDied.Raised -= OnPlayerDied;
        }

        private void Update()
        {
            if (_queue.Count > 0 && !_window.IsShowing)
            {
                _window.Show(_queue.Dequeue());
            }
        }

        private void Enqueue(SystemMessage message)
        {
            _queue.Enqueue(message);
        }

        // UI-13: a level-up mid-fight is a small gold toast; this window is that toast.
        private void OnLevelUp(int level) =>
            Enqueue(new SystemMessage(SystemMessageKind.Reward,
                string.Format("Level up! You are now Level {0}. Stat points available.", level)));

        private void OnPlayerDied() =>
            Enqueue(new SystemMessage(SystemMessageKind.Warning, "You have fallen. Returning to the gate entrance."));
    }
}
