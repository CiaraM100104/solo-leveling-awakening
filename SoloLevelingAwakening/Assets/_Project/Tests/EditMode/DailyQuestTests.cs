using NUnit.Framework;
using SLA.Data;
using SLA.Exercise;
using SLA.Quests;
using UnityEngine;

namespace SLA.Tests
{
    public class DailyQuestTests
    {
        private const float Standing = 1.7f;
        private const float Step = 1f / 72f;

        private DailyQuestConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<DailyQuestConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        private SquatCounter Calibrated()
        {
            var counter = new SquatCounter(_config.squatDownFraction, _config.squatUpFraction,
                _config.maxPitchDownDegrees, _config.maxDriftMeters, _config.minRepSeconds);
            counter.BeginCalibration();
            for (int i = 0; i < 144; i++)
            {
                counter.AddCalibrationSample(Standing);
            }
            counter.EndCalibration(Vector2.zero);
            return counter;
        }

        /// <summary>Plays a squat down to <paramref name="bottomFraction"/> × H₀ and back over the given time.</summary>
        private static int Squat(SquatCounter counter, ref float t, float seconds, float bottomFraction,
            Vector2 spot = default, float pitch = 0f)
        {
            int reps = 0;
            int frames = Mathf.CeilToInt(seconds / Step);
            for (int i = 0; i <= frames; i++)
            {
                float phase = i / (float)frames;
                float depth = Mathf.Sin(phase * Mathf.PI);
                float height = Mathf.Lerp(Standing, Standing * bottomFraction, depth);
                if (counter.Tick(t, height, spot, pitch))
                {
                    reps++;
                }
                t += Step;
            }
            return reps;
        }

        [Test]
        public void QST_8_CalibrationAveragesStandingHeight()
        {
            Assert.AreEqual(Standing, Calibrated().StandingHeight, 0.0001f);
        }

        [Test]
        public void QST_9_FullSquatCountsOnce()
        {
            var counter = Calibrated();
            float t = 0f;
            Assert.AreEqual(1, Squat(counter, ref t, 1.5f, 0.6f));
            Assert.AreEqual(1, Squat(counter, ref t, 1.5f, 0.6f));
        }

        [Test]
        public void QST_9_ShallowDipDoesNotCount()
        {
            var counter = Calibrated();
            float t = 0f;
            Assert.AreEqual(0, Squat(counter, ref t, 1.5f, 0.85f));
        }

        [Test]
        public void QST_10_LookingDownTooFarRejectsRep()
        {
            var counter = Calibrated();
            float t = 0f;
            Assert.AreEqual(0, Squat(counter, ref t, 1.5f, 0.6f, pitch: 60f));
        }

        [Test]
        public void QST_10_WalkingAwayRejectsRep()
        {
            var counter = Calibrated();
            float t = 0f;
            Assert.AreEqual(0, Squat(counter, ref t, 1.5f, 0.6f, spot: new Vector2(0.5f, 0f)));
        }

        [Test]
        public void QST_11_TooFastRejectsRep()
        {
            var counter = Calibrated();
            float t = 0f;
            Assert.AreEqual(0, Squat(counter, ref t, 0.5f, 0.6f));
        }

        [Test]
        public void QST_13_PunchNeedsSpeedAwayFromHead()
        {
            var head = new Vector3(0f, 1.7f, 0f);
            var hand = new Vector3(0f, 1.5f, 0.5f);
            Assert.IsTrue(PunchDetector.IsValidPunch(new Vector3(0f, 0f, 3f), hand, head, 2.5f));
            Assert.IsFalse(PunchDetector.IsValidPunch(new Vector3(0f, 0f, 2f), hand, head, 2.5f), "too slow");
            Assert.IsFalse(PunchDetector.IsValidPunch(new Vector3(0f, 0f, -3f), hand, head, 2.5f), "pulling back");
        }

        [Test]
        public void QST_5_RepsByLevelBand()
        {
            DailyQuestRules.RepsForLevel(1, _config, out int squats, out int punches);
            Assert.AreEqual(20, squats);
            Assert.AreEqual(100, punches);
            DailyQuestRules.RepsForLevel(19, _config, out squats, out punches);
            Assert.AreEqual(30, squats);
            Assert.AreEqual(150, punches);
            DailyQuestRules.RepsForLevel(45, _config, out squats, out punches);
            Assert.AreEqual(50, squats);
            Assert.AreEqual(250, punches);
        }

        [Test]
        public void QST_6_RoundsAlternateAndSplitReps()
        {
            Assert.IsTrue(DailyQuestRules.IsSquatRound(0));
            Assert.IsFalse(DailyQuestRules.IsSquatRound(1));
            Assert.IsTrue(DailyQuestRules.IsSquatRound(2));
            Assert.AreEqual(10, DailyQuestRules.RepsInRound(0, 20, 2));
            Assert.AreEqual(10, DailyQuestRules.RepsInRound(2, 20, 2));
            Assert.AreEqual(33, DailyQuestRules.RepsInRound(1, 100, 3));
            Assert.AreEqual(34, DailyQuestRules.RepsInRound(5, 100, 3));
        }
    }
}
