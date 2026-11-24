using UnityEngine;

namespace SLA.Data
{
    /// <summary>Comfort preset numbers (docs/design/comfort-and-accessibility.md, presets table).</summary>
    [CreateAssetMenu(menuName = "SLA/Config/Comfort Config")]
    public class ComfortConfig : ScriptableObject
    {
        [Tooltip("CMF presets: smooth movement walks at 1.5 m/s.")]
        public float walkSpeedMetersPerSecond = 1.5f;
        [Tooltip("PLACEHOLDER: vignette aperture for the 'Medium' setting (smaller = stronger).")]
        public float mediumVignetteAperture = 0.7f;
        [Tooltip("PLACEHOLDER: vignette aperture for the 'Strong' setting.")]
        public float strongVignetteAperture = 0.5f;
    }
}
