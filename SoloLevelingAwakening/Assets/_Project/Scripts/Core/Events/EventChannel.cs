using System;
using UnityEngine;

namespace SLA.Core
{
    /// <summary>
    /// Base for event channels that carry a value (docs/ARCHITECTURE.md#event-channels).
    /// Remembers the last value so listeners that enable late can read it.
    /// </summary>
    public abstract class EventChannel<T> : ScriptableObject
    {
        /// <summary>Raised with each new value.</summary>
        public event Action<T> Raised;

        /// <summary>The last value raised. Only valid when <see cref="HasValue"/> is true.</summary>
        public T Last { get; private set; }

        /// <summary>True once a value has been raised this play session.</summary>
        public bool HasValue { get; private set; }

        /// <summary>Stores and broadcasts <paramref name="value"/>.</summary>
        public void Raise(T value)
        {
            Last = value;
            HasValue = true;
            Raised?.Invoke(value);
        }

        /// <summary>ScriptableObjects survive between play sessions in the editor; start each one empty.</summary>
        protected virtual void OnEnable()
        {
            Last = default;
            HasValue = false;
        }
    }
}
