using System;
using System.IO;
using System.Linq;
using SLA.Core;
using SLA.Data;
using TMPro;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SLA.Editor
{
    /// <summary>
    /// Builds the barebones playable loop (Scenes/Sandbox/Sandbox_Barebones.unity) and everything it uses:
    /// a virtual hub room (MRH Hub_Virtual fallback) with a gate on the wall, the daily quest and the Status window,
    /// and the Goblin Warren E-rank gate (entrance, two combat rooms, antechamber, boss room).
    /// Run "SLA/Barebones/1. Import Dependencies" once, then "SLA/Barebones/2. Build Sandbox".
    /// Rerunning step 2 rebuilds prefabs and the scene but keeps existing data assets, so tuning survives.
    /// </summary>
    public static partial class BarebonesSandboxBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = Root + "/Scenes/Sandbox/Sandbox_Barebones.unity";
        private const string XriPackage = "com.unity.xr.interaction.toolkit";
        private const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        private const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private const int HubAreaId = 0;
        private const int GateAreaId = 1;

        private static readonly string[] ProjectLayers =
            { "Player", "PlayerHands", "PlayerWeapon", "Enemy", "EnemyWeapon", "Environment", "RoomScan" };

        // ---------------------------------------------------------------- step 1

        [MenuItem("SLA/Barebones/1. Import Dependencies")]
        public static void ImportDependencies()
        {
            if (!File.Exists(TmpFontPath))
            {
                // Asynchronous: in batch mode pass -importPackage on the command line instead.
                UnityEditor.AssetPackage.Package.Import(TmpEssentials, false);
                Debug.Log("[SLA] Importing TMP Essential Resources.");
            }

            foreach (var sample in Sample.FindByPackage(XriPackage, null))
            {
                if ((sample.displayName == "Starter Assets" || sample.displayName == "XR Device Simulator") && !sample.isImported)
                {
                    sample.Import(Sample.ImportOptions.HideImportWindow | Sample.ImportOptions.OverridePreviousImports);
                    Debug.Log($"[SLA] Imported XRI sample: {sample.displayName}");
                }
            }
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- step 2

        [MenuItem("SLA/Barebones/2. Build Sandbox")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            // Open the new scene first: opening a scene unloads assets that only C# variables hold,
            // which would turn every reference below into null.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var originPrefab = FindSamplePrefab("XR Origin (XR Rig)");
            var vignettePrefab = FindSamplePrefab("TunnelingVignette");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (originPrefab == null || font == null)
            {
                throw new InvalidOperationException("Run SLA/Barebones/1. Import Dependencies first.");
            }

            EnsureLayers();
            QuestProjectSettings.ApplyTeleportInteractionLayer();
            EnsureFolders();
            DeleteObsoleteAssets();

            var ctx = new Context
            {
                Font = font,
                Mats = CreateMaterials(),
                Sfx = CreateSounds(),
                Data = CreateData(),
                Sys = CreateSystemsData(),
            };
            BuildPrefabs(ctx);
            BuildSystemsPrefabs(ctx);
            CreateEncounters(ctx);
            BuildScene(scene, ctx, originPrefab, vignettePrefab);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[SLA] Barebones sandbox built: " + ScenePath);
        }

        // ---------------------------------------------------------------- shared state

        private sealed class Context
        {
            public TMP_FontAsset Font;
            public Materials Mats;
            public Sounds Sfx;
            public DataAssets Data;
            public SystemsData Sys;
            public GameObject Goblin, Kobold, Chieftain, Dagger, Sparring, Potion;
        }

        private sealed class Materials
        {
            public Material HubFloor, HubWall, HubFurniture, HubBedding;
            public Material CaveFloor, CaveWall, CaveCeiling, CrystalViolet, CrystalBlue, Barrier, Rack;
            public Material PortalRim, PortalCore, Pad, SquatSpot, Fade;
            public Material Goblin, Kobold, Chieftain, GoblinEyes, GoblinClub, Crown;
            public Material Blade, Handle;
        }

        private sealed class Sounds
        {
            public AudioClip Tell, Hit, Chime, Parry, Block, Rep, Hurt;
        }

        private sealed class DataAssets
        {
            public CombatConfig Combat;
            public ProgressionConfig Progression;
            public EconomyConfig Economy;
            public DailyQuestConfig DailyQuest;
            public WeaponData RustyDagger;
            public EnemyData Goblin, Kobold, Chieftain;
            public GateData GoblinWarren;
            public EncounterData Room1, Room2, BossRoom;

            public IntEventChannel OnXpGained, OnGoldGained, OnLevelUp, OnAllocateStat, OnDailyQuestCompleted;
            public VoidEventChannel OnPlayerDied, OnGateCleared, TravelStarting, DailyQuestStart, DailyQuestRest;
            public PlayerStatusChannel PlayerStatus;
            public HitEventChannel Hits;
            public SystemMessageChannel Messages;
            public TravelRequestChannel RequestTravel, Arrived;
            public DailyQuestStatusChannel DailyQuestStatus;
        }

        // ---------------------------------------------------------------- project setup

        private static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            foreach (var name in ProjectLayers)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                {
                    if (layers.GetArrayElementAtIndex(i).stringValue == name)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                {
                    continue;
                }
                for (int i = 8; i < layers.arraySize; i++)
                {
                    var slot = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.stringValue))
                    {
                        slot.stringValue = name;
                        break;
                    }
                }
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            foreach (var path in new[]
                     {
                         Root + "/Art/Materials", Root + "/Audio/SFX", Root + "/Data/Weapons", Root + "/Data/Enemies",
                         Root + "/Data/Gates", Root + "/Data/Events", Root + "/Prefabs/Enemies", Root + "/Prefabs/Weapons", Root + "/Prefabs/Items",
                         Root + "/Scenes/Sandbox",
                     })
            {
                EnsureFolder(path);
            }
        }

        private static void DeleteObsoleteAssets()
        {
            foreach (var path in new[]
                     {
                         Root + "/Data/Gates/Encounter_Sandbox.asset",
                         Root + "/Data/Events/OnEnemyKilled.asset",
                         Root + "/Data/Events/OnWaveStarted.asset",
                     })
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        // ---------------------------------------------------------------- materials

        private static Materials CreateMaterials()
        {
            var blue = new Color(0.25f, 0.72f, 1f);
            var violet = new Color(0.48f, 0.25f, 0.9f);
            return new Materials
            {
                HubFloor = Mat("M_Hub_Floor", new Color(0.32f, 0.25f, 0.19f)),
                HubWall = Mat("M_Hub_Wall", new Color(0.5f, 0.48f, 0.45f)),
                HubFurniture = Mat("M_Hub_Furniture", new Color(0.24f, 0.19f, 0.16f)),
                HubBedding = Mat("M_Hub_Bedding", new Color(0.18f, 0.22f, 0.36f)),
                CaveFloor = Mat("M_Cave_Floor", new Color(0.14f, 0.14f, 0.17f)),
                CaveWall = Mat("M_Cave_Wall", new Color(0.11f, 0.11f, 0.15f)),
                CaveCeiling = Mat("M_Cave_Ceiling", new Color(0.05f, 0.05f, 0.07f)),
                CrystalViolet = Mat("M_Crystal_Violet", violet * 0.4f, violet * 2.5f),
                CrystalBlue = Mat("M_Crystal_Blue", blue * 0.4f, blue * 2f),
                Barrier = Mat("M_System_Barrier", new Color(0.05f, 0.1f, 0.2f), blue * 1.4f),
                Rack = Mat("M_Rack_Wood", new Color(0.22f, 0.15f, 0.1f)),
                PortalRim = Mat("M_Gate_Rim", blue * 0.3f, blue * 2.2f),
                PortalCore = Mat("M_Gate_Core", new Color(0.02f, 0.02f, 0.06f), violet * 0.45f),
                Pad = Mat("M_System_Pad", new Color(0.05f, 0.1f, 0.2f), blue * 2f),
                SquatSpot = Mat("M_System_SquatSpot", new Color(0.05f, 0.1f, 0.2f), blue * 1.2f),
                Fade = FadeMaterial("M_ScreenFade"),
                Goblin = Mat("M_Goblin_Body", new Color(0.4f, 0.66f, 0.3f), Color.black),
                Kobold = Mat("M_Kobold_Body", new Color(0.72f, 0.46f, 0.26f), Color.black),
                Chieftain = Mat("M_GoblinChieftain_Body", new Color(0.3f, 0.46f, 0.2f), Color.black),
                GoblinEyes = Mat("M_Goblin_Eyes", new Color(1f, 0.8f, 0.1f), new Color(2.5f, 1.8f, 0.2f)),
                GoblinClub = Mat("M_Goblin_Club", new Color(0.3f, 0.22f, 0.15f), Color.black),
                Crown = Mat("M_GoblinChieftain_Crown", new Color(0.9f, 0.7f, 0.2f), Color.black, 0.8f),
                Blade = Mat("M_RustyDagger_Blade", new Color(0.55f, 0.42f, 0.35f), Color.black, 0.6f),
                Handle = Mat("M_RustyDagger_Handle", new Color(0.2f, 0.12f, 0.08f), Color.black),
            };
        }

        /// <summary>URP Lit. Pass an emission colour (black is fine) to let scripts drive emission.</summary>
        private static Material Mat(string name, Color color, Color? emission = null, float metallic = 0f)
        {
            string path = $"{Root}/Art/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", 0.3f);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                // URP turns _EMISSION back off on any material whose GI flags are not emissive.
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Unlit, transparent, both faces, drawn last: a black shell around the camera.</summary>
        private static Material FadeMaterial(string name)
        {
            string path = $"{Root}/Art/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Overlay;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- sounds (generated placeholders)

        private static Sounds CreateSounds()
        {
            return new Sounds
            {
                Tell = Wav("SFX_Enemy_Tell_01", 0.55f, (t, n) =>
                {
                    float f = Mathf.Lerp(160f, 320f, t / 0.55f);
                    float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
                    return square * 0.35f * Mathf.Clamp01(t * 20f) * Mathf.Clamp01((0.55f - t) * 10f);
                }),
                Hit = Wav("SFX_Impact_Hit_01", 0.12f, (t, n) =>
                    (n * 0.6f + Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.6f) * Mathf.Exp(-t * 45f)),
                Chime = Wav("SFX_System_Chime_01", 0.5f, (t, n) =>
                {
                    float f = t < 0.12f ? 988f : 1319f;
                    float local = t < 0.12f ? t : t - 0.12f;
                    return Mathf.Sin(2f * Mathf.PI * f * t) * 0.3f * Mathf.Exp(-local * 7f) * Mathf.Clamp01(local * 200f);
                }),
                Parry = Wav("SFX_Parry_01", 0.6f, (t, n) =>
                    (Mathf.Sin(2f * Mathf.PI * 1850f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 2630f * t) + n * 0.3f * Mathf.Exp(-t * 60f))
                    * 0.35f * Mathf.Exp(-t * 6f)),
                Block = Wav("SFX_Block_01", 0.18f, (t, n) =>
                    (n * 0.5f + Mathf.Sin(2f * Mathf.PI * 310f * t) * 0.5f) * Mathf.Exp(-t * 30f)),
                Rep = Wav("SFX_System_Rep_01", 0.09f, (t, n) =>
                    Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.3f * Mathf.Exp(-t * 25f) * Mathf.Clamp01(t * 300f)),
                Hurt = Wav("SFX_Player_Hurt_01", 0.22f, (t, n) =>
                    (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 70f, t / 0.22f) * t) * 0.7f + n * 0.25f) * Mathf.Exp(-t * 14f)),
            };
        }

        /// <summary>Writes a 16-bit mono WAV generated by <paramref name="sample"/>(time, noise), once.</summary>
        private static AudioClip Wav(string name, float seconds, Func<float, float, float> sample)
        {
            string path = $"{Root}/Audio/SFX/{name}.wav";
            if (!File.Exists(path))
            {
                const int rate = 44100;
                int count = Mathf.CeilToInt(seconds * rate);
                var random = new System.Random(name.GetHashCode());
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write("RIFF".ToCharArray());
                    writer.Write(36 + count * 2);
                    writer.Write("WAVEfmt ".ToCharArray());
                    writer.Write(16);
                    writer.Write((short)1);
                    writer.Write((short)1);
                    writer.Write(rate);
                    writer.Write(rate * 2);
                    writer.Write((short)2);
                    writer.Write((short)16);
                    writer.Write("data".ToCharArray());
                    writer.Write(count * 2);
                    for (int i = 0; i < count; i++)
                    {
                        float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                        float v = Mathf.Clamp(sample(i / (float)rate, noise), -1f, 1f);
                        writer.Write((short)(v * short.MaxValue));
                    }
                }
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ---------------------------------------------------------------- data

        private static DataAssets CreateData()
        {
            string events = Root + "/Data/Events";
            return new DataAssets
            {
                Combat = LoadOrCreate<CombatConfig>(Root + "/Data/CombatConfig.asset"),
                Progression = LoadOrCreate<ProgressionConfig>(Root + "/Data/ProgressionConfig.asset"),
                Economy = LoadOrCreate<EconomyConfig>(Root + "/Data/EconomyConfig.asset"),
                DailyQuest = LoadOrCreate<DailyQuestConfig>(Root + "/Data/DailyQuestConfig.asset"),
                RustyDagger = LoadOrCreate<WeaponData>(Root + "/Data/Weapons/RustyDagger.asset"),
                Goblin = LoadOrCreate<EnemyData>(Root + "/Data/Enemies/Goblin.asset"),
                Kobold = LoadOrCreate<EnemyData>(Root + "/Data/Enemies/Kobold.asset", e =>
                {
                    // Roster: 25 HP, 5 damage, very fast swarms. Timings and speed are PLACEHOLDERS.
                    e.displayName = "Kobold";
                    e.maxHp = 25;
                    e.damagePerHit = 5;
                    e.baseXp = 12;
                    e.windupSeconds = 0.6f;
                    e.attackSeconds = 0.2f;
                    e.recoverSeconds = 0.8f;
                    e.moveSpeedMetersPerSecond = 2.4f;
                    e.circleRadiusMeters = 1.6f;
                    e.attackRangeMeters = 0.9f;
                }),
                Chieftain = LoadOrCreate<EnemyData>(Root + "/Data/Enemies/GoblinChieftain.asset", e =>
                {
                    // Bosses: 400 HP, phase 2 at 50%. Damage, XP, gold and timings are PLACEHOLDERS.
                    e.displayName = "Goblin Chieftain";
                    e.maxHp = 400;
                    e.damagePerHit = 15;
                    e.baseXp = 150;
                    e.fixedGold = 30;
                    e.isBoss = true;
                    e.phase2HpFraction = 0.5f;
                    e.windupSeconds = 1f;
                    e.attackSeconds = 0.35f;
                    e.recoverSeconds = 1.4f;
                    e.moveSpeedMetersPerSecond = 1.3f;
                    e.circleRadiusMeters = 2.4f;
                    e.attackRangeMeters = 1.6f;
                    e.corpseSeconds = 6f;
                }),
                GoblinWarren = LoadOrCreate<GateData>(Root + "/Data/Gates/GoblinWarren.asset"),

                OnXpGained = LoadOrCreate<IntEventChannel>(events + "/OnXpGained.asset"),
                OnGoldGained = LoadOrCreate<IntEventChannel>(events + "/OnGoldGained.asset"),
                OnLevelUp = LoadOrCreate<IntEventChannel>(events + "/OnLevelUp.asset"),
                OnAllocateStat = LoadOrCreate<IntEventChannel>(events + "/OnAllocateStat.asset"),
                OnDailyQuestCompleted = LoadOrCreate<IntEventChannel>(events + "/OnDailyQuestCompleted.asset"),
                OnPlayerDied = LoadOrCreate<VoidEventChannel>(events + "/OnPlayerDied.asset"),
                OnGateCleared = LoadOrCreate<VoidEventChannel>(events + "/OnGateCleared.asset"),
                TravelStarting = LoadOrCreate<VoidEventChannel>(events + "/TravelStarting.asset"),
                DailyQuestStart = LoadOrCreate<VoidEventChannel>(events + "/DailyQuestStart.asset"),
                DailyQuestRest = LoadOrCreate<VoidEventChannel>(events + "/DailyQuestRest.asset"),
                PlayerStatus = LoadOrCreate<PlayerStatusChannel>(events + "/PlayerStatus.asset"),
                Hits = LoadOrCreate<HitEventChannel>(events + "/Hits.asset"),
                Messages = LoadOrCreate<SystemMessageChannel>(events + "/SystemMessages.asset"),
                RequestTravel = LoadOrCreate<TravelRequestChannel>(events + "/RequestTravel.asset"),
                Arrived = LoadOrCreate<TravelRequestChannel>(events + "/Arrived.asset"),
                DailyQuestStatus = LoadOrCreate<DailyQuestStatusChannel>(events + "/DailyQuestStatus.asset"),
            };
        }

        private static void CreateEncounters(Context ctx)
        {
            var d = ctx.Data;
            string gates = Root + "/Data/Gates";
            // Wave sizes follow the roster's pack sizes (goblins 2–3, kobolds 4–6); timings are PLACEHOLDERS.
            d.Room1 = LoadOrCreate<EncounterData>(gates + "/Encounter_GoblinWarren_Room1.asset", e =>
            {
                e.waves = new[] { WaveOf(Group(ctx.Goblin, 2)), WaveOf(Group(ctx.Kobold, 4)) };
            });
            d.Room2 = LoadOrCreate<EncounterData>(gates + "/Encounter_GoblinWarren_Room2.asset", e =>
            {
                e.waves = new[] { WaveOf(Group(ctx.Goblin, 3)), WaveOf(Group(ctx.Goblin, 2), Group(ctx.Kobold, 3)) };
            });
            d.BossRoom = LoadOrCreate<EncounterData>(gates + "/Encounter_GoblinWarren_Boss.asset", e =>
            {
                e.waves = new[] { WaveOf(Group(ctx.Chieftain, 1)) };
                e.isBossRoom = true;
                e.startDelaySeconds = 2f;
                e.clearedMessage = "";
                // The spec's phase 2 brings archers onto ledges; archers aren't built, so goblins stand in.
                e.phase2Reinforcements = WaveOf(Group(ctx.Goblin, 2));
                e.phase2Message = "The Goblin Chieftain calls for reinforcements.";
            });
        }

        private static SpawnGroup Group(GameObject prefab, int count) => new SpawnGroup { enemyPrefab = prefab, count = count };

        private static Wave WaveOf(params SpawnGroup[] groups) => new Wave { groups = groups };

        private static T LoadOrCreate<T>(string path, Action<T> initialize = null) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                initialize?.Invoke(asset);
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }
    }
}
