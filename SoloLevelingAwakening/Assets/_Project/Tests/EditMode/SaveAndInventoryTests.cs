using System.Collections.Generic;
using NUnit.Framework;
using SLA.Core;
using SLA.Data;
using SLA.Gates;
using SLA.Items;
using SLA.Progression;
using UnityEngine;

namespace SLA.Tests
{
    public class SaveAndInventoryTests
    {
        // The GDD's Save data example, verbatim.
        private const string GddExample = @"{
  ""version"": 1,
  ""player"": { ""level"": 14, ""xp"": 820, ""rank"": ""D"", ""job"": ""None"", ""title"": ""Survivor of the Double Dungeon"",
              ""stats"": { ""str"": 22, ""vit"": 18, ""agi"": 15, ""per"": 12, ""int"": 13 }, ""unspent"": 5 },
  ""inventory"": [ { ""id"": ""kasaka_venom_fang"", ""count"": 1 }, { ""id"": ""health_potion"", ""count"": 3 } ],
  ""equipped"": { ""hipL"": ""kasaka_venom_fang"", ""hipR"": ""steel_dagger"", ""gloves"": ""lizardhide_gloves"" },
  ""gold"": 640,
  ""quests"": { ""completed"": [""prologue"", ""goblin_warren""], ""active"": ""kasaka_nest"", ""dailyStreak"": 3 },
  ""settings"": { ""comfortPreset"": ""Recommended"", ""uiMode"": ""Diegetic"", ""leftHanded"": false }
}";

        [Test]
        public void Save_ReadsTheGddExample()
        {
            Assert.IsTrue(SaveCodec.TryFromJson(GddExample, out var data, out string error), error);
            Assert.AreEqual(14, data.player.level);
            Assert.AreEqual(13, data.player.stats.@int);
            Assert.AreEqual(2, data.inventory.Count);
            Assert.AreEqual(3, data.inventory[1].count);
            Assert.AreEqual(640, data.gold);
            Assert.AreEqual("kasaka_nest", data.quests.active);
            Assert.AreEqual(3, data.quests.dailyStreak);
            Assert.IsNotNull(data.records, "records added after the GDD example are repaired to empty");
        }

        [Test]
        public void Save_RoundTripsEverything()
        {
            var original = new SaveData { gold = 77 };
            original.player.level = 5;
            original.player.stats.str = 15;
            original.inventory.Add(new ItemStack("health_potion", 2));
            original.quests.completed.Add("tutorial");
            original.settings.comfortPreset = "Comfortable";
            original.records.Add(new RunRecord { gateId = "goblin_warren", score = 1234, timeSeconds = 222.5f });

            Assert.IsTrue(SaveCodec.TryFromJson(SaveCodec.ToJson(original), out var copy, out _));
            Assert.AreEqual(5, copy.player.level);
            Assert.AreEqual(15, copy.player.stats.str);
            Assert.AreEqual("health_potion", copy.inventory[0].id);
            Assert.AreEqual("tutorial", copy.quests.completed[0]);
            Assert.AreEqual("Comfortable", copy.settings.comfortPreset);
            Assert.AreEqual(1234, copy.records[0].score);
            Assert.AreEqual(77, copy.gold);
        }

        [Test]
        public void Save_RejectsNewerVersionsAndGarbage()
        {
            Assert.IsFalse(SaveCodec.TryFromJson("{\"version\": 99}", out _, out _));
            Assert.IsFalse(SaveCodec.TryFromJson("not json", out _, out _));
            Assert.IsFalse(SaveCodec.TryFromJson("", out _, out _));
        }

        [Test]
        public void Save_RepairsBadValues()
        {
            Assert.IsTrue(SaveCodec.TryFromJson("{\"player\": {\"level\": -3}, \"gold\": -50}", out var data, out _));
            Assert.AreEqual(1, data.player.level);
            Assert.AreEqual(0, data.gold);
        }

        private static Inventory Make(int slots = 20) => new Inventory(slots, id => id == "health_potion" ? 10 : 1);

        [Test]
        public void ITM_9_InventoryHasTwentySlotsAndStacks()
        {
            var inventory = Make();
            Assert.AreEqual(20, inventory.SlotCount);
            Assert.AreEqual(0, inventory.Add("health_potion", 12));
            Assert.AreEqual(12, inventory.Count("health_potion"));
            Assert.AreEqual(10, inventory.Slots[0].count, "fills a stack before opening a new slot");
            Assert.AreEqual(2, inventory.Slots[1].count);
        }

