using UnityEngine;

namespace SLA.Combat
{
    /// <summary>Tags a collider with its damage zone (CMB-6).</summary>
    public class HitZoneMarker : MonoBehaviour
    {
        [SerializeField] private HitZone _zone = HitZone.Torso;

        public HitZone Zone => _zone;
    }
}
