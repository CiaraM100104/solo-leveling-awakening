using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SLA.AI;
using SLA.Core;
using SLA.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SLA.Tests
{
    /// <summary>
    /// Plays the whole barebones game without a headset: title → new game → daily quest → shop and potion →
    /// gate run → boss → results and scores → save on the way home → pause and comfort → end the day →
    /// load → tutorial. Uses a throwaway save file.
    /// </summary>
    public class SandboxSmokeTest
    {
        private const float Kill = 99999f;
        private const string TestSave = "smoke_test_save.json";
        private readonly List<string> _ourErrors = new List<string>();

        private void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception) && (stack.Contains("SLA.") || message.Contains("SLA.")))
            {
                _ourErrors.Add(message + "\n" + stack);
            }
        }

        private static T Channel<T>(string name) where T : ScriptableObject =>
            Resources.FindObjectsOfTypeAll<T>().First(c => c.name == name);

        private static Transform Place(string name) => GameObject.Find(name).transform;

        private static void KillAlive(EnemyDirector director)
        {
            foreach (var enemy in director.Active.Where(e => !e.IsDead).ToList())
            {
                if (enemy.gameObject.activeSelf && !enemy.IsDead)
                {
                    enemy.Health.TakeDamage(Kill, enemy.transform.position);
                }
            }
        }

        private static int Alive(EnemyDirector director) => director.Active.Count(e => !e.IsDead);

        [UnityTest]
        public IEnumerator Game_FullFlowFromTitleToGateAndBack()
        {
            LogAssert.ignoreFailingMessages = true; // XR has no headset here; only our own errors matter.
            Application.logMessageReceived += OnLog;
            SceneManager.LoadScene("Sandbox_Barebones");
            yield return null;
            yield return null;

            var save = Object.FindAnyObjectByType<SaveService>();
            save.FileName = TestSave;
            save.DeleteSave();
            var states = Object.FindAnyObjectByType<GameStateManager>();
            var quest = Object.FindAnyObjectByType<DailyQuest>();
            var tutorial = Object.FindAnyObjectByType<TutorialSequence>();
            var director = GameObject.Find("Gate_GoblinWarren").GetComponentInChildren<EnemyDirector>();
            var player = Object.FindAnyObjectByType<SLA.Progression.PlayerProgression>().GetComponent<SLA.Combat.Health>();

            var status = Channel<PlayerStatusChannel>("PlayerStatus");
            var inventory = Channel<InventoryStatusChannel>("Inventory");
            var requests = Channel<TravelRequestChannel>("RequestTravel");
            var title = Channel<IntEventChannel>("TitleCommand");

            // Boot → Title.
            Assert.AreEqual(GameState.Title, states.Current);

            // New Game → Hub, fresh profile, save written.
            title.Raise((int)TitleCommand.NewGame);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(GameState.Hub, states.Current);
            Assert.AreEqual(1, status.Last.Level);
            Assert.AreEqual(0, inventory.Last.Gold);
            Assert.IsTrue(save.HasSave, "new game saves");

            // QST-1 daily quest; PRG-11 spend a point.
            quest.CompleteNow();
            yield return null;
            Assert.AreEqual(3, status.Last.UnspentStatPoints);
            Channel<IntEventChannel>("OnAllocateStat").Raise(0);
            yield return null;
            Assert.AreEqual(11, status.Last.Strength);

            // Shop: buy a health potion; ITM-4: drink it.
            Channel<IntEventChannel>("OnGoldGained").Raise(200);
            Channel<IntEventChannel>("BuyRequested").Raise(0);
            yield return null;
            Assert.AreEqual(1, inventory.Last.Count("health_potion"), "bought a potion");
            Assert.AreEqual(150, inventory.Last.Gold);
            player.TakeDamage(60f, Vector3.zero);
            int hurt = player.Current;
            Channel<StringEventChannel>("UseItemRequested").Raise("health_potion");
            yield return new WaitForSeconds(2.3f);
            Assert.AreEqual(0, inventory.Last.Count("health_potion"), "potion used up");
            Assert.AreEqual(Mathf.Min(player.Max, hurt + 50), player.Current, "+50 HP over 2 s");

            // Gate run: room 1, then the boss.
            player.RestoreFull();
            requests.Raise(new TravelRequest(Place("GateEntrance_Spawn"), 1, false));
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(GameState.Gate, states.Current);
            requests.Raise(new TravelRequest(Place("Checkpoint_Room1"), 1, false));
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(2, Alive(director), "room 1 wave 1");
            yield return new WaitForSeconds(5f);
            Assert.Less(status.Last.Hp, status.Last.MaxHp, "goblins walk up and hit the player");
            KillAlive(director);
            yield return null;
            int sparks = GameObject.Find("HitSparks").GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount);
            Assert.Greater(sparks, 0, "CMB-20 cyan sparks at the hit point");
            yield return new WaitForSeconds(4.5f);
            Assert.AreEqual(4, Alive(director), "room 1 wave 2");
            KillAlive(director);
            yield return new WaitForSeconds(0.5f);

            requests.Raise(new TravelRequest(Place("Checkpoint_Boss"), 1, false));
            yield return new WaitForSeconds(3.5f);
            var boss = director.Active.First(e => e.Data.isBoss);
            boss.Health.TakeDamage(boss.Health.Max * 0.55f, boss.transform.position);
            yield return null;
            Assert.AreEqual(3, Alive(director), "ENM-7 phase 2 reinforcements");
            boss.Health.TakeDamage(Kill, boss.transform.position);
            yield return null;

            // Results and scores.
            Assert.AreEqual(GameState.Results, states.Current);
            var results = Channel<RunRecordChannel>("ResultsReady").Last;
            Assert.IsNotNull(results);
            Assert.AreEqual(7, results.kills, "6 in room 1 + the boss (reinforcements vanish with it)");
            Assert.Greater(results.score, 0);
            Assert.AreEqual(1, results.placement);
            Assert.AreEqual(1, inventory.Last.Count("monster_core_e"), "boss drops a core");
            Debug.Log($"[SMOKE] run: {results.kills} kills, {results.xp} XP, {results.gold} G, {results.timeSeconds:0.0}s, damage {results.damageTaken}, score {results.score}");

            // Results → Hub saves.
            requests.Raise(new TravelRequest(Place("Hub_Spawn"), 0, false));
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(GameState.Hub, states.Current);
            Assert.IsTrue(SaveCodec.TryFromJson(File.ReadAllText(save.FilePath), out var saved, out _));
            Assert.AreEqual(1, saved.records.Count, "the run is on the saved scores board");
            Assert.AreEqual(inventory.Last.Gold, saved.gold);
            Assert.AreEqual(status.Last.Level, saved.player.level);
            Assert.Contains("goblin_warren", saved.quests.completed);

            // Shop: sell the core.
            int goldBefore = inventory.Last.Gold;
            Channel<VoidEventChannel>("SellRequested").Raise();
            yield return null;
            Assert.AreEqual(goldBefore + 20, inventory.Last.Gold);

            // Pause and comfort settings.
            Channel<VoidEventChannel>("PauseToggle").Raise();
            Assert.AreEqual(GameState.Paused, states.Current);
            Assert.AreEqual(0f, Time.timeScale);
            Channel<IntEventChannel>("SetComfortPreset").Raise(1);
            Assert.AreEqual("Comfortable", Channel<SettingsChannel>("Settings").Last.ComfortPreset);
            Channel<VoidEventChannel>("PauseToggle").Raise();
            Assert.AreEqual(GameState.Hub, states.Current);
            Assert.AreEqual(1f, Time.timeScale);

            // End the day → Title, saved; loading restores it.
            int level = status.Last.Level;
            int gold = inventory.Last.Gold;
            Channel<VoidEventChannel>("EndDay").Raise();
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(GameState.Title, states.Current);
            Assert.IsTrue(save.Load());
            yield return null;
            Assert.AreEqual(level, status.Last.Level);
            Assert.AreEqual(gold, inventory.Last.Gold);
            Assert.AreEqual("Comfortable", Channel<SettingsChannel>("Settings").Last.ComfortPreset);

            // Tutorial from the title.
            title.Raise((int)TitleCommand.Tutorial);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(GameState.Tutorial, states.Current);
            Assert.AreEqual(TutorialSequence.Step.Grab, tutorial.Current);
            tutorial.SkipStep();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(TutorialSequence.Step.Strike, tutorial.Current);
            tutorial.SkipStep();
            tutorial.SkipStep();
            tutorial.SkipStep();
            yield return null;
            Assert.AreEqual(TutorialSequence.Step.Done, tutorial.Current);
            Assert.IsTrue(GameObject.Find("Tutorial_TrainingGrounds").transform.Find("ExitPortal_Tutorial").gameObject.activeSelf);

            save.DeleteSave();
            Application.logMessageReceived -= OnLog;
            Assert.IsEmpty(_ourErrors, string.Join("\n---\n", _ourErrors));
        }
    }
}
