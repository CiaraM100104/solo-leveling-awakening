using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SLA.Editor
{
    /// <summary>
    /// Checks an imported model against docs/ART_PIPELINE.md budgets: triangles, size, materials,
    /// and saves preview renders from three sides to Logs/ModelReport/ (git-ignored).
    /// Select a model in the Project window, then SLA → Art → Report Selected Model.
    /// </summary>
    public static class ModelReport
    {
        private const int PreviewSize = 512;

        [MenuItem("SLA/Art/Report Selected Model")]
        public static void ReportSelected()
        {
            var model = Selection.activeObject as GameObject;
            if (model == null)
            {
                Debug.LogWarning("[SLA] Select a model asset in the Project window first.");
                return;
            }
            Report(model);
        }

        [MenuItem("SLA/Art/Report All Weapon Models")]
        public static void ReportWeapons()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Project/Art/Weapons" }))
            {
                Report(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            }
        }

        /// <summary>Batch mode: -executeMethod SLA.Editor.ModelReport.ReportFromCommandLine -reportPath Assets/...</summary>
        public static void ReportFromCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-reportPath")
                {
                    Report(AssetDatabase.LoadAssetAtPath<GameObject>(args[i + 1]));
                }
            }
        }

        [MenuItem("SLA/Art/Report Selected Model", true)]
        private static bool ReportSelectedValidate() => Selection.activeObject is GameObject;

        public static void Report(GameObject model)
        {
            var log = new StringBuilder();
            log.AppendLine($"[SLA] Model report: {AssetDatabase.GetAssetPath(model)}");

            int triangles = 0;
            var bounds = new Bounds();
            bool first = true;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                int t = mesh.triangles.Length / 3;
                triangles += t;
                var renderer = filter.GetComponent<Renderer>();
                log.AppendLine($"  mesh '{filter.name}': {t} tris, {mesh.vertexCount} verts, local bounds {mesh.bounds.size}, " +
                               $"materials {(renderer != null ? renderer.sharedMaterials.Length : 0)}, " +
                               $"local pos {filter.transform.localPosition}, rot {filter.transform.localEulerAngles}, scale {filter.transform.localScale}");
                foreach (var v in mesh.vertices)
                {
                    var world = filter.transform.localToWorldMatrix.MultiplyPoint3x4(v);
                    if (first)
                    {
                        bounds = new Bounds(world, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(world);
                    }
                }
            }
            log.AppendLine($"  total {triangles} triangles (weapon budget 1,500); combined bounds centre {bounds.center}, size {bounds.size}");
            log.AppendLine("  " + EndWidths(model, bounds));

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ModelReport");
            Directory.CreateDirectory(folder);
            RenderPreview(model, bounds, Vector3.forward, Path.Combine(folder, model.name + "_front.png"));
            RenderPreview(model, bounds, Vector3.right, Path.Combine(folder, model.name + "_side.png"));
            RenderPreview(model, bounds, Vector3.down, Path.Combine(folder, model.name + "_top.png"));
            log.AppendLine($"  previews saved to {folder}");
            Debug.Log(log.ToString());
        }

        /// <summary>How wide the model is near each end of its longest axis: the narrow end is usually the tip.</summary>
        private static string EndWidths(GameObject model, Bounds bounds)
        {
            Vector3 size = bounds.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            float min = bounds.min[axis];
            float length = size[axis];
            float lowWidth = 0f;
            float highWidth = 0f;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }
                foreach (var v in filter.sharedMesh.vertices)
                {
                    var p = filter.transform.localToWorldMatrix.MultiplyPoint3x4(v);
                    float along = (p[axis] - min) / length;
                    var offAxis = p - bounds.center;
                    offAxis[axis] = 0f;
                    if (along < 0.08f)
                    {
                        lowWidth = Mathf.Max(lowWidth, offAxis.magnitude);
                    }
                    else if (along > 0.92f)
                    {
                        highWidth = Mathf.Max(highWidth, offAxis.magnitude);
                    }
                }
            }
            string axisName = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
            return $"longest axis {axisName} ({length:0.###}); half-width near -{axisName} end {lowWidth:0.###}, near +{axisName} end {highWidth:0.###}";
        }

        private static void RenderPreview(GameObject model, Bounds bounds, Vector3 viewDirection, string path)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var instance = preview.InstantiatePrefabInScene(model);
                float radius = Mathf.Max(bounds.extents.magnitude, 0.001f);
                var cam = preview.camera;
                cam.orthographic = true;
                cam.orthographicSize = radius * 1.1f;
                cam.nearClipPlane = radius * 0.01f;
                cam.farClipPlane = radius * 10f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.25f, 0.25f, 0.28f);
                cam.transform.position = bounds.center - viewDirection * radius * 3f;
                cam.transform.rotation = Quaternion.LookRotation(viewDirection,
                    Mathf.Abs(Vector3.Dot(viewDirection, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up);
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
                preview.ambientColor = new Color(0.4f, 0.4f, 0.4f);

                preview.BeginStaticPreview(new Rect(0, 0, PreviewSize, PreviewSize));
                preview.Render(true);
                var texture = preview.EndStaticPreview();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(instance);
            }
            finally
            {
                preview.Cleanup();
            }
        }
    }
}
