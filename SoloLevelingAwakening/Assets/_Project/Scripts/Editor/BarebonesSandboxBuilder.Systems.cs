using SLA.AI;
using SLA.Combat;
using SLA.Core;
using SLA.Data;
using SLA.Gates;
using SLA.Items;
using SLA.MR;
using SLA.Quests;
using SLA.UI;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace SLA.Editor
{
    /// <summary>
    /// The core-loop systems around the gate: title screen, tutorial, save/load, game states, inventory, potions,
    /// shop, results and scores, pause and comfort settings.
    /// </summary>
    public static partial class BarebonesSandboxBuilder
    {
        private const int TitleAreaId = 2;
        private const int TutorialAreaId = 3;
        private static readonly Vector3 TitleOrigin = new Vector3(-100f, 0f, 0f);
        private static readonly Vector3 TutorialOrigin = new Vector3(0f, 0f, -100f);
        private static readonly Color TitleAmbient = new Color(0.08f, 0.08f, 0.14f);
        private static readonly Color TutorialAmbient = new Color(0.3f, 0.3f, 0.36f);

        private sealed class SystemsData
        {
            public ItemDatabase Items;
            public ItemData HealthPotion, ManaPotion, FatigueTonic, ReturnStone, MonsterCore, SteelDagger;
            public ShopData Shop;
            public ScoreConfig Score;
            public ComfortConfig Comfort;
            public EnemyData Sparring;
            public GateData Training;

            public VoidEventChannel SaveRequested, NewGameStarted, PauseToggle, EndDay, ReturnToTitle, OnEnemyKilled, SellRequested;
            public IntEventChannel SaveExists, TitleCommand, BuyRequested, SetComfortPreset, SetUiMode, OnPlayerDamaged;
            public SaveDataChannel CollectSave, ApplySave;
            public GameStateChannel GameState;
            public ItemGrantChannel ItemGranted;
            public StringEventChannel UseItemRequested, TutorialInstruction;
            public ItemDataEventChannel ItemUsed;
            public InventoryStatusChannel Inventory;
            public RunRecordChannel RunCompleted, ResultsReady;
            public ScoresChannel Scores;
            public SettingsChannel Settings;
        }

        // ---------------------------------------------------------------- data

        private static SystemsData CreateSystemsData()
        {
            string items = Root + "/Data/Items";
            string events = Root + "/Data/Events";
            EnsureFolder(items);
            var s = new SystemsData
            {
                // ITM-4 / consumables table. Stack sizes are PLACEHOLDERS.
                HealthPotion = LoadOrCreate<ItemData>(items + "/HealthPotion.asset", i =>
                {
                    i.id = "health_potion";
                    i.displayName = "Health Potion";
                    i.description = "Raise to your mouth to drink. +50 HP over 2 s.";
                    i.buyPrice = 50;
                    i.healHp = 50;
                    i.effectSeconds = 2f;
                }),
                ManaPotion = LoadOrCreate<ItemData>(items + "/ManaPotion.asset", i =>
                {
                    i.id = "mana_potion";
                    i.displayName = "Mana Potion";
                    i.description = "+30 MP. (Drinking it arrives with skills and MP.)";
                    i.buyPrice = 60;
                    i.restoreMp = 30;
                }),
                FatigueTonic = LoadOrCreate<ItemData>(items + "/FatigueTonic.asset", i =>
                {
                    i.id = "fatigue_tonic";
                    i.displayName = "Fatigue Tonic";
                    i.description = "Fatigue to 0. (Arrives with the Fatigue system.)";
                    i.buyPrice = 80;
                }),
                ReturnStone = LoadOrCreate<ItemData>(items + "/ReturnStone.asset", i =>
                {
                    i.id = "return_stone";
                    i.displayName = "Return Stone";
                    i.kind = ItemKind.Key;
                    i.description = "Crush to leave a blue gate. (Crushing is not built yet.)";
                    i.buyPrice = 200;
                    i.maxStack = 5;
                }),
                MonsterCore = LoadOrCreate<ItemData>(items + "/MonsterCoreE.asset", i =>
                {
                    i.id = "monster_core_e";
                    i.displayName = "Monster Core (E)";
                    i.kind = ItemKind.Material;
                    i.description = "Sells for 20 gold.";
                    i.sellPrice = 20;
                    i.maxStack = 99;
                }),
                SteelDagger = LoadOrCreate<ItemData>(items + "/SteelDagger.asset", i =>
                {
                    i.id = "steel_dagger";
                    i.displayName = "Steel Dagger";
                    i.kind = ItemKind.Weapon;
                    i.description = "Base damage 12. (Equipping arrives with holsters.)";
                    i.buyPrice = 150;
                    i.maxStack = 1;
                }),
                Score = LoadOrCreate<ScoreConfig>(Root + "/Data/ScoreConfig.asset"),
                Comfort = LoadOrCreate<ComfortConfig>(Root + "/Data/ComfortConfig.asset"),
                Sparring = LoadOrCreate<EnemyData>(Root + "/Data/Enemies/SparringGoblin.asset", e =>
                {
                    // Tutorial partner: can't be killed, barely hurts, slow and readable.
                    e.displayName = "Sparring Goblin";
                    e.maxHp = 99999;
                    e.damagePerHit = 1;
                    e.baseXp = 0;
                    e.windupSeconds = 1f;
                    e.attackSeconds = 0.35f;
                    e.recoverSeconds = 1.6f;
                    e.moveSpeedMetersPerSecond = 1f;
                }),
                Training = LoadOrCreate<GateData>(Root + "/Data/Gates/TrainingGrounds.asset", g =>
                {
                    g.id = "training_grounds";
                    g.displayName = "Training Grounds";
                    g.enemyTypes = "Sparring goblin";
                }),

                SaveRequested = LoadOrCreate<VoidEventChannel>(events + "/SaveRequested.asset"),
                NewGameStarted = LoadOrCreate<VoidEventChannel>(events + "/NewGameStarted.asset"),
                PauseToggle = LoadOrCreate<VoidEventChannel>(events + "/PauseToggle.asset"),
                EndDay = LoadOrCreate<VoidEventChannel>(events + "/EndDay.asset"),
                ReturnToTitle = LoadOrCreate<VoidEventChannel>(events + "/ReturnToTitle.asset"),
                OnEnemyKilled = LoadOrCreate<VoidEventChannel>(events + "/OnEnemyKilled.asset"),
                SellRequested = LoadOrCreate<VoidEventChannel>(events + "/SellRequested.asset"),
                SaveExists = LoadOrCreate<IntEventChannel>(events + "/SaveExists.asset"),
                TitleCommand = LoadOrCreate<IntEventChannel>(events + "/TitleCommand.asset"),
                BuyRequested = LoadOrCreate<IntEventChannel>(events + "/BuyRequested.asset"),
                SetComfortPreset = LoadOrCreate<IntEventChannel>(events + "/SetComfortPreset.asset"),
                SetUiMode = LoadOrCreate<IntEventChannel>(events + "/SetUiMode.asset"),
                OnPlayerDamaged = LoadOrCreate<IntEventChannel>(events + "/OnPlayerDamaged.asset"),
                CollectSave = LoadOrCreate<SaveDataChannel>(events + "/CollectSave.asset"),
                ApplySave = LoadOrCreate<SaveDataChannel>(events + "/ApplySave.asset"),
                GameState = LoadOrCreate<GameStateChannel>(events + "/GameState.asset"),
                ItemGranted = LoadOrCreate<ItemGrantChannel>(events + "/ItemGranted.asset"),
                UseItemRequested = LoadOrCreate<StringEventChannel>(events + "/UseItemRequested.asset"),
                TutorialInstruction = LoadOrCreate<StringEventChannel>(events + "/TutorialInstruction.asset"),
                ItemUsed = LoadOrCreate<ItemDataEventChannel>(events + "/ItemUsed.asset"),
                Inventory = LoadOrCreate<InventoryStatusChannel>(events + "/Inventory.asset"),
                RunCompleted = LoadOrCreate<RunRecordChannel>(events + "/RunCompleted.asset"),
                ResultsReady = LoadOrCreate<RunRecordChannel>(events + "/ResultsReady.asset"),
                Scores = LoadOrCreate<ScoresChannel>(events + "/Scores.asset"),
                Settings = LoadOrCreate<SettingsChannel>(events + "/Settings.asset"),
            };

            s.Items = LoadOrCreate<ItemDatabase>(Root + "/Data/ItemDatabase.asset");
            s.Items.items = new[] { s.HealthPotion, s.ManaPotion, s.FatigueTonic, s.ReturnStone, s.MonsterCore, s.SteelDagger };
            EditorUtility.SetDirty(s.Items);
            s.Shop = LoadOrCreate<ShopData>(Root + "/Data/Shop_HunterSupply.asset", shop =>
            {
                shop.stock = new[] { s.HealthPotion, s.ManaPotion, s.FatigueTonic, s.ReturnStone, s.SteelDagger };
            });
            return s;
        }

        // ---------------------------------------------------------------- prefabs

        private static void BuildSystemsPrefabs(Context ctx)
        {
            var sparringMaterial = Mat("M_SparringGoblin_Body", new Color(0.45f, 0.5f, 0.6f), Color.black);
            ctx.Sparring = BuildEnemyVariant(ctx, ctx.Goblin, "SparringGoblin", ctx.Sys.Sparring, sparringMaterial, 1f, false);
            ctx.Potion = BuildPotionPrefab();
        }

        private static GameObject BuildPotionPrefab()
        {
            var liquid = Mat("M_HealthPotion_Liquid", new Color(0.6f, 0.05f, 0.08f), new Color(1.2f, 0.1f, 0.15f));
            var glass = Mat("M_HealthPotion_Glass", new Color(0.75f, 0.8f, 0.85f), null, 0.2f);
            var cork = Mat("M_HealthPotion_Cork", new Color(0.4f, 0.28f, 0.16f));

            var root = new GameObject("HealthPotion");
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            Primitive(PrimitiveType.Cylinder, "Bottle", root.transform, Vector3.zero, Vector3.zero,
                new Vector3(0.06f, 0.045f, 0.06f), liquid, true);
            Primitive(PrimitiveType.Cylinder, "Neck", root.transform, new Vector3(0f, 0.06f, 0f), Vector3.zero,
                new Vector3(0.025f, 0.02f, 0.025f), glass, false);
            Primitive(PrimitiveType.Cylinder, "Cork", root.transform, new Vector3(0f, 0.085f, 0f), Vector3.zero,
                new Vector3(0.028f, 0.008f, 0.028f), cork, false);
            SetLayerRecursively(root, LayerMask.NameToLayer("PlayerHands"));

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            return SavePrefab(root, Root + "/Prefabs/Items/HealthPotion.prefab");
        }

        // ---------------------------------------------------------------- core systems

        private sealed class AreaRefs
        {
            public Transform TitleSpawn, TutorialSpawn;
            public Transform[] TutorialRack;
            public TutorialSequence Tutorial;
            public EnemyDirector TutorialDirector;
        }

        /// <summary>Save, game state, flow, inventory, shop, scores, comfort, pause and potion systems.</summary>
        private static void BuildCoreSystems(Context ctx, Rig rig, Transform systems, Transform hubSpawn, AreaRefs areas,
            GameObject wristPanel, GameObject hud)
        {
            var d = ctx.Data;
            var s = ctx.Sys;

            var save = Child(systems, "SaveService").gameObject.AddComponent<SaveService>();
            Set(save, "_saveRequested", s.SaveRequested);
            Set(save, "_collect", s.CollectSave);
            Set(save, "_apply", s.ApplySave);
            Set(save, "_saveExists", s.SaveExists);

            var states = Child(systems, "GameStateManager").gameObject.AddComponent<GameStateManager>();
            SetInt(states, "_titleAreaId", TitleAreaId);
            SetInt(states, "_hubAreaId", HubAreaId);
            SetInt(states, "_tutorialAreaId", TutorialAreaId);
            Set(states, "_arrived", d.Arrived);
            Set(states, "_onGateCleared", d.OnGateCleared);
            Set(states, "_dailyQuest", d.DailyQuestStatus);
            Set(states, "_pauseToggle", s.PauseToggle);
            Set(states, "_state", s.GameState);
            Set(states, "_saveRequested", s.SaveRequested);

            var flow = Child(systems, "GameFlow").gameObject.AddComponent<GameFlow>();
            Set(flow, "_save", save);
            Set(flow, "_states", states);
            Set(flow, "_titleSpawn", areas.TitleSpawn);
            SetInt(flow, "_titleAreaId", TitleAreaId);
            Set(flow, "_hubSpawn", hubSpawn);
            SetInt(flow, "_hubAreaId", HubAreaId);
            Set(flow, "_tutorialSpawn", areas.TutorialSpawn);
            SetInt(flow, "_tutorialAreaId", TutorialAreaId);
            Set(flow, "_titleCommand", s.TitleCommand);
            Set(flow, "_endDay", s.EndDay);
            Set(flow, "_returnToTitle", s.ReturnToTitle);
            Set(flow, "_newGameStarted", s.NewGameStarted);
            Set(flow, "_saveRequested", s.SaveRequested);
            Set(flow, "_pauseToggle", s.PauseToggle);
            Set(flow, "_requests", d.RequestTravel);
            Set(flow, "_messages", d.Messages);

            var inventory = Child(systems, "InventoryService").gameObject.AddComponent<InventoryService>();
            Set(inventory, "_items", s.Items);
            Set(inventory, "_economy", d.Economy);
            Set(inventory, "_onItemGranted", s.ItemGranted);
            Set(inventory, "_onGoldGained", d.OnGoldGained);
            Set(inventory, "_onUseRequested", s.UseItemRequested);
            Set(inventory, "_onNewGame", s.NewGameStarted);
            Set(inventory, "_onCollectSave", s.CollectSave);
            Set(inventory, "_onApplySave", s.ApplySave);
            Set(inventory, "_status", s.Inventory);
            Set(inventory, "_onItemUsed", s.ItemUsed);
            Set(inventory, "_messages", d.Messages);

            var shop = Child(systems, "ShopService").gameObject.AddComponent<ShopService>();
            Set(shop, "_shop", s.Shop);
            Set(shop, "_inventory", inventory);
            Set(shop, "_onBuyRequested", s.BuyRequested);
            Set(shop, "_onSellRequested", s.SellRequested);
            Set(shop, "_messages", d.Messages);

            var scores = Child(systems, "ScoreBoard").gameObject.AddComponent<ScoreBoard>();
            Set(scores, "_config", s.Score);
            Set(scores, "_onRunCompleted", s.RunCompleted);
            Set(scores, "_onResultsReady", s.ResultsReady);
            Set(scores, "_scores", s.Scores);
            Set(scores, "_onCollectSave", s.CollectSave);
            Set(scores, "_onApplySave", s.ApplySave);

            var pause = Child(systems, "PauseInput").gameObject.AddComponent<PauseInput>();
            Set(pause, "_pauseToggle", s.PauseToggle);

            // Comfort presets and UI mode live on the rig they change.
            var comfort = rig.Origin.gameObject.AddComponent<ComfortSettings>();
            Set(comfort, "_config", s.Comfort);
            SetArray(comfort, "_controllers", rig.Origin.GetComponentsInChildren<ControllerInputActionManager>(true));
            Set(comfort, "_move", rig.Origin.GetComponentInChildren<ContinuousMoveProvider>(true));
            Set(comfort, "_vignette", rig.Origin.GetComponentInChildren<TunnelingVignetteController>(true));
            Set(comfort, "_wristPanel", wristPanel);
            Set(comfort, "_hud", hud);
            Set(comfort, "_setPreset", s.SetComfortPreset);
            Set(comfort, "_setUiMode", s.SetUiMode);
            Set(comfort, "_settings", s.Settings);
            Set(comfort, "_onCollectSave", s.CollectSave);
            Set(comfort, "_onApplySave", s.ApplySave);

            // ITM-4: potions are drunk from a chest holster and heal over time.
            var effects = rig.Origin.gameObject.AddComponent<ConsumableEffects>();
            Set(effects, "_health", rig.PlayerHealth);
            Set(effects, "_onItemUsed", s.ItemUsed);
            var bottle = (GameObject)PrefabUtility.InstantiatePrefab(ctx.Potion);
            bottle.transform.position = rig.Head.position + Vector3.down * 0.4f;
            var holster = rig.Origin.gameObject.AddComponent<PotionHolster>();
            Set(holster, "_head", rig.Head);
            Set(holster, "_bottle", bottle.GetComponent<XRGrabInteractable>());
            Set(holster, "_inventory", s.Inventory);
            Set(holster, "_useRequested", s.UseItemRequested);
        }

        // ---------------------------------------------------------------- title

        /// <summary>
        /// The title screen area: Continue / New Game / Tutorial / Scores / Quit, the fan-project disclaimer
        /// (GDD Legal &amp; IP item 4) and the standing-play note (CMF-13).
        /// </summary>
        private static Transform BuildTitle(Context ctx, Rig rig)
        {
            var mats = ctx.Mats;
            var s = ctx.Sys;
            var root = new GameObject("Title").transform;
            root.position = TitleOrigin;
            SimpleRoom(root, TitleOrigin + new Vector3(0f, 0f, 1f), 8f, 8f, 4f, mats.CaveFloor, mats.CaveWall, mats.CaveCeiling);
            PointLight(root, "Light_Title", TitleOrigin + new Vector3(0f, 3.2f, 1.5f), new Color(0.48f, 0.25f, 0.9f), 9f, 3f);
            PointLight(root, "Light_TitleFill", TitleOrigin + new Vector3(0f, 1.5f, -1f), new Color(0.25f, 0.72f, 1f), 5f, 1.2f);
            var spawn = Marker(root, "Title_Spawn", TitleOrigin + new Vector3(0f, 0f, -1f), Quaternion.identity);

            var header = NewCanvas("TitleHeader", root, new Vector2(1100f, 420f), 0.001f, null);
            header.transform.SetPositionAndRotation(TitleOrigin + new Vector3(0f, 2.15f, 1.6f), Quaternion.identity);
            var headerText = NewText("Text", header.transform, 10f, ctx.Font, 30f, TextAlignmentOptions.Center);
            headerText.color = UI.SystemPalette.Text;
            headerText.text =
                "<size=96><color=#3FB8FF><b>SOLO LEVELING</b></color></size>\n<size=64><color=#7B3FE4><b>AWAKENING</b></color></size>\n" +
                "<i>From the weakest hunter to the strongest.</i>\n\n" +
                "<size=20><color=#9AA4B4>Unofficial, non-commercial fan project made for a university course. Not affiliated with or endorsed by " +
                "D&C Media, Kakao Entertainment, Chugong, REDICE Studio, Netmarble or Aniplex.\nStanding play only. Clear your play space before you begin.</color></size>";

            var menu = NewCanvas("TitleMenu", root, new Vector2(520f, 560f), 0.001f, rig.Camera);
            menu.transform.SetPositionAndRotation(TitleOrigin + new Vector3(0f, 1.15f, 1.0f), Quaternion.Euler(15f, 0f, 0f));
            var background = NewImage("Background", menu.transform, 0f);
            background.color = UI.SystemPalette.Background;
            AddBorder(menu.transform, 5f);
            string[] labels = { "Continue", "New Game", "Tutorial", "Scores", "Quit" };
            var buttons = new ButtonParts[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                float top = 0.94f - i * 0.18f;
                buttons[i] = NewButton("Button_" + labels[i], menu.transform, labels[i], ctx.Font);
                SetRect((RectTransform)buttons[i].Button.transform, new Vector2(0.1f, top - 0.15f), new Vector2(0.9f, top), Vector2.zero, Vector2.zero);
            }
            WireIntButton(buttons[0].Button, s.TitleCommand, (int)TitleCommand.Continue);
            WireIntButton(buttons[1].Button, s.TitleCommand, (int)TitleCommand.NewGame);
            WireIntButton(buttons[2].Button, s.TitleCommand, (int)TitleCommand.Tutorial);
            WireIntButton(buttons[4].Button, s.TitleCommand, (int)TitleCommand.Quit);

            var scores = NewCanvas("ScoresPanel", root, new Vector2(620f, 460f), 0.001f, null);
            scores.transform.SetPositionAndRotation(TitleOrigin + new Vector3(0.95f, 1.35f, 0.75f), Quaternion.Euler(0f, 35f, 0f));
            NewImage("Background", scores.transform, 0f).color = UI.SystemPalette.Background;
            AddBorder(scores.transform, 5f);
            var scoresText = NewText("Text", scores.transform, 24f, ctx.Font, 28f, TextAlignmentOptions.TopLeft);
            scoresText.color = UI.SystemPalette.Text;
            var scoresPanel = scores.gameObject.AddComponent<ScoresPanel>();
            Set(scoresPanel, "_scores", s.Scores);
            Set(scoresPanel, "_text", scoresText);
            scores.gameObject.SetActive(false);

            var title = menu.gameObject.AddComponent<TitleMenu>();
            Set(title, "_saveExists", s.SaveExists);
            Set(title, "_continueButton", buttons[0].Button);
            Set(title, "_scoresPanel", scores.gameObject);
            UnityEventTools.AddPersistentListener(buttons[3].Button.onClick, new UnityAction(title.ToggleScores));
            return spawn;
        }

        // ---------------------------------------------------------------- tutorial

        /// <summary>The Training Grounds: a stand-in for the Prologue's tutorial path.</summary>
        private static void BuildTutorial(Context ctx, Rig rig, AreaRefs areas, XRGrabInteractable[] daggers, Transform hubSpawn)
        {
            var mats = ctx.Mats;
            var d = ctx.Data;
            var s = ctx.Sys;
            var root = new GameObject("Tutorial_TrainingGrounds").transform;
            root.position = TutorialOrigin;
            var geometry = SimpleRoom(root, TutorialOrigin, 8f, 8f, 3.2f, mats.CaveFloor, mats.HubWall, mats.CaveCeiling);
            SetLayerRecursively(geometry.gameObject, LayerMask.NameToLayer("Environment"));
            PointLight(root, "Light_Training", TutorialOrigin + new Vector3(0f, 2.9f, 0f), new Color(1f, 0.92f, 0.8f), 11f, 3f);

            areas.TutorialSpawn = Marker(root, "Tutorial_Spawn", TutorialOrigin + new Vector3(0f, 0f, -2.6f), Quaternion.identity);
            Box(root, "WeaponRack", TutorialOrigin + new Vector3(-1.2f, 0.425f, -2.2f), new Vector3(0.5f, 0.85f, 0.9f), mats.Rack);
            areas.TutorialRack = new[]
            {
                Marker(root, "RackSlot_Tutorial_A", TutorialOrigin + new Vector3(-1.3f, 0.88f, -2.2f), Quaternion.identity),
                Marker(root, "RackSlot_Tutorial_B", TutorialOrigin + new Vector3(-1.1f, 0.88f, -2.2f), Quaternion.identity),
            };
            var sparringSpawn = Marker(root, "Sparring_Spawn", TutorialOrigin + new Vector3(0f, 0f, 1.5f), Quaternion.Euler(0f, 180f, 0f));

            var directorGo = new GameObject("TutorialDirector");
            directorGo.transform.SetParent(root, false);
            var director = directorGo.AddComponent<EnemyDirector>();
            Set(director, "_combat", d.Combat);
            Set(director, "_progression", d.Progression);
            Set(director, "_economy", d.Economy);
            Set(director, "_gate", s.Training);
            Set(director, "_playerHead", rig.Head);
            Set(director, "_playerHealth", rig.PlayerHealth);
            Set(director, "_onXpGained", d.OnXpGained);
            Set(director, "_onGoldGained", d.OnGoldGained);
            Set(director, "_hits", d.Hits);
            Set(director, "_onEnemyKilled", s.OnEnemyKilled);
            Set(director, "_onItemGranted", s.ItemGranted);
            SetPools(director, (ctx.Sparring, 1));
            areas.TutorialDirector = director;

            var exit = BuildPortal(ctx, rig, root, "ExitPortal_Tutorial", TutorialOrigin + new Vector3(3.98f, 1.05f, 0f),
                Quaternion.Euler(0f, -90f, 0f), hubSpawn, HubAreaId);

            var panel = NewCanvas("TrainingPanel", root, new Vector2(900f, 260f), 0.001f, null);
            // UI-11: about 2 m from the start point, angled toward it.
            panel.transform.SetPositionAndRotation(TutorialOrigin + new Vector3(-1.5f, 1.75f, -1.1f), Quaternion.Euler(0f, -45f, 0f));
            NewImage("Background", panel.transform, 0f).color = UI.SystemPalette.Background;
            AddBorder(panel.transform, 5f);
            var panelText = NewText("Text", panel.transform, 24f, ctx.Font, 36f, TextAlignmentOptions.Center);
            panelText.color = UI.SystemPalette.Text;
            var instructions = panel.gameObject.AddComponent<InstructionPanel>();
            Set(instructions, "_instruction", s.TutorialInstruction);
            Set(instructions, "_text", panelText);

            var surface = geometry.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Environment");

            var tutorial = root.gameObject.AddComponent<TutorialSequence>();
            SetInt(tutorial, "_areaId", TutorialAreaId);
            Set(tutorial, "_navMesh", surface);
            Set(tutorial, "_director", director);
            Set(tutorial, "_sparringPrefab", ctx.Sparring);
            Set(tutorial, "_sparringSpawn", sparringSpawn);
            Set(tutorial, "_head", rig.Head);
            SetArray(tutorial, "_daggers", daggers);
            Set(tutorial, "_exitPortal", exit);
            Set(tutorial, "_arrived", d.Arrived);
            Set(tutorial, "_hits", d.Hits);
            Set(tutorial, "_instruction", s.TutorialInstruction);
            Set(tutorial, "_messages", d.Messages);
            Set(tutorial, "_saveRequested", s.SaveRequested);
            Set(tutorial, "_onNewGame", s.NewGameStarted);
            Set(tutorial, "_onCollectSave", s.CollectSave);
            Set(tutorial, "_onApplySave", s.ApplySave);
            areas.Tutorial = tutorial;
        }

        // ---------------------------------------------------------------- hub windows

        private static void BuildHubSystemWindows(Context ctx, Rig rig, Transform hub)
        {
            float wall = HubHalfSize - 0.03f;
            BuildInventoryWindow(ctx, rig, hub, new Vector3(wall, 1.45f, 0f), Quaternion.Euler(0f, 90f, 0f));
            BuildShopWindow(ctx, rig, hub, new Vector3(wall, 1.45f, 1.15f), Quaternion.Euler(0f, 90f, 0f));
            BuildEndDayPanel(ctx, rig, hub, new Vector3(0.9f, 1.4f, -wall), Quaternion.Euler(0f, 180f, 0f));
        }

        private static void BuildInventoryWindow(Context ctx, Rig rig, Transform parent, Vector3 position, Quaternion rotation)
        {
            var s = ctx.Sys;
            var canvas = NewCanvas("InventoryWindow", parent, new Vector2(640f, 560f), 0.001f, null);
            canvas.transform.SetPositionAndRotation(position, rotation);
            NewImage("Background", canvas.transform, 0f).color = UI.SystemPalette.Background;
            AddBorder(canvas.transform, 5f);
            var title = NewText("Title", canvas.transform, 0f, ctx.Font, 34f, TextAlignmentOptions.Left);
            SetRect((RectTransform)title.transform, new Vector2(0f, 0.88f), Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, -8f));
            title.text = "<color=#3FB8FF><b>INVENTORY</b></color>";
            var gold = NewText("Gold", canvas.transform, 0f, ctx.Font, 30f, TextAlignmentOptions.Right);
            SetRect((RectTransform)gold.transform, new Vector2(0f, 0.88f), Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, -8f));

            var slots = new TMP_Text[ctx.Data.Economy.inventorySlots];
            const int columns = 5;
            int rows = Mathf.CeilToInt(slots.Length / (float)columns);
            for (int i = 0; i < slots.Length; i++)
            {
                int col = i % columns;
                int row = i / columns;
                float x0 = 0.03f + col * (0.94f / columns);
                float y1 = 0.86f - row * (0.83f / rows);
                var cell = NewImage("Slot" + i, canvas.transform, 0f);
                cell.color = new Color(0.12f, 0.2f, 0.32f, 0.9f);
                SetRect(cell.rectTransform, new Vector2(x0, y1 - 0.83f / rows + 0.01f), new Vector2(x0 + 0.94f / columns - 0.01f, y1), Vector2.zero, Vector2.zero);
                var text = NewText("Text", cell.transform, 4f, ctx.Font, 20f, TextAlignmentOptions.Center);
                text.color = UI.SystemPalette.Text;
                slots[i] = text;
            }

            var window = canvas.gameObject.AddComponent<InventoryWindow>();
            Set(window, "_items", s.Items);
            Set(window, "_inventory", s.Inventory);
            Set(window, "_gold", gold);
            SetArray(window, "_slots", slots);
        }

        private static void BuildShopWindow(Context ctx, Rig rig, Transform parent, Vector3 position, Quaternion rotation)
        {
            var s = ctx.Sys;
            var canvas = NewCanvas("ShopWindow", parent, new Vector2(640f, 600f), 0.001f, rig.Camera);
            canvas.transform.SetPositionAndRotation(position, rotation);
            NewImage("Background", canvas.transform, 0f).color = UI.SystemPalette.Background;
            AddBorder(canvas.transform, 5f);
            var title = NewText("Title", canvas.transform, 0f, ctx.Font, 34f, TextAlignmentOptions.Left);
            SetRect((RectTransform)title.transform, new Vector2(0f, 0.89f), Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, -8f));
            var gold = NewText("Gold", canvas.transform, 0f, ctx.Font, 28f, TextAlignmentOptions.Right);
            SetRect((RectTransform)gold.transform, new Vector2(0f, 0.89f), Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, -8f));

            int count = s.Shop.stock.Length;
            var rows = new TMP_Text[count];
            var buttons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                float top = 0.86f - i * 0.13f;
                var row = NewText("Row" + i, canvas.transform, 0f, ctx.Font, 28f, TextAlignmentOptions.Left);
                row.color = UI.SystemPalette.Text;
                SetRect((RectTransform)row.transform, new Vector2(0f, top - 0.11f), new Vector2(0.72f, top), new Vector2(28f, 0f), Vector2.zero);
                rows[i] = row;
                var buy = NewButton("Buy" + i, canvas.transform, "Buy", ctx.Font);
                SetRect((RectTransform)buy.Button.transform, new Vector2(0.74f, top - 0.11f), new Vector2(0.95f, top - 0.01f), Vector2.zero, Vector2.zero);
                WireIntButton(buy.Button, s.BuyRequested, i);
                buttons[i] = buy.Button;
            }
            var sell = NewButton("SellMaterials", canvas.transform, "Sell monster cores", ctx.Font);
            SetRect((RectTransform)sell.Button.transform, new Vector2(0.15f, 0.03f), new Vector2(0.85f, 0.14f), Vector2.zero, Vector2.zero);
            WireVoidButton(sell.Button, s.SellRequested);

            var window = canvas.gameObject.AddComponent<ShopWindow>();
            Set(window, "_shop", s.Shop);
            Set(window, "_inventory", s.Inventory);
            Set(window, "_title", title);
            Set(window, "_gold", gold);
            SetArray(window, "_rows", rows);
            SetArray(window, "_buyButtons", buttons);
        }

        /// <summary>MRH-10: "End the day" saves and closes the session (back to the title screen).</summary>
        private static void BuildEndDayPanel(Context ctx, Rig rig, Transform parent, Vector3 position, Quaternion rotation)
        {
            var canvas = NewCanvas("EndDayPanel", parent, new Vector2(460f, 220f), 0.001f, rig.Camera);
            canvas.transform.SetPositionAndRotation(position, rotation);
            NewImage("Background", canvas.transform, 0f).color = UI.SystemPalette.Background;
            AddBorder(canvas.transform, 5f);
            var text = NewText("Text", canvas.transform, 0f, ctx.Font, 28f, TextAlignmentOptions.Center);
            SetRect((RectTransform)text.transform, new Vector2(0f, 0.55f), Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, -10f));
            text.color = UI.SystemPalette.Text;
            text.text = "Done for today?\nYour progress is saved.";
            var button = NewButton("EndDay", canvas.transform, "End the Day", ctx.Font);
            SetRect((RectTransform)button.Button.transform, new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.45f), Vector2.zero, Vector2.zero);
            WireVoidButton(button.Button, ctx.Sys.EndDay);
        }

        // ---------------------------------------------------------------- overlays

        /// <summary>Pause and settings, opened with the Menu button.</summary>
        private static void BuildPauseMenu(Context ctx, Rig rig)
        {
            var s = ctx.Sys;
            var canvas = NewCanvas("PauseMenu", null, new Vector2(560f, 560f), 0.001f, rig.Camera);
            var panel = UiPanel(canvas.transform, "Panel");
            NewImage("Background", panel, 0f).color = UI.SystemPalette.Background;
            AddBorder(panel, 5f);
            var title = NewText("Title", panel, 0f, ctx.Font, 40f, TextAlignmentOptions.Center);
            SetRect((RectTransform)title.transform, new Vector2(0f, 0.85f), Vector2.one, Vector2.zero, new Vector2(0f, -8f));
            title.text = "<color=#3FB8FF><b>PAUSED</b></color>";

            var resume = NewButton("Resume", panel, "Resume", ctx.Font);
            var preset = NewButton("Comfort", panel, "Comfort: Recommended", ctx.Font);
            var uiMode = NewButton("UiMode", panel, "UI: Diegetic", ctx.Font);
            var leave = NewButton("ReturnToTitle", panel, "Save and Return to Title", ctx.Font);
            var all = new[] { resume, preset, uiMode, leave };
            for (int i = 0; i < all.Length; i++)
            {
                float top = 0.8f - i * 0.19f;
                SetRect((RectTransform)all[i].Button.transform, new Vector2(0.08f, top - 0.16f), new Vector2(0.92f, top), Vector2.zero, Vector2.zero);
                all[i].Label.fontSize = 28f;
            }
            WireVoidButton(resume.Button, s.PauseToggle);
            WireVoidButton(leave.Button, s.ReturnToTitle);

            var menu = canvas.gameObject.AddComponent<PauseMenu>();
            Set(menu, "_head", rig.Head);
            Set(menu, "_panel", panel.gameObject);
            Set(menu, "_presetLabel", preset.Label);
            Set(menu, "_uiModeLabel", uiMode.Label);
            Set(menu, "_returnLabel", leave.Label);
            Set(menu, "_state", s.GameState);
            Set(menu, "_settings", s.Settings);
            Set(menu, "_setPreset", s.SetComfortPreset);
            Set(menu, "_setUiMode", s.SetUiMode);
            UnityEventTools.AddPersistentListener(preset.Button.onClick, new UnityAction(menu.CyclePreset));
            UnityEventTools.AddPersistentListener(uiMode.Button.onClick, new UnityAction(menu.ToggleUiMode));
        }

        /// <summary>The Results screen above the boss room's exit portal.</summary>
        private static void BuildResultsWindow(Context ctx, Transform gateRoot, Vector3 position)
        {
            var s = ctx.Sys;
            var canvas = NewCanvas("ResultsWindow", gateRoot, new Vector2(700f, 640f), 0.001f, null);
            canvas.transform.SetPositionAndRotation(position, Quaternion.identity);
            var panel = UiPanel(canvas.transform, "Panel");
            NewImage("Background", panel, 0f).color = UI.SystemPalette.Background;
            var border = AddBorder(panel, 5f);
            foreach (var strip in border)
            {
                strip.color = UI.SystemPalette.Reward;
            }
            var text = NewText("Text", panel, 26f, ctx.Font, 30f, TextAlignmentOptions.Center);
            text.color = UI.SystemPalette.Text;

            var window = canvas.gameObject.AddComponent<ResultsWindow>();
            Set(window, "_onResultsReady", s.ResultsReady);
            Set(window, "_arrived", ctx.Data.Arrived);
            SetInt(window, "_gateAreaId", GateAreaId);
            Set(window, "_panel", panel.gameObject);
            Set(window, "_text", text);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A closed box room: floor (teleportable), four walls and a ceiling. Returns the geometry root.</summary>
        private static Transform SimpleRoom(Transform parent, Vector3 center, float sizeX, float sizeZ, float height,
            Material floor, Material wall, Material ceiling)
        {
            var geometry = Child(parent, "Geometry");
            const float t = 0.3f;
            MakeTeleportFloor(Box(geometry, "Floor", center + new Vector3(0f, -0.1f, 0f), new Vector3(sizeX, 0.2f, sizeZ), floor));
            Box(geometry, "Wall_N", center + new Vector3(0f, height / 2f, sizeZ / 2f + t / 2f), new Vector3(sizeX + t * 2f, height, t), wall);
            Box(geometry, "Wall_S", center + new Vector3(0f, height / 2f, -sizeZ / 2f - t / 2f), new Vector3(sizeX + t * 2f, height, t), wall);
            Box(geometry, "Wall_E", center + new Vector3(sizeX / 2f + t / 2f, height / 2f, 0f), new Vector3(t, height, sizeZ), wall);
            Box(geometry, "Wall_W", center + new Vector3(-sizeX / 2f - t / 2f, height / 2f, 0f), new Vector3(t, height, sizeZ), wall);
            Box(parent, "Ceiling", center + new Vector3(0f, height + 0.1f, 0f), new Vector3(sizeX + t * 2f, 0.2f, sizeZ + t * 2f), ceiling);
            return geometry;
        }

        /// <summary>An empty UI object filling its parent, for grouping a window's contents.</summary>
        private static RectTransform UiPanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rect;
        }

        /// <summary>Lets the Comfortable preset's teleport land here (interaction layer "Teleport").</summary>
        private static void MakeTeleportFloor(GameObject floor)
        {
            var area = floor.AddComponent<TeleportationArea>();
            area.interactionLayers = InteractionLayerMask.GetMask("Teleport");
        }

        private static void WireIntButton(Button button, IntEventChannel channel, int value)
        {
            var action = button.gameObject.AddComponent<ChannelButton>();
            Set(action, "_intChannel", channel);
            SetInt(action, "_value", value);
            UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(action.Press));
        }

        private static void WireVoidButton(Button button, VoidEventChannel channel)
        {
            var action = button.gameObject.AddComponent<ChannelButton>();
            Set(action, "_voidChannel", channel);
            UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(action.Press));
        }
    }
}
