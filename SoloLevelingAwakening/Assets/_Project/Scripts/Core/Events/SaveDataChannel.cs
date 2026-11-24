using UnityEngine;

namespace SLA.Core
{
    /// <summary>Carries a SaveData to fill (collect) or restore from (apply).</summary>
    [CreateAssetMenu(menuName = "SLA/Events/Save Data Channel")]
    public class SaveDataChannel : EventChannel<SaveData>
    {
    }
}
