using System;
using SLA.Core;
using UnityEngine;

namespace SLA.Gates
{
    /// <summary>
    /// Sets the ambient light for the area the player is in: warm in the hub, dark blue inside gates
    /// (art direction: dark, high contrast, blue and purple magic). Goes away once each area is its own scene.
    /// </summary>
    public class AreaAmbience : MonoBehaviour
    {
        [Serializable]
        private class AreaLook
        {
            public int areaId;
            public Color ambient = Color.gray;
        }

        [SerializeField] private int _startAreaId;
        [SerializeField] private AreaLook[] _looks = new AreaLook[0];
        [SerializeField] private TravelRequestChannel _arrived;

        private void OnEnable()
        {
            _arrived.Raised += OnArrived;
        }

        private void OnDisable()
        {
            _arrived.Raised -= OnArrived;
        }

        private void Start()
        {
            Apply(_startAreaId);
        }

        private void OnArrived(TravelRequest request)
        {
            Apply(request.AreaId);
        }

        private void Apply(int areaId)
        {
            foreach (var look in _looks)
            {
                if (look.areaId == areaId)
                {
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = look.ambient;
                    return;
                }
            }
        }
    }
}
