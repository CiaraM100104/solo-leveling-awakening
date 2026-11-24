using SLA.Quests;
using SLA.UI;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace SLA.Editor
{
    public static partial class BarebonesSandboxBuilder
    {
        private const float HubHalfSize = 2.5f;
        private const float HubHeight = 3f;

        private sealed class HubRefs
        {
            public Transform Spawn;
            public Transform[] RackSlots;
            public Transform PortalAnchor;
            public DailyQuest DailyQuest;
        }

        /// <summary>
        /// The virtual hub room (MRH-4 / MRH-12 Hub_Virtual): a 5 × 5 m bedroom with the gate on the north wall,
        /// the daily quest spot facing it (MRH-5), the Status window pinned to the west wall (MRH-7)
        /// and a table holding the daggers.
        /// </summary>
        private static HubRefs BuildHub(Context ctx, Rig rig)
        {
            var mats = ctx.Mats;
            var hub = new GameObject("Hub_Virtual").transform;
            var geometry = new GameObject("Geometry").transform;
            geometry.SetParent(hub, false);

            float h = HubHalfSize;
            const float t = 0.3f;
            Box(geometry, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(h * 2f, 0.2f, h * 2f), mats.HubFloor);
            Box(geometry, "Wall_N", new Vector3(0f, HubHeight / 2f, h + t / 2f), new Vector3(h * 2f + t * 2f, HubHeight, t), mats.HubWall);
            Box(geometry, "Wall_S", new Vector3(0f, HubHeight / 2f, -h - t / 2f), new Vector3(h * 2f + t * 2f, HubHeight, t), mats.HubWall);
            Box(geometry, "Wall_E", new Vector3(h + t / 2f, HubHeight / 2f, 0f), new Vector3(t, HubHeight, h * 2f), mats.HubWall);
            Box(geometry, "Wall_W", new Vector3(-h - t / 2f, HubHeight / 2f, 0f), new Vector3(t, HubHeight, h * 2f), mats.HubWall);
            Box(geometry, "Ceiling", new Vector3(0f, HubHeight + 0.1f, 0f), new Vector3(h * 2f + t * 2f, 0.2f, h * 2f + t * 2f), mats.HubWall);

            var furniture = new GameObject("Furniture").transform;
            furniture.SetParent(hub, false);
            Box(furniture, "Bed", new Vector3(-1.75f, 0.22f, -1.45f), new Vector3(1.3f, 0.44f, 2f), mats.HubFurniture);
            Box(furniture, "Blanket", new Vector3(-1.75f, 0.47f, -1.25f), new Vector3(1.25f, 0.06f, 1.5f), mats.HubBedding);
            Box(furniture, "Desk", new Vector3(-1.95f, 0.38f, 1.6f), new Vector3(0.8f, 0.76f, 1.3f), mats.HubFurniture);

            // Weapon table: the daggers rest here whenever the player is home.
            Box(furniture, "WeaponTable", new Vector3(1.6f, 0.425f, -1.2f), new Vector3(0.9f, 0.85f, 0.5f), mats.Rack);
            var rackSlots = new[]
            {
                Marker(hub, "RackSlot_Hub_A", new Vector3(1.6f, 0.88f, -1.3f), Quaternion.Euler(0f, -90f, 0f)),
                Marker(hub, "RackSlot_Hub_B", new Vector3(1.6f, 0.88f, -1.1f), Quaternion.Euler(0f, -90f, 0f)),
            };

            PointLight(hub, "Light_Ceiling", new Vector3(0f, 2.7f, 0f), new Color(1f, 0.86f, 0.7f), 8f, 1.6f);
            PointLight(hub, "Light_GateGlow", new Vector3(0f, 1.2f, 1.9f), new Color(0.25f, 0.72f, 1f), 3.5f, 1.5f);

            // MRH-3: about 1.2 × 2 m, 2 cm off the wall, centred 1.05 m up, facing into the room.
            var portalAnchor = Marker(hub, "GatePortal_Anchor", new Vector3(0f, 1.05f, h - 0.02f), Quaternion.Euler(0f, 180f, 0f));
            var spawn = Marker(hub, "Hub_Spawn", new Vector3(0f, 0f, -1.2f), Quaternion.identity);

            // MRH-5: the squat spot faces the gate. The pads appear around the player's head during punch rounds.
            var squatSpot = Marker(hub, "SquatSpot", new Vector3(0f, 0f, 0.3f), Quaternion.identity);
            Primitive(PrimitiveType.Cylinder, "Ring", squatSpot, new Vector3(0f, 0.006f, 0f), Vector3.zero,
                new Vector3(0.7f, 0.004f, 0.7f), mats.SquatSpot, false);
            var padRoot = new GameObject("PunchPads").transform;
            padRoot.SetParent(squatSpot, false);
            var pads = new Transform[4];
            var padRenderers = new Renderer[4];
            string[] padNames = { "Pad_Jab", "Pad_Cross", "Pad_Hook", "Pad_Uppercut" };
            for (int i = 0; i < pads.Length; i++)
            {
                var pad = Primitive(PrimitiveType.Sphere, padNames[i], padRoot, Vector3.zero, Vector3.zero,
                    Vector3.one * 0.2f, mats.Pad, false);
                pads[i] = pad.transform;
                padRenderers[i] = pad.GetComponent<Renderer>();
            }

            var questGo = new GameObject("DailyQuest");
            questGo.transform.SetParent(hub, false);
            var questAudio = questGo.AddComponent<AudioSource>();
            questAudio.playOnAwake = false;
            var quest = questGo.AddComponent<DailyQuest>();
            var d = ctx.Data;
            Set(quest, "_config", d.DailyQuest);
            SetInt(quest, "_hubAreaId", HubAreaId);
            Set(quest, "_head", rig.Head);
            Set(quest, "_floor", rig.Origin.transform);
            Set(quest, "_leftHand", rig.LeftHand);
            Set(quest, "_rightHand", rig.RightHand);
            Set(quest, "_padRoot", padRoot);
            SetArray(quest, "_pads", pads);
            SetArray(quest, "_padRenderers", padRenderers);
            Set(quest, "_audio", questAudio);
            Set(quest, "_repClip", ctx.Sfx.Rep);
            Set(quest, "_playerStatus", d.PlayerStatus);
            Set(quest, "_progress", d.DailyQuestStatus);
            Set(quest, "_startRequested", d.DailyQuestStart);
            Set(quest, "_restRequested", d.DailyQuestRest);
            Set(quest, "_onCompleted", d.OnDailyQuestCompleted);
            Set(quest, "_messages", d.Messages);
            Set(quest, "_arrived", d.Arrived);

            BuildGatePanel(ctx, hub, new Vector3(0f, 2.45f, h - 0.03f), Quaternion.identity);
            BuildDailyQuestWindow(ctx, rig, hub, new Vector3(1.55f, 1.5f, h - 0.03f), Quaternion.identity);
            BuildStatusWindow(ctx, rig, hub, new Vector3(-h + 0.03f, 1.45f, 0f), Quaternion.Euler(0f, -90f, 0f));

            return new HubRefs { Spawn = spawn, RackSlots = rackSlots, PortalAnchor = portalAnchor, DailyQuest = quest };
        }

        private static void BuildGatePanel(Context ctx, Transform parent, Vector3 position, Quaternion rotation)
        {
            var canvas = NewCanvas("GatePanel", parent, new Vector2(700f, 270f), 0.001f, null);
            canvas.transform.SetPositionAndRotation(position, rotation);
            var background = NewImage("Background", canvas.transform, 0f);
            var border = AddBorder(canvas.transform, 5f);
            var text = NewText("Text", canvas.transform, 18f, ctx.Font, 34f, TMPro.TextAlignmentOptions.Center);

            var panel = canvas.gameObject.AddComponent<GatePanel>();
            Set(panel, "_gate", ctx.Data.GoblinWarren);
            SetArray(panel, "_border", border);
            Set(panel, "_background", background);
            Set(panel, "_text", text);
        }

        private static void BuildDailyQuestWindow(Context ctx, Rig rig, Transform parent, Vector3 position, Quaternion rotation)
        {
            var canvas = NewCanvas("DailyQuestWindow", parent, new Vector2(760f, 380f), 0.001f, rig.Camera);
            canvas.transform.SetPositionAndRotation(position, rotation);
            var background = NewImage("Background", canvas.transform, 0f);
            var text = NewText("Text", canvas.transform, 0f, ctx.Font, 34f, TMPro.TextAlignmentOptions.TopLeft);
            SetRect((RectTransform)text.transform, new Vector2(0f, 0.25f), Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, -20f));

            var start = NewButton("StartButton", canvas.transform, "Start", ctx.Font);
            SetRect((RectTransform)start.Button.transform, new Vector2(0.08f, 0.04f), new Vector2(0.46f, 0.22f), Vector2.zero, Vector2.zero);
            var rest = NewButton("RestButton", canvas.transform, "Rest", ctx.Font);
            SetRect((RectTransform)rest.Button.transform, new Vector2(0.54f, 0.04f), new Vector2(0.92f, 0.22f), Vector2.zero, Vector2.zero);

            var window = canvas.gameObject.AddComponent<DailyQuestWindow>();
            var d = ctx.Data;
            Set(window, "_progress", d.DailyQuestStatus);
            Set(window, "_startRequested", d.DailyQuestStart);
            Set(window, "_restRequested", d.DailyQuestRest);
            Set(window, "_background", background);
            Set(window, "_text", text);
            Set(window, "_startButton", start.Button);
            Set(window, "_startLabel", start.Label);
            Set(window, "_restButton", rest.Button);
            UnityEventTools.AddPersistentListener(start.Button.onClick, new UnityAction(window.StartQuest));
            UnityEventTools.AddPersistentListener(rest.Button.onClick, new UnityAction(window.Rest));
        }

        private static void BuildStatusWindow(Context ctx, Rig rig, Transform parent, Vector3 position, Quaternion rotation)
        {
            var canvas = NewCanvas("StatusWindow", parent, new Vector2(640f, 660f), 0.001f, rig.Camera);
            canvas.transform.SetPositionAndRotation(position, rotation);
            var background = NewImage("Background", canvas.transform, 0f);

            var title = NewText("Title", canvas.transform, 0f, ctx.Font, 40f, TMPro.TextAlignmentOptions.Center);
            SetRect((RectTransform)title.transform, new Vector2(0f, 0.9f), Vector2.one, Vector2.zero, new Vector2(0f, -8f));
            title.text = "<color=#3FB8FF><b>STATUS</b></color>";

            var summary = NewText("Summary", canvas.transform, 0f, ctx.Font, 28f, TMPro.TextAlignmentOptions.TopLeft);
            SetRect((RectTransform)summary.transform, new Vector2(0f, 0.64f), new Vector2(1f, 0.9f), new Vector2(28f, 0f), new Vector2(-28f, 0f));

            var statTexts = new TMPro.TMP_Text[5];
            var buttons = new UnityEngine.UI.Button[5];
            for (int i = 0; i < 5; i++)
            {
                float top = 0.62f - i * 0.1f;
                var statText = NewText("Stat" + i, canvas.transform, 0f, ctx.Font, 30f, TMPro.TextAlignmentOptions.Left);
                SetRect((RectTransform)statText.transform, new Vector2(0f, top - 0.09f), new Vector2(0.75f, top), new Vector2(40f, 0f), Vector2.zero);
                statTexts[i] = statText;

                var plus = NewButton("Plus" + i, canvas.transform, "+", ctx.Font);
                SetRect((RectTransform)plus.Button.transform, new Vector2(0.78f, top - 0.088f), new Vector2(0.9f, top - 0.002f), Vector2.zero, Vector2.zero);
                buttons[i] = plus.Button;
            }

            var points = NewText("Points", canvas.transform, 0f, ctx.Font, 30f, TMPro.TextAlignmentOptions.Center);
            SetRect((RectTransform)points.transform, new Vector2(0f, 0.02f), new Vector2(1f, 0.11f), Vector2.zero, Vector2.zero);

            var window = canvas.gameObject.AddComponent<StatusWindow>();
            Set(window, "_status", ctx.Data.PlayerStatus);
            Set(window, "_allocateStat", ctx.Data.OnAllocateStat);
            Set(window, "_background", background);
            Set(window, "_summary", summary);
            SetArray(window, "_statTexts", statTexts);
            SetArray(window, "_plusButtons", buttons);
            Set(window, "_points", points);
            for (int i = 0; i < 5; i++)
            {
                UnityEventTools.AddIntPersistentListener(buttons[i].onClick, new UnityAction<int>(window.Allocate), i);
            }
        }
    }
}
