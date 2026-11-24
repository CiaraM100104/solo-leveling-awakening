using SLA.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SLA.Items
{
    /// <summary>
    /// ITM-1 / ITM-4: a potion rides on the chest; grab it and raise it to your mouth to drink.
    /// The bottle shows only while the inventory has one, and snaps back to the chest when let go or drunk.
    /// </summary>
    public class PotionHolster : MonoBehaviour
    {
        [SerializeField] private string _itemId = "health_potion";
        [SerializeField] private Transform _head;
        [SerializeField] private XRGrabInteractable _bottle;

        [Header("Placement (PLACEHOLDERS until height calibration, CMF-13)")]
        [Tooltip("From the head: right, down (negative up) and forward, turning with the head's yaw only.")]
        [SerializeField] private Vector3 _chestOffset = new Vector3(0.12f, -0.38f, 0.16f);
        [Tooltip("The mouth, from the head, in the head's own frame.")]
        [SerializeField] private Vector3 _mouthOffset = new Vector3(0f, -0.08f, 0.06f);
        [SerializeField] private float _drinkRadiusMeters = 0.12f;

        [Header("Channels")]
        [SerializeField] private InventoryStatusChannel _inventory;
        [SerializeField] private StringEventChannel _useRequested;

        private bool _hasPotion;

        private void OnEnable()
        {
            _inventory.Raised += OnInventory;
            _bottle.selectExited.AddListener(OnReleased);
            if (_inventory.HasValue)
            {
                OnInventory(_inventory.Last);
            }
        }

        private void OnDisable()
        {
            _inventory.Raised -= OnInventory;
            _bottle.selectExited.RemoveListener(OnReleased);
        }

        private void OnInventory(InventoryStatus status)
        {
            _hasPotion = status.Count(_itemId) > 0;
            if (!_hasPotion && _bottle.isSelected)
            {
                Release();
            }
            _bottle.gameObject.SetActive(_hasPotion);
        }

        private void LateUpdate()
        {
            if (!_hasPotion)
            {
                return;
            }
            if (!_bottle.isSelected)
            {
                SnapToChest();
                return;
            }

            Vector3 mouth = _head.TransformPoint(_mouthOffset);
            if ((_bottle.transform.position - mouth).sqrMagnitude <= _drinkRadiusMeters * _drinkRadiusMeters)
            {
                Release();
                _useRequested.Raise(_itemId);
            }
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            SnapToChest();
        }

        private void Release()
        {
            if (_bottle.interactionManager != null)
            {
                _bottle.interactionManager.CancelInteractableSelection((IXRSelectInteractable)_bottle);
            }
            SnapToChest();
        }

        private void SnapToChest()
        {
            Vector3 forward = _head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            var yaw = Quaternion.LookRotation(forward.normalized);
            _bottle.transform.SetPositionAndRotation(_head.position + yaw * _chestOffset, yaw);
        }
    }
}
