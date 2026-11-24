using UnityEngine;

namespace SLA.Core
{
    /// <summary>
    /// Owns the current <see cref="GameState"/> (GDD Technical architecture → Game states). Follows the player's
    /// travel, the gate clear and the daily quest; pauses on request; and asks for a save only at the exits the
    /// GDD lists (Results → Hub, DailyQuest → Hub), never mid-gate. Pausing stops AI and timers, never head tracking.
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        [Header("Areas")]
        [SerializeField] private int _titleAreaId = 2;
        [SerializeField] private int _hubAreaId;
        [SerializeField] private int _tutorialAreaId = 3;

        [Header("Channels in")]
        [SerializeField] private TravelRequestChannel _arrived;
        [SerializeField] private VoidEventChannel _onGateCleared;
        [SerializeField] private DailyQuestStatusChannel _dailyQuest;
        [SerializeField] private VoidEventChannel _pauseToggle;

        [Header("Channels out")]
        [SerializeField] private GameStateChannel _state;
        [SerializeField] private VoidEventChannel _saveRequested;

        private GameState _beforePause;

        public GameState Current { get; private set; } = GameState.Boot;

        private void OnEnable()
        {
            _arrived.Raised += OnArrived;
            _onGateCleared.Raised += OnGateCleared;
            _dailyQuest.Raised += OnDailyQuest;
            _pauseToggle.Raised += TogglePause;
        }

        private void OnDisable()
        {
            _arrived.Raised -= OnArrived;
            _onGateCleared.Raised -= OnGateCleared;
            _dailyQuest.Raised -= OnDailyQuest;
            _pauseToggle.Raised -= TogglePause;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            // Boot (save loaded by the game flow) → Title. First-run setup comes with height calibration.
            Set(GameState.Title);
        }

        private void OnArrived(TravelRequest request)
        {
            GameState next;
            if (request.AreaId == _titleAreaId)
            {
                next = GameState.Title;
            }
            else if (request.AreaId == _hubAreaId)
            {
                next = GameState.Hub;
            }
            else if (request.AreaId == _tutorialAreaId)
            {
                next = GameState.Tutorial;
            }
            else
            {
                next = GameState.Gate;
            }

            // Respawns and moves inside a gate keep the Results state until the player leaves.
            if (Current == GameState.Results && next == GameState.Gate)
            {
                return;
            }
            if (Current == GameState.Results && next == GameState.Hub)
            {
                _saveRequested.Raise(); // GDD: Results saves XP, loot and flags on exit.
            }
            Set(next);
        }

        private void OnGateCleared()
        {
            if (Current == GameState.Gate)
            {
                Set(GameState.Results);
            }
        }

        private void OnDailyQuest(DailyQuestStatus status)
        {
            bool active = status.State == DailyQuestState.Calibrating || status.State == DailyQuestState.InProgress;
            if (active && Current == GameState.Hub)
            {
                Set(GameState.DailyQuest);
            }
            else if (!active && Current == GameState.DailyQuest)
            {
                if (status.State == DailyQuestState.Complete)
                {
                    _saveRequested.Raise(); // GDD: the daily quest saves rewards and streak on exit.
                }
                Set(GameState.Hub);
            }
        }

        private void TogglePause()
        {
            if (Current == GameState.Paused)
            {
                Time.timeScale = 1f;
                Set(_beforePause);
                return;
            }
            if (Current == GameState.Boot || Current == GameState.Title || Current == GameState.FirstRunSetup)
            {
                return;
            }
            _beforePause = Current;
            Time.timeScale = 0f;
            Set(GameState.Paused);
        }

        /// <summary>The state the game is in, or was in before pausing.</summary>
        public GameState Underlying => Current == GameState.Paused ? _beforePause : Current;

        private void Set(GameState state)
        {
            Current = state;
            _state.Raise(state);
        }

        private void OnApplicationPause(bool paused)
        {
            // Taking the headset off can end the app without a quit; keep home progress safe.
            if (paused && Underlying == GameState.Hub)
            {
                _saveRequested.Raise();
            }
        }
    }
}
