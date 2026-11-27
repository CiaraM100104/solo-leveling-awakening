using NUnit.Framework;
using SLA.Core.Save;
using SLA.Data;
using SLA.Progression;
using UnityEngine;

namespace SLA.Tests
{
    public class CombatLoopTests
    {
        private ProgressionConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<ProgressionConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void PlayerStatsSave_RoundTripsEveryField() // save format: docs/ARCHITECTURE.md (no rule ID)
        {
            var save = new PlayerSave { level = 7, xp = 123, unspent = 4, skillPoints = 0 };
            save.stats.str = 14;
            save.stats.vit = 12;
            save.stats.agi = 11;
            save.stats.per = 13;
            save.stats.@int = 15;

            var copy = new PlayerSave();
            PlayerStatsSave.ToSave(PlayerStatsSave.FromSave(save), copy);

            Assert.AreEqual(JsonUtility.ToJson(save), JsonUtility.ToJson(copy));
        }

        [Test]
        public void PRG2_FiveEGoblinKills_LevelANewPlayerOnce()
        {
            var save = new PlayerSave();
            int gained = 0;
            for (int i = 0; i < 5; i++)
            {
                var stats = PlayerStatsSave.FromSave(save);
                gained += stats.AddXp(XpCurve.EnemyXp(20, Rank.E, _config), _config);
                PlayerStatsSave.ToSave(stats, save);
            }

            Assert.AreEqual(1, gained);
            Assert.AreEqual(2, save.level);
            Assert.AreEqual(50, save.xp); // 100 earned - 50 for level 2
            Assert.AreEqual(5, save.unspent); // PRG-3
        }

        [TestCase(10, 100)]
        [TestCase(15, 150)]
        [TestCase(8, 100)]
        public void PRG6_MaxHp_GrowsWithVitalityAboveTheBase(int vitality, int expected)
        {
            Assert.AreEqual(expected, _config.MaxHp(vitality));
        }
    }
}
