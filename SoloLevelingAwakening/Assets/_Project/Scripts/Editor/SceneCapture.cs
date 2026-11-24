using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SLA.Editor
{
    /// <summary>
    /// Renders still views of the sandbox scene to Logs/SceneCapture/ (git-ignored) for layout checks without
    /// a headset or Play mode. Colours that scripts set at runtime show as their editor defaults.
    /// Batch mode: -executeMethod SLA.Editor.SceneCapture.CaptureSandbox
    /// </summary>
    public static class SceneCapture
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/Sandbox_Barebones.unity";
        private const int Width = 1280;
        private const int Height = 720;

        private static readonly (string name, Vector3 eye, Vector3 lookAt)[] Views =
        {
            ("title", new Vector3(-100f, 1.6f, -1f), new Vector3(-100f, 1.6f, 1.6f)),
            ("hub_gate_wall", new Vector3(0f, 1.6f, -1.2f), new Vector3(0f, 1.6f, 2.5f)),
            ("hub_east_wall", new Vector3(-0.6f, 1.6f, 0.5f), new Vector3(2.5f, 1.45f, 0.55f)),
            ("hub_south_wall", new Vector3(0.5f, 1.6f, 0.5f), new Vector3(0.7f, 1.4f, -2.5f)),
            ("tutorial", new Vector3(0f, 1.6f, -102.6f), new Vector3(0f, 1.6f, -96f)),
            ("boss_room_results", new Vector3(100f, 1.6f, 45f), new Vector3(100f, 2.2f, 51f)),
        };

        [MenuItem("SLA/Art/Capture Sandbox Views")]
        public static void CaptureSandbox()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.34f);

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "SceneCapture");
            Directory.CreateDirectory(folder);
            var go = new GameObject("CaptureCamera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var target = new RenderTexture(Width, Height, 24);
            cam.targetTexture = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                foreach (var view in Views)
                {
                    go.transform.SetPositionAndRotation(view.eye, Quaternion.LookRotation(view.lookAt - view.eye));
                    cam.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(folder, view.name + ".png"), image.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(go);
            }
            Debug.Log("[SLA] Scene views saved to " + folder);
            // The scene is not saved: the capture camera never touches the asset.
            EditorSceneManager.CloseScene(scene, false);
        }
    }
}
