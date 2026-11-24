using System;
using System.Collections.Generic;
using SLA.Combat;
using SLA.Core;
using SLA.Data;
using SLA.Progression;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SLA.AI
{
    /// <summary>
    /// One per gate (GTE-8): pools every enemy the gate uses, caps how many are active (ENM-4),
    /// owns the shared attack tokens (ENM-2) and pays XP (PRG-2) and gold (ITM) for kills.
    /// Rooms ask it to spawn; it tells the owning room when one dies.
    /// </summary>
    public class EnemyDirector : MonoBehaviour
    {
        [Serializable]
        private class PoolEntry
        {
            public GameObject prefab;
            public int count = 4;
        }

        [SerializeField] private CombatConfig _combat;
        [SerializeField] private ProgressionConfig _progression;
        [SerializeField] private EconomyConfig _economy;
        [SerializeField] private GateData _gate;
        [SerializeField] private Transform _playerHead;
        [SerializeField] private Health _playerHealth;
        [SerializeField] private PoolEntry[] _pools = new PoolEntry[0];

        [Header("Channels")]
        [SerializeField] private IntEventChannel _onXpGained;
        [SerializeField] private IntEventChannel _onGoldGained;
        [SerializeField] private HitEventChannel _hits;
        [SerializeField] private VoidEventChannel _onEnemyKilled;
        [SerializeField] private ItemGrantChannel _onItemGranted;

        [Header("Loot")]
        [SerializeField] private ItemData _potionDrop;
        [SerializeField] private ItemData _monsterCore;

        private readonly Dictionary<GameObject, List<EnemyBrain>> _pool = new Dictionary<GameObject, List<EnemyBrain>>();
        private readonly List<EnemyBrain> _active = new List<EnemyBrain>();

        public AttackTokenPool Tokens { get; private set; }

        /// <summary>Every enemy currently in the world, alive or a corpse.</summary>
        public IReadOnlyList<EnemyBrain> Active => _active;

        private void Awake()
        {
            Tokens = new AttackTokenPool(_combat.maxSimultaneousAttackers);
            foreach (var entry in _pools)
            {
                var list = new List<EnemyBrain>(entry.count);
                for (int i = 0; i < entry.count; i++)
                {
                    var go = Instantiate(entry.prefab, transform);
                    go.SetActive(false);
                    list.Add(go.GetComponent<EnemyBrain>());
                }
                _pool[entry.prefab] = list;
            }
        }

        /// <summary>Spawns one enemy, or returns null if none is free or the active cap is reached.</summary>
        public EnemyBrain Spawn(GameObject prefab, Vector3 position, float circleAngleRadians, RoomEncounter owner)
        {
            if (_active.Count >= _combat.maxActiveEnemies || !_pool.TryGetValue(prefab, out var list))
            {
                return null;
            }
            foreach (var enemy in list)
            {
                if (enemy.gameObject.activeSelf)
                {
                    continue;
                }
                enemy.Spawn(this, owner, _playerHead, _playerHealth, position, circleAngleRadians);
                _active.Add(enemy);
                return enemy;
            }
            return null;
        }

        public void HandleEnemyDied(EnemyBrain enemy)
        {
            _onXpGained.Raise(XpCurve.EnemyXp(enemy.Data.baseXp, _gate.rank, _progression));
            int gold = Loot.GoldForKill(enemy.Data, _gate.rank, _economy, Random.value);
            _onGoldGained.Raise(gold);
            _hits.Raise(new HitEvent(enemy.transform.position + Vector3.up * 1.2f, gold, HitKind.Gold));
            _onEnemyKilled.Raise();

            // Loot goes straight to the System inventory (ITM-9).
            if (_potionDrop != null && Loot.DropsPotion(enemy.Data, _gate.rank, _economy, Random.value))
            {
                _onItemGranted.Raise(new ItemStack(_potionDrop.id, 1));
            }
            if (enemy.Data.isBoss && _monsterCore != null && _economy.bossMonsterCores > 0)
            {
                _onItemGranted.Raise(new ItemStack(_monsterCore.id, _economy.bossMonsterCores));
            }

            if (enemy.Owner != null)
            {
                enemy.Owner.HandleEnemyDied(enemy);
            }
        }

        public void Despawn(EnemyBrain enemy)
        {
            Tokens.Release(enemy.Id);
            enemy.gameObject.SetActive(false);
            _active.Remove(enemy);
        }

        /// <summary>Removes every enemy a room spawned. With <paramref name="aliveOnly"/>, corpses stay.</summary>
        public void DespawnOwnedBy(RoomEncounter owner, bool aliveOnly)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var enemy = _active[i];
                if (enemy.Owner == owner && !(aliveOnly && enemy.IsDead))
                {
                    Despawn(enemy);
                }
            }
        }
    }
}
