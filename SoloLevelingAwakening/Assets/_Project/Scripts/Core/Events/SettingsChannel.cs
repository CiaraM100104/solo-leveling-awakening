using UnityEngine;

namespace SLA.Core
{
    /// <summary>Comfort and UI settings, for the pause menu.</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Settings Channel")]
    public class SettingsChannel : EventChannel<SettingsStatus>
    {
    }
}
