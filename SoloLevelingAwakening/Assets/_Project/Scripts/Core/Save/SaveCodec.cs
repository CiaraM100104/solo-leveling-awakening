using System;
using System.Collections.Generic;
using UnityEngine;

namespace SLA.Core
{
    /// <summary>Pure JSON conversion for <see cref="SaveData"/>, with version checks and repair of missing parts.</summary>
    public static class SaveCodec
    {
        public static string ToJson(SaveData data) => JsonUtility.ToJson(data, true);

        /// <summary>Parses a save. Fails on empty or broken JSON and on saves from a newer game version.</summary>
        public static bool TryFromJson(string json, out SaveData data, out string error)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "The save file is empty.";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (ArgumentException e)
            {
                error = "The save file is not valid JSON: " + e.Message;
                return false;
            }

            if (data == null)
            {
                error = "The save file is empty.";
                return false;
            }
            if (data.version > SaveData.CurrentVersion)
            {
                error = $"The save is from a newer version ({data.version}) than this build ({SaveData.CurrentVersion}).";
                data = null;
                return false;
            }

            Repair(data);
            error = null;
            return true;
        }

        /// <summary>Fills anything a hand-edited or older save left out.</summary>
        private static void Repair(SaveData data)
        {
            data.version = SaveData.CurrentVersion;
            data.player = data.player ?? new PlayerSave();
            data.player.stats = data.player.stats ?? new StatsSave();
            data.player.level = Mathf.Max(1, data.player.level);
            data.player.xp = Mathf.Max(0, data.player.xp);
            data.player.unspent = Mathf.Max(0, data.player.unspent);
            data.inventory = data.inventory ?? new List<ItemStack>();
            data.inventory.RemoveAll(stack => stack.IsEmpty);
            data.equipped = data.equipped ?? new EquippedSave();
            data.gold = Mathf.Max(0, data.gold);
            data.quests = data.quests ?? new QuestSave();
            data.quests.completed = data.quests.completed ?? new List<string>();
            data.settings = data.settings ?? new SettingsSave();
            data.records = data.records ?? new List<RunRecord>();
        }
    }
}
