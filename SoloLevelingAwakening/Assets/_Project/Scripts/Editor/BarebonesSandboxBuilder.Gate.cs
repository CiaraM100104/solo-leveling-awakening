using SLA.AI;
using SLA.Data;
using SLA.Gates;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SLA.Editor
{
    public static partial class BarebonesSandboxBuilder
    {
        /// <summary>The gate sits far from the hub in the same scene until each area becomes its own scene.</summary>
        private static readonly Vector3 GateOrigin = new Vector3(100f, 0f, 0f);

        private const float WallHeight = 3.2f;   // GTE-3: ceilings at least 3 m.
        private const float WallThickness = 0.3f;
        private const float DoorWidth = 2.6f;    // GTE-3: corridors at least 2.5 m wide.

        private sealed class GateRefs
        {
            public Transform Spawn;
            public Transform[] RackSlots;
            public EnemyDirector Director;
            public Transform[] Checkpoints;
        }

        private sealed class GateParts
        {
            public Transform Geometry, Ceilings, Decor, Root;
        }

        /// <summary>
        /// Goblin Warren, an E-rank blue gate on the GTE-1 template: entrance hall (safe, respawn, exit back home)
        /// → combat room 1 → combat room 2 → antechamber → boss room (Goblin Chieftain). Low-light cave (gates.md).
        /// Rooms 8 × 8 m, corridors 2.6 m, boss room 12 × 12 m, ceilings 3.2 m (GTE-3). Flat floors (CMF-3).
        /// </summary>
        private static GateRefs BuildGate(Context ctx, Rig rig, Transform hubSpawn)
        {
            var mats = ctx.Mats;
            var d = ctx.Data;
            var root = new GameObject("Gate_GoblinWarren").transform;
            root.position = GateOrigin;
            var parts = new GateParts { Root = root };
            parts.Geometry = Child(root, "Geometry");
            parts.Ceilings = Child(root, "Ceilings");
            parts.Decor = Child(root, "Decor");

            var violet = new Color(0.48f, 0.25f, 0.9f);
            var blue = new Color(0.25f, 0.72f, 1f);

            // Entrance hall (safe; GTE-4 respawn point).
            Room(ctx, parts, "Entrance", 3f, 6f, 6f, false, true);
            PointLight(root, "Light_Entrance", Local(0f, 2.8f, 3f), blue, 9f, 4f);
            Crystal(ctx, parts, Local(-2.6f, 0.5f, 5.2f), mats.CrystalBlue);
            Box(parts.Decor, "WeaponTable", Local(1.6f, 0.425f, 2.6f), new Vector3(0.5f, 0.85f, 0.9f), mats.Rack);
            var rackSlots = new[]
            {
                Marker(root, "RackSlot_Gate_A", Local(1.5f, 0.88f, 2.6f), Quaternion.identity),
                Marker(root, "RackSlot_Gate_B", Local(1.7f, 0.88f, 2.6f), Quaternion.identity),
            };
            var spawn = Marker(root, "GateEntrance_Spawn", Local(0f, 0f, 1.4f), Quaternion.identity);

            Corridor(ctx, parts, 6f, 9f);
            var room1Barrier = Barrier(ctx, root, "Barrier_Room1", 17f);
            var room1 = CombatRoom(ctx, rig, parts, "Room1", 13f, 8f, 8f, d.Room1, room1Barrier, violet,
                new[] { Local(-2.6f, 0f, 15.6f), Local(-0.9f, 0f, 15.6f), Local(0.9f, 0f, 15.6f), Local(2.6f, 0f, 15.6f) });

            Corridor(ctx, parts, 17f, 20f);
            var room2Barrier = Barrier(ctx, root, "Barrier_Room2", 28f);
            var room2 = CombatRoom(ctx, rig, parts, "Room2", 24f, 8f, 8f, d.Room2, room2Barrier, blue,
                new[] { Local(-2.6f, 0f, 26.6f), Local(-0.9f, 0f, 26.6f), Local(0.9f, 0f, 26.6f), Local(2.6f, 0f, 26.6f) });

            Corridor(ctx, parts, 28f, 31f);
            Room(ctx, parts, "Antechamber", 33.5f, 5f, 5f, true, true);
            PointLight(root, "Light_Antechamber", Local(0f, 2.8f, 33.5f), violet, 8f, 3.5f);
            Crystal(ctx, parts, Local(2.1f, 0.6f, 35.4f), mats.CrystalViolet);

            Corridor(ctx, parts, 36f, 39f);
            var boss = CombatRoom(ctx, rig, parts, "BossRoom", 45f, 12f, 12f, d.BossRoom, null, violet,
                new[] { Local(0f, 0f, 49.5f), Local(-4f, 0f, 48.5f), Local(4f, 0f, 48.5f) }, northOpening: false);
            PointLight(root, "Light_BossRoom_2", Local(0f, 2.9f, 49f), new Color(0.9f, 0.2f, 0.3f), 10f, 3f);

            // Enemies live outside the geometry so the NavMesh ignores them.
            var directorGo = new GameObject("EnemyDirector");
            directorGo.transform.SetParent(root, false);
            var director = directorGo.AddComponent<EnemyDirector>();
            Set(director, "_combat", d.Combat);
            Set(director, "_progression", d.Progression);
            Set(director, "_economy", d.Economy);
            Set(director, "_gate", d.GoblinWarren);
            Set(director, "_playerHead", rig.Head);
            Set(director, "_playerHealth", rig.PlayerHealth);
            Set(director, "_onXpGained", d.OnXpGained);
            Set(director, "_onGoldGained", d.OnGoldGained);
            Set(director, "_hits", d.Hits);
            Set(director, "_onEnemyKilled", ctx.Sys.OnEnemyKilled);
            Set(director, "_onItemGranted", ctx.Sys.ItemGranted);
            Set(director, "_potionDrop", ctx.Sys.HealthPotion);
            Set(director, "_monsterCore", ctx.Sys.MonsterCore);
            SetPools(director, (ctx.Goblin, 4), (ctx.Kobold, 4), (ctx.Chieftain, 1));
            foreach (var room in new[] { room1, room2, boss })
            {
                Set(room, "_director", director);
            }

            // Portals: the gate you came through (blue gates can be left, GTE-6) and the exit after the boss (GTE-5).
            BuildPortal(ctx, rig, root, "ExitPortal_Entrance", Local(0f, 1.05f, 0.02f), Quaternion.identity, hubSpawn, HubAreaId);
            var bossExit = BuildPortal(ctx, rig, root, "ExitPortal_Boss", Local(0f, 1.05f, 50.98f), Quaternion.Euler(0f, 180f, 0f), hubSpawn, HubAreaId);

            SetLayerRecursively(parts.Geometry.gameObject, LayerMask.NameToLayer("Environment"));
            var surface = parts.Geometry.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Environment");

            var run = root.gameObject.AddComponent<GateRun>();
            Set(run, "_gate", d.GoblinWarren);
            SetInt(run, "_areaId", GateAreaId);
            Set(run, "_navMesh", surface);
            SetArray(run, "_rooms", room1, room2, boss);
            Set(run, "_exitPortal", bossExit);
            Set(run, "_entranceSpawn", spawn);
            Set(run, "_requests", d.RequestTravel);
            Set(run, "_arrived", d.Arrived);
            Set(run, "_onPlayerDied", d.OnPlayerDied);
            Set(run, "_onGateCleared", d.OnGateCleared);
            Set(run, "_messages", d.Messages);
            Set(run, "_onEnemyKilled", ctx.Sys.OnEnemyKilled);
            Set(run, "_onXpGained", d.OnXpGained);
            Set(run, "_onGoldGained", d.OnGoldGained);
            Set(run, "_onPlayerDamaged", ctx.Sys.OnPlayerDamaged);
            Set(run, "_onRunCompleted", ctx.Sys.RunCompleted);
            Set(run, "_onNewGame", ctx.Sys.NewGameStarted);
            Set(run, "_onCollectSave", ctx.Sys.CollectSave);
            Set(run, "_onApplySave", ctx.Sys.ApplySave);
            BuildResultsWindow(ctx, root, Local(0f, 2.45f, 50.96f));

            var checkpoints = new[]
            {
                spawn,
                Marker(root, "Checkpoint_Room1", Local(0f, 0f, 10.6f), Quaternion.identity),
                Marker(root, "Checkpoint_Room2", Local(0f, 0f, 21.6f), Quaternion.identity),
                Marker(root, "Checkpoint_Boss", Local(0f, 0f, 41f), Quaternion.identity),
            };

            return new GateRefs { Spawn = spawn, RackSlots = rackSlots, Director = director, Checkpoints = checkpoints };
        }

        private static Vector3 Local(float x, float y, float z) => GateOrigin + new Vector3(x, y, z);

        private static RoomEncounter CombatRoom(Context ctx, Rig rig, GateParts parts, string name, float centerZ,
            float sizeX, float sizeZ, EncounterData data, GameObject barrier, Color lightColor, Vector3[] spawnPoints,
            bool northOpening = true)
        {
            Room(ctx, parts, name, centerZ, sizeX, sizeZ, true, northOpening);
            PointLight(parts.Root, "Light_" + name, Local(0f, 2.9f, centerZ), lightColor, Mathf.Max(sizeX, sizeZ) * 1.5f, 7f);
            Crystal(ctx, parts, Local(-sizeX / 2f + 0.4f, 0.6f, centerZ + sizeZ / 2f - 0.6f), ctx.Mats.CrystalViolet);
            Crystal(ctx, parts, Local(sizeX / 2f - 0.4f, 0.5f, centerZ - 1f), ctx.Mats.CrystalBlue);

            var go = new GameObject("Encounter_" + name);
            go.transform.SetParent(parts.Root, false);
            go.transform.position = Local(0f, 0f, centerZ);
            var points = new Transform[spawnPoints.Length];
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                points[i] = Marker(go.transform, "Spawn_" + i, spawnPoints[i], Quaternion.Euler(0f, 180f, 0f));
            }

            var room = go.AddComponent<RoomEncounter>();
            var d = ctx.Data;
            Set(room, "_data", data);
            Set(room, "_playerHead", rig.Head);
            // Starts once the player is 1.5 m inside the south doorway.
            SetVector3(room, "_triggerCenter", new Vector3(0f, 1.5f, 0.75f));
            SetVector3(room, "_triggerSize", new Vector3(sizeX - 0.4f, 4f, sizeZ - 1.5f));
            SetArray(room, "_spawnPoints", points);
            Set(room, "_exitBarrier", barrier);
            Set(room, "_messages", d.Messages);
            Set(room, "_onPlayerDied", d.OnPlayerDied);
            return room;
        }

        /// <summary>Floor, four walls (with 2.6 m doorways south / north) and a ceiling, centred on the gate's axis.</summary>
        private static void Room(Context ctx, GateParts parts, string name, float centerZ, float sizeX, float sizeZ,
            bool southOpening, bool northOpening)
        {
            var mats = ctx.Mats;
            float zMin = centerZ - sizeZ / 2f;
            float zMax = centerZ + sizeZ / 2f;
            MakeTeleportFloor(Box(parts.Geometry, name + "_Floor", Local(0f, -0.1f, centerZ), new Vector3(sizeX, 0.2f, sizeZ), mats.CaveFloor));
            EndWall(parts, name + "_WallS", zMin - WallThickness / 2f, sizeX, southOpening, mats.CaveWall);
            EndWall(parts, name + "_WallN", zMax + WallThickness / 2f, sizeX, northOpening, mats.CaveWall);
            float sideX = sizeX / 2f + WallThickness / 2f;
            Box(parts.Geometry, name + "_WallE", Local(sideX, WallHeight / 2f, centerZ), new Vector3(WallThickness, WallHeight, sizeZ), mats.CaveWall);
            Box(parts.Geometry, name + "_WallW", Local(-sideX, WallHeight / 2f, centerZ), new Vector3(WallThickness, WallHeight, sizeZ), mats.CaveWall);
            Box(parts.Ceilings, name + "_Ceiling", Local(0f, WallHeight + 0.1f, centerZ),
                new Vector3(sizeX + WallThickness * 2f, 0.2f, sizeZ + WallThickness * 2f), mats.CaveCeiling);
        }

        private static void EndWall(GateParts parts, string name, float z, float sizeX, bool opening, Material mat)
        {
            float outer = sizeX / 2f + WallThickness;
            if (!opening)
            {
                Box(parts.Geometry, name, Local(0f, WallHeight / 2f, z), new Vector3(outer * 2f, WallHeight, WallThickness), mat);
                return;
            }
            float segment = outer - DoorWidth / 2f;
            float centerX = DoorWidth / 2f + segment / 2f;
            Box(parts.Geometry, name + "_L", Local(-centerX, WallHeight / 2f, z), new Vector3(segment, WallHeight, WallThickness), mat);
            Box(parts.Geometry, name + "_R", Local(centerX, WallHeight / 2f, z), new Vector3(segment, WallHeight, WallThickness), mat);
        }

        private static void Corridor(Context ctx, GateParts parts, float zStart, float zEnd)
        {
            var mats = ctx.Mats;
            float length = zEnd - zStart;
            float centerZ = (zStart + zEnd) / 2f;
            string name = $"Corridor_{zStart:0}";
            MakeTeleportFloor(Box(parts.Geometry, name + "_Floor", Local(0f, -0.1f, centerZ), new Vector3(DoorWidth, 0.2f, length), mats.CaveFloor));
            float sideX = DoorWidth / 2f + WallThickness / 2f;
            Box(parts.Geometry, name + "_WallE", Local(sideX, WallHeight / 2f, centerZ), new Vector3(WallThickness, WallHeight, length), mats.CaveWall);
            Box(parts.Geometry, name + "_WallW", Local(-sideX, WallHeight / 2f, centerZ), new Vector3(WallThickness, WallHeight, length), mats.CaveWall);
            Box(parts.Ceilings, name + "_Ceiling", Local(0f, WallHeight + 0.1f, centerZ),
                new Vector3(DoorWidth + WallThickness * 2f, 0.2f, length), mats.CaveCeiling);
        }

        /// <summary>A glowing System barrier across a doorway; the room opens it when cleared.</summary>
        private static GameObject Barrier(Context ctx, Transform root, string name, float z)
        {
            var barrier = Box(root, name, Local(0f, WallHeight / 2f, z), new Vector3(DoorWidth, WallHeight, 0.1f), ctx.Mats.Barrier);
            barrier.AddComponent<NavMeshObstacle>().carving = true;
            return barrier;
        }

        private static void Crystal(Context ctx, GateParts parts, Vector3 position, Material mat)
        {
            var crystal = Primitive(PrimitiveType.Cube, "Crystal", parts.Decor, position, new Vector3(20f, 35f, 15f),
                new Vector3(0.25f, 1.1f, 0.25f), mat, false);
            crystal.transform.position = position;
        }

        private static GameObject BuildPortal(Context ctx, Rig rig, Transform parent, string name, Vector3 position,
            Quaternion rotation, Transform destination, int areaId)
        {
            var portal = new GameObject(name);
            portal.transform.SetParent(parent, false);
            portal.transform.SetPositionAndRotation(position, rotation);
            // Ellipses about 1.2 × 2 m (MRH-3); the cylinder's axis points into the room.
            Primitive(PrimitiveType.Cylinder, "Rim", portal.transform, Vector3.zero, new Vector3(90f, 0f, 0f),
                new Vector3(1.2f, 0.01f, 2f), ctx.Mats.PortalRim, false);
            var swirl = Primitive(PrimitiveType.Cylinder, "Swirl", portal.transform, new Vector3(0f, 0f, 0.012f),
                new Vector3(90f, 0f, 0f), new Vector3(1.04f, 0.012f, 1.84f), ctx.Mats.PortalCore, false);

            var gate = portal.AddComponent<GatePortal>();
            Set(gate, "_playerHead", rig.Head);
            Set(gate, "_destination", destination);
            SetInt(gate, "_destinationAreaId", areaId);
            Set(gate, "_swirl", swirl.transform);
            Set(gate, "_requests", ctx.Data.RequestTravel);
            Set(gate, "_arrived", ctx.Data.Arrived);
            return portal;
        }
    }
}
