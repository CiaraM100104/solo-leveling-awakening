using System;
using System.Collections.Generic;

namespace SLA.Core
{
    /// <summary>
    /// The save file, shaped like the GDD's Save data example (one JSON file in persistentDataPath).
    /// <see cref="records"/> is an addition for the Scores board. Bump <see cref="CurrentVersion"/> whenever
    /// the shape changes and add a migration in <see cref="SaveCodec"/>.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public PlayerSave player = new PlayerSave();
        public List<ItemStack> inventory = new List<ItemStack>();
        public EquippedSave equipped = new EquippedSave();
        public int gold;
        public QuestSave quests = new QuestSave();
        public SettingsSave settings = new SettingsSave();
        public List<RunRecord> records = new List<RunRecord>();
    }

    [Serializable]
    public class PlayerSave
    {
        public int level = 1;
        public int xp;
        public string rank = "E";
        public string job = "None";
        public string title = "";
        public StatsSave stats = new StatsSave();
        public int unspent;
    }

    /// <summary>Field names match the GDD's JSON keys.</summary>
    [Serializable]
    public class StatsSave
    {
        public int str = 10;
        public int vit = 10;
        public int agi = 10;
        public int per = 10;
        public int @int = 10;
    }

    [Serializable]
    public class EquippedSave
    {
        public string hipL = "rusty_dagger";
        public string hipR = "rusty_dagger";
        public string gloves = "hunters_wraps";
    }

    [Serializable]
    public class QuestSave
    {
        public List<string> completed = new List<string>();
        public string active = "";
        public int dailyStreak;
    }

    [Serializable]
    public class SettingsSave
    {
        public string comfortPreset = "Recommended";
        public string uiMode = "Diegetic";
        public bool leftHanded;
    }

    /// <summary>One item stack: an item id and how many.</summary>
    [Serializable]
    public struct ItemStack
    {
        public string id;
        public int count;

        public ItemStack(string id, int count)
        {
            this.id = id;
            this.count = count;
        }

        public bool IsEmpty => string.IsNullOrEmpty(id) || count <= 0;
    }

    /// <summary>One finished gate run, for the Results screen and the Scores board.</summary>
    [Serializable]
    public class RunRecord
    {
        public string gateId;
        public string gateName;
        public float timeSeconds;
        public int kills;
        public int xp;
        public int gold;
        public int damageTaken;
        public int deaths;
        public int score;
        /// <summary>ISO 8601, UTC.</summary>
        public string dateUtc;
        /// <summary>1 = best on the board; 0 = didn't make the board. Set by the Scores board.</summary>
        public int placement;
    }
}
