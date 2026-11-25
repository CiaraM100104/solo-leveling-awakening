using SLA.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SLA.Editor
{
    public static partial class BarebonesSandboxBuilder
    {
        private const string HitSparkPrefabPath = Root + "/Art/VFX/VFX_HitSpark_Mana.prefab";
        private const int HitSparkPoolSize = 6;

        /// <summary>
        /// CMB-20's cyan spark burst. Built with the Particle System per ART_PIPELINE (no VFX Graph, at most 200
        /// particles, unlit additive, no lights). Artists can restyle the prefab; code only calls Emit on it.
        /// The prefab is created once and kept, so hand-made changes survive rebuilds.
        /// </summary>
        private static GameObject HitSparkPrefab(Context ctx)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HitSparkPrefabPath);
            if (existing != null)
            {
                return existing;
            }
            EnsureFolder(Root + "/Art/VFX");

            var go = new GameObject("VFX_HitSpark_Mana");
            var sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.9f, 1f), new Color(0.75f, 0.97f, 1f));
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = sparks.emission;
            emission.enabled = false; // Bursts come from code (Emit), sized by damage.

            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            var fade = sparks.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var shrink = sparks.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.5f;
            renderer.sharedMaterial = SparkMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return SavePrefab(go, HitSparkPrefabPath);
        }

        /// <summary>URP Particles/Unlit, additive: sparks brighten what's behind them and never cast light.</summary>
        private static Material SparkMaterial()
        {
            string path = Root + "/Art/Materials/M_VFX_Spark_Additive.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);   // Transparent
            mat.SetFloat("_Blend", 2f);     // Additive
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildHitSparks(Context ctx, Transform parent)
        {
            var prefab = HitSparkPrefab(ctx);
            var root = Child(parent, "HitSparks");
            var pool = new ParticleSystem[HitSparkPoolSize];
            for (int i = 0; i < pool.Length; i++)
            {
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                copy.name = "HitSpark_" + i;
                pool[i] = copy.GetComponent<ParticleSystem>();
            }
            var sparks = root.gameObject.AddComponent<HitSparks>();
            Set(sparks, "_combat", ctx.Data.Combat);
            Set(sparks, "_hits", ctx.Data.Hits);
            SetArray(sparks, "_pool", pool);
        }
    }
}
