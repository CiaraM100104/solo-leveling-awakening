using NUnit.Framework;
using SLA.Data;
using SLA.Progression;
using UnityEngine;

namespace SLA.Tests
{
    public class ProgressionTests
    {
        private ProgressionConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProgressionConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void PRG_1_XpToNextFollowsCurve()
        {
            Assert.AreEqual(50, XpCurve.XpToNext(1, _config));
            Assert.AreEqual(152, XpCurve.XpToNext(2, _config));
            Assert.AreEqual(1991, XpCurve.XpToNext(10, _config)); // 50 × 10^1.6 = 1990.54
        }

        [Test]
        public void PRG_1_XpStopsAtLevelCap()
        {
            var stats = new PlayerStats(_config);
            stats.AddXp(int.MaxValue / 2);
            Assert.AreEqual(50, stats.Level);
            Assert.AreEqual(0, stats.Xp);
            Assert.AreEqual(0, stats.AddXp(1000));
            Assert.AreEqual(0, stats.Xp);
        }

        [Test]
        public void PRG_1_LeftoverXpCarriesOverAndCanGainSeveralLevels()
        {
            var stats = new PlayerStats(_config);
            int gained = stats.AddXp(50 + 152 + 10);
            Assert.AreEqual(2, gained);
            Assert.AreEqual(3, stats.Level);
            Assert.AreEqual(10, stats.Xp);
        }

        [Test]
        public void PRG_2_EnemyXpUsesGateRankMultiplier()
        {
            Assert.AreEqual(20, XpCurve.EnemyXp(20, HunterRank.E, _config));
            Assert.AreEqual(60, XpCurve.EnemyXp(20, HunterRank.D, _config));
            Assert.AreEqual(2000, XpCurve.EnemyXp(20, HunterRank.S, _config));
        }

        [Test]
        public void PRG_3_EachLevelGrantsFiveStatPoints()
        {
            var stats = new PlayerStats(_config);
            stats.AddXp(50);
            Assert.AreEqual(2, stats.Level);
            Assert.AreEqual(5, stats.UnspentStatPoints);
        }

        [Test]
        public void PRG_4_SkillPointOnlyForLevelsAboveTen()
        {
            var stats = new PlayerStats(_config);
            int toTen = 0;
            for (int level = 1; level < 10; level++)
            {
                toTen += XpCurve.XpToNext(level, _config);
            }
            stats.AddXp(toTen);
            Assert.AreEqual(10, stats.Level);
            Assert.AreEqual(0, stats.SkillPoints);

            stats.AddXp(XpCurve.XpToNext(10, _config));
            Assert.AreEqual(11, stats.Level);
            Assert.AreEqual(1, stats.SkillPoints);
        }

        [Test]
        public void PRG_6_VitalityAddsTenMaxHpPerPoint()
        {
            var stats = new PlayerStats(_config);
            Assert.AreEqual(100, stats.MaxHp);
            stats.AddXp(50);
            Assert.IsTrue(stats.TryAllocate(Stat.Vitality));
            Assert.AreEqual(110, stats.MaxHp);
        }

        [Test]
        public void PRG_9_IntelligenceAddsTwoMaxMpPerPoint()
        {
            var stats = new PlayerStats(_config);
            Assert.AreEqual(20, stats.MaxMp);
            stats.AddXp(50);
            stats.TryAllocate(Stat.Intelligence);
            Assert.AreEqual(22, stats.MaxMp);
        }

        [Test]
        public void PRG_11_CannotAllocateWithoutPoints()
        {
            var stats = new PlayerStats(_config);
            Assert.IsFalse(stats.TryAllocate(Stat.Strength));
            Assert.AreEqual(10, stats.Get(Stat.Strength));
        }

        [Test]
        public void PRG_12_RankForLevel()
        {
            Assert.AreEqual(HunterRank.E, XpCurve.RankForLevel(1));
            Assert.AreEqual(HunterRank.E, XpCurve.RankForLevel(9));
            Assert.AreEqual(HunterRank.D, XpCurve.RankForLevel(10));
            Assert.AreEqual(HunterRank.A, XpCurve.RankForLevel(49));
            Assert.AreEqual(HunterRank.S, XpCurve.RankForLevel(50));
        }

        [Test]
        public void PRG_13_LevelsAloneNeverChangeRank()
        {
            var stats = new PlayerStats(_config);
            stats.AddXp(100000);
            Assert.AreEqual(HunterRank.E, stats.Rank);
        }
    }
}
