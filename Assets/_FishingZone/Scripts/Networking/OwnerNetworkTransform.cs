using Unity.Netcode.Components;

namespace FishingZone.Networking
{
    /// <summary>
    /// A NetworkTransform written by the object's owner rather than by the server.
    ///
    /// Used for the player because the Technical Specification gives each player authority over
    /// their own character input, camera and movement while the host keeps authority over shared
    /// outcomes. Waiting for a server round trip before a player sees their own step would make
    /// walking feel unresponsive, and nothing about a player's position needs defending in a
    /// co-op game.
    ///
    /// The boat deliberately does NOT use this: its movement is host-authoritative.
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
