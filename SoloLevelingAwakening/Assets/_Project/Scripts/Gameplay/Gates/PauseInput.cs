using SLA.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SLA.Gates
{
    /// <summary>The Menu button (left controller) pauses and resumes (GDD input table: "Pause and settings").</summary>
    public class PauseInput : MonoBehaviour
    {
        [SerializeField] private VoidEventChannel _pauseToggle;

        private InputAction _menu;

        private void Awake()
        {
            _menu = new InputAction("Pause", InputActionType.Button, "<XRController>{LeftHand}/{MenuButton}");
#if UNITY_EDITOR
            _menu.AddBinding("<Keyboard>/p"); // Desk testing; the XR Device Simulator maps Menu to M.
#endif
        }

        private void OnEnable()
        {
            _menu.performed += OnMenu;
            _menu.Enable();
        }

        private void OnDisable()
        {
            _menu.performed -= OnMenu;
            _menu.Disable();
        }

        private void OnDestroy()
        {
            _menu.Dispose();
        }

        private void OnMenu(InputAction.CallbackContext context)
        {
            _pauseToggle.Raise();
        }
    }
}
