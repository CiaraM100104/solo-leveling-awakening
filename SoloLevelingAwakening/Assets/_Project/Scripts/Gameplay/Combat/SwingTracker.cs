using UnityEngine;

namespace SLA.Combat
{
    /// <summary>
    /// CMB-9: remembers recent weapon-tip positions so a hit only counts after a real swing.
    /// Uses the farthest distance from the current tip to any recent sample, so wrist-waggling
    /// back and forth in place never adds up to a swing. Fixed-size ring buffer: no allocations after construction.
    /// </summary>
    public sealed class SwingTracker
    {
        private readonly Vector3[] _positions;
        private readonly float[] _times;
        private int _next;
        private int _count;

        public SwingTracker(int capacity = 64)
        {
            _positions = new Vector3[capacity];
            _times = new float[capacity];
        }

        public void Record(float timeSeconds, Vector3 tipPosition)
        {
            _positions[_next] = tipPosition;
            _times[_next] = timeSeconds;
            _next = (_next + 1) % _positions.Length;
            if (_count < _positions.Length)
            {
                _count++;
            }
        }

        public void Clear()
        {
            _next = 0;
            _count = 0;
        }

        /// <summary>Farthest distance from <paramref name="currentTip"/> to any sample within the window.</summary>
        public float MaxDisplacement(float nowSeconds, float windowSeconds, Vector3 currentTip)
        {
            float maxSqr = 0f;
            for (int i = 0; i < _count; i++)
            {
                if (nowSeconds - _times[i] > windowSeconds)
                {
                    continue;
                }
                float sqr = (currentTip - _positions[i]).sqrMagnitude;
                if (sqr > maxSqr)
                {
                    maxSqr = sqr;
                }
            }
            return Mathf.Sqrt(maxSqr);
        }

        /// <summary>True when the tip has travelled at least <paramref name="minArcMeters"/> within the window.</summary>
        public bool HasSwung(float nowSeconds, float windowSeconds, float minArcMeters, Vector3 currentTip)
        {
            return MaxDisplacement(nowSeconds, windowSeconds, currentTip) >= minArcMeters;
        }
    }
}
