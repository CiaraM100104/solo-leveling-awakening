using System.Collections;
using System.Collections.Generic;
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
    /// Plays the barebones loop without a headset: daily quest reward, spending a point, entering the gate,
    /// a room's waves and loot, the boss's phase 2 and death, the exit portal, and the reset on going home.
    /// </summary>
    public class SandboxSmokeTest
    {
        private const float Kill = 99999f;
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

        private static RoomEncounter Room(string name) => GameObject.Find("Encounter_" + name).GetComponent<RoomEncounter>();

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
        public IEnumerator Sandbox_FullLoopFromHubThroughGateAndBack()
        {
            LogAssert.ignoreFailingMessages = true; // XR has no headset here; only our own errors matter.
            Application.logMessageReceived += OnLog;
            SceneManager.LoadScene("Sandbox_Barebones");
            yield return null;
            yield return null;

            var status = Channel<PlayerStatusChannel>("PlayerStatus");
            var requests = Channel<TravelRequestChannel>("RequestTravel");
            var allocate = Channel<IntEventChannel>("OnAllocateStat");
            var director = Object.FindAnyObjectByType<EnemyDirector>();
            var quest = Object.FindAnyObjectByType<DailyQuest>();
            var head = Camera.main != null ? Camera.main.transform : Object.FindAnyObjectByType<Camera>().transform;

            // Hub: QST-1 daily quest pays 3 stat points; PRG-11 spending one.
            Assert.AreEqual(DailyQuestState.Available, quest.State);
            quest.CompleteNow();
            yield return null;
            Assert.AreEqual(3, status.Last.UnspentStatPoints, "QST-1 reward");
            allocate.Raise(0);
            yield return null;
            Assert.AreEqual(11, status.Last.Strength, "PRG-11 spend a point");
            Assert.AreEqual(2, status.Last.UnspentStatPoints);

            // MRH-8: travel into the gate.
            var entrance = GameObject.Find("GateEntrance_Spawn").transform;
            requests.Raise(new TravelRequest(entrance, 1, false));
            yield return new WaitForSeconds(1.2f);
            var flat = new Vector2(head.position.x - entrance.position.x, head.position.z - entrance.position.z);
            Assert.Less(flat.magnitude, 0.05f, "player arrived at the gate entrance");

            // Room 1: walking in starts wave 1 (2 goblins), then wave 2 (4 kobolds); clearing opens the barrier.
            requests.Raise(new TravelRequest(GameObject.Find("Checkpoint_Room1").transform, 1, false));
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(2, Alive(director), "room 1 wave 1");
            float hpBefore = status.Last.Hp;
            yield return new WaitForSeconds(5f);
            Debug.Log($"[SMOKE] HP {hpBefore} -> {status.Last.Hp} after goblins close in");
            Assert.Less(status.Last.Hp, hpBefore, "goblins walk up and hit the player");
            KillAlive(director);
            yield return new WaitForSeconds(4.5f);
            Assert.AreEqual(4, Alive(director), "room 1 wave 2");
            KillAlive(director);
            yield return new WaitForSeconds(0.5f);
            var room1 = Room("Room1");
            Assert.IsTrue(room1.IsCleared, "room 1 cleared");
            Assert.IsFalse(GameObject.Find("Gate_GoblinWarren").transform.Find("Barrier_Room1").gameObject.activeSelf, "barrier open");
            Assert.GreaterOrEqual(status.Last.Gold, 6 * 5, "ITM gold per kill");
            Debug.Log($"[SMOKE] after room 1: Lv {status.Last.Level} XP {status.Last.Xp}/{status.Last.XpToNext} Gold {status.Last.Gold}");

            // Boss: ENM-7 phase 2 at 50% calls reinforcements; GTE-5 the exit portal opens.
            requests.Raise(new TravelRequest(GameObject.Find("Checkpoint_Boss").transform, 1, false));
            yield return new WaitForSeconds(3.5f);
            var boss = director.Active.FirstOrDefault(e => e.Data.isBoss);
            Assert.IsNotNull(boss, "boss spawned");
            boss.Health.TakeDamage(boss.Health.Max * 0.55f, boss.transform.position);
            yield return null;
            Assert.AreEqual(3, Alive(director), "boss + 2 reinforcements");
            boss.Health.TakeDamage(Kill, boss.transform.position);
            yield return null;
            Assert.AreEqual(0, Alive(director), "followers vanish with the boss");
            var exit = GameObject.Find("Gate_GoblinWarren").transform.Find("ExitPortal_Boss").gameObject;
            Assert.IsTrue(exit.activeSelf, "GTE-5 exit portal open");

            // Home again: the gate resets for a replay.
            requests.Raise(new TravelRequest(GameObject.Find("Hub_Spawn").transform, 0, false));
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(room1.IsCleared, "gate reset after leaving");
            Assert.IsFalse(exit.activeSelf);
            Debug.Log($"[SMOKE] end: Lv {status.Last.Level} XP {status.Last.Xp}/{status.Last.XpToNext} Gold {status.Last.Gold} HP {status.Last.Hp}/{status.Last.MaxHp}");

            Application.logMessageReceived -= OnLog;
            Assert.IsEmpty(_ourErrors, string.Join("\n---\n", _ourErrors));
        }
    }
}
