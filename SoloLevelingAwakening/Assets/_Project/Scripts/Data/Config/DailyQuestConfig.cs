using UnityEngine;

namespace SLA.Data
{
    /// <summary>
    /// Daily quest numbers (docs/design/quests-and-exercise.md, QST-1 and QST-5 to QST-13).
    /// Fields marked PLACEHOLDER are not in the spec yet.
    /// </summary>
    [CreateAssetMenu(menuName = "SLA/Config/Daily Quest Config")]
    public class DailyQuestConfig : ScriptableObject
    {
        [Header("Reps by level (QST-5)")]
        [Tooltip("Lowest level of each band.")]
        public int[] bandMinLevel = { 1, 10, 20, 30 };
        public int[] squatsPerBand = { 20, 30, 40, 50 };
        public int[] punchesPerBand = { 100, 150, 200, 250 };

        [Header("Rounds (QST-6)")]
        [Tooltip("PLACEHOLDER: rounds of each exercise; rounds alternate squats and punches.")]
        public int roundsPerExercise = 2;

        [Header("Reward (QST-1)")]
        public int statPointReward = 3;

        [Header("Squat detection (QST-8 to QST-11)")]
        public float calibrationSeconds = 2f;
        [Range(0.7f, 0.85f)] public float squatDownFraction = 0.8f;
        public float squatUpFraction = 0.95f;
        public float maxPitchDownDegrees = 45f;
        public float maxDriftMeters = 0.3f;
        public float minRepSeconds = 0.8f;

        [Header("Punch detection (QST-13)")]
        public float punchMinSpeed = 2.5f;
        [Tooltip("PLACEHOLDER: how close a fist must come to a pad's centre.")]
        public float padRadiusMeters = 0.14f;
        [Tooltip("PLACEHOLDER: a pad ignores further hits for this long.")]
        public float padCooldownSeconds = 0.25f;
        [Tooltip("PLACEHOLDER: pad positions (right, up, forward) from the calibrated head. Jab, cross, hook, uppercut.")]
        public Vector3[] padOffsets =
        {
            new Vector3(-0.15f, -0.15f, 0.5f),
            new Vector3(0.15f, -0.15f, 0.5f),
            new Vector3(-0.4f, -0.2f, 0.35f),
            new Vector3(0.2f, -0.45f, 0.4f),
        };
    }
}
