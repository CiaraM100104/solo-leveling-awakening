using SLA.Data;
using UnityEngine;

namespace SLA.Quests
{
    /// <summary>Pure daily quest rules (QST-5, QST-6).</summary>
    public static class DailyQuestRules
    {
        /// <summary>QST-5: squats and punches for the player's level band.</summary>
        public static void RepsForLevel(int level, DailyQuestConfig config, out int squats, out int punches)
        {
            int band = 0;
            for (int i = 0; i < config.bandMinLevel.Length; i++)
            {
                if (level >= config.bandMinLevel[i])
                {
                    band = i;
                }
            }
            squats = config.squatsPerBand[band];
            punches = config.punchesPerBand[band];
        }

        /// <summary>QST-6: rounds alternate, starting with squats.</summary>
        public static bool IsSquatRound(int round) => round % 2 == 0;

        /// <summary>
        /// Reps in one round: the exercise's total split evenly over its rounds; the last round takes the remainder.
        /// </summary>
        public static int RepsInRound(int round, int totalReps, int roundsPerExercise)
        {
            int rounds = Mathf.Max(1, roundsPerExercise);
            int index = round / 2;
            int perRound = totalReps / rounds;
            return index == rounds - 1 ? totalReps - perRound * (rounds - 1) : perRound;
        }
    }
}
