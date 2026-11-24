using UnityEngine;

namespace SLA.Core
{
    /// <summary>Carries the current GameState.</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Game State Channel")]
    public class GameStateChannel : EventChannel<GameState>
    {
    }
}