        [Test]
        public void ITM_9_FullInventoryReturnsWhatDidNotFit()
        {
            var inventory = Make(2);
            Assert.AreEqual(0, inventory.Add("sword", 1));
            Assert.AreEqual(0, inventory.Add("health_potion", 5));
            Assert.AreEqual(0, inventory.SpaceFor("sword"));
            Assert.AreEqual(1, inventory.Add("sword", 1));
            Assert.AreEqual(5, inventory.SpaceFor("health_potion"));
        }

        [Test]
        public void ITM_InventoryRemoveIsAllOrNothing()
        {
            var inventory = Make();
            inventory.Add("health_potion", 3);
            Assert.IsFalse(inventory.TryRemove("health_potion", 4));
            Assert.AreEqual(3, inventory.Count("health_potion"));
            Assert.IsTrue(inventory.TryRemove("health_potion", 3));
            Assert.IsTrue(inventory.Slots[0].IsEmpty, "an emptied stack frees its slot");
        }

        [Test]
        public void ITM_GoldCannotGoNegative()
        {
            var inventory = Make();
            inventory.AddGold(40);
            Assert.IsFalse(inventory.TrySpend(50));
            Assert.IsTrue(inventory.TrySpend(40));
            Assert.AreEqual(0, inventory.Gold);
        }

        [Test]
        public void ITM_InventoryLoadsFromSave()
        {
            var inventory = Make();
            int lost = inventory.Load(new[] { new ItemStack("health_potion", 3), new ItemStack("sword", 1) }, 120);
            Assert.AreEqual(0, lost);
            Assert.AreEqual(3, inventory.Count("health_potion"));
            Assert.AreEqual(120, inventory.Gold);
        }

        [Test]
        public void ITM_PotionDropChanceByRankAndNeverFromBosses()
        {
            var economy = ScriptableObject.CreateInstance<EconomyConfig>();
            var goblin = ScriptableObject.CreateInstance<EnemyData>();
            Assert.IsTrue(Loot.DropsPotion(goblin, HunterRank.E, economy, 0.07f));
            Assert.IsFalse(Loot.DropsPotion(goblin, HunterRank.E, economy, 0.09f));
            Assert.IsTrue(Loot.DropsPotion(goblin, HunterRank.B, economy, 0.14f));
            goblin.isBoss = true;
            Assert.IsFalse(Loot.DropsPotion(goblin, HunterRank.E, economy, 0f));
            Object.DestroyImmediate(economy);
            Object.DestroyImmediate(goblin);
        }

        [Test]
        public void Score_RewardsKillsSpeedAndNoDamage()
        {
            var config = ScriptableObject.CreateInstance<ScoreConfig>();
            var run = new RunRecord { kills = 10, xp = 300, gold = 100, timeSeconds = 500f, damageTaken = 0, deaths = 0 };
            Assert.AreEqual(1000 + 300 + 200 + 500 + 1000, Scoring.Score(run, config));
            run.damageTaken = 20;
            run.deaths = 1;
            Assert.AreEqual(1000 + 300 + 200 + 500 - 500, Scoring.Score(run, config));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Score_BoardKeepsBestFirstAndTrims()
        {
            var board = new List<RunRecord>();
            Assert.AreEqual(1, Scoring.Insert(board, new RunRecord { score = 100 }, 3));
            Assert.AreEqual(1, Scoring.Insert(board, new RunRecord { score = 300 }, 3));
            Assert.AreEqual(2, Scoring.Insert(board, new RunRecord { score = 200 }, 3));
            Assert.AreEqual(0, Scoring.Insert(board, new RunRecord { score = 50 }, 3), "too low for a full board");
            Assert.AreEqual(1, Scoring.Insert(board, new RunRecord { score = 400 }, 3));
            Assert.AreEqual(3, board.Count);
            Assert.AreEqual(400, board[0].score);
            Assert.AreEqual(200, board[2].score);
        }

        [Test]
        public void PRG_RestoreClampsSavedProgress()
        {
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            var stats = new PlayerStats(config);
            stats.Restore(14, 820, 5, HunterRank.D, new[] { 22, 18, 15, 12, 13 });
            Assert.AreEqual(14, stats.Level);
            Assert.AreEqual(820, stats.Xp);
            Assert.AreEqual(5, stats.UnspentStatPoints);
            Assert.AreEqual(4, stats.SkillPoints, "PRG-4: levels 11 to 14");
            Assert.AreEqual(22, stats.Get(Stat.Strength));
            Assert.AreEqual(180, stats.MaxHp, "PRG-6: 100 + 10 per Vitality point above 10");

            stats.Restore(99, 5, -1, HunterRank.S, new[] { 1, 1, 1, 1, 1 });
            Assert.AreEqual(50, stats.Level);
            Assert.AreEqual(0, stats.Xp);
            Assert.AreEqual(10, stats.Get(Stat.Vitality), "stats never below the base");
            Object.DestroyImmediate(config);
        }
    }
}
