using SLA.Core.Save;

namespace SLA.Progression
{
    /// <summary>Copies the player's level, XP, points and stats between the save file and <see cref="PlayerStats"/>.</summary>
    public static class PlayerStatsSave
    {
        public static PlayerStats FromSave(PlayerSave save)
        {
            return new PlayerStats
            {
                level = save.level,
                xp = save.xp,
                unspentPoints = save.unspent,
                skillPoints = save.skillPoints,
                strength = save.stats.str,
                vitality = save.stats.vit,
                agility = save.stats.agi,
                perception = save.stats.per,
                intelligence = save.stats.@int,
            };
        }

        public static void ToSave(PlayerStats stats, PlayerSave save)
        {
            save.level = stats.level;
            save.xp = stats.xp;
            save.unspent = stats.unspentPoints;
            save.skillPoints = stats.skillPoints;
            save.stats.str = stats.strength;
            save.stats.vit = stats.vitality;
            save.stats.agi = stats.agility;
            save.stats.per = stats.perception;
            save.stats.@int = stats.intelligence;
        }
    }
}
