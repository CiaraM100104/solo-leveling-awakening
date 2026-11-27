using System;
using System.Collections.Generic;
using SLA.AI;
using SLA.Combat;
using SLA.Core;
using SLA.Core.Events;
using SLA.Data;
using SLA.Gates;
using SLA.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace SLA.EditorTools
{
    /// <summary>
    /// Makes the Goblin Warren playable as a combat loop: builds a greybox Goblin prefab and the Rusty Dagger
    /// prefab (from Art/Models/Weapons/SM_RustyDagger.fbx, exported from the artist's Blade3.blend), then fills the gate with goblins in both combat rooms, two daggers at the player's hips, the player
    /// hurtbox, XP and gate-clear tracking, and a placeholder HUD. The exit portal opens once every goblin is dead.
    /// Re-runnable: it rebuilds the two prefabs and the gate's "Combat Loop" object and leaves the rest alone.
    /// Headless: -executeMethod SLA.EditorTools.CombatLoopBuilder.BuildBatch
    /// </summary>
    public static class CombatLoopBuilder
    {
        const string PrefabDir = ProjectConfigurator.Root + "/Prefabs";
        const string GoblinPrefabPath = PrefabDir + "/Enemies/Goblin.prefab";
        const string DaggerPrefabPath = PrefabDir + "/Weapons/RustyDagger.prefab";
        const string EventsDir = ProjectConfigurator.DataDir + "/Events/";
        const string GoblinDataPath = ProjectConfigurator.DataDir + "/Enemies/Goblin.asset";
        const string DaggerDataPath = ProjectConfigurator.DataDir + "/Weapons/RustyDagger.asset";
        const string DaggerModelPath = ProjectConfigurator.Root + "/Art/Models/Weapons/SM_RustyDagger.fbx";
        const string DaggerTexturePrefix = ProjectConfigurator.Root + "/Art/Textures/Weapons/T_RustyDagger";
        const string LevelUpBurstPath = PrefabDir + "/VFX/VFX_LevelUpBurst.prefab";
        const string GateDataPath = ProjectConfigurator.DataDir + "/Gates/GoblinWarren.asset";
        const string CombatConfigPath = ProjectConfigurator.SettingsDir + "/CombatConfig.asset";
        const string ProgressionConfigPath = ProjectConfigurator.SettingsDir + "/ProgressionConfig.asset";
        const string GateScene = "Gates/" + SceneNames.GateGoblinWarren;
        const string RootName = "Combat Loop";
        const string ExitPortalName = "Exit Portal (to hub)";
        const string NoteLine = "Combat Loop (CombatLoopBuilder): goblins in both rooms, daggers at the hips, exit portal opens when all goblins are dead. Placeholder until the real enemies, weapons and Chieftain land.";

        const string Lit = "Universal Render Pipeline/Lit";
        const string Unlit = "Universal Render Pipeline/Unlit";

        // Packs of 2-3 (Goblin roster row). Room 1 is centered at z 11, Room 2 at z 23; both face the entrance.
        static readonly Vector3[] GoblinSpots =
        {
            new Vector3(-1.6f, 0f, 12.5f), new Vector3(1.6f, 0f, 12f),
            new Vector3(-2.2f, 0f, 24f), new Vector3(0f, 0f, 25.5f), new Vector3(2.2f, 0f, 24f),
        };

        [MenuItem("Tools/SLA/Build Playable Combat Loop")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                Build();
                EditorUtility.DisplayDialog("Combat loop built",
                    "Goblin Warren now has goblins, daggers and a working exit.\n\nOpen Scenes/Bootstrap and press Play, then walk through the hub portal.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Combat loop failed", e.Message + "\n\nSee the Console for details.", "OK");
            }
        }

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // scratch scene for prefab building
            ProjectConfigurator.CreateOrLoad<VoidEventChannel>(EventsDir + "OnPlayerDied.asset");
            ProjectConfigurator.CreateOrLoad<FloatEventChannel>(EventsDir + "OnPlayerHpChanged.asset");
            ProjectConfigurator.EnsureFolder(PrefabDir + "/Enemies");
            ProjectConfigurator.EnsureFolder(PrefabDir + "/Weapons");
            ProjectConfigurator.EnsureFolder(ProjectConfigurator.MaterialsDir);

            var goblin = BuildGoblinPrefab();
            BuildDaggerPrefab();
            VfxBuilder.BuildLevelUpBurstPrefab(Channel<IntEventChannel>("OnLevelUp"));
            SetObject(GameFlowSceneSetup.Load<EnemyData>(GoblinDataPath), "prefab", goblin);

            LetRigGrabWeapons();
            GameFlowSceneSetup.EditScene(GateScene, SetUpGate);
            GameFlowSceneSetup.EditScene(SceneNames.HubMR, SetUpHub);
            GameFlowSceneSetup.EditScene(SceneNames.HubVirtual, SetUpHub);
            AssetDatabase.SaveAssets();
            Debug.Log("[CombatLoopBuilder] Goblin Warren is playable: goblins, daggers, hurtbox, XP and exit are in. Both hubs have a dagger rack and a practice post.");
        }

        // ------------------------------------------------------------------ Hubs: practice daggers

        // Runs the whole build once by itself after it first compiles, so the daggers show up without a menu hunt.
        const string RanOnceKey = "SLA.CombatLoopBuilder.RanOnce.CyanVfx";

        [InitializeOnLoadMethod]
        static void BuildOnceAfterCompile()
        {
            if (Application.isBatchMode || EditorPrefs.GetBool(RanOnceKey, false)) return;
            EditorApplication.delayCall += RunOnce;
        }

        static void RunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunOnce;
                return;
            }
            EditorPrefs.SetBool(RanOnceKey, true);

            string returnTo = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            BuildMenu();
            if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        }

        // ------------------------------------------------------------------ Rig: let the hands grab weapons

        const string RigPrefabPath = PrefabDir + "/Player/PlayerRig.prefab";
        const string RigGrabKey = "SLA.CombatLoopBuilder.RigGrabsWeapons";

        // Separate one-time fix for machines that already ran the build before this step existed.
        [InitializeOnLoadMethod]
        static void FixRigOnceAfterCompile()
        {
            if (Application.isBatchMode || EditorPrefs.GetBool(RigGrabKey, false)) return;
            EditorApplication.delayCall += FixRigOnce;
        }

        static void FixRigOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += FixRigOnce;
                return;
            }
            EditorPrefs.SetBool(RigGrabKey, true);
            LetRigGrabWeapons();
        }

        /// <summary>
        /// The Starter rig's near-far interactors only cast against the Default layer (near) and Default/UI
        /// (far), so weapons on the PlayerWeapon layer can't be grabbed. Adds that layer to both casters.
        /// </summary>
        static void LetRigGrabWeapons()
        {
            int weaponBit = 1 << Layer("PlayerWeapon");
            var root = PrefabUtility.LoadPrefabContents(RigPrefabPath);
            try
            {
                int changed = 0;
                foreach (var hand in root.GetComponentsInChildren<NearFarInteractor>(true))
                {
                    var serialized = new SerializedObject(hand);
                    if (AddLayerBit(serialized.FindProperty("m_NearInteractionCaster").objectReferenceValue, "m_PhysicsLayerMask", weaponBit)) changed++;
                    if (AddLayerBit(serialized.FindProperty("m_FarInteractionCaster").objectReferenceValue, "m_RaycastMask", weaponBit)) changed++;
                }

                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, RigPrefabPath);
                Debug.Log($"[CombatLoopBuilder] PlayerRig hands can grab PlayerWeapon objects ({changed} caster masks updated).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool AddLayerBit(Object caster, string maskField, int bit)
        {
            if (caster == null) return false;
            var serialized = new SerializedObject(caster);
            var mask = serialized.FindProperty(maskField);
            if (mask == null || (mask.intValue & bit) != 0) return false;
            mask.intValue |= bit;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        const string RackName = "Practice Daggers (CombatLoopBuilder)";

        /// <summary>
        /// Two Rusty Daggers (the starter weapon) on a pedestal just right of the spawn, and a post to the left
        /// to swing at: the real-room hub may have no walls with colliders. Taken daggers holster at the hips.
        /// </summary>
        static bool SetUpHub()
        {
            var existing = GameObject.Find(RackName);
            if (existing != null) Object.DestroyImmediate(existing);

            var spawn = Object.FindAnyObjectByType<PlayerSpawn>();
            if (spawn == null)
            {
                Debug.LogWarning("[CombatLoopBuilder] A hub has no Player Spawn; skipped its dagger rack.");
                return false;
            }

            var daggerPrefab = GameFlowSceneSetup.Load<GameObject>(DaggerPrefabPath);
            var stone = GameFlowSceneSetup.Load<Material>(ProjectConfigurator.MaterialsDir + "/M_Greybox.mat");

            var root = new GameObject(RackName).transform;
            root.SetPositionAndRotation(spawn.transform.position, Quaternion.Euler(0f, spawn.transform.eulerAngles.y, 0f));

            // Pedestal: within arm's reach, front-right, top at 0.84 m.
            var rackCenter = new Vector3(0.45f, 0f, 0.55f);
            Part(PrimitiveType.Cube, "Pedestal", root, rackCenter + new Vector3(0f, 0.42f, 0f), new Vector3(0.3f, 0.84f, 0.5f), stone, true);

            foreach (float side in new[] { -1f, 1f })
            {
                var dagger = (GameObject)PrefabUtility.InstantiatePrefab(daggerPrefab);
                dagger.name = side < 0f ? "Rusty Dagger (left)" : "Rusty Dagger (right)";
                dagger.transform.SetParent(root, false);
                // Lying flat on the pedestal, handles toward the player.
                dagger.transform.localPosition = rackCenter + new Vector3(0.08f * side, 0.86f, -0.18f);
                dagger.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // rolled onto its flat
                var held = dagger.GetComponent<HeldWeapon>();
                SetVector2(held, "_holsterOffsetMeters", new Vector2(0.22f * side, 0.12f));
                Set(held, "_waitUntilFirstGrab", p => p.boolValue = true);
            }

            // Practice post: something solid to strike for the yellow sparks.
            Part(PrimitiveType.Cube, "Practice Post", root, new Vector3(-0.55f, 0.9f, 0.75f), new Vector3(0.25f, 1.8f, 0.25f), stone, true);
            return true;
        }

        // ------------------------------------------------------------------ Goblin

        static GameObject BuildGoblinPrefab()
        {
            var data = GameFlowSceneSetup.Load<EnemyData>(GoblinDataPath);
            var skin = Mat("M_Goblin_Skin", Lit, new Color(0.33f, 0.55f, 0.22f));
            var clubMat = Mat("M_Goblin_Club", Lit, new Color(0.42f, 0.28f, 0.15f));
            var eyeMat = Mat("M_Goblin_Eye", Unlit, new Color(1f, 0.85f, 0.2f));
            var barBack = Mat("M_HpBar_Back", Unlit, new Color(0.05f, 0.05f, 0.08f));
            var barFill = Mat("M_HpBar_Fill", Unlit, new Color(0.9f, 0.22f, 0.23f));

            var root = new GameObject("Goblin") { layer = Layer("Enemy") };

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.4f;
            agent.speed = data.moveSpeed;
            agent.angularSpeed = 540f;
            agent.acceleration = 16f;

            // Enemies are animated, not simulated (CMB summary): a kinematic body so weapons collide cleanly.
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var torso = root.AddComponent<CapsuleCollider>(); // no HitZoneMarker: torso (CMB-6)
            torso.center = new Vector3(0f, 0.6f, 0f);
            torso.height = 1.1f;
            torso.radius = 0.28f;

            root.AddComponent<Health>();
            var audio = AddAudio(root);

            var renderers = new List<Renderer>
            {
                Part(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.55f, 0.55f, 0.5f), skin, false),
                Part(PrimitiveType.Capsule, "Arm", root.transform, new Vector3(0.3f, 0.8f, 0.14f), new Vector3(0.12f, 0.2f, 0.12f), skin, false, new Vector3(70f, 0f, 0f)),
                Part(PrimitiveType.Cube, "Ear L", root.transform, new Vector3(-0.2f, 1.3f, 0f), new Vector3(0.05f, 0.2f, 0.05f), skin, false, new Vector3(0f, 0f, 55f)),
                Part(PrimitiveType.Cube, "Ear R", root.transform, new Vector3(0.2f, 1.3f, 0f), new Vector3(0.05f, 0.2f, 0.05f), skin, false, new Vector3(0f, 0f, -55f)),
            };
            var head = Part(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.25f, 0.02f), Vector3.one * 0.38f, skin, true);
            SetEnum(head.gameObject.AddComponent<HitZoneMarker>(), "_zone", (int)HitZone.Head);
            renderers.Add(head);
            Part(PrimitiveType.Sphere, "Eye L", root.transform, new Vector3(-0.08f, 1.29f, 0.17f), Vector3.one * 0.07f, eyeMat, false);
            Part(PrimitiveType.Sphere, "Eye R", root.transform, new Vector3(0.08f, 1.29f, 0.17f), Vector3.one * 0.07f, eyeMat, false);
            // No collider on the club: parrying the enemy weapon (CMB-14) isn't built yet.
            var club = Part(PrimitiveType.Cube, "Club", root.transform, new Vector3(0.3f, 0.92f, 0.36f), new Vector3(0.08f, 0.08f, 0.55f), clubMat, false, new Vector3(-25f, 0f, 0f));

            var bar = new GameObject("HP Bar").transform;
            bar.SetParent(root.transform, false);
            bar.localPosition = new Vector3(0f, 1.65f, 0f);
            Part(PrimitiveType.Quad, "Back", bar, Vector3.zero, new Vector3(0.5f, 0.06f, 1f), barBack, false);
            var fillPivot = new GameObject("Fill Pivot").transform;
            fillPivot.SetParent(bar, false);
            fillPivot.localPosition = new Vector3(-0.25f, 0f, -0.002f); // left edge, just in front of the back
            Part(PrimitiveType.Quad, "Fill", fillPivot, new Vector3(0.25f, 0f, 0f), new Vector3(0.5f, 0.06f, 1f), barFill, false);

            var brain = root.AddComponent<EnemyBrain>();
            SetObject(brain, "_data", data);
            SetFloat(brain, "_sightRange", 7f); // packs wake up room by room, not all from the entrance
            SetObject(brain, "_onEnemyKilled", Channel<StringEventChannel>("OnEnemyKilled"));
            SetObject(brain, "_onPlayerDied", Channel<VoidEventChannel>("OnPlayerDied"));

            var attack = root.AddComponent<EnemyMeleeAttack>();
            SetObject(attack, "_config", GameFlowSceneSetup.Load<CombatConfig>(CombatConfigPath));
            SetInt(attack, "_playerMask", 1 << Layer("Player"));
            SetInt(attack, "_playerWeaponMask", 1 << Layer("PlayerWeapon"));

            var feedback = root.AddComponent<EnemyFeedback>();
            SetObjects(feedback, "_bodyRenderers", renderers.ToArray());
            SetObject(feedback, "_weaponRenderer", club);
            SetObject(feedback, "_hpFill", fillPivot);
            SetObject(feedback, "_hpBar", bar);
            SetObject(feedback, "_audio", audio);

            return SavePrefab(root, GoblinPrefabPath);
        }

        // ------------------------------------------------------------------ Rusty Dagger

        static GameObject BuildDaggerPrefab()
        {
            var data = GameFlowSceneSetup.Load<WeaponData>(DaggerDataPath);
            ConfigureDaggerImports();
            var modelAsset = GameFlowSceneSetup.Load<GameObject>(DaggerModelPath);
            var material = DaggerMaterial();
            var glowMaterial = VfxBuilder.GlowMaterial();
            var config = GameFlowSceneSetup.Load<CombatConfig>(CombatConfigPath);

            var root = new GameObject("RustyDagger") { layer = Layer("PlayerWeapon") };
            var body = root.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(0.1f, data.massKg);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic = true; // starts in the holster

            // The model's origin is the middle of the handle (exported that way), so the grip is the root.
            var grip = new GameObject("Grip").transform;
            grip.SetParent(root.transform, false);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            Bounds bounds = OrientDaggerModel(model.transform, data.lengthMeters);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = material;

            // One convex hull: a dynamic body can't use a concave mesh, and the curve fills in only slightly.
            var meshFilter = model.GetComponentInChildren<MeshFilter>();
            var hull = meshFilter.gameObject.AddComponent<MeshCollider>();
            hull.sharedMesh = meshFilter.sharedMesh;
            hull.convex = true;

            var tip = new GameObject("Tip").transform;
            tip.SetParent(root.transform, false);
            tip.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);

            var spark = VfxBuilder.BuildHitSparks(root.transform, glowMaterial);
            var glow = VfxBuilder.BuildHitGlow(root.transform, glowMaterial);

            // The trail covers the blade from just past the guard (about 30% of the length) to the tip.
            float guardZ = bounds.min.z + bounds.size.z * 0.3f;
            var trailCenter = new Vector3(bounds.center.x, bounds.center.y, (guardZ + bounds.max.z) * 0.5f);
            var trail = VfxBuilder.BuildSlashTrail(root.transform, trailCenter, bounds.max.z - guardZ, VfxBuilder.TrailMaterial());

            var audioObject = new GameObject("Audio");
            audioObject.transform.SetParent(root.transform, false);
            var audio = AddAudio(audioObject);

            var detector = root.AddComponent<WeaponHitDetector>();
            SetObject(detector, "_weapon", data);
            SetObject(detector, "_config", config);
            SetObject(detector, "_tip", tip);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking; // CMB-1
            grab.throwOnDetach = false;
            grab.useDynamicAttach = false;
            grab.attachTransform = grip;

            var held = root.AddComponent<HeldWeapon>();
            SetObject(held, "_spark", spark);
            SetObject(held, "_glow", glow);
            SetObject(held, "_audio", audio);

            var slash = trail.gameObject.AddComponent<SlashTrail>();
            SetObject(slash, "_config", config);
            SetObject(slash, "_grab", grab);

            return SavePrefab(root, DaggerPrefabPath);
        }

        /// <summary>
        /// Lays the model's long axis along +Z (ahead of the fist, the way the controller points) with the blade
        /// standing edge-down, and scales it to <paramref name="lengthMeters"/>. Returns its bounds in the root's space.
        /// </summary>
        static Bounds OrientDaggerModel(Transform model, float lengthMeters)
        {
            model.localRotation = Quaternion.Euler(90f, 0f, 0f); // FBX is Y-up with the blade pointing up
            var bounds = LocalBounds(model);
            if (bounds.size.x > bounds.size.y) // the blade's flat should face sideways, edge down
            {
                model.localRotation = Quaternion.Euler(0f, 0f, 90f) * model.localRotation;
                bounds = LocalBounds(model);
            }
            if (bounds.max.z < -bounds.min.z) // the grip sits near the pommel, so most of the length is the blade
            {
                model.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.localRotation;
                bounds = LocalBounds(model);
            }

            model.localScale *= lengthMeters / Mathf.Max(0.0001f, bounds.size.z);
            return LocalBounds(model);
        }

        // Bounds of the model's renderers in its parent's space (the parent is the prefab root at the origin).
        static Bounds LocalBounds(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        // ART_PIPELINE "In Unity" and texture rules; materials come from DaggerMaterial, not the FBX.
        static void ConfigureDaggerImports()
        {
            var model = (ModelImporter)AssetImporter.GetAtPath(DaggerModelPath);
            if (model == null) throw new InvalidOperationException($"{DaggerModelPath} is missing.");
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.isReadable = false;
            model.meshCompression = ModelImporterMeshCompression.Medium;
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            model.importCameras = false;
            model.importLights = false;
            model.SaveAndReimport();

            ConfigureTexture(DaggerTexturePrefix + "_D.png", TextureImporterType.Default, true);
            ConfigureTexture(DaggerTexturePrefix + "_N.png", TextureImporterType.NormalMap, false);
            ConfigureTexture(DaggerTexturePrefix + "_MS.png", TextureImporterType.Default, false);
        }

        static void ConfigureTexture(string path, TextureImporterType type, bool sRgb)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException($"{path} is missing.");
            importer.textureType = type;
            importer.sRGBTexture = sRgb;
            importer.mipmapEnabled = true; // world texture
            importer.maxTextureSize = 1024;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        // URP Lit until the toon shader exists: color, normal, and metallic (RGB) with smoothness (A).
        static Material DaggerMaterial()
        {
            var material = Mat("M_RustyDagger", Lit, Color.white);
            material.SetTexture("_BaseMap", GameFlowSceneSetup.Load<Texture2D>(DaggerTexturePrefix + "_D.png"));
            material.SetTexture("_BumpMap", GameFlowSceneSetup.Load<Texture2D>(DaggerTexturePrefix + "_N.png"));
            material.SetTexture("_MetallicGlossMap", GameFlowSceneSetup.Load<Texture2D>(DaggerTexturePrefix + "_MS.png"));
            material.SetFloat("_Smoothness", 1f); // scales the map's smoothness
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back); // the exported mesh is closed
            material.doubleSidedGI = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ Gate scene

        static bool SetUpGate()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var goblinPrefab = GameFlowSceneSetup.Load<GameObject>(GoblinPrefabPath);
            var daggerPrefab = GameFlowSceneSetup.Load<GameObject>(DaggerPrefabPath);
            var progression = GameFlowSceneSetup.Load<ProgressionConfig>(ProgressionConfigPath);
            var exitPortal = GameObject.Find(ExitPortalName);
            if (exitPortal == null) throw new InvalidOperationException($"{GateScene} has no '{ExitPortalName}'.");

            var root = new GameObject(RootName).transform;

            // Player hurtbox and HP (CMB-15, GTE-4).
            var hurtbox = new GameObject("Player Hurtbox", typeof(CapsuleCollider), typeof(Rigidbody), typeof(Health)) { layer = Layer("Player") };
            hurtbox.transform.SetParent(root, false);
            var playerBody = hurtbox.AddComponent<PlayerHurtbox>();
            SetObject(playerBody, "_progression", progression);
            SetObject(playerBody, "_onDamageTaken", Channel<FloatEventChannel>("OnDamageTaken"));
            SetObject(playerBody, "_onPlayerHpChanged", Channel<FloatEventChannel>("OnPlayerHpChanged"));
            SetObject(playerBody, "_onPlayerDied", Channel<VoidEventChannel>("OnPlayerDied"));
            SetObject(playerBody, "_onLevelUp", Channel<IntEventChannel>("OnLevelUp"));

            // XP and clearing.
            var tracker = new GameObject("Gate Clear Tracker").AddComponent<GateClearTracker>();
            tracker.transform.SetParent(root, false);
            SetObject(tracker, "_gate", GameFlowSceneSetup.Load<GateData>(GateDataPath));
            SetObject(tracker, "_progression", progression);
            SetObject(tracker, "_onEnemyKilled", Channel<StringEventChannel>("OnEnemyKilled"));
            SetObject(tracker, "_onLevelUp", Channel<IntEventChannel>("OnLevelUp"));
            SetObject(tracker, "_onGateCleared", Channel<VoidEventChannel>("OnGateCleared"));
            SetObject(tracker, "_exitPortal", exitPortal);

            // Dual daggers, one per hip.
            foreach (float side in new[] { -1f, 1f })
            {
                var dagger = (GameObject)PrefabUtility.InstantiatePrefab(daggerPrefab);
                dagger.name = side < 0f ? "Rusty Dagger (left hip)" : "Rusty Dagger (right hip)";
                dagger.transform.SetParent(root, false);
                dagger.transform.position = new Vector3(0.22f * side, 1f, 0.2f);
                SetVector2(dagger.GetComponent<HeldWeapon>(), "_holsterOffsetMeters", new Vector2(0.22f * side, 0.12f));
            }

            // Goblins.
            var enemies = new GameObject("Goblins").transform;
            enemies.SetParent(root, false);
            for (int i = 0; i < GoblinSpots.Length; i++)
            {
                var goblin = (GameObject)PrefabUtility.InstantiatePrefab(goblinPrefab);
                goblin.name = $"Goblin {(i < 2 ? "Room 1" : "Room 2")} #{i + 1}";
                goblin.transform.SetParent(enemies, false);
                goblin.transform.SetPositionAndRotation(GoblinSpots[i], Quaternion.Euler(0f, 180f, 0f));
            }

            // Level-up burst (PRG-3); moves under the player when it plays.
            var burst = (GameObject)PrefabUtility.InstantiatePrefab(GameFlowSceneSetup.Load<GameObject>(LevelUpBurstPath));
            burst.transform.SetParent(root, false);

            BuildHud(root, progression);
            AppendNote();
            return true;
        }

        static void BuildHud(Transform root, ProgressionConfig progression)
        {
            var hudObject = new GameObject("Gate HUD");
            hudObject.transform.SetParent(root, false);
            var hud = hudObject.AddComponent<GateHud>();

            // Banner: trails the head with Lazy Follow, never head-locked.
            var banner = GameFlowSceneSetup.CreateCanvas("Banner", new Vector2(1000f, 300f));
            banner.SetParent(hudObject.transform, true);
            banner.GetComponent<Image>().raycastTarget = false;
            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            accent.transform.SetParent(banner, false);
            accent.GetComponent<LayoutElement>().preferredHeight = 8f;
            var accentImage = accent.GetComponent<Image>();
            accentImage.color = GameFlowSceneSetup.SystemBlue;
            accentImage.raycastTarget = false;
            var title = GameFlowSceneSetup.AddText(banner, "", 40, GameFlowSceneSetup.SystemBlue, 100f);
            var body = GameFlowSceneSetup.AddText(banner, "", 30, GameFlowSceneSetup.OffWhite, 90f);
            var follow = banner.gameObject.AddComponent<LazyFollow>();
            follow.targetOffset = new Vector3(0f, -0.1f, 1.6f);
            follow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
            follow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;

            // Wrist readout over the left controller.
            var wrist = GameFlowSceneSetup.CreateCanvas("Wrist Readout", new Vector2(170f, 110f));
            wrist.SetParent(hudObject.transform, true);
            wrist.GetComponent<Image>().raycastTarget = false;
            wrist.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(8, 8, 6, 6);
            var wristText = GameFlowSceneSetup.AddText(wrist, "", 20, GameFlowSceneSetup.OffWhite, 96f);
            wristText.lineSpacing = 0.9f;

            SetObject(hud, "_bannerRoot", banner.gameObject);
            SetObject(hud, "_bannerTitle", title);
            SetObject(hud, "_bannerBody", body);
            SetObject(hud, "_bannerBorder", accentImage);
            SetObject(hud, "_wristRoot", wrist);
            SetObject(hud, "_wristText", wristText);
            SetObject(hud, "_progression", progression);
            SetObject(hud, "_onLevelUp", Channel<IntEventChannel>("OnLevelUp"));
            SetObject(hud, "_onGateCleared", Channel<VoidEventChannel>("OnGateCleared"));
            SetObject(hud, "_onPlayerDied", Channel<VoidEventChannel>("OnPlayerDied"));
            SetObject(hud, "_onPlayerHpChanged", Channel<FloatEventChannel>("OnPlayerHpChanged"));
            SetObject(hud, "_onEnemyKilled", Channel<StringEventChannel>("OnEnemyKilled"));
            SetString(hud, "_objective",
                "Defeat every goblin in both chambers.\nGrip near your hip to draw a dagger. Swing hard: taps do nothing.");
        }

        static void AppendNote()
        {
            var note = Object.FindAnyObjectByType<SceneNote>();
            if (note == null || (note.text != null && note.text.Contains("CombatLoopBuilder"))) return;
            note.text = (note.text ?? "") + "\n" + NoteLine;
            EditorUtility.SetDirty(note);
        }

        // ------------------------------------------------------------------ helpers

        static Renderer Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, bool keepCollider, Vector3 localEuler = default)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            if (!keepCollider) Object.DestroyImmediate(part.GetComponent<Collider>());
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        static AudioSource AddAudio(GameObject target)
        {
            var audio = target.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f; // spatial cues (CMB-19)
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 1f;
            audio.maxDistance = 15f;
            audio.dopplerLevel = 0f;
            return audio;
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            Object.DestroyImmediate(root);
            if (!saved) throw new InvalidOperationException($"Could not save {path}.");
            return prefab;
        }

        static Material Mat(string name, string shaderName, Color color)
        {
            string path = $"{ProjectConfigurator.MaterialsDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader {shaderName} not found.");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static T Channel<T>(string name) where T : Object => GameFlowSceneSetup.Load<T>(EventsDir + name + ".asset");

        static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer < 0) throw new InvalidOperationException($"Layer '{name}' is missing; run Tools > SLA > Configure Project Settings.");
            return layer;
        }

        static SerializedProperty Property(SerializedObject serialized, string field)
        {
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException($"{serialized.targetObject.GetType().Name} has no field {field}.");
            return property;
        }

        static void Set(Object target, string field, Action<SerializedProperty> apply)
        {
            var serialized = new SerializedObject(target);
            apply(Property(serialized, field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetObject(Object target, string field, Object value) => Set(target, field, p => p.objectReferenceValue = value);
        static void SetFloat(Object target, string field, float value) => Set(target, field, p => p.floatValue = value);
        static void SetInt(Object target, string field, int value) => Set(target, field, p => p.intValue = value);
        static void SetEnum(Object target, string field, int value) => Set(target, field, p => p.enumValueIndex = value);
        static void SetString(Object target, string field, string value) => Set(target, field, p => p.stringValue = value);
        static void SetVector2(Object target, string field, Vector2 value) => Set(target, field, p => p.vector2Value = value);

        static void SetObjects(Object target, string field, Object[] values) => Set(target, field, p =>
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        });
    }
}
