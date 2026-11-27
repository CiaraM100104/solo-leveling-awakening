using SLA.AI;
using SLA.Core;
using SLA.Core.Events;
using SLA.Data;
using SLA.Progression;
using UnityEngine;

namespace SLA.Gates
{
    /// <summary>
    /// One per gate: gives XP for each kill (PRG-2) and clears the gate when every enemy in the scene is
    /// dead, opening the exit portal. Placeholder for GTE-5 until bosses exist: then the boss's death
    /// clears the gate instead.
    /// </summary>
    public class GateClearTracker : MonoBehaviour
    {
        [SerializeField] private GateData _gate;
        [SerializeField] private ProgressionConfig _progression;
        [SerializeField] private StringEventChannel _onEnemyKilled;
        [SerializeField] private IntEventChannel _onLevelUp;
        [SerializeField] private VoidEventChannel _onGateCleared;
        [Tooltip("Hidden until the gate is cleared.")]
        [SerializeField] private GameObject _exitPortal;

        private int _remaining;
        private bool _cleared;

        public int Remaining => _remaining;

        private void OnEnable()
        {
            if (_onEnemyKilled != null)
            {
                _onEnemyKilled.Raised += OnEnemyKilled;
            }
        }

        private void OnDisable()
        {
            if (_onEnemyKilled != null)
            {
                _onEnemyKilled.Raised -= OnEnemyKilled;
            }
        }

        private void Start()
        {
            _remaining = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            if (_exitPortal != null)
            {
                _exitPortal.SetActive(false);
            }
            if (_remaining == 0)
            {
                Clear();
            }
        }

        private void OnEnemyKilled(string enemyId)
        {
            AwardXp(enemyId);
            _remaining--;
            if (_remaining <= 0 && !_cleared)
            {
                Clear();
            }
        }

        private void AwardXp(string enemyId)
        {
            var enemy = FindEnemy(enemyId);
            if (enemy == null || _progression == null)
            {
                Debug.LogWarning($"[GateClearTracker] No XP for '{enemyId}': it isn't in {(_gate != null ? _gate.name : "the gate data")}.");
                return;
            }

            int xp = XpCurve.EnemyXp(enemy.baseXp, _gate.rank, _progression);
            var save = GameSession.Data.player;
            var stats = PlayerStatsSave.FromSave(save);
            int levelsGained = stats.AddXp(xp, _progression);
            PlayerStatsSave.ToSave(stats, save);
            if (levelsGained > 0 && _onLevelUp != null)
            {
                _onLevelUp.Raise(stats.level);
            }
        }

        private EnemyData FindEnemy(string enemyId)
        {
            if (_gate == null)
            {
                return null;
            }
            if (_gate.boss != null && _gate.boss.id == enemyId)
            {
                return _gate.boss;
            }
            if (_gate.enemies == null)
            {
                return null;
            }
            foreach (EnemyData enemy in _gate.enemies)
            {
                if (enemy != null && enemy.id == enemyId)
                {
                    return enemy;
                }
            }
            return null;
        }

        private void Clear()
        {
            _cleared = true;
            if (_exitPortal != null)
            {
                _exitPortal.SetActive(true);
            }
            if (_onGateCleared != null)
            {
                _onGateCleared.Raise();
            }
        }
    }
}
