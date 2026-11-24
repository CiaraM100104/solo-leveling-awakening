using SLA.Data;

namespace SLA.Progression
{
    /// <summary>The five stats (PRG-5 to PRG-9).</summary>
    public enum Stat
    {
        Strength,
        Vitality,
        Agility,
        Perception,
        Intelligence,
    }

    /// <summary>Pure player progression state: level, XP, points and stats (PRG-1 to PRG-9).</summary>
    public sealed class PlayerStats
    {
        private readonly ProgressionConfig _config;
        private readonly int[] _stats = new int[5];

        public PlayerStats(ProgressionConfig config)
        {
            _config = config;
            for (int i = 0; i < _stats.Length; i++)
            {
                _stats[i] = config.baseStat;
            }
        }

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int UnspentStatPoints { get; private set; }
        public int SkillPoints { get; private set; }

        /// <summary>PRG-13: rank changes only at a re-assessment, never from levels alone.</summary>
        public HunterRank Rank { get; private set; } = HunterRank.E;

        public bool IsAtCap => Level >= _config.levelCap;

        public int XpToNext => IsAtCap ? 0 : XpCurve.XpToNext(Level, _config);

        public int MaxHp => _config.baseHp + _config.hpPerVitality * (Get(Stat.Vitality) - _config.baseStat);

        public int MaxMp => _config.baseMp + _config.mpPerIntelligence * (Get(Stat.Intelligence) - _config.baseStat);

        public int Get(Stat stat) => _stats[(int)stat];

        /// <summary>Adds XP and returns how many levels were gained.</summary>
        public int AddXp(int amount)
        {
            if (amount <= 0 || IsAtCap)
            {
                return 0;
            }

            Xp += amount;
            int gained = 0;
            while (!IsAtCap && Xp >= XpCurve.XpToNext(Level, _config))
            {
                Xp -= XpCurve.XpToNext(Level, _config);
                Level++;
                gained++;
                UnspentStatPoints += _config.statPointsPerLevel; // PRG-3
                if (Level > _config.skillPointsAfterLevel)
                {
                    SkillPoints++; // PRG-4
                }
            }

            if (IsAtCap)
            {
                Xp = 0; // PRG-1: XP stops at the cap.
            }
            return gained;
        }

        /// <summary>Spends one stat point (PRG-11).</summary>
        public bool TryAllocate(Stat stat)
        {
            if (UnspentStatPoints <= 0)
            {
                return false;
            }
            _stats[(int)stat]++;
            UnspentStatPoints--;
            return true;
        }

        /// <summary>Stat points from outside levelling, such as the daily quest (QST-1).</summary>
        public void AddStatPoints(int points)
        {
            if (points > 0)
            {
                UnspentStatPoints += points;
            }
        }

        /// <summary>PRG-13: called by the Association re-assessment.</summary>
        public void SetRank(HunterRank rank)
        {
            Rank = rank;
        }
    }
}
