using System;
using UnityEngine;

namespace SLA.Data
{
    /// <summary>One group of identical enemies in a wave.</summary>
    [Serializable]
    public class SpawnGroup
    {
        [Tooltip("Prefab with an EnemyBrain.")]
        public GameObject enemyPrefab;
        public int count = 1;
    }

    /// <summary>One wave: everything in it spawns together.</summary>
    [Serializable]
    public class Wave
    {
        public SpawnGroup[] groups = new SpawnGroup[0];
    }

    /// <summary>
    /// The fight in one combat or boss room (GTE-1). Timings are PLACEHOLDERS.
    /// </summary>
    [CreateAssetMenu(menuName = "SLA/Data/Encounter")]
    public class EncounterData : ScriptableObject
    {
        public Wave[] waves = new Wave[0];
        public float startDelaySeconds = 1.5f;
        [Tooltip("Keep longer than the enemies' corpse time so bodies are gone before the next wave (ENM-4).")]
        public float secondsBetweenWaves = 3.5f;
        [TextArea] public string clearedMessage = "Room cleared. The way forward is open.";

        [Header("Boss room")]
        public bool isBossRoom;
        [Tooltip("Spawned when the boss enters phase 2 (ENM-7).")]
        public Wave phase2Reinforcements = new Wave();
        [TextArea] public string phase2Message = "";
    }
}
