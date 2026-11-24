using NUnit.Framework;
using SLA.Combat;
using UnityEngine;

namespace SLA.Tests
{
    public class SwingAndCooldownTests
    {
        private const float Window = 0.25f;
        private const float MinArc = 0.3f;
        private const float Step = 1f / 72f;

        [Test]
        public void CMB_9_RealSwingCounts()
        {
            var tracker = new SwingTracker();
            float t = 0f;
            for (int i = 0; i < 10; i++)
            {
                tracker.Record(t, new Vector3(i * 0.05f, 0f, 0f));
                t += Step;
            }
            Assert.IsTrue(tracker.HasSwung(t, Window, MinArc, new Vector3(0.5f, 0f, 0f)));
        }

        [Test]
        public void CMB_9_WristWaggleDoesNotCount()
        {
            var tracker = new SwingTracker();
            float t = 0f;
            for (int i = 0; i < 40; i++)
            {
                float x = (i % 2 == 0) ? 0f : 0.12f;
                tracker.Record(t, new Vector3(x, 0f, 0f));
                t += Step;
            }
            Assert.IsFalse(tracker.HasSwung(t, Window, MinArc, Vector3.zero));
        }

        [Test]
        public void CMB_9_OldMovementOutsideWindowDoesNotCount()
        {
            var tracker = new SwingTracker();
            tracker.Record(0f, Vector3.zero);
            tracker.Record(0.5f, new Vector3(1f, 0f, 0f));
            Assert.IsFalse(tracker.HasSwung(0.6f, Window, MinArc, new Vector3(1f, 0f, 0f)));
        }

        [Test]
        public void CMB_10_SameTargetOncePerCooldown()
        {
            var cooldown = new HitCooldownTracker();
            Assert.IsTrue(cooldown.CanHit(1, 0f, Window));
            cooldown.RegisterHit(1, 0f);
            Assert.IsFalse(cooldown.CanHit(1, 0.1f, Window));
            Assert.IsTrue(cooldown.CanHit(2, 0.1f, Window), "Other targets are not blocked");
            Assert.IsTrue(cooldown.CanHit(1, 0.25f, Window));
        }
    }
}
