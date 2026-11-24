using System;
using SLA.AI;
using SLA.Core;
using SLA.Quests;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace SLA.DebugTools
{
    /// <summary>
    /// Editor-only shortcuts for testing without a headset. Does nothing in builds.
    /// F1 kill nearest enemy · F2 kill all enemies · F3 finish the daily quest · F4 +100 XP · F5 jump to the next checkpoint.
    /// </summary>
    public class DebugCheats : MonoBehaviour
    {
        [Serializable]
        private class Checkpoint
        {
            public Transform point;
            public int areaId;
        }

        private const int DebugXp = 100;
        private const float KillDamage = 99999f;

        [SerializeField] private Transform _head;
        [SerializeField] private EnemyDirector[] _directors = new EnemyDirector[0];
        [SerializeField] private DailyQuest _dailyQuest;
        [SerializeField] private IntEventChannel _onXpGained;
        [SerializeField] private TravelRequestChannel _requests;
        [SerializeField] private Checkpoint[] _checkpoints = new Checkpoint[0];

        private int _nextCheckpoint = 1;

#if UNITY_EDITOR
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                KillNearest();
            }
            if (keyboard.f2Key.wasPressedThisFrame)
            {
                KillAll();
            }
            if (keyboard.f3Key.wasPressedThisFrame)
            {
                _dailyQuest.CompleteNow();
            }
            if (keyboard.f4Key.wasPressedThisFrame)
            {
                _onXpGained.Raise(DebugXp);
            }
            if (keyboard.f5Key.wasPressedThisFrame && _checkpoints.Length > 0)
            {
                var checkpoint = _checkpoints[_nextCheckpoint % _checkpoints.Length];
                _nextCheckpoint++;
                _requests.Raise(new TravelRequest(checkpoint.point, checkpoint.areaId, false));
            }
        }
#endif

        private void KillNearest()
        {
            EnemyBrain nearest = null;
            float best = float.MaxValue;
            foreach (var director in _directors)
            {
                foreach (var enemy in director.Active)
                {
                    float d = (enemy.transform.position - _head.position).sqrMagnitude;
                    if (!enemy.IsDead && d < best)
                    {
                        best = d;
                        nearest = enemy;
                    }
                }
            }
            if (nearest != null)
            {
                nearest.Health.TakeDamage(KillDamage, nearest.transform.position + Vector3.up);
            }
        }

        private void KillAll()
        {
            foreach (var director in _directors)
            {
                for (int i = director.Active.Count - 1; i >= 0; i--)
                {
                    if (i >= director.Active.Count)
                    {
                        continue; // A boss's death despawns its followers mid-loop.
                    }
                    var enemy = director.Active[i];
                    if (!enemy.IsDead)
                    {
                        enemy.Health.TakeDamage(KillDamage, enemy.transform.position + Vector3.up);
                    }
                }
            }
        }
    }
}
