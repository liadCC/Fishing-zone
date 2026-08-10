using FishingZone.Core;
using Unity.Netcode;
using UnityEngine;

namespace FishingZone.Networking
{
    /// <summary>
    /// Owns starting and stopping the network session.
    ///
    /// Everything runs inside a session, including solo play, which is a host of one. Keeping a
    /// single path means gameplay never has to ask whether it is networked, and it removes a whole
    /// second configuration that would otherwise need testing and would quietly rot.
    ///
    /// Buttons and menus call these methods rather than touching NetworkManager, so there is one
    /// place to change when the session gains a relay and a join code.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public bool IsSessionActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        /// <summary>Starts hosting. Solo play uses this too; the crew simply has one member.</summary>
        public bool StartHost()
        {
            if (!TryGetNetworkManager(out NetworkManager networkManager))
            {
                return false;
            }

            if (IsSessionActive)
            {
                GameLog.Warn(LogCategory.Network, "Ignored StartHost: a session is already running.");
                return false;
            }

            if (!networkManager.StartHost())
            {
                GameLog.Error(LogCategory.Network, "Failed to start host. Check the transport settings on NetworkManager.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Joins a host using the address already configured on the transport. Connecting by crew
        /// code arrives later; this is the same lifecycle either way.
        /// </summary>
        public bool StartClient()
        {
            if (!TryGetNetworkManager(out NetworkManager networkManager))
            {
                return false;
            }

            if (IsSessionActive)
            {
                GameLog.Warn(LogCategory.Network, "Ignored StartClient: a session is already running.");
                return false;
            }

            if (!networkManager.StartClient())
            {
                GameLog.Error(LogCategory.Network, "Failed to start client. Check the transport settings on NetworkManager.");
                return false;
            }

            return true;
        }

        public void Shutdown()
        {
            if (!IsSessionActive)
            {
                return;
            }

            NetworkManager.Singleton.Shutdown();
            GameLog.Info(LogCategory.Network, "Session shut down.");
        }

        private static bool TryGetNetworkManager(out NetworkManager networkManager)
        {
            networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                return true;
            }

            GameLog.Error(LogCategory.Network, "No NetworkManager in the scene. Add one to the persistent services object.");
            return false;
        }
    }
}
