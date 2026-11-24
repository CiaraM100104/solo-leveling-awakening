using SLA.Data;
using UnityEngine;

namespace SLA.Combat
{
    /// <summary>Everything <see cref="DamageCalculator"/> needs to know about one hit.</summary>
    public readonly struct DamageInput
    {
        public readonly float BaseDamage;
        public readonly float HitSpeedMetersPerSecond;
        /// <summary>Strength points above the base stat (PRG-5 says "per point above 10").</summary>
        public readonly int StrengthBonusPoints;
        public readonly HitZone Zone;
        public readonly bool IsCritical;
        public readonly float TargetArmor;
        public readonly bool TargetStaggered;

        public DamageInput(float baseDamage, float hitSpeedMetersPerSecond, int strengthBonusPoints, HitZone zone,
            bool isCritical, float targetArmor, bool targetStaggered)
        {
            BaseDamage = baseDamage;
            HitSpeedMetersPerSecond = hitSpeedMetersPerSecond;
            StrengthBonusPoints = strengthBonusPoints;
            Zone = zone;
            IsCritical = isCritical;
            TargetArmor = targetArmor;
            TargetStaggered = targetStaggered;
        }
    }

    /// <summary>Pure damage math for CMB-4 to CMB-8 and the parry window (CMB-14).</summary>
    public static class DamageCalculator
    {
        /// <summary>CMB-4: base × swing × (1 + k × STR) × zone × crit − armor, floored at 0.</summary>
        public static float Calculate(in DamageInput input, CombatConfig config)
        {
            float raw = input.BaseDamage
                        * SwingFactor(input.HitSpeedMetersPerSecond, config)
                        * (1f + config.strengthDamagePerPoint * input.StrengthBonusPoints)
                        * ZoneMultiplier(input.Zone, config)
                        * (input.IsCritical ? config.critMultiplier : 1f);

            // CMB-8: staggered targets ignore armor.
            float armor = input.TargetStaggered ? 0f : input.TargetArmor;
            return Mathf.Max(0f, raw - armor);
        }

        /// <summary>CMB-5: 0 below the minimum speed, linear to 1 at full speed, capped.</summary>
        public static float SwingFactor(float hitSpeedMetersPerSecond, CombatConfig config)
        {
            if (hitSpeedMetersPerSecond < config.minHitSpeed)
            {
                return 0f;
            }
            float t = (hitSpeedMetersPerSecond - config.minHitSpeed) / (config.fullHitSpeed - config.minHitSpeed);
            return Mathf.Min(t, config.swingFactorCap);
        }

        /// <summary>CMB-6 zone multipliers.</summary>
        public static float ZoneMultiplier(HitZone zone, CombatConfig config)
        {
            switch (zone)
            {
                case HitZone.Head: return config.headMultiplier;
                case HitZone.Limb: return config.limbMultiplier;
                case HitZone.WeakPoint: return config.weakPointMultiplier;
                default: return config.torsoMultiplier;
            }
        }

        /// <summary>CMB-14: parry window = base + per-point × Perception points above the base stat.</summary>
        public static float ParryWindowSeconds(int perceptionBonusPoints, CombatConfig config)
        {
            return config.parryWindowBase + config.parryWindowPerPerception * Mathf.Max(0, perceptionBonusPoints);
        }
    }
}
