using System.Collections.Generic;
using SLA.Core;
using SLA.Data;
using UnityEngine;

namespace SLA.Gates
{
    /// <summary>Pure scoring for gate runs and the high-score board (PLACEHOLDER weights in <see cref="ScoreConfig"/>).</summary>
    public static class Scoring
    {
        public static int Score(RunRecord run, ScoreConfig config)
        {
            float underPar = Mathf.Max(0f, config.parSeconds - run.timeSeconds);
            int score = run.kills * config.pointsPerKill
                        + run.xp * config.pointsPerXp
                        + run.gold * config.pointsPerGold
                        + Mathf.FloorToInt(underPar) * config.pointsPerSecondUnderPar
                        - run.deaths * config.deathPenalty
                        + (run.damageTaken == 0 ? config.noDamageBonus : 0);
            return Mathf.Max(0, score);
        }

        /// <summary>
        /// Puts a run on the board (best first, ties keep the older run first) and trims it to size.
        /// Returns the run's placement, 1-based, or 0 if it didn't make the board.
        /// </summary>
        public static int Insert(List<RunRecord> board, RunRecord run, int boardSize)
        {
            int index = board.Count;
            for (int i = 0; i < board.Count; i++)
            {
                if (run.score > board[i].score)
                {
                    index = i;
                    break;
                }
            }
            if (index >= boardSize)
            {
                return 0;
            }
            board.Insert(index, run);
            if (board.Count > boardSize)
            {
                board.RemoveRange(boardSize, board.Count - boardSize);
            }
            return index + 1;
        }
    }
}
