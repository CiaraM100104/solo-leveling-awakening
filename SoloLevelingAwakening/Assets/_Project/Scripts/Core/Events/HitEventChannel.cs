using UnityEngine;

namespace SLA.Core
{
    /// <summary>What a <see cref="HitEvent"/> shows.</summary>
    public enum HitKind
    {
        Damage,
        Parry,
        Block,
        Gold,
    }

    /// <summary>A world-space combat or loot popup: damage numbers (UI-7), parries, blocks, gold.</summary>
    public readonly struct HitEvent
    {
        public readonly Vector3 Position;
        public readonly int Amount;
        public readonly HitKind Kind;

        public HitEvent(Vector3 position, int amount, HitKind kind)
        {
            Position = position;
            Amount = amount;
            Kind = kind;
        }
    }

    /// <summary>Carries <see cref="HitEvent"/>s to the damage-number display.</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Hit Event Channel")]
    public class HitEventChannel : EventChannel<HitEvent>
    {
    }
}
