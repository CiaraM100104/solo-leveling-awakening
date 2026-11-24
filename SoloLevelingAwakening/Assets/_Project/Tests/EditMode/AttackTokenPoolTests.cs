using NUnit.Framework;
using SLA.AI;

namespace SLA.Tests
{
    public class AttackTokenPoolTests
    {
        [Test]
        public void ENM_2_AtMostTwoEnemiesHoldTokens()
        {
            var pool = new AttackTokenPool(2);
            Assert.IsTrue(pool.TryAcquire(1));
            Assert.IsTrue(pool.TryAcquire(2));
            Assert.IsFalse(pool.TryAcquire(3));
            Assert.AreEqual(2, pool.InUse);
        }

        [Test]
        public void ENM_2_ReleasedTokenCanBeTakenByAnother()
        {
            var pool = new AttackTokenPool(2);
            pool.TryAcquire(1);
            pool.TryAcquire(2);
            pool.Release(1);
            Assert.IsTrue(pool.TryAcquire(3));
            Assert.IsFalse(pool.Holds(1));
        }

        [Test]
        public void ENM_2_AcquiringTwiceDoesNotUseASecondToken()
        {
            var pool = new AttackTokenPool(2);
            pool.TryAcquire(1);
            Assert.IsTrue(pool.TryAcquire(1));
            Assert.AreEqual(1, pool.InUse);
        }
    }
}
