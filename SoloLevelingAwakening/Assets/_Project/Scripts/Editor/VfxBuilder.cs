using System;
using System.IO;
using SLA.Core.Events;
using SLA.Progression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SLA.EditorTools
{
    /// <summary>
    /// Builds the combat loop's placeholder VFX (ART_PIPELINE: Shuriken particles, additive unlit materials,
    /// under 200 live particles per effect, no lights): cyan hit sparks with a glow flash, the blade's slash
    /// trail, and the gold level-up burst. Glow is faked with additive soft sprites, since HDR and
    /// post-processing (Bloom) are off on Quest (ARCHITECTURE).
    /// The soft-dot and trail images are generated once; paint over the PNGs to restyle every effect.
    /// </summary>
    internal static class VfxBuilder
    {
        const string TextureDir = ProjectConfigurator.Root + "/Art/Textures/VFX";
        const string SoftDotPath = TextureDir + "/T_VFX_SoftDot_D.png";
        const string TrailPath = TextureDir + "/T_VFX_SlashTrail_D.png";
        const string GlowMaterialPath = ProjectConfigurator.MaterialsDir + "/M_VFX_Glow.mat";
        const string TrailMaterialPath = ProjectConfigurator.MaterialsDir + "/M_VFX_SlashTrail.mat";
        const string VfxPrefabDir = ProjectConfigurator.Root + "/Prefabs/VFX";
        const string LevelUpBurstPath = VfxPrefabDir + "/VFX_LevelUpBurst.prefab";
        const string ParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";

        static readonly Color SparkCyan = new Color(0.4f, 0.92f, 1f);
        static readonly Color SparkBlue = new Color(0.1f, 0.45f, 1f);
        static readonly Color GlowCyan = new Color(0.3f, 0.85f, 1f);
        static readonly Color LevelUpGold = new Color32(0xFF, 0xC9, 0x4A, 0xFF); // UI-1

        // ------------------------------------------------------------------ Materials

        /// <summary>Additive soft dot: sparks, glows and the level-up burst share it.</summary>
        internal static Material GlowMaterial() => AdditiveMaterial(GlowMaterialPath, SoftDot());

        internal static Material TrailMaterial() => AdditiveMaterial(TrailMaterialPath, TrailTexture());

        static Material AdditiveMaterial(string path, Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(ParticlesUnlit);
                if (shader == null) throw new InvalidOperationException($"Shader {ParticlesUnlit} not found.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white); // particle and trail colors tint it
            // Transparent, additive (src alpha, one): brightens what's behind it, which reads as glow.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ Textures

        // White with a soft round falloff; particle colors tint it.
        static Texture2D SoftDot() => GeneratedTexture(SoftDotPath, 128, 128, (u, v) =>
        {
            float r = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
            return Mathf.Pow(1f - r, 2.2f);
        });

        // Soft across the ribbon's width (v), even along its length (the trail's gradient fades it).
        static Texture2D TrailTexture() => GeneratedTexture(TrailPath, 64, 64, (u, v) => Mathf.Pow(Mathf.Sin(Mathf.PI * v), 1.5f));

        static Texture2D GeneratedTexture(string path, int width, int height, Func<float, float, float> alpha)
        {
            if (!File.Exists(path))
            {
                ProjectConfigurator.EnsureFolder(TextureDir);
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float a = Mathf.Clamp01(alpha((x + 0.5f) / width, (y + 0.5f) / height));
                        pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
                texture.SetPixels32(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false; // UI-like effect sprite
            importer.maxTextureSize = 256;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 256;
            android.format = TextureImporterFormat.ASTC_4x4; // soft gradients band at 6x6
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return GameFlowSceneSetup.Load<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Weapon hits

        /// <summary>Cyan streaks that fly out, fall, shrink and fade. HeldWeapon emits them per hit.</summary>
        internal static ParticleSystem BuildHitSparks(Transform parent, Material material)
        {
            var spark = NewSystem("VFX_HitSparks", parent, material);

            var main = spark.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
            main.startColor = SparkCyan;
            main.gravityModifier = 0.8f;
            main.maxParticles = 64;

            var shape = spark.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;

            Fade(spark, SparkCyan, SparkBlue);
            Shrink(spark, 0.3f);

            // Stretched along their velocity, so they read as streaks rather than dots.
            var renderer = spark.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1f;
            return spark;
        }

        /// <summary>One soft cyan flash per hit; HeldWeapon sets its size from the hit's strength.</summary>
        internal static ParticleSystem BuildHitGlow(Transform parent, Material material)
        {
            var glow = NewSystem("VFX_HitGlow", parent, material);

            var main = glow.main;
            main.duration = 0.3f;
            main.startLifetime = 0.15f;
            main.startSpeed = 0f;
            main.startSize = 0.15f;
            main.startColor = new Color(GlowCyan.r, GlowCyan.g, GlowCyan.b, 0.8f);
            main.maxParticles = 8;

            Fade(glow, GlowCyan, GlowCyan);
            Grow(glow, 0.7f, 1.3f);
            return glow;
        }

        /// <summary>
        /// The ribbon behind a swing: white at the blade fading to clear cyan. Its Z faces across the blade's flat,
        /// so the ribbon spans the blade's length. SlashTrail switches it on only for swings fast enough to hit.
        /// </summary>
        internal static TrailRenderer BuildSlashTrail(Transform parent, Vector3 localPosition, float widthMeters, Material material)
        {
            var trailObject = new GameObject("VFX_SlashTrail");
            trailObject.transform.SetParent(parent, false);
            trailObject.transform.localPosition = localPosition;
            trailObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // trail Z = blade's flat normal (+X)

            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.12f;
            trail.minVertexDistance = 0.01f;
            trail.widthMultiplier = widthMeters;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.6f);
            trail.colorGradient = MakeGradient(new Color(1f, 1f, 1f, 0.7f), new Color(SparkCyan.r, SparkCyan.g, SparkCyan.b, 0f));
            trail.alignment = LineAlignment.TransformZ;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCapVertices = 0;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            trail.autodestruct = false;
            return trail;
        }

        // ------------------------------------------------------------------ Level-up

        /// <summary>
        /// Gold sparks rising from a floor ring around the player, plus a glowing ring spreading on the floor.
        /// 60 + 1 particles, all below the eyes' sight line ahead (CMF-12: no full-screen flash).
        /// </summary>
        internal static GameObject BuildLevelUpBurstPrefab(IntEventChannel onLevelUp)
        {
            ProjectConfigurator.EnsureFolder(VfxPrefabDir);
            var material = GlowMaterial();

            var root = new GameObject("VFX_LevelUpBurst");
            var rising = root.AddComponent<ParticleSystem>();
            ConfigureNew(rising, material);
            var main = rising.main;
            main.duration = 1.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startColor = LevelUpGold;
            main.maxParticles = 80;

            var emission = rising.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40), new ParticleSystem.Burst(0.15f, 20) });

            var shape = rising.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;
            shape.radiusThickness = 0.1f; // near the rim, a ring around the player rather than through them
            shape.rotation = new Vector3(90f, 0f, 0f); // the circle lies flat on the floor

            var rise = rising.velocityOverLifetime;
            rise.enabled = true;
            rise.space = ParticleSystemSimulationSpace.World;
            rise.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            rise.y = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            rise.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            Fade(rising, LevelUpGold, LevelUpGold);
            Shrink(rising, 0.2f);

            var ringObject = new GameObject("Floor Ring");
            ringObject.transform.SetParent(root.transform, false);
            ringObject.transform.localPosition = new Vector3(0f, 0.02f, 0f); // just above the floor
            var ring = ringObject.AddComponent<ParticleSystem>();
            ConfigureNew(ring, material);
            var ringMain = ring.main;
            ringMain.duration = 1f;
            ringMain.startLifetime = 0.7f;
            ringMain.startSpeed = 0f;
            ringMain.startSize = 1.6f;
            ringMain.startColor = new Color(LevelUpGold.r, LevelUpGold.g, LevelUpGold.b, 0.7f);
            ringMain.maxParticles = 1;
            var ringEmission = ring.emission;
            ringEmission.enabled = true;
            ringEmission.rateOverTime = 0f;
            ringEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            Fade(ring, LevelUpGold, LevelUpGold);
            Grow(ring, 0.4f, 1.2f);
            ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

            var burst = root.AddComponent<LevelUpBurst>();
            var serialized = new SerializedObject(burst);
            serialized.FindProperty("_onLevelUp").objectReferenceValue = onLevelUp;
            serialized.FindProperty("_burst").objectReferenceValue = rising;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, LevelUpBurstPath, out bool saved);
            Object.DestroyImmediate(root);
            if (!saved) throw new InvalidOperationException($"Could not save {LevelUpBurstPath}.");
            return prefab;
        }

        // ------------------------------------------------------------------ helpers

        // A world-space system that only emits when code calls Emit.
        static ParticleSystem NewSystem(string name, Transform parent, Material material)
        {
            var systemObject = new GameObject(name);
            systemObject.transform.SetParent(parent, false);
            var system = systemObject.AddComponent<ParticleSystem>();
            ConfigureNew(system, material);
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            return system;
        }

        static void ConfigureNew(ParticleSystem system, Material material)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Color shifts from start to end while alpha goes from full to nothing.
        static void Fade(ParticleSystem system, Color start, Color end)
        {
            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(MakeGradient(start, new Color(end.r, end.g, end.b, 0f)));
        }

        static void Shrink(ParticleSystem system, float endScale) => Grow(system, 1f, endScale);

        static void Grow(ParticleSystem system, float startScale, float endScale)
        {
            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, startScale, 1f, endScale));
        }

        static Gradient MakeGradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
            return gradient;
        }
    }
}
