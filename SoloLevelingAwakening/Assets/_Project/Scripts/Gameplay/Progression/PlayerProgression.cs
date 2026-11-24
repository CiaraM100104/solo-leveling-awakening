using SLA.Combat;
using SLA.Core;
using SLA.Data;
using UnityEngine;

namespace SLA.Progression
{
    /// <summary>
    /// Owns the player's <see cref="PlayerStats"/> and gold: turns kills into XP and gold, heals on level-up (PRG-3),
    /// spends stat points (PRG-11), pays the daily quest (QST-1), handles death (GTE-4, XP kept)
    /// and publishes a <see cref="PlayerStatus"/> for the UI.
    /// </summary>
    public class PlayerProgression : MonoBehaviour
    {
        [SerializeField] private ProgressionConfig _config;
        [SerializeField] private Health _health;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _hurtClip;

        [Header("Channels in")]
        [SerializeField] private IntEventChannel _onXpGained;
        [SerializeField] private IntEventChannel _onGoldGained;
        [Tooltip("Stat index: 0 Strength, 1 Vitality, 2 Agility, 3 Perception, 4 Intelligence.")]
        [SerializeField] private IntEventChannel _onAllocateStat;
        [Tooltip("Stat points paid by the daily quest.")]
        [SerializeField] private IntEventChannel _onDailyQuestCompleted;

        [Header("Channels out")]
        [SerializeField] private IntEventChannel _onLevelUp;
        [SerializeField] private VoidEventChannel _onPlayerDied;
        [SerializeField] private PlayerStatusChannel _status;

        private PlayerStats _stats;
        private int _gold;

        private void Awake()
        {
            _stats = new PlayerStats(_config);
            _health.SetMax(_stats.MaxHp, true);
        }

        private void OnEnable()
        {
            _onXpGained.Raised += AddXp;
            _onGoldGained.Raised += AddGold;
            _onAllocateStat.Raised += Allocate;
            _onDailyQuestCompleted.Raised += CompleteDailyQuest;
            _health.Changed += Publish;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _onXpGained.Raised -= AddXp;
            _onGoldGained.Raised -= AddGold;
            _onAllocateStat.Raised -= Allocate;
            _onDailyQuestCompleted.Raised -= CompleteDailyQuest;
            _health.Changed -= Publish;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void Start()
        {
            Publish();
        }

        private void AddXp(int xp)
        {
            int levelsGained = _stats.AddXp(xp);
            if (levelsGained > 0)
            {
                // PRG-3: a level-up fully restores HP, even mid-fight. SetMax publishes through Changed.
                _health.SetMax(_stats.MaxHp, true);
                _onLevelUp.Raise(_stats.Level);
            }
            Publish();
        }

        private void AddGold(int gold)
        {
            _gold += gold;
            Publish();
        }

        private void Allocate(int statIndex)
        {
            var stat = (Stat)Mathf.Clamp(statIndex, 0, 4);
            if (!_stats.TryAllocate(stat))
            {
                return;
            }
            if (stat == Stat.Vitality)
            {
                // PRG-6: the new max HP comes with the HP to fill it.
                _health.SetMax(_stats.MaxHp, false);
                _health.Heal(_config.hpPerVitality);
            }
            Publish();
        }

        private void CompleteDailyQuest(int statPoints)
        {
            _stats.AddStatPoints(statPoints);
            _health.RestoreFull(); // QST-1: full recovery.
            Publish();
        }

        private void OnDamaged(int amount, Vector3 point)
        {
            if (_hurtClip != null)
            {
                _audio.PlayOneShot(_hurtClip);
            }
        }

        private void OnDied()
        {
            // GTE-4: respawn with XP kept. The gate moves the player back to its entrance.
            _onPlayerDied.Raise();
            _health.RestoreFull();
        }

        private void Publish()
        {
            _status.Raise(new PlayerStatus(
                _stats.Level,
                _stats.Xp,
                _stats.XpToNext,
                _health.Current,
                _health.Max,
                _stats.MaxMp, // Nothing spends MP until skills arrive.
                _stats.MaxMp,
                (int)_stats.Rank,
                _stats.UnspentStatPoints,
                _gold,
                _stats.Get(Stat.Strength),
                _stats.Get(Stat.Vitality),
                _stats.Get(Stat.Agility),
                _stats.Get(Stat.Perception),
                _stats.Get(Stat.Intelligence)));
        }
    }
}
