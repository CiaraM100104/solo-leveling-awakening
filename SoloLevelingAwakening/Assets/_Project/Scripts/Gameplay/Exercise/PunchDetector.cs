using UnityEngine;

namespace SLA.Exercise
{
    /// <summary>Pure punch checks for the daily quest's target pads (QST-13).</summary>
    public static class PunchDetector
    {
        /// <summary>
        /// QST-13: a hit counts at the minimum speed or faster, moving away from the headset.
        /// </summary>
        public static bool IsValidPunch(Vector3 handVelocity, Vector3 handPosition, Vector3 headPosition, float minSpeed)
        {
            if (handVelocity.sqrMagnitude < minSpeed * minSpeed)
            {
                return false;
            }
            return Vector3.Dot(handVelocity, handPosition - headPosition) > 0f;
        }

        /// <summary>True when the fist is inside the pad's radius.</summary>
        public static bool IsOnPad(Vector3 handPosition, Vector3 padCenter, float padRadius)
        {
            return (handPosition - padCenter).sqrMagnitude <= padRadius * padRadius;
        }
    }
}
