using NUnit.Framework;
using SLA.Data;
using SLA.Progression;
using UnityEngine;

namespace SLA.Tests
{
    public class LootTests
    {
        private EconomyConfig _economy;
        private EnemyData _goblin;
        private ProgressionConfig _progression;

        [SetUp]
        public void SetUp()
        {
            _economy = ScriptableObject.CreateInstance<EconomyConfig>();
            _goblin = ScriptableObject.CreateInstance<EnemyData>();
            _progression = ScriptableObject.CreateInstance<ProgressionConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_economy);
            Object.DestroyImmediate(_goblin);
            Object.DestroyImmediate(_progression);
        }

        [Test]
        public void ITM_GoldPerRegularKillStaysInRankRange()
        {
            Assert.AreEqual(5, Loot.GoldForKill(_goblin, HunterRank.E, _economy, 0f));
            Assert.AreEqual(10, Loot.GoldForKill(_goblin, HunterRank.E, _economy, 0.9999f));
            Assert.AreEqual(10, Loot.GoldForKill(_goblin, HunterRank.E, _economy, 1f));
            Assert.AreEqual(15, Loot.GoldForKill(_goblin, HunterRank.D, _economy, 0f));
        }

        [Test]
        public void ITM_10_ARankAndSRankScaleFromB()
        {
            Assert.AreEqual(150, Loot.GoldForKill(_goblin, HunterRank.A, _economy, 0f));
            Assert.AreEqual(300, Loot.GoldForKill(_goblin, HunterRank.S, _economy, 1f));
        }

        [Test]
        public void ITM_FixedGoldOverridesRange()
        {
            _goblin.fixedGold = 30;
            Assert.AreEqual(30, Loot.GoldForKill(_goblin, HunterRank.E, _economy, 0.5f));
        }

        [Test]
        public void QST_1_DailyQuestStatPointsAddToUnspent()
        {
            var stats = new PlayerStats(_progression);
            stats.AddStatPoints(3);
            Assert.AreEqual(3, stats.UnspentStatPoints);
            stats.AddStatPoints(-2);
            Assert.AreEqual(3, stats.UnspentStatPoints);
        }
    }
}
