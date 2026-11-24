namespace SLA.Core
{
    /// <summary>
    /// Snapshot of what the UI shows about the player. Published on <see cref="PlayerStatusChannel"/>.
    /// </summary>
    public readonly struct PlayerStatus
    {
        /// <summary>Current level (PRG-1).</summary>
        public readonly int Level;
        /// <summary>XP earned toward the next level.</summary>
        public readonly int Xp;
        /// <summary>XP needed for the next level; 0 at the level cap.</summary>
        public readonly int XpToNext;
        public readonly int Hp;
        public readonly int MaxHp;
        public readonly int Mp;
        public readonly int MaxMp;
        /// <summary>Hunter rank index, 0 = E to 5 = S (PRG-12).</summary>
        public readonly int RankIndex;
        /// <summary>Stat points not yet spent (PRG-3, PRG-11).</summary>
        public readonly int UnspentStatPoints;
        public readonly int Strength;
        public readonly int Vitality;
        public readonly int Agility;
        public readonly int Perception;
        public readonly int Intelligence;

        /// <summary>Creates a snapshot.</summary>
        public PlayerStatus(int level, int xp, int xpToNext, int hp, int maxHp, int mp, int maxMp, int rankIndex,
            int unspentStatPoints, int strength, int vitality, int agility, int perception, int intelligence)
        {
            Level = level;
            Xp = xp;
            XpToNext = xpToNext;
            Hp = hp;
            MaxHp = maxHp;
            Mp = mp;
            MaxMp = maxMp;
            RankIndex = rankIndex;
            UnspentStatPoints = unspentStatPoints;
            Strength = strength;
            Vitality = vitality;
            Agility = agility;
            Perception = perception;
            Intelligence = intelligence;
        }

        /// <summary>Stat by index, in the order Strength, Vitality, Agility, Perception, Intelligence.</summary>
        public int Stat(int index)
        {
            switch (index)
            {
                case 0: return Strength;
                case 1: return Vitality;
                case 2: return Agility;
                case 3: return Perception;
                default: return Intelligence;
            }
        }
    }
}
