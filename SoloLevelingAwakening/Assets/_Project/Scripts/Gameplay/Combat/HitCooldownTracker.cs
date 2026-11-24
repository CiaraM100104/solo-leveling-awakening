using System.Collections.Generic;

namespace SLA.Combat
{
    /// <summary>CMB-10: one weapon damages the same target at most once per cooldown.</summary>
    public sealed class HitCooldownTracker
    {
        private readonly Dictionary<int, float> _lastHitSeconds = new Dictionary<int, float>();

        public bool CanHit(int targetId, float nowSeconds, float cooldownSeconds)
        {
            return !_lastHitSeconds.TryGetValue(targetId, out float last) || nowSeconds - last >= cooldownSeconds;
        }

        public void RegisterHit(int targetId, float nowSeconds)
        {
            _lastHitSeconds[targetId] = nowSeconds;
        }

        public void Clear()
        {
            _lastHitSeconds.Clear();
        }
    }
}
