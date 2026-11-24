using UnityEngine;

namespace SLA.Core
{
    /// <summary>Title screen choices, sent as ints on the title command channel.</summary>
    public enum TitleCommand
    {
        Continue = 0,
        NewGame = 1,
        Tutorial = 2,
        Quit = 3,
    }

    /// <summary>
    /// Boot loads the save (screen flow: "Boot: logo, load save"); the title menu's choices, "End the day"
    /// (MRH-10) and "Return to Title" move the player between the title, the tutorial and the hub.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        [SerializeField] private SaveService _save;
        [SerializeField] private GameStateManager _states;

        [Header("Places")]
        [SerializeField] private Transform _titleSpawn;
        [SerializeField] private int _titleAreaId = 2;
        [SerializeField] private Transform _hubSpawn;
        [SerializeField] private int _hubAreaId;
        [SerializeField] private Transform _tutorialSpawn;
        [SerializeField] private int _tutorialAreaId = 3;

        [Header("Channels in")]
        [Tooltip("A TitleCommand as an int.")]
        [SerializeField] private IntEventChannel _titleCommand;
        [SerializeField] private VoidEventChannel _endDay;
        [SerializeField] private VoidEventChannel _returnToTitle;

        [Header("Channels out")]
        [SerializeField] private VoidEventChannel _newGameStarted;
        [SerializeField] private VoidEventChannel _saveRequested;
        [SerializeField] private VoidEventChannel _pauseToggle;
        [SerializeField] private TravelRequestChannel _requests;
        [SerializeField] private SystemMessageChannel _messages;

        private void OnEnable()
        {
            _titleCommand.Raised += OnTitleCommand;
            _endDay.Raised += OnEndDay;
            _returnToTitle.Raised += OnReturnToTitle;
        }

        private void OnDisable()
        {
            _titleCommand.Raised -= OnTitleCommand;
            _endDay.Raised -= OnEndDay;
            _returnToTitle.Raised -= OnReturnToTitle;
        }

        private void Start()
        {
            if (_save.HasSave && !_save.Load())
            {
                _messages.Raise(new SystemMessage(SystemMessageKind.Warning, "The save could not be read. Start a new game."));
            }
        }

        private void OnTitleCommand(int command)
        {
            switch ((TitleCommand)command)
            {
                case TitleCommand.Continue:
                    if (_save.HasSave)
                    {
                        Go(_hubSpawn, _hubAreaId);
                    }
                    break;

                case TitleCommand.NewGame:
                    _newGameStarted.Raise();
                    _saveRequested.Raise();
                    Go(_hubSpawn, _hubAreaId);
                    break;

                case TitleCommand.Tutorial:
                    Go(_tutorialSpawn, _tutorialAreaId);
                    break;

                case TitleCommand.Quit:
                    // Nothing to save: every way back to the title already saved where the GDD allows.
                    Application.Quit();
#if UNITY_EDITOR
                    Debug.Log("[SLA] Quit requested (ignored in the editor).");
#endif
                    break;
            }
        }

        private void OnEndDay()
        {
            // MRH-10: "End the day" saves and closes the session.
            _saveRequested.Raise();
            _messages.Raise(new SystemMessage(SystemMessageKind.Info, "Progress saved. Rest well, Player."));
            Go(_titleSpawn, _titleAreaId);
        }

        private void OnReturnToTitle()
        {
            var state = _states.Underlying;
            if (_states.Current == GameState.Paused)
            {
                _pauseToggle.Raise();
            }
            // Saves only where the GDD allows; leaving a gate early keeps the last save.
            if (state == GameState.Hub || state == GameState.DailyQuest)
            {
                _saveRequested.Raise();
            }
            Go(_titleSpawn, _titleAreaId);
        }

        private void Go(Transform spawn, int areaId)
        {
            _requests.Raise(new TravelRequest(spawn, areaId, false));
        }
    }
}
