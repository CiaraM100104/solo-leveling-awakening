using System.Collections;
using SLA.Core;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace SLA.Gates
{
    /// <summary>
    /// Moves the player between the hub and gates (MRH-8) and back to a gate's entrance on death (GTE-4):
    /// fade to black, move the XR Origin so the head lands on the destination facing its forward, fade back.
    /// </summary>
    public class PlayerTravel : MonoBehaviour
    {
        [SerializeField] private XROrigin _origin;
        [SerializeField] private CharacterController _body;
        [SerializeField] private ScreenFade _fade;
        [Tooltip("PLACEHOLDER: CMF asks for a brief fade.")]
        [SerializeField] private float _fadeSeconds = 0.25f;
        [SerializeField] private float _blackHoldSeconds = 0.15f;

        [Header("Channels")]
        [SerializeField] private TravelRequestChannel _requests;
        [SerializeField] private TravelRequestChannel _arrived;
        [Tooltip("Raised at full black before the move, so held items can be let go.")]
        [SerializeField] private VoidEventChannel _travelStarting;

        private bool _busy;

        private void OnEnable()
        {
            _requests.Raised += OnRequest;
        }

        private void OnDisable()
        {
            _requests.Raised -= OnRequest;
        }

        private void OnRequest(TravelRequest request)
        {
            if (!_busy)
            {
                StartCoroutine(Travel(request));
            }
        }

        private IEnumerator Travel(TravelRequest request)
        {
            _busy = true;
            yield return _fade.FadeTo(1f, _fadeSeconds);
            _travelStarting.Raise();
            yield return new WaitForFixedUpdate();

            MoveTo(request.Destination);
            yield return new WaitForSeconds(_blackHoldSeconds);
            _arrived.Raise(request);

            yield return _fade.FadeTo(0f, _fadeSeconds);
            _busy = false;
        }

        private void MoveTo(Transform destination)
        {
            bool bodyWasEnabled = _body != null && _body.enabled;
            if (bodyWasEnabled)
            {
                _body.enabled = false;
            }

            Vector3 forward = destination.forward;
            forward.y = 0f;
            _origin.MatchOriginUpCameraForward(Vector3.up, forward.normalized);

            float headHeight = _origin.Camera.transform.position.y - _origin.transform.position.y;
            Vector3 floor = destination.position;
            _origin.MoveCameraToWorldLocation(new Vector3(floor.x, floor.y + headHeight, floor.z));
            Physics.SyncTransforms();

            if (bodyWasEnabled)
            {
                _body.enabled = true;
            }
        }
    }
}
