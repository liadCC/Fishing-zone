using UnityEngine;

namespace FishingZone.Core
{
    /// <summary>
    /// Implemented by anything a player can interact with: NPCs, fishing stations, the boat wheel,
    /// shops, doors, treasure. Player interaction code talks to this and never to the concrete type.
    /// The interactor is passed in so implementations can gate on role or equipment later
    /// without this interface needing to know what a player is.
    /// </summary>
    public interface IInteractable
    {
        bool CanInteract(GameObject interactor);

        void Interact(GameObject interactor);

        /// <summary>Prompt text shown to the interactor, for example "Take the wheel".</summary>
        string GetInteractionText(GameObject interactor);
    }
}
