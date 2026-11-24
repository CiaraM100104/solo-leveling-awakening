using UnityEngine;

namespace SLA.Core
{
    /// <summary>A request to move the player to another place, behind a fade (MRH-8, GTE-4).</summary>
    public readonly struct TravelRequest
    {
        /// <summary>Where the player's feet go, facing this transform's forward.</summary>
        public readonly Transform Destination;
        /// <summary>Which area the destination is in (hub, a gate).</summary>
        public readonly int AreaId;
        /// <summary>True when respawning after death (GTE-4).</summary>
        public readonly bool IsRespawn;

        public TravelRequest(Transform destination, int areaId, bool isRespawn)
        {
            Destination = destination;
            AreaId = areaId;
            IsRespawn = isRespawn;
        }
    }

    /// <summary>Carries <see cref="TravelRequest"/>s: one asset for requests, one for arrivals.</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Travel Request Channel")]
    public class TravelRequestChannel : EventChannel<TravelRequest>
    {
    }
}
