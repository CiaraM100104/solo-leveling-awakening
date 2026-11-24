using System;
using SLA.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SLA.Gates
{
    /// <summary>
    /// Puts the player's weapons on the rack of whatever area they arrive in. A held physics weapon can't
    /// follow a jump across the map, so it is let go at full black and laid on the rack.
    /// Stand-in until hip holsters (ITM-1) exist.
    /// </summary>
    public class WeaponStowing : MonoBehaviour
    {
        [Serializable]
        private class Rack
        {
            public int areaId;
            public Transform[] slots = new Transform[0];
        }

        [SerializeField] private XRGrabInteractable[] _weapons = new XRGrabInteractable[0];
        [SerializeField] private Rack[] _racks = new Rack[0];

        [Header("Channels")]
        [SerializeField] private VoidEventChannel _travelStarting;
        [SerializeField] private TravelRequestChannel _arrived;

        private void OnEnable()
        {
            _travelStarting.Raised += LetGo;
            _arrived.Raised += Stow;
        }

        private void OnDisable()
        {
            _travelStarting.Raised -= LetGo;
            _arrived.Raised -= Stow;
        }

        private void LetGo()
        {
            foreach (var weapon in _weapons)
            {
                if (weapon.isSelected && weapon.interactionManager != null)
                {
                    weapon.interactionManager.CancelInteractableSelection((IXRSelectInteractable)weapon);
                }
            }
        }

        private void Stow(TravelRequest request)
        {
            foreach (var rack in _racks)
            {
                if (rack.areaId != request.AreaId)
                {
                    continue;
                }
                for (int i = 0; i < _weapons.Length && i < rack.slots.Length; i++)
                {
                    var body = _weapons[i].GetComponent<Rigidbody>();
                    var slot = rack.slots[i];
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.position = slot.position;
                    body.rotation = slot.rotation;
                    _weapons[i].transform.SetPositionAndRotation(slot.position, slot.rotation);
                }
                return;
            }
        }
    }
}
