using UnityEngine;

namespace SLA.Exercise
{
    /// <summary>
    /// Pure squat detection from the headset (QST-8 to QST-11). Calibrate standing head height first,
    /// then feed one sample per frame to <see cref="Tick"/>. No allocations after construction.
    /// </summary>
    public sealed class SquatCounter
    {
        private enum Phase
        {
            Standing,
            Descending,
            Down,
        }

        private readonly float _downFraction;
        private readonly float _upFraction;
        private readonly float _maxPitchDownDegrees;
        private readonly float _maxDriftMeters;
        private readonly float _minRepSeconds;

        private float _calibrationSum;
        private int _calibrationSamples;
        private Vector2 _startSpot;
        private Phase _phase;
        private float _leftStandingAt;
        private bool _rejected;

        public SquatCounter(float downFraction, float upFraction, float maxPitchDownDegrees, float maxDriftMeters,
            float minRepSeconds)
        {
            _downFraction = downFraction;
            _upFraction = upFraction;
            _maxPitchDownDegrees = maxPitchDownDegrees;
            _maxDriftMeters = maxDriftMeters;
            _minRepSeconds = minRepSeconds;
        }

        /// <summary>QST-8: standing head height H₀ above the floor.</summary>
        public float StandingHeight { get; private set; }

        public bool IsCalibrated => StandingHeight > 0f;

        public void BeginCalibration()
        {
            _calibrationSum = 0f;
            _calibrationSamples = 0;
            StandingHeight = 0f;
        }

        public void AddCalibrationSample(float headHeight)
        {
            _calibrationSum += headHeight;
            _calibrationSamples++;
        }

        /// <summary>Finishes calibration; <paramref name="startSpot"/> is the head's floor position (x, z).</summary>
        public void EndCalibration(Vector2 startSpot)
        {
            StandingHeight = _calibrationSamples > 0 ? _calibrationSum / _calibrationSamples : 0f;
            _startSpot = startSpot;
            _phase = Phase.Standing;
        }

        /// <summary>
        /// One frame of head data. Returns true when a valid rep finishes.
        /// </summary>
        /// <param name="headSpot">Head position on the floor plane (x, z).</param>
        /// <param name="pitchDownDegrees">How far the head is tilted down; negative looks up.</param>
        public bool Tick(float timeSeconds, float headHeight, Vector2 headSpot, float pitchDownDegrees)
        {
            if (!IsCalibrated)
            {
                return false;
            }

            float downLine = StandingHeight * _downFraction;
            float upLine = StandingHeight * _upFraction;

            if (_phase != Phase.Standing)
            {
                // QST-10: bending over to look down, or walking away, spoils the rep.
                if (pitchDownDegrees > _maxPitchDownDegrees || (headSpot - _startSpot).magnitude > _maxDriftMeters)
                {
                    _rejected = true;
                }
            }

            switch (_phase)
            {
                case Phase.Standing:
                    if (headHeight < upLine)
                    {
                        _phase = Phase.Descending;
                        _leftStandingAt = timeSeconds;
                        _rejected = pitchDownDegrees > _maxPitchDownDegrees;
                    }
                    break;

                case Phase.Descending:
                    if (headHeight < downLine)
                    {
                        _phase = Phase.Down;
                    }
                    else if (headHeight > upLine)
                    {
                        _phase = Phase.Standing; // A partial dip is not a rep.
                    }
                    break;

                case Phase.Down:
                    if (headHeight > upLine)
                    {
                        _phase = Phase.Standing;
                        // QST-11: a rep takes at least the minimum time from leaving the standing zone.
                        return !_rejected && timeSeconds - _leftStandingAt >= _minRepSeconds;
                    }
                    break;
            }
            return false;
        }

        /// <summary>Degrees the head is tilted down, from its forward vector.</summary>
        public static float PitchDownDegrees(Vector3 headForward)
        {
            return Mathf.Asin(Mathf.Clamp(-headForward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }
    }
}
