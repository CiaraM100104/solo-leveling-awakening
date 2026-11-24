using SLA.AI;
using SLA.Combat;
using SLA.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SLA.Editor
{
    public static partial class BarebonesSandboxBuilder
    {
        private static void BuildPrefabs(Context ctx)
        {
            ctx.Goblin = BuildGoblinPrefab(ctx);
            ctx.Kobold = BuildEnemyVariant(ctx, ctx.Goblin, "Kobold", ctx.Data.Kobold, ctx.Mats.Kobold, 0.75f, false);
            ctx.Chieftain = BuildEnemyVariant(ctx, ctx.Goblin, "GoblinChieftain", ctx.Data.Chieftain, ctx.Mats.Chieftain, 1.5f, true);
            ctx.Dagger = BuildDaggerPrefab(ctx);
        }

        private static GameObject BuildGoblinPrefab(Context ctx)
        {
            var mats = ctx.Mats;
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            var root = new GameObject("Goblin") { layer = enemyLayer };

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = 30f;
            // The NavMeshAgent moves this body; interpolation would snap it back to its physics pose every frame.
            body.interpolation = RigidbodyInterpolation.None;

            var torso = root.AddComponent<CapsuleCollider>();
            torso.center = new Vector3(0f, 0.55f, 0f);
            torso.height = 1.1f;
            torso.radius = 0.3f;

            root.AddComponent<Health>();
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f; // CMB-19 / art direction: spatial audio for enemies.
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 1f;
            audio.maxDistance = 15f;

            // Placed on the gate NavMesh at spawn; disabled until then so pooled copies don't complain.
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.4f;
            agent.acceleration = 20f;
            agent.angularSpeed = 0f;
            agent.enabled = false;

            var bodyMesh = Primitive(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.55f, 0f),
                Vector3.zero, new Vector3(0.6f, 0.55f, 0.6f), mats.Goblin, false);
            var head = Primitive(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.27f, 0f),
                Vector3.zero, Vector3.one * 0.36f, mats.Goblin, true);
            head.layer = enemyLayer;
            SetEnum(head.AddComponent<HitZoneMarker>(), "_zone", (int)HitZone.Head);
            Primitive(PrimitiveType.Sphere, "EyeL", head.transform, new Vector3(-0.2f, 0.12f, 0.47f), Vector3.zero,
                Vector3.one * 0.2f, mats.GoblinEyes, false);
            Primitive(PrimitiveType.Sphere, "EyeR", head.transform, new Vector3(0.2f, 0.12f, 0.47f), Vector3.zero,
                Vector3.one * 0.2f, mats.GoblinEyes, false);

            // The club swings around the shoulder; its collider is the parry target (CMB-14).
            var pivot = new GameObject("WeaponPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0.32f, 0.85f, 0.1f);
            pivot.localRotation = Quaternion.Euler(30f, 0f, 0f);
            var club = Primitive(PrimitiveType.Cube, "Club", pivot, new Vector3(0f, 0f, 0.3f), Vector3.zero,
                new Vector3(0.09f, 0.09f, 0.55f), mats.GoblinClub, true);
            club.layer = LayerMask.NameToLayer("EnemyWeapon");
            club.AddComponent<EnemyWeaponHitbox>();
            var tip = new GameObject("Tip").transform;
            tip.SetParent(pivot, false);
            tip.localPosition = new Vector3(0f, 0f, 0.58f);

            var brain = root.AddComponent<EnemyBrain>();
            var d = ctx.Data;
            Set(brain, "_data", d.Goblin);
            Set(brain, "_combat", d.Combat);
            Set(brain, "_progression", d.Progression);
            Set(brain, "_playerStatus", d.PlayerStatus);
            Set(brain, "_hits", d.Hits);
            SetArray(brain, "_bodyRenderers", bodyMesh.GetComponent<Renderer>(), head.GetComponent<Renderer>());
            Set(brain, "_weaponRenderer", club.GetComponent<Renderer>());
            Set(brain, "_weaponPivot", pivot);
            Set(brain, "_weaponTip", tip);
            SetLayerMask(brain, "_blockMask", LayerMask.GetMask("PlayerWeapon"));
            Set(brain, "_audio", audio);
            Set(brain, "_tellClip", ctx.Sfx.Tell);
            Set(brain, "_hitClip", ctx.Sfx.Hit);
            Set(brain, "_parryClip", ctx.Sfx.Parry);
            Set(brain, "_blockClip", ctx.Sfx.Block);

            return SavePrefab(root, Root + "/Prefabs/Enemies/Goblin.prefab");
        }

        /// <summary>A prefab variant of the goblin (CODING_STANDARDS rule 7): new data, colour and size.</summary>
        private static GameObject BuildEnemyVariant(Context ctx, GameObject basePrefab, string name, EnemyData data,
            Material bodyMaterial, float scale, bool crown)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = name;
            instance.transform.localScale = Vector3.one * scale;
            Set(instance.GetComponent<EnemyBrain>(), "_data", data);
            instance.transform.Find("Body").GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            var head = instance.transform.Find("Head");
            head.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            if (crown)
            {
                Primitive(PrimitiveType.Cylinder, "Crown", head, new Vector3(0f, 0.55f, 0f), Vector3.zero,
                    new Vector3(0.75f, 0.18f, 0.75f), ctx.Mats.Crown, false);
            }
            return SavePrefab(instance, $"{Root}/Prefabs/Enemies/{name}.prefab");
        }

        private static GameObject BuildDaggerPrefab(Context ctx)
        {
            var mats = ctx.Mats;
            var d = ctx.Data;
            var root = new GameObject("RustyDagger");
            var body = root.AddComponent<Rigidbody>();
            body.mass = d.RustyDagger.massKg;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var attach = new GameObject("Attach").transform;
            attach.SetParent(root.transform, false);

            Primitive(PrimitiveType.Cylinder, "Handle", root.transform, Vector3.zero, new Vector3(90f, 0f, 0f),
                new Vector3(0.032f, 0.06f, 0.032f), mats.Handle, true);
            Primitive(PrimitiveType.Cube, "Guard", root.transform, new Vector3(0f, 0f, 0.07f), Vector3.zero,
                new Vector3(0.09f, 0.018f, 0.025f), mats.Handle, false);
            Primitive(PrimitiveType.Cube, "Blade", root.transform, new Vector3(0f, 0f, 0.19f), Vector3.zero,
                new Vector3(0.03f, 0.006f, 0.22f), mats.Blade, true);
            var tip = new GameObject("Tip").transform;
            tip.SetParent(root.transform, false);
            tip.localPosition = new Vector3(0f, 0f, 0.3f);
            SetLayerRecursively(root, LayerMask.NameToLayer("PlayerWeapon"));
            ApplyShadowDagger(root); // Uses the art model when it's in the project; the shapes stay as colliders.

            // CMB-1: physics weapon held with Velocity Tracking.
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.attachTransform = attach;

            var hits = root.AddComponent<WeaponHitDetector>();
            Set(hits, "_weapon", d.RustyDagger);
            Set(hits, "_combat", d.Combat);
            Set(hits, "_progression", d.Progression);
            Set(hits, "_playerStatus", d.PlayerStatus);
            Set(hits, "_tip", tip);

            return SavePrefab(root, Root + "/Prefabs/Weapons/RustyDagger.prefab");
        }
    }
}
