using UnityEngine;

namespace SLA.Core
{
    /// <summary>Carries the latest <see cref="PlayerStatus"/> for the UI and for systems that read player stats.</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Player Status Channel")]
    public class PlayerStatusChannel : EventChannel<PlayerStatus>
    {
    }
}
