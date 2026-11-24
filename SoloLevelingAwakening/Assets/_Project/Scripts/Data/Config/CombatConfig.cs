using UnityEngine;

namespace SLA.Data
{
    /// <summary>
    /// Every tunable combat number (docs/design/combat.md, Data table). Defaults are the spec values;
    /// fields marked PLACEHOLDER are not in the spec yet.
    /// </summary>
    [CreateAssetMenu(menuName = "SLA/Config/Combat Config")]
    public class CombatConfig : ScriptableObject
    {
        [Header("Swing factor (CMB-5)")]
        [Tooltip("Relative hit speed in m/s below which a hit does nothing.")]
        public float minHitSpeed = 1.5f;
        [Tooltip("Relative hit speed in m/s that gives a swing factor of 1.")]
        public float fullHitSpeed = 5f;
        public float swingFactorCap = 1.2f;

        [Header("Damage (CMB-4, CMB-6, CMB-7, CMB-11)")]
        [Tooltip("Damage bonus per Strength point above the base stat (PRG-5).")]
        public float strengthDamagePerPoint = 0.02f;
        public float headMultiplier = 2f;
        public float torsoMultiplier = 1f;
        public float limbMultiplier = 0.6f;
        public float weakPointMultiplier = 2.5f;
        public float critMultiplier = 1.5f;
        public float bluntFraction = 0.3f;

        [Header("Anti-cheese (CMB-9, CMB-10, CMB-12)")]
        [Tooltip("Metres the weapon tip must travel inside the swing window.")]
        public float minSwingArc = 0.3f;
        public float swingArcWindow = 0.25f;
        public float perTargetCooldown = 0.25f;
        public float contactDragSeconds = 0.1f;

        [Header("Defense (CMB-13, CMB-14)")]
        public float blockDamageReduction = 0.8f;
        public float blockFatigueCost = 10f;
        public float parryWindowBase = 0.15f;
        public float parryWindowPerPerception = 0.004f;
        public float parryMinSpeed = 2f;
        public float staggerSeconds = 1.5f;
        [Tooltip("PLACEHOLDER: radius of the check for a held weapon between an enemy weapon and the player's head.")]
        public float blockProbeRadiusMeters = 0.08f;

        [Header("Pacing (CMB-18, ENM-2, ENM-4)")]
        public int maxSimultaneousAttackers = 2;
        public int maxActiveEnemies = 6;

        [Header("Hit feedback (CMB-20)")]
        public float hapticMinSeconds = 0.04f;
        public float hapticMaxSeconds = 0.12f;
        public float hitstopMinSeconds = 0.04f;
        public float hitstopMaxSeconds = 0.06f;
        [Tooltip("PLACEHOLDER: damage at which feedback reaches its maximum.")]
        public float feedbackFullScaleDamage = 20f;
        [Tooltip("Cyan hit flash on the enemy (CMB-20).")]
        public Color hitFlashColor = new Color(0.25f, 0.9f, 1f);
        [Tooltip("PLACEHOLDER.")]
        public float hitFlashSeconds = 0.1f;
        [Tooltip("Red weapon glow during an enemy windup (CMB-17).")]
        public Color tellGlowColor = new Color(1.5f, 0.1f, 0.1f);
        [Tooltip("PLACEHOLDER: purple tint while staggered after a parry.")]
        public Color staggerColor = new Color(0.5f, 0.2f, 0.9f);
    }
}
