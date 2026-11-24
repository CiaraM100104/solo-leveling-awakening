using System.Collections.Generic;
using SLA.Core;
using SLA.Data;
using UnityEngine;

namespace SLA.Gates
{
    /// <summary>
    /// Local high scores: scores each finished gate run (PLACEHOLDER weights), keeps the best on a board,
    /// saves it, and hands the scored run to the Results screen. Survives New Game.
    /// </summary>
    public class ScoreBoard : MonoBehaviour
    {
        [SerializeField] private ScoreConfig _config;

        [Header("Channels")]
        [SerializeField] private RunRecordChannel _onRunCompleted;
        [Tooltip("The scored run, with its placement, for the Results screen.")]
        [SerializeField] private RunRecordChannel _onResultsReady;
        [SerializeField] private ScoresChannel _scores;
        [SerializeField] private SaveDataChannel _onCollectSave;
        [SerializeField] private SaveDataChannel _onApplySave;

        private readonly List<RunRecord> _board = new List<RunRecord>();

        private void OnEnable()
        {
            _onRunCompleted.Raised += OnRunCompleted;
            _onCollectSave.Raised += Collect;
            _onApplySave.Raised += Apply;
        }

        private void OnDisable()
        {
            _onRunCompleted.Raised -= OnRunCompleted;
            _onCollectSave.Raised -= Collect;
            _onApplySave.Raised -= Apply;
        }

        private void Start()
        {
            Publish();
        }

        private void OnRunCompleted(RunRecord run)
        {
            run.score = Scoring.Score(run, _config);
            run.placement = Scoring.Insert(_board, run, _config.boardSize);
            _onResultsReady.Raise(run);
            Publish();
        }

        private void Collect(SaveData save)
        {
            save.records.Clear();
            save.records.AddRange(_board);
        }

        private void Apply(SaveData save)
        {
            _board.Clear();
            foreach (var record in save.records)
            {
                Scoring.Insert(_board, record, _config.boardSize);
            }
            Publish();
        }

        private void Publish()
        {
            _scores.Raise(_board.ToArray());
        }
    }
}
