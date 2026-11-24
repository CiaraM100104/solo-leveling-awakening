using NUnit.Framework;
using SLA.Combat;
using SLA.Data;
using UnityEngine;

namespace SLA.Tests
{
    public class DamageCalculatorTests
    {
        private CombatConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CombatConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        private float Hit(float speed, int strBonus = 0, HitZone zone = HitZone.Torso, bool crit = false,
            float armor = 0f, bool staggered = false, float baseDamage = 10f)
        {
            var input = new DamageInput(baseDamage, speed, strBonus, zone, crit, armor, staggered);
            return DamageCalculator.Calculate(input, _config);
        }

        [Test]
        public void CMB_4_FullSwingRustyDaggerTorsoAtStr10_DealsAbout12()
        {
            Assert.AreEqual(12f, Hit(10f), 0.001f);
        }

        [Test]
        public void CMB_4_StrengthAddsTwoPercentPerPointAboveBase()
        {
            Assert.AreEqual(12f * 1.1f, Hit(10f, strBonus: 5), 0.001f);
        }

        [Test]
        public void CMB_4_ArmorIsSubtractedAndFlooredAtZero()
        {
            Assert.AreEqual(7f, Hit(10f, armor: 5f), 0.001f);
            Assert.AreEqual(0f, Hit(10f, armor: 50f));
        }

        [Test]
        public void CMB_5_BelowMinimumSpeed_DealsNothing()
        {
            Assert.AreEqual(0f, Hit(1.49f));
        }

        [Test]
        public void CMB_5_SwingFactorIsLinearToOneAtFullSpeedAndCapped()
        {
            Assert.AreEqual(0f, DamageCalculator.SwingFactor(1.5f, _config), 0.001f);
            Assert.AreEqual(0.5f, DamageCalculator.SwingFactor(3.25f, _config), 0.001f);
            Assert.AreEqual(1f, DamageCalculator.SwingFactor(5f, _config), 0.001f);
            Assert.AreEqual(1.2f, DamageCalculator.SwingFactor(20f, _config), 0.001f);
        }

        [Test]
        public void CMB_6_ZoneMultipliers()
        {
            Assert.AreEqual(2f, DamageCalculator.ZoneMultiplier(HitZone.Head, _config));
            Assert.AreEqual(1f, DamageCalculator.ZoneMultiplier(HitZone.Torso, _config));
            Assert.AreEqual(0.6f, DamageCalculator.ZoneMultiplier(HitZone.Limb, _config));
            Assert.AreEqual(2.5f, DamageCalculator.ZoneMultiplier(HitZone.WeakPoint, _config));
        }

        [Test]
        public void CMB_7_CriticalMultipliesByOneAndAHalf()
        {
            Assert.AreEqual(18f, Hit(10f, crit: true), 0.001f);
        }

        [Test]
        public void CMB_8_StaggeredTargetsIgnoreArmor()
        {
            Assert.AreEqual(12f, Hit(10f, armor: 5f, staggered: true), 0.001f);
        }

        [Test]
        public void CMB_14_ParryWindowGrowsFourMsPerPerceptionPoint()
        {
            Assert.AreEqual(0.15f, DamageCalculator.ParryWindowSeconds(0, _config), 0.0001f);
            Assert.AreEqual(0.19f, DamageCalculator.ParryWindowSeconds(10, _config), 0.0001f);
            Assert.AreEqual(0.15f, DamageCalculator.ParryWindowSeconds(-3, _config), 0.0001f);
        }
    }
}
