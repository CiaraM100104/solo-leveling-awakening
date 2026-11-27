using UnityEngine;

namespace SLA.Gates
{
    /// <summary>
    /// Where the player stands when a content scene loads. The XR Origin lives in Bootstrap,
    /// so each gate moves it here (position and facing) instead of spawning a new rig.
    /// Also the respawn point after death (GTE-4).
    /// </summary>
    public class PlayerSpawn : MonoBehaviour
    {
        /// <summary>The spawn in the loaded content scene, if any.</summary>
        public static PlayerSpawn Current { get; private set; }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Start() => MovePlayerHere();

        /// <summary>Puts the rig on this spawn. Fade first if the player can see it happen (CMF-1).</summary>
        public void MovePlayerHere()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                return;
            }

            var cc = player.GetComponent<CharacterController>();
            bool hadController = cc != null && cc.enabled;
            if (hadController) cc.enabled = false; // CharacterController fights teleports otherwise

            player.transform.SetPositionAndRotation(
                transform.position,
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

            if (hadController) cc.enabled = true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.3f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + transform.forward * 0.6f);
        }
    }
}
