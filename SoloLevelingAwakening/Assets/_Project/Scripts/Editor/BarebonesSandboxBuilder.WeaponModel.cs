using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SLA.Editor
{
    public static partial class BarebonesSandboxBuilder
    {
        private const string DaggerPrefabPath = Root + "/Prefabs/Weapons/RustyDagger.prefab";
        private const string ShadowDaggerFolder = Root + "/Art/Weapons/ShadowDagger";
        private const string ShadowDaggerModelPath = ShadowDaggerFolder + "/SM_ShadowDagger.fbx";
        private const string ShadowDaggerTexturePath = ShadowDaggerFolder + "/T_ShadowDagger_D.png";
        private const string ShadowDaggerMaterialPath = Root + "/Art/Materials/M_ShadowDagger.mat";

        /// <summary>
        /// How the Shadow Dagger model sits in the dagger prefab. The prefab's frame: grip (Attach) at the origin,
        /// blade along +Z, blade flat facing ±Y. The model as imported: tip toward +Y, blade edges toward ±Z.
        /// </summary>
        private static class ShadowDaggerFit
        {
            /// <summary>Whole weapon, pommel to tip. A real dagger is 30–40 cm.</summary>
            public const float LengthMeters = 0.38f;
            /// <summary>Where the hand closes, measured from the pommel, as a fraction of the length.</summary>
            public const float GripFraction = 0.2f;
            /// <summary>Where the guard sits (measured from the preview renders).</summary>
            public const float GuardFraction = 0.325f;
            /// <summary>Collider sizes, slimmer than the jagged silhouette so swings don't snag.</summary>
            public const float HandleDiameterMeters = 0.035f;
            public const float BladeWidthMeters = 0.06f;
            public const float BladeThicknessMeters = 0.012f;

            /// <summary>Model +Y → prefab +Z (tip forward), model +Z → prefab +X (edges sideways).</summary>
            public static readonly Quaternion Rotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>
        /// Puts the Shadow Dagger model on the existing dagger prefab without rebuilding the scene.
        /// The prefab is edited in place, so the daggers already in scenes keep their settings.
        /// </summary>
        [MenuItem("SLA/Art/Apply Shadow Dagger Model")]
        public static void ApplyShadowDaggerToPrefab()
        {
            var contents = PrefabUtility.LoadPrefabContents(DaggerPrefabPath);
            try
            {
                if (ApplyShadowDagger(contents))
                {
                    AddGrabHint(contents, AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(TmpFontPath));
                    PrefabUtility.SaveAsPrefabAsset(contents, DaggerPrefabPath);
                    Debug.Log("[SLA] Shadow Dagger model applied to " + DaggerPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Fits the model onto a dagger root built by <see cref="BuildDaggerPrefab"/>. False if the model is missing.</summary>
        private static bool ApplyShadowDagger(GameObject dagger)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ShadowDaggerModelPath);
            if (model == null)
            {
                return false;
            }
            ConfigureShadowDaggerImport();
            model = AssetDatabase.LoadAssetAtPath<GameObject>(ShadowDaggerModelPath);
            var material = ShadowDaggerMaterial();

            var old = dagger.transform.Find("Model");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // Measure the model in its own space: pommel at min Y, tip at max Y.
            var points = ModelPoints(model);
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (var p in points)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
            float length = maxY - minY;
            float scale = ShadowDaggerFit.LengthMeters / length;

            // The grip's centre: average of the handle vertices around the grip height.
            float gripY = minY + length * ShadowDaggerFit.GripFraction;
            Vector3 gripCenter = Vector3.zero;
            int count = 0;
            foreach (var p in points)
            {
                if (Mathf.Abs(p.y - gripY) < length * 0.05f)
                {
                    gripCenter += p;
                    count++;
                }
            }
            gripCenter = count > 0 ? gripCenter / count : new Vector3(0f, gripY, 0f);
            gripCenter.y = gripY;

            // A holder carries the fit, so the model keeps its own import transform (this FBX has rotation and
            // ×100 scale on its root).
            var holder = new GameObject("Model").transform;
            holder.SetParent(dagger.transform, false);
            holder.localRotation = ShadowDaggerFit.Rotation;
            holder.localScale = Vector3.one * scale;
            holder.localPosition = -(ShadowDaggerFit.Rotation * (gripCenter * scale));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }
            SetLayerRecursively(holder.gameObject, dagger.layer);

            // Colliders: simple shapes along the model (mesh colliders cost too much on Quest).
            float butt = -ShadowDaggerFit.LengthMeters * ShadowDaggerFit.GripFraction;
            float guard = ShadowDaggerFit.LengthMeters * (ShadowDaggerFit.GuardFraction - ShadowDaggerFit.GripFraction);
            float tip = ShadowDaggerFit.LengthMeters * (1f - ShadowDaggerFit.GripFraction);

            var handle = dagger.transform.Find("Handle");
            handle.localPosition = new Vector3(0f, 0f, (butt + guard) / 2f);
            handle.localScale = new Vector3(ShadowDaggerFit.HandleDiameterMeters, (guard - butt) / 2f, ShadowDaggerFit.HandleDiameterMeters);
            var blade = dagger.transform.Find("Blade");
            blade.localPosition = new Vector3(0f, 0f, (guard + tip) / 2f);
            blade.localScale = new Vector3(ShadowDaggerFit.BladeWidthMeters, ShadowDaggerFit.BladeThicknessMeters, tip - guard);
            dagger.transform.Find("Tip").localPosition = new Vector3(0f, 0f, tip);

            // The placeholder shapes keep their colliders but stop drawing.
            foreach (var name in new[] { "Handle", "Guard", "Blade" })
            {
                var part = dagger.transform.Find(name);
                if (part != null && part.TryGetComponent(out Renderer r))
                {
                    r.enabled = false;
                }
            }
            return true;
        }

        private static List<Vector3> ModelPoints(GameObject model)
        {
            var points = new List<Vector3>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }
                // The asset sits at the origin, so world space here includes the root's own import transform.
                var toModelSpace = filter.transform.localToWorldMatrix;
                foreach (var v in filter.sharedMesh.vertices)
                {
                    points.Add(toModelSpace.MultiplyPoint3x4(v));
                }
            }
            return points;
        }

        /// <summary>ART_PIPELINE import settings for the model and its texture.</summary>
        private static void ConfigureShadowDaggerImport()
        {
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(ShadowDaggerModelPath);
            bool modelChanged = modelImporter.materialImportMode != ModelImporterMaterialImportMode.None
                                || modelImporter.isReadable
                                || modelImporter.meshCompression != ModelImporterMeshCompression.Medium
                                || modelImporter.importAnimation;
            if (modelChanged)
            {
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
                modelImporter.isReadable = false;
                modelImporter.meshCompression = ModelImporterMeshCompression.Medium;
                modelImporter.importAnimation = false;
                modelImporter.importCameras = false;
                modelImporter.importLights = false;
                modelImporter.SaveAndReimport();
            }

            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(ShadowDaggerTexturePath);
            var android = textureImporter.GetPlatformTextureSettings("Android");
            if (textureImporter.maxTextureSize != 1024 || !android.overridden)
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.sRGBTexture = true;
                textureImporter.mipmapEnabled = true;
                textureImporter.maxTextureSize = 1024;
                android.overridden = true;
                android.maxTextureSize = 1024;
                android.format = TextureImporterFormat.ASTC_6x6;
                textureImporter.SetPlatformTextureSettings(android);
                textureImporter.SaveAndReimport();
            }
        }

        private static Material ShadowDaggerMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ShadowDaggerMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, ShadowDaggerMaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ShadowDaggerTexturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0.5f);
            material.SetFloat("_Smoothness", 0.55f);
            // Emission on (black) so the grab highlight can glow; URP needs the emissive GI flag to keep it.
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
