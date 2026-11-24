using UnityEngine;

namespace SLA.Combat
{
    /// <summary>Something whose attack can be parried (CMB-14).</summary>
    public interface IParryable
    {
        /// <summary>Tries a parry with the player's weapon hitting at <paramref name="relativeSpeed"/> m/s.</summary>
        bool TryParry(float relativeSpeed, Vector3 contactPoint);
    }

    /// <summary>
    /// Marks an enemy weapon's collider (layer EnemyWeapon). Player weapons that hit it try a parry
    /// instead of dealing damage (CMB-14). The collider is solid so blades never pass through it (CMB-1).
    /// </summary>
    public class EnemyWeaponHitbox : MonoBehaviour
    {
        public IParryable Owner { get; private set; }

        private void Awake()
        {
            Owner = GetComponentInParent<IParryable>();
        }
    }
}
