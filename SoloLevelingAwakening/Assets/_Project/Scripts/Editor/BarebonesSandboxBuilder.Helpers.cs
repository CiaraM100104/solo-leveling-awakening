using System;
using System.IO;
using System.Linq;
using SLA.AI;
using SLA.DebugTools;
using SLA.Gates;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace SLA.Editor
{
    public static partial class BarebonesSandboxBuilder
    {
        // ---------------------------------------------------------------- objects

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>An empty marker at a world position.</summary>
        private static Transform Marker(Transform parent, string name, Vector3 worldPosition, Quaternion worldRotation)
        {
            var t = Child(parent, name);
            t.SetPositionAndRotation(worldPosition, worldRotation);
            return t;
        }

        /// <summary>A solid box at a world position.</summary>
        private static GameObject Box(Transform parent, string name, Vector3 worldPosition, Vector3 size, Material material)
        {
            var go = Primitive(PrimitiveType.Cube, name, parent, Vector3.zero, Vector3.zero, size, material, true);
            go.transform.position = worldPosition;
            return go;
        }

        private static void PointLight(Transform parent, string name, Vector3 worldPosition, Color color, float range, float intensity)
        {
            var t = Child(parent, name);
            t.position = worldPosition;
            var light = t.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        /// <summary>A primitive at a local position under <paramref name="parent"/>.</summary>
        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localEuler, Vector3 localScale, Material material, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            return go;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject FindSamplePrefab(string fileName)
        {
            return AssetDatabase.FindAssets("t:Prefab " + fileName, new[] { "Assets/Samples" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == fileName)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .FirstOrDefault();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path)?.Replace(Path.DirectorySeparatorChar, '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------------------------------------------------------------- UI

        private static Canvas NewCanvas(string name, Transform parent, Vector2 sizePixels, float metersPerPixel, Camera interactiveFor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = sizePixels;
            rect.localScale = Vector3.one * metersPerPixel;
            if (interactiveFor != null)
            {
                // UI-12: XRI rays and pokes press these buttons.
                canvas.worldCamera = interactiveFor;
                go.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
            return canvas;
        }

        private static Image NewImage(string name, Transform parent, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, float inset, TMP_FontAsset font,
            float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private struct ButtonParts
        {
            public Button Button;
            public TMP_Text Label;
        }

        private static ButtonParts NewButton(string name, Transform parent, string label, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = Color.white;
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.1f, 0.28f, 0.45f);
            colors.highlightedColor = new Color(0.25f, 0.72f, 1f);
            colors.pressedColor = new Color(1f, 0.79f, 0.29f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.12f, 0.14f, 0.18f);
            button.colors = colors;

            var text = NewText("Label", go.transform, 0f, font, 32f, TextAlignmentOptions.Center);
            text.text = label;
            text.color = UI.SystemPalette.Text;
            return new ButtonParts { Button = button, Label = text };
        }

        /// <summary>A bar background with a fill whose right anchor shows the value.</summary>
        private static RectTransform Bar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color fillColor)
        {
            var back = NewImage(name, parent, 0f);
            back.color = new Color(0f, 0f, 0f, 0.6f);
            SetRect(back.rectTransform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var fill = NewImage("Fill", back.transform, 0f);
            fill.color = fillColor;
            return fill.rectTransform;
        }

        /// <summary>UI-1 coloured edge: four thin strips, so nothing shows through the 80% navy background.</summary>
        private static Image[] AddBorder(Transform parent, float thickness)
        {
            var strips = new Image[4];
            var anchors = new[]
            {
                (new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -thickness), Vector2.zero),
                (Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness)),
                (Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f)),
                (new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero),
            };
            for (int i = 0; i < strips.Length; i++)
            {
                strips[i] = NewImage("Border" + i, parent, 0f);
                SetRect(strips[i].rectTransform, anchors[i].Item1, anchors[i].Item2, anchors[i].Item3, anchors[i].Item4);
                strips[i].color = UI.SystemPalette.Info;
            }
            return strips;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        // ---------------------------------------------------------------- serialized fields

        private static SerializedProperty Property(SerializedObject so, string field)
        {
            var property = so.FindProperty(field);
            if (property == null)
            {
                throw new InvalidOperationException($"{so.targetObject.GetType().Name} has no serialized field {field}");
            }
            return property;
        }

        private static void Edit(Object target, string field, Action<SerializedProperty> change)
        {
            var so = new SerializedObject(target);
            change(Property(so, field));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Object value) =>
            Edit(target, field, p => p.objectReferenceValue = value);

        private static void SetInt(Object target, string field, int value) =>
            Edit(target, field, p => p.intValue = value);

        private static void SetEnum(Object target, string field, int value) =>
            Edit(target, field, p => p.enumValueIndex = value);

        private static void SetVector3(Object target, string field, Vector3 value) =>
            Edit(target, field, p => p.vector3Value = value);

        private static void SetLayerMask(Object target, string field, int mask) =>
            Edit(target, field, p => p.intValue = mask);

        private static void SetArray(Object target, string field, params Object[] values) =>
            Edit(target, field, p =>
            {
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }
            });

        private static void SetPools(EnemyDirector director, params (GameObject prefab, int count)[] pools) =>
            Edit(director, "_pools", p =>
            {
                p.arraySize = pools.Length;
                for (int i = 0; i < pools.Length; i++)
                {
                    var element = p.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("prefab").objectReferenceValue = pools[i].prefab;
                    element.FindPropertyRelative("count").intValue = pools[i].count;
                }
            });

        private static void SetRacks(WeaponStowing stowing, params (int areaId, Transform[] slots)[] racks) =>
            Edit(stowing, "_racks", p =>
            {
                p.arraySize = racks.Length;
                for (int i = 0; i < racks.Length; i++)
                {
                    var element = p.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("areaId").intValue = racks[i].areaId;
                    var slots = element.FindPropertyRelative("slots");
                    slots.arraySize = racks[i].slots.Length;
                    for (int s = 0; s < racks[i].slots.Length; s++)
                    {
                        slots.GetArrayElementAtIndex(s).objectReferenceValue = racks[i].slots[s];
                    }
                }
            });

        private static void SetAreaLooks(AreaAmbience ambience, params (int areaId, Color ambient)[] looks) =>
            Edit(ambience, "_looks", p =>
            {
                p.arraySize = looks.Length;
                for (int i = 0; i < looks.Length; i++)
                {
                    var element = p.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("areaId").intValue = looks[i].areaId;
                    element.FindPropertyRelative("ambient").colorValue = looks[i].ambient;
                }
            });

        /// <summary>F5 cycles hub spawn → gate entrance → room 1 → room 2 → boss room.</summary>
        private static void SetCheckpoints(DebugCheats cheats, Transform hubSpawn, Transform[] gateCheckpoints) =>
            Edit(cheats, "_checkpoints", p =>
            {
                p.arraySize = gateCheckpoints.Length + 1;
                for (int i = 0; i < p.arraySize; i++)
                {
                    var element = p.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("point").objectReferenceValue = i == 0 ? hubSpawn : gateCheckpoints[i - 1];
                    element.FindPropertyRelative("areaId").intValue = i == 0 ? HubAreaId : GateAreaId;
                }
            });
    }
}
