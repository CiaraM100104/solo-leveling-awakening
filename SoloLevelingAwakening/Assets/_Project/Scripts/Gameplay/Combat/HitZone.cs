namespace SLA.Combat
{
    /// <summary>Where a hit landed (CMB-6). Colliders without a <see cref="HitZoneMarker"/> count as torso.</summary>
    public enum HitZone
    {
        Torso,
        Head,
        Limb,
        WeakPoint,
    }
}
