namespace FishingZone.Core
{
    /// <summary>
    /// The high-level states the game can be in. Each state (except Boot) owns exactly one scene.
    /// The intended flow is Main Menu -> Lobby -> Port -> Expedition -> Port.
    /// </summary>
    public enum GameState
    {
        /// <summary>Bootstrap scene only. Never loaded again after startup.</summary>
        Boot,
        MainMenu,
        Lobby,
        Port,
        Expedition
    }
}
