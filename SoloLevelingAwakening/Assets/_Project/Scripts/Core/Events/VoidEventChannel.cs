using System;
using UnityEngine;

namespace SLA.Core
{
    /// <summary>
    /// An event with no payload that any system can raise or listen to (docs/ARCHITECTURE.md#event-channels).
    /// </summary>
    [CreateAssetMenu(menuName = "SLA/Events/Void Event Channel")]
    public class VoidEventChannel : ScriptableObject
    {
        /// <summary>Raised when <see cref="Raise"/> is called.</summary>
        public event Action Raised;

        /// <summary>Notifies every listener.</summary>
        public void Raise() => Raised?.Invoke();
    }
}
