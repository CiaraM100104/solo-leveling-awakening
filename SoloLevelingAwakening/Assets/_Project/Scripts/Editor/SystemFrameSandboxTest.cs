using SLA.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SLA.EditorTools
{
    /// <summary>
    /// Look test for the glowing System window frame: puts UI_Frame_SystemWindow.png (9-sliced) on the
    /// Comfort Debug Panel in Sandbox_Ciara only, in place of its flat navy fill and outline.
    /// Runs once by itself after it compiles; Tools > SLA > Sandbox > Apply System Frame to Ciara's Menu re-runs it.
    /// </summary>
    [InitializeOnLoad]
    public static class SystemFrameSandboxTest
    {
        const string SpritePath = ProjectConfigurator.Root + "/Art/UI/UI_Frame_SystemWindow.png";
        const string ScenePath = ProjectConfigurator.ScenesDir + "/Sandbox/Sandbox_Ciara.unity";
        const string RanOnceKey = "SLA.SystemFrameSandboxTest.RanOnce";

        // The corner ornaments sit inside about 130 px of each edge of the 640 px image.
        static readonly Vector4 BorderPixels = new Vector4(130f, 130f, 130f, 130f);
        // 130 px corners / 2 = 65 mm on the panel, so the buttons still fit inside the frame.
        const float CornerShrink = 2f;
        const int Padding = 64;

        static SystemFrameSandboxTest()
        {
            if (Application.isBatchMode || EditorPrefs.GetBool(RanOnceKey, false)) return;
            EditorApplication.delayCall += RunOnce;
        }

        static void RunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += RunOnce;
                return;
            }
            EditorPrefs.SetBool(RanOnceKey, true);
            Run();
        }

        [MenuItem("Tools/SLA/Sandbox/Apply System Frame to Ciara's Menu")]
        public static void Run()
        {
            ImportAsSprite();

            // Edit the scene where it is if it's already open (keeps the user's view); otherwise open it.
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            ComfortDebugMenu menu = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                menu = root.GetComponentInChildren<ComfortDebugMenu>(true);
                if (menu != null) break;
            }
            if (menu == null)
            {
                Debug.LogWarning("[SystemFrameSandboxTest] Sandbox_Ciara has no Comfort Debug Panel to frame.");
                return;
            }

            var image = menu.GetComponent<Image>();
            Undo.RecordObject(image, "Apply System frame");
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.color = Color.white; // the art carries its own colors
            image.pixelsPerUnitMultiplier = CornerShrink;

            var outline = menu.GetComponent<Outline>();
            if (outline != null)
            {
                Undo.RecordObject(outline, "Apply System frame");
                outline.enabled = false;
            }

            var layout = menu.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                Undo.RecordObject(layout, "Apply System frame");
                layout.padding = new RectOffset(Padding, Padding, Padding, Padding);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = menu.gameObject;
            Debug.Log("[SystemFrameSandboxTest] Framed the Comfort Debug Panel in Sandbox_Ciara.");
        }

        static void ImportAsSprite()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            if (importer == null) throw new System.InvalidOperationException($"{SpritePath} is missing.");
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == BorderPixels) return;

            // ART_PIPELINE: UI sprites have no mipmaps and use ASTC 4x4 on Android.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = BorderPixels;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_4x4;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }
    }
}
