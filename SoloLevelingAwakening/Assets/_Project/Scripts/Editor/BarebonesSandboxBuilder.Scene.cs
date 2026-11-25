using SLA.AI;
using SLA.Combat;
using SLA.DebugTools;
using SLA.Gates;
using SLA.Progression;
using SLA.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SLA.Editor
{
    public static partial class BarebonesSandboxBuilder
    {
        private sealed class Rig
        {
            public XROrigin Origin;
            public Camera Camera;
            public Transform Head;
            public Transform LeftHand;
            public Transform RightHand;
            public CharacterController Body;
            public Health PlayerHealth;
        }

        private static readonly Color HubAmbient = new Color(0.33f, 0.31f, 0.29f);
        private static readonly Color GateAmbient = new Color(0.24f, 0.25f, 0.36f);

        private static void BuildScene(Scene scene, Context ctx, GameObject originPrefab, GameObject vignettePrefab)
        {
            var d = ctx.Data;

            // Art direction: dark and high contrast. No sky; each area sets its own ambient light.
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = HubAmbient;

            var rig = BuildRig(ctx, scene, originPrefab, vignettePrefab);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<XRUIInputModule>();

            var hub = BuildHub(ctx, rig);
            var gate = BuildGate(ctx, rig, hub.Spawn);
            var hubRoot = hub.Spawn.parent;
            BuildPortal(ctx, rig, hubRoot, "GatePortal_GoblinWarren", hub.PortalAnchor.position, hub.PortalAnchor.rotation,
                gate.Spawn, GateAreaId);
            BuildHubSystemWindows(ctx, rig, hubRoot);

            // The game starts on the title screen (screen flow: Boot → Title).
            var areas = new AreaRefs { TitleSpawn = BuildTitle(ctx, rig) };
            rig.Origin.transform.SetPositionAndRotation(areas.TitleSpawn.position, areas.TitleSpawn.rotation);

            // Dual Rusty Daggers on the hub table; they follow the player to each area's rack.
            var daggers = new XRGrabInteractable[2];
            for (int i = 0; i < daggers.Length; i++)
            {
                var dagger = (GameObject)PrefabUtility.InstantiatePrefab(ctx.Dagger, scene);
                dagger.name = i == 0 ? "RustyDagger_L" : "RustyDagger_R";
                dagger.transform.SetPositionAndRotation(hub.RackSlots[i].position, hub.RackSlots[i].rotation);
                if (rig.Body != null)
                {
                    SetArray(dagger.GetComponent<WeaponHitDetector>(), "_ignoredColliders", rig.Body);
                }
                daggers[i] = dagger.GetComponent<XRGrabInteractable>();
            }

            BuildTutorial(ctx, rig, areas, daggers, hub.Spawn);

            var systems = new GameObject("Systems").transform;
            var stowing = Child(systems, "WeaponStowing").gameObject.AddComponent<WeaponStowing>();
            SetArray(stowing, "_weapons", daggers);
            SetRacks(stowing, (HubAreaId, hub.RackSlots), (GateAreaId, gate.RackSlots), (TutorialAreaId, areas.TutorialRack));
            Set(stowing, "_travelStarting", d.TravelStarting);
            Set(stowing, "_arrived", d.Arrived);

            var ambience = Child(systems, "AreaAmbience").gameObject.AddComponent<AreaAmbience>();
            SetInt(ambience, "_startAreaId", TitleAreaId);
            SetAreaLooks(ambience, (HubAreaId, HubAmbient), (GateAreaId, GateAmbient), (TitleAreaId, TitleAmbient),
                (TutorialAreaId, TutorialAmbient));
            Set(ambience, "_arrived", d.Arrived);

            BuildSystemWindow(ctx, rig);
            var wrist = BuildWristPanel(ctx, rig);
            var hud = BuildHud(ctx, rig);
            BuildDamageNumbers(ctx, rig, systems);
            BuildHitSparks(ctx, systems);
            BuildPauseMenu(ctx, rig);
            BuildCoreSystems(ctx, rig, systems, hub.Spawn, areas, wrist, hud);

            var s = ctx.Sys;
            var cheats = Child(systems, "DebugCheats (editor only)").gameObject.AddComponent<DebugCheats>();
            Set(cheats, "_head", rig.Head);
            SetArray(cheats, "_directors", gate.Director, areas.TutorialDirector);
            Set(cheats, "_dailyQuest", hub.DailyQuest);
            Set(cheats, "_onXpGained", d.OnXpGained);
            Set(cheats, "_requests", d.RequestTravel);
            SetCheckpoints(cheats, hub.Spawn, gate.Checkpoints);
            Set(cheats, "_onGoldGained", d.OnGoldGained);
            Set(cheats, "_onItemGranted", s.ItemGranted);
            Set(cheats, "_tutorial", areas.Tutorial);
            Set(cheats, "_saveRequested", s.SaveRequested);
        }

        private static Rig BuildRig(Context ctx, Scene scene, GameObject originPrefab, GameObject vignettePrefab)
        {
            var d = ctx.Data;
            var originGo = (GameObject)PrefabUtility.InstantiatePrefab(originPrefab, scene);
            var rig = new Rig
            {
                Origin = originGo.GetComponent<XROrigin>(),
                Camera = originGo.GetComponentInChildren<Camera>(true),
                LeftHand = FindDeep(originGo.transform, "Left Controller"),
                RightHand = FindDeep(originGo.transform, "Right Controller"),
                Body = originGo.GetComponentInChildren<CharacterController>(true),
            };
            rig.Head = rig.Camera.transform;
            rig.Camera.clearFlags = CameraClearFlags.SolidColor;
            rig.Camera.backgroundColor = Color.black;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.Camera);

            // CMF Recommended preset: smooth 1.5 m/s, snap 45°, vignette while moving.
            var move = originGo.GetComponentInChildren<ContinuousMoveProvider>(true);
            if (move != null)
            {
                move.moveSpeed = 1.5f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(move);
            }
            var snap = originGo.GetComponentInChildren<SnapTurnProvider>(true);
            if (snap != null)
            {
                snap.turnAmount = 45f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(snap);
            }
            if (vignettePrefab != null && move != null)
            {
                var vignette = (GameObject)PrefabUtility.InstantiatePrefab(vignettePrefab, rig.Camera.transform);
                var controller = vignette.GetComponent<TunnelingVignetteController>();
                controller.locomotionVignetteProviders.Clear();
                controller.locomotionVignetteProviders.Add(new LocomotionVignetteProvider
                {
                    locomotionProvider = move,
                    enabled = true,
                });
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            }

            // Player stats, health and hurt sound.
            rig.PlayerHealth = originGo.AddComponent<Health>();
            var audio = originGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var progression = originGo.AddComponent<PlayerProgression>();
            Set(progression, "_config", d.Progression);
            Set(progression, "_health", rig.PlayerHealth);
            Set(progression, "_audio", audio);
            Set(progression, "_hurtClip", ctx.Sfx.Hurt);
            Set(progression, "_onXpGained", d.OnXpGained);
            Set(progression, "_onAllocateStat", d.OnAllocateStat);
            Set(progression, "_onDailyQuestCompleted", d.OnDailyQuestCompleted);
            Set(progression, "_onNewGame", ctx.Sys.NewGameStarted);
            Set(progression, "_onCollectSave", ctx.Sys.CollectSave);
            Set(progression, "_onApplySave", ctx.Sys.ApplySave);
            Set(progression, "_onLevelUp", d.OnLevelUp);
            Set(progression, "_onPlayerDied", d.OnPlayerDied);
            Set(progression, "_onPlayerDamaged", ctx.Sys.OnPlayerDamaged);
            Set(progression, "_status", d.PlayerStatus);

            // Fade shell around the head, and travel.
            var shell = Primitive(PrimitiveType.Sphere, "ScreenFade", rig.Camera.transform, Vector3.zero, Vector3.zero,
                Vector3.one * 0.3f, ctx.Mats.Fade, false);
            var shellRenderer = shell.GetComponent<MeshRenderer>();
            shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var fade = shell.AddComponent<ScreenFade>();
            Set(fade, "_renderer", shellRenderer);

            var travel = originGo.AddComponent<PlayerTravel>();
            Set(travel, "_origin", rig.Origin);
            Set(travel, "_body", rig.Body);
            Set(travel, "_fade", fade);
            Set(travel, "_requests", d.RequestTravel);
            Set(travel, "_arrived", d.Arrived);
            Set(travel, "_travelStarting", d.TravelStarting);
            return rig;
        }

        private static void BuildSystemWindow(Context ctx, Rig rig)
        {
            var d = ctx.Data;
            var canvas = NewCanvas("SystemWindow", null, new Vector2(620f, 170f), 0.001f, null);
            canvas.transform.SetPositionAndRotation(rig.Head.position + new Vector3(0f, -0.25f, 1.2f), Quaternion.identity);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var background = NewImage("Background", canvas.transform, 0f);
            var border = AddBorder(canvas.transform, 5f);
            var text = NewText("Text", canvas.transform, 20f, ctx.Font, 30f, TextAlignmentOptions.Center);

            var audio = canvas.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var window = canvas.gameObject.AddComponent<SystemWindow>();
            Set(window, "_group", group);
            SetArray(window, "_border", border);
            Set(window, "_background", background);
            Set(window, "_text", text);
            Set(window, "_audio", audio);
            Set(window, "_chime", ctx.Sfx.Chime);

            var service = canvas.gameObject.AddComponent<SystemMessageService>();
            Set(service, "_window", window);
            Set(service, "_messages", d.Messages);
            Set(service, "_onLevelUp", d.OnLevelUp);
            Set(service, "_onPlayerDied", d.OnPlayerDied);

            // UI-3: 1.2 m ahead, about 12° below eye level, lazy-following the head.
            var follow = canvas.gameObject.AddComponent<LazyFollow>();
            follow.target = rig.Head;
            follow.targetOffset = new Vector3(0f, -0.25f, 1.2f);
            follow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
            follow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
        }

        private static GameObject BuildWristPanel(Context ctx, Rig rig)
        {
            if (rig.LeftHand == null)
            {
                return null;
            }
            var canvas = StatusBars(ctx, "WristPanel", rig.LeftHand, 0.0004f);
            canvas.transform.localPosition = new Vector3(0f, 0.05f, -0.05f);
            canvas.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            return canvas.gameObject;
        }

        /// <summary>UI-15 HUD mode: the same bars fixed at the lower left of the view. Off unless chosen in settings.</summary>
        private static GameObject BuildHud(Context ctx, Rig rig)
        {
            var canvas = StatusBars(ctx, "HudPanel", rig.Head, 0.0006f);
            canvas.transform.localPosition = new Vector3(-0.28f, -0.22f, 0.9f);
            canvas.transform.localRotation = Quaternion.Euler(10f, -12f, 0f);
            canvas.gameObject.SetActive(false);
            return canvas.gameObject;
        }

        private static Canvas StatusBars(Context ctx, string name, Transform parent, float metersPerPixel)
        {
            var canvas = NewCanvas(name, parent, new Vector2(460f, 230f), metersPerPixel, null);
            var background = NewImage("Background", canvas.transform, 0f);
            var text = NewText("Text", canvas.transform, 0f, ctx.Font, 24f, TextAlignmentOptions.TopLeft);
            SetRect((RectTransform)text.transform, new Vector2(0f, 0.46f), Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, -8f));
            var hpFill = Bar(canvas.transform, "HpBar", new Vector2(0.04f, 0.31f), new Vector2(0.96f, 0.41f), UI.SystemPalette.Warning);
            var mpFill = Bar(canvas.transform, "MpBar", new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.27f), UI.SystemPalette.Info);
            var xpFill = Bar(canvas.transform, "XpBar", new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.13f), UI.SystemPalette.Reward);

            var panel = canvas.gameObject.AddComponent<WristPanel>();
            Set(panel, "_status", ctx.Data.PlayerStatus);
            Set(panel, "_inventory", ctx.Sys.Inventory);
            Set(panel, "_text", text);
            Set(panel, "_background", background);
            Set(panel, "_hpFill", hpFill);
            Set(panel, "_mpFill", mpFill);
            Set(panel, "_xpFill", xpFill);
            return canvas;
        }

        private static void BuildDamageNumbers(Context ctx, Rig rig, Transform parent)
        {
            var root = Child(parent, "DamageNumbers");
            var pool = new TextMeshPro[10];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Number_" + i, typeof(RectTransform));
                go.transform.SetParent(root, false);
                ((RectTransform)go.transform).sizeDelta = new Vector2(2f, 0.5f);
                var text = go.AddComponent<TextMeshPro>();
                text.font = ctx.Font;
                text.fontSize = 1.2f;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
                text.text = string.Empty;
                pool[i] = text;
            }
            var numbers = root.gameObject.AddComponent<DamageNumbers>();
            Set(numbers, "_hits", ctx.Data.Hits);
            Set(numbers, "_head", rig.Head);
            SetArray(numbers, "_pool", pool);
        }
    }
}
