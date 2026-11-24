using UnityEngine;

namespace SLA.Core
{
    /// <summary>Where the daily quest is (QST-1).</summary>
    public enum DailyQuestState
    {
        Available,
        Calibrating,
        InProgress,
        Resting,
        Complete,
    }

    /// <summary>Progress of the daily quest, for its System window.</summary>
    public readonly struct DailyQuestStatus
    {
        public readonly DailyQuestState State;
        /// <summary>Zero-based round index (QST-6).</summary>
        public readonly int Round;
        public readonly int RoundCount;
        /// <summary>True when the current round is squats, false for punches.</summary>
        public readonly bool IsSquatRound;
        public readonly int SquatsDone;
        public readonly int SquatsTarget;
        public readonly int PunchesDone;
        public readonly int PunchesTarget;

        public DailyQuestStatus(DailyQuestState state, int round, int roundCount, bool isSquatRound,
            int squatsDone, int squatsTarget, int punchesDone, int punchesTarget)
        {
            State = state;
            Round = round;
            RoundCount = roundCount;
            IsSquatRound = isSquatRound;
            SquatsDone = squatsDone;
            SquatsTarget = squatsTarget;
            PunchesDone = punchesDone;
            PunchesTarget = punchesTarget;
        }
    }

    /// <summary>Carries <see cref="DailyQuestStatus"/>.</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Daily Quest Status Channel")]
    public class DailyQuestStatusChannel : EventChannel<DailyQuestStatus>
    {
    }
}
