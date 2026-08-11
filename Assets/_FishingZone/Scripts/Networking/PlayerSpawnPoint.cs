using System.Collections.Generic;
using UnityEngine;

namespace FishingZone.Networking
{
    /// <summary>
    /// Marks a place in a gameplay scene where a player may appear.
    ///
    /// Without these, players are placed around world origin, which is solid ground in the port but
    /// open water in an expedition map: the crew spawns over the sea and falls out of the level.
    ///
    /// Points register themselves as they load, so the spawner never has to search the scene.
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        private static readonly List<PlayerSpawnPoint> Registered = new List<PlayerSpawnPoint>();

        public static IReadOnlyList<PlayerSpawnPoint> All => Registered;

        // The list is static, so it outlives a play session when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Registered.Clear();
        }

        private void OnEnable()
        {
            if (!Registered.Contains(this))
            {
                Registered.Add(this);
            }
        }

        private void OnDisable()
        {
            Registered.Remove(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawRay(transform.position, transform.forward);
        }
    }
}
