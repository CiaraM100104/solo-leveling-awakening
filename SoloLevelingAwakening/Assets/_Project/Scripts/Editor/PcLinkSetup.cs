using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace SLA.EditorTools
{
    /// <summary>
    /// Lets Play mode run on a Quest over Meta Horizon Link: turns on the OpenXR loader for Windows and
    /// the Quest controller profiles there. Without a profile, OpenXR renders to the headset but sends no
    /// controller input, so nothing can be pointed at or clicked. Leaves the Android (Quest build) settings alone.
    /// Runs once per machine after it first compiles; Tools > SLA > Enable PC VR (Link) re-runs it.
    /// </summary>
    [InitializeOnLoad]
    public static class PcLinkSetup
    {
        const string RanOnceKey = "SLA.PcLinkSetup.RanOnce";

        static readonly string[] ControllerProfiles =
        {
            "OculusTouchControllerProfile",
            "MetaQuestTouchPlusControllerProfile",
            "MetaQuestTouchProControllerProfile",
        };

        static PcLinkSetup()
        {
            if (Application.isBatchMode || EditorPrefs.GetBool(RanOnceKey, false)) return;
            EditorApplication.delayCall += RunOnce;
        }

        static void RunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunOnce;
                return;
            }
            if (Configure()) EditorPrefs.SetBool(RanOnceKey, true);
        }

        [MenuItem("Tools/SLA/Enable PC VR (Link)")]
        public static void ConfigureMenu() => Configure();

        public static bool Configure()
        {
            const BuildTargetGroup pc = BuildTargetGroup.Standalone;

            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                Debug.LogError("[PcLinkSetup] No XR settings asset; run Tools > SLA > Configure Project Settings first.");
                return false;
            }

            if (!perTarget.HasSettingsForBuildTarget(pc)) perTarget.CreateDefaultSettingsForBuildTarget(pc);
            if (!perTarget.HasManagerSettingsForBuildTarget(pc)) perTarget.CreateDefaultManagerSettingsForBuildTarget(pc);

            perTarget.SettingsForBuildTarget(pc).InitManagerOnStart = true;
            var manager = perTarget.ManagerSettingsForBuildTarget(pc);
            XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", pc);
            EditorUtility.SetDirty(perTarget);

            FeatureHelpers.RefreshFeatures(pc);
            var openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(pc);
            if (openXr == null)
            {
                Debug.LogWarning("[PcLinkSetup] No OpenXR settings for Windows yet; open Project Settings > XR Plug-in Management once, then run Tools > SLA > Enable PC VR (Link).");
                return false;
            }

            foreach (var feature in openXr.GetFeatures())
            {
                if (ControllerProfiles.Contains(feature.GetType().Name) && !feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                }
            }
            EditorUtility.SetDirty(openXr);
            AssetDatabase.SaveAssets();

            string loaders = string.Join(", ", manager.activeLoaders.Select(l => l.GetType().Name));
            string profiles = string.Join(", ", openXr.GetFeatures().Where(f => f.enabled && ControllerProfiles.Contains(f.GetType().Name)).Select(f => f.GetType().Name));
            Debug.Log($"[PcLinkSetup] Windows XR loaders: {loaders}. Controller profiles on: {profiles}.");
            return true;
        }
    }
}
