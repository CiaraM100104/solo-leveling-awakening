using System.Collections;
using UnityEngine;

namespace SLA.Gates
{
    /// <summary>
    /// A black shell around the camera that fades in and out around travel (MRH-8). Comfort: the view
    /// never moves while visible; the player only changes place while the screen is black (CMF-1).
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _renderer;

        private MaterialPropertyBlock _block;
        private float _alpha;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            SetAlpha(0f);
        }

        public IEnumerator FadeTo(float targetAlpha, float seconds)
        {
            float start = _alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, targetAlpha, t / seconds));
                yield return null;
            }
            SetAlpha(targetAlpha);
        }

        private void SetAlpha(float alpha)
        {
            _alpha = alpha;
            _renderer.enabled = alpha > 0.001f;
            _block.SetColor(BaseColorId, new Color(0f, 0f, 0f, alpha));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
