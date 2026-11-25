using System.IO;
using System.Linq;
using SLA.Items;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SLA.Editor
{
    public static partial class BarebonesSandboxBuilder
    {
        private const string GrabIconPath = Root + "/Art/UI/UI_Icon_Grab.png";
        private const int GrabIconSize = 128;

        /// <summary>
        /// Adds the "you can pick this up" highlight and floating icon to a weapon root. Glows the art model when
        /// there is one, otherwise the placeholder shapes. Safe to run again: it replaces an existing icon.
        /// </summary>
        private static void AddGrabHint(GameObject weapon, TMP_FontAsset font)
        {
            var old = weapon.transform.Find("GrabIcon");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var model = weapon.transform.Find("Model");
            var renderers = model != null
                ? model.GetComponentsInChildren<Renderer>(true)
                : weapon.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();

            var canvas = NewCanvas("GrabIcon", weapon.transform, new Vector2(160f, 210f), 0.0006f, null);
            canvas.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var icon = NewImage("Icon", canvas.transform, 0f);
            icon.sprite = GrabIconSprite();
            icon.color = UI.SystemPalette.Info;
            SetRect(icon.rectTransform, new Vector2(0.05f, 0.25f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
            icon.preserveAspect = true;

            var label = NewText("Label", canvas.transform, 0f, font, 36f, TextAlignmentOptions.Center);
            SetRect((RectTransform)label.transform, Vector2.zero, new Vector2(1f, 0.25f), Vector2.zero, Vector2.zero);
            label.fontStyle = FontStyles.Bold;
            label.color = UI.SystemPalette.Text;
            label.text = "GRIP";
            SetLayerRecursively(canvas.gameObject, LayerMask.NameToLayer("UI"));

            var hint = weapon.GetComponent<GrabHint>();
            if (hint == null)
            {
                hint = weapon.AddComponent<GrabHint>();
            }
            SetArray(hint, "_renderers", renderers);
            Set(hint, "_icon", canvas.transform);
            Set(hint, "_iconGroup", group);
        }

        /// <summary>A ring with a downward chevron: "here, take this". Generated once, white, tinted by the Image.</summary>
        private static Sprite GrabIconSprite()
        {
            if (!File.Exists(GrabIconPath))
            {
                EnsureFolder(Path.GetDirectoryName(GrabIconPath)?.Replace(Path.DirectorySeparatorChar, '/'));
                var texture = new Texture2D(GrabIconSize, GrabIconSize, TextureFormat.RGBA32, false);
                float c = (GrabIconSize - 1) / 2f;
                for (int y = 0; y < GrabIconSize; y++)
                {
                    for (int x = 0; x < GrabIconSize; x++)
                    {
                        var p = new Vector2(x, y);
                        float ring = Mathf.Abs(Vector2.Distance(p, new Vector2(c, c)) - 56f) - 4f;
                        // Chevron pointing down, centred in the ring.
                        float left = DistanceToSegment(p, new Vector2(c - 26f, c + 14f), new Vector2(c, c - 14f)) - 6f;
                        float right = DistanceToSegment(p, new Vector2(c + 26f, c + 14f), new Vector2(c, c - 14f)) - 6f;
                        float shape = Mathf.Min(ring, Mathf.Min(left, right));
                        float alpha = Mathf.Clamp01(0.5f - shape); // One pixel of anti-aliasing.
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                File.WriteAllBytes(GrabIconPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(GrabIconPath);

                // ART_PIPELINE: UI sprites, no mipmaps, small, ASTC 4×4 on Quest.
                var importer = (TextureImporter)AssetImporter.GetAtPath(GrabIconPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 128;
                var android = importer.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = 128;
                android.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(GrabIconPath);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
