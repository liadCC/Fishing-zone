using System;
using FishingZone.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FishingZone.Player
{
    /// <summary>
    /// Finds what the player is looking at and interacts with it on request.
    /// It only ever talks to <see cref="IInteractable"/>, so it needs no knowledge of NPCs,
    /// fishing stations, the boat wheel, shops or doors (Technical Specification section 16).
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField]
        private InputActionReference _interactAction;

        /// <summary>
        /// Transform whose forward defines the view ray. In first person this is the camera anchor,
        /// so the ray follows pitch as well as yaw.
        /// </summary>
        [SerializeField]
        private Transform _viewSource;

        [SerializeField]
        private float _range = 3f;

        [SerializeField]
        private LayerMask _interactableLayers;

        /// <summary>Raised when the looked-at interactable changes, including to null. Null means nothing in range.</summary>
        public event Action<IInteractable> FocusChanged;

        public IInteractable CurrentTarget { get; private set; }

        /// <summary>
        /// While set, this is the target regardless of where the player is looking.
        /// A station uses it so that an occupied seat cannot lose the key that releases it.
        /// </summary>
        private IInteractable _capturedTarget;

        private bool _isConfigured;

        private void Awake()
        {
            _isConfigured = _interactAction != null && _viewSource != null;
            if (!_isConfigured)
            {
                GameLog.Error(LogCategory.UI, "PlayerInteraction is missing an Interact action reference or a View Source. Assign both in the Inspector.");
            }
        }

        private void OnDisable()
        {
            _capturedTarget = null;
            SetTarget(null);
        }

        /// <summary>
        /// Forces the focus onto <paramref name="target"/> until it is released, bypassing the ray.
        /// The change is always announced even if the target is unchanged, because a captured
        /// interactable usually wants to show different prompt text once it owns the focus.
        /// </summary>
        public void CaptureFocus(IInteractable target)
        {
            if (target == null)
            {
                return;
            }

            _capturedTarget = target;
            CurrentTarget = target;
            FocusChanged?.Invoke(target);
        }

        /// <summary>Releases a capture. Ignored if something else holds it.</summary>
        public void ReleaseFocus(IInteractable target)
        {
            if (!ReferenceEquals(_capturedTarget, target))
            {
                return;
            }

            _capturedTarget = null;
            CurrentTarget = null;
            FocusChanged?.Invoke(null);
        }

        private void Update()
        {
            if (!_isConfigured)
            {
                return;
            }

            // A capture skips the raycast entirely, so nothing on deck can compete for the key.
            SetTarget(_capturedTarget ?? FindTarget());

            if (CurrentTarget != null && _interactAction.action.WasPressedThisFrame())
            {
                CurrentTarget.Interact(gameObject);
            }
        }

        private IInteractable FindTarget()
        {
            if (!Physics.Raycast(_viewSource.position, _viewSource.forward, out RaycastHit hit, _range, _interactableLayers))
            {
                return null;
            }

            // Searched from the parent chain so a station or boat can carry the component on its root
            // while the collider the player actually looks at is a child mesh.
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(gameObject))
            {
                return null;
            }

            return interactable;
        }

        private void SetTarget(IInteractable target)
        {
            if (ReferenceEquals(CurrentTarget, target))
            {
                return;
            }

            CurrentTarget = target;
            FocusChanged?.Invoke(target);
        }
    }
}
