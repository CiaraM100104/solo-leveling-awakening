using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace SLA.Editor
{
    /// <summary>
    /// Applies the Quest build settings from docs/ARCHITECTURE.md#build-settings-quest.
    /// Safe to run any number of times. Android-only settings need Unity's Android Build Support module.
    /// </summary>
    public static class QuestProjectSettings
    {
        private const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string MetaQuestFeatureSetId = "com.unity.openxr.featureset.meta";
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const int MsaaSamples = 4;
        private const string InteractionLayerSettingsPath = "Assets/XRI/Settings/Resources/InteractionLayerSettings.asset";
        private const int TeleportInteractionLayer = 31;

        private static readonly string[] _controllerProfiles =
        {
            "OculusTouchControllerProfile",
            "MetaQuestTouchPlusControllerProfile",
        };

        [MenuItem("SLA/Apply Quest Project Settings")]
        public static void Apply()
        {
            ApplyXRLoaders();
            ApplyOpenXRFeatures(BuildTargetGroup.Android);
            ApplyOpenXRFeatures(BuildTargetGroup.Standalone);
            ApplyPlayerSettings();
            ApplyUrpSettings();
            ApplyTeleportInteractionLayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[SLA] Quest project settings applied.");
        }

        /// <summary>The XRI starter rig's teleport (the CMF "Comfortable" preset) expects interaction layer 31 named Teleport.</summary>
        public static void ApplyTeleportInteractionLayer()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(InteractionLayerSettingsPath);
            if (asset == null)
            {
                return;
            }
            var settings = new SerializedObject(asset);
            var names = settings.FindProperty("m_LayerNames");
            if (names != null && names.arraySize > TeleportInteractionLayer)
            {
                var slot = names.GetArrayElementAtIndex(TeleportInteractionLayer);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = "Teleport";
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void ApplyXRLoaders()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
            {
                Debug.LogError("[SLA] XR Plug-in Management settings are missing. Open Project Settings > XR Plug-in Management once, then rerun.");
                return;
            }

            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                if (!perTarget.HasSettingsForBuildTarget(group))
                {
                    perTarget.CreateDefaultSettingsForBuildTarget(group);
                }
                if (!perTarget.HasManagerSettingsForBuildTarget(group))
                {
                    perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
                }
                XRPackageMetadataStore.AssignLoader(perTarget.ManagerSettingsForBuildTarget(group), OpenXRLoaderType, group);
            }
        }

        private static void ApplyOpenXRFeatures(BuildTargetGroup group)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null)
            {
                Debug.LogWarning($"[SLA] No OpenXR settings for {group}. Install that platform's build support module, then rerun.");
                return;
            }

            if (group == BuildTargetGroup.Android)
            {
                var metaSet = OpenXRFeatureSetManager.GetFeatureSetWithId(group, MetaQuestFeatureSetId);
                if (metaSet != null)
                {
                    metaSet.isEnabled = true;
                    OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
                }
                else
                {
                    Debug.LogWarning("[SLA] Meta Quest OpenXR feature group not found. Is com.unity.xr.meta-openxr installed?");
                }
            }

            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            foreach (var feature in settings.GetFeatures())
            {
                var typeName = feature.GetType().Name;
                if (typeName == "MetaQuestFeature" || _controllerProfiles.Contains(typeName))
                {
                    feature.enabled = true;
                }
            }
            EditorUtility.SetDirty(settings);
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Solo Leveling: Awakening";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            // Keeps the game running in the editor while the headset over Link has focus.
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        }

        private static void ApplyUrpSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { SettingsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("Mobile"))
                {
                    continue;
                }
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                asset.msaaSampleCount = MsaaSamples;
                asset.supportsHDR = false;
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
